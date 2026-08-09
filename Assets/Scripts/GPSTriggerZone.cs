using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Checks distance from the player GPS position to the active PointOfInterest
/// (the one whose <see cref="PointOfInterest.order"/> matches HuntManager.CurrentIndex).
/// Uses enter/exit radius hysteresis so GPS drift near the boundary
/// does not spam spawn/despawn of the AR object.
/// On enter: spawns the AR coin. Collection happens only when the player
/// taps/clicks that coin (screen raycast from the AR camera).
/// </summary>
public class GPSTriggerZone : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GPSManager gpsManager;
    [SerializeField] private HuntManager huntManager;
    [SerializeField] private Camera arCamera;

    [Header("POIs")]
    [SerializeField] private PointOfInterest[] pointsOfInterest;

    [Header("Spawn")]
    [SerializeField] private float spawnDistanceInFrontOfCamera = 2f;

    [Header("Collection")]
    [Tooltip("Max raycast distance from the AR camera when tapping a coin.")]
    [SerializeField] private float tapRaycastDistance = 50f;
    [Tooltip("Scale multiplier at the peak of the collect punch (e.g. 1.15 = 115%).")]
    [SerializeField] private float collectPunchScale = 1.15f;
    [Tooltip("Duration of the scale-punch jitter in seconds.")]
    [SerializeField] private float collectPunchDuration = 0.3f;

    /// <summary>Fired after a POI is collected (collider disabled + punch played).</summary>
    public UnityEvent<PointOfInterest> OnPOICollected = new UnityEvent<PointOfInterest>();

    /// <summary>Shared POI list for HuntManager / UI to read without duplicating data.</summary>
    public PointOfInterest[] PointsOfInterest => pointsOfInterest;

    private const double EarthRadiusMeters = 6371000.0;
    private bool _isCollecting;

    private void Awake()
    {
        if (gpsManager == null)
        {
            gpsManager = FindFirstObjectByType<GPSManager>();
        }

        if (huntManager == null)
        {
            huntManager = FindFirstObjectByType<HuntManager>();
        }

        if (arCamera == null)
        {
            arCamera = Camera.main;
        }
    }

    private void Update()
    {
        if (gpsManager == null || !gpsManager.IsReady || pointsOfInterest == null)
        {
            return;
        }

        PointOfInterest poi = GetActivePOI();
        if (poi == null || poi.arObjectPrefab == null || poi.isCollected)
        {
            return;
        }

        if (!_isCollecting)
        {
            UpdateProximity(poi);
        }

        TryCollectFromTap(poi);
    }

    private void UpdateProximity(PointOfInterest poi)
    {
        double userLat = gpsManager.latitude;
        double userLon = gpsManager.longitude;

        float enterRadius = Mathf.Max(0.1f, poi.enterRadiusMeters);
        // Exit must be larger than enter — adds a dead-band against GPS jitter.
        float exitRadius = Mathf.Max(enterRadius + 1f, poi.exitRadiusMeters);

        float distance = HaversineDistanceMeters(userLat, userLon, poi.latitude, poi.longitude);

        if (!poi.isInside && distance <= enterRadius)
        {
            EnterZone(poi, distance);
        }
        else if (poi.isInside && distance > exitRadius)
        {
            ExitZone(poi, distance);
        }
        // While enterRadius < distance <= exitRadius: hold current state (no flicker).
    }

    /// <summary>
    /// POI whose <see cref="PointOfInterest.order"/> matches the hunt's current sequence index.
    /// </summary>
    private PointOfInterest GetActivePOI()
    {
        int currentIndex = huntManager != null ? huntManager.CurrentIndex : 0;

        for (int i = 0; i < pointsOfInterest.Length; i++)
        {
            PointOfInterest poi = pointsOfInterest[i];
            if (poi != null && poi.order == currentIndex)
            {
                return poi;
            }
        }

        return null;
    }

    private void EnterZone(PointOfInterest poi, float distance)
    {
        poi.isInside = true;
        SpawnArObject(poi);
        Debug.Log($"[GPSTriggerZone] ENTER '{poi.name}' at {distance:F1} m (enter ≤ {poi.enterRadiusMeters:F0} m). Tap the coin to collect.");
    }

    private void ExitZone(PointOfInterest poi, float distance)
    {
        poi.isInside = false;

        // Despawn uncollected coins so they can respawn on re-enter.
        // Collected coins are left alone.
        if (!poi.isCollected && poi.spawnedInstance != null)
        {
            Destroy(poi.spawnedInstance);
            poi.spawnedInstance = null;
        }

        Debug.Log($"[GPSTriggerZone] EXIT '{poi.name}' at {distance:F1} m (exit > {poi.exitRadiusMeters:F0} m).");
    }

    private void SpawnArObject(PointOfInterest poi)
    {
        if (arCamera == null)
        {
            Debug.LogError("[GPSTriggerZone] No AR camera assigned. Cannot spawn prefab.");
            return;
        }

        // Avoid duplicates if something already exists.
        if (poi.spawnedInstance != null)
        {
            return;
        }

        Vector3 spawnPos = arCamera.transform.position
                           + arCamera.transform.forward * spawnDistanceInFrontOfCamera;
        Quaternion spawnRot = Quaternion.LookRotation(arCamera.transform.forward, Vector3.up);
        poi.spawnedInstance = Instantiate(poi.arObjectPrefab, spawnPos, spawnRot);
    }

    private void TryCollectFromTap(PointOfInterest poi)
    {
        if (_isCollecting || poi == null || poi.isCollected || poi.spawnedInstance == null || arCamera == null)
        {
            return;
        }

        if (!TryGetTapScreenPosition(out Vector2 screenPos))
        {
            return;
        }

        // Ignore taps that land on UI (map / riddle buttons, etc.).
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Ray ray = arCamera.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit, tapRaycastDistance))
        {
            return;
        }

        if (!IsHitOnCoin(hit.collider, poi.spawnedInstance))
        {
            return;
        }

        StartCoroutine(CollectPOICoroutine(poi));
    }

    private static bool TryGetTapScreenPosition(out Vector2 screenPos)
    {
        screenPos = default;

        // Project is Input System–only (activeInputHandler = 1). Pointer covers touch + mouse.
        if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
        {
            screenPos = Pointer.current.position.ReadValue();
            return true;
        }

        return false;
    }

    private static bool IsHitOnCoin(Collider hitCollider, GameObject coin)
    {
        if (hitCollider == null || coin == null)
        {
            return false;
        }

        Transform hitTransform = hitCollider.transform;
        return hitTransform == coin.transform || hitTransform.IsChildOf(coin.transform);
    }

    private IEnumerator CollectPOICoroutine(PointOfInterest poi)
    {
        if (poi == null || poi.isCollected || poi.spawnedInstance == null)
        {
            yield break;
        }

        _isCollecting = true;
        poi.isCollected = true;

        GameObject coin = poi.spawnedInstance;

        Collider col = coin.GetComponentInChildren<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }

        yield return StartCoroutine(ScalePunchCoroutine(coin.transform, collectPunchScale, collectPunchDuration));

        OnPOICollected.Invoke(poi);
        _isCollecting = false;
    }

    private static IEnumerator ScalePunchCoroutine(Transform target, float peakScale, float duration)
    {
        if (target == null || duration <= 0f)
        {
            yield break;
        }

        Vector3 original = target.localScale;
        Vector3 peak = original * peakScale;
        float half = duration * 0.5f;

        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / half);
            target.localScale = Vector3.Lerp(original, peak, a);
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / half);
            target.localScale = Vector3.Lerp(peak, original, a);
            yield return null;
        }

        target.localScale = original;
    }

    /// <summary>
    /// Great-circle distance between two GPS coordinates in meters.
    /// </summary>
    public static float HaversineDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = DegreesToRadians(lat2 - lat1);
        double dLon = DegreesToRadians(lon2 - lon1);

        double a = Math.Sin(dLat * 0.5) * Math.Sin(dLat * 0.5)
                 + Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2))
                 * Math.Sin(dLon * 0.5) * Math.Sin(dLon * 0.5);

        double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return (float)(EarthRadiusMeters * c);
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * (Math.PI / 180.0);
    }
}

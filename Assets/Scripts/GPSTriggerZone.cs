using System;
using UnityEngine;

/// <summary>
/// Checks distance from the player GPS position to each PointOfInterest.
/// Uses enter/exit radius hysteresis so GPS drift near the boundary
/// does not spam spawn/despawn of the AR object.
/// </summary>
public class GPSTriggerZone : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GPSManager gpsManager;
    [SerializeField] private Camera arCamera;

    [Header("POIs")]
    [SerializeField] private PointOfInterest[] pointsOfInterest;

    [Header("Spawn")]
    [SerializeField] private float spawnDistanceInFrontOfCamera = 2f;

    private const double EarthRadiusMeters = 6371000.0;

    private void Awake()
    {
        if (gpsManager == null)
        {
            gpsManager = FindFirstObjectByType<GPSManager>();
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

        double userLat = gpsManager.latitude;
        double userLon = gpsManager.longitude;

        for (int i = 0; i < pointsOfInterest.Length; i++)
        {
            PointOfInterest poi = pointsOfInterest[i];
            if (poi == null || poi.arObjectPrefab == null)
            {
                continue;
            }

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
    }

    private void EnterZone(PointOfInterest poi, float distance)
    {
        poi.isInside = true;
        SpawnArObject(poi);
        Debug.Log($"[GPSTriggerZone] ENTER '{poi.name}' at {distance:F1} m (enter ≤ {poi.enterRadiusMeters:F0} m).");
    }

    private void ExitZone(PointOfInterest poi, float distance)
    {
        poi.isInside = false;

        if (poi.spawnedInstance != null)
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

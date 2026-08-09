using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reads device GPS and exposes a jitter-reduced latitude/longitude
/// by averaging the last N valid readings (one per second).
/// Rejects samples that are too inaccurate to reduce GPS drift.
/// </summary>
public class GPSManager : MonoBehaviour
{
    [Header("Status (read-only at runtime)")]
    public float latitude;
    public float longitude;
    public bool IsReady { get; private set; }

    [Header("Settings")]
    [SerializeField] private float desiredAccuracyMeters = 5f;
    [SerializeField] private float updateDistanceMeters = 1f;
    [SerializeField] private float maxWaitForServiceSeconds = 20f;
    [SerializeField] private int averageSampleCount = 5;
    [Tooltip("Ignore GPS samples worse than this accuracy (meters).")]
    [SerializeField] private float maxAcceptedAccuracyMeters = 25f;

    private readonly Queue<Vector2> _readings = new Queue<Vector2>();

    private void Start()
    {
        StartCoroutine(StartLocationService());
    }

    private IEnumerator StartLocationService()
    {
        if (!Input.location.isEnabledByUser)
        {
            Debug.LogError("[GPSManager] Location services are disabled on this device. Enable GPS in system settings.");
            yield break;
        }

        Input.location.Start(desiredAccuracyMeters, updateDistanceMeters);

        float timer = 0f;
        while (Input.location.status == LocationServiceStatus.Initializing && timer < maxWaitForServiceSeconds)
        {
            yield return new WaitForSeconds(1f);
            timer += 1f;
        }

        if (Input.location.status != LocationServiceStatus.Running)
        {
            Debug.LogError($"[GPSManager] Failed to start location service. Status: {Input.location.status}");
            yield break;
        }

        Debug.Log("[GPSManager] Location service started. Collecting averaged readings...");
        StartCoroutine(UpdateLocationLoop());
    }

    private IEnumerator UpdateLocationLoop()
    {
        while (Input.location.status == LocationServiceStatus.Running)
        {
            LocationInfo info = Input.location.lastData;

            // Skip bad/drifty samples — horizontalAccuracy is meters; lower is better.
            // Unity reports negative accuracy when the value is invalid.
            if (info.horizontalAccuracy < 0f || info.horizontalAccuracy > maxAcceptedAccuracyMeters)
            {
                Debug.LogWarning(
                    $"[GPSManager] Skipped inaccurate sample ({info.horizontalAccuracy:F1} m). " +
                    $"Need <= {maxAcceptedAccuracyMeters:F0} m.");
            }
            else
            {
                _readings.Enqueue(new Vector2(info.latitude, info.longitude));

                while (_readings.Count > averageSampleCount)
                {
                    _readings.Dequeue();
                }

                float sumLat = 0f;
                float sumLon = 0f;
                foreach (Vector2 reading in _readings)
                {
                    sumLat += reading.x;
                    sumLon += reading.y;
                }

                latitude = sumLat / _readings.Count;
                longitude = sumLon / _readings.Count;
                IsReady = _readings.Count >= averageSampleCount;
            }

            yield return new WaitForSeconds(1f);
        }
    }

    private void OnDisable()
    {
        if (Input.location.status == LocationServiceStatus.Running)
        {
            Input.location.Stop();
        }
    }
}

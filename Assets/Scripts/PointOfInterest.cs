using System;
using UnityEngine;

/// <summary>
/// Data for one scavenger-hunt GPS location and the AR prefab to spawn there.
/// Uses a smaller enter radius and larger exit radius (hysteresis) to resist GPS drift.
/// </summary>
[Serializable]
public class PointOfInterest
{
    public string name;
    public double latitude;
    public double longitude;

    [Tooltip("Distance at which the AR object appears.")]
    public float enterRadiusMeters = 10f;

    [Tooltip("Distance at which the AR object disappears. Must be larger than enter radius.")]
    public float exitRadiusMeters = 18f;

    public GameObject arObjectPrefab;

    [HideInInspector] public bool isInside;
    [HideInInspector] public GameObject spawnedInstance;
}

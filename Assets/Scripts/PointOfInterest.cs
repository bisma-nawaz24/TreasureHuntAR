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

    [Tooltip("Sequence position in the hunt (0-based). Must match HuntManager.CurrentIndex to be active.")]
    public int order;

    [Tooltip("Score awarded when this POI is collected.")]
    public int pointValue = 1;

    [Header("Riddle / Hint")]
    [TextArea(2, 6)]
    public string riddleText;
    public Sprite riddleImage;
    [TextArea(2, 4)]
    public string hintText;
    public Sprite hintImage;
    [Tooltip("Coins required to reveal this POI's hint.")]
    public int hintCost;
    [HideInInspector] public bool hintUsed;

    [HideInInspector] public bool isCollected;

    [Tooltip("Distance at which the AR object appears.")]
    public float enterRadiusMeters = 10f;

    [Tooltip("Distance at which the AR object disappears. Must be larger than enter radius.")]
    public float exitRadiusMeters = 18f;

    public GameObject arObjectPrefab;

    [HideInInspector] public bool isInside;
    [HideInInspector] public GameObject spawnedInstance;
}

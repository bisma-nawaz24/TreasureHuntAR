using UnityEngine;
using UnityEngine.Events;

/*
 * UI teammate — wire these (no UI types live in this script):
 *
 * Events to subscribe to:
 *   OnProgressChanged (float 0–1)  — progress bar / slider fill
 *   OnScoreChanged    (int)        — score label
 *   OnPOICollected    (PointOfInterest) — after score/index update (collected POI);
 *                       CurrentIndex already points at the NEXT POI
 *
 * Properties to poll (optional alternative to events):
 *   CurrentIndex
 *   TotalScore
 *
 * Button onClick → call these methods:
 *   OnMapButtonPressed()
 *   OnRiddleButtonPressed()
 *   OnHintButtonPressed()
 *
 * Also assign GPSTriggerZone in the Inspector (or leave empty to auto-find).
 * POI list / riddles come from GPSTriggerZone.PointsOfInterest; set each POI's
 * `order` to 0, 1, 2… so it matches CurrentIndex.
 */
public class HuntManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GPSTriggerZone gpsTriggerZone;

    [Header("Events (UI hooks)")]
    public UnityEvent<float> OnProgressChanged = new UnityEvent<float>();
    public UnityEvent<int> OnScoreChanged = new UnityEvent<int>();
    public UnityEvent<PointOfInterest> OnPOICollected = new UnityEvent<PointOfInterest>();

    public int CurrentIndex { get; private set; }
    public int TotalScore { get; private set; } = 50;

    private PointOfInterest[] poiList;

    private void Awake()
    {
        if (gpsTriggerZone == null)
        {
            gpsTriggerZone = FindFirstObjectByType<GPSTriggerZone>();
        }
    }

    private void OnEnable()
    {
        ResolvePoiList();

        if (gpsTriggerZone != null)
        {
            gpsTriggerZone.OnPOICollected.AddListener(HandlePOICollected);
        }
    }

    private void OnDisable()
    {
        if (gpsTriggerZone != null)
        {
            gpsTriggerZone.OnPOICollected.RemoveListener(HandlePOICollected);
        }
    }

    private void Start()
    {
        ResolvePoiList();
        // Initial UI sync so listeners can set 0 progress / 0 score without waiting for a collect.
        OnProgressChanged.Invoke(GetProgressFraction());
        OnScoreChanged.Invoke(TotalScore);
    }

    private void ResolvePoiList()
    {
        if (gpsTriggerZone != null)
        {
            poiList = gpsTriggerZone.PointsOfInterest;
        }
    }

    private void HandlePOICollected(PointOfInterest poi)
    {
        if (poi == null)
        {
            return;
        }

        TotalScore += poi.pointValue;
        CurrentIndex++;

        OnScoreChanged.Invoke(TotalScore);
        OnProgressChanged.Invoke(GetProgressFraction());
        // Fired after index advances so listeners can show the NEXT POI via CurrentIndex / GetCurrentPOI().
        OnPOICollected.Invoke(poi);

        Debug.Log(
            $"[HuntManager] Collected '{poi.name}' (+{poi.pointValue}). " +
            $"Score={TotalScore}, NextIndex={CurrentIndex}, Progress={GetProgressFraction():P0}.");
    }

    private float GetProgressFraction()
    {
        int total = poiList != null ? poiList.Length : 0;
        if (total <= 0)
        {
            return 0f;
        }

        return Mathf.Clamp01((float)CurrentIndex / total);
    }

    /// <summary>POI whose order matches CurrentIndex, or null if none / hunt complete.</summary>
    public PointOfInterest GetCurrentPOI()
    {
        ResolvePoiList();
        return GetPOIByOrder(CurrentIndex);
    }

    /// <summary>POI whose <see cref="PointOfInterest.order"/> equals <paramref name="order"/>.</summary>
    public PointOfInterest GetPOIByOrder(int order)
    {
        ResolvePoiList();
        if (poiList == null)
        {
            return null;
        }

        for (int i = 0; i < poiList.Length; i++)
        {
            if (poiList[i] != null && poiList[i].order == order)
            {
                return poiList[i];
            }
        }

        return null;
    }

    /// <summary>Returns the POI's authored <see cref="PointOfInterest.riddleText"/>.</summary>
    public string GetRiddleText(PointOfInterest poi)
    {
        if (poi == null || string.IsNullOrEmpty(poi.riddleText))
        {
            return string.Empty;
        }

        return poi.riddleText;
    }

    /// <summary>
    /// Spends <see cref="PointOfInterest.hintCost"/> coins and reveals the current POI's hint once.
    /// Does nothing (and does not mark hint used) when the player cannot afford it.
    /// </summary>
    public void OnHintButtonPressed()
    {
        PointOfInterest current = GetCurrentPOI();
        if (current == null)
        {
            Debug.Log("[HuntManager] OnHintButtonPressed — no current POI (hunt may be complete).");
            return;
        }

        if (current.hintUsed)
        {
            Debug.Log("[HuntManager] hint already used");
            return;
        }

        int cost = Mathf.Max(0, current.hintCost);
        if (TotalScore < cost)
        {
            // OnInsufficientCoinsForHint does not exist yet — log only.
            Debug.Log(
                $"[HuntManager] not enough coins (have {TotalScore}, need {cost} for '{current.name}').");
            return;
        }

        TotalScore -= cost;
        current.hintUsed = true;
        OnScoreChanged.Invoke(TotalScore);

        Debug.Log(
            $"[HuntManager] Hint for '{current.name}' (order={current.order}, spent={cost}, " +
            $"score={TotalScore}): {current.hintText}");
    }

    /// <summary>Placeholder — UI teammate should open the map panel from here.</summary>
    public void OnMapButtonPressed()
    {
        Debug.Log("[HuntManager] OnMapButtonPressed — wire map UI here.");
    }

    /// <summary>Placeholder — UI teammate should show the riddle for the active POI.</summary>
    public void OnRiddleButtonPressed()
    {
        PointOfInterest current = GetCurrentPOI();
        if (current == null)
        {
            Debug.Log(
                $"[HuntManager] OnRiddleButtonPressed — no POI with order={CurrentIndex} " +
                "(hunt may be complete).");
            return;
        }

        Debug.Log(
            $"[HuntManager] OnRiddleButtonPressed — '{current.name}' (order={current.order}): " +
            $"{current.riddleText}");
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Wires MainUiElements popups (Starting / Riddle / Map) in code.
/// Map popup is optional — leave <see cref="mapPopupPanel"/> unassigned until built.
/// </summary>
public class PopupManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject startingPopupPanel;
    public GameObject riddlePopupPanel;
    public GameObject mapPopupPanel;

    [Header("Starting Popup")]
    public TMP_Text startTitleText;
    public Image coinImage;
    public Button startCloseButton;
    public Button letsPlayButton;

    [Header("Riddle Popup")]
    public TMP_Text riddleTitleText;
    public TMP_Text riddleBodyText;
    public Button riddleCloseButton;
    public Button hintButton;
    public Button continueButton;

    [Header("Map (optional — null-safe)")]
    public Button mapOpenButton;

    [Header("References")]
    [SerializeField] private HuntManager huntManager;

    private void Awake()
    {
        if (huntManager == null)
        {
            huntManager = FindFirstObjectByType<HuntManager>();
        }

        WireButtons();
    }

    private void OnEnable()
    {
        if (huntManager != null)
        {
            huntManager.OnPOICollected.AddListener(HandlePOICollected);
            huntManager.OnScoreChanged.AddListener(HandleScoreChanged);
        }
    }

    private void OnDisable()
    {
        if (huntManager != null)
        {
            huntManager.OnPOICollected.RemoveListener(HandlePOICollected);
            huntManager.OnScoreChanged.RemoveListener(HandleScoreChanged);
        }
    }

    private void Start()
    {
        if (riddlePopupPanel != null)
        {
            riddlePopupPanel.SetActive(false);
        }

        if (mapPopupPanel != null)
        {
            mapPopupPanel.SetActive(false);
        }

        if (startingPopupPanel != null)
        {
            startingPopupPanel.SetActive(true);
        }

        RefreshStartingPopupCoinState();
    }

    private void WireButtons()
    {
        if (startCloseButton != null)
        {
            startCloseButton.onClick.AddListener(OnStartClosePressed);
        }

        if (letsPlayButton != null)
        {
            letsPlayButton.onClick.AddListener(OnLetsPlayPressed);
        }

        if (riddleCloseButton != null)
        {
            riddleCloseButton.onClick.AddListener(OnRiddleClosePressed);
        }

        if (hintButton != null)
        {
            hintButton.onClick.AddListener(OnHintPressed);
        }

        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinuePressed);
        }

        if (mapOpenButton != null)
        {
            mapOpenButton.onClick.AddListener(OnMapOpenPressed);
        }
    }

    private void OnStartClosePressed()
    {
        if (startingPopupPanel != null)
        {
            startingPopupPanel.SetActive(false);
        }
    }

    private void OnLetsPlayPressed()
    {
        if (startingPopupPanel != null)
        {
            startingPopupPanel.SetActive(false);
        }

        ShowRiddleForCurrentPOI(congratulationTitle: false);
    }

    private void OnRiddleClosePressed()
    {
        if (riddlePopupPanel != null)
        {
            riddlePopupPanel.SetActive(false);
        }
    }

    private void OnHintPressed()
    {
        if (huntManager != null)
        {
            huntManager.OnHintButtonPressed();
        }
        else
        {
            Debug.LogWarning("[PopupManager] Hint pressed but HuntManager is missing.");
        }
    }

    private void OnContinuePressed()
    {
        if (riddlePopupPanel != null)
        {
            riddlePopupPanel.SetActive(false);
        }
    }

    private void OnMapOpenPressed()
    {
        if (mapPopupPanel != null)
        {
            mapPopupPanel.SetActive(true);
        }
        else
        {
            Debug.Log("[PopupManager] Map popup not built yet");
        }
    }

    /// <summary>
    /// After a collect, HuntManager has already advanced CurrentIndex to the NEXT POI.
    /// </summary>
    private void HandlePOICollected(PointOfInterest collectedPoi)
    {
        string collectedName = collectedPoi != null ? collectedPoi.name : "treasure";
        ShowRiddleForCurrentPOI(congratulationTitle: true, collectedName);
    }

    private void HandleScoreChanged(int newScore)
    {
        // Keep starting popup coin/title in sync if it's still visible.
        if (startingPopupPanel != null && startingPopupPanel.activeSelf)
        {
            RefreshStartingPopupCoinState();
        }
    }

    private void ShowRiddleForCurrentPOI(bool congratulationTitle, string collectedName = null)
    {
        if (riddlePopupPanel == null)
        {
            return;
        }

        PointOfInterest poi = huntManager != null ? huntManager.GetCurrentPOI() : null;

        if (riddleTitleText != null)
        {
            if (congratulationTitle)
            {
                if (poi != null)
                {
                    riddleTitleText.text =
                        $"Congratulations! You found {collectedName ?? "it"}!\nNext: {poi.name}";
                }
                else
                {
                    riddleTitleText.text =
                        $"Congratulations! You found {collectedName ?? "it"}!\nHunt complete!";
                }
            }
            else
            {
                riddleTitleText.text = poi != null ? poi.name : "Riddle";
            }
        }

        if (riddleBodyText != null)
        {
            if (poi != null && huntManager != null)
            {
                riddleBodyText.text = huntManager.GetRiddleText(poi);
            }
            else if (congratulationTitle && poi == null)
            {
                riddleBodyText.text = "You've collected every treasure. Nice work!";
            }
            else
            {
                riddleBodyText.text = "No riddle available.";
            }
        }

        riddlePopupPanel.SetActive(true);
    }

    private void RefreshStartingPopupCoinState()
    {
        int score = huntManager != null ? huntManager.TotalScore : 0;

        if (startTitleText != null)
        {
            startTitleText.text = $"Treasure Hunt\nCoins: {score}";
        }

        // Image has no numeric score channel — keep visible; pair with title text for coin state.
        if (coinImage != null)
        {
            coinImage.enabled = true;
            coinImage.gameObject.SetActive(true);
        }
    }
}

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenuController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Start button that begins the triathlon race.")]
    public Button startButton;

    [Header("Other Menu Buttons")]
    public Button howToPlayButton;
    public Button leaderboardButton;
    public Button settingsButton;
    public Button audioButton;

    [Header("Footer Stats")]
    public TextMeshProUGUI personalBestText;
    public TextMeshProUGUI versionText;

    [Header("Scene Configuration")]
    [Tooltip("Target scene to load on start. Default is Run_Scene.")]
    public string runSceneName = "Run_Scene";

    private void Awake()
    {
        if (startButton == null)
        {
            Transform btnTr = transform.Find("CenterGroup/StartButton");
            if (btnTr == null) btnTr = transform.Find("StartButton");

            if (btnTr != null)
            {
                startButton = btnTr.GetComponent<Button>();
            }
            else
            {
                var allButtons = GetComponentsInChildren<Button>(true);
                foreach (var btn in allButtons)
                {
                    if (btn.name.ToLower().Contains("start"))
                    {
                        startButton = btn;
                        break;
                    }
                }
            }
        }
    }

    private void Start()
    {
        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartClicked);
        }
        else
        {
            Debug.LogWarning("[MainMenuController] StartButton is not assigned on " + gameObject.name);
        }

        if (howToPlayButton != null) howToPlayButton.onClick.AddListener(OnHowToPlayClicked);
        if (leaderboardButton != null) leaderboardButton.onClick.AddListener(OnLeaderboardClicked);
        if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
        if (audioButton != null) audioButton.onClick.AddListener(OnAudioClicked);

        UpdatePersonalBestDisplay();
    }

    public void OnStartClicked()
    {
        Debug.Log($"[MainMenuController] Starting game -> loading scene: {runSceneName}");

        if (GameManager.I != null)
        {
            GameManager.I.ResetRace();
        }

        SceneManager.LoadScene(runSceneName);
    }

    public void OnHowToPlayClicked()
    {
        Debug.Log("[MainMenuController] How To Play clicked");
    }

    public void OnLeaderboardClicked()
    {
        Debug.Log("[MainMenuController] Leaderboard clicked");
    }

    public void OnSettingsClicked()
    {
        Debug.Log("[MainMenuController] Settings clicked");
    }

    public void OnAudioClicked()
    {
        Debug.Log("[MainMenuController] Audio toggle clicked");
    }

    private void UpdatePersonalBestDisplay()
    {
        if (personalBestText != null)
        {
            float bestTime = PlayerPrefs.GetFloat("Triathlon_BestTime", 252f); // 04:12 default
            int minutes = Mathf.FloorToInt(bestTime / 60f);
            int seconds = Mathf.FloorToInt(bestTime % 60f);
            personalBestText.text = $"⏱  PERSONAL BEST   {minutes:00}:{seconds:00}";
        }
    }
}

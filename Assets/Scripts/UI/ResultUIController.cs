using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class ResultUIController : MonoBehaviour
{
    [Header("Header Outcome")]
    public TextMeshProUGUI raceCompleteText;
    public TextMeshProUGUI outcomeTitleText; // "VICTORY" or "DEFEAT"
    public GameObject newBestBadge;
    public TextMeshProUGUI newBestText;

    [Header("Your Total Time")]
    public TextMeshProUGUI yourTotalTimeValue;
    public TextMeshProUGUI aiTimeValue;
    public TextMeshProUGUI timeDiffText;

    [Header("Stage Splits (Left Card)")]
    public TextMeshProUGUI swimTimeText;
    public TextMeshProUGUI cycleTimeText;
    public TextMeshProUGUI runTimeText;
    public TextMeshProUGUI stageSplitsFooterText;

    [Header("Performance Summary (Right Card)")]
    public TextMeshProUGUI staminaPercentText;
    public Image staminaProgressBar;
    public TextMeshProUGUI totalScoreText;
    public TextMeshProUGUI coinsEarnedText;
    public TextMeshProUGUI outcomeStatusText;

    [Header("Action Buttons")]
    public Button retryButton;
    public Button mainMenuButton;
    public Button nextRaceButton;

    [Header("Fallback / Default Preview Values")]
    public float defaultPlayerTime = 272.50f; // 04:32.50
    public float defaultAiTime = 281.20f;     // 04:41.20
    public float defaultSwimTime = 48.32f;    // 00:48.32
    public float defaultCycleTime = 112.11f;  // 01:52.11
    public float defaultRunTime = 112.07f;    // 01:52.07
    public float defaultScore = 2850f;
    public float defaultStamina = 68f;
    public int defaultCoins = 150;

    private void Start()
    {
        if (retryButton != null) retryButton.onClick.AddListener(OnRetryClicked);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        if (nextRaceButton != null) nextRaceButton.onClick.AddListener(OnNextRaceClicked);

        LoadAndDisplayResults();
    }

    public void LoadAndDisplayResults()
    {
        float pTime = defaultPlayerTime;
        float aiTime = defaultAiTime;
        float swim = defaultSwimTime;
        float cycle = defaultCycleTime;
        float run = defaultRunTime;
        float score = defaultScore;
        float stamina = defaultStamina;
        int coins = defaultCoins;

        if (GameManager.I != null)
        {
            if (GameManager.I.totalTime > 0) pTime = GameManager.I.totalTime;
            score = GameManager.I.totalScore;
            stamina = GameManager.I.stamina;
            coins = GameManager.I.coinsCollected;

            if (GameManager.I.swimTime > 0) swim = GameManager.I.swimTime;
            if (GameManager.I.cycleTime > 0) cycle = GameManager.I.cycleTime;
            if (GameManager.I.runTime > 0) run = GameManager.I.runTime;
            if (GameManager.I.aiTime > 0) aiTime = GameManager.I.aiTime;
        }

        SetResults(pTime, aiTime, swim, cycle, run, score, stamina, coins);
    }

    public void SetResults(float playerTotalTime, float aiTotalTime, float swimTime, float cycleTime, float runTime, float score, float staminaPercent, int coins)
    {
        bool isVictory = playerTotalTime <= aiTotalTime;

        // 1. Victory / Defeat Title
        if (outcomeTitleText != null)
        {
            outcomeTitleText.text = isVictory ? "VICTORY" : "DEFEAT";
            outcomeTitleText.color = isVictory ? new Color(1f, 0.84f, 0.31f, 1f) : new Color(1f, 0.25f, 0.25f, 1f);
        }

        if (newBestBadge != null)
        {
            newBestBadge.SetActive(isVictory);
        }

        // 2. Your Total Time & AI Comparison
        if (yourTotalTimeValue != null) yourTotalTimeValue.text = FormatTime(playerTotalTime);
        if (aiTimeValue != null) aiTimeValue.text = FormatTime(aiTotalTime);

        float diff = Mathf.Abs(playerTotalTime - aiTotalTime);
        if (timeDiffText != null)
        {
            timeDiffText.text = isVictory ? $"-{diff:F2}s AHEAD" : $"+{diff:F2}s BEHIND";
            timeDiffText.color = isVictory ? new Color(0f, 0.95f, 0.45f) : new Color(1f, 0.35f, 0.35f);
        }

        // 3. Stage Splits
        if (swimTimeText != null) swimTimeText.text = FormatTime(swimTime);
        if (cycleTimeText != null) cycleTimeText.text = FormatTime(cycleTime);
        if (runTimeText != null) runTimeText.text = FormatTime(runTime);

        // 4. Performance Summary
        // Stamina Remaining (Single percentage + progress bar)
        float clampedStamina = Mathf.Clamp(staminaPercent, 0f, 100f);
        Color staminaColor = clampedStamina > 40f 
            ? new Color(0f, 0.92f, 1f, 1f) 
            : (clampedStamina > 20f ? new Color(1f, 0.75f, 0.1f, 1f) : new Color(1f, 0.3f, 0.3f, 1f));

        if (staminaPercentText != null)
        {
            staminaPercentText.text = $"{Mathf.RoundToInt(clampedStamina)}%";
            staminaPercentText.color = staminaColor;
        }

        if (staminaProgressBar != null)
        {
            staminaProgressBar.fillAmount = clampedStamina / 100f;
            staminaProgressBar.color = staminaColor;
        }

        // Total Score
        if (totalScoreText != null)
        {
            totalScoreText.text = $"{Mathf.RoundToInt(score):N0} PTS";
        }

        // Coins Earned
        if (coinsEarnedText != null)
        {
            coinsEarnedText.text = $"+{coins} COINS";
        }

        // Outcome Status (Performance Summary footer)
        if (outcomeStatusText != null)
        {
            outcomeStatusText.text = isVictory ? "RACE WON" : "RACE LOST";
            outcomeStatusText.color = isVictory ? new Color(0f, 0.95f, 0.45f) : new Color(1f, 0.35f, 0.35f);
        }
    }

    private string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
        float seconds = timeInSeconds % 60f;
        return string.Format("{0:00}:{1:00.00}", minutes, seconds);
    }

    public void OnRetryClicked()
    {
        Debug.Log("[ResultUI] Retry clicked");
        if (GameManager.I != null)
        {
            GameManager.I.ResetRace();
            GameManager.I.LoadScene("Run_Scene");
        }
        else
        {
            SceneManager.LoadScene("Run_Scene");
        }
    }

    public void OnMainMenuClicked()
    {
        Debug.Log("[ResultUI] Main Menu clicked");
        if (GameManager.I != null)
        {
            GameManager.I.LoadScene("UIScene");
        }
        else
        {
            SceneManager.LoadScene("UIScene");
        }
    }

    public void OnNextRaceClicked()
    {
        Debug.Log("[ResultUI] Next Race clicked");
        if (GameManager.I != null)
        {
            GameManager.I.ResetRace();
            GameManager.I.LoadScene("Run_Scene");
        }
        else
        {
            SceneManager.LoadScene("Run_Scene");
        }
    }
}

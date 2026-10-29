using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class ResultUIController : MonoBehaviour
{
    [Header("Panels Navigation")]
    [Tooltip("Reference to the MenuPanel GameObject to activate when Menu button is clicked.")]
    public GameObject menuPanel;
    [Tooltip("Reference to this ResultPanel GameObject to deactivate when Menu button is clicked.")]
    public GameObject resultPanel;

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
    public TextMeshProUGUI swimDistanceText;
    public TextMeshProUGUI cycleTimeText;
    public TextMeshProUGUI cycleDistanceText;
    public TextMeshProUGUI runTimeText;
    public TextMeshProUGUI runDistanceText;
    public TextMeshProUGUI stageSplitsFooterText;
    public TextMeshProUGUI stageDistanceFooterText;

    [Header("Performance & Score (Right Card)")]
    public TextMeshProUGUI staminaPercentText;
    public Image staminaProgressBar;
    public TextMeshProUGUI totalScoreText;
    public TextMeshProUGUI scoreFormulaText;
    public TextMeshProUGUI distanceScoreText;
    public TextMeshProUGUI coinsEarnedText;
    public TextMeshProUGUI outcomeStatusText;

    [Header("Action Buttons")]
    public Button retryButton;       // Restart button (resets game & starts race)
    public Button mainMenuButton;    // Menu button (menuPanel = true, resultPanel = false)
    public Button nextRaceButton;    // Next race / proceed button

    [Header("Fallback / Default Benchmark Preview Values")]
    public float defaultPlayerTime = 272.50f; // 04:32.50
    public float defaultAiTime = 281.20f;     // 04:41.20
    public float defaultSwimTime = 48.32f;    // 00:48.32
    public float defaultCycleTime = 112.11f;  // 01:52.11
    public float defaultRunTime = 112.07f;    // 01:52.07
    public float defaultSwimDist = 300f;
    public float defaultCycleDist = 1000f;
    public float defaultRunDist = 500f;
    public float defaultStamina = 68f;
    public int defaultCoins = 150;
    public float defaultCoinValue = 10f;
    public float defaultBonus = 0f;

    private void Awake()
    {
        if (resultPanel == null) resultPanel = gameObject;

        if (menuPanel == null)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var mp = canvas.transform.Find("MenuPanel");
                if (mp != null) menuPanel = mp.gameObject;
            }
        }
    }

    private void OnEnable()
    {
        LoadAndDisplayResults();
    }

    private void Start()
    {
        if (retryButton != null) retryButton.onClick.AddListener(OnRestartClicked);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        if (nextRaceButton != null) nextRaceButton.onClick.AddListener(OnNextRaceClicked);

        LoadAndDisplayResults();
    }

    public void LoadAndDisplayResults()
    {
        float pTime = 0f;
        float aiTime = defaultAiTime;
        float swim = 0f;
        float cycle = 0f;
        float run = 0f;
        float swimD = 0f;
        float cycleD = 0f;
        float runD = 0f;
        float stamina = 100f;
        int coins = 0;
        float cValue = 10f;
        float bonus = 0f;

        if (GameManager.I != null)
        {
            swim = GameManager.I.swimTime;
            cycle = GameManager.I.cycleTime;
            run = GameManager.I.runTime;

            swimD = (GameManager.I.swimDistance > 0) ? GameManager.I.swimDistance : (swim > 0 ? GameManager.DefaultSwimDistance : 0f);
            cycleD = (GameManager.I.cycleDistance > 0) ? GameManager.I.cycleDistance : (cycle > 0 ? GameManager.DefaultCycleDistance : 0f);
            runD = (GameManager.I.runDistance > 0) ? GameManager.I.runDistance : (run > 0 ? GameManager.DefaultRunDistance : 0f);

            stamina = GameManager.I.stamina;
            coins = GameManager.I.coinsCollected;
            cValue = GameManager.I.coinValue;
            bonus = GameManager.I.bonuses;

            // If testing a single stage directly (e.g. Cycle_Scene only), provide simulated benchmark stats for unplayed stages
            if (swim <= 0f && run <= 0f && cycle > 0f)
            {
                swim = defaultSwimTime;
                run = defaultRunTime;
                swimD = defaultSwimDist;
                runD = defaultRunDist;
            }

            pTime = swim + cycle + run;
            aiTime = (GameManager.I.aiTime > 0) ? GameManager.I.aiTime : defaultAiTime;
        }
        else
        {
            // Pure fallback if GameManager is absent
            pTime = defaultPlayerTime;
            aiTime = defaultAiTime;
            swim = defaultSwimTime;
            cycle = defaultCycleTime;
            run = defaultRunTime;
            swimD = defaultSwimDist;
            cycleD = defaultCycleDist;
            runD = defaultRunDist;
            stamina = defaultStamina;
            coins = defaultCoins;
            cValue = defaultCoinValue;
            bonus = defaultBonus;
        }

        // Section 6.1 Formula: totalScore = Σ(stageDistance for Run+Swim+Cycle) + (coinsCollected * coinValue) + bonuses
        float calculatedScore = (swimD + cycleD + runD) + (coins * cValue) + bonus;

        SetResults(pTime, aiTime, swim, cycle, run, swimD, cycleD, runD, calculatedScore, stamina, coins, cValue, bonus);
    }

    public void SetResults(float playerTotalTime, float aiTotalTime,
                           float swimTime, float cycleTime, float runTime,
                           float swimDist, float cycleDist, float runDist,
                           float score, float staminaPercent, int coins, float cVal, float bonusVal)
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

        // 3. Stage Splits Breakdown
        if (swimTimeText != null) swimTimeText.text = FormatTime(swimTime);
        if (swimDistanceText != null) swimDistanceText.text = $"{Mathf.RoundToInt(swimDist):N0}m";

        if (cycleTimeText != null) cycleTimeText.text = FormatTime(cycleTime);
        if (cycleDistanceText != null) cycleDistanceText.text = $"{Mathf.RoundToInt(cycleDist):N0}m";

        if (runTimeText != null) runTimeText.text = FormatTime(runTime);
        if (runDistanceText != null) runDistanceText.text = $"{Mathf.RoundToInt(runDist):N0}m";

        float totalDist = swimDist + cycleDist + runDist;
        if (stageDistanceFooterText != null) stageDistanceFooterText.text = $"{Mathf.RoundToInt(totalDist):N0}m";
        if (stageSplitsFooterText != null) stageSplitsFooterText.text = FormatTime(swimTime + cycleTime + runTime);

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

        // Distance Points
        if (distanceScoreText != null)
        {
            distanceScoreText.text = $"{Mathf.RoundToInt(totalDist):N0} PTS";
        }

        // Coins Earned
        float coinScore = coins * cVal;
        if (coinsEarnedText != null)
        {
            coinsEarnedText.text = $"+{coins} COINS (+{Mathf.RoundToInt(coinScore):N0} PTS)";
        }

        // Total Score
        if (totalScoreText != null)
        {
            totalScoreText.text = $"{Mathf.RoundToInt(score):N0} PTS";
        }

        // Score Formula Breakdown
        if (scoreFormulaText != null)
        {
            scoreFormulaText.text = $"ΣDist ({Mathf.RoundToInt(totalDist)}) + Coins ({Mathf.RoundToInt(coinScore)}) + Bonus ({Mathf.RoundToInt(bonusVal)})";
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

    /// <summary>
    /// Restart Button: Resets the entire race/game and restarts from Run_Scene.
    /// </summary>
    public void OnRestartClicked()
    {
        Debug.Log("[ResultUI] Restart clicked -> resetting race and launching Run_Scene");
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

    /// <summary>
    /// Menu Button: Sets active true MenuPanel and false ResultPanel.
    /// </summary>
    public void OnMainMenuClicked()
    {
        Debug.Log("[ResultUI] Menu button clicked -> setting MenuPanel active true and ResultPanel false");

        if (GameManager.I != null)
        {
            GameManager.I.showResultPanel = false;
        }

        if (menuPanel == null)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var mp = canvas.transform.Find("MenuPanel");
                if (mp != null) menuPanel = mp.gameObject;
            }
        }

        if (menuPanel != null)
        {
            menuPanel.SetActive(true);
            if (resultPanel != null)
                resultPanel.SetActive(false);
            else
                gameObject.SetActive(false);
        }
        else
        {
            // If in a different scene without MenuPanel, load UIScene
            if (GameManager.I != null)
                GameManager.I.LoadScene("UIScene");
            else
                SceneManager.LoadScene("UIScene");
        }
    }

    public void OnNextRaceClicked()
    {
        Debug.Log("[ResultUI] Next Race clicked");
        OnRestartClicked();
    }
}

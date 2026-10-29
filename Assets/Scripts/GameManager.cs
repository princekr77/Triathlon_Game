using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    private static GameManager _instance;
    public static GameManager I
    {
        get
        {
            if (_instance == null)
            {
                _instance = Object.FindAnyObjectByType<GameManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("GameManager");
                    _instance = go.AddComponent<GameManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
        set
        {
            _instance = value;
        }
    }

    [Header("UI Panel Routing")]
    [Tooltip("When true, UIScene displays ResultPanel instead of MenuPanel on load.")]
    public bool showResultPanel = false;

    // ---- Cross-phase persistent state ----
    [Header("Stamina & Performance")]
    public float stamina = 100f;
    public int perfectStreak = 0; // Streak is a stamina reward (not score - Section 6.1)
    public bool recovering = false;

    [Header("Stage Distances (raw meters - Section 6.1)")]
    public float swimDistance = 0f;
    public float cycleDistance = 0f;
    public float runDistance = 0f;

    [Header("Default Benchmark Distances")]
    public const float DefaultSwimDistance = 300f;
    public const float DefaultCycleDistance = 1000f;
    public const float DefaultRunDistance = 500f;
    public const float CycleDistance = 1000f;

    [Header("Stage Times (seconds)")]
    public float swimTime = 0f;
    public float cycleTime = 0f;
    public float runTime = 0f;
    public float totalTime = 0f;
    public float aiTime = 281.20f;

    [Header("Score System (Section 6.1 Global Formula)")]
    // Formula: totalScore = Σ(stageDistance for Run+Swim+Cycle) + (coinsCollected * coinValue) + (bonuses)
    public int coinsCollected = 0;
    public float coinValue = 10f; // Default 10 per Section 6.1
    public float bonuses = 0f;    // Reserved for medals / future additions (no bonus from streak)
    public float totalScore = 0f;

    private void Awake()
    {
        // Singleton + survive scene loads
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Calculates the total triathlon time by summing stage times.
    /// </summary>
    public float CalculateTotalTime()
    {
        totalTime = swimTime + cycleTime + runTime;
        return totalTime;
    }

    /// <summary>
    /// Global Score System Formula 6.1:
    /// totalScore = Σ(stageDistance for Run+Swim+Cycle) + (coinsCollected * coinValue) + (bonuses)
    /// Distances summed raw (in meters).
    /// coinValue default = 10.
    /// bonuses default = 0 (streak is stamina reward, not score).
    /// </summary>
    public float CalculateTotalScore()
    {
        float sDist = (swimDistance > 0f) ? swimDistance : (swimTime > 0f ? DefaultSwimDistance : 0f);
        float cDist = (cycleDistance > 0f) ? cycleDistance : (cycleTime > 0f ? DefaultCycleDistance : 0f);
        float rDist = (runDistance > 0f) ? runDistance : (runTime > 0f ? DefaultRunDistance : 0f);

        float sumDistances = sDist + cDist + rDist;
        float coinScore = coinsCollected * coinValue;
        totalScore = sumDistances + coinScore + bonuses;
        return totalScore;
    }

    /// <summary>
    /// Resets all race state, distances, times, stamina, coins, and score for a fresh game.
    /// </summary>
    public void ResetRace()
    {
        showResultPanel = false;
        stamina = 100f;
        perfectStreak = 0;
        recovering = false;

        swimDistance = 0f;
        cycleDistance = 0f;
        runDistance = 0f;

        swimTime = 0f;
        cycleTime = 0f;
        runTime = 0f;
        totalTime = 0f;
        aiTime = 281.20f;

        coinsCollected = 0;
        coinValue = 10f;
        bonuses = 0f;
        totalScore = 0f;
    }

    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}

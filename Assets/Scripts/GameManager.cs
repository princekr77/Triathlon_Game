using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager I;

    // ---- Cross-phase persistent state ----
    public float stamina = 100f;
    public float totalScore = 0f;
    public float totalTime = 0f;
    public int coinsCollected = 0;
    public int perfectStreak = 0;
    public bool recovering = false;

    // Stage breakdown times & AI benchmark
    public float swimTime = 0f;
    public float cycleTime = 0f;
    public float runTime = 0f;
    public float aiTime = 281.20f;

    // ---- Stage config (tune later) ----
    public const float CycleDistance = 1000f;

    private void Awake()
    {
        // Singleton + survive scene loads
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    public void ResetRace()
    {
        stamina = 100f;
        totalScore = 0f;
        totalTime = 0f;
        coinsCollected = 0;
        perfectStreak = 0;
        recovering = false;
        swimTime = 0f;
        cycleTime = 0f;
        runTime = 0f;
        aiTime = 281.20f;
    }

    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}

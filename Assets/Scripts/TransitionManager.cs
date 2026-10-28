using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TransitionManager : MonoBehaviour
{
    public static TransitionManager Instance;

    [Header("UI")]
    [Tooltip("CanvasGroup controlling the black overlay.")]
    public CanvasGroup blackOverlay;

    [Tooltip("Fade-in duration to black (seconds).")]
    public float fadeInDuration = 0.5f;

    [Tooltip("Total black screen duration (seconds). Includes load time.")]
    public float blackScreenDuration = 3.0f;

    [Tooltip("Fade-out duration on the NEW scene (handled by the new scene's own TransitionManager).")]
    public float fadeOutDuration = 0.5f;

    [Header("Stamina regen during transition")]
    public float staminaRegenPerSecond = 0.5f;

    private bool transitioning = false;

    void Awake()
    {
        // Each scene has its own TransitionManager, but only the active one matters.
        Instance = this;
    }

    public void BeginTransition(string nextScene, System.Action onComplete)
    {
        if (transitioning) return;
        transitioning = true;
        StartCoroutine(TransitionRoutine(nextScene, onComplete));
    }

    private IEnumerator TransitionRoutine(string nextScene, System.Action onComplete)
    {
        // 1) Fade TO black
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.unscaledDeltaTime;
            blackOverlay.alpha = Mathf.Clamp01(t / fadeInDuration);
            yield return null;
        }
        blackOverlay.alpha = 1f;

        // 2) Start async load, but HOLD activation
        AsyncOperation op = SceneManager.LoadSceneAsync(nextScene);
        op.allowSceneActivation = false;

        // 3) Hold black for the remaining time while loading
        float elapsed = fadeInDuration;
        while (elapsed < blackScreenDuration || op.progress < 0.9f)
        {
            elapsed += Time.unscaledDeltaTime;

            // Stamina regen during transition
            if (GameManager.I != null)
            {
                GameManager.I.stamina = Mathf.Min(100f,
                    GameManager.I.stamina + staminaRegenPerSecond * Time.unscaledDeltaTime);
            }

            yield return null;
        }

        // 4) Release the scene — it will activate immediately
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;

        // 5) Call onComplete (usually redundant — LoadSceneAsync already switches)
        onComplete?.Invoke();

        // 6) The new scene's TransitionManager will fade OUT from black
        transitioning = false;
    }

    /// <summary>
    /// Call this in the NEW scene's Start() to fade from black to clear.
    /// </summary>
    public IEnumerator FadeFromBlackRoutine()
    {
        blackOverlay.alpha = 1f;
        float t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            blackOverlay.alpha = 1f - Mathf.Clamp01(t / fadeOutDuration);
            yield return null;
        }
        blackOverlay.alpha = 0f;
    }
}
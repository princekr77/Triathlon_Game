using System.Collections;
using UnityEngine;

/// <summary>
/// Initializes the Run stage scene:
/// - Executes screen fade-in via TransitionManager
/// - Syncs with GameManager
/// </summary>
public class RunSceneBootstrap : MonoBehaviour
{
    [Header("Transition Settings")]
    [Tooltip("Delay before starting fade in from black (seconds).")]
    public float startFadeDelay = 0.2f;

    private void Awake()
    {
        // Automatically disable solid colliders on visual road stripes so players can steer freely
        var allColliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        foreach (var col in allColliders)
        {
            if (col != null && col.gameObject.name.ToLower().Contains("stripe"))
            {
                col.enabled = false;
            }
        }
    }

    private IEnumerator Start()
    {
        if (startFadeDelay > 0f)
            yield return new WaitForSeconds(startFadeDelay);

        if (TransitionManager.Instance != null)
        {
            StartCoroutine(TransitionManager.Instance.FadeFromBlackRoutine());
        }
    }
}

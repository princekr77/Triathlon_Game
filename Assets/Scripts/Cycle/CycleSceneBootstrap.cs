using UnityEngine;

public class CycleSceneBootstrap : MonoBehaviour
{
    void Start()
    {
        // Fade in from black at scene start
        if (TransitionManager.Instance != null)
            StartCoroutine(TransitionManager.Instance.FadeFromBlackRoutine());
    }
}
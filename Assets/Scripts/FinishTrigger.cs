using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FinishTrigger : MonoBehaviour
{
    [Tooltip("Tag of the player object that triggers finish.")]
    public string playerTag = "Player";

    [Tooltip("Next scene to load when crossing the finish line.")]
    public string nextScene = "Swim_Scene";

    private bool triggered = false;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (triggered) return;

        // Check if the collider belongs to cycle or runner
        CycleController cycle = other.GetComponentInParent<CycleController>();
        if (cycle == null && other.transform.root != null)
        {
            cycle = other.transform.root.GetComponent<CycleController>();
        }

        RunnerController runner = other.GetComponentInParent<RunnerController>();
        if (runner == null && other.transform.root != null)
        {
            runner = other.transform.root.GetComponent<RunnerController>();
        }

        RunSwimController runSwim = other.GetComponentInParent<RunSwimController>();
        if (runSwim == null && other.transform.root != null)
        {
            runSwim = other.transform.root.GetComponent<RunSwimController>();
        }

        bool isGroundContact = other.gameObject.name.Contains("GroundControlContact");
        bool isPlayer = other.CompareTag(playerTag)
                     || (other.transform.root != null && other.transform.root.CompareTag(playerTag))
                     || isGroundContact
                     || cycle != null
                     || runner != null
                     || runSwim != null;

        if (!isPlayer) return;

        triggered = true;
        Debug.Log($"[Finish Line] Crossed finish line! Triggered by: '{other.gameObject.name}' (isGroundContact: {isGroundContact})");

        if (cycle != null)
        {
            cycle.TriggerFinish();
        }
        else if (runner != null)
        {
            runner.CompleteStage();
        }
        else
        {
            // Direct transition to next scene (Cycle_Scene)
            if (TransitionManager.Instance != null)
            {
                TransitionManager.Instance.BeginTransition(
                    nextScene: nextScene,
                    onComplete: () => {
                        if (GameManager.I != null)
                            GameManager.I.LoadScene(nextScene);
                        else
                            UnityEngine.SceneManagement.SceneManager.LoadScene(nextScene);
                    }
                );
            }
            else
            {
                if (GameManager.I != null)
                    GameManager.I.LoadScene(nextScene);
                else
                    UnityEngine.SceneManagement.SceneManager.LoadScene(nextScene);
            }
        }
    }
}

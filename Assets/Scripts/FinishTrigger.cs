using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FinishTrigger : MonoBehaviour
{
    [Tooltip("Tag of the player object that triggers finish.")]
    public string playerTag = "Player";

    private bool triggered = false;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (triggered) return;

        // Check if the collider is GroundControlContact, player, or belongs to the bike
        CycleController cycle = other.GetComponentInParent<CycleController>();
        if (cycle == null && other.transform.root != null)
        {
            cycle = other.transform.root.GetComponent<CycleController>();
        }

        bool isGroundContact = other.gameObject.name.Contains("GroundControlContact");
        bool isPlayer = other.CompareTag(playerTag)
                     || (other.transform.root != null && other.transform.root.CompareTag(playerTag))
                     || isGroundContact
                     || cycle != null;

        if (!isPlayer) return;

        triggered = true;
        Debug.Log($"[Finish Line] Crossed finish line! Triggered by: '{other.gameObject.name}' (isGroundContact: {isGroundContact})");

        if (cycle != null)
        {
            cycle.TriggerFinish();
        }
        else
        {
            // Direct fallback transition if CycleController was not found
            if (TransitionManager.Instance != null)
            {
                TransitionManager.Instance.BeginTransition(
                    nextScene: "Swim_Scene",
                    onComplete: () => {
                        if (GameManager.I != null)
                            GameManager.I.LoadScene("Swim_Scene");
                        else
                            UnityEngine.SceneManagement.SceneManager.LoadScene("Swim_Scene");
                    }
                );
            }
        }
    }
}

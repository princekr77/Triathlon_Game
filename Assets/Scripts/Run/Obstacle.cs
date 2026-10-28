using UnityEngine;

/// <summary>
/// Placed on hurdles or track obstacles.
/// If the runner does not jump over it and collides, applies a speed penalty or trigger feedback.
/// </summary>
public class Obstacle : MonoBehaviour
{
    [Header("Penalty")]
    [Tooltip("Temporary speed reduction applied when hitting the obstacle.")]
    public float speedPenalty = 3.0f;

    [Tooltip("Duration of the speed penalty in seconds.")]
    public float penaltyDuration = 1.0f;

    [Tooltip("Optional sound effect to play on collision.")]
    public AudioClip hitSound;

    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;

        RunnerController runner = other.GetComponentInParent<RunnerController>();
        if (runner == null && other.transform.root != null)
        {
            runner = other.transform.root.GetComponent<RunnerController>();
        }

        if (runner != null)
        {
            hasTriggered = true;
            Debug.Log($"[Obstacle] Player hit obstacle: {gameObject.name}");

            if (hitSound != null)
            {
                AudioSource.PlayClipAtPoint(hitSound, transform.position);
            }
        }
    }
}

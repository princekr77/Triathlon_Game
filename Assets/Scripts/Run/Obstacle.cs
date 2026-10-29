using UnityEngine;

/// <summary>
/// Placed on hurdles or track obstacles (StopBoard, StopBoard_Big).
/// Implements GDD §3.1: On obstacle hit: currentSpeed *= 0.5f for 1.0s, then ramp back to runSpeed.
/// </summary>
public class Obstacle : MonoBehaviour
{
    [Header("Penalty Settings")]
    [Tooltip("Multiplier applied to currentSpeed on collision (0.5 = 50% speed).")]
    public float speedMultiplier = 0.5f;

    [Tooltip("Duration of the speed penalty in seconds (GDD specifies 1.0s).")]
    public float penaltyDuration = 1.0f;

    [Header("Audio Feedback")]
    [Tooltip("Optional sound effect to play on collision.")]
    public AudioClip hitSound;

    private bool hasTriggered = false;

    /// <summary>
    /// Executes obstacle hit logic on the runner.
    /// </summary>
    public void TriggerObstacle(RunnerController runner)
    {
        if (hasTriggered) return;
        hasTriggered = true;

        if (runner != null)
        {
            runner.OnObstacleHit(penaltyDuration, speedMultiplier);
        }

        if (hitSound != null)
        {
            AudioSource.PlayClipAtPoint(hitSound, transform.position);
        }

        Debug.Log($"[Obstacle] Runner hit obstacle: {gameObject.name} (Speed halved for {penaltyDuration}s)");
    }

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
            TriggerObstacle(runner);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasTriggered) return;

        RunnerController runner = collision.collider.GetComponentInParent<RunnerController>();
        if (runner == null && collision.collider.transform.root != null)
        {
            runner = collision.collider.transform.root.GetComponent<RunnerController>();
        }

        if (runner != null)
        {
            TriggerObstacle(runner);
        }
    }
}

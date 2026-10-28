using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CycleController : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Base speed in m/s. Actual speed = baseSpeed * speedMultiplier.")]
    public float baseSpeed = 12f;

    [Tooltip("Direct speed multiplier for testing. Later replace with cadence logic.")]
    public float speedMultiplier = 1.0f;

    [Header("Animation")]
    [Tooltip("Animator playing the pedaling cycle.")]
    public Animator pedalingAnimator;

    [Tooltip("Animation speed at baseSpeed. Higher = faster pedaling.")]
    public float animSpeedScale = 1.0f;

    [Header("Grounding")]
    [Tooltip("Layer mask for ground detection.")]
    public LayerMask groundMask = ~0;

    [Tooltip("Distance to check below the player for grounding.")]
    public float groundCheckDistance = 0.2f;

    protected Rigidbody rb;
    protected bool raceFinished = false;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX
                       | RigidbodyConstraints.FreezeRotationY
                       | RigidbodyConstraints.FreezeRotationZ;

        if (pedalingAnimator == null)
            pedalingAnimator = GetComponentInChildren<Animator>();
    }

    protected virtual void FixedUpdate()
    {
        if (raceFinished) return;

        // Always move forward (+Z). Later, speedMultiplier will come from cadence.
        float currentSpeed = baseSpeed * speedMultiplier;
        Vector3 forward = Vector3.forward * currentSpeed * Time.fixedDeltaTime;

        // Stick to ground (prevents floating on slopes)
        Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
                            groundCheckDistance + 0.1f, groundMask))
        {
            forward.y = (hit.point.y - transform.position.y) * 0.5f;
        }

        rb.MovePosition(rb.position + forward);
    }

    protected virtual void Update()
    {
        // Pedaling animation speed follows movement speed
        if (pedalingAnimator != null)
        {
#if UNITY_6000_0_OR_NEWER
            float speed01 = Mathf.InverseLerp(0f, baseSpeed * 1.5f, rb.linearVelocity.magnitude);
#else
            float speed01 = Mathf.InverseLerp(0f, baseSpeed * 1.5f, rb.velocity.magnitude);
#endif
            pedalingAnimator.speed = Mathf.Lerp(0.3f, 2.0f, speed01) * animSpeedScale;
        }
    }

    // Called by FinishTrigger when player crosses the line
    public virtual void TriggerFinish()
    {
        if (raceFinished) return;
        raceFinished = true;
        if (rb != null)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector3.zero;
#else
            rb.velocity = Vector3.zero;
#endif
            rb.isKinematic = true;
        }

        // Hand off to TransitionManager
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

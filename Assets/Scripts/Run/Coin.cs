using UnityEngine;

/// <summary>
/// Collectible coin in the Run stage.
/// Rotates smoothly on the vertical axis, detects runner collection,
/// validates physical proximity to prevent collecting from far away,
/// and awards points and coins via RunnerController and GameManager.
/// </summary>
public class Coin : MonoBehaviour
{
    [Header("Coin Value (GDD §3.1)")]
    [Tooltip("Point value added to stageScore when collected (GDD default 10).")]
    public int coinValue = 10;

    [Header("Visual Effects")]
    [Tooltip("Rotation speed in degrees per second.")]
    public float rotationSpeed = 160.0f;

    [Tooltip("Gentle floating bob speed.")]
    public float bobSpeed = 3.0f;

    [Tooltip("Gentle floating bob height amplitude.")]
    public float bobHeight = 0.08f;

    [Header("Collection Proximity Guard")]
    [Tooltip("Maximum horizontal distance (meters) between runner and coin to trigger collection. Prevents collecting coins from adjacent lanes or from far away.")]
    public float maxHorizontalCollectionDistance = 1.6f;

    [Tooltip("Maximum vertical distance (meters) between runner torso and coin.")]
    public float maxVerticalCollectionDistance = 2.2f;

    [Header("Audio Feedback")]
    [Tooltip("Optional sound effect played when coin is collected.")]
    public AudioClip collectSound;

    private bool isCollected = false;
    private float initialY;

    private void Awake()
    {
        // Safety guard: if an instance scale was accidentally inflated (e.g. 100), normalize it
        if (transform.localScale.x > 3.0f || transform.localScale.z > 3.0f)
        {
            transform.localScale = Vector3.one;
        }
    }

    private void Start()
    {
        initialY = transform.position.y;
    }

    private void Update()
    {
        if (isCollected) return;

        // Continuous vertical spin
        transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.World);

        // Gentle floating bob
        if (bobHeight > 0f)
        {
            float newY = initialY + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCollectFromCollider(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryCollectFromCollider(other);
    }

    private void TryCollectFromCollider(Collider other)
    {
        if (isCollected) return;

        RunnerController runner = other.GetComponentInParent<RunnerController>();
        if (runner == null && other.transform.root != null)
        {
            runner = other.transform.root.GetComponent<RunnerController>();
        }

        if (runner != null)
        {
            // Verify runner is physically close enough to this coin
            Vector3 runnerPos = runner.transform.position;
            float horizontalDist = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(runnerPos.x, runnerPos.z));

            // Runner height is ~1.8m; torso/center is at runnerPos.y + 0.9m
            float verticalDist = Mathf.Abs(transform.position.y - (runnerPos.y + 0.9f));

            if (horizontalDist <= maxHorizontalCollectionDistance && verticalDist <= maxVerticalCollectionDistance)
            {
                Collect(runner);
            }
        }
    }

    public void Collect(RunnerController runner)
    {
        if (isCollected) return;
        isCollected = true;

        if (runner != null)
        {
            runner.CollectCoin(coinValue);
        }

        if (collectSound != null)
        {
            AudioSource.PlayClipAtPoint(collectSound, transform.position);
        }

        Destroy(gameObject);
    }
}

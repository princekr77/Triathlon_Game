using UnityEngine;

/// <summary>
/// Collectible coin in the Run stage.
/// Rotates smoothly on the vertical axis, detects runner collection,
/// awards points and coins via RunnerController and GameManager.
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

    [Header("Audio Feedback")]
    [Tooltip("Optional sound effect played when coin is collected.")]
    public AudioClip collectSound;

    private bool isCollected = false;
    private float initialY;

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
        if (isCollected) return;

        RunnerController runner = other.GetComponentInParent<RunnerController>();
        if (runner == null && other.transform.root != null)
        {
            runner = other.transform.root.GetComponent<RunnerController>();
        }

        if (runner != null)
        {
            Collect(runner);
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

using UnityEngine;

/// <summary>
/// Smooth third-person chase camera for the runner.
/// Automatically captures and respects the REAL camera position and rotation
/// set in the Scene View, preventing unwanted downward sinking or sudden snaps.
/// </summary>
public class RunnerCameraFollow : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Target runner transform to follow.")]
    public Transform target;

    [Header("Position Tracking")]
    [Tooltip("If true, automatically captures the camera's real Scene position relative to the player at Start.")]
    public bool useCurrentScenePosition = true;

    [Tooltip("Camera offset relative to runner heading (X: lateral, Y: height, Z: forward/back). Auto-calculated if useCurrentScenePosition is true.")]
    public Vector3 offset = new Vector3(0f, 4.0f, -6.0f);

    [Header("Rotation Settings")]
    [Tooltip("If true, maintains the exact Scene camera angle and orientation.")]
    public bool lockSceneRotation = true;

    [Tooltip("If true (and lockSceneRotation is false), smoothly turns camera Y to match runner heading while keeping original pitch.")]
    public bool followTargetHeading = false;

    [Header("Smooth Speeds")]
    [Tooltip("Catch-up speed along the forward axis.")]
    public float forwardSmoothSpeed = 16.0f;

    [Tooltip("Smooth speed for lateral (X) movements.")]
    public float lateralSmoothSpeed = 10.0f;

    [Tooltip("Smooth speed for vertical (Y) movements.")]
    public float verticalSmoothSpeed = 10.0f;

    [Tooltip("Smooth speed for camera rotation.")]
    public float rotationSmoothSpeed = 8.0f;

    private Quaternion initialRotation;
    private bool initialized = false;

    private void Start()
    {
        InitializeCamera();
    }

    public void InitializeCamera()
    {
        if (target == null)
        {
            var runner = FindFirstObjectByType<RunnerController>();
            if (runner != null)
            {
                target = runner.transform;
            }
            else
            {
                GameObject playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null) target = playerObj.transform;
            }
        }

        initialRotation = transform.rotation;

        if (target != null && useCurrentScenePosition)
        {
            // Capture the real camera position relative to the player's horizontal orientation
            Quaternion targetHeading = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
            offset = Quaternion.Inverse(targetHeading) * (transform.position - target.position);
            initialized = true;
        }
        else if (target != null)
        {
            initialized = true;
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        if (!initialized)
        {
            InitializeCamera();
            if (!initialized) return;
        }

        // Calculate desired world position respecting the runner's forward heading
        Quaternion targetHeading = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
        Vector3 desiredPos = target.position + (targetHeading * offset);

        // Smooth position tracking
        float newX = Mathf.Lerp(transform.position.x, desiredPos.x, lateralSmoothSpeed * Time.deltaTime);
        float newY = Mathf.Lerp(transform.position.y, desiredPos.y, verticalSmoothSpeed * Time.deltaTime);
        float newZ = Mathf.Lerp(transform.position.z, desiredPos.z, forwardSmoothSpeed * Time.deltaTime);

        transform.position = new Vector3(newX, newY, newZ);

        // Rotation handling
        if (lockSceneRotation)
        {
            // Keep the exact pitch, yaw, and roll set in the scene
            transform.rotation = initialRotation;
        }
        else if (followTargetHeading)
        {
            // Match runner Y heading while preserving the initial pitch angle
            float pitch = initialRotation.eulerAngles.x;
            float roll = initialRotation.eulerAngles.z;
            Quaternion desiredRotation = Quaternion.Euler(pitch, target.eulerAngles.y, roll);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSmoothSpeed * Time.deltaTime);
        }
    }
}

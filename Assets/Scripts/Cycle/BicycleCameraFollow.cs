using UnityEngine;

/// <summary>
/// Smooth third-person chase camera for the bicycle.
/// Follows the bicycle smoothly from behind and slightly above.
/// </summary>
public class BicycleCameraFollow : MonoBehaviour
{
    [Tooltip("Target to follow (e.g. Bicycle_2.0_RIg).")]
    public Transform target;

    [Tooltip("Camera offset relative to the bicycle (X: right, Y: up, Z: back).")]
    public Vector3 offset = new Vector3(0f, 2.2f, -4.5f);

    [Tooltip("How smoothly the camera catches up to the target position.")]
    public float positionSmoothSpeed = 8.0f;

    [Tooltip("How smoothly the camera rotates to match the target heading.")]
    public float rotationSmoothSpeed = 6.0f;

    [Tooltip("Look ahead offset (e.g. at the handlebars/rider head).")]
    public Vector3 lookAtOffset = new Vector3(0f, 1.2f, 0f);

    void Start()
    {
        if (target == null)
        {
            // Auto-find bicycle by tag or name
            GameObject bike = GameObject.FindWithTag("Player");
            if (bike != null) target = bike.transform;
            else
            {
                var cadenceController = FindFirstObjectByType<CyclingCadenceController>();
                if (cadenceController != null) target = cadenceController.transform;
            }
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Calculate desired position in world space based on target's orientation
        Vector3 targetPos = target.position;
        Quaternion targetRot = Quaternion.Euler(0f, target.eulerAngles.y, 0f); // keep horizon level
        Vector3 desiredPosition = targetPos + targetRot * offset;

        // Smoothly interpolate position
        transform.position = Vector3.Lerp(transform.position, desiredPosition, positionSmoothSpeed * Time.deltaTime);

        // Smoothly look at the target
        Vector3 lookTarget = targetPos + lookAtOffset;
        Vector3 dir = (lookTarget - transform.position).normalized;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSmoothSpeed * Time.deltaTime);
        }
    }
}

using UnityEngine;

/// <summary>
/// Smooth chase camera for the bicycle that preserves the exact scene camera position and angle.
/// </summary>
public class BicycleCameraFollow : MonoBehaviour
{
    [Tooltip("Target to follow (e.g. Bicycle_2.0_RIg).")]
    public Transform target;

    [Tooltip("Camera offset relative to the bicycle (X: right, Y: up, Z: back).")]
    public Vector3 offset = new Vector3(0f, 2.2f, -4.5f);

    [Tooltip("If true, automatically preserves the camera's exact position and angle from the scene.")]
    public bool preserveSceneCameraTransform = true;

    [Tooltip("How smoothly the camera catches up to the target position.")]
    public float positionSmoothSpeed = 10.0f;

    [Tooltip("How smoothly the camera rotates to match the target heading.")]
    public float rotationSmoothSpeed = 8.0f;

    private Quaternion initialRotOffset = Quaternion.identity;

    void Start()
    {
        if (target == null)
        {
            GameObject bike = GameObject.Find("Bicycle_2.0_RIg");
            if (bike != null) target = bike.transform;
            else
            {
                var cadenceController = FindFirstObjectByType<CyclingCadenceController>();
                if (cadenceController != null) target = cadenceController.transform;
            }
        }

        if (target != null && preserveSceneCameraTransform)
        {
            // Lock in the camera's current scene position and angle relative to the bike
            offset = Quaternion.Inverse(target.rotation) * (transform.position - target.position);
            initialRotOffset = Quaternion.Inverse(target.rotation) * transform.rotation;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Calculate target world position using preserved relative offset
        Vector3 desiredPosition = target.position + target.rotation * offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, positionSmoothSpeed * Time.deltaTime);

        // Calculate target world rotation using preserved relative angle
        Quaternion desiredRotation = target.rotation * initialRotOffset;
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSmoothSpeed * Time.deltaTime);
    }
}

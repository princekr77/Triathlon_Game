using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controls Player_Swim in Swim_Scene:
/// 1. Immediately plays Swimming animation on entering Play mode.
/// 2. Moves the swimmer forward continuously along the water canal.
/// 3. Ensures the swimmer properly lays horizontally on the water surface with realistic buoyancy.
/// 4. Supports player steering (A/D) and speed adjustment (W/S) via InputSystem_Actions, Keyboard, and Gamepad.
/// 5. Automatically positions and smoothly follows with Camera.main.
/// </summary>
public class SwimmerController : MonoBehaviour
{
    [Header("Swimming Movement")]
    [Tooltip("Base forward swimming speed in meters per second.")]
    public float baseSwimSpeed = 4.5f;

    [Tooltip("Fast sprint swimming speed when holding W / Forward.")]
    public float sprintSwimSpeed = 7.0f;

    [Tooltip("If true, automatically swims forward upon entering Play mode.")]
    public bool autoMoveForward = true;

    [Tooltip("Lateral steering speed (A/D or Left/Right).")]
    public float lateralSpeed = 5.0f;

    [Tooltip("Turning smoothing speed.")]
    public float rotationSmoothing = 8.0f;

    [Header("Canal Boundaries")]
    [Tooltip("Restricts swimmer within canal walls.")]
    public bool clampToCanal = true;
    public float minCanalX = 153.0f;
    public float maxCanalX = 169.5f;

    [Header("Water Surface Alignment")]
    [Tooltip("Target water surface Y level. Auto-detected from Water_Surface if present.")]
    public float waterSurfaceY = 12.6f;

    [Tooltip("Vertical offset to ensure the swimmer properly lays on the water surface (half-submerged, prone).")]
    public float surfaceOffset = -0.35f;

    [Tooltip("Body pitch tilt in degrees to ensure the swimmer lays completely flat/horizontal on the water.")]
    public float bodyPitch = 0.0f;

    [Tooltip("Body roll tilt in degrees.")]
    public float bodyRoll = 0.0f;

    [Header("Water Buoyancy & Bobbing")]
    [Tooltip("Enables gentle realistic water bobbing and stroke sway.")]
    public bool enableWaterBobbing = true;
    public float bobbingFrequency = 3.2f;
    public float bobbingAmplitude = 0.05f;
    public float rollSwayAmplitude = 3.0f;

    [Header("Animation")]
    [Tooltip("Animator reference (uses Swim_Animator). Auto-found if null.")]
    public Animator animator;
    public string swimStateName = "Swimming";
    public string speedParam = "Speed";
    public string isSwimmingParam = "isSwimming";

    [Header("Input System Actions")]
    [Tooltip("Reference to Assets/InputSystem_Actions.inputactions.")]
#if ENABLE_INPUT_SYSTEM
    public InputActionAsset inputActions;
    private InputAction moveAction;
#endif

    [Header("Camera Follow")]
    [Tooltip("Automatically attaches and smoothly follows with Camera.main.")]
    public bool autoFollowCamera = true;
    public Vector3 cameraOffset = new Vector3(0f, 3.2f, 6.5f);
    public float cameraFollowSpeed = 10.0f;
    public float cameraRotationSpeed = 8.0f;

    // Runtime state
    private Camera mainCam;
    private float currentForwardSpeed;
    private float currentLateralInput = 0f;
    private float currentHeadingYaw = 180f; // Facing -Z along the water corridor
    private int speedHash;
    private int isSwimmingHash;
    private bool hasSpeedParam;
    private bool hasIsSwimmingParam;

    private void Awake()
    {
        // 1. Resolve Animator
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        // 2. Cache animator parameters
        if (animator != null)
        {
            animator.applyRootMotion = false;
            speedHash = Animator.StringToHash(speedParam);
            isSwimmingHash = Animator.StringToHash(isSwimmingParam);

            foreach (var param in animator.parameters)
            {
                if (param.nameHash == speedHash) hasSpeedParam = true;
                if (param.nameHash == isSwimmingHash) hasIsSwimmingParam = true;
            }

            if (hasIsSwimmingParam)
            {
                animator.SetBool(isSwimmingHash, true);
            }
        }

        // 3. Auto-detect Water_Surface in scene to get exact water height and disable solid blocking
        GameObject waterObj = GameObject.Find("Water_Surface");
        if (waterObj != null)
        {
            waterSurfaceY = waterObj.transform.position.y;

            // Ensure water surface has isTrigger = true so it doesn't block the swimmer
            if (waterObj.TryGetComponent<Collider>(out var waterCol))
            {
                waterCol.isTrigger = true;
            }
        }

        // 4. Ensure kinematic Rigidbody and trigger Collider for zone interactions
        if (!TryGetComponent<Rigidbody>(out var rb))
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;

        if (!TryGetComponent<Collider>(out var col))
        {
            var box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(2.0f, 1.0f, 2.5f);
            box.center = new Vector3(0f, 0f, 0f);
        }

        // 5. Setup Input Actions
        SetupInputActions();

        currentForwardSpeed = baseSwimSpeed;
        currentHeadingYaw = transform.eulerAngles.y;
        if (Mathf.Abs(currentHeadingYaw) < 1f)
        {
            currentHeadingYaw = 180f; // Default facing -Z along water corridor
        }
    }

    private void OnEnable()
    {
        if (mainCam == null)
        {
            mainCam = Camera.main;
        }

        currentForwardSpeed = autoMoveForward ? baseSwimSpeed : 0.0f;
        SnapToWaterSurface();

        if (animator != null)
        {
            if (hasIsSwimmingParam) animator.SetBool(isSwimmingHash, true);
            animator.Play(swimStateName, 0, 0f);
        }

#if ENABLE_INPUT_SYSTEM
        if (inputActions != null)
        {
            var playerMap = inputActions.FindActionMap("Player");
            playerMap?.Enable();
        }
#endif
    }

    private void Start()
    {
        mainCam = Camera.main;

        // Immediately start playing the swim animation
        if (animator != null)
        {
            animator.Play(swimStateName, 0, 0f);
        }

        // Snap immediately to water surface on start
        SnapToWaterSurface();
    }

    private void SetupInputActions()
    {
#if ENABLE_INPUT_SYSTEM
        if (inputActions == null)
        {
            var allAssets = Resources.FindObjectsOfTypeAll<InputActionAsset>();
            foreach (var asset in allAssets)
            {
                if (asset != null && asset.name.Contains("InputSystem_Actions"))
                {
                    inputActions = asset;
                    break;
                }
            }
        }

        if (inputActions != null)
        {
            var playerMap = inputActions.FindActionMap("Player");
            if (playerMap != null)
            {
                moveAction = playerMap.FindAction("Move");
                playerMap.Enable();
            }
        }
#endif
    }

    private void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        if (inputActions != null)
        {
            var playerMap = inputActions.FindActionMap("Player");
            playerMap?.Disable();
        }
#endif
    }

    private void Update()
    {
        ReadInput(out float forwardInput, out float lateralInput);

        // 1. Calculate Forward Speed
        float targetSpeed = baseSwimSpeed;
        if (forwardInput > 0.1f)
        {
            targetSpeed = sprintSwimSpeed;
        }
        else if (forwardInput < -0.1f)
        {
            targetSpeed = baseSwimSpeed * 0.5f;
        }
        else if (!autoMoveForward)
        {
            targetSpeed = 0f;
        }

        currentForwardSpeed = Mathf.Lerp(currentForwardSpeed, targetSpeed, 6.0f * Time.deltaTime);

        // 2. Calculate Forward & Lateral Movement
        Vector3 forwardMove = -Vector3.forward * (currentForwardSpeed * Time.deltaTime); // Heading -Z
        Vector3 lateralMove = Vector3.right * (lateralInput * lateralSpeed * Time.deltaTime);
        Vector3 pos = transform.position + forwardMove + lateralMove;

        // 3. Clamp X inside water canal
        if (clampToCanal)
        {
            pos.x = Mathf.Clamp(pos.x, minCanalX, maxCanalX);
        }

        // 4. Properly Lay on Water Surface (Y Height Calculation)
        float targetY = waterSurfaceY + surfaceOffset;
        float bobbing = 0f;
        float rollSway = 0f;

        if (enableWaterBobbing && currentForwardSpeed > 0.1f)
        {
            bobbing = Mathf.Sin(Time.time * bobbingFrequency) * bobbingAmplitude;
            rollSway = Mathf.Sin(Time.time * (bobbingFrequency * 0.5f)) * rollSwayAmplitude;
        }

        pos.y = targetY + bobbing;
        transform.position = pos;

        // 5. Orientation: Lay flat horizontally on water with gentle stroke roll
        float steerYawAngle = currentHeadingYaw + (lateralInput * -12.0f);
        Quaternion targetRotation = Quaternion.Euler(bodyPitch, steerYawAngle, bodyRoll + rollSway);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSmoothing * Time.deltaTime);

        // 6. Keep Swimming Animation Looping and update speed
        UpdateAnimationLoop();
    }

    private void LateUpdate()
    {
        if (autoFollowCamera && mainCam != null)
        {
            UpdateCameraFollow();
        }
    }

    /// <summary>
    /// Snaps position and rotation immediately to the water surface.
    /// </summary>
    public void SnapToWaterSurface()
    {
        Vector3 pos = transform.position;
        pos.y = waterSurfaceY + surfaceOffset;
        if (clampToCanal)
        {
            pos.x = Mathf.Clamp(pos.x, minCanalX, maxCanalX);
        }
        transform.position = pos;
        transform.rotation = Quaternion.Euler(bodyPitch, currentHeadingYaw, bodyRoll);
    }

    /// <summary>
    /// Reads movement inputs from InputSystem_Actions, Keyboard, and Gamepad.
    /// </summary>
    private void ReadInput(out float forwardInput, out float lateralInput)
    {
        forwardInput = 0f;
        lateralInput = 0f;

#if ENABLE_INPUT_SYSTEM
        if (moveAction != null)
        {
            Vector2 val = moveAction.ReadValue<Vector2>();
            lateralInput = val.x;
            forwardInput = val.y;
        }

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) forwardInput = Mathf.Max(forwardInput, 1f);
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) forwardInput = Mathf.Min(forwardInput, -1f);
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) lateralInput -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) lateralInput += 1f;
        }

        var gp = Gamepad.current;
        if (gp != null)
        {
            Vector2 stick = gp.leftStick.ReadValue();
            if (Mathf.Abs(stick.x) > 0.15f) lateralInput += stick.x;
            if (Mathf.Abs(stick.y) > 0.15f) forwardInput += stick.y;
        }
#else
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) forwardInput += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) forwardInput -= 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) lateralInput -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) lateralInput += 1f;
#endif

        lateralInput = Mathf.Clamp(lateralInput, -1f, 1f);
    }

    /// <summary>
    /// Ensures the swimming animation loops continuously and matches current swim speed.
    /// </summary>
    private void UpdateAnimationLoop()
    {
        if (animator == null) return;

        // If animation reached end and clip has not looped, loop it
        var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName(swimStateName))
        {
            if (!stateInfo.loop && stateInfo.normalizedTime >= 1.0f)
            {
                animator.Play(swimStateName, 0, 0f);
            }

            // Sync animation speed with forward swimming speed
            float animSpeed = Mathf.Lerp(0.85f, 1.4f, (currentForwardSpeed - baseSwimSpeed) / (sprintSwimSpeed - baseSwimSpeed + 0.001f));
            animator.speed = animSpeed;

            if (hasSpeedParam)
            {
                animator.SetFloat(speedHash, currentForwardSpeed);
            }
        }
        else
        {
            // If another state is active, transition back to Swimming
            animator.Play(swimStateName);
        }
    }

    /// <summary>
    /// Smooth chase camera follow behind the swimmer.
    /// </summary>
    private void UpdateCameraFollow()
    {
        Vector3 targetCamPos = transform.position + cameraOffset;
        mainCam.transform.position = Vector3.Lerp(mainCam.transform.position, targetCamPos, cameraFollowSpeed * Time.deltaTime);

        Vector3 lookTarget = transform.position + Vector3.up * 0.5f;
        Quaternion targetCamRot = Quaternion.LookRotation(lookTarget - mainCam.transform.position, Vector3.up);
        mainCam.transform.rotation = Quaternion.Slerp(mainCam.transform.rotation, targetCamRot, cameraRotationSpeed * Time.deltaTime);
    }
}

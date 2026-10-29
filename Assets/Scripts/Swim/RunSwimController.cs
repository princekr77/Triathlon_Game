using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Simple movement and animation controller for Player_RunSwim in Swim_Scene.
/// Links InputSystem_Actions ("Player/Move" and "Player/Jump"):
/// - W: Runs forward
/// - Space: Jumps
/// Drives RunSwim_Animator parameters: isRunning, isGrounded, Jump, isIdle.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class RunSwimController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Movement speed when running forward (m/s).")]
    public float moveSpeed = 8.0f;

    [Tooltip("Turning/rotation smoothing speed.")]
    public float rotationSpeed = 12.0f;

    [Tooltip("If true, W/S/A/D moves relative to camera view. If false, moves relative to character orientation.")]
    public bool moveRelativeToCamera = true;

    [Header("Jump & Gravity")]
    [Tooltip("Jump peak height in meters.")]
    public float jumpHeight = 1.8f;

    [Tooltip("Gravity acceleration.")]
    public float gravity = 20.0f;

    [Tooltip("Stick-to-ground downward force applied when grounded.")]
    public float groundedStickForce = -2.0f;

    [Header("Animation Settings")]
    [Tooltip("Animator reference (uses RunSwim_Animator).")]
    public Animator animator;

    public string isRunningParam = "isRunning";
    public string isGroundedParam = "isGrounded";
    public string jumpTriggerParam = "Jump";
    public string isIdleParam = "isIdle";
    public string isSwimmingParam = "isSwimming";

    [Header("Input System Actions")]
    [Tooltip("Reference to Assets/InputSystem_Actions.inputactions.")]
#if ENABLE_INPUT_SYSTEM
    public InputActionAsset inputActions;
    private InputAction moveAction;
    private InputAction jumpAction;
#endif

    [Header("Camera Follow")]
    [Tooltip("If true and no SwimCameraFollow exists on Main Camera, automatically positions and aligns targetCamera to follow this player.")]
    public bool autoFollowCamera = false;
    public Vector3 cameraOffset = new Vector3(0f, 3.5f, -6.5f);
    public float cameraFollowSpeed = 10.0f;
    public float cameraRotationSpeed = 8.0f;

    // Component and runtime state
    private CharacterController characterController;
    private Camera targetCamera;
    private float verticalVelocity = 0.0f;
    private bool isGrounded = true;
    private float airborneGraceTimer = 0.0f;

    // Animator parameter hashes
    private int isRunningHash;
    private int isGroundedHash;
    private int jumpTriggerHash;
    private int isIdleHash;
    private int isSwimmingHash;

    private bool hasIsRunning;
    private bool hasIsGrounded;
    private bool hasJumpTrigger;
    private bool hasIsIdle;
    private bool hasIsSwimming;

    private void Awake()
    {
        // 1. Setup CharacterController
        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            characterController = gameObject.AddComponent<CharacterController>();
        }
        characterController.center = new Vector3(0f, 0.9f, 0f);
        characterController.height = 1.8f;
        characterController.radius = 0.35f;
        characterController.skinWidth = 0.05f;
        characterController.stepOffset = 0.3f;
        characterController.minMoveDistance = 0.0f;

        // 2. Prevent Rigidbody physics interference if present, or add kinematic Rigidbody for triggers
        if (!TryGetComponent<Rigidbody>(out var rb))
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;

        // 3. Cache Animator and hashes
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        CacheAnimatorHashes();

        // 4. Setup Input Actions
        SetupInputActions();

        // 5. Disable solid colliders on road stripes if any exist
        DisableStripeColliders();
    }

    private void OnEnable()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        verticalVelocity = 0.0f;
        isGrounded = true;

        if (animator != null)
        {
            if (hasIsGrounded) animator.SetBool(isGroundedHash, true);
            if (hasIsRunning) animator.SetBool(isRunningHash, false);
            if (hasIsIdle) animator.SetBool(isIdleHash, true);
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
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            CacheAnimatorHashes();
        }

        // Initial grounded and idle animation state
        if (animator != null)
        {
            if (hasIsGrounded) animator.SetBool(isGroundedHash, true);
            if (hasIsRunning) animator.SetBool(isRunningHash, false);
            if (hasIsIdle) animator.SetBool(isIdleHash, true);
        }
    }

    private void CacheAnimatorHashes()
    {
        if (animator == null) return;

        isRunningHash = Animator.StringToHash(isRunningParam);
        isGroundedHash = Animator.StringToHash(isGroundedParam);
        jumpTriggerHash = Animator.StringToHash(jumpTriggerParam);
        isIdleHash = Animator.StringToHash(isIdleParam);
        isSwimmingHash = Animator.StringToHash(isSwimmingParam);

        hasIsRunning = false;
        hasIsGrounded = false;
        hasJumpTrigger = false;
        hasIsIdle = false;
        hasIsSwimming = false;

        foreach (var param in animator.parameters)
        {
            if (param.nameHash == isRunningHash) hasIsRunning = true;
            else if (param.nameHash == isGroundedHash) hasIsGrounded = true;
            else if (param.nameHash == jumpTriggerHash) hasJumpTrigger = true;
            else if (param.nameHash == isIdleHash) hasIsIdle = true;
            else if (param.nameHash == isSwimmingHash) hasIsSwimming = true;
        }
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
                jumpAction = playerMap.FindAction("Jump");
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
        ReadInput(out Vector2 moveInput, out bool jumpRequested);

        CheckGrounded();

        // Handle Jump (Space)
        if (jumpRequested && isGrounded)
        {
            ExecuteJump();
        }

        // Handle Movement (W / S / A / D)
        Vector3 horizontalMove = CalculateMovement(moveInput);

        // Apply Vertical Movement & Gravity
        Vector3 totalVelocity = horizontalMove + Vector3.up * verticalVelocity;
        characterController.Move(totalVelocity * Time.deltaTime);

        // Update Animations (isRunning, isGrounded, isIdle)
        bool isMoving = moveInput.sqrMagnitude > 0.01f;
        UpdateAnimations(isMoving);
    }

    private void LateUpdate()
    {
        if (autoFollowCamera && targetCamera != null && targetCamera.GetComponent<SwimCameraFollow>() == null)
        {
            UpdateCameraFollow();
        }
    }

    /// <summary>
    /// Reads movement (W/S/A/D) and jump (Space) input from InputSystem_Actions with keyboard/gamepad fallback.
    /// </summary>
    private void ReadInput(out Vector2 moveInput, out bool jumpRequested)
    {
        moveInput = Vector2.zero;
        jumpRequested = false;

#if ENABLE_INPUT_SYSTEM
        // 1. Read from InputSystem_Actions
        if (moveAction != null)
        {
            moveInput = moveAction.ReadValue<Vector2>();
        }

        if (jumpAction != null && jumpAction.triggered)
        {
            jumpRequested = true;
        }

        // 2. Direct Keyboard fallback (W, S, A, D, Space)
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) moveInput.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) moveInput.y -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) moveInput.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) moveInput.x += 1f;

            if (kb.spaceKey.wasPressedThisFrame)
            {
                jumpRequested = true;
            }
        }

        // 3. Direct Gamepad fallback
        var gp = Gamepad.current;
        if (gp != null)
        {
            Vector2 stick = gp.leftStick.ReadValue();
            if (stick.sqrMagnitude > 0.04f)
            {
                moveInput = stick;
            }
            if (gp.buttonSouth.wasPressedThisFrame)
            {
                jumpRequested = true;
            }
        }
#else
        // Legacy input fallback if Input System is disabled
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) moveInput.y += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) moveInput.y -= 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) moveInput.x -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) moveInput.x += 1f;
        if (Input.GetKeyDown(KeyCode.Space)) jumpRequested = true;
#endif

        if (moveInput.sqrMagnitude > 1.0f)
        {
            moveInput.Normalize();
        }
    }

    /// <summary>
    /// Calculates the horizontal movement vector and smoothly turns the player in the direction of travel.
    /// </summary>
    private Vector3 CalculateMovement(Vector2 moveInput)
    {
        Vector3 moveDirection = Vector3.zero;

        if (moveInput.sqrMagnitude > 0.001f)
        {
            if (moveRelativeToCamera && targetCamera != null)
            {
                Vector3 camFwd = Vector3.ProjectOnPlane(targetCamera.transform.forward, Vector3.up).normalized;
                Vector3 camRight = Vector3.ProjectOnPlane(targetCamera.transform.right, Vector3.up).normalized;
                moveDirection = (camRight * moveInput.x + camFwd * moveInput.y).normalized;
            }
            else
            {
                moveDirection = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;
            }

            if (moveDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }

        return moveDirection * moveSpeed;
    }

    /// <summary>
    /// Checks ground state using CharacterController and Physics SphereCast.
    /// </summary>
    private void CheckGrounded()
    {
        if (airborneGraceTimer > 0f)
        {
            airborneGraceTimer -= Time.deltaTime;
            isGrounded = false;
        }
        else
        {
            bool ccGrounded = characterController.isGrounded;
            bool sphereGrounded = false;

            Vector3 sphereOrigin = transform.position + Vector3.up * 0.2f;
            if (Physics.SphereCast(sphereOrigin, characterController.radius * 0.8f, Vector3.down, out RaycastHit hit, 0.35f, ~0, QueryTriggerInteraction.Ignore))
            {
                sphereGrounded = true;
            }

            isGrounded = ccGrounded || (sphereGrounded && verticalVelocity <= 0.1f);
        }

        if (isGrounded && verticalVelocity <= 0f)
        {
            verticalVelocity = groundedStickForce;
        }
        else
        {
            verticalVelocity -= gravity * Time.deltaTime;
        }
    }

    /// <summary>
    /// Executes jump physics and triggers jump animation.
    /// </summary>
    private void ExecuteJump()
    {
        verticalVelocity = Mathf.Sqrt(2f * jumpHeight * gravity);
        isGrounded = false;
        airborneGraceTimer = 0.2f;

        if (animator != null)
        {
            if (hasJumpTrigger)
            {
                animator.ResetTrigger(jumpTriggerHash);
                animator.SetTrigger(jumpTriggerHash);
            }
            if (hasIsGrounded)
            {
                animator.SetBool(isGroundedHash, false);
            }
        }
    }

    /// <summary>
    /// Updates the Animator with current motion parameters.
    /// </summary>
    private void UpdateAnimations(bool isMoving)
    {
        if (animator == null) return;

        if (hasIsGrounded)
        {
            animator.SetBool(isGroundedHash, isGrounded);
        }

        if (hasIsRunning)
        {
            animator.SetBool(isRunningHash, isMoving);
        }

        if (hasIsIdle)
        {
            animator.SetBool(isIdleHash, !isMoving && isGrounded);
        }
    }

    /// <summary>
    /// Smooth third-person camera follow behind the player.
    /// </summary>
    private void UpdateCameraFollow()
    {
        Vector3 targetCamPos = transform.position + transform.rotation * cameraOffset;
        targetCamera.transform.position = Vector3.Lerp(targetCamera.transform.position, targetCamPos, cameraFollowSpeed * Time.deltaTime);

        Vector3 lookTarget = transform.position + Vector3.up * 1.5f;
        Quaternion targetCamRot = Quaternion.LookRotation(lookTarget - targetCamera.transform.position, Vector3.up);
        targetCamera.transform.rotation = Quaternion.Slerp(targetCamera.transform.rotation, targetCamRot, cameraRotationSpeed * Time.deltaTime);
    }

    private void DisableStripeColliders()
    {
        var colliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        foreach (var col in colliders)
        {
            if (col != null && col.gameObject.name.ToLower().Contains("stripe"))
            {
                col.enabled = false;
            }
        }
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
#endif

/// <summary>
/// Running Phase (Endless Runner / Auto-Run)
/// Implements GDD 3.1, 3.2, 3.3:
/// - 3.1 Core Logic:
///   * Auto-run: Moves forward automatically at runSpeed (constant or gently ramping). No stamina, no regen, no exhaustion.
///   * Steering: A/D, Left/Right arrows, InputSystem_Actions, or Android touch swipe in the Left Zone.
///   * Jump: Space, Up arrow, InputSystem_Actions, or Android touch tap in the Right Zone.
///   * Finish: When stageDistance >= runDistance, completes stage and loads Swim_Scene.
/// - 3.2 Scoring:
///   * stageScore = stageDistance (in meters). No bonuses.
/// - 3.3 HUD:
///   * Distance (m), Speed indicator, Android touch zone layout. No stamina bar.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class RunnerController : MonoBehaviour
{
    public enum MovementMode
    {
        ContinuousLateral, // Smooth lateral movement across the track
        LaneBased          // Discrete lane switching
    }

    [Header("Auto-Run Settings (GDD 3.1)")]
    [Tooltip("Forward speed in meters per second.")]
    public float runSpeed = 10.0f;

    [Tooltip("Gentle speed increase per second. Set to 0 for constant speed.")]
    public float speedRampRate = 0.05f;

    [Tooltip("Maximum run speed when ramping.")]
    public float maxRunSpeed = 16.0f;

    [Tooltip("If true, runner follows transform.forward direction.")]
    public bool useTransformForward = true;
    public Vector3 customForwardDirection = Vector3.forward;

    [Header("Steering Settings")]
    [Tooltip("Steering style: Continuous lateral movement or discrete lane switching.")]
    public MovementMode movementMode = MovementMode.ContinuousLateral;

    [Tooltip("Speed of lateral movement in Continuous mode.")]
    public float lateralSpeed = 9.0f;

    [Tooltip("If true, restricts lateral movement within minLateralX and maxLateralX. If false, player can steer freely across the entire road.")]
    public bool clampLateralMovement = false;

    [Tooltip("Left/right boundary limits in Continuous mode (only used if clampLateralMovement is true).")]
    public float minLateralX = -12.0f;
    public float maxLateralX = 12.0f;

    [Tooltip("Distance between lanes in Lane-Based mode (meters).")]
    public float laneDistance = 3.5f;

    [Tooltip("Maximum lane index on each side in Lane-Based mode (e.g. 2 allows 5 lanes: -2, -1, 0, 1, 2).")]
    public int maxLaneIndex = 2;

    [Tooltip("Speed of lane transition interpolation.")]
    public float laneChangeSpeed = 12.0f;

    [Header("Jump & Physics Settings")]
    [Tooltip("Jump peak height in meters (clears low obstacles).")]
    public float jumpHeight = 1.8f;

    [Tooltip("Custom downward gravity acceleration.")]
    public float gravity = 24.0f;

    [Tooltip("Extra gravity multiplier applied while falling for crisp runner jumps.")]
    public float fallMultiplier = 1.2f;

    [Tooltip("Downward stick force applied when grounded to keep character on slopes.")]
    public float groundedStickForce = -2.0f;

    [Header("Stage Distance & Finish (GDD 3.1)")]
    [Tooltip("Fixed distance in meters for the running stage.")]
    public float runDistance = 500.0f;

    [Tooltip("Name of the next scene to load upon stage completion.")]
    public string nextSceneName = "Swim_Scene";

    [Header("Animation Parameters (Player_Controller)")]
    [Tooltip("Reference to the Animator component (uses Player_Controller).")]
    public Animator animator;

    [Tooltip("Bool parameter for auto-run state.")]
    public string isRunningParam = "isRunning";

    [Tooltip("Bool parameter for ground contact state.")]
    public string isGroundedParam = "isGrounded";

    [Tooltip("Trigger parameter for initiating jump.")]
    public string jumpTriggerParam = "Jump";

    [Header("Input System Actions (Assets/InputSystem_Actions)")]
    [Tooltip("Reference to InputSystem_Actions asset. Auto-linked if found.")]
#if ENABLE_INPUT_SYSTEM
    public InputActionAsset inputActions;
    private InputAction moveAction;
    private InputAction jumpAction;
#endif

    [Header("HUD & Android Touch Controls (GDD 3.3)")]
    [Tooltip("Show runner HUD (Distance, Speed, Touch Zones). No stamina bar.")]
    public bool showHUD = true;

    [Tooltip("Enable touch swipe in Left Zone and tap in Right Zone for Android.")]
    public bool enableTouchControls = true;

    [Tooltip("Horizontal pixel swipe distance required to trigger lane shift in Lane mode.")]
    public float swipeThresholdPixels = 40.0f;

    [Tooltip("Sensitivity of touch drag in Continuous mode.")]
    public float touchDragSensitivity = 1.5f;

    // Runtime state
    private CharacterController characterController;
    private float currentSpeed;
    private float verticalVelocity;
    private bool isGrounded;
    private bool isRunningActive = true;
    private bool stageCompleted = false;

    // Distance & Score (GDD 3.2)
    private float stageDistance = 0.0f;
    private float stageTime = 0.0f;
    private Vector3 lastPosition;

    // Lateral steering state
    private float lateralOffset = 0.0f;
    private int currentLane = 0; // -2, -1, 0, 1, 2
    private float targetLateralX = 0.0f;
    private float currentLateralX = 0.0f;
    private bool laneShiftInputHeld = false;

    // Animator hash caching
    private int isRunningHash;
    private int isGroundedHash;
    private int jumpTriggerHash;

    // Mobile touch tracking (Android)
    private int steerFingerId = -1;
    private Vector2 steerTouchStartPos;
    private bool isSteerTouching = false;
    private float touchSteerInput = 0.0f;

    // Ground check grace timer for smooth jump animation transitions
    private float airborneTimer = 0.0f;

#if UNITY_EDITOR
    private void Reset()
    {
        AutoFindInputActions();
    }

    private void OnValidate()
    {
        if (inputActions == null)
        {
            AutoFindInputActions();
        }
    }

    private void AutoFindInputActions()
    {
#if ENABLE_INPUT_SYSTEM
        if (inputActions == null)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("InputSystem_Actions t:InputActionAsset");
            if (guids != null && guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                inputActions = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            }
        }
#endif
    }
#endif

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            characterController = gameObject.AddComponent<CharacterController>();
            characterController.center = new Vector3(0f, 0.9f, 0f);
            characterController.height = 1.8f;
            characterController.radius = 0.35f;
            characterController.skinWidth = 0.05f;
            characterController.stepOffset = 0.3f;
            characterController.minMoveDistance = 0.0f;
        }

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator != null)
        {
            animator.applyRootMotion = false;
            isRunningHash = Animator.StringToHash(isRunningParam);
            isGroundedHash = Animator.StringToHash(isGroundedParam);
            jumpTriggerHash = Animator.StringToHash(jumpTriggerParam);
        }

        currentSpeed = runSpeed;
        currentLateralX = 0.0f;
        targetLateralX = 0.0f;
        lastPosition = transform.position;

        DisableRoadStripeColliders();
        SetupInputActions();
    }

    /// <summary>
    /// Disables any solid MeshColliders on visual road stripes that could block player lateral movement.
    /// </summary>
    private void DisableRoadStripeColliders()
    {
        var allColliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        foreach (var col in allColliders)
        {
            if (col != null && col.gameObject.name.ToLower().Contains("stripe"))
            {
                col.enabled = false;
            }
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
                if (asset.name.Contains("InputSystem_Actions"))
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

    private void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        EnhancedTouchSupport.Enable();
        if (inputActions != null)
        {
            var playerMap = inputActions.FindActionMap("Player");
            playerMap?.Enable();
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
        EnhancedTouchSupport.Disable();
#endif
    }

    private void Start()
    {
        // Immediately start running animation
        if (animator != null)
        {
            animator.SetBool(isRunningHash, true);
            animator.SetBool(isGroundedHash, true);
        }
    }

    private void Update()
    {
        if (stageCompleted || !isRunningActive)
        {
            if (animator != null)
                animator.SetBool(isRunningHash, false);
            return;
        }

        stageTime += Time.deltaTime;

        // 1. Gently ramp speed over time (if speedRampRate > 0)
        if (speedRampRate > 0f && currentSpeed < maxRunSpeed)
        {
            currentSpeed = Mathf.Min(currentSpeed + speedRampRate * Time.deltaTime, maxRunSpeed);
        }

        // 2. Ground detection
        CheckGrounded();

        // 3. Process inputs (InputSystem_Actions / Keyboard / Android Touch)
        ReadInputs();

        // 4. Calculate movement vectors
        Vector3 moveVector = CalculateMovement();

        // 5. Apply movement via CharacterController
        characterController.Move(moveVector * Time.deltaTime);

        // 6. Track distance traveled (horizontal meters only)
        Vector3 displacement = transform.position - lastPosition;
        displacement.y = 0f;
        stageDistance += displacement.magnitude;
        lastPosition = transform.position;

        // 7. Check Finish condition: stageDistance >= runDistance (GDD 3.1)
        if (stageDistance >= runDistance && !stageCompleted)
        {
            CompleteStage();
        }

        // 8. Update animator state
        UpdateAnimator();
    }

    /// <summary>
    /// Evaluates grounded state using CharacterController and backup spherecast.
    /// </summary>
    private void CheckGrounded()
    {
        bool ccGrounded = characterController.isGrounded;

        bool rayGrounded = false;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.2f;
        if (Physics.SphereCast(rayOrigin, characterController.radius * 0.8f, Vector3.down, out RaycastHit hit, 0.35f, ~0, QueryTriggerInteraction.Ignore))
        {
            rayGrounded = true;
        }

        if (airborneTimer > 0f)
        {
            airborneTimer -= Time.deltaTime;
            isGrounded = false;
        }
        else
        {
            isGrounded = ccGrounded || (rayGrounded && verticalVelocity <= 0.1f);
        }

        if (isGrounded && verticalVelocity <= 0.0f)
        {
            verticalVelocity = groundedStickForce;
        }
        else
        {
            float currentGravity = gravity;
            if (verticalVelocity < 0.0f)
            {
                currentGravity *= fallMultiplier;
            }
            verticalVelocity -= currentGravity * Time.deltaTime;
        }
    }

    /// <summary>
    /// Reads steering and jump inputs from InputSystem_Actions, Keyboard, and Android Touchscreen.
    /// </summary>
    private void ReadInputs()
    {
        float steerX = 0f;
        bool jumpPressed = false;

#if ENABLE_INPUT_SYSTEM
        // 1. READ FROM InputSystem_Actions ("Player/Move" and "Player/Jump")
        if (moveAction != null)
        {
            Vector2 moveVec = moveAction.ReadValue<Vector2>();
            steerX = moveVec.x;
        }
        else
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steerX -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steerX += 1f;
            }
            var gp = Gamepad.current;
            if (gp != null)
            {
                float stickX = gp.leftStick.x.ReadValue();
                if (Mathf.Abs(stickX) > 0.15f) steerX += stickX;
            }
        }

        if (jumpAction != null)
        {
            if (jumpAction.WasPressedThisFrame())
            {
                jumpPressed = true;
            }
        }
        else
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame))
            {
                jumpPressed = true;
            }
            var gp = Gamepad.current;
            if (gp != null && gp.buttonSouth.wasPressedThisFrame)
            {
                jumpPressed = true;
            }
        }

        // 2. Android Touch Controls (Left zone swipe, Right zone tap)
        if (enableTouchControls)
        {
            ReadAndroidTouchInputs(ref steerX, ref jumpPressed);
        }
#else
        steerX = Input.GetAxisRaw("Horizontal");
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            jumpPressed = true;
        }

        if (enableTouchControls)
        {
            ReadLegacyTouchInputs(ref steerX, ref jumpPressed);
        }
#endif

        // Process Steering
        if (movementMode == MovementMode.LaneBased)
        {
            if (steerX < -0.4f && !laneShiftInputHeld)
            {
                ShiftLane(-1);
                laneShiftInputHeld = true;
            }
            else if (steerX > 0.4f && !laneShiftInputHeld)
            {
                ShiftLane(1);
                laneShiftInputHeld = true;
            }
            else if (Mathf.Abs(steerX) <= 0.2f)
            {
                laneShiftInputHeld = false;
            }
        }
        else
        {
            // Continuous lateral steering
            float combinedSteer = steerX + touchSteerInput;
            targetLateralX += combinedSteer * lateralSpeed * Time.deltaTime;
            if (clampLateralMovement)
            {
                targetLateralX = Mathf.Clamp(targetLateralX, minLateralX, maxLateralX);
            }
        }

        // Process Jump
        if (jumpPressed)
        {
            TryJump();
        }
    }

#if ENABLE_INPUT_SYSTEM
    /// <summary>
    /// Android Touch Inputs:
    /// - Left Zone (X < 50% width): Swipe/drag steering
    /// - Right Zone (X >= 50% width): Tap to jump
    /// </summary>
    private void ReadAndroidTouchInputs(ref float outSteer, ref bool outJump)
    {
        touchSteerInput = 0f;
        float halfScreenWidth = Screen.width * 0.5f;

        foreach (var touch in Touch.activeTouches)
        {
            Vector2 screenPos = touch.screenPosition;

            // --- LEFT ZONE: SWIPE TO STEER ---
            if (screenPos.x < halfScreenWidth)
            {
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    steerFingerId = touch.touchId;
                    steerTouchStartPos = screenPos;
                    isSteerTouching = true;
                }
                else if (touch.touchId == steerFingerId &&
                        (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved || touch.phase == UnityEngine.InputSystem.TouchPhase.Stationary))
                {
                    float deltaX = screenPos.x - steerTouchStartPos.x;

                    if (movementMode == MovementMode.LaneBased)
                    {
                        if (deltaX < -swipeThresholdPixels)
                        {
                            ShiftLane(-1);
                            steerTouchStartPos = screenPos;
                        }
                        else if (deltaX > swipeThresholdPixels)
                        {
                            ShiftLane(1);
                            steerTouchStartPos = screenPos;
                        }
                    }
                    else
                    {
                        float normalizedDelta = deltaX / (Screen.width * 0.25f);
                        touchSteerInput = Mathf.Clamp(normalizedDelta * touchDragSensitivity, -1f, 1f);
                    }
                }
                else if (touch.touchId == steerFingerId &&
                        (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended || touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled))
                {
                    isSteerTouching = false;
                    steerFingerId = -1;
                }
            }
            // --- RIGHT ZONE: TAP TO JUMP ---
            else
            {
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    outJump = true;
                }
            }
        }

        // Editor mouse simulation for testing Android controls without a device
#if UNITY_EDITOR
        if (Touch.activeTouches.Count == 0 && UnityEngine.InputSystem.Mouse.current != null)
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            Vector2 mousePos = mouse.position.ReadValue();

            if (mousePos.x < halfScreenWidth)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    steerTouchStartPos = mousePos;
                    isSteerTouching = true;
                }
                else if (mouse.leftButton.isPressed && isSteerTouching)
                {
                    float deltaX = mousePos.x - steerTouchStartPos.x;
                    if (movementMode == MovementMode.LaneBased)
                    {
                        if (deltaX < -swipeThresholdPixels)
                        {
                            ShiftLane(-1);
                            steerTouchStartPos = mousePos;
                        }
                        else if (deltaX > swipeThresholdPixels)
                        {
                            ShiftLane(1);
                            steerTouchStartPos = mousePos;
                        }
                    }
                    else
                    {
                        float normalizedDelta = deltaX / (Screen.width * 0.25f);
                        touchSteerInput = Mathf.Clamp(normalizedDelta * touchDragSensitivity, -1f, 1f);
                    }
                }
                else if (mouse.leftButton.wasReleasedThisFrame)
                {
                    isSteerTouching = false;
                }
            }
            else
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    outJump = true;
                }
            }
        }
#endif
    }
#else
    private void ReadLegacyTouchInputs(ref float outSteer, ref bool outJump)
    {
        float halfScreenWidth = Screen.width * 0.5f;

        for (int i = 0; i < Input.touchCount; i++)
        {
            UnityEngine.Touch t = Input.GetTouch(i);
            if (t.position.x < halfScreenWidth)
            {
                if (t.phase == UnityEngine.TouchPhase.Began)
                {
                    steerTouchStartPos = t.position;
                }
                else if (t.phase == UnityEngine.TouchPhase.Moved || t.phase == UnityEngine.TouchPhase.Stationary)
                {
                    float deltaX = t.position.x - steerTouchStartPos.x;
                    if (movementMode == MovementMode.LaneBased)
                    {
                        if (deltaX < -swipeThresholdPixels) { ShiftLane(-1); steerTouchStartPos = t.position; }
                        else if (deltaX > swipeThresholdPixels) { ShiftLane(1); steerTouchStartPos = t.position; }
                    }
                    else
                    {
                        touchSteerInput = Mathf.Clamp((deltaX / (Screen.width * 0.25f)) * touchDragSensitivity, -1f, 1f);
                    }
                }
            }
            else
            {
                if (t.phase == UnityEngine.TouchPhase.Began)
                {
                    outJump = true;
                }
            }
        }
    }
#endif

    /// <summary>
    /// Shifts discrete lane left (-1) or right (+1).
    /// </summary>
    public void ShiftLane(int direction)
    {
        int targetLane = Mathf.Clamp(currentLane + direction, -maxLaneIndex, maxLaneIndex);
        if (targetLane != currentLane)
        {
            currentLane = targetLane;
            targetLateralX = currentLane * laneDistance;
        }
    }

    /// <summary>
    /// Performs jump if grounded.
    /// </summary>
    public void TryJump()
    {
        if (!isGrounded || stageCompleted) return;

        verticalVelocity = Mathf.Sqrt(2.0f * gravity * jumpHeight);
        isGrounded = false;
        airborneTimer = 0.1f;

        if (animator != null)
        {
            animator.SetTrigger(jumpTriggerHash);
        }
    }

    /// <summary>
    /// Calculates combined 3D motion: forward auto-run, lateral steering, and vertical gravity/jump.
    /// </summary>
    private Vector3 CalculateMovement()
    {
        Vector3 forwardVec = useTransformForward ? transform.forward : customForwardDirection.normalized;
        Vector3 forwardMove = forwardVec * currentSpeed;

        if (movementMode == MovementMode.LaneBased)
        {
            currentLateralX = Mathf.MoveTowards(currentLateralX, targetLateralX, laneChangeSpeed * Time.deltaTime);
        }
        else
        {
            currentLateralX = Mathf.MoveTowards(currentLateralX, targetLateralX, lateralSpeed * Time.deltaTime);
        }

        Vector3 rightVec = useTransformForward ? transform.right : Vector3.right;
        Vector3 lateralMove = rightVec * ((currentLateralX - lateralOffset) / Time.deltaTime);
        lateralOffset = currentLateralX;

        Vector3 verticalMove = Vector3.up * verticalVelocity;

        return forwardMove + lateralMove + verticalMove;
    }

    /// <summary>
    /// Updates required Animator parameters (isRunning, isGrounded).
    /// </summary>
    private void UpdateAnimator()
    {
        if (animator == null) return;

        animator.SetBool(isRunningHash, isRunningActive && !stageCompleted);
        animator.SetBool(isGroundedHash, isGrounded);
    }

    /// <summary>
    /// Stops the player upon crossing finish line or completing distance.
    /// </summary>
    public void StopRunning()
    {
        isRunningActive = false;
        verticalVelocity = 0f;
        if (animator != null)
        {
            animator.SetBool(isRunningHash, false);
            animator.SetBool(isGroundedHash, true);
        }
    }

    /// <summary>
    /// Completes the running stage (GDD 3.1 & 3.2):
    /// - Records stageScore = stageDistance in meters.
    /// - Transitions to Swim_Scene.
    /// </summary>
    public void CompleteStage()
    {
        if (stageCompleted) return;
        stageCompleted = true;

        StopRunning();

        // 3.2 Score: stageScore = stageDistance (meters). No bonuses.
        float stageScore = stageDistance;
        if (GameManager.I != null)
        {
            GameManager.I.totalScore += stageScore;
            GameManager.I.totalTime += stageTime;
        }

        Debug.Log($"[Running Complete] Stage finished! Distance: {stageDistance:F1}m, Time: {stageTime:F1}s, Score: {stageScore:F0}. Transitioning to: {nextSceneName}");

        // Transition to Swim_Scene
        if (TransitionManager.Instance != null)
        {
            TransitionManager.Instance.BeginTransition(
                nextScene: nextSceneName,
                onComplete: () => {
                    if (GameManager.I != null)
                        GameManager.I.LoadScene(nextSceneName);
                    else
                        SceneManager.LoadScene(nextSceneName);
                }
            );
        }
        else
        {
            if (GameManager.I != null)
                GameManager.I.LoadScene(nextSceneName);
            else
                SceneManager.LoadScene(nextSceneName);
        }
    }

    public float CurrentSpeed => currentSpeed;
    public float StageDistance => stageDistance;
    public float RunDistance => runDistance;
    public bool IsGrounded => isGrounded;
    public int CurrentLane => currentLane;
    public bool StageCompleted => stageCompleted;

    private void OnGUI()
    {
        if (!showHUD) return;

        float screenW = Screen.width;
        float screenH = Screen.height;
        float halfW = screenW * 0.5f;

        // 3.3 HUD: Distance (m), Speed indicator optional. No stamina bar.
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Clamp(Screen.height / 30, 16, 24),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft
        };
        titleStyle.normal.textColor = Color.white;

        // Distance Display
        float displayDistance = Mathf.Min(stageDistance, runDistance);
        GUI.Label(new Rect(30, 25, 400, 35), $"DISTANCE: {displayDistance:F0}m / {runDistance:F0}m", titleStyle);

        // Progress Bar
        float progress = Mathf.Clamp01(stageDistance / runDistance);
        float barWidth = Mathf.Clamp(screenW * 0.35f, 200, 450);
        float barHeight = 12;
        Rect barBgRect = new Rect(30, 65, barWidth, barHeight);
        Rect barFillRect = new Rect(30, 65, barWidth * progress, barHeight);

        Color prevColor = GUI.color;
        GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.7f);
        GUI.DrawTexture(barBgRect, Texture2D.whiteTexture);
        GUI.color = new Color(0.2f, 0.85f, 0.3f, 0.9f);
        GUI.DrawTexture(barFillRect, Texture2D.whiteTexture);

        // Speed Display (optional, per GDD 3.3)
        GUI.color = Color.white;
        GUIStyle speedStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Clamp(Screen.height / 40, 13, 18),
            alignment = TextAnchor.UpperLeft
        };
        speedStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 0.85f);
        GUI.Label(new Rect(30, 82, 300, 30), $"Speed: {currentSpeed:F1} m/s", speedStyle);

        // Stage Complete Message
        if (stageCompleted)
        {
            GUIStyle completeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Clamp(Screen.height / 20, 24, 40),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            completeStyle.normal.textColor = new Color(1f, 0.85f, 0.1f, 1f);
            GUI.Label(new Rect(0, screenH * 0.35f, screenW, 50), "★ STAGE COMPLETE! ★", completeStyle);

            GUIStyle subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Clamp(Screen.height / 32, 16, 24),
                alignment = TextAnchor.MiddleCenter
            };
            subStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(0, screenH * 0.35f + 55, screenW, 40), "Entering Swim Stage...", subStyle);
        }

        // Android Touch Controls layout indicators
        if (enableTouchControls && !stageCompleted)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.12f);
            // Center separator line
            GUI.DrawTexture(new Rect(halfW - 1, screenH * 0.5f, 2, screenH * 0.5f), Texture2D.whiteTexture);

            GUI.color = new Color(1f, 1f, 1f, 0.4f);
            GUIStyle touchStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Clamp(Screen.height / 38, 12, 18),
                alignment = TextAnchor.LowerCenter
            };
            touchStyle.normal.textColor = new Color(1f, 1f, 1f, 0.5f);

            GUI.Label(new Rect(0, screenH - 45, halfW, 35), "◀ LEFT ZONE: Swipe to Steer ▶", touchStyle);
            GUI.Label(new Rect(halfW, screenH - 45, halfW, 35), "▲ RIGHT ZONE: Tap to Jump", touchStyle);
        }

        GUI.color = prevColor;
    }
}

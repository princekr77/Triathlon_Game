using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
#endif

/// <summary>
/// Swimming Stage (Rhythm Stroke + Oxygen + Streak + Android Touch)
/// Implements GDD Section 4:
/// - 4.1 Core Logic:
///   * Stroke-based movement: each valid press (Space / tap right zone) = one stroke impulse -> forward velocity burst.
///   * Rhythm bar with moving marker provides timing judgment (§2.4).
///   * PERFECT stroke -> larger impulse + streak count + speed burst (1.25x).
///   * GOOD / MISS -> smaller/no impulse, streak resets.
///   * Oxygen = shared stamina bar, displayed as "Oxygen". Starts from carried-over value.
///   * Drain: 2.0/sec while stroking, 1.0/sec while gliding.
///   * Passive regen: +0.5/sec when gliding (rest vs. push decision).
///   * Exhaustion Recovery: fires at stamina <= 0 -> 30% speed + +1.0/sec until 20 oxygen.
///   * Finish line: transitions to "Cycle_Scene".
/// - 4.2 Streak:
///   * 5 consecutive PERFECT -> +5 oxygen, "STREAK!" feedback, streak resets to 0.
///   * Any GOOD / MISS -> streak = 0.
///   * Streak reset to 0 at transition.
/// - 4.3 Scoring:
///   * stageScore = stageDistance (m) + bonuses.
/// - 4.4 HUD:
///   * Distance, Oxygen bar, Rhythm bar with green/yellow zones, Streak counter, Android touch controls.
/// </summary>
public class SwimmerController : MonoBehaviour
{
    public enum TimingResult { None, Perfect, Good, Miss }

    [Header("4.1 Stroke & Speed Configuration")]
    [Tooltip("Base speed impulse on a GOOD stroke (m/s).")]
    public float goodStrokeSpeed = 5.2f;

    [Tooltip("Base speed impulse on a PERFECT stroke (m/s).")]
    public float perfectStrokeSpeed = 8.0f;

    [Tooltip("Speed burst multiplier granted on a PERFECT stroke (1.25x).")]
    public float perfectSpeedBurstMultiplier = 1.25f;

    [Tooltip("Weak speed impulse on a MISS stroke (m/s).")]
    public float missStrokeSpeed = 1.8f;

    [Tooltip("Minimum glide speed when coasting forward in the water (m/s).")]
    public float minGlideSpeed = 1.2f;

    [Tooltip("Water resistance deceleration rate when gliding (m/s^2).")]
    public float glideDeceleration = 2.4f;

    [Tooltip("Duration of active stroking state in seconds after pressing stroke.")]
    public float strokeImpulseDuration = 0.65f;

    [Tooltip("Steering sensitivity for lateral movement.")]
    public float lateralSpeed = 6.0f;

    [Tooltip("Turning rotation smoothing speed.")]
    public float rotationSmoothing = 8.0f;

    [Header("Rhythm Timing Marker (§2.4 & §4.1)")]
    [Tooltip("Speed of rhythm marker oscillation across the bar.")]
    public float rhythmSpeed = 1.25f;

    [Tooltip("Center of the green perfect zone (0.0 to 1.0).")]
    [Range(0.2f, 0.8f)] public float perfectZoneCenter = 0.5f;

    [Tooltip("Half-width of the green perfect zone (PERFECT: within center +/- halfWidth).")]
    [Range(0.03f, 0.15f)] public float perfectZoneHalfWidth = 0.08f;

    [Tooltip("Half-width of the yellow good zone (GOOD: within center +/- halfWidth).")]
    [Range(0.10f, 0.30f)] public float goodZoneHalfWidth = 0.20f;

    [Header("4.1 Oxygen (Stamina) Configuration")]
    [Tooltip("Maximum oxygen capacity.")]
    public float maxOxygen = 100.0f;

    [Tooltip("Oxygen drain rate per second during active stroke impulse (default: 2.0/sec).")]
    public float activeDrainRate = 2.0f;

    [Tooltip("Oxygen drain rate per second while gliding (default: 1.0/sec).")]
    public float glideDrainRate = 1.0f;

    [Tooltip("Passive oxygen regeneration per second during glide state (default: +0.5/sec).")]
    public float passiveRegenRate = 0.5f;

    [Tooltip("Oxygen recovery rate per second during exhaustion (default: +1.0/sec until 20).")]
    public float exhaustionRegenRate = 1.0f;

    [Tooltip("Oxygen threshold required to recover from exhaustion.")]
    public float exhaustionRecoverThreshold = 20.0f;

    [Tooltip("Speed multiplier applied during exhaustion (default: 30%).")]
    public float exhaustionSpeedMultiplier = 0.30f;

    [Header("4.2 Streak Configuration")]
    [Tooltip("Consecutive PERFECT strokes required to trigger a streak reward.")]
    public int streakGoal = 5;

    [Tooltip("Oxygen bonus awarded on completing a streak.")]
    public float streakOxygenReward = 5.0f;

    [Header("4.1 & 4.3 Distance, Finish & Scoring")]
    [Tooltip("Total swim race distance in meters (default: 225m, canal length).")]
    public float targetDistance = 225.0f;

    [Tooltip("Next scene to load when finishing the swim stage.")]
    public string nextScene = "Cycle_Scene";

    [Header("Canal Boundaries (Terrain & Wall Protection)")]
    [Tooltip("If true, strictly clamps swimmer within canal walls and prevents escaping terrain.")]
    public bool clampToCanal = true;
    public float minCanalX = 153.0f;
    public float maxCanalX = 169.5f;
    public float minCanalZ = 246.0f;
    public float maxCanalZ = 472.0f;

    [Header("Water Surface Alignment & Buoyancy")]
    [Tooltip("Target water surface Y level. Auto-detected from Water_Surface if present.")]
    public float waterSurfaceY = 12.6f;

    [Tooltip("Vertical offset to ensure the swimmer properly lays prone on water surface.")]
    public float surfaceOffset = -0.35f;

    [Tooltip("Body pitch tilt in degrees to ensure flat prone swimming posture.")]
    public float bodyPitch = 0.0f;

    [Tooltip("Body roll tilt in degrees.")]
    public float bodyRoll = 0.0f;

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

    [Header("Camera Follow")]
    [Tooltip("If true and no SwimCameraFollow exists on Main Camera, automatically positions and aligns Camera.main.")]
    public bool autoFollowCamera = false;
    public Vector3 cameraOffset = new Vector3(0f, 3.2f, 6.5f);
    public float cameraFollowSpeed = 10.0f;
    public float cameraRotationSpeed = 8.0f;

    [Header("HUD & Touch Display")]
    [Tooltip("Show Section 4.4 HUD overlay (Distance, Oxygen, Rhythm Bar, Streak).")]
    public bool showHUD = true;

    [Tooltip("Show on-screen touch controls for Android / Mobile.")]
    public bool showTouchControls = true;

    // Runtime state
    private Camera mainCam;
    private float currentForwardSpeed = 0.0f;
    private float currentHeadingYaw = 180f; // Facing -Z down the water canal
    private int speedHash;
    private int isSwimmingHash;
    private bool hasSpeedParam;
    private bool hasIsSwimmingParam;

    // Rhythm & Stroke state
    private float rhythmMarkerPos = 0.5f; // Oscillates 0.0 to 1.0
    private float activeStrokeTimer = 0.0f;
    private TimingResult lastTimingResult = TimingResult.None;
    private float timingBannerTimer = 0.0f;

    // Oxygen state
    private float oxygen = 100.0f;
    private bool isExhausted = false;

    // Streak state
    private int perfectStreak = 0;
    private float streakRewardTimer = 0.0f;

    // Distance & Stage
    private float stageDistance = 0.0f;
    private Vector3 lastPosition;
    private bool stageCompleted = false;

    // Lateral touch steer
    private float lateralInput = 0.0f;
    private float touchSteerHeld = 0.0f;

    private void Awake()
    {
        // 1. Resolve Animator
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

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

        // 2. Auto-detect Water_Surface in scene to get exact water height and disable solid blocking
        GameObject waterObj = GameObject.Find("Water_Surface");
        if (waterObj != null)
        {
            waterSurfaceY = waterObj.transform.position.y;
            if (waterObj.TryGetComponent<Collider>(out var waterCol))
            {
                waterCol.isTrigger = true;
            }
        }

        // 3. Ensure kinematic Rigidbody and trigger Collider for PhysX zone interactions
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

        // 4. Initialize Oxygen from carried-over stamina
        if (GameManager.I != null)
        {
            oxygen = Mathf.Clamp(GameManager.I.stamina, 0f, maxOxygen);
            perfectStreak = 0; // Streak reset to 0 at transition (§4.2)
        }
        else
        {
            oxygen = maxOxygen;
        }

        currentHeadingYaw = transform.eulerAngles.y;
        if (Mathf.Abs(currentHeadingYaw) < 1f)
        {
            currentHeadingYaw = 180f;
        }

        lastPosition = transform.position;
    }

    private void OnEnable()
    {
        if (mainCam == null)
        {
            mainCam = Camera.main;
        }

        // Pull latest stamina if GameManager exists
        if (GameManager.I != null)
        {
            oxygen = Mathf.Clamp(GameManager.I.stamina, 0f, maxOxygen);
        }

        // Initial prone placement
        SnapToWaterSurface();
        lastPosition = transform.position;

        if (animator != null)
        {
            if (hasIsSwimmingParam) animator.SetBool(isSwimmingHash, true);
            animator.Play(swimStateName, 0, 0f);
        }

#if ENABLE_INPUT_SYSTEM
        EnhancedTouchSupport.Enable();
#endif
    }

    private void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        EnhancedTouchSupport.Disable();
#endif
    }

    private void Start()
    {
        if (mainCam == null)
        {
            mainCam = Camera.main;
        }

        SnapToWaterSurface();
        lastPosition = transform.position;

        if (animator != null)
        {
            animator.Play(swimStateName, 0, 0f);
        }
    }

    private void Update()
    {
        if (stageCompleted) return;

        // 1. Rhythm Marker Oscillation (0.0 to 1.0)
        rhythmMarkerPos = Mathf.PingPong(Time.time * rhythmSpeed, 1.0f);

        // 2. Read Inputs (Keyboard, Gamepad, Touch)
        ReadInputs();

        // 3. Update Oxygen & Exhaustion Logic (§4.1)
        UpdateOxygen();

        // 4. Update Velocity & Forward Glide Deceleration
        UpdateMovementSpeed();

        // 5. Execute World Movement & Enforce Terrain/Canal Clamping
        ExecuteMovement();

        // 6. Water Surface Alignment & Prone Laying
        AlignWaterSurface();

        // 7. Update Distance Progress & Check Finish Line
        UpdateDistanceAndScore();

        // 8. Timers for UI feedback banners
        if (timingBannerTimer > 0f) timingBannerTimer -= Time.deltaTime;
        if (streakRewardTimer > 0f) streakRewardTimer -= Time.deltaTime;
        if (activeStrokeTimer > 0f) activeStrokeTimer -= Time.deltaTime;

        // 9. Sync Animation Speed
        UpdateAnimationLoop();
    }

    private void LateUpdate()
    {
        if (autoFollowCamera && mainCam != null && mainCam.GetComponent<SwimCameraFollow>() == null)
        {
            UpdateCameraFollow();
        }
    }

    /// <summary>
    /// Reads inputs from Keyboard, Gamepad, and Android Touch.
    /// </summary>
    private void ReadInputs()
    {
        lateralInput = 0f;
        bool strokeTriggered = false;

        // --- Keyboard & Gamepad Inputs ---
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)
            {
                strokeTriggered = true;
            }

            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) lateralInput -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) lateralInput += 1f;
        }

        var gp = Gamepad.current;
        if (gp != null)
        {
            if (gp.buttonSouth.wasPressedThisFrame || gp.rightTrigger.wasPressedThisFrame)
            {
                strokeTriggered = true;
            }
            Vector2 stick = gp.leftStick.ReadValue();
            if (Mathf.Abs(stick.x) > 0.15f) lateralInput += stick.x;
        }

        // --- Android Touch Inputs ---
        if (Touch.activeTouches.Count > 0)
        {
            foreach (var touch in Touch.activeTouches)
            {
                Vector2 pos = touch.screenPosition;

                // Right half of screen: Tap to stroke
                if (pos.x > Screen.width * 0.5f)
                {
                    if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                    {
                        strokeTriggered = true;
                    }
                }
                // Left half of screen: Drag horizontally to steer
                else
                {
                    if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved)
                    {
                        lateralInput += Mathf.Clamp(touch.delta.x * 0.08f, -1f, 1f);
                    }
                }
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            strokeTriggered = true;
        }
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) lateralInput -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) lateralInput += 1f;

        // Legacy Touch
        for (int i = 0; i < Input.touchCount; i++)
        {
            UnityEngine.Touch t = Input.GetTouch(i);
            if (t.position.x > Screen.width * 0.5f && t.phase == UnityEngine.TouchPhase.Began)
            {
                strokeTriggered = true;
            }
            else if (t.position.x <= Screen.width * 0.5f && t.phase == UnityEngine.TouchPhase.Moved)
            {
                lateralInput += Mathf.Clamp(t.deltaPosition.x * 0.08f, -1f, 1f);
            }
        }
#endif

        // On-screen touch buttons override (from GUI)
        if (touchSteerHeld != 0f)
        {
            lateralInput = touchSteerHeld;
        }

        lateralInput = Mathf.Clamp(lateralInput, -1f, 1f);

        if (strokeTriggered)
        {
            PerformStrokeWithTiming();
        }
    }

    /// <summary>
    /// Evaluates rhythm marker position when the player presses stroke.
    /// PERFECT -> speed burst + streak count.
    /// GOOD / MISS -> smaller/no impulse, streak resets.
    /// </summary>
    public void PerformStrokeWithTiming()
    {
        // Calculate distance from center of perfect zone
        float distFromCenter = Mathf.Abs(rhythmMarkerPos - perfectZoneCenter);

        // 1. PERFECT STROKE (Green Zone)
        if (distFromCenter <= perfectZoneHalfWidth)
        {
            lastTimingResult = TimingResult.Perfect;
            timingBannerTimer = 1.2f;

            // Speed burst (1.25x)
            float burstSpeed = perfectStrokeSpeed * (isExhausted ? exhaustionSpeedMultiplier : perfectSpeedBurstMultiplier);
            currentForwardSpeed = Mathf.Max(currentForwardSpeed, burstSpeed);
            activeStrokeTimer = strokeImpulseDuration;

            // Streak tracking (§4.2)
            perfectStreak++;
            if (perfectStreak >= streakGoal)
            {
                // 5 consecutive PERFECT -> +5 oxygen, "STREAK!" feedback, streak resets
                oxygen = Mathf.Min(maxOxygen, oxygen + streakOxygenReward);
                streakRewardTimer = 2.0f;
                perfectStreak = 0;
                Debug.Log("[SwimmerController] 5 PERFECT STREAK! +5 Oxygen awarded!");
            }
        }
        // 2. GOOD STROKE (Yellow Zone)
        else if (distFromCenter <= goodZoneHalfWidth)
        {
            lastTimingResult = TimingResult.Good;
            timingBannerTimer = 1.0f;

            float strokeSpeed = goodStrokeSpeed * (isExhausted ? exhaustionSpeedMultiplier : 1.0f);
            currentForwardSpeed = Mathf.Max(currentForwardSpeed, strokeSpeed);
            activeStrokeTimer = strokeImpulseDuration;

            // Any GOOD resets streak (§4.2)
            perfectStreak = 0;
        }
        // 3. MISS (Red Zone / Outside)
        else
        {
            lastTimingResult = TimingResult.Miss;
            timingBannerTimer = 1.0f;

            float strokeSpeed = missStrokeSpeed * (isExhausted ? exhaustionSpeedMultiplier : 1.0f);
            currentForwardSpeed = Mathf.Max(currentForwardSpeed, strokeSpeed);
            activeStrokeTimer = strokeImpulseDuration * 0.4f;

            // Any MISS resets streak (§4.2)
            perfectStreak = 0;
        }

        // Trigger swimming animator stroke
        if (animator != null)
        {
            animator.Play(swimStateName, 0, 0f);
        }
    }

    /// <summary>
    /// Handles Oxygen drain, passive glide regen, and exhaustion recovery (§4.1).
    /// </summary>
    private void UpdateOxygen()
    {
        // 1. If Exhausted (stamina <= 0):
        if (isExhausted)
        {
            // Recover +1.0/sec until 20 oxygen
            oxygen += exhaustionRegenRate * Time.deltaTime;
            if (oxygen >= exhaustionRecoverThreshold)
            {
                isExhausted = false;
                Debug.Log("[SwimmerController] Recovered from exhaustion!");
            }
        }
        else
        {
            // 2. Active Stroking: drain 2.0/sec
            if (activeStrokeTimer > 0f)
            {
                oxygen -= activeDrainRate * Time.deltaTime;
            }
            // 3. Gliding (no stroke input active): glide drain 1.0/sec + passive regen 0.5/sec (net: -0.5/sec)
            else
            {
                oxygen -= (glideDrainRate - passiveRegenRate) * Time.deltaTime;
            }

            // Check for exhaustion trigger
            if (oxygen <= 0f)
            {
                oxygen = 0f;
                isExhausted = true;
                Debug.Log("[SwimmerController] Swimmer is EXHAUSTED! Speed reduced to 30%, recovering +1/s...");
            }
        }

        oxygen = Mathf.Clamp(oxygen, 0f, maxOxygen);

        // Sync with GameManager
        if (GameManager.I != null)
        {
            GameManager.I.stamina = oxygen;
            GameManager.I.recovering = isExhausted;
            GameManager.I.perfectStreak = perfectStreak;
        }
    }

    /// <summary>
    /// Decelerates forward velocity smoothly towards glide speed with water drag.
    /// </summary>
    private void UpdateMovementSpeed()
    {
        float targetDecelSpeed = isExhausted ? (minGlideSpeed * exhaustionSpeedMultiplier) : minGlideSpeed;

        if (currentForwardSpeed > targetDecelSpeed)
        {
            currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, targetDecelSpeed, glideDeceleration * Time.deltaTime);
        }
        else
        {
            currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, targetDecelSpeed, 4.0f * Time.deltaTime);
        }

        // Cap speed if exhausted
        if (isExhausted)
        {
            currentForwardSpeed = Mathf.Min(currentForwardSpeed, perfectStrokeSpeed * exhaustionSpeedMultiplier);
        }
    }

    /// <summary>
    /// Applies movement and strictly clamps the swimmer within the canal boundaries (preventing escaping terrain).
    /// </summary>
    private void ExecuteMovement()
    {
        // Heading is -Z along the water canal
        Vector3 forwardMove = -Vector3.forward * (currentForwardSpeed * Time.deltaTime);
        // Note: Facing -Z down the canal means Screen-Left is +X and Screen-Right is -X.
        // Therefore, pressing 'A' (lateralInput = -1) moves +X (Left), and 'D' (lateralInput = +1) moves -X (Right).
        Vector3 lateralMove = -Vector3.right * (lateralInput * lateralSpeed * Time.deltaTime);
        Vector3 pos = transform.position + forwardMove + lateralMove;

        // Strict boundary protection: Clamps swimmer to canal walls and prevents escaping terrain
        if (clampToCanal)
        {
            pos.x = Mathf.Clamp(pos.x, minCanalX, maxCanalX);
            pos.z = Mathf.Clamp(pos.z, minCanalZ, maxCanalZ);
        }

        transform.position = pos;

        // Orientation: Lay flat horizontally on water with gentle stroke roll.
        // Turning Left (lateralInput = -1) turns yaw towards 90 deg (+X).
        // Turning Right (lateralInput = +1) turns yaw towards 270 deg (-X).
        float steerYawAngle = currentHeadingYaw + (lateralInput * 12.0f);
        float rollSway = (enableWaterBobbing && currentForwardSpeed > 0.1f)
            ? Mathf.Sin(Time.time * (bobbingFrequency * 0.5f)) * rollSwayAmplitude
            : 0f;

        Quaternion targetRotation = Quaternion.Euler(bodyPitch, steerYawAngle, bodyRoll + rollSway);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSmoothing * Time.deltaTime);
    }

    /// <summary>
    /// Snaps and locks the swimmer prone horizontally on the water surface with realistic buoyancy bobbing.
    /// </summary>
    private void AlignWaterSurface()
    {
        Vector3 pos = transform.position;
        float targetY = waterSurfaceY + surfaceOffset;
        float bobbing = 0f;

        if (enableWaterBobbing && currentForwardSpeed > 0.1f)
        {
            bobbing = Mathf.Sin(Time.time * bobbingFrequency) * bobbingAmplitude;
        }

        pos.y = targetY + bobbing;
        transform.position = pos;
    }

    /// <summary>
    /// Snaps position immediately to water surface.
    /// </summary>
    public void SnapToWaterSurface()
    {
        Vector3 pos = transform.position;
        pos.y = waterSurfaceY + surfaceOffset;
        if (clampToCanal)
        {
            pos.x = Mathf.Clamp(pos.x, minCanalX, maxCanalX);
            pos.z = Mathf.Clamp(pos.z, minCanalZ, maxCanalZ);
        }
        transform.position = pos;
        transform.rotation = Quaternion.Euler(bodyPitch, currentHeadingYaw, bodyRoll);
    }

    /// <summary>
    /// Updates distance traveled, score, and triggers finish when target distance is reached (§4.3 & §8).
    /// </summary>
    private void UpdateDistanceAndScore()
    {
        float movedZ = Mathf.Abs(transform.position.z - lastPosition.z);
        stageDistance += movedZ;
        lastPosition = transform.position;

        if (GameManager.I != null)
        {
            GameManager.I.totalScore += movedZ;
            GameManager.I.totalTime += Time.deltaTime;
        }

        // Check if finished by target distance or reached canal end
        if (stageDistance >= targetDistance || transform.position.z <= (minCanalZ + 2.0f))
        {
            var transitionMgr = SwimTransitionManager.Instance != null ? SwimTransitionManager.Instance : FindFirstObjectByType<SwimTransitionManager>();
            if (transitionMgr != null)
            {
                // Swimmer reached exit end - display "Click E to exit swim" prompt and await 'E' press
                transitionMgr.PromptExitSwim();
            }
            else
            {
                // Direct finish transition to Cycle_Scene if no manager is present
                CompleteSwimStage();
            }
        }
    }

    /// <summary>
    /// Completes the swim stage and transitions to Cycle_Scene.
    /// </summary>
    public void CompleteSwimStage()
    {
        if (stageCompleted) return;
        stageCompleted = true;

        Debug.Log($"[SwimmerController] Swim stage complete! Distance: {stageDistance:F1}m. Transitioning to '{nextScene}'...");

        // Reset streak at transition (§4.2)
        perfectStreak = 0;
        if (GameManager.I != null)
        {
            GameManager.I.perfectStreak = 0;
            GameManager.I.stamina = oxygen;
        }

        if (TransitionManager.Instance != null)
        {
            TransitionManager.Instance.BeginTransition(
                nextScene: nextScene,
                onComplete: () =>
                {
                    if (GameManager.I != null)
                        GameManager.I.LoadScene(nextScene);
                    else
                        SceneManager.LoadScene(nextScene);
                }
            );
        }
        else
        {
            if (GameManager.I != null)
                GameManager.I.LoadScene(nextScene);
            else
                SceneManager.LoadScene(nextScene);
        }
    }

    /// <summary>
    /// Updates Animator loop and syncs animation playback rate with speed.
    /// </summary>
    private void UpdateAnimationLoop()
    {
        if (animator == null) return;

        var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName(swimStateName))
        {
            if (!stateInfo.loop && stateInfo.normalizedTime >= 1.0f)
            {
                animator.Play(swimStateName, 0, 0f);
            }

            float animSpeed = Mathf.Lerp(0.7f, 1.4f, currentForwardSpeed / (perfectStrokeSpeed * 1.25f + 0.001f));
            animator.speed = animSpeed;

            if (hasSpeedParam)
            {
                animator.SetFloat(speedHash, currentForwardSpeed);
            }
        }
        else
        {
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

    #region OnGUI HUD & Android Touch Controls (Section 4.4)

    private void OnGUI()
    {
        if (!showHUD) return;

        float screenW = Screen.width;
        float screenH = Screen.height;

        // Dynamic 2D scale: tracks whichever dimension is more constrained (width or height)
        // so UI perfectly adapts on both left-right and top-down screen resize without overflowing.
        float scaleW = screenW / 1100f;
        float scaleH = screenH / 650f;
        float uiScale = Mathf.Clamp(Mathf.Min(scaleW, scaleH), 0.55f, 1.45f);

        // --- SECTION 4.4 RESPONSIVE STRETCHED HUD OVERLAY ---
        float hudMarginX = Mathf.Clamp(screenW * 0.025f, 10f, 30f);
        float hudWidth = screenW - (hudMarginX * 2f);
        float hudX = hudMarginX;
        float hudY = Mathf.Clamp(6f * uiScale, 4f, 16f);

        int labelFontSize = Mathf.Clamp(Mathf.RoundToInt(12f * uiScale), 9, 18);
        int subFontSize = Mathf.Clamp(Mathf.RoundToInt(10f * uiScale), 8, 14);
        int feedbackFontSize = Mathf.Clamp(Mathf.RoundToInt(12f * uiScale), 9, 18);

        float labelH = Mathf.Clamp(16f * uiScale, 11f, 24f);
        float progressH = Mathf.Clamp(10f * uiScale, 6f, 16f);
        float rhythmH = Mathf.Clamp(18f * uiScale, 11f, 26f);
        float gap = Mathf.Clamp(3f * uiScale, 1f, 6f);
        float padX = Mathf.Clamp(12f * uiScale, 8f, 22f);
        float padY = Mathf.Clamp(5f * uiScale, 3f, 12f);

        // Exactly calculated inner height to fit all 5 items + bars + gaps perfectly
        float innerHeight = (5f * labelH) + (2f * progressH) + rhythmH + (4f * gap);
        float hudHeight = innerHeight + (padY * 2f);
        Rect hudRect = new Rect(hudX, hudY, hudWidth, hudHeight);

        // Semi-transparent dark background
        GUI.color = new Color(0.08f, 0.10f, 0.16f, 0.90f);
        GUI.DrawTexture(hudRect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(hudRect.x + padX, hudRect.y + padY, hudWidth - (padX * 2f), innerHeight));

        // 1. Distance Header & Bar
        float dist01 = Mathf.Clamp01(stageDistance / targetDistance);
        GUILayout.Label($"<b>Distance:</b> {stageDistance:F0}m / {targetDistance:F0}m  ({dist01 * 100f:F0}%)", GetBoldLabelStyle(labelFontSize, labelH), GUILayout.Height(labelH));
        DrawProgressBar(dist01, new Color(0.2f, 0.75f, 1.0f), progressH);

        GUILayout.Space(gap);

        // 2. Oxygen Bar (Shared Stamina labeled "Oxygen")
        float oxy01 = Mathf.Clamp01(oxygen / maxOxygen);
        Color oxyColor = isExhausted ? Color.red : (oxy01 > 0.45f ? new Color(0.1f, 0.9f, 0.85f) : (oxy01 > 0.2f ? Color.yellow : new Color(1f, 0.35f, 0.35f)));
        string oxyStatus = isExhausted ? "<color=red><b>EXHAUSTED! (+1/s Recovery)</b></color>" : $"{oxygen:F0}%";
        GUILayout.Label($"<b>Oxygen:</b> {oxyStatus}", GetBoldLabelStyle(labelFontSize, labelH), GUILayout.Height(labelH));
        DrawProgressBar(oxy01, oxyColor, progressH);

        GUILayout.Space(gap);

        // 3. Streak Counter (§4.2)
        string stars = "";
        for (int i = 0; i < streakGoal; i++)
        {
            stars += (i < perfectStreak) ? "★ " : "☆ ";
        }
        GUILayout.Label($"<b>Streak:</b> {perfectStreak}/{streakGoal}  <color=yellow>{stars}</color>  <size={subFontSize}>(5 PERFECT -> +5 Oxygen)</size>", GetBoldLabelStyle(labelFontSize, labelH), GUILayout.Height(labelH));

        GUILayout.Space(gap);

        // 4. Rhythm Timing Bar with Green/Yellow Zones & Moving Marker
        GUILayout.Label("<b>Rhythm Timing (Press SPACE / Tap on Green):</b>", GetBoldLabelStyle(labelFontSize, labelH), GUILayout.Height(labelH));
        DrawRhythmTimingBar(rhythmH);

        GUILayout.Space(gap);

        // 5. Timing Feedback Banner
        DrawFeedbackBanner(feedbackFontSize, subFontSize, labelH);

        GUILayout.EndArea();

        // --- ANDROID ON-SCREEN TOUCH CONTROLS ---
        if (showTouchControls)
        {
            DrawAndroidTouchHUD(screenW, screenH, uiScale);
        }
    }

    private void DrawFeedbackBanner(int feedbackFontSize, int subFontSize, float labelH)
    {
        if (streakRewardTimer > 0f)
        {
            GUI.color = Color.yellow;
            GUILayout.Label("★ <b>STREAK COMPLETED! +5 OXYGEN REWARD!</b> ★", GetBoldLabelStyle(feedbackFontSize, labelH), GUILayout.Height(labelH));
            GUI.color = Color.white;
        }
        else if (timingBannerTimer > 0f)
        {
            if (lastTimingResult == TimingResult.Perfect)
            {
                GUI.color = Color.green;
                GUILayout.Label("★ <b>PERFECT STROKE! (+25% SPEED BURST)</b> ★", GetBoldLabelStyle(feedbackFontSize, labelH), GUILayout.Height(labelH));
                GUI.color = Color.white;
            }
            else if (lastTimingResult == TimingResult.Good)
            {
                GUI.color = Color.yellow;
                GUILayout.Label("✔ <b>GOOD STROKE</b>", GetBoldLabelStyle(feedbackFontSize, labelH), GUILayout.Height(labelH));
                GUI.color = Color.white;
            }
            else if (lastTimingResult == TimingResult.Miss)
            {
                GUI.color = new Color(1f, 0.4f, 0.4f);
                GUILayout.Label("✘ <b>MISS! (Hit when Marker is on Green)</b>", GetBoldLabelStyle(feedbackFontSize, labelH), GUILayout.Height(labelH));
                GUI.color = Color.white;
            }
        }
        else
        {
            // Subtle guidance line to keep HUD height stable
            GUI.color = new Color(0.75f, 0.85f, 0.95f, 0.65f);
            GUILayout.Label($"<size={subFontSize}>Glide Rest: +0.5/s  |  Stroke Drain: -2.0/s  |  Press SPACE / Tap right to stroke</size>", GetBoldLabelStyle(subFontSize, labelH), GUILayout.Height(labelH));
            GUI.color = Color.white;
        }
    }

    private void DrawProgressBar(float fill01, Color fillColor, float height)
    {
        Rect r = GUILayoutUtility.GetRect(20, height, GUILayout.Height(height));
        // Background
        GUI.color = new Color(0.12f, 0.12f, 0.18f, 0.95f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);

        // Fill
        GUI.color = fillColor;
        Rect fillRect = new Rect(r.x, r.y, r.width * Mathf.Clamp01(fill01), r.height);
        GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private void DrawRhythmTimingBar(float height)
    {
        Rect barRect = GUILayoutUtility.GetRect(24, height, GUILayout.Height(height));

        // Bar background
        GUI.color = new Color(0.12f, 0.14f, 0.20f, 0.95f);
        GUI.DrawTexture(barRect, Texture2D.whiteTexture);

        // Yellow Good Zone
        float goodX = barRect.x + (perfectZoneCenter - goodZoneHalfWidth) * barRect.width;
        float goodW = (goodZoneHalfWidth * 2f) * barRect.width;
        GUI.color = new Color(0.85f, 0.75f, 0.15f, 0.85f);
        GUI.DrawTexture(new Rect(goodX, barRect.y, goodW, barRect.height), Texture2D.whiteTexture);

        // Green Perfect Zone
        float greenX = barRect.x + (perfectZoneCenter - perfectZoneHalfWidth) * barRect.width;
        float greenW = (perfectZoneHalfWidth * 2f) * barRect.width;
        GUI.color = new Color(0.1f, 0.95f, 0.35f, 0.95f);
        GUI.DrawTexture(new Rect(greenX, barRect.y, greenW, barRect.height), Texture2D.whiteTexture);

        // Moving Marker (Red Line Pointer)
        float markerWidth = Mathf.Clamp(barRect.width * 0.012f, 4f, 10f);
        float markerX = barRect.x + (rhythmMarkerPos * barRect.width) - (markerWidth * 0.5f);
        GUI.color = Color.red;
        GUI.DrawTexture(new Rect(markerX, barRect.y - 2f, markerWidth, barRect.height + 4f), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private void DrawAndroidTouchHUD(float screenW, float screenH, float uiScale)
    {
        float btnW = Mathf.Clamp(95f * uiScale, 55f, 150f);
        float btnH = Mathf.Clamp(46f * uiScale, 28f, 75f);
        float strokeBtnW = Mathf.Clamp(135f * uiScale, 80f, 210f);
        float strokeBtnH = Mathf.Clamp(56f * uiScale, 34f, 90f);
        float margin = Mathf.Clamp(12f * uiScale, 6f, 22f);

        int navFontSize = Mathf.Clamp(Mathf.RoundToInt(13f * uiScale), 9, 20);
        int strokeFontSize = Mathf.Clamp(Mathf.RoundToInt(14f * uiScale), 10, 22);
        int strokeSubFontSize = Mathf.Clamp(Mathf.RoundToInt(8f * uiScale), 7, 13);

        // Bottom Left: Steering buttons
        Rect leftSteerRect = new Rect(margin, screenH - btnH - margin, btnW, btnH);
        Rect rightSteerRect = new Rect(margin + btnW + (6f * uiScale), screenH - btnH - margin, btnW, btnH);

        GUI.color = new Color(0.12f, 0.18f, 0.28f, 0.85f);
        if (GUI.RepeatButton(leftSteerRect, $"<size={navFontSize}><b>◄ LEFT</b></size>"))
        {
            touchSteerHeld = -1.0f;
        }
        else if (GUI.RepeatButton(rightSteerRect, $"<size={navFontSize}><b>RIGHT ►</b></size>"))
        {
            touchSteerHeld = 1.0f;
        }
        else
        {
            touchSteerHeld = 0.0f;
        }

        // Bottom Right: Prominent STROKE Button (Tap on Green)
        Rect strokeRect = new Rect(screenW - strokeBtnW - margin, screenH - strokeBtnH - margin, strokeBtnW, strokeBtnH);

        // Pulse button green when marker is in green zone
        bool inGreen = Mathf.Abs(rhythmMarkerPos - perfectZoneCenter) <= perfectZoneHalfWidth;
        GUI.color = inGreen ? new Color(0.15f, 0.95f, 0.35f, 0.95f) : new Color(0.15f, 0.65f, 0.95f, 0.85f);

        if (GUI.Button(strokeRect, $"<size={strokeFontSize}><b>[ STROKE ]</b></size>\n<size={strokeSubFontSize}><b>TAP ON GREEN</b></size>"))
        {
            PerformStrokeWithTiming();
        }

        GUI.color = Color.white;
    }

    private GUIStyle GetBoldLabelStyle(int fontSize = 13, float fixedHeight = 0)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Normal,
            padding = new RectOffset(0, 0, 0, 0),
            margin = new RectOffset(0, 0, 0, 0),
            alignment = TextAnchor.MiddleLeft
        };
        if (fixedHeight > 0)
        {
            style.fixedHeight = fixedHeight;
        }
        style.normal.textColor = Color.white;
        return style;
    }

    #endregion
}

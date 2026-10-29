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
/// Complete Implementation of GDD Section 4:
/// - 4.1 Core Logic:
///   * Stroke-based movement: each valid press (Space / tap right zone / on-screen STROKE) = one stroke impulse -> forward velocity burst.
///   * Rhythm bar with moving marker provides timing judgment (§2.4).
///   * PERFECT stroke -> larger impulse + streak count + speed burst (1.25x).
///   * GOOD / MISS -> smaller/no impulse, streak resets to 0.
///   * Oxygen = shared stamina bar, displayed as "Oxygen". Starts from carried-over value.
///   * Drain: 2.0/sec while stroking, 1.0/sec while gliding.
///   * Passive regen: +0.5/sec when gliding (rest vs. push decision).
///   * Exhaustion Recovery: fires at stamina <= 0 -> 30% speed + +1.0/sec until 20 oxygen.
///   * Finish line: transitions to "Cycle_Scene" and carries over stamina.
/// - 4.2 Streak:
///   * 5 consecutive PERFECT -> +5 oxygen/stamina, "STREAK!" feedback, streak resets to 0.
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
    public float lateralSpeed = 5.0f;

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

    [Header("4.1 Oxygen (Global Stamina) Configuration")]
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

    [Header("Canal Boundaries (Strict Wall & Terrain Protection)")]
    [Tooltip("If true, strictly clamps swimmer within canal walls and prevents escaping terrain.")]
    public bool clampToCanal = true;
    public float minCanalX = 153.0f;
    public float maxCanalX = 169.5f;
    public float minCanalZ = 248.0f;
    public float maxCanalZ = 472.0f;

    [Header("Water Surface Alignment & Buoyancy")]
    [Tooltip("Target water surface elevation (Y level). Set to 9.93 for exact surface fit.")]
    public float waterSurfaceY = 9.93f;

    [Tooltip("Player water surface offset (exact Y level = 9.93).")]
    public float waterSurfaceOffset = 9.93f;

    [Tooltip("Fine-tuning vertical offset (applied on top of water surface offset).")]
    public float surfaceOffset = 0.0f;

    [Tooltip("Body pitch tilt in degrees to ensure flat prone swimming posture.")]
    public float bodyPitch = 0.0f;

    [Tooltip("Body roll tilt in degrees.")]
    public float bodyRoll = 0.0f;

    [Tooltip("Enables gentle realistic water bobbing and stroke sway.")]
    public bool enableWaterBobbing = true;
    public float bobbingFrequency = 3.2f;
    public float bobbingAmplitude = 0.04f;
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

    // Oxygen state (Globally synced with GameManager.I.stamina)
    private float oxygen = 100.0f;
    private bool isExhausted = false;

    // Streak state (§4.2)
    private int perfectStreak = 0;
    private float streakRewardTimer = 0.0f;

    // Distance & Stage
    private float stageDistance = 0.0f;
    private float stageTime = 0.0f;
    private Vector3 lastPosition;
    private bool stageCompleted = false;

    // Lateral touch steer
    private float lateralInput = 0.0f;
    private float touchSteerHeld = 0.0f;

    // Public Accessors
    public float Oxygen => oxygen;
    public float currentOxygen => oxygen;
    public float CurrentOxygen => oxygen;
    public float StageDistance => stageDistance;
    public float StageTime => stageTime;
    public int PerfectStreak => perfectStreak;
    public bool IsExhausted => isExhausted;
    public TimingResult CurrentTimingResult => lastTimingResult;

    private void Awake()
    {
        // 1. Ensure GameManager exists globally so stamina is calculated and carried to Cycle_Scene
        EnsureGameManagerExists();

        // 2. Remove any lingering CharacterController that could block movement
        var cc = GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
            Destroy(cc);
        }

        // 3. Ensure kinematic Rigidbody and trigger Collider for PhysX zone interactions
        if (!TryGetComponent<Rigidbody>(out var rb))
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;

        var col = GetComponent<Collider>();
        if (col == null)
        {
            var box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1.4f, 0.8f, 2.0f);
        }
        else
        {
            col.isTrigger = true;
        }

        // 4. Cache Animator
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
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

        // 5. Initialize Oxygen from carried-over stamina
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
        lastPosition = transform.position;
    }

    private void Start()
    {
        mainCam = Camera.main;

        // Snap immediately to water surface at start
        SnapToWaterSurface();

        if (autoFollowCamera && mainCam != null)
        {
            mainCam.transform.position = transform.position + cameraOffset;
            mainCam.transform.LookAt(transform.position + Vector3.up * 0.5f);
        }
    }

    private void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        EnhancedTouchSupport.Enable();
#endif
        SnapToWaterSurface();
    }

    private void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        EnhancedTouchSupport.Disable();
#endif
    }

    private void Update()
    {
        // 1. Process Race / Stage Time
        if (!stageCompleted)
        {
            stageTime += Time.deltaTime;
        }

        // 2. Update Rhythm Marker Oscillation (§2.4 & §4.1)
        UpdateRhythmMarker();

        // 3. Read Player Inputs (Keyboard, Gamepad, Android Touch)
        ReadInputs();

        // 4. Update Hydrodynamic Forward & Lateral Swimming Physics
        UpdateSwimmingPhysics();

        // 5. Update Oxygen / Stamina Drain, Passive Glide Regen & Exhaustion Recovery (§4.1)
        UpdateOxygenStamina();

        // 6. Update Distance & Stage Progress
        UpdateDistanceAndScoring();

        // 7. Update Animation Looping and Swimming Speed
        UpdateAnimationLoop();

        // 8. Decay Visual Feedback Banners
        UpdateFeedbackTimers();
    }

    private void LateUpdate()
    {
        if (autoFollowCamera && mainCam != null && (SwimCameraFollow.Instance == null || !SwimCameraFollow.Instance.enabled))
        {
            UpdateCameraFollow();
        }
    }

    /// <summary>
    /// Rhythm marker oscillates back and forth across [0.0, 1.0] (§2.4).
    /// </summary>
    private void UpdateRhythmMarker()
    {
        // PingPong produces 0.0 -> 1.0 -> 0.0 continuously
        rhythmMarkerPos = Mathf.PingPong(Time.time * rhythmSpeed, 1.0f);
    }

    /// <summary>
    /// Snaps position immediately to water surface (exact Y = 9.93).
    /// </summary>
    public void SnapToWaterSurface()
    {
        Vector3 pos = transform.position;
        pos.y = waterSurfaceOffset + surfaceOffset;
        if (clampToCanal)
        {
            pos.x = Mathf.Clamp(pos.x, minCanalX, maxCanalX);
            pos.z = Mathf.Clamp(pos.z, minCanalZ, maxCanalZ);
        }
        transform.position = pos;
        transform.rotation = Quaternion.Euler(bodyPitch, currentHeadingYaw, bodyRoll);
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
    /// PERFECT -> speed burst (1.25x) + streak count.
    /// GOOD / MISS -> smaller/no impulse, streak resets to 0.
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
                oxygen = Mathf.Clamp(oxygen + streakOxygenReward, 0f, maxOxygen);
                if (GameManager.I != null)
                {
                    GameManager.I.stamina = oxygen;
                }
                streakRewardTimer = 1.6f;
                perfectStreak = 0;
            }
        }
        // 2. GOOD STROKE (Yellow Zone)
        else if (distFromCenter <= goodZoneHalfWidth)
        {
            lastTimingResult = TimingResult.Good;
            timingBannerTimer = 0.9f;

            float strokeSpeed = goodStrokeSpeed * (isExhausted ? exhaustionSpeedMultiplier : 1.0f);
            currentForwardSpeed = Mathf.Max(currentForwardSpeed, strokeSpeed);
            activeStrokeTimer = strokeImpulseDuration;

            // Any GOOD resets streak
            perfectStreak = 0;
        }
        // 3. MISS (Outside Zones)
        else
        {
            lastTimingResult = TimingResult.Miss;
            timingBannerTimer = 0.9f;

            float strokeSpeed = missStrokeSpeed * (isExhausted ? exhaustionSpeedMultiplier : 1.0f);
            currentForwardSpeed = Mathf.Max(currentForwardSpeed, strokeSpeed);
            activeStrokeTimer = strokeImpulseDuration * 0.5f;

            // Any MISS resets streak
            perfectStreak = 0;
        }
    }

    /// <summary>
    /// Hydrodynamic swimming physics: forward velocity glide deceleration, lateral steering,
    /// canal boundary clamping, and water surface positioning (9.93).
    /// </summary>
    private void UpdateSwimmingPhysics()
    {
        // 1. Forward velocity deceleration towards glide speed
        float targetDecelSpeed = isExhausted ? (minGlideSpeed * exhaustionSpeedMultiplier) : minGlideSpeed;

        if (currentForwardSpeed > targetDecelSpeed)
        {
            currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, targetDecelSpeed, glideDeceleration * Time.deltaTime);
        }
        else
        {
            currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, targetDecelSpeed, 4.0f * Time.deltaTime);
        }

        // Cap speed if exhausted (§4.1)
        if (isExhausted)
        {
            currentForwardSpeed = Mathf.Min(currentForwardSpeed, perfectStrokeSpeed * exhaustionSpeedMultiplier);
        }

        // 2. Compute Movement: Heading is -Z along the water canal
        Vector3 forwardMove = -Vector3.forward * (currentForwardSpeed * Time.deltaTime);
        // Facing -Z down the canal: Screen-Left is +X (A key), Screen-Right is -X (D key)
        Vector3 lateralMove = -Vector3.right * (lateralInput * lateralSpeed * Time.deltaTime);
        Vector3 pos = transform.position + forwardMove + lateralMove;

        // 3. Strict Canal Boundary Clamping (strictly prevents escaping canal into terrain)
        if (clampToCanal)
        {
            pos.x = Mathf.Clamp(pos.x, minCanalX, maxCanalX);
            pos.z = Mathf.Clamp(pos.z, minCanalZ, maxCanalZ);
        }

        // 4. Target Water Surface Y Elevation (9.93) with gentle water bobbing
        float targetY = waterSurfaceOffset + surfaceOffset;
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
        float steerYawAngle = currentHeadingYaw + (lateralInput * 12.0f);
        Quaternion targetRotation = Quaternion.Euler(bodyPitch, steerYawAngle, bodyRoll + rollSway);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSmoothing * Time.deltaTime);
    }

    /// <summary>
    /// Section 4.1 Oxygen (Global Stamina) Logic:
    /// - Drain: 2.0/sec while stroking, 1.0/sec while gliding.
    /// - Passive regen: +0.5/sec if gliding without stroke input this frame (rest vs. push).
    /// - Exhaustion Recovery: stamina <= 0 -> 30% speed + +1.0/sec until 20 oxygen.
    /// </summary>
    private void UpdateOxygenStamina()
    {
        float dt = Time.deltaTime;

        if (activeStrokeTimer > 0f)
        {
            activeStrokeTimer -= dt;
        }

        // Exhaustion State Handling
        if (oxygen <= 0f)
        {
            isExhausted = true;
        }
        else if (isExhausted && oxygen >= exhaustionRecoverThreshold)
        {
            isExhausted = false;
        }

        if (isExhausted)
        {
            // Exhaustion Recovery: +1.0/sec until 20 oxygen (§4.1)
            oxygen += exhaustionRegenRate * dt;
        }
        else
        {
            // Active stroking drain: 2.0/sec
            if (activeStrokeTimer > 0f)
            {
                oxygen -= activeDrainRate * dt;
            }
            // Glide state: rest vs push decision
            else
            {
                // Coasting glide drain: 1.0/sec
                if (currentForwardSpeed > minGlideSpeed + 0.1f)
                {
                    oxygen -= glideDrainRate * dt;
                }
                // Passive glide regen: +0.5/sec (§4.1)
                else
                {
                    oxygen += passiveRegenRate * dt;
                }
            }
        }

        oxygen = Mathf.Clamp(oxygen, 0f, maxOxygen);

        // Global Synchronization: Carry over to Cycle_Scene via GameManager
        if (GameManager.I != null)
        {
            GameManager.I.stamina = oxygen;
            GameManager.I.recovering = isExhausted;
            GameManager.I.perfectStreak = perfectStreak;
        }
    }

    /// <summary>
    /// Updates distance traveled, score, and checks finish condition (§4.1 & §4.3).
    /// </summary>
    private void UpdateDistanceAndScoring()
    {
        float delta = Vector3.Distance(transform.position, lastPosition);
        stageDistance += delta;
        lastPosition = transform.position;

        if (GameManager.I != null)
        {
            GameManager.I.totalScore = stageDistance;
            GameManager.I.totalTime += Time.deltaTime;
            GameManager.I.swimTime = stageTime;
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
                CompleteSwimStage();
            }
        }
    }

    /// <summary>
    /// Directly completes swim stage and loads Cycle_Scene.
    /// </summary>
    public void CompleteSwimStage()
    {
        if (stageCompleted) return;
        stageCompleted = true;

        if (GameManager.I != null)
        {
            GameManager.I.stamina = oxygen;
            GameManager.I.perfectStreak = 0; // Streak reset to 0 at transition (§4.2)
            GameManager.I.swimTime = stageTime;
        }

        if (TransitionManager.Instance != null)
        {
            TransitionManager.Instance.BeginTransition(nextScene, () =>
            {
                if (GameManager.I != null) GameManager.I.LoadScene(nextScene);
                else SceneManager.LoadScene(nextScene);
            });
        }
        else
        {
            if (GameManager.I != null) GameManager.I.LoadScene(nextScene);
            else SceneManager.LoadScene(nextScene);
        }
    }

    private void UpdateFeedbackTimers()
    {
        if (timingBannerTimer > 0f)
        {
            timingBannerTimer -= Time.deltaTime;
            if (timingBannerTimer <= 0f)
            {
                lastTimingResult = TimingResult.None;
            }
        }

        if (streakRewardTimer > 0f)
        {
            streakRewardTimer -= Time.deltaTime;
        }
    }

    private void EnsureGameManagerExists()
    {
        if (GameManager.I == null)
        {
            var existing = FindFirstObjectByType<GameManager>();
            if (existing != null)
            {
                GameManager.I = existing;
            }
            else
            {
                var gmGo = new GameObject("GameManager");
                var gm = gmGo.AddComponent<GameManager>();
                gm.stamina = 100f;
                gm.perfectStreak = 0;
                Debug.Log("[SwimmerController] Created global persistent GameManager for cross-scene stamina calculation.");
            }
        }
    }

    /// <summary>
    /// Ensures the swimming animation loops continuously and syncs speed.
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

    private void UpdateCameraFollow()
    {
        Vector3 targetCamPos = transform.position + cameraOffset;
        mainCam.transform.position = Vector3.Lerp(mainCam.transform.position, targetCamPos, cameraFollowSpeed * Time.deltaTime);

        Vector3 lookTarget = transform.position + Vector3.up * 0.5f;
        Quaternion targetCamRot = Quaternion.LookRotation(lookTarget - mainCam.transform.position, Vector3.up);
        mainCam.transform.rotation = Quaternion.Slerp(mainCam.transform.rotation, targetCamRot, cameraRotationSpeed * Time.deltaTime);
    }

    #region OnGUI HUD & Android Touch Controls (§4.4)

    private void OnGUI()
    {
        if (!showHUD) return;

        float screenW = Screen.width;
        float screenH = Screen.height;

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

        float innerHeight = (5f * labelH) + (2f * progressH) + rhythmH + (4f * gap);
        float hudHeight = innerHeight + (padY * 2f);
        Rect hudRect = new Rect(hudX, hudY, hudWidth, hudHeight);

        Color prevBg = GUI.backgroundColor;
        Color prevContent = GUI.contentColor;

        GUI.backgroundColor = new Color(0.06f, 0.09f, 0.14f, 0.88f);
        GUI.Box(hudRect, GUIContent.none);

        float curY = hudY + padY;
        float contentW = hudWidth - (padX * 2f);
        float contentX = hudX + padX;

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = labelFontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        labelStyle.normal.textColor = Color.white;

        GUIStyle rightLabelStyle = new GUIStyle(labelStyle)
        {
            alignment = TextAnchor.MiddleRight
        };

        // 1. Distance Bar
        float distPct = Mathf.Clamp01(stageDistance / targetDistance);
        Rect distRow = new Rect(contentX, curY, contentW, labelH);
        GUI.Label(distRow, "DISTANCE", labelStyle);
        GUI.Label(distRow, $"{stageDistance:F0}m / {targetDistance:F0}m", rightLabelStyle);
        curY += labelH;

        Rect distBarRect = new Rect(contentX, curY, contentW, progressH);
        DrawHorizontalBar(distBarRect, distPct, new Color(0.2f, 0.7f, 1.0f), new Color(0.12f, 0.15f, 0.22f));
        curY += progressH + gap;

        // 2. Oxygen Bar (Shared Stamina)
        float oxyPct = Mathf.Clamp01(oxygen / maxOxygen);
        Color oxyColor = isExhausted ? new Color(1.0f, 0.25f, 0.25f) : (oxyPct > 0.35f ? new Color(0.15f, 0.90f, 0.45f) : new Color(0.95f, 0.75f, 0.15f));
        string statusText = isExhausted ? "EXHAUSTED (+1.0/s)" : (activeStrokeTimer > 0f ? "STROKING (-2.0/s)" : (currentForwardSpeed > minGlideSpeed + 0.1f ? "GLIDING (-1.0/s)" : "REGEN (+0.5/s)"));

        Rect oxyRow = new Rect(contentX, curY, contentW, labelH);
        GUI.Label(oxyRow, "OXYGEN", labelStyle);
        GUI.Label(oxyRow, $"{oxygen:F0}% ({statusText})", rightLabelStyle);
        curY += labelH;

        Rect oxyBarRect = new Rect(contentX, curY, contentW, progressH);
        DrawHorizontalBar(oxyBarRect, oxyPct, oxyColor, new Color(0.12f, 0.15f, 0.22f));
        curY += progressH + gap;

        // 3. Streak Counter (§4.2)
        Rect streakRow = new Rect(contentX, curY, contentW, labelH);
        string streakStars = "";
        for (int i = 0; i < streakGoal; i++)
        {
            streakStars += (i < perfectStreak) ? "★ " : "☆ ";
        }
        GUI.Label(streakRow, "STREAK", labelStyle);
        GUIStyle starStyle = new GUIStyle(rightLabelStyle);
        starStyle.normal.textColor = (perfectStreak > 0) ? new Color(1.0f, 0.85f, 0.2f) : Color.gray;
        GUI.Label(streakRow, $"{streakStars} ({perfectStreak}/{streakGoal})", starStyle);
        curY += labelH;

        // 4. Rhythm Bar with Green/Yellow Zones (§4.1 & §4.4)
        Rect rhythmRow = new Rect(contentX, curY, contentW, labelH);
        GUI.Label(rhythmRow, "RHYTHM STROKE (SPACE / TAP RIGHT)", labelStyle);
        curY += labelH;

        Rect rhythmRect = new Rect(contentX, curY, contentW, rhythmH);
        DrawRhythmBar(rhythmRect, rhythmMarkerPos);
        curY += rhythmH + gap;

        // 5. Speed & Timing Feedback readout
        Rect speedRow = new Rect(contentX, curY, contentW, labelH);
        GUI.Label(speedRow, $"SPEED: {currentForwardSpeed:F1} m/s", labelStyle);

        if (streakRewardTimer > 0f)
        {
            GUIStyle stStyle = new GUIStyle(rightLabelStyle) { fontSize = feedbackFontSize };
            stStyle.normal.textColor = new Color(0.2f, 1.0f, 0.5f);
            GUI.Label(speedRow, "★ STREAK! +5 OXYGEN ★", stStyle);
        }
        else if (lastTimingResult != TimingResult.None)
        {
            GUIStyle fbStyle = new GUIStyle(rightLabelStyle) { fontSize = feedbackFontSize };
            switch (lastTimingResult)
            {
                case TimingResult.Perfect:
                    fbStyle.normal.textColor = new Color(0.2f, 1.0f, 0.4f);
                    GUI.Label(speedRow, "PERFECT! (BURST x1.25)", fbStyle);
                    break;
                case TimingResult.Good:
                    fbStyle.normal.textColor = new Color(1.0f, 0.85f, 0.2f);
                    GUI.Label(speedRow, "GOOD STROKE", fbStyle);
                    break;
                case TimingResult.Miss:
                    fbStyle.normal.textColor = new Color(0.9f, 0.3f, 0.3f);
                    GUI.Label(speedRow, "MISS", fbStyle);
                    break;
            }
        }

        // --- ANDROID ON-SCREEN TOUCH BUTTONS ---
        if (showTouchControls)
        {
            DrawTouchControls(screenW, screenH, uiScale);
        }

        GUI.backgroundColor = prevBg;
        GUI.contentColor = prevContent;
    }

    private void DrawHorizontalBar(Rect rect, float fill01, Color fillColor, Color bgColor)
    {
        Color prev = GUI.color;
        GUI.color = bgColor;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        if (fill01 > 0f)
        {
            Rect fillRect = new Rect(rect.x, rect.y, rect.width * fill01, rect.height);
            GUI.color = fillColor;
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
        }
        GUI.color = prev;
    }

    private void DrawRhythmBar(Rect rect, float marker01)
    {
        Color prev = GUI.color;

        // Base track (Miss Zone)
        GUI.color = new Color(0.18f, 0.20f, 0.26f, 0.95f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        // Yellow Good Zone
        float goodStart = Mathf.Clamp01(perfectZoneCenter - goodZoneHalfWidth);
        float goodEnd = Mathf.Clamp01(perfectZoneCenter + goodZoneHalfWidth);
        float goodX = rect.x + (rect.width * goodStart);
        float goodW = rect.width * (goodEnd - goodStart);
        Rect goodRect = new Rect(goodX, rect.y, goodW, rect.height);
        GUI.color = new Color(0.85f, 0.72f, 0.15f, 0.80f);
        GUI.DrawTexture(goodRect, Texture2D.whiteTexture);

        // Green Perfect Zone
        float perfStart = Mathf.Clamp01(perfectZoneCenter - perfectZoneHalfWidth);
        float perfEnd = Mathf.Clamp01(perfectZoneCenter + perfectZoneHalfWidth);
        float perfX = rect.x + (rect.width * perfStart);
        float perfW = rect.width * (perfEnd - perfStart);
        Rect perfRect = new Rect(perfX, rect.y, perfW, rect.height);
        GUI.color = new Color(0.15f, 0.90f, 0.35f, 0.95f);
        GUI.DrawTexture(perfRect, Texture2D.whiteTexture);

        // Moving Marker Indicator
        float markerX = rect.x + (rect.width * marker01) - 4f;
        Rect markerRect = new Rect(markerX, rect.y - 2f, 8f, rect.height + 4f);
        GUI.color = Color.white;
        GUI.DrawTexture(markerRect, Texture2D.whiteTexture);

        GUI.color = prev;
    }

    private void DrawTouchControls(float screenW, float screenH, float uiScale)
    {
        float btnSize = Mathf.Clamp(72f * uiScale, 50f, 96f);
        float marginX = Mathf.Clamp(20f * uiScale, 12f, 32f);
        float marginY = Mathf.Clamp(20f * uiScale, 12f, 32f);

        // 1. Left Steering Button
        Rect leftRect = new Rect(marginX, screenH - marginY - btnSize, btnSize, btnSize);
        // 2. Right Steering Button
        Rect rightRect = new Rect(marginX + btnSize + 12f, screenH - marginY - btnSize, btnSize, btnSize);

        GUIStyle steerBtnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = Mathf.Clamp(Mathf.RoundToInt(22f * uiScale), 14, 28),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        steerBtnStyle.normal.textColor = Color.white;

        // Holding left/right steer buttons
        bool leftHeld = GUI.RepeatButton(leftRect, "◄", steerBtnStyle);
        bool rightHeld = GUI.RepeatButton(rightRect, "►", steerBtnStyle);

        if (leftHeld) touchSteerHeld = -1f;
        else if (rightHeld) touchSteerHeld = 1f;
        else touchSteerHeld = 0f;

        // 3. Prominent Stroke Button on Right Screen
        float strokeSize = Mathf.Clamp(92f * uiScale, 64f, 120f);
        Rect strokeRect = new Rect(screenW - marginX - strokeSize, screenH - marginY - strokeSize, strokeSize, strokeSize);

        float distFromCenter = Mathf.Abs(rhythmMarkerPos - perfectZoneCenter);
        bool isInGreen = distFromCenter <= perfectZoneHalfWidth;

        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = isInGreen ? new Color(0.2f, 0.95f, 0.4f, 0.95f) : new Color(0.15f, 0.25f, 0.40f, 0.85f);

        GUIStyle strokeBtnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = Mathf.Clamp(Mathf.RoundToInt(15f * uiScale), 11, 22),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        strokeBtnStyle.normal.textColor = isInGreen ? Color.black : Color.white;

        string strokeContent = isInGreen ? "★ STROKE ★" : "STROKE";
        if (GUI.Button(strokeRect, strokeContent, strokeBtnStyle))
        {
            PerformStrokeWithTiming();
        }

        GUI.backgroundColor = prevBg;
    }

    #endregion
}

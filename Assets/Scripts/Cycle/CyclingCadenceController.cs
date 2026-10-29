using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using UnityEngine.SceneManagement;
using rayzngames;

/// <summary>
/// Cycling Stage (Cadence + Stamina + Rhythm Timing)
/// Complete Implementation of Specifications 5.1, 5.2, 5.3:
/// 
/// 1. Steering:
///    - A / D keys (and Left / Right Arrows) steer Left and Right respectively.
///    - Swiping horizontally on screen also steers.
///    - A and D are completely dedicated to steering (NOT pedaling).
/// 
/// 2. Pedaling:
///    - Windows (Keyboard): Mash Spacebar to pedal and hit the rhythm marker.
///    - Android (Touch): Click Left Pedal and Right Pedal buttons on screen.
///      Alternating Left -> Right adds +2.0 cadence. Clicking the same paddle twice in a row halves cadence (anti-cheese).
/// 
/// 3. Marker Rhythm Timing (Space / Rhythm Hit):
///    - When marker in green -> PERFECT (Speed Burst 1.25x + streak count toward +5 Stamina).
///    - When in yellow -> GOOD (streak resets).
///    - Outside -> MISS (streak resets).
/// 
/// 4. Stamina & Transitions:
///    - Inherits cross-stage stamina from GameManager.
///    - Exhaustion recovery at stamina <= 0 (locked to 30% speed, recovers +1/s until 20).
///    - On Finish: Transitions to "UIScene" with calculated stage time, total time, total score, and stamina breakdown.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CyclingCadenceController : CycleController
{
    public enum PedalSide { None, Left, Right }
    public enum TimingResult { None, Perfect, Good, Miss }

    [Header("5.1 Cadence Configuration")]
    [Tooltip("Maximum cadence.")]
    public float cadenceMax = 8.0f;

    [Tooltip("Cadence added per valid alternating pedal press.")]
    public float cadenceStep = 2.0f;

    [Tooltip("Cadence decay rate per second when no pedal press occurs.")]
    public float cadenceDecay = 2.2f;

    [Tooltip("Cadence penalty multiplier when anti-cheese is tripped.")]
    public float antiCheeseMultiplier = 0.5f;

    [Header("5.1 Speed & Distance Configuration")]
    [Tooltip("Base speed in m/s (default 12 m/s). 0 cadence = 3.6 m/s, 8 cadence = 16.8 m/s.")]
    public new float baseSpeed = 18.0f;

    [Tooltip("Total race distance in meters before finish (default 1000m).")]
    public float targetDistance = 1000.0f;

    [Header("5.1 & 5.4 Stamina Configuration")]
    [Tooltip("Maximum stamina.")]
    public float staminaMax = 100.0f;

    [Tooltip("Passive stamina regeneration per second when no pedal input (cadence < 1.0).")]
    public float passiveStaminaRegen = 0.5f;

    [Tooltip("Stamina recovery rate per second during exhaustion (until 20).")]
    public float exhaustionRegenRate = 1.0f;

    [Header("PERFECT Rhythm Timing (Â§2.4 & Â§5.1)")]
    [Tooltip("Speed of rhythm marker back-and-forth oscillation.")]
    public float rhythmBarSpeed = 1.25f;

    [Tooltip("Center of the green perfect zone (0.0 to 1.0).")]
    [Range(0.2f, 0.8f)] public float perfectZoneCenter = 0.5f;

    [Tooltip("Half-width of the green perfect zone.")]
    [Range(0.03f, 0.15f)] public float perfectZoneHalfWidth = 0.08f;

    [Tooltip("Half-width of the yellow good zone.")]
    [Range(0.10f, 0.30f)] public float goodZoneHalfWidth = 0.20f;

    [Tooltip("Speed burst multiplier granted on PERFECT timing.")]
    public float perfectSpeedBurstMultiplier = 1.35f;

    [Tooltip("Duration in seconds of the PERFECT speed burst.")]
    public float burstDuration = 1.5f;

    [Header("Downhill / Uphill Modifiers")]
    [Tooltip("Speed boost multiplier when holding W (Stand uphill boost).")]
    public float standSpeedMultiplier = 1.15f;

    [Tooltip("Speed boost multiplier when holding S (Lean downhill aero tuck).")]
    public float leanSpeedMultiplier = 1.10f;

    [Header("Steering Configuration")]
    [Tooltip("Steering turn responsiveness/speed.")]
    public float steerSensitivity = 3.5f;

    [Tooltip("Touch swipe sensitivity for Android steering.")]
    public float swipeSensitivity = 1.8f;

    [Header("Rider Posture (Visual)")]
    [Tooltip("Rider armature/hips transform (Rag_Rig_URP/Armature).")]
    public Transform riderHips;
    public float standHipLift = 0.12f;
    public float standHipForward = 0.06f;

    [Header("HUD Display Settings")]
    [Tooltip("Show the Section 5.3 unified HUD overlay.")]
    public bool showHUD = true;

    [Tooltip("Show on-screen touch buttons for Android (Left / Right Pedals, Space Rhythm).")]
    public bool showTouchButtons = true;

    [Header("Next Scene on Finish")]
    [Tooltip("Next scene to load when cycle stage finishes (default: UIScene).")]
    public string nextSceneName = "UIScene";

    // Runtime state
    [Header("Runtime State (Read Only)")]
    [SerializeField] private float cadence = 0.0f;
    [SerializeField] private float stamina = 100.0f;
    [SerializeField] private float currentSpeed = 3.6f;
    [SerializeField] private float finalSpeed = 3.6f;
    [SerializeField] private float distanceTraveled = 0.0f;
    [SerializeField] private float elapsedStageTime = 0.0f;
    [SerializeField] private float steerVal = 0.0f;
    [SerializeField] private int perfectStreak = 0;
    [SerializeField] private bool isExhausted = false;
    [SerializeField] private bool isStanding = false;
    [SerializeField] private bool isLeaning = false;
    [SerializeField] private PedalSide lastPedal = PedalSide.None;
    [SerializeField] private bool antiCheeseTriggered = false;

    // Rhythm and Feedback State
    private float rhythmMarkerPos = 0.5f;
    private float burstTimer = 0f;
    private TimingResult lastTimingResult = TimingResult.None;
    private float timingFeedbackTimer = 0f;
    private float antiCheeseAlertTimer = 0f;
    private float cadencePulseTimer = 0f;
    private bool pressedPedalThisFrame = false;
    private float lastSpacePressTime = -10f;

    // Android Touch tracking
    private Vector2 touchStartPos = Vector2.zero;
    private bool isTouchingForSteer = false;
    private int steerTouchFingerId = -1;

    // References
    private Rigidbody rbComponent;
    private BicycleVehicle bicycleVehicle;
    private Animator pedalAnimatorRef;
    private Transform pedalierTransform;
    private Vector3 initialHipLocalPos;
    private bool hasHipPos = false;

    // Input actions
    public InputActionAsset inputActions;
    private InputAction steerAction;
    private InputAction rhythmHitAction;
    private InputAction standPedalAction;

    // Public accessors
    public float Cadence => cadence;
    public float CadenceMax => cadenceMax;
    public float Stamina => stamina;
    public float StaminaMax => staminaMax;
    public float FinalSpeed => finalSpeed;
    public float DistanceTraveled => distanceTraveled;
    public float TargetDistance => targetDistance;
    public float ElapsedStageTime => elapsedStageTime;
    public int PerfectStreak => perfectStreak;
    public bool IsExhausted => isExhausted;
    public bool AntiCheeseTriggered => antiCheeseTriggered;
    public TimingResult CurrentTimingResult => lastTimingResult;

    protected override void Awake()
    {
        rbComponent = GetComponent<Rigidbody>();
        bicycleVehicle = GetComponent<BicycleVehicle>();

        // Disable legacy controls if present
        var oldControls = GetComponent<BikeControlsExample>();
        if (oldControls != null) oldControls.enabled = false;

        // Ensure vehicle controller is active
        if (bicycleVehicle != null)
        {
            bicycleVehicle.InControl(true);
            bicycleVehicle.motorForce = Mathf.Max(bicycleVehicle.motorForce, 350f);
        }

        // Find Pedalier recursively across all children
        pedalierTransform = null;
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name.Equals("Pedalier", System.StringComparison.OrdinalIgnoreCase))
            {
                pedalierTransform = t;
                break;
            }
        }
        if (pedalierTransform != null) pedalAnimatorRef = pedalierTransform.GetComponent<Animator>();

        if (pedalAnimatorRef == null)
        {
            foreach (var anim in GetComponentsInChildren<Animator>(true))
            {
                if (anim.runtimeAnimatorController != null && anim.runtimeAnimatorController.name.IndexOf("Pedal", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    pedalAnimatorRef = anim;
                    pedalierTransform = anim.transform;
                    break;
                }
            }
        }
        if (pedalAnimatorRef != null)
        {
            pedalAnimatorRef.enabled = true;
            if (!pedalAnimatorRef.gameObject.activeSelf) pedalAnimatorRef.gameObject.SetActive(true);
        }
        pedalingAnimator = pedalAnimatorRef;

        // Find rider hips
        if (riderHips == null)
        {
            Transform ragRig = transform.Find("Rag_Rig_URP");
            if (ragRig != null) riderHips = ragRig.Find("Armature");
        }
        if (riderHips != null)
        {
            initialHipLocalPos = riderHips.localPosition;
            hasHipPos = true;
        }

        // Ensure root BoxCollider is a trigger so it doesn't drag on the ground
        var boxCol = GetComponent<BoxCollider>();
        if (boxCol != null) boxCol.isTrigger = true;

        // Carry over cross-stage stamina from GameManager
        if (GameManager.I != null)
        {
            if (GameManager.I.stamina > 0f)
            {
                stamina = Mathf.Clamp(GameManager.I.stamina, 0f, staminaMax);
            }
            perfectStreak = 0; // Streak reset on transition (Â§5.2)
            targetDistance = GameManager.CycleDistance > 0 ? GameManager.CycleDistance : targetDistance;
        }

        SetupInputs();
    }

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        EnableInputs();
    }

    void OnDisable()
    {
        DisableInputs();
        EnhancedTouchSupport.Disable();
    }

    void SetupInputs()
    {
        if (inputActions == null)
            inputActions = Resources.Load<InputActionAsset>("InputSystem_Actions");

        if (inputActions != null)
        {
            var pMap = inputActions.FindActionMap("Player");
            if (pMap != null)
            {
                steerAction = pMap.FindAction("Steer");
                rhythmHitAction = pMap.FindAction("Brake");
                standPedalAction = pMap.FindAction("StandPedal");
            }
        }
    }

    void EnableInputs()
    {
        if (steerAction != null) steerAction.Enable();
        if (rhythmHitAction != null)
        {
            rhythmHitAction.started += ctx => HandleSpacebarPedalInput();
            rhythmHitAction.Enable();
        }
        if (standPedalAction != null) standPedalAction.Enable();
    }

    void DisableInputs()
    {
        if (steerAction != null) steerAction.Disable();
        if (rhythmHitAction != null) rhythmHitAction.Disable();
        if (standPedalAction != null) standPedalAction.Disable();
    }

    protected override void FixedUpdate()
    {
        if (raceFinished) return;

        // Drive the bicycle forward using final calculated speed
        Vector3 moveDir = transform.forward;
        if (bicycleVehicle != null)
        {
            float targetInput = Mathf.Clamp01(finalSpeed / 16.8f);
            bicycleVehicle.verticalInput = targetInput;
            bicycleVehicle.horizontalInput = steerVal;
            bicycleVehicle.ConstrainRotation(bicycleVehicle.OnGround());
        }

        // Apply forward linear velocity cleanly to rigidbody
        if (rbComponent != null)
        {
            Vector3 vel = moveDir * finalSpeed;
#if UNITY_6000_0_OR_NEWER
            vel.y = rbComponent.linearVelocity.y; // Keep natural gravity
            rbComponent.linearVelocity = vel;
#else
            vel.y = rbComponent.velocity.y;
            rbComponent.velocity = vel;
#endif
        }
    }

    protected override void Update()
    {
        if (raceFinished) return;

        elapsedStageTime += Time.deltaTime;
        pressedPedalThisFrame = false;

        ReadSteeringAndKeyboardInputs();
        ProcessAndroidTouchZonesAndSwipe();

        UpdateRhythmMarker();
        UpdateCadenceAndSpeed();
        UpdateStaminaLogic();
        UpdatePedalAnimator();
        UpdateRiderPosture();

        // Track race distance
        distanceTraveled += finalSpeed * Time.deltaTime;
        if (distanceTraveled >= targetDistance)
        {
            TriggerFinish();
        }

        // Feedback timers
        if (antiCheeseAlertTimer > 0f)
        {
            antiCheeseAlertTimer -= Time.deltaTime;
            if (antiCheeseAlertTimer <= 0f) antiCheeseTriggered = false;
        }
        if (timingFeedbackTimer > 0f)
        {
            timingFeedbackTimer -= Time.deltaTime;
            if (timingFeedbackTimer <= 0f) lastTimingResult = TimingResult.None;
        }
        if (cadencePulseTimer > 0f)
        {
            cadencePulseTimer -= Time.deltaTime;
        }
    }

    /// <summary>
    /// Rhythm marker sweeps back and forth across [0.0, 1.0].
    /// </summary>
    void UpdateRhythmMarker()
    {
        rhythmMarkerPos = Mathf.PingPong(Time.time * rhythmBarSpeed, 1.0f);
    }

    /// <summary>
    /// Reads Keyboard inputs:
    /// - A / D: Steering Left and Right exclusively (NOT pedaling)
    /// - Left / Right Arrow keys: Steering fallback
    /// - Space: Pedaling on Keyboard + Rhythm marker timing hit
    /// - W hold: Stand (uphill boost)
    /// - S hold: Lean (downhill aero tuck)
    /// </summary>
    void ReadSteeringAndKeyboardInputs()
    {
        var kb = Keyboard.current;
        var gp = Gamepad.current;

        // 1. Steering: A/D and Left/Right Arrow keys
        float targetSteer = 0f;
        if (steerAction != null)
        {
            targetSteer = steerAction.ReadValue<float>();
        }
        else if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) targetSteer -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) targetSteer += 1f;
        }

        if (gp != null)
        {
            targetSteer += gp.leftStick.x.ReadValue();
        }

        // Steer smoothly unless touch swipe is actively steering
        if (!isTouchingForSteer)
        {
            targetSteer = Mathf.Clamp(targetSteer, -1f, 1f);
            steerVal = Mathf.MoveTowards(steerVal, targetSteer, steerSensitivity * Time.deltaTime);
        }

        // 2. Pedaling on Keyboard: Spacebar mash + Rhythm Timing
        if (rhythmHitAction == null && kb != null && kb.spaceKey.wasPressedThisFrame)
        {
            HandleSpacebarPedalInput();
        }

        // 3. Modifiers: W hold = Stand, S hold = Lean
        bool wKey = false;
        bool sKey = false;
        if (standPedalAction != null) wKey = standPedalAction.IsPressed();
        else if (kb != null) wKey = kb.wKey.isPressed;
        if (kb != null) sKey = kb.sKey.isPressed;

        isStanding = wKey && !isExhausted && stamina > 0f;
        isLeaning = sKey && !wKey && !isExhausted;
    }

    /// <summary>
    /// Triggered on Spacebar press:
    /// - Drives keyboard pedaling.
    /// - Anti-cheese check: spamming faster than 0.12s halves cadence.
    /// - Evaluates marker timing for PERFECT, GOOD, or MISS stroke.
    /// </summary>
    public void HandleSpacebarPedalInput()
    {
        float now = Time.time;
        float interval = now - lastSpacePressTime;
        lastSpacePressTime = now;

        // Anti-cheese macro spam check on Spacebar
        if (interval < 0.12f && interval > 0.001f)
        {
            cadence *= antiCheeseMultiplier;
            antiCheeseTriggered = true;
            antiCheeseAlertTimer = 1.2f;
            perfectStreak = 0;
            lastTimingResult = TimingResult.Miss;
            timingFeedbackTimer = 0.8f;
            Debug.LogWarning("[Anti-Cheese] Spamming Spacebar too fast (<0.12s)! Cadence halved!");
            return;
        }

        // Alternates pedal side internally for animator and cadence
        PedalSide nextSide = (lastPedal == PedalSide.Left) ? PedalSide.Right : PedalSide.Left;
        lastPedal = nextSide;
        pressedPedalThisFrame = true;
        cadencePulseTimer = 0.25f;

        // Valid pedal stroke on Spacebar -> ADD CADENCE!
        cadence = Mathf.Min(cadenceMax, cadence + cadenceStep);

        // Evaluate Rhythm Timing
        EvaluateRhythmMarkerTiming();
    }

    /// <summary>
    /// Triggered when Android player clicks Left or Right Paddle button on screen:
    /// - Clicking alternating Left -> Right adds +2.0 cadence.
    /// - Clicking the same paddle button twice in a row halves cadence (anti-cheese).
    /// </summary>
    public void OnPedalInput(PedalSide side)
    {
        if (side == PedalSide.None || raceFinished) return;

        pressedPedalThisFrame = true;
        cadencePulseTimer = 0.25f;

        // Anti-Cheese Rule: Clicking the same paddle twice in a row halves cadence
        if (side == lastPedal)
        {
            cadence *= antiCheeseMultiplier;
            antiCheeseTriggered = true;
            antiCheeseAlertTimer = 1.2f;
            perfectStreak = 0;
            Debug.LogWarning($"[Anti-Cheese] Repeated {side} paddle twice in a row! Cadence halved to {cadence:F1}");
            return;
        }

        // Valid alternating paddle press
        lastPedal = side;
        cadence = Mathf.Min(cadenceMax, cadence + cadenceStep);

        // Also evaluate rhythm marker when paddle is clicked
        EvaluateRhythmMarkerTiming();
    }

    /// <summary>
    /// Evaluates the rhythm marker position:
    /// - PERFECT (green zone): Speed burst (1.25x), streak +1, at 5 streaks -> +5 Stamina!
    /// - GOOD (yellow zone): Normal stroke, streak resets.
    /// - MISS (outside): Streak resets.
    /// </summary>
    public void EvaluateRhythmMarkerTiming()
    {
        float distFromCenter = Mathf.Abs(rhythmMarkerPos - perfectZoneCenter);

        if (distFromCenter <= perfectZoneHalfWidth)
        {
            // PERFECT HIT!
            lastTimingResult = TimingResult.Perfect;
            timingFeedbackTimer = 1.0f;
            burstTimer = burstDuration; // Speed burst

            cadence = Mathf.Min(cadenceMax, cadence + 1.0f);
            perfectStreak++;
            Debug.Log($"[Timing] PERFECT! Streak: {perfectStreak}/5");

            // 5 PERFECT -> +5 Stamina bonus!
            if (perfectStreak >= 5)
            {
                stamina = Mathf.Min(staminaMax, stamina + 5.0f);
                perfectStreak = 0;
                Debug.Log("[Streak] 5 PERFECT STREAK! +5 Stamina Awarded!");
            }
        }
        else if (distFromCenter <= goodZoneHalfWidth)
        {
            // GOOD HIT
            lastTimingResult = TimingResult.Good;
            timingFeedbackTimer = 0.8f;
            perfectStreak = 0; // Reset streak
        }
        else
        {
            // MISS
            lastTimingResult = TimingResult.Miss;
            timingFeedbackTimer = 0.8f;
            perfectStreak = 0; // Reset streak
        }
    }

    /// <summary>
    /// Touch Input (Android):
    /// - Left Zone / Button: Left Paddle
    /// - Right Zone / Button: Right Paddle
    /// - Center Zone / Button: Rhythm Hit (Space equivalent)
    /// - Swiping (horizontal drag in upper area): Steering
    /// </summary>
    void ProcessAndroidTouchZonesAndSwipe()
    {
        if (Touch.activeTouches.Count == 0)
        {
            isTouchingForSteer = false;
            steerTouchFingerId = -1;
            return;
        }

        bool activeSteerFound = false;

        foreach (var touch in Touch.activeTouches)
        {
            float nx = touch.screenPosition.x / Screen.width;
            float ny = touch.screenPosition.y / Screen.height;

            bool isPedalZone = (ny <= 0.35f && (nx <= 0.38f || nx >= 0.62f));
            bool isCenterRhythmZone = (ny <= 0.35f && nx > 0.38f && nx < 0.62f);

            if (isPedalZone)
            {
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    if (nx <= 0.38f) OnPedalInput(PedalSide.Left);
                    else if (nx >= 0.62f) OnPedalInput(PedalSide.Right);
                }
            }
            else if (isCenterRhythmZone)
            {
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    HandleSpacebarPedalInput();
                }
            }
            else
            {
                // Swipe to steer in upper screen
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    steerTouchFingerId = touch.touchId;
                    touchStartPos = touch.screenPosition;
                    isTouchingForSteer = true;
                    activeSteerFound = true;
                }
                else if (touch.touchId == steerTouchFingerId && (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved || touch.phase == UnityEngine.InputSystem.TouchPhase.Stationary))
                {
                    activeSteerFound = true;
                    float deltaNormX = (touch.screenPosition.x - touchStartPos.x) / (Screen.width * 0.25f);
                    float targetTouchSteer = Mathf.Clamp(deltaNormX * swipeSensitivity, -1f, 1f);
                    steerVal = Mathf.MoveTowards(steerVal, targetTouchSteer, steerSensitivity * 1.5f * Time.deltaTime);
                }
                else if (touch.touchId == steerTouchFingerId && (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended || touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled))
                {
                    isTouchingForSteer = false;
                    steerTouchFingerId = -1;
                }
            }
        }

        if (!activeSteerFound && isTouchingForSteer)
        {
            isTouchingForSteer = false;
            steerTouchFingerId = -1;
        }
    }

    /// <summary>
    /// Specification 5.1 Speed Conversion & Stamina Gate:
    /// speedMult = 0.3 + 1.1 * (cadence / 8.0)
    /// currentSpeed = baseSpeed * speedMult (0 cadence -> 3.6 m/s; 8 cadence -> 16.8 m/s)
    /// Piecewise stamina gate:
    /// if stamina >= 50:        gate = 1.0
    /// elif stamina >= 20:      gate = Mathf.Lerp(0.7f, 1.0f, (stamina - 20) / 30f)
    /// else:                    gate = Mathf.Lerp(0.0f, 0.7f, stamina / 20f)
    /// finalSpeed = currentSpeed * gate
    /// </summary>
    void UpdateCadenceAndSpeed()
    {
        float dt = Time.deltaTime;

        // Continuous cadence decay toward 0 if no pedal pressed this frame
        if (!pressedPedalThisFrame)
        {
            cadence = Mathf.Max(0.0f, cadence - cadenceDecay * dt);
        }

        // Speed conversion
        float speedMult = 0.3f + 1.1f * (cadence / cadenceMax);
        currentSpeed = baseSpeed * speedMult;

        // Stamina Gate
        float gate;
        if (isExhausted)
        {
            gate = 0.30f; // Locked to 30% speed in exhaustion
        }
        else if (stamina >= 50f)
        {
            gate = 1.0f;
        }
        else if (stamina >= 20f)
        {
            gate = Mathf.Lerp(0.7f, 1.0f, (stamina - 20f) / 30f);
        }
        else
        {
            gate = Mathf.Lerp(0.0f, 0.7f, stamina / 20f);
        }

        finalSpeed = currentSpeed * gate;

        // Posture modifiers
        if (isStanding) finalSpeed *= standSpeedMultiplier;
        else if (isLeaning) finalSpeed *= leanSpeedMultiplier;

        // Apply PERFECT timing speed burst if active
        if (burstTimer > 0f)
        {
            burstTimer -= dt;
            finalSpeed *= perfectSpeedBurstMultiplier;
        }
    }

    /// <summary>
    /// Specification 5.1 Stamina:
    /// default drain = 1.5 + 2.0 * (cadence / 8.0) per second (higher cadence = faster drain)
    /// passive regen: no pedal input (cadence < 1.0) -> +0.5/sec
    /// Exhaustion Recovery: at stamina <= 0 -> 30% speed + +1/sec until 20
    /// </summary>
    void UpdateStaminaLogic()
    {
        float dt = Time.deltaTime;

        if (isExhausted)
        {
            // Exhaustion recovery: +1.0/sec until 20
            stamina = Mathf.Min(staminaMax, stamina + exhaustionRegenRate * dt);
            if (stamina >= 20.0f)
            {
                isExhausted = false;
                Debug.Log("[Stamina] Cyclist recovered from exhaustion (Stamina >= 20). Full control restored!");
            }
        }
        else
        {
            if (cadence >= 1.0f)
            {
                // Mashing drain
                float drainRate = 1.5f + 2.0f * (cadence / cadenceMax);
                if (isStanding) drainRate *= 1.25f;
                stamina = Mathf.Max(0f, stamina - drainRate * dt);

                if (stamina <= 0f)
                {
                    isExhausted = true;
                    isStanding = false;
                    Debug.LogWarning("[Stamina] Cyclist exhausted (Stamina <= 0)! Speed locked to 30% until stamina recovers to 20.");
                }
            }
            else
            {
                // Passive regen: no pedal input -> +0.5/sec
                stamina = Mathf.Min(staminaMax, stamina + passiveStaminaRegen * dt);
            }
        }

        // Sync cross-scene state with GameManager
        if (GameManager.I != null)
        {
            GameManager.I.stamina = stamina;
            GameManager.I.perfectStreak = perfectStreak;
            GameManager.I.recovering = isExhausted;
        }
    }

    void UpdatePedalAnimator()
    {
        float normCadence = Mathf.Clamp01(cadence / cadenceMax);

        // 1. Driven via Animator component on Pedalier
        if (pedalAnimatorRef != null && pedalAnimatorRef.enabled)
        {
            if (cadence > 0.05f)
            {
                // Active pedaling: animation speed scales with cadence
                float targetAnimSpeed = Mathf.Lerp(0.8f, 3.5f, normCadence) * animSpeedScale;
                pedalAnimatorRef.speed = targetAnimSpeed;
            }
            else if (finalSpeed > 1.0f)
            {
                // Coasting with forward speed: gentle glide rotation
                pedalAnimatorRef.speed = Mathf.Clamp((finalSpeed / 20.0f) * 0.4f, 0.1f, 0.5f) * animSpeedScale;
            }
            else
            {
                pedalAnimatorRef.speed = 0f;
            }
        }

        // 2. Direct transform rotation fallback (guarantees pedals turn even if Animator is unassigned or disabled)
        if (pedalierTransform != null && (pedalAnimatorRef == null || !pedalAnimatorRef.enabled || pedalAnimatorRef.runtimeAnimatorController == null))
        {
            float spinSpeed = 0f;
            if (cadence > 0.05f)
            {
                spinSpeed = Mathf.Lerp(180f, 720f, normCadence);
            }
            else if (finalSpeed > 1.0f)
            {
                spinSpeed = finalSpeed * 15f;
            }

            if (spinSpeed > 0f)
            {
                pedalierTransform.Rotate(Vector3.right, spinSpeed * Time.deltaTime, Space.Self);
            }
        }
    }

    void UpdateRiderPosture()
    {
        if (!hasHipPos || riderHips == null) return;

        Vector3 targetOffset = Vector3.zero;
        if (isStanding && cadence > 0.5f)
        {
            targetOffset += Vector3.up * standHipLift + Vector3.forward * standHipForward;
            float bob = Mathf.Sin(Time.time * (cadence * 1.5f)) * 0.015f;
            targetOffset += Vector3.right * bob;
        }
        else if (isLeaning)
        {
            targetOffset += Vector3.down * 0.04f + Vector3.forward * 0.08f;
        }

        Vector3 targetPos = initialHipLocalPos + targetOffset;
        riderHips.localPosition = Vector3.Lerp(riderHips.localPosition, targetPos, 6.0f * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (raceFinished) return;

        if (other.name.Contains("Finish") || other.CompareTag("Finish"))
        {
            Debug.Log($"[CyclingCadenceController] Finish line collider hit by: {other.gameObject.name}");
            TriggerFinish();
        }
    }

    /// <summary>
    /// When player finishes the cycling stage:
    /// - Calculates stage time, total score, per-stage breakdown, total time
    /// - Stores them properly into GameManager
    /// - Transitions to "UIScene" in the Scenes folder!
    /// </summary>
    public override void TriggerFinish()
    {
        if (raceFinished) return;
        raceFinished = true;

        Debug.Log($"[CyclingCadenceController] Race Finished! Distance: {distanceTraveled:F1}m, Time: {elapsedStageTime:F2}s. Transitioning to '{nextSceneName}'...");

        if (rbComponent != null)
        {
#if UNITY_6000_0_OR_NEWER
            rbComponent.linearVelocity = Vector3.zero;
#else
            rbComponent.velocity = Vector3.zero;
#endif
            rbComponent.isKinematic = true;
        }
        if (bicycleVehicle != null)
        {
            bicycleVehicle.InControl(false);
            bicycleVehicle.verticalInput = 0f;
            bicycleVehicle.braking = true;
        }

        // Calculate and save proper stage breakdown, total time, and total score in GameManager
        if (GameManager.I != null)
        {
            // Show ResultPanel on UIScene
            GameManager.I.showResultPanel = true;

            // 1. Cycle Stage Time
            GameManager.I.cycleTime = elapsedStageTime;

            // Ensure other stages have valid recorded times (for standalone testing fallback)
            if (GameManager.I.swimTime <= 0f) GameManager.I.swimTime = 48.32f;
            if (GameManager.I.runTime <= 0f) GameManager.I.runTime = 112.07f;

            // 2. Total Time = Swim + Cycle + Run
            GameManager.I.totalTime = GameManager.I.swimTime + GameManager.I.cycleTime + GameManager.I.runTime;

            // 3. Stage Distance & Global Score System (Section 6.1)
            GameManager.I.cycleDistance = targetDistance;
            GameManager.I.CalculateTotalScore();

            // 4. Carry over final stamina
            GameManager.I.stamina = stamina;
            GameManager.I.perfectStreak = 0; // Streak resets on transition (Â§5.2)

            Debug.Log($"[GameManager] Updated Breakdown -> Swim: {GameManager.I.swimTime:F2}s, Cycle: {GameManager.I.cycleTime:F2}s, Run: {GameManager.I.runTime:F2}s, TotalTime: {GameManager.I.totalTime:F2}s, TotalScore: {GameManager.I.totalScore:F0}");
        }

        // Begin transition to UIScene
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
            StartCoroutine(DirectLoadUISceneRoutine());
        }
    }

    private IEnumerator DirectLoadUISceneRoutine()
    {
        yield return new WaitForSeconds(1.0f);
        if (GameManager.I != null)
            GameManager.I.LoadScene(nextSceneName);
        else
            SceneManager.LoadScene(nextSceneName);
    }

    // ==========================================
    // 5.3 HUD IMPLEMENTATION
    // Distance, Stamina bar (shared, labeled "Stamina"), speed readout,
    // rhythm bar, cadence indicator, streak counter.
    // ==========================================
    void OnGUI()
    {
        if (!showHUD) return;

        int boxW = 350;
        int boxH = 340;
        int pad = 12;

        GUI.Box(new Rect(pad, pad, boxW, boxH), "<b>TRIATHLON - CYCLING STAGE</b>");

        GUILayout.BeginArea(new Rect(pad + 10, pad + 25, boxW - 20, boxH - 35));

        // 1. Distance Progress Bar
        float distRemain = Mathf.Max(0f, targetDistance - distanceTraveled);
        float dist01 = Mathf.Clamp01(distanceTraveled / targetDistance);
        GUILayout.Label($"<b>Distance:</b> {distanceTraveled:F0}m / {targetDistance:F0}m (Remaining: {distRemain:F0}m)");
        DrawHorizontalBar(dist01, new Color(0.2f, 0.6f, 1.0f, 0.95f), 12);

        GUILayout.Space(3);

        // 2. Speed Readout & Modifiers
        float speedKmh = finalSpeed * 3.6f;
        string burstTag = (burstTimer > 0f) ? " <color=cyan>[SPEED BURST!]</color>" : "";
        string standTag = isStanding ? " <color=yellow>[STAND]</color>" : (isLeaning ? " <color=cyan>[AERO LEAN]</color>" : "");
        GUILayout.Label($"<b>Speed:</b> {speedKmh:F1} km/h <size=11>({finalSpeed:F1} m/s)</size>{burstTag}{standTag}");

        GUILayout.Space(2);

        // 3. Cadence Meter (filling bar 0 to 8)
        float cad01 = Mathf.Clamp01(cadence / cadenceMax);
        Color cadColor = Color.Lerp(new Color(0.25f, 0.8f, 0.25f), new Color(1.0f, 0.55f, 0.0f), cad01);
        if (cadencePulseTimer > 0f) cadColor = Color.white;
        GUILayout.Label($"<b>Cadence Meter:</b> {cadence:F1} / {cadenceMax:F1} RPM");
        DrawHorizontalBar(cad01, cadColor, 14);

        GUILayout.Space(3);

        // 4. Shared Stamina Bar (labeled "Stamina")
        float stam01 = stamina / staminaMax;
        Color stamCol = isExhausted ? Color.red : (stam01 >= 0.5f ? new Color(0.1f, 0.85f, 0.2f) : (stam01 >= 0.2f ? Color.yellow : new Color(1.0f, 0.35f, 0.1f)));
        string statusStr = isExhausted ? " <color=red>[EXHAUSTION - 30% SPEED]</color>" : (cadence < 1.0f ? " <color=cyan>[Coasting +0.5/s]</color>" : "");
        GUILayout.Label($"<b>Stamina:</b> {stamina:F0}%{statusStr}");
        DrawHorizontalBar(stam01, stamCol, 12);

        GUILayout.Space(3);

        // 5. Streak Counter (Â§5.2: 5 PERFECT -> +5 Stamina)
        string stars = new string('*', perfectStreak) + new string('-', 5 - perfectStreak);
        GUILayout.Label($"<b>Streak:</b> {perfectStreak}/5 <color=yellow>[{stars}]</color> <size=11>(5 PERFECT -> +5 Stamina)</size>");

        GUILayout.Space(2);

        // 6. Rhythm Timing Bar & Moving Marker
        GUILayout.Label("<b>Rhythm Timing (Press SPACE on Green):</b>");
        DrawRhythmTimingBar();

        // 7. Timing Result Feedback
        if (lastTimingResult == TimingResult.Perfect)
        {
            GUI.color = Color.green;
            GUILayout.Label("<b>PERFECT! Speed Burst Active!</b>");
            GUI.color = Color.white;
        }
        else if (lastTimingResult == TimingResult.Good)
        {
            GUI.color = Color.yellow;
            GUILayout.Label("<b>GOOD!</b>");
            GUI.color = Color.white;
        }
        else if (lastTimingResult == TimingResult.Miss)
        {
            GUI.color = Color.red;
            GUILayout.Label("<b>MISS! (Wait for Green Zone)</b>");
            GUI.color = Color.white;
        }

        // 8. Anti-Cheese Warning
        if (antiCheeseTriggered)
        {
            GUI.color = Color.red;
            GUILayout.Label("<b>ANTI-CHEESE: Same paddle repeated! Cadence halved!</b>");
            GUI.color = Color.white;
        }

        // 9. Steering Indicator
        string steerStr = steerVal < -0.1f ? "STEERING LEFT <--" : (steerVal > 0.1f ? "STEERING RIGHT -->" : "STRAIGHT");
        GUILayout.Label($"<size=11>Steer: {steerStr} (A / D or Swipe)</size>");

        GUILayout.EndArea();

        // 10. On-Screen Touch Controls (Android)
        // Only Left/Right Pedals and Center Rhythm Hit (Stand & Lean buttons removed per user request)
        if (showTouchButtons)
        {
            DrawAndroidTouchButtons();
        }
    }

    private void DrawHorizontalBar(float fill01, Color fillColor, float height)
    {
        Rect r = GUILayoutUtility.GetRect(20, height);
        // Background
        GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        // Fill
        GUI.color = fillColor;
        Rect fill = new Rect(r.x, r.y, r.width * Mathf.Clamp01(fill01), r.height);
        GUI.DrawTexture(fill, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private void DrawRhythmTimingBar()
    {
        Rect barRect = GUILayoutUtility.GetRect(24, 22);

        // Bar background (Dark Gray)
        GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);
        GUI.DrawTexture(barRect, Texture2D.whiteTexture);

        // Yellow Good Zone
        float goodX = barRect.x + (perfectZoneCenter - goodZoneHalfWidth) * barRect.width;
        float goodW = (goodZoneHalfWidth * 2f) * barRect.width;
        GUI.color = new Color(0.85f, 0.75f, 0.15f, 0.85f);
        GUI.DrawTexture(new Rect(goodX, barRect.y, goodW, barRect.height), Texture2D.whiteTexture);

        // Green Perfect Zone
        float greenX = barRect.x + (perfectZoneCenter - perfectZoneHalfWidth) * barRect.width;
        float greenW = (perfectZoneHalfWidth * 2f) * barRect.width;
        GUI.color = new Color(0.1f, 0.95f, 0.25f, 0.95f);
        GUI.DrawTexture(new Rect(greenX, barRect.y, greenW, barRect.height), Texture2D.whiteTexture);

        // Moving Marker (Red Line Pointer)
        float markerX = barRect.x + (rhythmMarkerPos * barRect.width) - 3f;
        GUI.color = Color.red;
        GUI.DrawTexture(new Rect(markerX, barRect.y - 2, 6, barRect.height + 4), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private void DrawAndroidTouchButtons()
    {
        int btnW = 120;
        int btnH = 80;
        int margin = 20;

        // Bottom Left Zone: [ LEFT PEDAL ]
        Rect leftZoneRect = new Rect(margin, Screen.height - btnH - margin, btnW, btnH);
        GUI.color = (lastPedal == PedalSide.Left) ? new Color(0.6f, 0.6f, 0.6f, 0.95f) : Color.white;
        if (GUI.Button(leftZoneRect, "<size=15><b>[ LEFT ]\nPEDAL</b></size>"))
        {
            OnPedalInput(PedalSide.Left);
        }

        // Bottom Right Zone: [ RIGHT PEDAL ]
        Rect rightZoneRect = new Rect(Screen.width - btnW - margin, Screen.height - btnH - margin, btnW, btnH);
        GUI.color = (lastPedal == PedalSide.Right) ? new Color(0.6f, 0.6f, 0.6f, 0.95f) : Color.white;
        if (GUI.Button(rightZoneRect, "<size=15><b>[ RIGHT ]\nPEDAL</b></size>"))
        {
            OnPedalInput(PedalSide.Right);
        }

        // Bottom Center: [ SPACE / HIT ] (Hit on Green!)
        int centerW = 160;
        Rect centerBtnRect = new Rect((Screen.width - centerW) / 2, Screen.height - btnH - margin, centerW, btnH);
        GUI.color = new Color(0.2f, 0.9f, 0.4f, 0.95f);
        if (GUI.Button(centerBtnRect, "<size=13><b>[ SPACE / HIT ]\nRHYTHM HIT\n(ON GREEN)</b></size>"))
        {
            HandleSpacebarPedalInput();
        }
        GUI.color = Color.white;
    }
}


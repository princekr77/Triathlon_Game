using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using UnityEngine.SceneManagement;
using rayzngames;

/// <summary>
/// Cycling Stage (Cadence + Stamina + Rhythm Timing)
/// Complete Implementation of Specifications 5.1, 5.2, 5.4, 5.5:
/// 
/// 1. Pedaling & Marker Timing:
///    - Pressing Space (or Left Shift / Touch Buttons) pedals the bike and evaluates timing against the rhythm marker.
///    - PERFECT (green zone): Speed burst (1.25x), +2.0 cadence, adds to streak (5 PERFECT -> +5 Stamina).
///    - GOOD (yellow zone): Normal stroke, streak resets.
///    - MISS (outside): Weak stroke, streak resets.
/// 
/// 2. Steering (Separated from pedaling):
///    - A/D or Left/Right Arrow on Keyboard.
///    - Touch drag/swipe horizontally on screen.
/// 
/// 3. Anti-Cheese:
///    - Repeating the exact same input twice in a row (or spamming Space faster than 0.12s) halves cadence.
/// 
/// 4. Stamina Drain & Regen (5.4):
///    - Drain (cadence >= 1.0): stamina -= (1.5 + 2.0 * cadence / cadenceMax) * dt
///    - Coasting (cadence < 1.0): stamina += 0.5 * dt
///    - Exhaustion (stamina <= 0): speed locked to 30%, stamina += 1.0 * dt until 20.
/// 
/// 5. Single Unified HUD:
///    - Distance progress bar, Stamina bar, Speed readout, Cadence meter, Rhythm marker bar, Streak counter.
///    - Android on-screen pedal buttons.
///    - Finish line collider & distance trigger -> Transitions to "Swim_Scene".
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
    public float cadenceDecay = 3.0f;

    [Tooltip("Cadence penalty multiplier when anti-cheese is tripped.")]
    public float antiCheeseMultiplier = 0.5f;

    [Header("5.1 Speed & Distance Configuration")]
    [Tooltip("Base speed in m/s (default 12 m/s). 0 cadence = 3.6 m/s, 8 cadence = 16.8 m/s.")]
    public new float baseSpeed = 12.0f;

    [Tooltip("Total race distance in meters before finish (default 1000m).")]
    public float targetDistance = 1000.0f;

    [Header("5.4 Stamina Configuration")]
    [Tooltip("Maximum stamina.")]
    public float staminaMax = 100.0f;

    [Tooltip("Passive stamina regeneration per second when coasting (cadence < 1.0).")]
    public float passiveStaminaRegen = 0.5f;

    [Tooltip("Stamina recovery rate per second during exhaustion (until 20).")]
    public float exhaustionRegenRate = 1.0f;

    [Header("Rhythm Timing Marker (§2.4 & §5.1)")]
    [Tooltip("Speed of rhythm marker back-and-forth oscillation.")]
    public float rhythmBarSpeed = 1.25f;

    [Tooltip("Center of the green perfect zone (0.0 to 1.0).")]
    [Range(0.2f, 0.8f)] public float perfectZoneCenter = 0.5f;

    [Tooltip("Half-width of the green perfect zone.")]
    [Range(0.03f, 0.15f)] public float perfectZoneHalfWidth = 0.08f;

    [Tooltip("Half-width of the yellow good zone.")]
    [Range(0.10f, 0.30f)] public float goodZoneHalfWidth = 0.20f;

    [Tooltip("Speed burst multiplier granted on PERFECT timing.")]
    public float perfectSpeedBurstMultiplier = 1.25f;

    [Tooltip("Duration in seconds of the PERFECT speed burst.")]
    public float burstDuration = 1.2f;

    [Header("Windows Input Options")]
    [Tooltip("If true, pressing Spacebar alone pedals (alternates left/right per press; anti-cheese triggers if pressed < 0.12s spam). If false, alternate Left Shift and Spacebar.")]
    public bool allowSingleKeySpaceMash = true;

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
    [Tooltip("Show the Section 5.5 unified HUD overlay.")]
    public bool showHUD = true;

    [Tooltip("Show on-screen touch buttons for Android (Left Zone & Right Zone).")]
    public bool showTouchButtons = true;

    // Runtime state
    [Header("Runtime State (Read Only)")]
    [SerializeField] private float cadence = 0.0f;
    [SerializeField] private float stamina = 100.0f;
    [SerializeField] private float currentSpeed = 3.6f;
    [SerializeField] private float finalSpeed = 3.6f;
    [SerializeField] private float distanceTraveled = 0.0f;
    [SerializeField] private float steerVal = 0.0f;
    [SerializeField] private int perfectStreak = 0;
    [SerializeField] private bool isExhausted = false;
    [SerializeField] private bool isStanding = false;
    [SerializeField] private PedalSide lastPedal = PedalSide.None;
    [SerializeField] private bool antiCheeseTriggered = false;

    // Rhythm and Feedback State
    private float rhythmMarkerPos = 0.5f; // sweeps 0.0 to 1.0
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
    private Vector3 initialHipLocalPos;
    private bool hasHipPos = false;

    // Input actions
    public InputActionAsset inputActions;
    private InputAction pedalLeftAction;
    private InputAction pedalRightAction;
    private InputAction standPedalAction;
    private InputAction steerAction;

    // Accessors
    public float Cadence => cadence;
    public float CadenceMax => cadenceMax;
    public float Stamina => stamina;
    public float StaminaMax => staminaMax;
    public float FinalSpeed => finalSpeed;
    public float DistanceTraveled => distanceTraveled;
    public float TargetDistance => targetDistance;
    public int PerfectStreak => perfectStreak;
    public bool IsExhausted => isExhausted;
    public bool AntiCheeseTriggered => antiCheeseTriggered;
    public TimingResult CurrentTimingResult => lastTimingResult;

    protected override void Awake()
    {
        rbComponent = GetComponent<Rigidbody>();
        bicycleVehicle = GetComponent<BicycleVehicle>();

        // Disable legacy BikeControlsExample
        var oldControls = GetComponent<BikeControlsExample>();
        if (oldControls != null) oldControls.enabled = false;

        // Ensure bicycle vehicle is enabled and responsive
        if (bicycleVehicle != null)
        {
            bicycleVehicle.InControl(true);
            bicycleVehicle.motorForce = Mathf.Max(bicycleVehicle.motorForce, 160f);
        }

        // Find Pedalier Animator
        Transform pedalier = transform.Find("Pedalier");
        if (pedalier != null) pedalAnimatorRef = pedalier.GetComponent<Animator>();
        if (pedalAnimatorRef == null) pedalAnimatorRef = GetComponentInChildren<Animator>();
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

        // Sync with GameManager
        if (GameManager.I != null)
        {
            stamina = GameManager.I.stamina;
            perfectStreak = GameManager.I.perfectStreak;
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
                pedalLeftAction = pMap.FindAction("PedalLeft");
                pedalRightAction = pMap.FindAction("PedalRight");
                standPedalAction = pMap.FindAction("StandPedal");
                steerAction = pMap.FindAction("Steer");
            }
        }
    }

    void EnableInputs()
    {
        if (pedalLeftAction != null)
        {
            pedalLeftAction.started += ctx => PerformPedalStrokeWithTiming(PedalSide.Left);
            pedalLeftAction.Enable();
        }
        if (pedalRightAction != null)
        {
            pedalRightAction.started += ctx => HandleSpacebarPedalInput();
            pedalRightAction.Enable();
        }
        if (standPedalAction != null) standPedalAction.Enable();
        if (steerAction != null) steerAction.Enable();
    }

    void DisableInputs()
    {
        if (pedalLeftAction != null) pedalLeftAction.Disable();
        if (pedalRightAction != null) pedalRightAction.Disable();
        if (standPedalAction != null) standPedalAction.Disable();
        if (steerAction != null) steerAction.Disable();
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

        // Apply forward linear velocity to rigidbody
        if (rbComponent != null)
        {
            Vector3 vel = moveDir * finalSpeed;
            vel.y = rbComponent.linearVelocity.y; // Keep natural gravity
            rbComponent.linearVelocity = vel;
        }
    }

    protected override void Update()
    {
        if (raceFinished) return;

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

        // Timers
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
    /// Rhythm marker oscillates back and forth across [0.0, 1.0].
    /// </summary>
    void UpdateRhythmMarker()
    {
        rhythmMarkerPos = Mathf.PingPong(Time.time * rhythmBarSpeed, 1.0f);
    }

    /// <summary>
    /// Reads A/D steering and keyboard pedal inputs.
    /// </summary>
    void ReadSteeringAndKeyboardInputs()
    {
        var kb = Keyboard.current;
        var gp = Gamepad.current;

        // 1. Steering (A/D or Left/Right Arrow)
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

        // Update steer smoothly unless touch swipe is actively overriding
        if (!isTouchingForSteer)
        {
            targetSteer = Mathf.Clamp(targetSteer, -1f, 1f);
            steerVal = Mathf.MoveTowards(steerVal, targetSteer, steerSensitivity * Time.deltaTime);
        }

        // 2. Pedaling from keyboard
        // Left Shift = Left Pedal
        if (pedalLeftAction == null && kb != null && kb.leftShiftKey.wasPressedThisFrame)
        {
            PerformPedalStrokeWithTiming(PedalSide.Left);
        }

        // Spacebar = Right Pedal (or Spacebar Mash + Timing Stroke)
        if (pedalRightAction == null && kb != null && kb.spaceKey.wasPressedThisFrame)
        {
            HandleSpacebarPedalInput();
        }

        // Stand and pedal (W hold)
        bool standKey = false;
        if (standPedalAction != null) standKey = standPedalAction.IsPressed();
        else if (kb != null && kb.wKey.isPressed) standKey = true;
        isStanding = standKey && !isExhausted && stamina > 0f;
    }

    /// <summary>
    /// Triggered whenever Spacebar is pressed:
    /// - Performs the pedal stroke.
    /// - Checks anti-macro spam if single-key mashing.
    /// - Evaluates marker timing for PERFECT / GOOD / MISS stroke.
    /// </summary>
    public void HandleSpacebarPedalInput()
    {
        float now = Time.time;
        float interval = now - lastSpacePressTime;
        lastSpacePressTime = now;

        if (allowSingleKeySpaceMash)
        {
            // Anti-cheat macro penalty: pressing faster than 0.12s
            if (interval < 0.12f && interval > 0.001f)
            {
                TriggerAntiCheese("Spamming Spacebar too fast (<0.12s)! Cadence halved!");
                return;
            }

            // Alternates pedal side automatically per Space press
            PedalSide nextSide = (lastPedal == PedalSide.Left) ? PedalSide.Right : PedalSide.Left;
            PerformPedalStrokeWithTiming(nextSide);
        }
        else
        {
            // Strict dual-key mode: Spacebar is Right Pedal
            PerformPedalStrokeWithTiming(PedalSide.Right);
        }
    }

    /// <summary>
    /// Executes a pedal stroke and evaluates the Rhythm Marker:
    /// - PERFECT: Speed burst (1.25x), +2.0 cadence, streak +1 (5 -> +5 Stamina).
    /// - GOOD: Normal stroke (+1.6 cadence), streak resets.
    /// - MISS: Weak stroke (+0.5 cadence), streak resets.
    /// - If repeated same side: Anti-cheese penalty (halves cadence).
    /// </summary>
    public void PerformPedalStrokeWithTiming(PedalSide side)
    {
        if (side == PedalSide.None || raceFinished) return;

        // Anti-Cheese Check
        if (side == lastPedal)
        {
            TriggerAntiCheese($"Repeated {side} pedal twice in a row! Cadence halved!");
            return;
        }

        pressedPedalThisFrame = true;
        lastPedal = side;
        cadencePulseTimer = 0.25f;

        // Timing Evaluation against Marker
        float distFromCenter = Mathf.Abs(rhythmMarkerPos - perfectZoneCenter);

        if (distFromCenter <= perfectZoneHalfWidth)
        {
            // ⚡ PERFECT STROKE!
            lastTimingResult = TimingResult.Perfect;
            timingFeedbackTimer = 1.0f;
            burstTimer = burstDuration; // Speed burst

            cadence = Mathf.Min(cadenceMax, cadence + cadenceStep);
            perfectStreak++;
            Debug.Log($"[Timing] PERFECT STROKE! Streak: {perfectStreak}/5");

            // 5 PERFECT STREAK -> +5 Stamina bonus!
            if (perfectStreak >= 5)
            {
                stamina = Mathf.Min(staminaMax, stamina + 5.0f);
                perfectStreak = 0;
                Debug.Log("[Streak] 5 PERFECT STREAK! +5 Stamina Awarded!");
            }
        }
        else if (distFromCenter <= goodZoneHalfWidth)
        {
            // ✓ GOOD STROKE
            lastTimingResult = TimingResult.Good;
            timingFeedbackTimer = 0.8f;
            perfectStreak = 0; // Reset streak on non-perfect

            cadence = Mathf.Min(cadenceMax, cadence + cadenceStep * 0.8f);
        }
        else
        {
            // ✗ MISS STROKE
            lastTimingResult = TimingResult.Miss;
            timingFeedbackTimer = 0.8f;
            perfectStreak = 0; // Reset streak on miss

            cadence = Mathf.Min(cadenceMax, cadence + cadenceStep * 0.35f);
        }
    }

    private void TriggerAntiCheese(string message)
    {
        cadence *= antiCheeseMultiplier;
        antiCheeseTriggered = true;
        antiCheeseAlertTimer = 1.2f;
        perfectStreak = 0;
        lastTimingResult = TimingResult.Miss;
        timingFeedbackTimer = 0.8f;
        Debug.LogWarning($"[Anti-Cheese] {message} Cadence reduced to {cadence:F1}");
    }

    /// <summary>
    /// Android touch:
    /// - Left Zone (bottom-left corner): Left Pedal
    /// - Right Zone (bottom-right corner): Right Pedal
    /// - Swiping (horizontal touch drag): Steering
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

            bool isPedalZone = (ny <= 0.35f && (nx <= 0.40f || nx >= 0.60f));

            if (isPedalZone)
            {
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    if (nx <= 0.40f)
                    {
                        PerformPedalStrokeWithTiming(PedalSide.Left);
                    }
                    else if (nx >= 0.60f)
                    {
                        PerformPedalStrokeWithTiming(PedalSide.Right);
                    }
                }
            }
            else
            {
                // Swipe to steer
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
    /// Cadence Decay & Speed Conversion (§5.1):
    /// - Continuous decay when not pedaling: cadence -= 3.0 * dt
    /// - speedMult = 0.3 + 1.1 * (cadence / 8.0)
    /// - currentSpeed = baseSpeed * speedMult (3.6 to 16.8 m/s)
    /// - Stamina Gate:
    ///   - stamina >= 50: 1.0
    ///   - stamina >= 20: Lerp(0.7, 1.0)
    ///   - stamina < 20: Lerp(0.0, 0.7)
    /// - Exhaustion: speed locked to 30%
    /// - PERFECT Burst: finalSpeed *= 1.25x
    /// </summary>
    void UpdateCadenceAndSpeed()
    {
        float dt = Time.deltaTime;

        // Continuous cadence decay if no pedal press this frame
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

        // Apply PERFECT timing speed burst if active
        if (burstTimer > 0f)
        {
            burstTimer -= dt;
            finalSpeed *= perfectSpeedBurstMultiplier;
        }
    }

    /// <summary>
    /// Stamina Drain & Regen (§5.4):
    /// - Drain (Mashing): stamina -= (1.5 + 2.0 * cadence / cadenceMax) * dt
    /// - Passive Regen (Coasting): if cadence < 1.0 -> stamina += 0.5 * dt
    /// - Exhaustion: at stamina <= 0 -> locked to 30% speed + stamina += 1.0 * dt until 20.
    /// </summary>
    void UpdateStaminaLogic()
    {
        float dt = Time.deltaTime;

        if (isExhausted)
        {
            // Recovery: +1.0/s until 20
            stamina = Mathf.Min(staminaMax, stamina + exhaustionRegenRate * dt);
            if (stamina >= 20.0f)
            {
                isExhausted = false;
                Debug.Log("[Stamina] Cyclist recovered from exhaustion (Stamina >= 20).");
            }
        }
        else
        {
            if (cadence >= 1.0f)
            {
                // Mashing drain
                float drainRate = 1.5f + 2.0f * (cadence / cadenceMax);
                if (isStanding) drainRate *= 1.3f;
                stamina = Mathf.Max(0f, stamina - drainRate * dt);

                if (stamina <= 0f)
                {
                    isExhausted = true;
                    isStanding = false;
                    Debug.LogWarning("[Stamina] Cyclist exhausted! Speed locked to 30% until stamina recovers to 20.");
                }
            }
            else
            {
                // Coasting passive regen
                stamina = Mathf.Min(staminaMax, stamina + passiveStaminaRegen * dt);
            }
        }

        // Cross-scene state sync
        if (GameManager.I != null)
        {
            GameManager.I.stamina = stamina;
            GameManager.I.perfectStreak = perfectStreak;
            GameManager.I.recovering = isExhausted;
        }
    }

    void UpdatePedalAnimator()
    {
        if (pedalAnimatorRef == null) return;

        if (cadence <= 0.05f)
        {
            pedalAnimatorRef.speed = 0f;
        }
        else
        {
            float targetAnimSpeed = (cadence / cadenceMax) * animSpeedScale * 2.2f;
            pedalAnimatorRef.speed = Mathf.Clamp(targetAnimSpeed, 0.2f, 3.5f);
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
    /// Finishes race and transitions to "Swim_Scene".
    /// </summary>
    public override void TriggerFinish()
    {
        if (raceFinished) return;
        raceFinished = true;

        Debug.Log("[CyclingCadenceController] Race Finished! Transitioning to 'Swim_Scene'...");

        if (rbComponent != null)
        {
            rbComponent.linearVelocity = Vector3.zero;
            rbComponent.isKinematic = true;
        }
        if (bicycleVehicle != null)
        {
            bicycleVehicle.InControl(false);
            bicycleVehicle.verticalInput = 0f;
            bicycleVehicle.braking = true;
        }

        if (GameManager.I != null)
        {
            GameManager.I.stamina = stamina;
            GameManager.I.perfectStreak = 0;
        }

        if (TransitionManager.Instance != null)
        {
            TransitionManager.Instance.BeginTransition(
                nextScene: "Swim_Scene",
                onComplete: () => {
                    SceneManager.LoadScene("Swim_Scene");
                }
            );
        }
        else
        {
            StartCoroutine(DirectLoadSwimSceneRoutine());
        }
    }

    private IEnumerator DirectLoadSwimSceneRoutine()
    {
        yield return new WaitForSeconds(1.0f);
        SceneManager.LoadScene("Swim_Scene");
    }

    // ==========================================
    // UNIFIED HUD IMPLEMENTATION (Single OnGUI HUD)
    // Distance, Speed, Cadence, Stamina, Streak, Rhythm Marker Bar
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

        // 2. Speed Readout
        float speedKmh = finalSpeed * 3.6f;
        string burstTag = (burstTimer > 0f) ? " <color=cyan>[⚡ SPEED BURST!]</color>" : "";
        GUILayout.Label($"<b>Speed:</b> {speedKmh:F1} km/h <size=11>({finalSpeed:F1} m/s)</size>{burstTag}");

        GUILayout.Space(2);

        // 3. Cadence Meter
        float cad01 = Mathf.Clamp01(cadence / cadenceMax);
        Color cadColor = Color.Lerp(new Color(0.25f, 0.8f, 0.25f), new Color(1.0f, 0.55f, 0.0f), cad01);
        if (cadencePulseTimer > 0f) cadColor = Color.white;
        GUILayout.Label($"<b>Cadence Meter:</b> {cadence:F1} / {cadenceMax:F1} RPM");
        DrawHorizontalBar(cad01, cadColor, 14);

        GUILayout.Space(3);

        // 4. Shared Stamina Bar
        float stam01 = stamina / staminaMax;
        Color stamCol = isExhausted ? Color.red : (stam01 >= 0.5f ? new Color(0.1f, 0.85f, 0.2f) : (stam01 >= 0.2f ? Color.yellow : new Color(1.0f, 0.35f, 0.1f)));
        string statusStr = isExhausted ? " <color=red>[EXHAUSTION - 30% SPEED]</color>" : (cadence < 1.0f ? " <color=cyan>[Coasting +0.5/s]</color>" : "");
        GUILayout.Label($"<b>Stamina:</b> {stamina:F0}%{statusStr}");
        DrawHorizontalBar(stam01, stamCol, 12);

        GUILayout.Space(3);

        // 5. Streak Counter
        string stars = new string('★', perfectStreak) + new string('☆', 5 - perfectStreak);
        GUILayout.Label($"<b>Streak:</b> {perfectStreak}/5 <color=yellow>{stars}</color> <size=11>(5 PERFECT -> +5 Stamina)</size>");

        GUILayout.Space(2);

        // 6. Rhythm Timing Bar & Moving Marker
        GUILayout.Label("<b>Rhythm Timing (Press SPACE on Green):</b>");
        DrawRhythmTimingBar();

        // 7. Timing Result Feedback
        if (lastTimingResult == TimingResult.Perfect)
        {
            GUI.color = Color.green;
            GUILayout.Label("⚡ <b>PERFECT STROKE! Speed Burst Active!</b>");
            GUI.color = Color.white;
        }
        else if (lastTimingResult == TimingResult.Good)
        {
            GUI.color = Color.yellow;
            GUILayout.Label("✓ <b>GOOD STROKE!</b>");
            GUI.color = Color.white;
        }
        else if (lastTimingResult == TimingResult.Miss)
        {
            GUI.color = Color.red;
            GUILayout.Label("✗ <b>MISS! (Wait for Green Zone)</b>");
            GUI.color = Color.white;
        }

        // 8. Anti-Cheese Warning
        if (antiCheeseTriggered)
        {
            GUI.color = Color.red;
            GUILayout.Label("⚠️ <b>ANTI-CHEESE: Same input repeated! Cadence halved!</b>");
            GUI.color = Color.white;
        }

        // 9. Steering Indicator
        string steerStr = steerVal < -0.1f ? "◄ STEERING LEFT" : (steerVal > 0.1f ? "STEERING RIGHT ►" : "▲ STRAIGHT");
        GUILayout.Label($"<size=11>Steer: {steerStr} (A/D or Swipe)</size>");

        GUILayout.EndArea();

        // On-Screen Touch Controls (Android / Mobile Testing)
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
            PerformPedalStrokeWithTiming(PedalSide.Left);
        }

        // Bottom Right Zone: [ RIGHT PEDAL ]
        Rect rightZoneRect = new Rect(Screen.width - btnW - margin, Screen.height - btnH - margin, btnW, btnH);
        GUI.color = (lastPedal == PedalSide.Right) ? new Color(0.6f, 0.6f, 0.6f, 0.95f) : Color.white;
        if (GUI.Button(rightZoneRect, "<size=15><b>[ RIGHT ]\nPEDAL</b></size>"))
        {
            PerformPedalStrokeWithTiming(PedalSide.Right);
        }

        // Bottom Center: [ SPACE / PEDAL ] (Hit on Green!)
        int centerW = 160;
        Rect centerBtnRect = new Rect((Screen.width - centerW) / 2, Screen.height - btnH - margin, centerW, btnH);
        GUI.color = new Color(0.2f, 0.9f, 0.4f, 0.95f);
        if (GUI.Button(centerBtnRect, "<size=14><b>[ SPACE / PEDAL ]\nHIT ON GREEN!</b></size>"))
        {
            HandleSpacebarPedalInput();
        }
        GUI.color = Color.white;
    }
}

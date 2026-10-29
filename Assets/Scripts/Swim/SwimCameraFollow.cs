using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dedicated camera follow script for Swim_Scene.
/// Attaches to Main Camera to smoothly follow the active player (Player_RunSwim or Player_Swim)
/// using the exact real scene offset (1.00, 8.12, 9.39) and rotation Euler (22, 180, 0).
/// </summary>
public class SwimCameraFollow : MonoBehaviour
{
    public static SwimCameraFollow Instance { get; private set; }

    [Header("Target Tracking")]
    [Tooltip("Target Transform to follow (Player_RunSwim or Player_Swim). Auto-detected if null.")]
    public Transform target;

    [Tooltip("Automatically detect and switch to whichever player is currently active in the scene.")]
    public bool autoDetectActivePlayer = true;

    [Header("Position & Rotation Offsets")]
    [Tooltip("Camera offset relative to player target position. Exact scene value: (1.00, 8.12, 9.39).")]
    public Vector3 offset = new Vector3(1.0f, 8.12f, 9.39f);

    [Tooltip("Camera fixed rotation Euler angles. Exact scene value: (22, 180, 0).")]
    public Vector3 fixedEulerAngles = new Vector3(22.0f, 180.0f, 0.0f);

    [Tooltip("If true, dynamically captures the camera's initial scene position and rotation relative to target at Start.")]
    public bool captureSceneOffsetOnStart = true;

    [Header("Follow Settings")]
    [Tooltip("Smooth speed for following the target position.")]
    public float positionSmoothSpeed = 10.0f;

    [Tooltip("Lock the camera rotation to fixedEulerAngles to prevent unwanted tilt or roll.")]
    public bool lockRotation = true;

    [Tooltip("Snap camera immediately to target + offset on Start / OnEnable without lerping.")]
    public bool snapOnStart = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (target == null && autoDetectActivePlayer)
        {
            FindActiveTarget();
        }

        if (target != null && captureSceneOffsetOnStart)
        {
            offset = transform.position - target.position;
            fixedEulerAngles = transform.eulerAngles;
            Debug.Log($"[SwimCameraFollow] Captured exact scene offset: {offset}, Euler: {fixedEulerAngles}");
        }

        if (target != null && snapOnStart)
        {
            transform.position = target.position + offset;
            if (lockRotation)
            {
                transform.rotation = Quaternion.Euler(fixedEulerAngles);
            }
        }
    }

    private void LateUpdate()
    {
        // 1. If target is null or deactivated, detect active player
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            if (autoDetectActivePlayer)
            {
                FindActiveTarget();
            }
        }

        if (target == null) return;

        // 2. Follow target position smoothly
        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, positionSmoothSpeed * Time.deltaTime);

        // 3. Keep camera angle locked to the scene's real Euler rotation
        if (lockRotation)
        {
            transform.rotation = Quaternion.Euler(fixedEulerAngles);
        }
    }

    /// <summary>
    /// Finds whichever player GameObject is currently active in Swim_Scene.
    /// </summary>
    public void FindActiveTarget()
    {
        // Check SwimTransitionManager first
        if (SwimTransitionManager.Instance != null)
        {
            if (SwimTransitionManager.Instance.playerSwim != null && SwimTransitionManager.Instance.playerSwim.activeInHierarchy)
            {
                target = SwimTransitionManager.Instance.playerSwim.transform;
                return;
            }
            if (SwimTransitionManager.Instance.playerRunSwim != null && SwimTransitionManager.Instance.playerRunSwim.activeInHierarchy)
            {
                target = SwimTransitionManager.Instance.playerRunSwim.transform;
                return;
            }
        }

        // Fallback: search by name
        GameObject runner = GameObject.Find("Player_RunSwim");
        if (runner != null && runner.activeInHierarchy)
        {
            target = runner.transform;
            return;
        }

        GameObject swimmer = GameObject.Find("Player_Swim");
        if (swimmer != null && swimmer.activeInHierarchy)
        {
            target = swimmer.transform;
            return;
        }

        // Fallback by tag
        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null && taggedPlayer.activeInHierarchy)
        {
            target = taggedPlayer.transform;
        }
    }

    /// <summary>
    /// Explicitly sets a new target transform (e.g., during transition) and immediately snaps if requested.
    /// </summary>
    public void SetTarget(Transform newTarget, bool snapImmediately = false)
    {
        target = newTarget;
        if (snapImmediately && target != null)
        {
            transform.position = target.position + offset;
            if (lockRotation)
            {
                transform.rotation = Quaternion.Euler(fixedEulerAngles);
            }
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Central manager for Swim_Scene transitions:
/// 1. When Player_RunSwim interacts with "SwimPlayer_Spawn" collider:
///    - Shows prompt: "Click E to Swim"
///    - On clicking E: Player_Swim is set active, Player_RunSwim is deactivated, prompt is hidden.
/// 2. When Player_Swim interacts with "RunPlayer_Spawn" collider:
///    - Shows prompt: "Click E to exit swim"
///    - On clicking E: Player_RunSwim is set active at a different point (canal exit), Player_Swim is deactivated, prompt is hidden.
/// 3. When Player_RunSwim collides with the Finish Line:
///    - Transitions the scene to "Cycle_Scene" (via TransitionManager or direct SceneManager).
/// </summary>
public class SwimTransitionManager : MonoBehaviour
{
    public static SwimTransitionManager Instance { get; private set; }

    [Header("Player GameObjects")]
    [Tooltip("The running character GameObject (Player_RunSwim).")]
    public GameObject playerRunSwim;

    [Tooltip("The swimming character GameObject (Player_Swim).")]
    public GameObject playerSwim;

    [Header("Colliders / Trigger Zones")]
    [Tooltip("Trigger collider named 'SwimPlayer_Spawn' where player transitions into swimming.")]
    public Collider swimPlayerSpawnTrigger;

    [Tooltip("Trigger collider named 'RunPlayer_Spawn' where player exits swimming back into running.")]
    public Collider runPlayerSpawnTrigger;

    [Tooltip("Finish Line trigger collider that leads to Cycle_Scene.")]
    public Collider finishLineTrigger;

    [Header("Spawn Points & Coordinates")]
    [Tooltip("Initial start position for Player_RunSwim at the entrance dock.")]
    public Vector3 runStartSpawnPosition = new Vector3(164.06f, 11.98f, 477.75f);
    public Vector3 runStartSpawnEuler = new Vector3(0f, 180f, 0f);

    [Tooltip("Spawn position for Player_Swim in the water canal.")]
    public Vector3 swimSpawnPosition = new Vector3(161.3f, 12.25f, 470.0f);
    public Vector3 swimSpawnEuler = new Vector3(0f, 180f, 0f);

    [Tooltip("Point at Finish Line where Player_RunSwim spawns when exiting the water to directly finish the race.")]
    public Vector3 runExitSpawnPosition = new Vector3(161.3f, 23.7f, 234.57f);
    public Vector3 runExitSpawnEuler = new Vector3(0f, 180f, 0f);

    [Header("UI Prompts")]
    [Tooltip("Text displayed when touching SwimPlayer_Spawn.")]
    public string enterSwimPrompt = "Click E to Swim";

    [Tooltip("Text displayed when touching RunPlayer_Spawn.")]
    public string exitSwimPrompt = "Click E to exit swim";

    [Header("Scene Transition")]
    [Tooltip("Next scene to load when touching the finish line.")]
    public string nextSceneName = "Cycle_Scene";

    [Header("GUI Styling")]
    [Tooltip("Enable modern OnGUI overlay prompt.")]
    public bool enableOnGUIPrompt = true;

    // Runtime state
    private bool canEnterSwim = false;
    private bool canExitSwim = false;
    private bool isTransitioningScene = false;
    private string currentPrompt = "";

    // Proximity tolerance for failsafe zone detection
    private const float EnterSwimRadius = 4.0f;
    private const float ExitSwimRadius = 5.0f;
    private const float FinishLineRadius = 4.0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 1. Locate players if not assigned
        ResolvePlayerReferences();

        // 2. Locate or create triggers if missing
        ResolveTriggerZones();

        // 3. Initial character state: Player_RunSwim is active, Player_Swim is inactive
        if (playerRunSwim != null)
        {
            playerRunSwim.SetActive(true);
        }

        if (playerSwim != null)
        {
            playerSwim.SetActive(false);
        }
    }

    private void Start()
    {
        // Fade in from black if TransitionManager exists
        if (TransitionManager.Instance != null)
        {
            StartCoroutine(TransitionManager.Instance.FadeFromBlackRoutine());
        }
    }

    private void Update()
    {
        // 1. Check for 'E' key press to perform transition
        if (IsEKeyPressed())
        {
            if (canEnterSwim)
            {
                SwitchToSwim();
            }
            else if (canExitSwim)
            {
                SwitchToRun();
            }
        }

        // 2. Failsafe proximity checking (ensures prompt shows even if PhysX triggers miss)
        CheckProximitySafety();
    }

    /// <summary>
    /// Switches character from Player_RunSwim to Player_Swim.
    /// </summary>
    public void SwitchToSwim()
    {
        Debug.Log("[SwimTransitionManager] 'E' pressed: Entering swim mode. Activating Player_Swim.");
        canEnterSwim = false;
        ClearPrompt();

        // 1. Activate Player_Swim at swim spawn location
        if (playerSwim != null)
        {
            playerSwim.transform.position = swimSpawnPosition;
            playerSwim.transform.rotation = Quaternion.Euler(swimSpawnEuler);
            playerSwim.SetActive(true);

            var swimmer = playerSwim.GetComponent<SwimmerController>();
            if (swimmer != null)
            {
                swimmer.SnapToWaterSurface();
            }
        }

        // 2. Deactivate Player_RunSwim
        if (playerRunSwim != null)
        {
            playerRunSwim.SetActive(false);
        }

        // 3. Update Camera Follow Target
        if (SwimCameraFollow.Instance != null && playerSwim != null)
        {
            SwimCameraFollow.Instance.SetTarget(playerSwim.transform, true);
        }
    }

    /// <summary>
    /// Switches character from Player_Swim to Player_RunSwim at the Finish Line and immediately transitions to Cycle_Scene.
    /// </summary>
    public void SwitchToRun()
    {
        canExitSwim = false;
        ClearPrompt();

        // 1. Determine spawn position at Finish Line
        Vector3 targetSpawnPos = (finishLineTrigger != null) ? finishLineTrigger.bounds.center : runExitSpawnPosition;
        Debug.Log($"[SwimTransitionManager] Spawning Player_RunSwim directly at Finish Line: {targetSpawnPos}");

        // 2. Activate Player_RunSwim at the Finish Line
        if (playerRunSwim != null)
        {
            // Disable CharacterController temporarily so transform repositioning is respected
            var cc = playerRunSwim.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            playerRunSwim.transform.position = targetSpawnPos;
            playerRunSwim.transform.rotation = Quaternion.Euler(runExitSpawnEuler);

            if (cc != null) cc.enabled = true;

            playerRunSwim.SetActive(true);
        }

        // 3. Deactivate Player_Swim
        if (playerSwim != null)
        {
            playerSwim.SetActive(false);
        }

        // 4. Update Camera Follow Target
        if (SwimCameraFollow.Instance != null && playerRunSwim != null)
        {
            SwimCameraFollow.Instance.SetTarget(playerRunSwim.transform, true);
        }

        // 5. Directly transition to Cycle_Scene
        TriggerFinishLine();
    }

    /// <summary>
    /// Triggered when Player_RunSwim crosses the finish line.
    /// </summary>
    public void TriggerFinishLine()
    {
        if (isTransitioningScene) return;
        isTransitioningScene = true;

        Debug.Log($"[SwimTransitionManager] Finish line reached! Transitioning to scene: '{nextSceneName}'");
        ClearPrompt();

        if (TransitionManager.Instance != null)
        {
            TransitionManager.Instance.BeginTransition(
                nextScene: nextSceneName,
                onComplete: () =>
                {
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

    #region Zone Callback Handlers

    public void OnZoneEnter(SwimTriggerZone.ZoneKind kind, Collider other)
    {
        switch (kind)
        {
            case SwimTriggerZone.ZoneKind.EnterSwim:
                if (IsRunPlayer(other))
                {
                    canEnterSwim = true;
                    SetPrompt(enterSwimPrompt);
                }
                break;

            case SwimTriggerZone.ZoneKind.ExitSwim:
                if (IsSwimPlayer(other))
                {
                    Debug.Log("[SwimTransitionManager] Swim player collided with RunPlayer_Spawn! Directly spawning runner at finish line and switching scene...");
                    SwitchToRun();
                }
                break;

            case SwimTriggerZone.ZoneKind.FinishLine:
                if (IsRunPlayer(other))
                {
                    TriggerFinishLine();
                }
                break;
        }
    }

    public void OnZoneExit(SwimTriggerZone.ZoneKind kind, Collider other)
    {
        switch (kind)
        {
            case SwimTriggerZone.ZoneKind.EnterSwim:
                if (IsRunPlayer(other))
                {
                    canEnterSwim = false;
                    if (currentPrompt == enterSwimPrompt) ClearPrompt();
                }
                break;

            case SwimTriggerZone.ZoneKind.ExitSwim:
                if (IsSwimPlayer(other))
                {
                    canExitSwim = false;
                    if (currentPrompt == exitSwimPrompt) ClearPrompt();
                }
                break;
        }
    }

    #endregion

    #region Proximity & Failsafe Checks

    private void CheckProximitySafety()
    {
        // 1. If Player_RunSwim is active
        if (playerRunSwim != null && playerRunSwim.activeInHierarchy)
        {
            Vector3 runPos = playerRunSwim.transform.position;

            // Check SwimPlayer_Spawn trigger
            if (swimPlayerSpawnTrigger != null)
            {
                bool inZone = swimPlayerSpawnTrigger.bounds.Contains(runPos)
                           || Vector3.Distance(runPos, swimPlayerSpawnTrigger.bounds.center) < EnterSwimRadius;

                if (inZone && !canEnterSwim)
                {
                    canEnterSwim = true;
                    SetPrompt(enterSwimPrompt);
                }
                else if (!inZone && canEnterSwim)
                {
                    canEnterSwim = false;
                    if (currentPrompt == enterSwimPrompt) ClearPrompt();
                }
            }

            // Check Finish Line
            if (finishLineTrigger != null && !isTransitioningScene)
            {
                bool atFinish = finishLineTrigger.bounds.Contains(runPos)
                             || Vector3.Distance(runPos, finishLineTrigger.bounds.center) < FinishLineRadius;

                if (atFinish)
                {
                    TriggerFinishLine();
                }
            }
        }

        // 2. If Player_Swim is active
        if (playerSwim != null && playerSwim.activeInHierarchy)
        {
            Vector3 swimPos = playerSwim.transform.position;

            if (runPlayerSpawnTrigger != null)
            {
                bool inZone = runPlayerSpawnTrigger.bounds.Contains(swimPos)
                           || Vector3.Distance(swimPos, runPlayerSpawnTrigger.bounds.center) < ExitSwimRadius;

                if (inZone)
                {
                    Debug.Log("[SwimTransitionManager] Swim player in proximity to RunPlayer_Spawn! Directly spawning runner at finish line.");
                    SwitchToRun();
                }
            }
        }
    }

    private bool IsRunPlayer(Collider other)
    {
        if (playerRunSwim != null)
        {
            if (other.gameObject == playerRunSwim || other.transform.IsChildOf(playerRunSwim.transform))
                return true;
        }
        return other.GetComponentInParent<RunSwimController>() != null;
    }

    private bool IsSwimPlayer(Collider other)
    {
        if (playerSwim != null)
        {
            if (other.gameObject == playerSwim || other.transform.IsChildOf(playerSwim.transform))
                return true;
        }
        return other.GetComponentInParent<SwimmerController>() != null;
    }

    private void SetPrompt(string prompt)
    {
        currentPrompt = prompt;
    }

    private void ClearPrompt()
    {
        currentPrompt = "";
    }

    private bool IsEKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && (kb.eKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
        {
            return true;
        }

        var gp = Gamepad.current;
        if (gp != null && (gp.buttonNorth.wasPressedThisFrame || gp.buttonSouth.wasPressedThisFrame))
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return))
        {
            return true;
        }
#endif
        return false;
    }

    #endregion

    #region Auto-Detection & Trigger Setup

    private void ResolvePlayerReferences()
    {
        if (playerRunSwim == null)
        {
            playerRunSwim = FindGameObjectInScene("Player_RunSwim");
        }

        if (playerSwim == null)
        {
            playerSwim = FindGameObjectInScene("Player_Swim");
        }
    }

    private void ResolveTriggerZones()
    {
        // 1. SwimPlayer_Spawn
        if (swimPlayerSpawnTrigger == null)
        {
            var go = FindGameObjectInScene("SwimPlayer_Spawn");
            if (go != null)
            {
                swimPlayerSpawnTrigger = go.GetComponent<Collider>();
            }
            else
            {
                go = new GameObject("SwimPlayer_Spawn");
                go.transform.position = new Vector3(161.3f, 12.0f, 473.0f);
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(16.0f, 4.0f, 4.0f);
                swimPlayerSpawnTrigger = box;
            }
        }
        EnsureTriggerZoneComponent(swimPlayerSpawnTrigger, SwimTriggerZone.ZoneKind.EnterSwim);

        // 2. RunPlayer_Spawn
        if (runPlayerSpawnTrigger == null)
        {
            var go = FindGameObjectInScene("RunPlayer_Spawn");
            if (go != null)
            {
                runPlayerSpawnTrigger = go.GetComponent<Collider>();
            }
            else
            {
                go = new GameObject("RunPlayer_Spawn");
                go.transform.position = new Vector3(161.3f, 12.6f, 250.0f);
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(16.0f, 4.0f, 6.0f);
                runPlayerSpawnTrigger = box;
            }
        }
        EnsureTriggerZoneComponent(runPlayerSpawnTrigger, SwimTriggerZone.ZoneKind.ExitSwim);

        // 3. Finish Line
        if (finishLineTrigger == null)
        {
            var go = FindGameObjectInScene("Finish Line") ?? FindGameObjectInScene("FinishLine");
            if (go != null)
            {
                finishLineTrigger = go.GetComponent<Collider>();
            }
            else
            {
                go = new GameObject("Finish Line");
                go.tag = "Finish";
                go.transform.position = new Vector3(161.3f, 12.0f, 230.0f);
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(18.0f, 5.0f, 4.0f);
                finishLineTrigger = box;

                var ft = go.AddComponent<FinishTrigger>();
                ft.nextScene = nextSceneName;
            }
        }
        EnsureTriggerZoneComponent(finishLineTrigger, SwimTriggerZone.ZoneKind.FinishLine);
    }

    private void EnsureTriggerZoneComponent(Collider col, SwimTriggerZone.ZoneKind kind)
    {
        if (col == null) return;
        col.isTrigger = true;

        var zone = col.GetComponent<SwimTriggerZone>();
        if (zone == null)
        {
            zone = col.gameObject.AddComponent<SwimTriggerZone>();
        }
        zone.zoneKind = kind;
        zone.manager = this;
    }

    private GameObject FindGameObjectInScene(string name)
    {
        // Check active objects
        GameObject found = GameObject.Find(name);
        if (found != null) return found;

        // Check root and children including inactive objects
        Scene scene = SceneManager.GetActiveScene();
        if (scene.isLoaded)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
                var match = FindInChildren(root.transform, name);
                if (match != null) return match.gameObject;
            }
        }
        return null;
    }

    private Transform FindInChildren(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name) return child;
            Transform deeper = FindInChildren(child, name);
            if (deeper != null) return deeper;
        }
        return null;
    }

    #endregion

    #region OnGUI UI Rendering

    private void OnGUI()
    {
        if (!enableOnGUIPrompt || string.IsNullOrEmpty(currentPrompt)) return;

        // Responsive centered prompt pill
        float bannerWidth = Mathf.Clamp(Screen.width * 0.45f, 320f, 600f);
        float bannerHeight = Mathf.Clamp(Screen.height * 0.08f, 54f, 80f);
        float x = (Screen.width - bannerWidth) * 0.5f;
        float y = Screen.height - bannerHeight - Mathf.Clamp(Screen.height * 0.06f, 30f, 70f);
        Rect rect = new Rect(x, y, bannerWidth, bannerHeight);

        // Mobile touch tap on the prompt banner triggers the transition
        if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
        {
            if (canEnterSwim)
            {
                SwitchToSwim();
                return;
            }
            else if (canExitSwim)
            {
                SwitchToRun();
                return;
            }
        }

        // Background box
        Color prevBg = GUI.backgroundColor;
        Color prevContent = GUI.contentColor;

        GUI.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 0.88f);
        GUI.Box(rect, GUIContent.none);

        float keyW = Mathf.Clamp(bannerHeight * 0.72f, 36f, 52f);
        float keyH = keyW;
        float keyY = y + (bannerHeight - keyH) * 0.5f;

        // Icon indicator "[ E ]"
        GUIStyle keyStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = Mathf.RoundToInt(keyH * 0.5f),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        keyStyle.normal.textColor = new Color(0.98f, 0.84f, 0.15f); // Gold highlight
        GUI.Box(new Rect(x + 12f, keyY, keyW, keyH), "E", keyStyle);

        // Text prompt
        GUIStyle textStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(bannerHeight * 0.35f),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        textStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x + keyW + 24f, y, bannerWidth - keyW - 32f, bannerHeight), currentPrompt, textStyle);

        GUI.backgroundColor = prevBg;
        GUI.contentColor = prevContent;
    }

    #endregion
}

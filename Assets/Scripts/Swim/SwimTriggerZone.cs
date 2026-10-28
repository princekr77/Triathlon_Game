using UnityEngine;

/// <summary>
/// Trigger zone component for Swim_Scene.
/// Detects interactions for:
/// - "SwimPlayer_Spawn": Transitions Player_RunSwim to Player_Swim
/// - "RunPlayer_Spawn": Transitions Player_Swim back to Player_RunSwim at exit
/// - "Finish Line": Transitions scene to Cycle_Scene
/// </summary>
[RequireComponent(typeof(Collider))]
public class SwimTriggerZone : MonoBehaviour
{
    public enum ZoneKind
    {
        EnterSwim, // SwimPlayer_Spawn
        ExitSwim,  // RunPlayer_Spawn
        FinishLine // Finish Line
    }

    [Tooltip("The role of this trigger zone.")]
    public ZoneKind zoneKind = ZoneKind.EnterSwim;

    [Tooltip("Reference to the scene's SwimTransitionManager.")]
    public SwimTransitionManager manager;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        if (manager == null)
        {
            manager = FindFirstObjectByType<SwimTransitionManager>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (manager == null)
        {
            manager = FindFirstObjectByType<SwimTransitionManager>();
        }

        if (manager != null)
        {
            manager.OnZoneEnter(zoneKind, other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (manager == null)
        {
            manager = FindFirstObjectByType<SwimTransitionManager>();
        }

        if (manager != null)
        {
            manager.OnZoneExit(zoneKind, other);
        }
    }
}

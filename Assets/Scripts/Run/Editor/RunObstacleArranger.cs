#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utility to arrange obstacles along the track in Run_Scene.
/// Implements the 4 GDD arrangements:
/// 1. The Slalom (Left & Center blocked, Right open)
/// 2. The Reverse Slalom (Center & Right blocked, Left open)
/// 3. The Edge Trap (Left & Right blocked, Center open)
/// 4. The Double (High Skill weave: Left then Right, or Right then Left)
/// </summary>
public class RunObstacleArranger : EditorWindow
{
    [MenuItem("Tools/Triathlon/Arrange Run Obstacles")]
    public static void ShowWindow()
    {
        GetWindow<RunObstacleArranger>("Run Obstacles Arranger");
    }

    private GameObject stopBoardPrefab;
    private GameObject stopBoardBigPrefab;

    private float roadCenterX = 816.0f;
    private float laneOffset = 3.0f;
    private float roadSurfaceY = 0.1f;
    private float trackStartZ = 705.0f;
    private float finishLineZ = 244.0f;

    private void OnEnable()
    {
        if (stopBoardPrefab == null)
            stopBoardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/StopBoard.prefab");
        if (stopBoardBigPrefab == null)
            stopBoardBigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/StopBoard_Big.prefab");
    }

    private void OnGUI()
    {
        GUILayout.Label("Run Track Obstacle Placement", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Arranges obstacles from start (Z=705) to finish line (Z=244) using the 4 arrangements:\n" +
            "• 1. Slalom (Left & Center blocked)\n" +
            "• 2. Reverse Slalom (Center & Right blocked)\n" +
            "• 3. Edge Trap (Left & Right blocked)\n" +
            "• 4. Double Weave (Left -> Right or Right -> Left)",
            MessageType.Info);

        stopBoardPrefab = (GameObject)EditorGUILayout.ObjectField("StopBoard Prefab", stopBoardPrefab, typeof(GameObject), false);
        stopBoardBigPrefab = (GameObject)EditorGUILayout.ObjectField("StopBoard_Big Prefab", stopBoardBigPrefab, typeof(GameObject), false);

        roadCenterX = EditorGUILayout.FloatField("Road Center X", roadCenterX);
        laneOffset = EditorGUILayout.FloatField("Lane Offset (m)", laneOffset);
        roadSurfaceY = EditorGUILayout.FloatField("Road Surface Y", roadSurfaceY);

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate / Re-arrange Obstacles", GUILayout.Height(36)))
        {
            GenerateObstacles();
        }

        if (GUILayout.Button("Remove All Generated Obstacles", GUILayout.Height(28)))
        {
            ClearExistingObstacles();
        }
    }

    public static void ClearExistingObstacles()
    {
        var allObstacles = GameObject.FindObjectsByType<Obstacle>(FindObjectsSortMode.None);
        int count = 0;
        foreach (var obs in allObstacles)
        {
            if (obs != null && obs.gameObject.name.StartsWith("Obstacle_"))
            {
                Undo.DestroyObjectImmediate(obs.gameObject);
                count++;
            }
        }
        Debug.Log($"[RunObstacleArranger] Removed {count} obstacle objects.");
    }

    public void GenerateObstacles()
    {
        if (stopBoardPrefab == null || stopBoardBigPrefab == null)
        {
            EditorUtility.DisplayDialog("Missing Prefabs", "Please assign both StopBoard and StopBoard_Big prefabs.", "OK");
            return;
        }

        ClearExistingObstacles();

        float leftLaneX = roadCenterX - laneOffset;
        float centerLaneX = roadCenterX;
        float rightLaneX = roadCenterX + laneOffset;

        // Structured sequence using all 4 arrangements
        var arrangements = new (string name, GameObject prefab, Vector3 pos)[]
        {
            // Set 1: The Slalom (Z = 705)
            ("Obstacle_01_Slalom_Left", stopBoardPrefab, new Vector3(leftLaneX, roadSurfaceY, 705f)),
            ("Obstacle_01_Slalom_Center", stopBoardPrefab, new Vector3(centerLaneX, roadSurfaceY, 705f)),

            // Set 2: The Reverse Slalom (Z = 665)
            ("Obstacle_02_RevSlalom_Center", stopBoardPrefab, new Vector3(centerLaneX, roadSurfaceY, 665f)),
            ("Obstacle_02_RevSlalom_Right", stopBoardBigPrefab, new Vector3(rightLaneX, roadSurfaceY, 665f)),

            // Set 3: The Edge Trap (Z = 625)
            ("Obstacle_03_EdgeTrap_Left", stopBoardBigPrefab, new Vector3(leftLaneX, roadSurfaceY, 625f)),
            ("Obstacle_03_EdgeTrap_Right", stopBoardBigPrefab, new Vector3(rightLaneX, roadSurfaceY, 625f)),

            // Set 4: The Double (Z = 585 & 573)
            ("Obstacle_04_Double_Left", stopBoardPrefab, new Vector3(leftLaneX, roadSurfaceY, 585f)),
            ("Obstacle_04_Double_Right", stopBoardBigPrefab, new Vector3(rightLaneX, roadSurfaceY, 573f)),

            // Set 5: The Slalom (Z = 535)
            ("Obstacle_05_Slalom_Left", stopBoardBigPrefab, new Vector3(leftLaneX, roadSurfaceY, 535f)),
            ("Obstacle_05_Slalom_Center", stopBoardPrefab, new Vector3(centerLaneX, roadSurfaceY, 535f)),

            // Set 6: The Reverse Slalom (Z = 495)
            ("Obstacle_06_RevSlalom_Center", stopBoardBigPrefab, new Vector3(centerLaneX, roadSurfaceY, 495f)),
            ("Obstacle_06_RevSlalom_Right", stopBoardPrefab, new Vector3(rightLaneX, roadSurfaceY, 495f)),

            // Set 7: The Edge Trap (Z = 455)
            ("Obstacle_07_EdgeTrap_Left", stopBoardPrefab, new Vector3(leftLaneX, roadSurfaceY, 455f)),
            ("Obstacle_07_EdgeTrap_Right", stopBoardPrefab, new Vector3(rightLaneX, roadSurfaceY, 455f)),

            // Set 8: The Double (Z = 415 & 403)
            ("Obstacle_08_Double_Right", stopBoardBigPrefab, new Vector3(rightLaneX, roadSurfaceY, 415f)),
            ("Obstacle_08_Double_Left", stopBoardPrefab, new Vector3(leftLaneX, roadSurfaceY, 403f)),

            // Set 9: The Slalom (Z = 365)
            ("Obstacle_09_Slalom_Left", stopBoardPrefab, new Vector3(leftLaneX, roadSurfaceY, 365f)),
            ("Obstacle_09_Slalom_Center", stopBoardBigPrefab, new Vector3(centerLaneX, roadSurfaceY, 365f)),

            // Set 10: The Reverse Slalom (Z = 330)
            ("Obstacle_10_RevSlalom_Center", stopBoardPrefab, new Vector3(centerLaneX, roadSurfaceY, 330f)),
            ("Obstacle_10_RevSlalom_Right", stopBoardPrefab, new Vector3(rightLaneX, roadSurfaceY, 330f)),

            // Set 11: The Edge Trap (Z = 295)
            ("Obstacle_11_EdgeTrap_Left", stopBoardBigPrefab, new Vector3(leftLaneX, roadSurfaceY, 295f)),
            ("Obstacle_11_EdgeTrap_Right", stopBoardPrefab, new Vector3(rightLaneX, roadSurfaceY, 295f)),
        };

        foreach (var item in arrangements)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(item.prefab);
            if (instance != null)
            {
                instance.name = item.name;
                instance.transform.position = item.pos;
                instance.transform.rotation = Quaternion.identity;
                Undo.RegisterCreatedObjectUndo(instance, "Generate Run Obstacle");
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log($"[RunObstacleArranger] Successfully generated {arrangements.Length} track obstacles!");
    }
}
#endif

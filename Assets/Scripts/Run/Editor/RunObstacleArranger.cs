#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

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
            GenerateObstacles(stopBoardPrefab, stopBoardBigPrefab, roadCenterX, laneOffset, roadSurfaceY);
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

    [MenuItem("Tools/Triathlon/Generate All Track Obstacles")]
    public static void ExecuteGenerate()
    {
        var p1 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/StopBoard.prefab");
        var p2 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/StopBoard_Big.prefab");
        if (p1 == null || p2 == null)
        {
            Debug.LogError("[RunObstacleArranger] Could not find StopBoard prefabs!");
            return;
        }

        GenerateObstacles(p1, p2, 816.0f, 3.0f, 0.1f);
    }

    public static void GenerateObstacles(GameObject pSmall, GameObject pBig, float centerX = 816.0f, float offset = 3.0f, float surfaceY = 0.1f)
    {
        if (pSmall == null || pBig == null)
        {
            EditorUtility.DisplayDialog("Missing Prefabs", "Please assign both StopBoard and StopBoard_Big prefabs.", "OK");
            return;
        }

        ClearExistingObstacles();

        float leftLaneX = centerX - offset;
        float centerLaneX = centerX;
        float rightLaneX = centerX + offset;

        GameObject obsParent = GameObject.Find("Obstacles_Group");
        if (obsParent == null)
        {
            obsParent = new GameObject("Obstacles_Group");
            Undo.RegisterCreatedObjectUndo(obsParent, "Create Obstacles Group");
        }

        // Structured sequence using all 4 arrangements
        var arrangements = new (string name, GameObject prefab, Vector3 pos)[]
        {
            // Set 1: The Slalom (Z = 705)
            ("Obstacle_01_Slalom_Left", pSmall, new Vector3(leftLaneX, surfaceY, 705f)),
            ("Obstacle_01_Slalom_Center", pSmall, new Vector3(centerLaneX, surfaceY, 705f)),

            // Set 2: The Reverse Slalom (Z = 665)
            ("Obstacle_02_RevSlalom_Center", pSmall, new Vector3(centerLaneX, surfaceY, 665f)),
            ("Obstacle_02_RevSlalom_Right", pBig, new Vector3(rightLaneX, surfaceY, 665f)),

            // Set 3: The Edge Trap (Z = 625)
            ("Obstacle_03_EdgeTrap_Left", pBig, new Vector3(leftLaneX, surfaceY, 625f)),
            ("Obstacle_03_EdgeTrap_Right", pBig, new Vector3(rightLaneX, surfaceY, 625f)),

            // Set 4: The Double (Z = 585 & 573)
            ("Obstacle_04_Double_Left", pSmall, new Vector3(leftLaneX, surfaceY, 585f)),
            ("Obstacle_04_Double_Right", pBig, new Vector3(rightLaneX, surfaceY, 573f)),

            // Set 5: The Slalom (Z = 535)
            ("Obstacle_05_Slalom_Left", pBig, new Vector3(leftLaneX, surfaceY, 535f)),
            ("Obstacle_05_Slalom_Center", pSmall, new Vector3(centerLaneX, surfaceY, 535f)),

            // Set 6: The Reverse Slalom (Z = 495)
            ("Obstacle_06_RevSlalom_Center", pBig, new Vector3(centerLaneX, surfaceY, 495f)),
            ("Obstacle_06_RevSlalom_Right", pSmall, new Vector3(rightLaneX, surfaceY, 495f)),

            // Set 7: The Edge Trap (Z = 455)
            ("Obstacle_07_EdgeTrap_Left", pSmall, new Vector3(leftLaneX, surfaceY, 455f)),
            ("Obstacle_07_EdgeTrap_Right", pSmall, new Vector3(rightLaneX, surfaceY, 455f)),

            // Set 8: The Double (Z = 415 & 403)
            ("Obstacle_08_Double_Right", pBig, new Vector3(rightLaneX, surfaceY, 415f)),
            ("Obstacle_08_Double_Left", pSmall, new Vector3(leftLaneX, surfaceY, 403f)),

            // Set 9: The Slalom (Z = 365)
            ("Obstacle_09_Slalom_Left", pSmall, new Vector3(leftLaneX, surfaceY, 365f)),
            ("Obstacle_09_Slalom_Center", pBig, new Vector3(centerLaneX, surfaceY, 365f)),

            // Set 10: The Reverse Slalom (Z = 330)
            ("Obstacle_10_RevSlalom_Center", pSmall, new Vector3(centerLaneX, surfaceY, 330f)),
            ("Obstacle_10_RevSlalom_Right", pSmall, new Vector3(rightLaneX, surfaceY, 330f)),

            // Set 11: The Edge Trap (Z = 295)
            ("Obstacle_11_EdgeTrap_Left", pBig, new Vector3(leftLaneX, surfaceY, 295f)),
            ("Obstacle_11_EdgeTrap_Right", pSmall, new Vector3(rightLaneX, surfaceY, 295f)),
        };

        foreach (var item in arrangements)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(item.prefab);
            if (instance != null)
            {
                instance.name = item.name;
                instance.transform.position = item.pos;
                instance.transform.rotation = Quaternion.identity;
                instance.transform.SetParent(obsParent.transform);
                Undo.RegisterCreatedObjectUndo(instance, "Generate Run Obstacle");
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[RunObstacleArranger] Successfully generated {arrangements.Length} track obstacles!");
    }
}
#endif

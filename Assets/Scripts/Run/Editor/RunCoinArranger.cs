#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

/// <summary>
/// Editor utility to arrange collectible coins along the track in Run_Scene.
/// Implements all 5 GDD coin formations:
/// 1. The "Straight Line" (The Safe Zone - 5-10 coins leading into safe lanes)
/// 2. The "Slalom Arc" (The Skill Path - dynamic curving path forcing steering)
/// 3. The "Risk vs. Reward" (The Dangerous Path - coins skimming obstacles near 60% mark)
/// 4. The "Jump Arc" (The Airborne Path - parabolic bell curve over StopBoard hurdles)
/// 5. The "Finish Line Spam" (Massive 3-lane wall of coins before finish line)
/// </summary>
public class RunCoinArranger : EditorWindow
{
    [MenuItem("Tools/Triathlon/Arrange Run Coins")]
    public static void ShowWindow()
    {
        GetWindow<RunCoinArranger>("Run Coins Arranger");
    }

    private GameObject goldCoinPrefab;
    private float roadCenterX = 816.0f;
    private float laneOffset = 3.0f;
    private float roadSurfaceY = 0.10f;
    private float waistCoinY = 1.00f; // 0.9m above road surface

    private void OnEnable()
    {
        if (goldCoinPrefab == null)
        {
            goldCoinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/GoldCoin.prefab");
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Run Track Coin Placement (5 Formations)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Arranges 83 coins across all 5 GDD formations from start to finish line:\n" +
            "• 1. Straight Line (Safe lanes before Slalom & Edge Traps)\n" +
            "• 2. Slalom Arc (Curving steering paths on open stretches)\n" +
            "• 3. Risk vs. Reward (Coins grazing obstacles at 60% mark)\n" +
            "• 4. Jump Arc (Airborne parabolic arcs over StopBoard hurdles)\n" +
            "• 5. Finish Line Spam (3-lane dopamine wall from Z=282 to Z=255)",
            MessageType.Info);

        goldCoinPrefab = (GameObject)EditorGUILayout.ObjectField("GoldCoin Prefab", goldCoinPrefab, typeof(GameObject), false);
        roadCenterX = EditorGUILayout.FloatField("Road Center X", roadCenterX);
        laneOffset = EditorGUILayout.FloatField("Lane Offset (m)", laneOffset);
        roadSurfaceY = EditorGUILayout.FloatField("Road Surface Y", roadSurfaceY);
        waistCoinY = EditorGUILayout.FloatField("Standard Coin Y", waistCoinY);

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate / Re-arrange Coins (83 Coins)", GUILayout.Height(36)))
        {
            GenerateCoins(goldCoinPrefab, roadCenterX, laneOffset, waistCoinY);
        }

        if (GUILayout.Button("Remove All Generated Coins", GUILayout.Height(28)))
        {
            ClearExistingCoins();
        }
    }

    public static void ClearExistingCoins()
    {
        var allCoins = GameObject.FindObjectsByType<Coin>(FindObjectsSortMode.None);
        int count = 0;
        foreach (var c in allCoins)
        {
            if (c != null && (c.gameObject.name.StartsWith("Coin_") || c.gameObject.name == "GoldCoin"))
            {
                Undo.DestroyObjectImmediate(c.gameObject);
                count++;
            }
        }
        Debug.Log($"[RunCoinArranger] Removed {count} coin objects.");
    }

    [MenuItem("Tools/Triathlon/Generate All Track Coins")]
    public static void ExecuteGenerate()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/GoldCoin.prefab");
        if (prefab == null)
        {
            Debug.LogError("[RunCoinArranger] Could not find Assets/Prefab/GoldCoin.prefab!");
            return;
        }

        GenerateCoins(prefab, 816.0f, 3.0f, 1.00f);
    }

    public static void GenerateCoins(GameObject prefab, float centerX = 816.0f, float offset = 3.0f, float coinY = 1.00f)
    {
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Missing Prefab", "Please assign the GoldCoin prefab.", "OK");
            return;
        }

        ClearExistingCoins();

        float leftX = centerX - offset;   // 813.0
        float midX = centerX;            // 816.0
        float rightX = centerX + offset;  // 819.0

        var coinPositions = new System.Collections.Generic.List<(string name, Vector3 pos)>();

        // =========================================================================
        // 1. THE "STRAIGHT LINE" (The Safe Zone - Training & Guidance)
        // =========================================================================
        // Formation 1A: Guiding into open Right lane before Set 1 Slalom (Z = 705)
        float[] f1A_Z = { 730f, 727f, 724f, 721f, 718f, 715f };
        for (int i = 0; i < f1A_Z.Length; i++)
            coinPositions.Add(($"Coin_F1_Straight_A_{i+1:D2}", new Vector3(rightX, coinY, f1A_Z[i])));

        // Formation 1B: Guiding down Center lane into Set 3 Edge Trap (Z = 625)
        float[] f1B_Z = { 650f, 647f, 644f, 641f, 638f, 635f };
        for (int i = 0; i < f1B_Z.Length; i++)
            coinPositions.Add(($"Coin_F1_Straight_B_{i+1:D2}", new Vector3(midX, coinY, f1B_Z[i])));

        // Formation 1C: Guiding down Center lane into Set 7 Edge Trap (Z = 455)
        float[] f1C_Z = { 480f, 477f, 474f, 471f, 468f, 465f };
        for (int i = 0; i < f1C_Z.Length; i++)
            coinPositions.Add(($"Coin_F1_Straight_C_{i+1:D2}", new Vector3(midX, coinY, f1C_Z[i])));

        // =========================================================================
        // 2. THE "SLALOM ARC" (The Skill Path - Dynamic Steering)
        // =========================================================================
        // Formation 2A: Open stretch between Set 1 and Set 2 (Right lane to Left lane)
        var f2A = new (float x, float z)[]
        {
            (rightX, 692f),
            (rightX - 1.0f, 689f),
            (rightX - 2.0f, 686f),
            (midX, 683f),
            (leftX + 2.0f, 680f),
            (leftX + 1.0f, 677f),
            (leftX, 674f)
        };
        for (int i = 0; i < f2A.Length; i++)
            coinPositions.Add(($"Coin_F2_SlalomArc_A_{i+1:D2}", new Vector3(f2A[i].x, coinY, f2A[i].z)));

        // Formation 2B: Weave through Set 4 Double Obstacles (Left at 585, Right at 573)
        var f2B = new (float x, float z)[]
        {
            (midX + 1.5f, 590f),
            (midX, 586f),
            (midX - 1.0f, 582f),
            (midX, 578f),
            (midX + 1.0f, 574f),
            (midX, 570f)
        };
        for (int i = 0; i < f2B.Length; i++)
            coinPositions.Add(($"Coin_F2_SlalomArc_B_{i+1:D2}", new Vector3(f2B[i].x, coinY, f2B[i].z)));

        // Formation 2C: Open stretch after Set 10 (Left lane arcing to Center)
        var f2C = new (float x, float z)[]
        {
            (leftX, 320f),
            (leftX + 1.0f, 317f),
            (leftX + 2.0f, 314f),
            (midX, 311f),
            (midX, 308f),
            (midX, 305f)
        };
        for (int i = 0; i < f2C.Length; i++)
            coinPositions.Add(($"Coin_F2_SlalomArc_C_{i+1:D2}", new Vector3(f2C[i].x, coinY, f2C[i].z)));

        // =========================================================================
        // 3. THE "RISK VS. REWARD" (The Dangerous Path - Near 60% mark)
        // =========================================================================
        // Set 8 has Right obstacle at Z=415 and Left obstacle at Z=403
        // Skimming Right obstacle closely (X = 817.4 vs obstacle at 819.0)
        float[] f3A_Z = { 417f, 415f, 413f };
        for (int i = 0; i < f3A_Z.Length; i++)
            coinPositions.Add(($"Coin_F3_RiskReward_Right_{i+1:D2}", new Vector3(midX + 1.4f, coinY, f3A_Z[i])));

        // Skimming Left obstacle closely (X = 814.6 vs obstacle at 813.0)
        float[] f3B_Z = { 405f, 403f, 401f };
        for (int i = 0; i < f3B_Z.Length; i++)
            coinPositions.Add(($"Coin_F3_RiskReward_Left_{i+1:D2}", new Vector3(midX - 1.4f, coinY, f3B_Z[i])));

        // =========================================================================
        // 4. THE "JUMP ARC" (The Airborne Path - Over Low StopBoard Hurdles)
        // =========================================================================
        // Set 5 hurdle at Center lane (Z = 535)
        var f4A = new (float z, float y)[]
        {
            (539f, coinY),
            (537f, coinY + 0.55f),
            (535f, coinY + 1.05f), // Apex ~2.05m
            (533f, coinY + 0.55f),
            (531f, coinY)
        };
        for (int i = 0; i < f4A.Length; i++)
            coinPositions.Add(($"Coin_F4_JumpArc_Center_{i+1:D2}", new Vector3(midX, f4A[i].y, f4A[i].z)));

        // Set 9 hurdle at Left lane (Z = 365)
        var f4B = new (float z, float y)[]
        {
            (369f, coinY),
            (367f, coinY + 0.55f),
            (365f, coinY + 1.05f), // Apex ~2.05m
            (363f, coinY + 0.55f),
            (361f, coinY)
        };
        for (int i = 0; i < f4B.Length; i++)
            coinPositions.Add(($"Coin_F4_JumpArc_Left_{i+1:D2}", new Vector3(leftX, f4B[i].y, f4B[i].z)));

        // =========================================================================
        // 5. THE "FINISH LINE SPAM" (Massive 3-Lane Reward Wall)
        // =========================================================================
        // 10 rows spaced every 3m from Z = 282 down to Z = 255 (Finish line at Z = 244)
        float[] f5_Z = { 282f, 279f, 276f, 273f, 270f, 267f, 264f, 261f, 258f, 255f };
        for (int r = 0; r < f5_Z.Length; r++)
        {
            float z = f5_Z[r];
            coinPositions.Add(($"Coin_F5_FinishSpam_Row{r+1:D2}_Left", new Vector3(leftX, coinY, z)));
            coinPositions.Add(($"Coin_F5_FinishSpam_Row{r+1:D2}_Mid", new Vector3(midX, coinY + 0.15f, z)));
            coinPositions.Add(($"Coin_F5_FinishSpam_Row{r+1:D2}_Right", new Vector3(rightX, coinY, z)));
        }

        // Parent container for clean scene hierarchy
        GameObject coinParent = GameObject.Find("Coins");
        if (coinParent == null)
            coinParent = GameObject.Find("Coins_Group");
        if (coinParent == null)
        {
            coinParent = new GameObject("Coins");
            Undo.RegisterCreatedObjectUndo(coinParent, "Create Coins Group");
        }

        // Parent must be at origin so child coordinates are exact world coordinates
        coinParent.transform.position = Vector3.zero;
        coinParent.transform.rotation = Quaternion.identity;
        coinParent.transform.localScale = Vector3.one;

        foreach (var item in coinPositions)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance != null)
            {
                instance.name = item.name;
                instance.transform.position = item.pos;
                instance.transform.rotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                instance.transform.SetParent(coinParent.transform, true);
                Undo.RegisterCreatedObjectUndo(instance, "Generate Run Coin");
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[RunCoinArranger] Successfully placed {coinPositions.Count} coins along the track!");
    }
}
#endif

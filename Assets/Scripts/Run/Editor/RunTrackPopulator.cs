#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RunTrackPopulator
{
    [MenuItem("Tools/Triathlon/Populate Run Scene Track (Obstacles & Coins)")]
    public static void Populate()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Run_Scene.unity");
        RunObstacleArranger.ExecuteGenerate();
        RunCoinArranger.ExecuteGenerate();
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[RunTrackPopulator] Successfully populated and saved Run_Scene with Obstacles and Coins!");
    }
}
#endif

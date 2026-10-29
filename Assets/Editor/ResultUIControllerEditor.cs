using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ResultUIController))]
public class ResultUIControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(15);
        GUI.backgroundColor = new Color(0.2f, 0.7f, 1f);
        if (GUILayout.Button("Rebuild Result Panel UI Layout", GUILayout.Height(36)))
        {
            ResultPanelBuilder.BuildResultPanelUI();
        }
        GUI.backgroundColor = Color.white;
    }

    [MenuItem("CONTEXT/ResultUIController/Rebuild Result Panel UI")]
    private static void RebuildContext(MenuCommand command)
    {
        ResultPanelBuilder.BuildResultPanelUI();
    }
}

using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MainMenuController))]
public class MainMenuControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(15);
        GUI.backgroundColor = new Color(0.2f, 0.7f, 1f);
        if (GUILayout.Button("Rebuild Menu Panel UI Layout", GUILayout.Height(36)))
        {
            MenuPanelBuilder.BuildMenuPanelUI();
        }
        GUI.backgroundColor = Color.white;
    }

    [MenuItem("CONTEXT/MainMenuController/Rebuild Menu Panel UI")]
    private static void RebuildMenuContext(MenuCommand command)
    {
        MenuPanelBuilder.BuildMenuPanelUI();
    }
}

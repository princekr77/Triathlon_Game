using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MainMenuController))]
public class MainMenuControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Link Start Button to Run_Scene", GUILayout.Height(30)))
        {
            MenuPanelSetup.SetupMenuController();
        }
    }
}

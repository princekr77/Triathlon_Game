using UnityEngine;
using UnityEditor;

public class MenuPanelSetup
{
    [MenuItem("Tools/Setup Main Menu Controller")]
    public static void SetupMenuController()
    {
        MenuPanelBuilder.BuildMenuPanelUI();
    }
}

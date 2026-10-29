using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public class MenuPanelSetup
{
    static MenuPanelSetup()
    {
        EditorApplication.delayCall += AutoSetupOnce;
    }

    private static void AutoSetupOnce()
    {
        if (!SessionState.GetBool("MenuPanelSetupDone_v2", false))
        {
            SessionState.SetBool("MenuPanelSetupDone_v2", true);
            SetupMenuController();
        }
    }

    [MenuItem("Tools/Setup Main Menu Controller")]
    public static void SetupMenuController()
    {
        string scenePath = "Assets/Scenes/UIScene.unity";
        var currentScene = EditorSceneManager.GetActiveScene();
        if (currentScene.path != scenePath)
        {
            if (currentScene.isDirty) EditorSceneManager.SaveScene(currentScene);
            currentScene = EditorSceneManager.OpenScene(scenePath);
        }

        GameObject menuPanel = GameObject.Find("MenuPanel");
        if (menuPanel != null)
        {
            MainMenuController mmc = menuPanel.GetComponent<MainMenuController>();
            if (mmc == null)
            {
                mmc = menuPanel.AddComponent<MainMenuController>();
            }

            Transform startBtnTr = menuPanel.transform.Find("StartButton");
            if (startBtnTr != null)
            {
                Button btn = startBtnTr.GetComponent<Button>();
                mmc.startButton = btn;

                for (int i = btn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                {
                    UnityEventTools.RemovePersistentListener(btn.onClick, i);
                }
                UnityEventTools.AddPersistentListener(btn.onClick, mmc.OnStartClicked);
                EditorUtility.SetDirty(btn);
            }

            mmc.runSceneName = "Run_Scene";

            EditorUtility.SetDirty(menuPanel);
            EditorSceneManager.MarkSceneDirty(currentScene);
            EditorSceneManager.SaveScene(currentScene);
            Debug.Log("[MenuPanelSetup] MainMenuController successfully attached to MenuPanel in UIScene and StartButton linked!");
        }
    }
}

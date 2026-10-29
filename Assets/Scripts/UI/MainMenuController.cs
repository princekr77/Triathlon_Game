using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Start button that begins the triathlon race.")]
    public Button startButton;

    [Header("Scene Configuration")]
    [Tooltip("Target scene to load on start. Default is Run_Scene.")]
    public string runSceneName = "Run_Scene";

    private void Awake()
    {
        if (startButton == null)
        {
            Transform btnTr = transform.Find("StartButton");
            if (btnTr != null)
            {
                startButton = btnTr.GetComponent<Button>();
            }
            else
            {
                var allButtons = GetComponentsInChildren<Button>(true);
                foreach (var btn in allButtons)
                {
                    if (btn.name.ToLower().Contains("start"))
                    {
                        startButton = btn;
                        break;
                    }
                }
            }
        }
    }

    private void Start()
    {
        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartClicked);
        }
        else
        {
            Debug.LogWarning("[MainMenuController] StartButton is not assigned on " + gameObject.name);
        }
    }

    public void OnStartClicked()
    {
        Debug.Log($"[MainMenuController] Starting game -> loading scene: {runSceneName}");

        if (GameManager.I != null)
        {
            GameManager.I.ResetRace();
        }

        SceneManager.LoadScene(runSceneName);
    }
}

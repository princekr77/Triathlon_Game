using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ResultUIController : MonoBehaviour
{
    [Header("Header")]
    public TextMeshProUGUI raceCompleteText;
    public TextMeshProUGUI victoryText;
    public TextMeshProUGUI newBestText;

    [Header("Center Times")]
    public TextMeshProUGUI yourTimeValue;
    public TextMeshProUGUI aiTimeValue;

    [Header("Stage Splits")]
    public TextMeshProUGUI swimTimeText;
    public TextMeshProUGUI cycleTimeText;
    public TextMeshProUGUI runTimeText;
    public TextMeshProUGUI penaltiesText;

    [Header("Stamina Report")]
    public TextMeshProUGUI enteredRunPercentText;
    public Image enteredRunProgressBar;
    public TextMeshProUGUI finalStaminaPercentText;
    public Image finalStaminaProgressBar;
    public TextMeshProUGUI theWallSubText;
    public Button pushedThroughBadge;

    [Header("Action Buttons")]
    public Button retryButton;
    public Button mainMenuButton;
    public Button nextRaceButton;

    private void Start()
    {
        if (retryButton != null) retryButton.onClick.AddListener(OnRetryClicked);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        if (nextRaceButton != null) nextRaceButton.onClick.AddListener(OnNextRaceClicked);
    }

    public void OnRetryClicked()
    {
        Debug.Log("[ResultUI] Retry clicked");
        if (GameManager.I != null)
        {
            GameManager.I.ResetRace();
            GameManager.I.LoadScene("Swim_Scene");
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
    }

    public void OnMainMenuClicked()
    {
        Debug.Log("[ResultUI] Main Menu clicked");
        if (GameManager.I != null)
        {
            GameManager.I.LoadScene("UIScene");
        }
    }

    public void OnNextRaceClicked()
    {
        Debug.Log("[ResultUI] Next Race clicked");
        if (GameManager.I != null)
        {
            GameManager.I.ResetRace();
            GameManager.I.LoadScene("Swim_Scene");
        }
    }

    public void SetResults(float yourTime, float aiTime, float swimTime, float cycleTime, float runTime, float penalties, float runEntryStamina, float finalStamina)
    {
        if (yourTimeValue != null) yourTimeValue.text = FormatTime(yourTime);
        if (aiTimeValue != null) aiTimeValue.text = FormatTime(aiTime);
        if (swimTimeText != null) swimTimeText.text = FormatTime(swimTime);
        if (cycleTimeText != null) cycleTimeText.text = FormatTime(cycleTime);
        if (runTimeText != null) runTimeText.text = FormatTime(runTime);
        if (penaltiesText != null) penaltiesText.text = $"+{penalties:0.00}s";

        if (enteredRunPercentText != null) enteredRunPercentText.text = $"{Mathf.RoundToInt(runEntryStamina)}%";
        if (enteredRunProgressBar != null) enteredRunProgressBar.fillAmount = Mathf.Clamp01(runEntryStamina / 100f);

        if (finalStaminaPercentText != null) finalStaminaPercentText.text = $"{Mathf.RoundToInt(finalStamina)}%";
        if (finalStaminaProgressBar != null) finalStaminaProgressBar.fillAmount = Mathf.Clamp01(finalStamina / 100f);
    }

    private string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
        float seconds = timeInSeconds % 60f;
        return string.Format("{0:00}:{1:00.00}", minutes, seconds);
    }
}

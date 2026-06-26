using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("UI Elements (assign in Inspector)")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI targetText;
    public GameObject levelCompletePanel;

    void Start()
    {
        if (levelCompletePanel != null)
            levelCompletePanel.SetActive(false);
    }

    // FUTURE: add animated score counter
    public void UpdateScore(int currentScore, int targetScore)
    {
        if (scoreText != null)
            scoreText.text = "Score: " + currentScore;

        if (targetText != null)
            targetText.text = "Target: " + targetScore;
    }

    // FUTURE: add star rating, particle burst
    public void ShowLevelComplete()
    {
        if (levelCompletePanel != null)
            levelCompletePanel.SetActive(true);
    }

    public void HideLevelComplete()
    {
        if (levelCompletePanel != null)
            levelCompletePanel.SetActive(false);
    }
}

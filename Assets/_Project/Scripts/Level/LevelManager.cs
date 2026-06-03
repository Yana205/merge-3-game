using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    public GridManager gridManager;
    public UIManager uiManager;
    public ScoreController scoreController;
    public LevelData[] levels;

    [Header("Runtime State")]
    public LevelData currentLevel;
    public int CurrentScore => scoreController != null ? scoreController.Score : _localScore;
    private int _localScore;

    void Start()
    {
        int levelIndex = PlayerPrefs.GetInt("SelectedLevel", 0);
        if (levelIndex >= 0 && levelIndex < levels.Length)
            LoadLevel(levels[levelIndex]);
    }

    // FUTURE: add loading screen, level transition animation
    public void LoadLevel(LevelData data)
    {
        currentLevel = data;
        _localScore = 0;
        scoreController?.ResetScore();

        // Build the grid from level data
        gridManager.CreateGrid(data.rows, data.cols);

        // Fill every cell with a Tier 1 (Obsidian) at the start
        for (int r = 0; r < data.rows; r++)
            for (int c = 0; c < data.cols; c++)
                gridManager.SpawnItem(gridManager.GetCell(r, c));

        // Reset UI
        if (uiManager != null)
        {
            uiManager.UpdateScore(CurrentScore, data.targetScore);
            uiManager.HideLevelComplete();
        }
    }

    public void AddScore(int points)
    {
        if (scoreController != null)
            scoreController.AddScore(points);
        else
            _localScore += points;

        if (uiManager != null)
        {
            uiManager.UpdateScore(CurrentScore, currentLevel.targetScore);

            if (CurrentScore >= currentLevel.targetScore)
                uiManager.ShowLevelComplete();
        }
    }

    // Called by "Next Level" button
    public void GoToNextLevel()
    {
        int currentIndex = PlayerPrefs.GetInt("SelectedLevel", 0);
        int nextIndex = currentIndex + 1;

        if (nextIndex < levels.Length)
        {
            PlayerPrefs.SetInt("SelectedLevel", nextIndex);
            SceneManager.LoadScene("Game");
        }
        else
        {
            // FUTURE: show "All Levels Complete!" screen
            SceneManager.LoadScene("MainMenu");
        }
    }
}

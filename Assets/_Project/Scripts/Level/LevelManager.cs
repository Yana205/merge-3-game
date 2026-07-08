using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    public GridManager gridManager;
    public UIManager uiManager;
    public ScoreController scoreController;
    public InputHandler inputHandler;
    public LevelData[] levels;

    [Header("Runtime State")]
    public LevelData currentLevel;
    public int CurrentScore => scoreController != null ? scoreController.Score : _localScore;
    private int _localScore;

    void Start()
    {
        if (inputHandler != null)
            inputHandler.OnGameOver += HandleGameOver;

        if (levels == null || levels.Length == 0)
        {
            Debug.LogError("LevelManager: No levels assigned.");
            return;
        }
        int levelIndex = PlayerPrefs.GetInt("SelectedLevel", 0);
        if (levelIndex >= 0 && levelIndex < levels.Length)
            LoadLevel(levels[levelIndex]);
    }

    void OnDestroy()
    {
        if (inputHandler != null)
            inputHandler.OnGameOver -= HandleGameOver;
    }

    void HandleGameOver()
    {
        if (uiManager != null)
            uiManager.ShowGameOver();
    }

    // FUTURE: add loading screen, level transition animation
    public void LoadLevel(LevelData data)
    {
        if (data == null)
        {
            Debug.LogError("LevelManager: LevelData is null — cannot load level.");
            return;
        }
        if (gridManager == null)
        {
            Debug.LogError("LevelManager: GridManager reference is missing.");
            return;
        }

        currentLevel = data;
        _localScore = 0;
        scoreController?.ResetScore();

        gridManager.CreateGrid(data.rows, data.cols);

        for (int r = 0; r < data.rows; r++)
            for (int c = 0; c < data.cols; c++)
            {
                if (Random.value < data.emptyChance)
                    continue;
                int tier = data.PickRandomTier();
                gridManager.SpawnItem(gridManager.GetCell(r, c), tier);
            }

        if (inputHandler != null)
            inputHandler.ResetState();

        if (uiManager != null)
        {
            uiManager.UpdateScore(CurrentScore, data.targetScore);
            uiManager.HideLevelComplete();
            uiManager.HideGameOver();
        }
    }

    public void AddScore(int points)
    {
        if (scoreController != null)
            scoreController.AddScore(points);
        else
            _localScore += points;

        if (uiManager != null)
            uiManager.UpdateScore(CurrentScore, currentLevel.targetScore);

        if (CurrentScore >= currentLevel.targetScore)
        {
            // Level complete — freeze the board so no more cells can be moved/merged.
            if (uiManager != null)
                uiManager.ShowLevelComplete();
            if (inputHandler != null)
                inputHandler.SetInputEnabled(false);
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
        }
        else
        {
            // All levels complete — back to the main menu on reload.
            // FUTURE: show "All Levels Complete!" screen
            PlayerPrefs.SetInt("SelectedLevel", 0);
            MenuController.ShowMenuOnNextLoad();
        }

        // Single-scene project: reloading the active scene restarts cleanly
        // at whatever "SelectedLevel" now points to. Hardcoded scene names
        // ("Game"/"MainMenu") broke this button — those scenes don't exist.
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}

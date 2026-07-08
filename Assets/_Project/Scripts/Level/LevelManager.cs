using UnityEngine;

public class LevelManager : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    public GridManager gridManager;
    public UIManager uiManager;
    public ScoreController scoreController;
    public InputHandler inputHandler;
    public LevelData[] levels;

    [Header("Transitions (assign in Inspector)")]
    public ScreenFader screenFader;
    public BackgroundFitter background;

    [Header("Services (assign in Inspector)")]
    [SerializeField] private ServiceLoader serviceLoader;

    // Fired after the last level is finished; MenuController shows the menu.
    public event System.Action OnAllLevelsComplete;

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

        // Don't touch the board until ServiceLoader has loaded the GemItem
        // Addressable and injected the ItemFactory into GridManager.
        if (serviceLoader == null)
        {
            Debug.LogError("LevelManager: serviceLoader is not assigned — loading the level without waiting for services.");
            LoadSelectedLevel();
        }
        else if (serviceLoader.IsReady)
        {
            LoadSelectedLevel();
        }
        else
        {
            serviceLoader.OnServicesReady += HandleServicesReady;
        }
    }

    void OnDestroy()
    {
        if (inputHandler != null)
            inputHandler.OnGameOver -= HandleGameOver;

        if (serviceLoader != null)
            serviceLoader.OnServicesReady -= HandleServicesReady;
    }

    void HandleServicesReady()
    {
        serviceLoader.OnServicesReady -= HandleServicesReady;
        LoadSelectedLevel();
    }

    void LoadSelectedLevel()
    {
        int levelIndex = PlayerPrefs.GetInt("SelectedLevel", 0);
        if (levelIndex >= 0 && levelIndex < levels.Length)
            LoadLevel(levels[levelIndex]);
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

        if (background != null && data.backgroundSprite != null)
            background.SetSprite(data.backgroundSprite);

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

    // Loads a level in place (no scene reload) and announces it.
    public void LoadLevelByIndex(int index)
    {
        if (levels == null || index < 0 || index >= levels.Length)
        {
            Debug.LogError("LevelManager: level index " + index + " is out of range.");
            return;
        }

        PlayerPrefs.SetInt("SelectedLevel", index);
        LoadLevel(levels[index]);

        if (uiManager != null)
            uiManager.ShowLevelBanner("Level " + (index + 1));
    }

    // Called by "Next Level" button
    public void GoToNextLevel()
    {
        int nextIndex = PlayerPrefs.GetInt("SelectedLevel", 0) + 1;

        if (nextIndex < levels.Length)
        {
            RunWithFade(() => LoadLevelByIndex(nextIndex));
        }
        else
        {
            // All levels complete — hand control back to the main menu.
            // FUTURE: show "All Levels Complete!" screen
            PlayerPrefs.SetInt("SelectedLevel", 0);
            RunWithFade(ReturnToMenu);
        }
    }

    void ReturnToMenu()
    {
        if (uiManager != null)
        {
            uiManager.HideLevelComplete();
            uiManager.HideGameOver();
        }
        OnAllLevelsComplete?.Invoke();
    }

    // Fades if a ScreenFader is wired; degrades to an instant switch if not.
    void RunWithFade(System.Action midpoint)
    {
        if (screenFader != null)
            screenFader.RunTransition(midpoint);
        else
            midpoint();
    }
}

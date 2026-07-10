using UnityEngine;

public class LevelManager : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    public GridManager gridManager;
    public UIManager uiManager;
    public ScoreController scoreController;
    public InputHandler inputHandler;
    public MergeManager mergeManager;
    public LevelData[] levels;

    [Header("Transitions (assign in Inspector)")]
    public ScreenFader screenFader;
    public BackgroundFitter background;

    [Header("Services (assign in Inspector)")]
    [SerializeField] private ServiceLoader serviceLoader;

    // Fired after the last level is finished; MenuController shows the menu.
    public event System.Action OnAllLevelsComplete;

    // Fired whenever the score changes, with (score, target). UIManager listens.
    public event System.Action<int, int> OnScoreChanged;

    // Fired once when the target score is reached. UIManager listens.
    public event System.Action OnLevelComplete;

    [Header("Runtime State")]
    public LevelData currentLevel;
    public int CurrentScore => scoreController != null ? scoreController.Score : _localScore;
    private int _localScore;

    void Start()
    {
        if (inputHandler != null)
            inputHandler.OnGameOver += HandleGameOver;

        if (mergeManager != null)
            mergeManager.OnMerged += HandleMerged;

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

        if (mergeManager != null)
            mergeManager.OnMerged -= HandleMerged;

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

        EnsureGuaranteedPairs(data.guaranteedPairs);

        if (inputHandler != null)
            inputHandler.ResetState();

        OnScoreChanged?.Invoke(CurrentScore, data.targetScore);

        if (uiManager != null)
        {
            uiManager.HideLevelComplete();
            uiManager.HideGameOver();
        }
    }

    // A random fill can start with no adjacent same-tier pair, which is an
    // instant game over on a full board. Copy a random item's tier into one
    // of its neighbours until the board has at least `wanted` merge pairs.
    void EnsureGuaranteedPairs(int wanted)
    {
        if (wanted <= 0 || gridManager == null) return;

        for (int safety = 0; safety < 64; safety++)
        {
            if (gridManager.CountAdjacentSameTierPairs() >= wanted)
                return;

            Cell source = gridManager.GetCell(
                Random.Range(0, gridManager.rows), Random.Range(0, gridManager.cols));
            if (source == null || !source.IsOccupied()
                || source.CurrentItem.Tier >= Item.MaxTier)
                continue;

            int dr = Random.Range(-1, 2), dc = Random.Range(-1, 2);
            if (dr == 0 && dc == 0) continue;
            Cell target = gridManager.GetCell(source.row + dr, source.col + dc);
            if (target == null) continue;

            int tier = source.CurrentItem.Tier;
            if (target.IsOccupied())
            {
                if (target.CurrentItem.Tier == tier) continue;
                Item old = target.CurrentItem;
                target.RemoveItem();
                gridManager.DespawnItem(old);
            }
            gridManager.SpawnItem(target, tier);
        }

        Debug.LogWarning("LevelManager: could not guarantee " + wanted + " merge pairs at board setup.");
    }

    // Scoring lives here, not in MergeManager — the merge just announces itself.
    void HandleMerged(Item newItem, Cell cell)
    {
        int points = newItem.GemData != null ? newItem.GemData.scoreValue : newItem.Tier * 10;
        AddScore(points);
    }

    public void AddScore(int points)
    {
        if (scoreController != null)
            scoreController.AddScore(points);
        else
            _localScore += points;

        OnScoreChanged?.Invoke(CurrentScore, currentLevel.targetScore);

        if (CurrentScore >= currentLevel.targetScore)
        {
            // Level complete — freeze the board so no more cells can be moved/merged.
            OnLevelComplete?.Invoke();
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

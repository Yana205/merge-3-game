using UnityEngine;

public class LevelManager : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    public GridManager gridManager;
    public UIManager uiManager;
    public ScoreController scoreController;
    public InputHandler inputHandler;
    // Retained as an inspector reference (assigned in the scene). LevelManager no
    // longer subscribes to MergeManager.OnMerged for scoring — merges now flow
    // through the GameEvents bus — but the field is kept to avoid dirtying scene
    // serialization and for future direct-hook needs.
    public MergeManager mergeManager;
    public LevelData[] levels;
    [SerializeField] private ProgressManager progressManager;

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

    // Guards the level-complete side effects (progress record, OnLevelComplete,
    // input freeze) so they fire exactly once per level, even though ScoreChanged
    // can arrive many times. Reset in LoadLevel.
    private bool _levelComplete;

    void Start()
    {
        AddListeners();

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
        RemoveListeners();

        if (serviceLoader != null)
            serviceLoader.OnServicesReady -= HandleServicesReady;
    }

    // AddListeners / RemoveListeners: this manager's event wiring in one matched
    // pair (called from Start / OnDestroy). The one-shot serviceLoader.OnServicesReady
    // subscription is conditional — only while services aren't ready yet — so it
    // stays inline in Start and is torn down in OnDestroy / HandleServicesReady.
    private void AddListeners()
    {
        if (inputHandler != null)
            inputHandler.OnGameOver += HandleGameOver;

        // Bus: react to score changes from anywhere. A merge now routes
        // GameEvents.TileMerged -> ScoreController -> GameEvents.ScoreChanged, and
        // LevelManager reacts here instead of scoring the merge itself.
        GameEvents.ScoreChanged += HandleScoreChanged;
    }

    private void RemoveListeners()
    {
        if (inputHandler != null)
            inputHandler.OnGameOver -= HandleGameOver;

        GameEvents.ScoreChanged -= HandleScoreChanged;
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
        _levelComplete = false;
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

        // With a ScoreController wired, ResetScore() above already raised
        // ScoreChanged(0) through the bus -> HandleScoreChanged refreshed the UI.
        // Only sync the UI directly on the no-ScoreController fallback path.
        if (scoreController == null)
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

    // Bus handler: ScoreController owns the number and raises ScoreChanged after
    // every change (merge scoring, manual AddScore, ResetScore). LevelManager
    // reacts by refreshing the UI signal and checking for completion — it no
    // longer computes merge points itself (that moved to ScoreController).
    void HandleScoreChanged(int total)
    {
        OnScoreChanged?.Invoke(total, currentLevel != null ? currentLevel.targetScore : 0);

        if (currentLevel != null)
            CheckLevelComplete(total);
    }

    void CheckLevelComplete(int total)
    {
        if (_levelComplete) return;
        // A non-positive target would be satisfied by the load-time ScoreChanged(0)
        // and complete the level instantly — treat it as "no target".
        if (currentLevel.targetScore <= 0) return;
        if (total < currentLevel.targetScore) return;

        _levelComplete = true;

        int levelIndex = System.Array.IndexOf(levels, currentLevel);
        if (levelIndex >= 0)
            progressManager?.RecordResult(levelIndex, total);

        // Level complete — freeze the board so no more cells can be moved/merged.
        OnLevelComplete?.Invoke();
        if (inputHandler != null)
            inputHandler.SetInputEnabled(false);
    }

    // Manual scoring seam (bonuses, tests). Routes through ScoreController so the
    // bus and completion check fire exactly like a merge does; falls back to a
    // local tally only when no ScoreController is wired.
    public void AddScore(int points)
    {
        if (scoreController != null)
        {
            scoreController.AddScore(points);   // raises ScoreChanged -> HandleScoreChanged
        }
        else
        {
            _localScore += points;
            HandleScoreChanged(_localScore);
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

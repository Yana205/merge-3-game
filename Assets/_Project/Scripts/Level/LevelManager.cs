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

    // Fired whenever the endless level number changes (run start / advance).
    public event System.Action<int> OnLevelChanged;

    [Header("Endless Mode")]
    [Tooltip("Board size for every endless level.")]
    [SerializeField] private int endlessRows = 6;
    [SerializeField] private int endlessCols = 6;
    [Tooltip("Points needed to clear level 1.")]
    [SerializeField] private int baseTarget = 120;
    [Tooltip("How much each level's point requirement grows over the previous one.")]
    [SerializeField] private int targetGrowth = 90;

    [Header("Runtime State")]
    public LevelData currentLevel;
    public int CurrentScore => scoreController != null ? scoreController.Score : _localScore;
    private int _localScore;

    // Endless run state.
    private bool _runActive;
    private int _endlessLevel;      // 1-based level number of the current run
    private int _levelTarget;       // absolute cumulative score that clears this level

    public int EndlessLevel => _endlessLevel;
    public int LevelTarget => _levelTarget;

    // Guards the level-complete side effects (progress record, OnLevelComplete,
    // input freeze) so they fire exactly once per level, even though ScoreChanged
    // can arrive many times. Reset in LoadLevel.
    private bool _levelComplete;

    void Start()
    {
        AddListeners();

        // Endless mode generates its levels procedurally, so the `levels` array is
        // no longer required to start a run.

        // Don't touch the board until ServiceLoader has loaded the GemItem
        // Addressable and injected the ItemFactory into GridManager.
        if (serviceLoader == null)
        {
            Debug.LogError("LevelManager: serviceLoader is not assigned — starting the run without waiting for services.");
            StartEndlessRun();
        }
        else if (serviceLoader.IsReady)
        {
            StartEndlessRun();
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
        StartEndlessRun();
    }

    void HandleGameOver()
    {
        // End the run and record it before the game-over screen shows the result.
        int score = CurrentScore;
        int level = _endlessLevel;
        if (_runActive)
        {
            _runActive = false;
            progressManager?.RecordRun(score, level);
        }

        if (uiManager != null)
            uiManager.ShowGameOver(score, level);
    }

    // ----- Endless mode ------------------------------------------------------

    /// <summary>
    /// Start a fresh infinite run: score back to 0, level 1, first board built.
    /// Levels then advance automatically as the running score passes each target.
    /// </summary>
    public void StartEndlessRun()
    {
        _runActive = true;
        _endlessLevel = 1;
        _levelComplete = false;
        _levelTarget = baseTarget;

        scoreController?.ResetScore();   // resets score AND raises ScoreChanged(0)
        _localScore = 0;

        BuildEndlessBoard(_endlessLevel);
        OnLevelChanged?.Invoke(_endlessLevel);
        OnScoreChanged?.Invoke(CurrentScore, _levelTarget);

        if (uiManager != null)
        {
            uiManager.HideGameOver();
            uiManager.HideLevelComplete();
            uiManager.ShowLevelBanner("Level 1");
        }
        if (inputHandler != null)
            inputHandler.ResetState();
    }

    // Clear the run's score requirement for the current level is met — build the
    // next, harder board (score carries over) and announce the new level.
    void AdvanceLevel()
    {
        _endlessLevel++;
        int increment = baseTarget + (_endlessLevel - 1) * targetGrowth;
        _levelTarget = CurrentScore + increment;

        BuildEndlessBoard(_endlessLevel);
        OnLevelChanged?.Invoke(_endlessLevel);
        OnScoreChanged?.Invoke(CurrentScore, _levelTarget);

        if (uiManager != null)
            uiManager.ShowLevelBanner("Level " + _endlessLevel);
        if (inputHandler != null)
            inputHandler.ResetState();
    }

    // Fill a fresh board for the given level WITHOUT resetting the run score.
    void BuildEndlessBoard(int level)
    {
        if (gridManager == null) return;

        LevelData data = BuildEndlessLevelData(level);
        currentLevel = data;

        gridManager.CreateGrid(data.rows, data.cols);
        for (int r = 0; r < data.rows; r++)
            for (int c = 0; c < data.cols; c++)
            {
                if (Random.value < data.emptyChance) continue;
                gridManager.SpawnItem(gridManager.GetCell(r, c), data.PickRandomTier());
            }

        EnsureGuaranteedPairs(data.guaranteedPairs);
    }

    // Procedurally scale a level: same board size, escalating target, and a
    // rising spawn floor so higher levels start with more high-tier clutter.
    LevelData BuildEndlessLevelData(int level)
    {
        var data = ScriptableObject.CreateInstance<LevelData>();
        data.rows = endlessRows;
        data.cols = endlessCols;
        data.targetScore = _levelTarget;
        data.emptyChance = 0.28f;
        data.guaranteedPairs = 3;

        // Spawn tiers 1..maxSpawn, weighted toward the low end; maxSpawn climbs
        // with the level (capped below the ladder top so a merge is always left).
        int maxSpawn = Mathf.Clamp(1 + (level - 1) / 2, 1, Mathf.Max(1, Item.MaxTier - 2));
        var table = new System.Collections.Generic.List<SpawnEntry>();
        for (int t = 1; t <= maxSpawn; t++)
            table.Add(new SpawnEntry { tier = t, weight = Mathf.Max(1f, maxSpawn - t + 1) });
        data.spawnTable = table.ToArray();

        return data;
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
        if (_runActive)
        {
            OnScoreChanged?.Invoke(total, _levelTarget);
            // Reaching the target rolls straight into the next, harder level —
            // the score carries over, so the run only ends on a board jam.
            if (total >= _levelTarget)
                AdvanceLevel();
            return;
        }

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

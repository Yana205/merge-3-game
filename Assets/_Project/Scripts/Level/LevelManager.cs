using UnityEngine;

/// <summary>
/// Runs the game as a single endless board. There is no level select, no authored
/// level data, and no depth: one grid is built when the run starts and lives until
/// it jams. Difficulty is not a stage the player crosses into — it is a continuous
/// function of the running score, applied on every move by <see cref="DifficultyCurve"/>.
///
/// This manager owns the spawn decision because it is the only object that already
/// holds references to all three parties: the score, the grid, and the input that
/// signals a completed move.
/// </summary>
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
    [SerializeField] private ProgressManager progressManager;
    [Tooltip("Optional. Cleared at the start of each run so banked charges never " +
             "carry over into a fresh board.")]
    [SerializeField] private PickaxeController pickaxeController;

    [Header("Transitions (assign in Inspector)")]
    public ScreenFader screenFader;
    public BackgroundFitter background;

    [Header("Services (assign in Inspector)")]
    [SerializeField] private ServiceLoader serviceLoader;

    // Fired whenever the score changes, with (score, best). UIManager listens.
    public event System.Action<int, int> OnScoreChanged;

    [Header("Endless Board")]
    [Tooltip("Build the board as soon as the scene loads. Off while the intro " +
             "banner owns the screen — the run begins on PRESS START.")]
    [SerializeField] private bool autoStartOnLoad = false;

    [Tooltip("Board size. Built once per run and never rebuilt.")]
    [SerializeField] private int endlessRows = 6;
    [SerializeField] private int endlessCols = 6;

    [Tooltip("Chance each cell starts empty when the board is first laid out.")]
    [Range(0f, 1f)]
    [SerializeField] private float startingEmptyChance = 0.28f;

    [Tooltip("Adjacent same-tier pairs guaranteed on the opening board. A random " +
             "fill can contain none at all, which is an instant loss.")]
    [Min(0)]
    [SerializeField] private int startingGuaranteedPairs = 3;

    [Header("Difficulty")]
    [SerializeField] private DifficultyCurve difficulty = new DifficultyCurve();

    // ----- Runtime state -----------------------------------------------------

    public int CurrentScore => scoreController != null ? scoreController.Score : _localScore;
    private int _localScore;

    private bool _runActive;

    // Best score to display: whatever previous runs recorded, or this run once it
    // overtakes them — a run in progress that beats your record should say so.
    private int BestScore
    {
        get
        {
            int recorded = progressManager != null ? progressManager.GetBestScore() : 0;
            return Mathf.Max(recorded, CurrentScore);
        }
    }

    // Services are a precondition for building a board; the menu is a
    // precondition for wanting one. Both are tracked so StartEndlessRun can be
    // called at any time and simply waits for whichever is outstanding.
    private bool _servicesReady;
    private bool _startRequested;

    void Start()
    {
        AddListeners();

        // Don't touch the board until ServiceLoader has loaded the GemItem
        // Addressable and injected the ItemFactory into GridManager.
        if (serviceLoader == null)
        {
            Debug.LogError("LevelManager: serviceLoader is not assigned — starting the run without waiting for services.");
            _servicesReady = true;
        }
        else if (serviceLoader.IsReady)
        {
            _servicesReady = true;
        }
        else
        {
            serviceLoader.OnServicesReady += HandleServicesReady;
        }

        // Off by default: the intro banner is up, and StartEndlessRun arrives
        // from MenuController when the player presses start.
        if (autoStartOnLoad)
            StartEndlessRun();
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
        {
            inputHandler.OnGameOver += HandleGameOver;
            inputHandler.OnMoveCompleted += HandleMoveCompleted;
        }

        // Bus: react to score changes from anywhere. A merge now routes
        // GameEvents.TileMerged -> ScoreController -> GameEvents.ScoreChanged, and
        // LevelManager reacts here instead of scoring the merge itself.
        GameEvents.ScoreChanged += HandleScoreChanged;
    }

    private void RemoveListeners()
    {
        if (inputHandler != null)
        {
            inputHandler.OnGameOver -= HandleGameOver;
            inputHandler.OnMoveCompleted -= HandleMoveCompleted;
        }

        GameEvents.ScoreChanged -= HandleScoreChanged;
    }

    void HandleServicesReady()
    {
        serviceLoader.OnServicesReady -= HandleServicesReady;
        _servicesReady = true;

        // Replay a start that arrived while the Addressable was still loading.
        if (_startRequested)
        {
            _startRequested = false;
            StartEndlessRun();
        }
    }

    void HandleGameOver()
    {
        // End the run and record it before the game-over screen shows the result,
        // so a new personal best is already in the leaderboard when BestScore reads it.
        int score = CurrentScore;
        if (_runActive)
        {
            _runActive = false;
            progressManager?.RecordRun(score);
        }

        GameEvents.RaiseBestScoreChanged(BestScore);

        if (uiManager != null)
            uiManager.ShowGameOver(score, BestScore);
    }

    // ----- Endless run -------------------------------------------------------

    /// <summary>
    /// Start a fresh run: score back to 0 and one board laid out. That board is
    /// never rebuilt — the run ends only when it jams.
    /// </summary>
    public void StartEndlessRun()
    {
        // Pressing start before the GemItem Addressable has landed would build a
        // board with no factory. Remember the request and run it on the callback.
        if (!_servicesReady)
        {
            _startRequested = true;
            return;
        }

        _runActive = true;

        // Open this run's leaderboard row before any score arrives, so the reset
        // below cannot grow the previous run's entry.
        progressManager?.BeginRun();

        scoreController?.ResetScore();   // resets score AND raises ScoreChanged(0)
        _localScore = 0;

        // After the score reset, not before: PickaxeController watches ScoreChanged
        // to move its goalpost, so resetting it first would let the reset-to-zero
        // event walk the goalpost straight back down again.
        pickaxeController?.ResetRun();

        BuildStartingBoard();
        OnScoreChanged?.Invoke(CurrentScore, BestScore);
        GameEvents.RaiseBestScoreChanged(BestScore);

        if (uiManager != null)
            uiManager.HideGameOver();
        if (inputHandler != null)
            inputHandler.ResetState();
    }

    // The one and only board build of a run. Everything after this is spawning
    // into the board the player is already playing on.
    void BuildStartingBoard()
    {
        if (gridManager == null) return;

        gridManager.CreateGrid(endlessRows, endlessCols);

        for (int r = 0; r < endlessRows; r++)
            for (int c = 0; c < endlessCols; c++)
            {
                if (Random.value < startingEmptyChance) continue;
                // Score 0, so the curve hands back tier 1 — the opening board is
                // pure fodder by construction, no special case needed.
                gridManager.SpawnItem(gridManager.GetCell(r, c), difficulty.PickTierAt(0));
            }

        EnsureGuaranteedPairs(startingGuaranteedPairs);
    }

    // A move landed. This is where difficulty actually bites: how many gems arrive
    // and how awkward they are is read straight off the running score.
    void HandleMoveCompleted()
    {
        if (!_runActive || gridManager == null) return;

        int score = CurrentScore;
        int count = difficulty.SpawnCountAt(score);

        for (int i = 0; i < count; i++)
        {
            Cell cell = gridManager.GetRandomEmptyCell();
            // Board is full. Stop here — InputHandler runs the jam check next.
            if (cell == null) break;

            // Family first, then tier: reds always enter at tier 1, so the tier
            // roll depends on which chain this gem landed in.
            GemFamily family = difficulty.PickFamilyAt(score);
            gridManager.SpawnItem(cell, difficulty.PickTierAt(score, family), family);
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
                || source.CurrentItem.Tier >= Item.MaxTierFor(source.CurrentItem.Family))
                continue;

            int dr = Random.Range(-1, 2), dc = Random.Range(-1, 2);
            if (dr == 0 && dc == 0) continue;
            Cell target = gridManager.GetCell(source.row + dr, source.col + dc);
            if (target == null) continue;

            // Copy family as well as tier — a "guaranteed pair" that spans two
            // chains is not a pair at all, and the board could open unplayable.
            int tier = source.CurrentItem.Tier;
            GemFamily family = source.CurrentItem.Family;
            if (target.IsOccupied())
            {
                if (target.CurrentItem.Tier == tier
                    && target.CurrentItem.Family == family) continue;
                Item old = target.CurrentItem;
                target.RemoveItem();
                gridManager.DespawnItem(old);
            }
            gridManager.SpawnItem(target, tier, family);
        }

        Debug.LogWarning("LevelManager: could not guarantee " + wanted + " merge pairs at board setup.");
    }

    // Bus handler: ScoreController owns the number and raises ScoreChanged after
    // every change. With no target to clear, this is now purely a HUD relay — the
    // score's only mechanical job is feeding DifficultyCurve on the next move.
    void HandleScoreChanged(int total)
    {
        if (!_runActive) return;

        // Keep this run's leaderboard row current on every change rather than
        // waiting for game over — otherwise refreshing the page mid-run loses the
        // run entirely, which is exactly how a saved best could sit above an
        // empty leaderboard.
        progressManager?.SubmitRunScore(total);

        OnScoreChanged?.Invoke(total, BestScore);
        GameEvents.RaiseBestScoreChanged(BestScore);
    }

    // Manual scoring seam (bonuses, tests). Routes through ScoreController so the
    // bus fires exactly like a merge does; falls back to a local tally only when
    // no ScoreController is wired.
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
}

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

    [Tooltip("Optional. Owns the armed red crystals. Referenced here only so a new " +
             "run can forget the last run's bombs.")]
    [SerializeField] private BombController bombs;

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

        // Before the board is built, so the wipe cannot catch a crystal from the new
        // board. The old board's Items are pooled by CreateGrid without passing
        // through BombController, which would otherwise keep tracking them.
        bombs?.ResetRun();

        BuildStartingBoard();

        OnScoreChanged?.Invoke(CurrentScore, BestScore);
        GameEvents.RaiseBestScoreChanged(BestScore);

        if (uiManager != null)
            uiManager.HideGameOver();
        if (inputHandler != null)
            inputHandler.ResetState();
    }

    // The one and only board build of a run. The board is filled completely — a
    // match-3 board is always full — with random colours chosen so no line of three
    // exists at the start, then handed to MergeManager to guarantee a legal move.
    void BuildStartingBoard()
    {
        if (gridManager == null) return;

        gridManager.CreateGrid(endlessRows, endlessCols);

        int colors = mergeManager != null ? mergeManager.ColorCount : 6;

        for (int r = 0; r < endlessRows; r++)
        {
            for (int c = 0; c < endlessCols; c++)
            {
                int color = PickColorNoMatch(r, c, colors);
                gridManager.SpawnItem(gridManager.GetCell(r, c), color, GemFamily.Standard);
            }
        }

        // A random fill can still deal a board with no possible swap; MergeManager
        // reshuffles until at least one move exists.
        mergeManager?.EnsurePlayable();
    }

    // A colour for cell (r,c) that does not complete a run of three with the two
    // cells already placed to its left or below it. Filling left-to-right,
    // bottom-to-top means those neighbours are the only ones that exist yet.
    int PickColorNoMatch(int r, int c, int colors)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            int color = Random.Range(1, colors + 1);
            if (c >= 2 && ColorAt(r, c - 1) == color && ColorAt(r, c - 2) == color) continue;
            if (r >= 2 && ColorAt(r - 1, c) == color && ColorAt(r - 2, c) == color) continue;
            return color;
        }
        return Random.Range(1, colors + 1);
    }

    int ColorAt(int r, int c)
    {
        Cell cell = gridManager.GetCell(r, c);
        return (cell != null && cell.IsOccupied()) ? cell.CurrentItem.Tier : 0;
    }

    // A placement resolved (and any fusion with it). Nothing spawns here any more —
    // the board only ever grows by the crystal the player just put down, which is
    // the whole point of the redesign. Kept as a hook so the run has one clear
    // "a move happened" seam for future systems.
    void HandleMoveCompleted()
    {
        if (!_runActive) return;
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

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Global event bus for cross-system, "anyone can listen" signals — the Observer
/// pattern at the app level. Publishers raise; subscribers react; neither holds a
/// reference to the other, so systems stay decoupled (e.g. a match can add score
/// without MergeManager ever knowing ScoreController exists).
///
/// Use this bus for GLOBAL signals (score changed, a gem cleared, a save wanted).
/// For a parent that owns a specific child instance, prefer a DIRECT event on the
/// child (see <see cref="Item.OnDespawned"/>) rather than routing through here.
///
/// Caller rules:
///  - Invoke only through the Raise* helpers below — they use ?.Invoke() so a bus
///    with zero listeners never throws.
///  - Subscribe in OnEnable, unsubscribe in OnDisable (or on pool return). Every
///    += needs a matching -=.
/// </summary>
public static class GameEvents
{
    // ---- Global events -----------------------------------------------------

    /// <summary>Raised after the running score total changes. Arg: the new total.</summary>
    public static event Action<int> ScoreChanged;

    /// <summary>Raised once per gem as a match clears it, BEFORE it despawns —
    /// listeners can still read its colour, data and world position.
    /// Args: the doomed Item and its Cell.</summary>
    public static event Action<Item, Cell> TileMerged;

    /// <summary>Raised once per match group as it pops. Args: cascade combo level
    /// (1 = the swap's own match, 2+ = cascades), gems cleared in this group,
    /// points the group is worth, the group's world-space centre, and the
    /// signature colour of the gems that made it (for tinted feedback).</summary>
    public static event Action<int, int, int, Vector3, Color> MatchResolved;

    /// <summary>Raised when a match of four or five turns a gem into a special.
    /// Args: the new special and its Cell.</summary>
    public static event Action<Item, Cell> SpecialCreated;

    /// <summary>Raised when a special gem goes off, BEFORE its victims pop.
    /// Args: the kind, the special's world position, its signature colour, and
    /// every cell its blast covers.</summary>
    public static event Action<SpecialKind, Vector3, Color, List<Cell>> SpecialFired;

    /// <summary>Raised for points that are not tied to one gem (special-gem
    /// bonuses). Args: the points and where they were earned.</summary>
    public static event Action<int, Vector3> BonusScore;

    /// <summary>Raised when the run's stage changes (including the reset to stage 0
    /// at run start). Args: stage index, gem colours in play, the score the stage
    /// began at, and the score the next stage begins at.</summary>
    public static event Action<int, int, int, int> StageChanged;

    /// <summary>Raised when a stage-up brings a new gem colour into play.
    /// Arg: the colour index now available (see GemPalette).</summary>
    public static event Action<int> ColorUnlocked;

    /// <summary>Raised when a swap is refused because it makes no match.
    /// Arg: the midpoint of the two gems, for a "denied" effect there.</summary>
    public static event Action<Vector3> SwapDenied;

    /// <summary>Raised when an adjacent match cracks a stone that survives the
    /// hit. Args: the stone and its Cell.</summary>
    public static event Action<Item, Cell> StoneDamaged;

    /// <summary>Raised when a stone shatters, BEFORE it despawns — listeners can
    /// still read its data and position. Args: the stone and its Cell.</summary>
    public static event Action<Item, Cell> StoneBroken;

    /// <summary>Raised when the player selects a gem (tap or press). Arg: the gem's
    /// world position. Exists so feedback (a click sound) stays out of input code.</summary>
    public static event Action<Vector3> GemSelected;

    /// <summary>Raised when a system wants persistent state written to disk.</summary>
    public static event Action SaveRequested;

    /// <summary>Raised when the player asks for a fresh run (the HUD's RESTART).</summary>
    public static event Action RestartRequested;

    /// <summary>Raised when the best recorded score changes. Arg: the new best.</summary>
    public static event Action<int> BestScoreChanged;

    // ---- Raisers -----------------------------------------------------------
    // Publishers call these instead of touching the events directly, so the
    // "?.Invoke() everywhere" rule lives in exactly one place.

    public static void RaiseScoreChanged(int newTotal) => ScoreChanged?.Invoke(newTotal);

    public static void RaiseTileMerged(Item cleared, Cell cell) => TileMerged?.Invoke(cleared, cell);

    public static void RaiseMatchResolved(int combo, int gemCount, int points, Vector3 centre, Color colour)
        => MatchResolved?.Invoke(combo, gemCount, points, centre, colour);

    public static void RaiseSpecialCreated(Item gem, Cell cell) => SpecialCreated?.Invoke(gem, cell);

    public static void RaiseSpecialFired(SpecialKind kind, Vector3 origin, Color colour, List<Cell> cells)
        => SpecialFired?.Invoke(kind, origin, colour, cells);

    public static void RaiseBonusScore(int points, Vector3 at) => BonusScore?.Invoke(points, at);

    public static void RaiseStageChanged(int stage, int colours, int stageStart, int nextStart)
        => StageChanged?.Invoke(stage, colours, stageStart, nextStart);

    public static void RaiseColorUnlocked(int colourIndex) => ColorUnlocked?.Invoke(colourIndex);

    public static void RaiseSwapDenied(Vector3 centre) => SwapDenied?.Invoke(centre);

    public static void RaiseStoneDamaged(Item stone, Cell cell) => StoneDamaged?.Invoke(stone, cell);

    public static void RaiseStoneBroken(Item stone, Cell cell) => StoneBroken?.Invoke(stone, cell);

    public static void RaiseGemSelected(Vector3 position) => GemSelected?.Invoke(position);

    public static void RaiseSaveRequested() => SaveRequested?.Invoke();

    public static void RaiseRestartRequested() => RestartRequested?.Invoke();

    public static void RaiseBestScoreChanged(int best) => BestScoreChanged?.Invoke(best);

    /// <summary>
    /// Static events keep their subscriber lists across Editor play sessions when
    /// "Enter Play Mode / Reload Domain" is disabled, silently leaking handlers
    /// from the previous run (and double-firing them). Clearing the bus before the
    /// first scene loads guarantees every play session starts with no subscribers.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad()
    {
        ScoreChanged = null;
        TileMerged = null;
        MatchResolved = null;
        SpecialCreated = null;
        SpecialFired = null;
        BonusScore = null;
        StageChanged = null;
        ColorUnlocked = null;
        SwapDenied = null;
        StoneDamaged = null;
        StoneBroken = null;
        GemSelected = null;
        SaveRequested = null;
        RestartRequested = null;
        BestScoreChanged = null;
    }
}

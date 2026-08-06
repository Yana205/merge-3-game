using System;
using UnityEngine;

/// <summary>
/// Global event bus for cross-system, "anyone can listen" signals — the Observer
/// pattern at the app level. Publishers raise; subscribers react; neither holds a
/// reference to the other, so systems stay decoupled (e.g. a merge can add score
/// without MergeManager ever knowing ScoreController exists).
///
/// Use this bus for GLOBAL signals (score changed, a tile merged, a save wanted).
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

    /// <summary>Raised after two tiles merge into a higher tier. Args: the new merged Item and its Cell.</summary>
    public static event Action<Item, Cell> TileMerged;

    /// <summary>Raised when a system wants persistent state written to disk.</summary>
    public static event Action SaveRequested;

    /// <summary>Raised when the player asks for a fresh run (the HUD's RESTART).</summary>
    public static event Action RestartRequested;

    /// <summary>Raised when the best recorded score changes. Arg: the new best.</summary>
    public static event Action<int> BestScoreChanged;

    /// <summary>Raised when pickaxe charges are earned/spent or the tool is armed
    /// or disarmed. Args: (charges in hand, armed).</summary>
    public static event Action<int, bool> PickaxeChanged;

    /// <summary>Raised when the board has jammed but the player still holds a
    /// charge — the run is not over, the pickaxe is the only legal move. Arg: true
    /// when entering that state, false when it clears.</summary>
    public static event Action<bool> JamRescuePending;

    /// <summary>Raised when a gem is shattered by the pickaxe. Args: the doomed
    /// Item and its Cell, read before either is torn down.</summary>
    public static event Action<Item, Cell> TileShattered;

    /// <summary>
    /// Raised after EVERY successful move — merge or slide alike. This is the
    /// corruption clock's tick: the whole point of the mechanic is that shuffling
    /// costs you time even though it costs you no board space, so a slide must
    /// reach CorruptionController exactly like a merge does.
    ///
    /// Distinct from <see cref="InputHandler.OnMoveCompleted"/>, which now fires
    /// only for merges because it drives the cyan spawn.
    /// </summary>
    public static event Action MoveCompleted;

    /// <summary>Raised whenever the corruption meter moves. Args: (current, max).</summary>
    public static event Action<int, int> CorruptionChanged;

    /// <summary>Raised when a red crystal reaches the top of its ladder and becomes
    /// a live bomb. Arg: the armed Item.</summary>
    public static event Action<Item> BombArmed;

    /// <summary>Raised after a bomb detonates. Args: the blast's centre Cell and how
    /// many items it cleared (the bomb itself included).</summary>
    public static event Action<Cell, int> BombDetonated;

    // ---- Raisers -----------------------------------------------------------
    // Publishers call these instead of touching the events directly, so the
    // "?.Invoke() everywhere" rule lives in exactly one place.

    public static void RaiseScoreChanged(int newTotal) => ScoreChanged?.Invoke(newTotal);

    public static void RaiseTileMerged(Item merged, Cell cell) => TileMerged?.Invoke(merged, cell);

    public static void RaiseSaveRequested() => SaveRequested?.Invoke();

    public static void RaiseRestartRequested() => RestartRequested?.Invoke();

    public static void RaiseBestScoreChanged(int best) => BestScoreChanged?.Invoke(best);

    public static void RaisePickaxeChanged(int charges, bool armed) => PickaxeChanged?.Invoke(charges, armed);

    public static void RaiseJamRescuePending(bool pending) => JamRescuePending?.Invoke(pending);

    public static void RaiseTileShattered(Item item, Cell cell) => TileShattered?.Invoke(item, cell);

    public static void RaiseMoveCompleted() => MoveCompleted?.Invoke();

    public static void RaiseCorruptionChanged(int current, int max) => CorruptionChanged?.Invoke(current, max);

    public static void RaiseBombArmed(Item bomb) => BombArmed?.Invoke(bomb);

    public static void RaiseBombDetonated(Cell centre, int cleared) => BombDetonated?.Invoke(centre, cleared);

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
        SaveRequested = null;
        RestartRequested = null;
        BestScoreChanged = null;
        PickaxeChanged = null;
        JamRescuePending = null;
        TileShattered = null;
        MoveCompleted = null;
        CorruptionChanged = null;
        BombArmed = null;
        BombDetonated = null;
    }
}

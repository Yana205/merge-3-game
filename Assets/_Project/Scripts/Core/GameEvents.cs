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

    // ---- Raisers -----------------------------------------------------------
    // Publishers call these instead of touching the events directly, so the
    // "?.Invoke() everywhere" rule lives in exactly one place.

    public static void RaiseScoreChanged(int newTotal) => ScoreChanged?.Invoke(newTotal);

    public static void RaiseTileMerged(Item merged, Cell cell) => TileMerged?.Invoke(merged, cell);

    public static void RaiseSaveRequested() => SaveRequested?.Invoke();

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
    }
}

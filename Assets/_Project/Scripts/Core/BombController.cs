using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Turns the top of the red ladder from a punishment into a payoff.
///
/// Before this existed, a red that reached <see cref="Item.MaxTierFor"/> could never
/// merge again (MergeManager rejects it) and could never be moved off the board — a
/// permanently dead cell the player was handed by a dice roll. Red was variance, not
/// a decision.
///
/// Now a red that reaches <see cref="ArmTier"/> ARMS instead. It sits on the board
/// until the player taps it, then clears its whole 3x3 neighbourhood — their own cyan
/// crystals included. That single change makes the red chain something a player can
/// choose to grow: you pick where to build it knowing what you will have to sacrifice,
/// and you pick when to spend it.
///
/// Owns the board half of the mechanic only. Everything else hangs off
/// <see cref="GameEvents.BombDetonated"/>.
/// </summary>
public class BombController : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    [SerializeField] private GridManager gridManager;

    [Tooltip("Blast reach in cells. 1 means the bomb's own cell plus its 8 " +
             "neighbours — a 3x3.")]
    [Min(1)]
    [SerializeField] private int blastRadius = 1;

    [Tooltip("The red tier that arms as a bomb.\n\n" +
             "Stated outright rather than derived from the top of the red ladder, " +
             "because the cost is exponential in the FUSE rule, not in the ladder " +
             "length: three crystals per rung means tier 3 is nine tier-1 reds. Tier " +
             "4 would be twenty-seven — more than a 36-cell board can stage, so the " +
             "bomb would be a mechanic the player only ever reads about.\n\n" +
             "An armed bomb refuses to fuse (see MergeManager and " +
             "GridManager.GetConnectedGroup), so arming below the top of the ladder " +
             "does not leave a rung dangling — it moves the ceiling down.")]
    [Min(1)]
    [SerializeField] private int armTier = 3;

    [Header("Glow")]
    [Tooltip("Pulses per second of the armed-bomb glow. Fast enough to read as " +
             "'act on me', slow enough not to strobe.")]
    [Min(0.1f)]
    [SerializeField] private float pulseSpeed = 2.6f;

    // Live bombs, so the jam check and the glow never walk all 36 cells. Entries go
    // stale when an item is pooled (shattered, caught in another blast, new run) —
    // Prune drops anything whose IsArmedBomb flag no longer agrees, which
    // Item.ResetForPool guarantees to clear.
    private readonly List<Item> _armed = new List<Item>();

    void OnEnable()
    {
        GameEvents.TileMerged += HandleTileMerged;
    }

    void OnDisable()
    {
        GameEvents.TileMerged -= HandleTileMerged;
    }

    void Update()
    {
        if (_armed.Count == 0) return;

        Prune();

        // 0..1 triangle rather than a raw sine, so the glow spends real time at both
        // ends instead of rushing through the bright frame the player needs to see.
        float t = Mathf.PingPong(Time.time * pulseSpeed, 1f);
        foreach (Item bomb in _armed)
            bomb.ApplyBombPulse(t);
    }

    /// <summary>
    /// The red tier that becomes a bomb. Returns 0 when the red ladder is unwired,
    /// which disables the whole mechanic rather than arming every red on sight.
    ///
    /// Clamped to the top of the ladder so a configured tier the art does not reach
    /// still arms something, instead of quietly never arming at all.
    /// </summary>
    public int ArmTier
    {
        get
        {
            int max = Item.MaxTierFor(GemFamily.Red);
            if (max <= 0) return 0;
            return Mathf.Clamp(armTier, 1, max);
        }
    }

    /// <summary>
    /// True while at least one live bomb sits on the board. The jam check consults
    /// this: an armed bomb IS a legal move, and ending the run with one sitting
    /// there would be the same bug the pickaxe rescue was written to avoid.
    /// </summary>
    public bool HasArmedBomb()
    {
        Prune();
        return _armed.Count > 0;
    }

    /// <summary>Forget every tracked bomb. Called when a run starts, because the old
    /// board's Items are pooled without passing through this class.</summary>
    public void ResetRun()
    {
        foreach (Item bomb in _armed)
            if (bomb != null) bomb.SetArmedBomb(false);

        _armed.Clear();
    }

    // A merge landed. If it completed the red ladder, that gem is now a bomb.
    // Reds are the only family that arms: a maxed standard gem is the top of the
    // player's own ladder and a reward, not a hazard to be cashed in.
    private void HandleTileMerged(Item merged, Cell cell)
    {
        if (merged == null) return;
        if (merged.Family != GemFamily.Red) return;

        int armAt = ArmTier;
        // A family with no configured ladder reports 0; without this guard every
        // red would arm the instant it spawned.
        if (armAt <= 0 || merged.Tier < armAt) return;

        merged.SetArmedBomb(true);
        if (!_armed.Contains(merged))
            _armed.Add(merged);

        GameEvents.RaiseBombArmed(merged);
    }

    /// <summary>
    /// Blow the bomb. Clears every item within <see cref="blastRadius"/> cells,
    /// including the bomb itself and including the player's own cyan crystals, and
    /// returns how many it cleared. Returns 0 for anything that is not a live bomb,
    /// so a stray tap costs nothing.
    /// </summary>
    public int Detonate(Item bomb)
    {
        if (bomb == null || gridManager == null) return 0;
        if (!bomb.IsArmedBomb) return 0;

        Cell centre = gridManager.FindCellWithItem(bomb);
        if (centre == null) return 0;

        // Collect first, tear down second. Mutating cells while walking the
        // neighbourhood would be reading the board mid-edit.
        var doomed = new List<(Item item, Cell cell)>();

        for (int dr = -blastRadius; dr <= blastRadius; dr++)
            for (int dc = -blastRadius; dc <= blastRadius; dc++)
            {
                Cell cell = gridManager.GetCell(centre.row + dr, centre.col + dc);
                if (cell == null || !cell.IsOccupied()) continue;
                doomed.Add((cell.CurrentItem, cell));
            }

        // Announce every victim BEFORE any despawn. DespawnItem hands the Item back
        // to ItemFactory, whose ResetForPool wipes Tier, Family and GemData — the
        // same trap documented in MergeManager.TryMerge. Listeners that place a
        // burst or pick a sound need those fields intact, and reading them after
        // the loop below would give them a reset gem every time.
        foreach ((Item item, Cell cell) in doomed)
            GameEvents.RaiseTileShattered(item, cell);

        foreach ((Item item, Cell cell) in doomed)
        {
            // Disarm on the way out. ResetForPool clears the flag too, but a second
            // bomb caught in this blast would otherwise sit armed and cell-less for
            // the rest of the loop.
            item.SetArmedBomb(false);
            cell.RemoveItem();
            gridManager.DespawnItem(item);
        }

        Prune();
        GameEvents.RaiseBombDetonated(centre, doomed.Count);
        return doomed.Count;
    }

    // Drop entries that are gone or no longer armed. Cheap: the list holds at most
    // a handful, and Item.ResetForPool clearing IsArmedBomb is what makes a pooled
    // gem fall out of it automatically.
    private void Prune()
    {
        for (int i = _armed.Count - 1; i >= 0; i--)
            if (_armed[i] == null || !_armed[i].IsArmedBomb)
                _armed.RemoveAt(i);
    }
}

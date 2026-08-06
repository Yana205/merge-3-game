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
/// Now a maxed red ARMS instead. It sits on the board until the player taps it, then
/// clears its whole 3x3 neighbourhood — their own cyan crystals included — and purges
/// the corruption meter. That single change makes the red chain something a player
/// can choose to grow: you pick where to build it knowing what you will have to
/// sacrifice, and you pick when to spend it.
///
/// Owns the board half of the mechanic only. It does not touch corruption — it
/// announces <see cref="GameEvents.BombDetonated"/> and LevelManager, which holds
/// both references, does the purge.
/// </summary>
public class BombController : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    [SerializeField] private GridManager gridManager;

    [Tooltip("Blast reach in cells. 1 means the bomb's own cell plus its 8 " +
             "neighbours — a 3x3.")]
    [Min(1)]
    [SerializeField] private int blastRadius = 1;

    [Tooltip("How far below the top of the red ladder a bomb arms.\n\n" +
             "This is exponential, so it matters more than it looks: the red ladder " +
             "tops out at tier 5, and every rung DOUBLES the reds needed. Arming at " +
             "the very top (0) costs 16 tier-1 reds — more than most runs will ever " +
             "see, so the bomb would be a mechanic the player only reads about. One " +
             "rung down costs 8, which a run can realistically reach.\n\n" +
             "An armed bomb refuses to merge (see MergeManager), so arming below the " +
             "top does not leave a rung dangling — it moves the ceiling down.")]
    [Min(0)]
    [SerializeField] private int armTiersBelowMax = 1;

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
    /// </summary>
    public int ArmTier
    {
        get
        {
            int max = Item.MaxTierFor(GemFamily.Red);
            if (max <= 0) return 0;
            return Mathf.Max(1, max - armTiersBelowMax);
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

using UnityEngine;

/// <summary>
/// The run's clock. Every move winds it forward; every merge winds it back by the
/// tier the player just made. When it fills, a red crystal erupts.
///
/// It exists because the old model spawned a gem after every move, which made a
/// merge net-zero on tiles (the board could never drain) and a slide strictly
/// negative (repositioning was punished, so nobody repositioned). Moving the
/// pressure off the tile count and onto a meter frees the slide to be a real move
/// again while keeping the run finite — the board still fills, but only from reds
/// the player's own pace summoned.
///
/// One rule carries the whole mechanic: THE TIER YOU MAKE IS THE CORRUPTION YOU
/// CLEAR. Basic merges tread water, big merges buy relief, shuffling costs time.
///
/// A pure Observer of the <see cref="GameEvents"/> bus, like ScoreController and
/// PickaxeController: it watches, it counts, it announces. It holds no reference to
/// the grid, the HUD, or the input layer. Deciding WHERE a red erupts is a board
/// question and belongs to LevelManager, which is why this class announces a full
/// meter and waits to be told the eruption happened rather than resetting itself.
/// </summary>
public class CorruptionController : MonoBehaviour
{
    [Header("The meter")]
    [Tooltip("Corruption needed to erupt a red crystal. With the default rate this " +
             "is about ten idle moves.")]
    [Min(1)]
    [SerializeField] private int threshold = 20;

    [Header("Rate")]
    [Tooltip("Corruption added by every successful move, merge or slide alike.\n\n" +
             "MUST stay above the smallest possible drain. The cheapest merge makes " +
             "a tier 2 and so clears 2; at a rate of 2 that merge would be exactly " +
             "net zero, and a player who only ever merged would hold the clock still " +
             "forever and never see a single red. At 3, basic merging loses one " +
             "corruption a move — slowly — and only climbing actually buys ground.")]
    [Min(1)]
    [SerializeField] private int basePerMove = 3;

    [Tooltip("Scores at which the clock speeds up by one more corruption per move. " +
             "This is the run's only escalation now — the fodder rate no longer " +
             "ramps, so difficulty arrives as pressure rather than as clutter.")]
    [SerializeField] private int[] _rateThresholds = { 600, 2000 };

    [Tooltip("Corruption cleared per tier of a completed merge. At 1, making a " +
             "tier-2 gem clears 2 — exactly the base rate — so ordinary merging " +
             "treads water and only climbing buys real relief.")]
    [Min(0)]
    [SerializeField] private int drainPerMergedTier = 1;

    /// <summary>Corruption on the meter right now, in [0, Threshold].</summary>
    public int Corruption { get; private set; }

    public int Threshold => threshold;

    /// <summary>True while the meter is full and an eruption is owed. It stays true
    /// until <see cref="ConsumeEruption"/> is called, so a full board that cannot
    /// take a red holds the meter at the top instead of silently discarding it.</summary>
    public bool EruptionPending => Corruption >= threshold;

    // The running score, tracked only to pick the current rate. Held rather than
    // asked for, so this class needs no reference to ScoreController.
    private int _score;

    void OnEnable()
    {
        GameEvents.MoveCompleted += HandleMoveCompleted;
        GameEvents.TileMerged += HandleTileMerged;
        GameEvents.ScoreChanged += HandleScoreChanged;
    }

    void OnDisable()
    {
        GameEvents.MoveCompleted -= HandleMoveCompleted;
        GameEvents.TileMerged -= HandleTileMerged;
        GameEvents.ScoreChanged -= HandleScoreChanged;
    }

    /// <summary>
    /// Wipe the meter for a fresh run. Called by LevelManager after the score reset,
    /// for the same reason PickaxeController.ResetRun is — this class watches
    /// ScoreChanged, and the reset-to-zero event must not land on a meter that has
    /// already been cleared for the new run.
    /// </summary>
    public void ResetRun()
    {
        Corruption = 0;
        _score = 0;
        Announce();
    }

    /// <summary>Corruption added per move at this score. Public so tests can pin the
    /// ramp without driving the whole bus.</summary>
    public int PerMoveAt(int score)
    {
        int extra = 0;
        if (_rateThresholds != null)
            foreach (int t in _rateThresholds)
                if (score >= t) extra++;

        return basePerMove + extra;
    }

    /// <summary>
    /// Clear the meter after a red has actually landed on the board. LevelManager
    /// calls this only on a successful eruption; if the board was full there was
    /// nowhere to put the red, and the meter deliberately stays pinned at the top.
    /// </summary>
    public void ConsumeEruption()
    {
        if (Corruption < threshold) return;
        Corruption = 0;
        Announce();
    }

    /// <summary>
    /// Empty the meter outright. The bomb's payoff: detonating purges corruption
    /// completely, which is what makes deliberately growing a red chain a strategy
    /// rather than damage control.
    /// </summary>
    public void Purge()
    {
        if (Corruption == 0) return;
        Corruption = 0;
        Announce();
    }

    // Order is load-bearing and guaranteed by the caller: MergeManager raises
    // TileMerged inside TryMerge, and InputHandler raises MoveCompleted only after
    // TryMerge returns. So a merge always DRAINS before it TICKS — the player gets
    // credit for the gem they just made before the clock charges them for the move.
    // Reversed, a merge at 19/20 would erupt a red and only then be paid for it.
    private void HandleTileMerged(Item merged, Cell cell)
    {
        if (merged == null) return;
        Add(-merged.Tier * drainPerMergedTier);
    }

    private void HandleMoveCompleted()
    {
        Add(PerMoveAt(_score));
    }

    private void HandleScoreChanged(int total)
    {
        _score = total;
    }

    private void Add(int delta)
    {
        int next = Mathf.Clamp(Corruption + delta, 0, threshold);
        if (next == Corruption) return;

        Corruption = next;
        Announce();
    }

    private void Announce()
    {
        GameEvents.RaiseCorruptionChanged(Corruption, threshold);
    }
}

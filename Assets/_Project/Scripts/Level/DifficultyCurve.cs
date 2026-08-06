using UnityEngine;

/// <summary>
/// What CYAN fodder looks like at a given score, expressed as a pure function of the
/// running score. No Unity lifecycle, no state — feed it a score and it tells you
/// what tier the next crystal should be.
///
/// Held as a serialized field on <see cref="LevelManager"/> rather than as a
/// ScriptableObject, so the curve is tunable in the inspector with no asset to
/// create, wire, or keep in sync.
///
/// THIS CLASS NO LONGER OWNS DIFFICULTY. It used to: it decided how many gems
/// arrived per move and whether each was red. Both are gone.
///
///  - Spawn COUNT is gone because it was the bug. The board is 6x6 = 36 cells, a
///    merge is net -1 tile and a spawn is +1, so one spawn per move made a merge
///    net-zero (the board could never drain) and made a slide net-positive
///    (repositioning was strictly punished). Cyan is now exactly one crystal per
///    MERGE, and slides are free.
///  - Family rolling is gone because a red that arrives on a dice roll is variance,
///    not a decision. Reds now come only from <see cref="CorruptionController"/> —
///    a clock the player winds themselves.
///
/// What is left is the tier of the next cyan gem, which is genuinely a difficulty
/// question and is still tuned here.
/// </summary>
[System.Serializable]
public class DifficultyCurve
{
    [Header("Spawn Tiers")]
    [Tooltip("Scores at which one more spawn tier unlocks. Starts at tier 1 only; " +
             "each threshold passed raises the ceiling by one.")]
    [SerializeField] private int[] _tierUnlockThresholds = { 200, 700, 1600 };

    [Tooltip("How much less likely each tier is than the one below it. 0.45 means " +
             "tier 2 spawns 45% as often as tier 1, tier 3 45% as often as tier 2. " +
             "Keeps low-tier fodder dominant so higher tiers read as a threat, not a wall.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float _tierFalloff = 0.45f;

    [Tooltip("Never spawn within this many tiers of the top of the ladder — a gem " +
             "spawned near the top is nearly-free points rather than pressure.")]
    [Min(1)]
    [SerializeField] private int _headroomBelowMaxTier = 3;

    /// <summary>Highest tier that may spawn at this score, clamped clear of the ladder top.</summary>
    public int TierCeilingAt(int score)
    {
        int unlocked = 1 + CountPassed(_tierUnlockThresholds, score);
        int cap = Mathf.Max(1, Item.MaxTier - _headroomBelowMaxTier);
        return Mathf.Min(unlocked, cap);
    }

    /// <summary>
    /// Roll one cyan spawn's tier for this score. Reds never come through here —
    /// they always enter at tier 1, because a red spawned mid-ladder needs two more
    /// reds of that exact tier to ever leave the board, which the player cannot
    /// influence. Entering at the bottom keeps every red climbable.
    /// </summary>
    public int PickTierAt(int score)
    {
        int ceiling = TierCeilingAt(score);
        if (ceiling <= 1) return 1;

        // Geometric weights: tier 1 = 1, tier 2 = falloff, tier 3 = falloff^2 ...
        // A linear ramp (ceiling, ceiling-1, ... 1) would make the top tier ~10% of
        // all spawns at ceiling 4 — enough unmergeable clutter to choke the board.
        float total = 0f;
        float weight = 1f;
        for (int tier = 1; tier <= ceiling; tier++)
        {
            total += weight;
            weight *= _tierFalloff;
        }

        float roll = Random.Range(0f, total);
        float accumulated = 0f;
        weight = 1f;
        for (int tier = 1; tier <= ceiling; tier++)
        {
            accumulated += weight;
            if (roll < accumulated) return tier;
            weight *= _tierFalloff;
        }

        return 1;
    }

    // How many thresholds this score has reached. Counts rather than scans in
    // order, so an unsorted array in the inspector still gives the right answer.
    // A null or empty array yields 0, degrading to the easiest setting instead of
    // throwing on a half-configured component.
    static int CountPassed(int[] thresholds, int score)
    {
        if (thresholds == null) return 0;

        int passed = 0;
        foreach (int threshold in thresholds)
            if (score >= threshold) passed++;

        return passed;
    }
}

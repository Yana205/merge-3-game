using UnityEngine;

/// <summary>
/// The entire difficulty model of the endless run, expressed as a pure function of
/// the running score. No Unity lifecycle, no state — feed it a score and it tells
/// you how many gems to spawn this move and what tier each one should be.
///
/// Held as a serialized field on <see cref="LevelManager"/> rather than as a
/// ScriptableObject, so the curve is tunable in the inspector with no asset to
/// create, wire, or keep in sync.
///
/// Why spawn count is the master lever: the board is 6x6 = 36 cells, a merge is
/// net -1 tile and a spawn is +1. So 1 spawn per move is equilibrium, 2 costs you
/// a cell per merge, and 3 is a death spiral. Everything else is seasoning.
/// </summary>
[System.Serializable]
public class DifficultyCurve
{
    [Header("Spawn Pressure")]
    [Tooltip("Scores at which one more gem per move is added. Starts at 1 gem; " +
             "each threshold passed adds another. Order does not matter.")]
    [SerializeField] private int[] _spawnCountThresholds = { 350, 1200 };

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

    [Header("Red Chain")]
    [Tooltip("Score at which red gems start appearing at all. Below this the board " +
             "is pure standard gems, which keeps the opening minutes teachable.")]
    [Min(0)]
    [SerializeField] private int _redUnlockScore = 400;

    [Tooltip("Share of spawns that are red once unlocked. Reds only merge with " +
             "reds and their ladder dead-ends, so every red is a cell the player " +
             "may never get back — keep this low.")]
    [Range(0f, 1f)]
    [SerializeField] private float _redSpawnChance = 0.12f;

    /// <summary>Gems to spawn after a successful move at this score. Always >= 1.</summary>
    public int SpawnCountAt(int score)
    {
        return 1 + CountPassed(_spawnCountThresholds, score);
    }

    /// <summary>Highest tier that may spawn at this score, clamped clear of the ladder top.</summary>
    public int TierCeilingAt(int score)
    {
        int unlocked = 1 + CountPassed(_tierUnlockThresholds, score);
        int cap = Mathf.Max(1, Item.MaxTier - _headroomBelowMaxTier);
        return Mathf.Min(unlocked, cap);
    }

    /// <summary>
    /// Roll one spawn tier for this score. Call once per gem — three gems spawning
    /// on the same move each get their own roll, not one shared result.
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

    /// <summary>
    /// Roll one spawn's family. Call once per gem, alongside <see cref="PickTierAt"/>.
    /// Returns Standard until the red unlock score, and Standard always if the red
    /// ladder is unwired — so a half-configured GemConfig degrades to the original
    /// single-chain game rather than spawning gems with no art.
    /// </summary>
    public GemFamily PickFamilyAt(int score)
    {
        if (score < _redUnlockScore) return GemFamily.Standard;
        if (Item.MaxTierFor(GemFamily.Red) <= 0) return GemFamily.Standard;

        return Random.value < _redSpawnChance ? GemFamily.Red : GemFamily.Standard;
    }

    /// <summary>
    /// Tier for a freshly spawned gem of <paramref name="family"/>. Reds always
    /// enter at tier 1: a red spawned mid-ladder needs two more reds of that exact
    /// tier to ever leave the board, which the player cannot influence. Entering at
    /// the bottom keeps every red climbable, so a jam is the player's doing.
    /// </summary>
    public int PickTierAt(int score, GemFamily family)
    {
        return family == GemFamily.Red ? 1 : PickTierAt(score);
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

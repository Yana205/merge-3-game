using UnityEngine;

/// <summary>
/// What the queue hands the player next, as a pure function of the running score.
/// No Unity lifecycle, no state — feed it a score and it rolls one crystal.
///
/// This is now the ENTIRE difficulty model, and it is an honest one: the only thing
/// that gets harder is how often a red turns up, and the player sees every red three
/// moves before they have to deal with it. Nothing is hidden and nothing arrives
/// unannounced.
///
/// Everything else this class used to own is gone. Spawn count is meaningless (the
/// player places exactly one crystal per turn, by hand). The tier ladder no longer
/// needs a falloff curve, because fusion — not spawning — is how high tiers appear.
/// </summary>
[System.Serializable]
public class DifficultyCurve
{
    [Header("Red")]
    [Tooltip("Score below which the queue is pure cyan. Keeps the opening minutes " +
             "teachable: learn to fuse before learning to bury junk.")]
    [Min(0)]
    [SerializeField] private int _redUnlockScore = 250;

    [Tooltip("Score at which the red rate reaches its ceiling. Between the unlock " +
             "and here, red odds ramp smoothly rather than switching on.")]
    [Min(1)]
    [SerializeField] private int _redRampScore = 3000;

    [Tooltip("Most of the queue that is ever red. Nine reds make a bomb, so this " +
             "also sets how often a bomb is realistically achievable — but every " +
             "red the player does NOT commit to a cluster is a cell lost for good.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float _maxRedChance = 0.22f;

    [Header("Cyan")]
    [Tooltip("Chance a cyan crystal arrives at tier 2 instead of tier 1, at or " +
             "above the ramp score. A tier 2 is worth three tier 1s, so this is a " +
             "gift — keep it small or the board climbs without the player earning it.")]
    [Range(0f, 0.35f)]
    [SerializeField] private float _maxTier2Chance = 0.12f;

    [Tooltip("Score at which the tier-2 chance reaches its ceiling.")]
    [Min(1)]
    [SerializeField] private int _tier2RampScore = 2000;

    /// <summary>
    /// Roll one queue entry. Call once per crystal — three crystals in the preview
    /// each get their own roll, not one shared result.
    /// </summary>
    public CrystalSpec Roll(int score)
    {
        if (Random.value < RedChanceAt(score))
        {
            // Reds always enter at tier 1. A red arriving mid-ladder would need the
            // player to find matching reds they cannot influence; entering at the
            // bottom keeps every red cluster something they can actually finish.
            return new CrystalSpec(GemFamily.Red, 1);
        }

        int tier = Random.value < Tier2ChanceAt(score) ? 2 : 1;

        // Guard against a GemConfig whose cyan ladder is a single tier.
        int max = Item.MaxTierFor(GemFamily.Standard);
        if (max > 0) tier = Mathf.Min(tier, max);

        return new CrystalSpec(GemFamily.Standard, tier);
    }

    /// <summary>Share of queue entries that are red at this score. Public so the
    /// balance can be checked without rolling thousands of crystals.</summary>
    public float RedChanceAt(int score)
    {
        // A family with no configured ladder disables reds entirely, so a
        // half-configured GemConfig degrades to the pure-cyan game rather than
        // queueing crystals with no art.
        if (Item.MaxTierFor(GemFamily.Red) <= 0) return 0f;
        if (score < _redUnlockScore) return 0f;
        if (_redRampScore <= _redUnlockScore) return _maxRedChance;

        float t = Mathf.InverseLerp(_redUnlockScore, _redRampScore, score);
        return Mathf.Lerp(0f, _maxRedChance, t);
    }

    /// <summary>Share of cyan entries that arrive at tier 2 at this score.</summary>
    public float Tier2ChanceAt(int score)
    {
        float t = Mathf.InverseLerp(0f, _tier2RampScore, score);
        return Mathf.Lerp(0f, _maxTier2Chance, t);
    }
}

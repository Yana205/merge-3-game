using UnityEngine;

[CreateAssetMenu(fileName = "GemConfig", menuName = "Merge Game/Gem Config")]
public class GemConfig : ScriptableObject
{
    [Tooltip("Index 0 = Tier 1 (Obsidian). Array length sets the standard MaxTier.")]
    public GemTierData[] tiers;

    [Tooltip("The red chain: index 0 = red Tier 1. A separate ladder — reds merge " +
             "only with reds. Shorter than the standard ladder on purpose, so a " +
             "maxed red is a permanently occupied cell. Leave empty to disable red " +
             "entirely; the game then behaves as it did before the red chain existed.")]
    public GemTierData[] redTiers;

    public int MaxTier => tiers != null ? tiers.Length : 0;

    /// <summary>
    /// Top of the ladder for one family. Returns 0 for a family with no tiers
    /// configured, which is the signal every caller uses to skip that family — an
    /// unwired redTiers array disables red rather than throwing.
    /// </summary>
    public int MaxTierFor(GemFamily family)
    {
        GemTierData[] ladder = LadderFor(family);
        return ladder != null ? ladder.Length : 0;
    }

    public GemTierData GetTier(int tier)
    {
        return GetTier(tier, GemFamily.Standard);
    }

    public GemTierData GetTier(int tier, GemFamily family)
    {
        GemTierData[] ladder = LadderFor(family);

        // An unwired ladder falls back to the standard one rather than returning
        // null: a red gem with no red data spawns looking like a standard gem —
        // wrong, but playable. Returning null would NRE in Item.Setup.
        if (ladder == null || ladder.Length == 0)
        {
            if (family == GemFamily.Standard) return null;
            return GetTier(tier, GemFamily.Standard);
        }

        int index = Mathf.Clamp(tier - 1, 0, ladder.Length - 1);
        return ladder[index];
    }

    private GemTierData[] LadderFor(GemFamily family)
    {
        switch (family)
        {
            case GemFamily.Red: return redTiers;
            default:            return tiers;
        }
    }
}

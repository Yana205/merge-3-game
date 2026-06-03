using UnityEngine;

[CreateAssetMenu(fileName = "GemConfig", menuName = "Merge Game/Gem Config")]
public class GemConfig : ScriptableObject
{
    [Tooltip("Index 0 = Tier 1 (Obsidian). Array length sets MaxTier.")]
    public GemTierData[] tiers;

    public int MaxTier => tiers != null ? tiers.Length : 0;

    public GemTierData GetTier(int tier)
    {
        int index = Mathf.Clamp(tier - 1, 0, tiers.Length - 1);
        return tiers[index];
    }
}

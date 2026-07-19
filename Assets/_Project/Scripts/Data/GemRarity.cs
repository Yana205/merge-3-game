/// <summary>
/// Rarity band for a gem. Drives weighted spawn odds — the numeric weight per band
/// lives in data (GemDatabase), not here, so designers retune odds without code.
/// </summary>
public enum GemRarity
{
    Common,
    Uncommon,
    Rare,
    Legendary
}

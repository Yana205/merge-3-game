using UnityEngine;

[System.Serializable]
public class GemTierData
{
    [Header("Identity")]
    public string gemName;
    public float hardness;

    [Header("Visuals")]
    public Color tintColor;
    public Sprite sprite;

    [Tooltip("Alternate looks for this exact tier — e.g. the gold-tear crystals. " +
             "Purely cosmetic: a gem wearing a variant merges and scores exactly " +
             "like the plain one, so no rule ever reads this.")]
    public Sprite[] variantSprites;

    [Tooltip("Odds a spawned gem of this tier wears one of variantSprites instead " +
             "of the plain sprite. 0 = never, 1 = always. Ignored when " +
             "variantSprites is empty.")]
    [Range(0f, 1f)]
    public float variantChance;

    [Header("Scoring")]
    public int scoreValue;

    /// <summary>
    /// Rolls this tier's look for one spawn. Returns a variant only when the tier
    /// actually has variants configured AND the roll lands, so a tier with an empty
    /// variantSprites array behaves exactly as it did before variants existed.
    /// </summary>
    public Sprite PickSprite()
    {
        if (variantSprites == null || variantSprites.Length == 0) return sprite;
        if (Random.value >= variantChance) return sprite;

        // A null entry (an unassigned inspector slot) would blank the gem, which
        // reads as a bug rather than a variant — fall back to the plain sprite.
        Sprite picked = variantSprites[Random.Range(0, variantSprites.Length)];
        return picked != null ? picked : sprite;
    }
}

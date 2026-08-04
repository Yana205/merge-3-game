using UnityEngine;

/// <summary>
/// Built-in 7-step gem tier ladder ("the merge cell table"), used when no
/// GemConfig asset is assigned on the Item prefab. Each tier gets a distinct
/// color and name so the merge progression is readable in-game with no art.
///
/// Kept in lockstep with the GemConfig ladder: 7 entries, one per shape stage
/// in the gem-chain-alpha art set, using that set's per-tier stone colours.
/// Item.MaxTier falls back to Count here, so this length IS the max mergeable
/// level whenever the config is missing — the two must not drift apart.
///
/// Every merge still jumps far around the colour wheel (slate -> red -> green
/// -> blue -> gold -> magenta -> white), so a fresh merge is always an obvious
/// colour change rather than a subtle shade shift.
///
/// To use real gem sprites: create a GemConfig asset, fill its tiers with the
/// gem sprites, and assign it to the Item prefab. GemConfig then overrides this
/// table automatically.
/// </summary>
public static class GemTierTable
{
    // Ordered tier 1 -> 7. Names are flavor; the item label shows the level number.
    static readonly string[] Names =
    {
        "Obsidian", "Ruby", "Emerald", "Sapphire",
        "Citrine", "Rhodochrosite", "Diamond",
    };

    // Maximally-separated hues, matching the stone colour of each tier's sprite.
    static readonly Color[] Colors =
    {
        Hex(0x949FB3), Hex(0xD9455A), Hex(0x62E691), Hex(0x58ACE6),
        Hex(0xE6B84E), Hex(0xE660BF), Hex(0xDCEAF0),
    };

    /// <summary>Number of tiers in the ladder (also the max mergeable level).</summary>
    public static int Count => Colors.Length;

    public static Color ColorFor(int tier)
    {
        int i = Mathf.Clamp(tier - 1, 0, Colors.Length - 1);
        return Colors[i];
    }

    public static string NameFor(int tier)
    {
        int i = Mathf.Clamp(tier - 1, 0, Names.Length - 1);
        return Names[i];
    }

    /// <summary>Black or white label, whichever stays readable on the tier color.</summary>
    public static Color LabelColorFor(int tier)
    {
        Color c = ColorFor(tier);
        float luminance = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
        return luminance > 0.6f ? Color.black : Color.white;
    }

    static Color Hex(int rgb)
    {
        return new Color(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f,
            1f);
    }
}

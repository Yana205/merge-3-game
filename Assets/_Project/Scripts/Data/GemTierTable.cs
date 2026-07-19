using UnityEngine;

/// <summary>
/// Built-in 10-step gem tier ladder ("the merge cell table"), used when no
/// GemConfig asset is assigned on the Item prefab. Each tier gets a distinct
/// color and name so the merge progression is readable in-game with no art.
///
/// Trimmed from 20 near-duplicate hues to 10 maximally-separated ones: every
/// merge jumps far around the colour wheel (dark -> red -> green -> blue ->
/// yellow -> purple -> cyan -> orange -> pink -> white), so a fresh merge is
/// always an obvious colour change rather than a subtle shade shift.
///
/// To use real gem sprites later: create a GemConfig asset, fill its tiers with
/// the sliced gem sprites, and assign it to the Item prefab. GemConfig then
/// overrides this table automatically.
/// </summary>
public static class GemTierTable
{
    // Ordered tier 1 -> 10. Names are flavor; the item label shows the level number.
    static readonly string[] Names =
    {
        "Obsidian", "Ruby", "Emerald", "Sapphire", "Citrine",
        "Amethyst", "Turquoise", "Carnelian", "Rhodochrosite", "Diamond",
    };

    // Maximally-separated hues so every consecutive tier is an obvious jump.
    static readonly Color[] Colors =
    {
        Hex(0x2B2B33), Hex(0xE7263C), Hex(0x16C25A), Hex(0x2E6BFF), Hex(0xFFC531),
        Hex(0x9B3CE0), Hex(0x17D9D0), Hex(0xFF6A1A), Hex(0xFF3DAE), Hex(0xEAF6FF),
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

using UnityEngine;

/// <summary>
/// Built-in 20-step gem tier ladder ("the merge cell table"), used when no
/// GemConfig asset is assigned on the Item prefab. Each tier gets a distinct
/// color and name so the merge progression is readable in-game with no art.
///
/// To use real gem sprites later: create a GemConfig asset, fill its tiers with
/// the sliced gem sprites, and assign it to the Item prefab. GemConfig then
/// overrides this table automatically.
/// </summary>
public static class GemTierTable
{
    // Ordered tier 1 -> 20. Names are flavor; the item label shows the level number.
    static readonly string[] Names =
    {
        "Obsidian", "Hematite", "Garnet", "Carnelian", "Amber",
        "Citrine", "Peridot", "Emerald", "Malachite", "Aquamarine",
        "Turquoise", "Sapphire", "Lapis", "Amethyst", "Sugilite",
        "Rhodochrosite", "Rose Quartz", "Ruby", "Diamond", "Star Gem",
    };

    // Distinct, well-separated colors so adjacent levels never look alike.
    static readonly Color[] Colors =
    {
        Hex(0x2C3E50), Hex(0x7F8C8D), Hex(0xC0392B), Hex(0xE67E22), Hex(0xF5B041),
        Hex(0xF7DC6F), Hex(0xA9DFBF), Hex(0x229954), Hex(0x17A589), Hex(0x48C9B0),
        Hex(0x5DADE2), Hex(0x2E86C1), Hex(0x1F3A93), Hex(0x8E44AD), Hex(0x6C3483),
        Hex(0xE84393), Hex(0xF5B7CE), Hex(0xE74C3C), Hex(0xFDFEFE), Hex(0xFFD700),
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

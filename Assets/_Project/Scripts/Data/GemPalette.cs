using UnityEngine;

/// <summary>
/// The match-3 colour identity table. The board engine deals in colour indices
/// (1..6); this maps each index to the Standard-ladder sprite whose core actually
/// shows that colour, plus a vivid identity colour used everywhere the gem needs
/// to read at a glance — the glow backing behind the sprite, spark bursts, and
/// floating score text.
///
/// Exists because the crystal sprites share one dark diamond frame and differ only
/// in a small coloured core, so at board size they blur together. The palette
/// carries the identity the art can't: tier 1 (obsidian, near-black) is skipped
/// entirely and the remaining six get a saturated signature colour each.
/// </summary>
public static class GemPalette
{
    /// <summary>How many distinct colours the palette defines.</summary>
    public const int Count = 6;

    // colour index (1-based) -> Standard-ladder tier whose sprite core matches.
    // 1 obsidian is skipped (a black gem reads as a hole in the board).
    private static readonly int[] SpriteTiers = { 2, 3, 4, 5, 6, 7 };
    //                                            ruby  emerald  sapphire  citrine  rhodo  diamond

    // The vivid signature colour per index — deliberately louder than the sprite
    // cores so glows and particles separate cleanly even on a small screen.
    private static readonly Color[] Colors =
    {
        new Color(1.00f, 0.28f, 0.31f),   // 1 ruby      — red
        new Color(0.22f, 0.90f, 0.42f),   // 2 emerald   — green
        new Color(0.26f, 0.62f, 1.00f),   // 3 sapphire  — blue
        new Color(1.00f, 0.84f, 0.25f),   // 4 citrine   — gold
        new Color(1.00f, 0.45f, 0.85f),   // 5 rhodo     — pink
        new Color(0.55f, 0.95f, 1.00f),   // 6 diamond   — ice cyan
    };

    // Short display names for HUD toasts ("NEW GEM: SAPPHIRE").
    private static readonly string[] Names = { "RUBY", "EMERALD", "SAPPHIRE", "CITRINE", "ROSE", "DIAMOND" };

    /// <summary>Display name of a colour index, upper-case for the pixel fonts.</summary>
    public static string NameFor(int colorIndex)
    {
        int i = Mathf.Clamp(colorIndex - 1, 0, Names.Length - 1);
        return Names[i];
    }

    /// <summary>The Standard-ladder tier whose sprite this colour index wears.</summary>
    public static int SpriteTierFor(int colorIndex)
    {
        int i = Mathf.Clamp(colorIndex - 1, 0, SpriteTiers.Length - 1);
        return SpriteTiers[i];
    }

    /// <summary>The signature colour for glows, sparks and score text.</summary>
    public static Color ColorFor(int colorIndex)
    {
        int i = Mathf.Clamp(colorIndex - 1, 0, Colors.Length - 1);
        return Colors[i];
    }
}

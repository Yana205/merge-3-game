/// <summary>
/// Which merge chain a gem belongs to. A gem's identity is (family, tier): two
/// gems merge only when BOTH match, so a red 3 and a standard 3 are different
/// gems that happen to share a number.
///
/// Every rule that used to compare bare tiers must compare family too — most
/// importantly <see cref="GridManager.HasAnyValidMerge"/>, the jam check. A
/// tier-only check would read a board of reds beside standards as playable and
/// the run would never end.
///
/// Values are explicit because this enum is serialized into GemConfig and onto
/// Items; reordering the members would silently repaint the board.
/// </summary>
public enum GemFamily
{
    /// <summary>The original ladder — obsidian through diamond.</summary>
    Standard = 0,

    /// <summary>The red chain. Merges only with itself; tops out short of the
    /// standard ladder, so a maxed red is a permanently occupied cell.</summary>
    Red = 1,
}

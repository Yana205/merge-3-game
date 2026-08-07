/// <summary>
/// Which merge chain a gem belongs to. A gem's identity is (family, tier): two
/// gems fuse only when BOTH match, so a red 3 and a standard 3 are different
/// gems that happen to share a number.
///
/// Every rule that used to compare bare tiers must compare family too — most
/// importantly <see cref="GridManager.GetConnectedGroup"/>, which decides what a
/// placement fuses. A tier-only walk would collapse a red into a cyan cluster.
///
/// Values are explicit because this enum is serialized into GemConfig and onto
/// Items; reordering the members would silently repaint the board.
/// </summary>
public enum GemFamily
{
    /// <summary>The original ladder — obsidian through diamond.</summary>
    Standard = 0,

    /// <summary>The red chain. Fuses only with itself, and arms as a bomb at
    /// BombController.ArmTier instead of climbing to the top of its ladder.</summary>
    Red = 1,
}

/// <summary>
/// Legacy axis from the merge-chain era, kept because GemConfig serializes it.
/// The match-3 game only ever spawns <see cref="Standard"/>; a gem's playable
/// identity is its colour index alone (see <see cref="GemPalette"/>).
///
/// Values are explicit because this enum is serialized into GemConfig and onto
/// Items; reordering the members would silently repaint stored data.
/// </summary>
public enum GemFamily
{
    /// <summary>The original ladder — obsidian through diamond.</summary>
    Standard = 0,

    /// <summary>The red chain from the retired place-and-fuse mode. Unused in
    /// match-3 but kept so GemConfig's serialized red ladder stays readable.</summary>
    Red = 1,
}

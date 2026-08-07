/// <summary>
/// One crystal the queue is holding: which ladder it belongs to and which rung.
///
/// A value type on purpose. A queue entry is a description of a crystal, not a
/// crystal — the actual <see cref="Item"/> is not created until the player places
/// it, and the preview shown under the board is a separate throwaway Item that is
/// destroyed and rebuilt whenever the queue moves. Keeping the queue as plain data
/// means nothing in it can be left dangling by the object pool.
/// </summary>
public readonly struct CrystalSpec
{
    public readonly GemFamily Family;
    public readonly int Tier;

    public CrystalSpec(GemFamily family, int tier)
    {
        Family = family;
        Tier = tier;
    }

    public override string ToString() => Family + " t" + Tier;
}

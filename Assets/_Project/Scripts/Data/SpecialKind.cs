/// <summary>
/// What a gem does beyond matching. Made by matching four (Cross) or five
/// (Prism) in a line; fired when the gem is matched again, hit by another
/// special's blast, or — for a Prism — swapped with any gem.
/// </summary>
public enum SpecialKind
{
    None = 0,

    /// <summary>Clears its whole row and column when it goes off.</summary>
    Cross = 1,

    /// <summary>Clears every gem of one colour: the colour it was swapped with,
    /// or its own when matched in a line. Two Prisms swapped clear the board.</summary>
    Prism = 2,
}

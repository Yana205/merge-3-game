using UnityEngine;

/// <summary>
/// The one move in the game: swap two adjacent gems. If the swap makes a match it
/// stands and the board resolves; if it makes nothing the gems snap back, so an
/// illegal swap costs the player nothing.
///
/// Kept as the single thing the input layer talks to (its name is historical — it
/// used to place crystals). InputHandler asks "can these two swap?" and gets a yes
/// or no; it never learns that matches, gravity or refills exist.
/// </summary>
public class PlacementController : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private MergeManager mergeManager;

    /// <summary>Two cells are swappable only if they are orthogonal neighbours.</summary>
    public bool AreAdjacent(Cell a, Cell b)
    {
        if (a == null || b == null) return false;
        return Mathf.Abs(a.row - b.row) + Mathf.Abs(a.col - b.col) == 1;
    }

    /// <summary>
    /// Swap the gems in <paramref name="a"/> and <paramref name="b"/>. Returns true
    /// and resolves the board when the swap creates a match; returns false and
    /// reverts the swap when it does not.
    /// </summary>
    public bool TrySwap(Cell a, Cell b)
    {
        if (mergeManager == null) return false;
        if (a == null || b == null || !a.IsOccupied() || !b.IsOccupied()) return false;
        if (!AreAdjacent(a, b)) return false;

        mergeManager.SwapItems(a, b);

        if (mergeManager.HasAnyMatch())
        {
            mergeManager.ResolveBoard();
            return true;
        }

        // No match: put them back. A swap that does nothing is not a move.
        mergeManager.SwapItems(a, b);
        return false;
    }
}

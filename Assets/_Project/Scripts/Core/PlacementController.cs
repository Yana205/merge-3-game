using UnityEngine;

/// <summary>
/// The one move in the game: swap two adjacent gems. If the swap makes a match it
/// stands and the board resolves (animated — see MergeManager); if it makes
/// nothing the input layer slides the gems back, so an illegal swap costs the
/// player nothing.
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

    /// <summary>True while the board is mid-resolve — input must wait.</summary>
    public bool IsBusy => mergeManager != null && mergeManager.IsResolving;

    /// <summary>Two cells are swappable only if they are orthogonal neighbours.</summary>
    public bool AreAdjacent(Cell a, Cell b)
    {
        if (a == null || b == null) return false;
        return Mathf.Abs(a.row - b.row) + Mathf.Abs(a.col - b.col) == 1;
    }

    /// <summary>Would swapping these two adjacent gems form a match? Lets the input
    /// layer animate a real swap or a bounce-back before committing.</summary>
    public bool WouldMatch(Cell a, Cell b)
    {
        return mergeManager != null && AreAdjacent(a, b) && mergeManager.WouldSwapMatch(a, b);
    }

    /// <summary>The first swap on the board that would make a match — used by the
    /// input layer's idle hint.</summary>
    public bool TryFindHintMove(out Cell a, out Cell b)
    {
        if (mergeManager != null) return mergeManager.TryFindHintMove(out a, out b);
        a = null; b = null;
        return false;
    }

    /// <summary>
    /// Swap the gems in <paramref name="a"/> and <paramref name="b"/>. Returns true
    /// and starts the animated resolve when the swap creates a match; returns false
    /// and reverts the (logical) swap when it does not. The caller owns the slide
    /// animation either way — this only ever touches board state.
    /// </summary>
    public bool TrySwap(Cell a, Cell b)
    {
        if (mergeManager == null || mergeManager.IsResolving) return false;
        if (a == null || b == null || !a.IsOccupied() || !b.IsOccupied()) return false;
        if (!AreAdjacent(a, b)) return false;

        mergeManager.SwapItems(a, b);

        if (mergeManager.HasAnyMatch())
        {
            mergeManager.BeginResolve();
            return true;
        }

        // No match: put them back. A swap that does nothing is not a move.
        mergeManager.SwapItems(a, b);
        return false;
    }
}

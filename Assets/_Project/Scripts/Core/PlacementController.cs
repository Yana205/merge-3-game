using UnityEngine;

/// <summary>
/// The one move in the game: put the queue's next crystal on an empty cell and let
/// fusion resolve.
///
/// Kept separate from LevelManager because it is the only thing the input layer needs
/// to talk to. InputHandler asks this "can I place here?" and gets a yes or no;
/// it never learns that a queue, a difficulty curve or a fusion rule exist.
/// </summary>
public class PlacementController : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private MergeManager mergeManager;
    [SerializeField] private CrystalQueue queue;

    /// <summary>
    /// The on-screen crystal that stands for "what you are about to place". The
    /// input layer needs to recognise it so a player who grabs it can drag it onto
    /// the board — which is what everyone tries first, the row under the board
    /// looking exactly like three objects waiting to be picked up.
    ///
    /// Exposed through here rather than handing InputHandler the queue itself, so
    /// the input layer still knows nothing about rolling, refilling or difficulty.
    /// </summary>
    public bool IsNextHandle(Item item) => item != null && queue != null && item == queue.NextPreview;

    /// <summary>Abandon a drag: put the preview row back the way it was.</summary>
    public void CancelDrag() => queue?.RestorePreviews();

    /// <summary>
    /// Place the queue's next crystal in <paramref name="cell"/> and resolve every
    /// fusion it completes. Returns false when the cell cannot take a crystal, so
    /// the caller can play a rejection instead of silently doing nothing.
    /// </summary>
    public bool TryPlace(Cell cell)
    {
        if (gridManager == null || queue == null) return false;
        if (cell == null || cell.IsOccupied()) return false;
        if (!queue.HasNext) return false;

        // Take only once the placement is certain to succeed. Popping first and
        // then failing would silently eat the crystal the player was looking at.
        CrystalSpec spec = queue.Take();

        Item placed = gridManager.SpawnItem(cell, spec.Tier, spec.Family);
        if (placed == null)
        {
            Debug.LogError("PlacementController: SpawnItem returned null for an empty cell.");
            return false;
        }

        GameEvents.RaiseCrystalPlaced(placed, cell);

        // Fusion is resolved from the tapped cell, so the result lands where the
        // player aimed. Zero fusions is a perfectly ordinary outcome — most
        // placements are building toward a group rather than completing one.
        mergeManager?.ResolveAt(cell);

        return true;
    }
}

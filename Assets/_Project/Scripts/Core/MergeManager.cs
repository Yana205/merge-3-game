using UnityEngine;

// CACHE AUDIT (Lesson 3.1)
// - TryMerge(): replaced direct Destroy(itemA.gameObject)/Destroy(itemB.gameObject)
//   and Instantiate(gridManager.itemPrefab, ...) with gridManager.DespawnItem(...)
//   and gridManager.SpawnItem(cellB, newTier), so all Item lifetime is
//   centralized in GridManager (later phases build pooling on top of it).
public class MergeManager : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    public GridManager gridManager;

    // Direct (local) event: fired after a successful merge with the merged item
    // and its cell. Kept for owners that hold a MergeManager reference and want a
    // tightly-scoped hook (juice, sound, screen shake) without going global.
    public event System.Action<Item, Cell> OnMerged;

    public bool TryMerge(Item itemA, Item itemB)
    {
        // Can't merge an item with itself
        if (itemA == itemB) return false;

        // Must be the same tier
        if (itemA.Tier != itemB.Tier) return false;

        if (itemA.Tier >= Item.MaxTier) return false;

        Cell cellA = gridManager.FindCellWithItem(itemA);
        Cell cellB = gridManager.FindCellWithItem(itemB);

        if (cellA == null || cellB == null) return false;

        int newTier = itemA.Tier + 1;

        // Remove from cells and despawn old items through GridManager
        cellA.RemoveItem();
        cellB.RemoveItem();
        gridManager.DespawnItem(itemA);
        gridManager.DespawnItem(itemB);

        // Spawn merged item at the drop destination
        // (cellB is free here because RemoveItem already ran)
        Item newItem = gridManager.SpawnItem(cellB, newTier);
        if (newItem == null)
        {
            Debug.LogError("MergeManager: SpawnItem returned null during merge.");
            return false;
        }

        // Direct event for local subscribers...
        OnMerged?.Invoke(newItem, cellB);

        // ...and the global bus so any system can react (ScoreController adds the
        // score here — MergeManager no longer needs to know scoring exists).
        GameEvents.RaiseTileMerged(newItem, cellB);

        return true;
    }
}

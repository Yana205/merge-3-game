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
    public LevelManager levelManager;

    // FUTURE: add merge particle effect, sound, screen shake
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

        if (levelManager != null)
        {
            int points = newItem.GemData != null ? newItem.GemData.scoreValue : newTier * 10;
            levelManager.AddScore(points);
        }

        return true;
    }
}

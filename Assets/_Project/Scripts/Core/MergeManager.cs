using UnityEngine;

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

        // Remove from cells and destroy old items
        cellA.RemoveItem();
        cellB.RemoveItem();
        Destroy(itemA.gameObject);
        Destroy(itemB.gameObject);

        // Spawn merged item at the drop destination
        GameObject newGO = Instantiate(gridManager.itemPrefab, cellB.transform.position, Quaternion.identity);
        Item newItem = newGO.GetComponent<Item>();
        newItem.Setup(newTier);
        cellB.PlaceItem(newItem);

        if (levelManager != null)
        {
            int points = newItem.GemData != null ? newItem.GemData.scoreValue : newTier * 10;
            levelManager.AddScore(points);
        }

        return true;
    }
}

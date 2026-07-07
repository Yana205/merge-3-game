using UnityEngine;

// POOL DESIGN:
// - WHAT IS POOLED: the gem Item prefab. It is the highest-churn object in the
//   game — every player move spawns one Item, and every merge despawns two
//   Items and spawns one. Cells are created once per level and never churn,
//   so only Items are worth pooling.
// - POOL SIZE: 32 pre-warmed instances. The default grid is 5x5 = 25 cells, so
//   at most 25 Items can be on the board at once; a few extra cover the
//   transient churn of a merge in flight (despawn two + spawn one before the
//   released pair is reusable), rounded up to 32. Larger LevelData grids are
//   still safe because the pool grows on demand.
// - STATIC OR DYNAMIC: dynamic. The board size comes from LevelData assets, so
//   a hard cap would break the moment a level ships with a grid bigger than
//   the pre-warm count. Growth beyond 32 is a rare, one-time cost; capping
//   would trade that for a gameplay bug.
// - RESET ON RELEASE: Release() calls Item.ResetForPool(), which clears
//   Tier (-> 0), GemData (-> null), spriteRenderer.sprite (-> null),
//   spriteRenderer.color (-> Color.white) and tierLabel.text (-> ""), so a
//   recycled Item never shows the previous gem's state.
public class ItemPoolManager : MonoBehaviour
{
    [Header("Pool Settings")]
    [SerializeField] private GameObject itemPrefab;
    [SerializeField] private int prewarmCount = 32;

    private IPool<Item> _pool;

    void Awake()
    {
        if (itemPrefab == null)
        {
            Debug.LogError("ItemPoolManager: itemPrefab is not assigned.");
            return;
        }

        MonoBehaviourPool<Item> pool = new MonoBehaviourPool<Item>();
        pool.Init(itemPrefab, prewarmCount);
        _pool = pool;
    }

    /// <summary>
    /// Takes an Item from the pool and places it at <paramref name="position"/>.
    /// The pool grows on demand, so this only returns null if the pool could
    /// not be created (missing prefab or prefab without an Item component).
    /// </summary>
    public Item Get(Vector3 position)
    {
        if (_pool == null)
        {
            Debug.LogError("ItemPoolManager: pool was not created (missing itemPrefab?).");
            return null;
        }

        Item item = _pool.Get();
        if (item == null)
            return null;

        item.transform.position = position;
        return item;
    }

    /// <summary>
    /// Resets the item's per-life state and returns it to the pool.
    /// </summary>
    public void Release(Item item)
    {
        if (item == null)
            return;

        if (_pool == null)
        {
            Debug.LogError("ItemPoolManager: pool was not created (missing itemPrefab?).");
            return;
        }

        item.ResetForPool();
        _pool.Release(item);
    }
}

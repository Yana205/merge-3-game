using UnityEngine;

// POOL DESIGN (carried over from Lesson 3's ItemPoolManager, now retired):
// WHAT: gem Items — one spawned per move, two released + one taken per merge.
// SIZE: 32 prewarmed (ServiceLoader.LoadAsync) — 5x5 board = 25 cells max,
//       rounded up for merge churn; larger LevelData grids covered by growth.
// STATIC OR DYNAMIC: dynamic — MonoBehaviourPool<T>.Get() grows on demand, so
//       bigger boards never break; prewarm just avoids startup hitches.
// RESET ON RELEASE: Item.ResetForPool() clears Tier, GemData, sprite, color,
//       and the tier label before the item re-enters the pool.

/// <summary>
/// Thin creation seam between gameplay code and the Item pool. Plain C# class —
/// it knows nothing about tiers, scoring, or the grid, and never calls
/// Object.Instantiate (the pool owns instantiation).
/// </summary>
public class ItemFactory
{
    private GameObject _prefab;
    private IPool<Item> _pool;

    public void Init(GameObject prefab, IPool<Item> pool)
    {
        if (prefab == null)
            Debug.LogError("ItemFactory: Init called with a null prefab.");
        if (pool == null)
            Debug.LogError("ItemFactory: Init called with a null pool.");

        _prefab = prefab;
        _pool = pool;
    }

    /// <summary>
    /// Takes an Item from the pool and places it at <paramref name="position"/>.
    /// Returns null if the factory was not initialized.
    /// </summary>
    public Item Get(Vector3 position)
    {
        if (_prefab == null || _pool == null)
        {
            Debug.LogError("ItemFactory: Get called before Init.");
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
            Debug.LogError("ItemFactory: Release called before Init.");
            return;
        }

        item.ResetForPool();
        _pool.Release(item);
    }
}

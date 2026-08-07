using System.Collections.Generic;
using UnityEngine;

// CACHE AUDIT (Lesson 3.1)
// - ClearGrid(): replaced the whole-scene FindObjectsByType<Item> scan with the
//   owned _liveItems list while playing. The scan is kept ONLY as the edit-mode
//   fallback — editor tooling calls ClearGrid outside play mode, where the
//   non-serialized list can be stale.
// - SpawnItem() now registers every spawned Item in _liveItems, and the new
//   DespawnItem() unregisters + destroys via SafeDestroy, so all Item lifetime
//   is centralized in GridManager.
// - Also audited: the project was searched for "new WaitForSeconds" in loops
//   and none exist.
public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    public int rows = 5;
    public int cols = 5;
    public float cellSize = 1.2f;

    [Header("Prefabs (assign in Inspector)")]
    public GameObject cellPrefab;
    public GameObject itemPrefab;

    // Injected by ServiceLoader once the GemItem Addressable is loaded; the
    // only spawn/despawn path in play mode.
    private ItemFactory _itemFactory;

    private Cell[,] grid;

    // Every Item spawned by this manager, so ClearGrid doesn't need a scene scan.
    private readonly List<Item> _liveItems = new List<Item>();

    // Log the "factory not injected" error only once, not on every spawn.
    private bool _warnedPoolUnwired;

    // FUTURE: add grid border highlight, cell padding

    public void CreateGrid(int newRows, int newCols)
    {
        if (cellPrefab == null)
        {
            Debug.LogError("GridManager: cellPrefab is not assigned.");
            return;
        }

        rows = newRows;
        cols = newCols;

        ClearGrid();

        grid = new Cell[rows, cols];

        float gridW = (cols - 1) * cellSize;
        float gridH = (rows - 1) * cellSize;
        Vector3 origin = new Vector3(-gridW / 2f, -gridH / 2f, 0);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                Vector3 pos = origin + new Vector3(c * cellSize, r * cellSize, 0);
                GameObject go = Instantiate(cellPrefab, pos, Quaternion.identity, transform);
                Cell cell = go.GetComponent<Cell>();
                cell.row = r;
                cell.col = c;
                grid[r, c] = cell;
            }
        }
    }

    public void ClearGrid()
    {
        if (Application.isPlaying)
        {
            // Despawn a copy: DespawnItem mutates _liveItems while we iterate.
            Item[] items = _liveItems.ToArray();
            foreach (Item item in items)
                DespawnItem(item);
            _liveItems.Clear();
        }
        else
        {
            // Edit-mode fallback: editor tooling calls ClearGrid outside play
            // mode, where the non-serialized _liveItems list can be stale.
            Item[] items = FindObjectsByType<Item>();
            foreach (Item item in items)
                SafeDestroy(item.gameObject);
            _liveItems.Clear();
        }

        if (grid != null)
        {
            foreach (Cell cell in grid)
            {
                if (cell != null)
                    SafeDestroy(cell.gameObject);
            }
        }

        for (int i = transform.childCount - 1; i >= 0; i--)
            SafeDestroy(transform.GetChild(i).gameObject);

        grid = null;
    }

    static void SafeDestroy(Object obj)
    {
        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }

    public Cell GetCell(int row, int col)
    {
        // rows/cols are serialized and keep their inspector values before
        // CreateGrid runs, so an in-range index is not proof the grid exists.
        // Every caller already handles a null cell; throwing here does not help.
        if (grid == null) return null;

        if (row < 0 || row >= rows || col < 0 || col >= cols)
            return null;
        return grid[row, col];
    }

    // Called by ServiceLoader after the GemItem Addressable finished loading.
    public void SetItemFactory(ItemFactory factory)
    {
        _itemFactory = factory;
    }

    // The one place an Item comes into existence, shared by the board spawn and the
    // loose queue-preview spawn so the pool/fallback logic is not written twice.
    private Item CreateItem(Vector3 position)
    {
        if (_itemFactory != null)
            return _itemFactory.Get(position);

        // Last-resort fallback: keep the game running when ServiceLoader
        // has not injected the factory yet (or is missing), but say so once.
        if (!_warnedPoolUnwired)
        {
            Debug.LogError("GridManager: ItemFactory is not injected — falling back to Instantiate/Destroy for Items.");
            _warnedPoolUnwired = true;
        }

        if (itemPrefab == null)
        {
            Debug.LogError("GridManager: itemPrefab is not assigned.");
            return null;
        }

        GameObject go = Instantiate(itemPrefab, position, Quaternion.identity);
        return go.GetComponent<Item>();
    }

    public Item SpawnItem(Cell cell, int tier = 1, GemFamily family = GemFamily.Standard)
    {
        if (cell == null || cell.IsOccupied())
            return null;

        Item item = CreateItem(cell.transform.position);
        if (item == null)
            return null;

        item.Setup(tier, family);

        // Parent subscribes to the child's direct event via += — the Observer
        // pattern for an owner that holds the child instance. Item.ResetForPool
        // clears its own subscribers on pool return, so this subscription is
        // dropped automatically when the item despawns (no manual -= leak for the
        // pooled object).
        item.OnDespawned += HandleItemDespawned;

        cell.PlaceItem(item);
        _liveItems.Add(item);
        return item;
    }

    // Central despawn point: every Item created by SpawnItem should die here so
    // _liveItems stays accurate. In play mode items go back through the factory;
    // the edit-mode path (and the unwired fallback) still uses SafeDestroy.
    public void DespawnItem(Item item)
    {
        if (item == null) return;
        _liveItems.Remove(item);

        if (Application.isPlaying && _itemFactory != null)
            _itemFactory.Release(item);
        else
            SafeDestroy(item.gameObject);
    }

    // Running count of items that have left the board this play session, driven
    // solely by the child's OnDespawned event. Small on its own, but it proves the
    // parent hook fires; the juice pass hangs a despawn burst off the same signal.
    public int TilesDespawnedThisSession { get; private set; }

    void HandleItemDespawned(Item item)
    {
        TilesDespawnedThisSession++;
    }

    public Cell FindCellWithItem(Item item)
    {
        if (grid == null) return null;
        foreach (Cell cell in grid)
        {
            if (cell != null && cell.CurrentItem == item)
                return cell;
        }
        return null;
    }

    public Cell GetRandomEmptyCell()
    {
        if (grid == null) return null;

        var emptyCells = new List<Cell>();
        foreach (Cell cell in grid)
        {
            if (cell != null && !cell.IsOccupied())
                emptyCells.Add(cell);
        }

        if (emptyCells.Count == 0) return null;
        return emptyCells[Random.Range(0, emptyCells.Count)];
    }

    /// <summary>
    /// Every cell reachable from <paramref name="origin"/> through ORTHOGONAL
    /// neighbours holding the identical crystal — same family and same tier.
    /// Includes the origin itself, so the result is never empty for an occupied
    /// cell. Returns an empty list for a null or empty origin.
    ///
    /// Orthogonal on purpose. With diagonals on a 6x6 board, groups of three form
    /// almost by accident and the puzzle evaporates; 4-way is also the rule every
    /// classic merge game uses.
    ///
    /// Armed bombs are excluded from both ends of the walk: a bomb is a button, not
    /// a crystal, and a fourth red landing beside a finished bomb must not be able
    /// to fuse it back into an ordinary gem.
    /// </summary>
    public List<Cell> GetConnectedGroup(Cell origin)
    {
        var group = new List<Cell>();
        if (grid == null || origin == null || !origin.IsOccupied()) return group;
        if (origin.CurrentItem.IsArmedBomb) return group;

        int tier = origin.CurrentItem.Tier;
        GemFamily family = origin.CurrentItem.Family;

        var seen = new HashSet<Cell> { origin };
        var frontier = new Queue<Cell>();
        frontier.Enqueue(origin);
        group.Add(origin);

        // Orthogonal only: no diagonals in this table, and that is the whole rule.
        var steps = new (int dr, int dc)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };

        while (frontier.Count > 0)
        {
            Cell cell = frontier.Dequeue();
            foreach ((int dr, int dc) in steps)
            {
                Cell next = GetCell(cell.row + dr, cell.col + dc);
                if (next == null || seen.Contains(next)) continue;
                if (!next.IsOccupied()) continue;

                Item item = next.CurrentItem;
                if (item.IsArmedBomb) continue;
                if (item.Tier != tier || item.Family != family) continue;

                seen.Add(next);
                group.Add(next);
                frontier.Enqueue(next);
            }
        }

        return group;
    }

    /// <summary>
    /// Spawn an Item at a world position with no Cell behind it — used for the
    /// queue previews below the board.
    ///
    /// A loose item is deliberately inert: placement requires an empty Cell, and
    /// PickaxeController.Shatter already refuses an item that FindCellWithItem
    /// cannot locate, so tapping a preview does nothing in every input mode.
    ///
    /// It IS registered in _liveItems, so ClearGrid reclaims it — which means the
    /// queue must re-render AFTER a board rebuild, never before.
    /// </summary>
    public Item SpawnLooseItem(Vector3 position, int tier, GemFamily family)
    {
        Item item = CreateItem(position);
        if (item == null) return null;

        item.Setup(tier, family);
        item.OnDespawned += HandleItemDespawned;
        _liveItems.Add(item);
        return item;
    }

    /// <summary>World-space centre of the board, so callers can lay things out
    /// relative to it without duplicating the origin maths in CreateGrid.</summary>
    public Vector3 BoardCentre => transform.position;

    /// <summary>World-space Y of the row below the bottom of the board.</summary>
    public float BottomEdgeY => transform.position.y - ((rows - 1) * cellSize) / 2f;

    public bool IsFull()
    {
        if (grid == null) return true;

        foreach (Cell cell in grid)
        {
            if (cell != null && !cell.IsOccupied())
                return false;
        }

        return true;
    }
}

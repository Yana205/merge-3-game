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
        if (row < 0 || row >= rows || col < 0 || col >= cols)
            return null;
        return grid[row, col];
    }

    // Called by ServiceLoader after the GemItem Addressable finished loading.
    public void SetItemFactory(ItemFactory factory)
    {
        _itemFactory = factory;
    }

    public Item SpawnItem(Cell cell, int tier = 1)
    {
        if (cell == null || cell.IsOccupied())
            return null;

        Item item;
        if (_itemFactory != null)
        {
            item = _itemFactory.Get(cell.transform.position);
        }
        else
        {
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

            GameObject go = Instantiate(itemPrefab, cell.transform.position, Quaternion.identity);
            item = go.GetComponent<Item>();
        }

        if (item == null)
            return null;

        item.Setup(tier);
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

    public bool AreAdjacent(Cell a, Cell b)
    {
        return a != b
            && Mathf.Abs(a.row - b.row) <= 1
            && Mathf.Abs(a.col - b.col) <= 1;
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

    public bool HasAnyValidMerge()
    {
        if (grid == null) return false;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                Cell cell = grid[r, c];
                if (cell == null || !cell.IsOccupied()) continue;

                int tier = cell.CurrentItem.Tier;
                if (tier >= Item.MaxTier) continue;

                for (int dr = -1; dr <= 1; dr++)
                {
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        if (dr == 0 && dc == 0) continue;
                        Cell neighbour = GetCell(r + dr, c + dc);
                        if (neighbour != null && neighbour.IsOccupied()
                            && neighbour.CurrentItem.Tier == tier)
                            return true;
                    }
                }
            }
        }

        return false;
    }

    // Counts distinct adjacent same-tier pairs (each unordered pair once).
    // LevelManager uses this to enforce LevelData.guaranteedPairs at board setup.
    public int CountAdjacentSameTierPairs()
    {
        if (grid == null) return 0;

        int pairs = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                Cell cell = grid[r, c];
                if (cell == null || !cell.IsOccupied()) continue;

                int tier = cell.CurrentItem.Tier;
                if (tier >= Item.MaxTier) continue;

                // Only look at forward neighbours so each pair is counted once:
                // east, south-west, south, south-east.
                foreach (var (dr, dc) in new[] { (0, 1), (1, -1), (1, 0), (1, 1) })
                {
                    Cell neighbour = GetCell(r + dr, c + dc);
                    if (neighbour != null && neighbour.IsOccupied()
                        && neighbour.CurrentItem.Tier == tier)
                        pairs++;
                }
            }
        }

        return pairs;
    }

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

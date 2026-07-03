using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    public int rows = 5;
    public int cols = 5;
    public float cellSize = 1.2f;

    [Header("Prefabs (assign in Inspector)")]
    public GameObject cellPrefab;
    public GameObject itemPrefab;

    private Cell[,] grid;

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
        Item[] items = FindObjectsByType<Item>(FindObjectsSortMode.None);
        foreach (Item item in items)
            SafeDestroy(item.gameObject);

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

    public Item SpawnItem(Cell cell, int tier = 1)
    {
        if (cell == null || cell.IsOccupied())
            return null;
        if (itemPrefab == null)
        {
            Debug.LogError("GridManager: itemPrefab is not assigned.");
            return null;
        }

        GameObject go = Instantiate(itemPrefab, cell.transform.position, Quaternion.identity);
        Item item = go.GetComponent<Item>();
        item.Setup(tier);
        cell.PlaceItem(item);
        return item;
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

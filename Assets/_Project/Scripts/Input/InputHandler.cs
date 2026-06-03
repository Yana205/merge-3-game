using UnityEngine;

public class InputHandler : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    public GridManager gridManager;
    public MergeManager mergeManager;

    private Item draggedItem;
    private Cell sourceCell;
    private Vector2 dragOffset;
    private bool isDragging;

    // Highlight colors shown during a drag
    private static readonly Color HighlightMerge = new Color(0.15f, 0.60f, 0.15f); // green — same-tier neighbour
    private static readonly Color HighlightMove  = new Color(0.30f, 0.30f, 0.50f); // blue-gray — empty neighbour

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
            HandlePointerDown();

        if (isDragging)
            HandleDrag();

        if (Input.GetMouseButtonUp(0) && isDragging)
            HandlePointerUp();
    }

    void HandlePointerDown()
    {
        Vector2 worldPos = GetMouseWorldPos();
        Collider2D[] hits = Physics2D.OverlapPointAll(worldPos);

        Item hitItem = null;
        Cell hitCell = null;

        foreach (Collider2D col in hits)
        {
            if (col == null) continue;
            Item item = col.GetComponent<Item>();
            if (item != null) hitItem = item;
            Cell cell = col.GetComponent<Cell>();
            if (cell != null) hitCell = cell;
        }

        if (hitItem != null)
        {
            StartDrag(hitItem, worldPos);
            return;
        }

        if (hitCell != null && !hitCell.IsOccupied())
            gridManager.SpawnItem(hitCell);
    }

    void StartDrag(Item item, Vector2 worldPos)
    {
        draggedItem = item;
        isDragging = true;
        dragOffset = (Vector2)item.transform.position - worldPos;
        sourceCell = gridManager.FindCellWithItem(item);
        HighlightAdjacentCells(sourceCell, item.Tier);
    }

    void HandleDrag()
    {
        if (draggedItem == null) return;
        draggedItem.transform.position = GetMouseWorldPos() + dragOffset;
    }

    void HandlePointerUp()
    {
        ClearAllHighlights();

        if (draggedItem == null)
        {
            isDragging = false;
            return;
        }

        Vector2 worldPos = GetMouseWorldPos();
        Collider2D[] hits = Physics2D.OverlapPointAll(worldPos);

        Item targetItem = null;
        Cell targetCell = null;

        foreach (Collider2D col in hits)
        {
            if (col == null) continue;
            Item item = col.GetComponent<Item>();
            if (item != null && item != draggedItem) targetItem = item;
            Cell cell = col.GetComponent<Cell>();
            if (cell != null) targetCell = cell;
        }

        // If we found an item but no cell, look up the cell via grid
        if (targetCell == null && targetItem != null)
            targetCell = gridManager.FindCellWithItem(targetItem);

        bool success = false;

        if (sourceCell != null && targetCell != null && gridManager.AreAdjacent(sourceCell, targetCell))
        {
            if (targetItem != null)
            {
                // Try merge — MergeManager handles same-tier check internally
                success = mergeManager.TryMerge(draggedItem, targetItem);
            }
            else if (!targetCell.IsOccupied())
            {
                // Move to empty adjacent cell
                sourceCell.RemoveItem();
                targetCell.PlaceItem(draggedItem);
                draggedItem.transform.position = targetCell.transform.position;
                success = true;
            }
        }

        if (!success)
        {
            // Snap back to origin
            if (sourceCell != null)
                draggedItem.transform.position = sourceCell.transform.position;
        }

        draggedItem = null;
        sourceCell = null;
        isDragging = false;
    }

    void HighlightAdjacentCells(Cell source, int tier)
    {
        if (source == null) return;
        for (int dr = -1; dr <= 1; dr++)
        {
            for (int dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0) continue;
                Cell neighbour = gridManager.GetCell(source.row + dr, source.col + dc);
                if (neighbour == null) continue;

                if (!neighbour.IsOccupied())
                    neighbour.SetHighlight(HighlightMove);
                else if (neighbour.CurrentItem.Tier == tier)
                    neighbour.SetHighlight(HighlightMerge);
            }
        }
    }

    void ClearAllHighlights()
    {
        for (int r = 0; r < gridManager.rows; r++)
            for (int c = 0; c < gridManager.cols; c++)
            {
                Cell cell = gridManager.GetCell(r, c);
                if (cell != null) cell.ClearHighlight();
            }
    }

    Vector2 GetMouseWorldPos()
    {
        Camera cam = Camera.main;
        if (cam == null) return Vector2.zero;
        Vector3 pos = cam.ScreenToWorldPoint(Input.mousePosition);
        return new Vector2(pos.x, pos.y);
    }
}

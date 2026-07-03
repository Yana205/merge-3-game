using UnityEngine;
using System;

public class InputHandler : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    public GridManager gridManager;
    public MergeManager mergeManager;

    public event Action OnGameOver;

    private Item _draggedItem;
    private Cell _sourceCell;
    private Vector2 _dragOffset;
    private bool _isDragging;
    private bool _gameOver;
    private bool _inputEnabled = true;

    private static readonly Color HighlightMerge = new Color(0.15f, 0.60f, 0.15f);
    private static readonly Color HighlightMove  = new Color(0.30f, 0.30f, 0.50f);

    void Update()
    {
        if (_gameOver || !_inputEnabled) return;

        if (Input.GetMouseButtonDown(0))
            HandlePointerDown();

        if (_isDragging)
            HandleDrag();

        if (Input.GetMouseButtonUp(0) && _isDragging)
            HandlePointerUp();
    }

    void HandlePointerDown()
    {
        Vector2 worldPos = GetMouseWorldPos();
        Collider2D[] hits = Physics2D.OverlapPointAll(worldPos);

        foreach (Collider2D col in hits)
        {
            if (col == null) continue;
            Item item = col.GetComponent<Item>();
            if (item != null)
            {
                StartDrag(item, worldPos);
                return;
            }
        }
    }

    void StartDrag(Item item, Vector2 worldPos)
    {
        _draggedItem = item;
        _isDragging = true;
        _dragOffset = (Vector2)item.transform.position - worldPos;
        _sourceCell = gridManager.FindCellWithItem(item);
        HighlightAdjacentCells(_sourceCell, item.Tier);
    }

    void HandleDrag()
    {
        if (_draggedItem == null) return;
        _draggedItem.transform.position = GetMouseWorldPos() + _dragOffset;
    }

    void HandlePointerUp()
    {
        ClearAllHighlights();

        if (_draggedItem == null)
        {
            _isDragging = false;
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
            if (item != null && item != _draggedItem) targetItem = item;
            Cell cell = col.GetComponent<Cell>();
            if (cell != null) targetCell = cell;
        }

        if (targetCell == null && targetItem != null)
            targetCell = gridManager.FindCellWithItem(targetItem);

        bool success = false;

        if (_sourceCell != null && targetCell != null && gridManager.AreAdjacent(_sourceCell, targetCell))
        {
            if (targetItem != null)
            {
                success = mergeManager.TryMerge(_draggedItem, targetItem);
            }
            else if (!targetCell.IsOccupied())
            {
                _sourceCell.RemoveItem();
                targetCell.PlaceItem(_draggedItem);
                _draggedItem.transform.position = targetCell.transform.position;
                success = true;
            }
        }

        if (!success)
        {
            if (_sourceCell != null)
                _draggedItem.transform.position = _sourceCell.transform.position;
        }
        // If the merge completed the level, input is locked mid-call — skip the
        // post-move spawn / game-over check so a won board stays frozen and clean.
        else if (_inputEnabled)
        {
            SpawnAndCheckGameOver();
        }

        _draggedItem = null;
        _sourceCell = null;
        _isDragging = false;
    }

    void SpawnAndCheckGameOver()
    {
        Cell emptyCell = gridManager.GetRandomEmptyCell();
        if (emptyCell != null)
            gridManager.SpawnItem(emptyCell);

        if (gridManager.IsFull() && !gridManager.HasAnyValidMerge())
        {
            _gameOver = true;
            OnGameOver?.Invoke();
        }
    }

    public void ResetState()
    {
        _gameOver = false;
        _inputEnabled = true;
        _draggedItem = null;
        _sourceCell = null;
        _isDragging = false;
    }

    // Called by LevelManager to freeze/unfreeze board interaction
    // (e.g. lock the board once the level is complete).
    public void SetInputEnabled(bool enabled)
    {
        _inputEnabled = enabled;
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

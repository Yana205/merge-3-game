using UnityEngine;
using System;

/// <summary>
/// Match-3 swap input, reachable two ways — whichever the player reaches for:
///
///   swipe a gem toward a neighbour   → the two swap
///   tap a gem, then tap a neighbour  → the two swap
///
/// A swap that makes no match snaps back (PlacementController decides), so there is
/// no wrong move to punish. The selected gem is highlighted so the two-tap path has
/// visible state.
///
/// This replaces the old place-a-crystal input. There is no queue, no drag-onto-an-
/// empty-cell, no pickaxe and no bomb tap any more — the board is always full and
/// the only verb is the swap.
/// </summary>
public class InputHandler : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    public GridManager gridManager;

    [Tooltip("The only thing this class needs to know about the rules: whether a " +
             "swap of two cells turned into a move.")]
    [SerializeField] private PlacementController placement;

    [Header("Feedback")]
    [Tooltip("Tint laid over the currently selected gem's cell.\n\n" +
             "Gold specifically: a cell renders WHITE and takes its colour from the " +
             "gem shader underneath, so a tint can only subtract — pulling the blue " +
             "channel down is what makes the selection legible.")]
    [SerializeField] private Color selectedTint = new Color(1f, 0.79f, 0.28f, 1f);

    [Tooltip("How far the cursor must travel from the pressed gem, as a fraction of " +
             "one cell, before the gesture counts as a swipe rather than a tap.")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float swipeFraction = 0.35f;

    // Kept so LevelManager's existing subscriptions still bind. OnGameOver never
    // fires in the endless match-3 game — the board reshuffles instead of dead-
    // ending — but the seam is left in place for a future move/time limit.
    public event Action OnGameOver;

    /// <summary>Raised after a swap resolves into a move.</summary>
    public event Action OnMoveCompleted;

    private bool _inputEnabled = true;
    private Camera _camera;

    // The gem waiting for its partner in the tap-tap path, and where the press that
    // may become a swipe began.
    private Cell _selected;
    private Vector2 _pressWorld;

    void Awake()
    {
        _camera = Camera.main;
        if (_camera == null)
            Debug.LogError("InputHandler: no camera tagged 'MainCamera' found in the scene.");
    }

    void Update()
    {
        if (!_inputEnabled)
        {
            ClearSelection();
            return;
        }

        if (Input.GetMouseButtonDown(0))
            HandleDown();
        else if (Input.GetMouseButtonUp(0))
            HandleUp();
    }

    void HandleDown()
    {
        _pressWorld = GetMouseWorldPos();
        Cell cell = ProbeCell();

        // Pressed empty space / off the board: drop any pending selection.
        if (cell == null || !cell.IsOccupied())
        {
            ClearSelection();
            return;
        }

        // Second tap of a tap-tap on a neighbour of the selected gem: swap them.
        if (_selected != null && _selected != cell && placement != null && placement.AreAdjacent(_selected, cell))
        {
            DoSwap(_selected, cell);
            return;
        }

        Select(cell);
    }

    void HandleUp()
    {
        if (_selected == null) return;

        Vector2 delta = GetMouseWorldPos() - _pressWorld;
        float threshold = gridManager != null ? gridManager.cellSize * swipeFraction : 0.4f;

        // Not far enough to be a swipe: leave the gem selected so a following tap on
        // a neighbour completes the move.
        if (delta.magnitude < threshold) return;

        Cell neighbour = NeighbourInDirection(_selected, delta);
        if (neighbour != null && neighbour.IsOccupied())
            DoSwap(_selected, neighbour);
        else
            ClearSelection();
    }

    // The board's row axis is +y and its column axis is +x (see GridManager.CreateGrid),
    // so a swipe's dominant axis maps straight onto a grid step.
    Cell NeighbourInDirection(Cell from, Vector2 dir)
    {
        if (from == null || gridManager == null) return null;

        if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
            return gridManager.GetCell(from.row, from.col + (dir.x > 0 ? 1 : -1));

        return gridManager.GetCell(from.row + (dir.y > 0 ? 1 : -1), from.col);
    }

    void DoSwap(Cell a, Cell b)
    {
        ClearSelection();
        if (placement == null) return;

        if (placement.TrySwap(a, b))
            OnMoveCompleted?.Invoke();
        // A no-match swap already snapped back inside TrySwap; nothing to undo here.
    }

    void Select(Cell cell)
    {
        ClearSelection();
        _selected = cell;
        cell.SetHighlight(selectedTint);
    }

    void ClearSelection()
    {
        if (_selected != null)
            _selected.ClearHighlight();
        _selected = null;
    }

    void OnDisable() => ClearSelection();

    Cell ProbeCell()
    {
        foreach (Collider2D col in Physics2D.OverlapPointAll(GetMouseWorldPos()))
        {
            if (col != null && col.TryGetComponent(out Cell cell))
                return cell;
        }
        return null;
    }

    // Called by LevelManager at the start of each run.
    public void ResetState()
    {
        _inputEnabled = true;
        _selected = null;
    }

    // Called by LevelManager to freeze/unfreeze board interaction.
    public void SetInputEnabled(bool enabled)
    {
        _inputEnabled = enabled;
    }

    Vector2 GetMouseWorldPos()
    {
        if (_camera == null) return Vector2.zero;
        Vector3 pos = _camera.ScreenToWorldPoint(Input.mousePosition);
        return new Vector2(pos.x, pos.y);
    }
}

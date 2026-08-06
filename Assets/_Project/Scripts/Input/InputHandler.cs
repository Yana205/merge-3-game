using UnityEngine;
using System;

// CACHE AUDIT (Lesson 3.1)
// - GetMouseWorldPos(): resolved Camera.main on every call — and it runs every
//   Update frame while dragging. The camera is now cached in _camera (assigned
//   once in Awake, with an error log if missing); the null guard is kept.
// - HandlePointerDown()/HandlePointerUp(): per-collider col.GetComponent<Item>()
//   and col.GetComponent<Cell>() calls replaced with col.TryGetComponent(out ...).
public class InputHandler : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    public GridManager gridManager;
    public MergeManager mergeManager;

    [Tooltip("Optional. When assigned, an armed pickaxe turns a tap on a gem into a " +
             "shatter, and a jam with charges in hand becomes a rescue instead of a " +
             "game over. Leave empty and the board plays exactly as it did before.")]
    [SerializeField] private PickaxeController pickaxe;

    public event Action OnGameOver;

    /// <summary>
    /// Raised after any successful move — merge or slide alike — and before the
    /// jam check runs. LevelManager listens and spawns the move's gems; how many
    /// and at what tier is a difficulty decision, which does not belong in the
    /// input layer.
    /// </summary>
    public event Action OnMoveCompleted;

    private Item _draggedItem;
    private Cell _sourceCell;
    private Vector2 _dragOffset;
    private bool _isDragging;
    private bool _gameOver;
    private bool _inputEnabled = true;
    private bool _rescuePending;
    private Camera _camera;

    private static readonly Color HighlightMerge = new Color(0.15f, 0.60f, 0.15f);
    private static readonly Color HighlightMove  = new Color(0.30f, 0.30f, 0.50f);

    void Awake()
    {
        _camera = Camera.main;
        if (_camera == null)
            Debug.LogError("InputHandler: no camera tagged 'MainCamera' found in the scene.");
    }

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
            if (col.TryGetComponent(out Item item))
            {
                // An armed pickaxe consumes the tap: shatter instead of drag. No
                // drag state is set, so this never falls through to HandlePointerUp.
                if (pickaxe != null && pickaxe.IsArmed)
                {
                    if (pickaxe.Shatter(item))
                        AfterShatter();
                    return;
                }

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
        HighlightAdjacentCells(_sourceCell, item.Tier, item.Family);
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
            if (col.TryGetComponent(out Item item) && item != _draggedItem) targetItem = item;
            if (col.TryGetComponent(out Cell cell)) targetCell = cell;
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
        // If something locked input mid-call, skip the post-move spawn and
        // game-over check so a frozen board stays frozen and clean.
        else if (_inputEnabled)
        {
            AfterMove();
        }

        _draggedItem = null;
        _sourceCell = null;
        _isDragging = false;
    }

    // Order is load-bearing: the spawn has to land before the jam check, or the
    // check judges a board that is one move out of date.
    void AfterMove()
    {
        OnMoveCompleted?.Invoke();
        CheckForJam();
    }

    // A shatter is NOT a move: it deliberately does not raise OnMoveCompleted, so
    // no gems spawn afterwards. Spawning here would hand back the cell the player
    // just paid a charge for, which is the whole point of the tool.
    //
    // The jam check still runs, because freeing a cell is exactly what un-jams a
    // board — and because a player who spends their last charge without fixing
    // anything has genuinely reached the end of the run.
    void AfterShatter()
    {
        CheckForJam();
    }

    // The board is lost only when there is no legal move AND no charge left to make
    // one. Ending the run while the player still holds a pickaxe would make the
    // tool worthless precisely when it is needed — you would bank three rescues and
    // watch the game over screen anyway.
    void CheckForJam()
    {
        bool jammed = gridManager.IsFull() && !gridManager.HasAnyValidMerge();

        if (!jammed)
        {
            if (_rescuePending)
            {
                _rescuePending = false;
                GameEvents.RaiseJamRescuePending(false);
            }
            return;
        }

        if (pickaxe != null && pickaxe.HasCharge)
        {
            // The pickaxe is now the only legal action, so arm it for the player
            // rather than making them find the button while staring at a dead board.
            if (!_rescuePending)
            {
                _rescuePending = true;
                GameEvents.RaiseJamRescuePending(true);
            }
            pickaxe.TryArm();
            return;
        }

        _gameOver = true;
        OnGameOver?.Invoke();
    }

    public void ResetState()
    {
        _gameOver = false;
        _inputEnabled = true;
        _draggedItem = null;
        _sourceCell = null;
        _isDragging = false;

        if (_rescuePending)
        {
            _rescuePending = false;
            GameEvents.RaiseJamRescuePending(false);
        }
    }

    // Called by LevelManager to freeze/unfreeze board interaction
    // (e.g. lock the board once the level is complete).
    public void SetInputEnabled(bool enabled)
    {
        _inputEnabled = enabled;
    }

    // The green "you can merge here" glow has to agree with MergeManager exactly,
    // so it matches on family as well as tier. Matching on tier alone would light
    // up a red neighbour of the same number and then refuse the drop.
    void HighlightAdjacentCells(Cell source, int tier, GemFamily family)
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
                else if (neighbour.CurrentItem.Tier == tier
                         && neighbour.CurrentItem.Family == family
                         && tier < Item.MaxTierFor(family))
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
        if (_camera == null) return Vector2.zero;
        Vector3 pos = _camera.ScreenToWorldPoint(Input.mousePosition);
        return new Vector2(pos.x, pos.y);
    }
}

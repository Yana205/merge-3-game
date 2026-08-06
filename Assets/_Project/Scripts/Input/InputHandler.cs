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

    [Tooltip("Optional. When assigned, a tap on a maxed red crystal detonates it, " +
             "and a live bomb counts as a legal move so the run does not end while " +
             "one is still on the board.")]
    [SerializeField] private BombController bombs;

    public event Action OnGameOver;

    /// <summary>
    /// Raised after a successful MERGE, and before the jam check runs. LevelManager
    /// listens and spawns the move's cyan crystal.
    ///
    /// This used to fire for slides too, which is what made repositioning strictly
    /// punishing: a slide removes no tile and used to add one, so the only move that
    /// should cost nothing but position cost a cell. Slides now cost time on the
    /// corruption clock instead — see <see cref="GameEvents.MoveCompleted"/>, which
    /// still fires for every move.
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
                // A live bomb wins over everything, including an armed pickaxe.
                // Detonating is free and shattering costs a charge, so letting the
                // pickaxe consume this tap would silently spend a rescue on the one
                // gem that did not need it. A bomb is also unmergeable and
                // undraggable, so there is nothing else the tap could have meant.
                if (item.IsArmedBomb && bombs != null)
                {
                    if (bombs.Detonate(item) > 0)
                    {
                        pickaxe?.Disarm();
                        AfterDetonation();
                    }
                    return;
                }

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
        bool merged = false;

        if (_sourceCell != null && targetCell != null && gridManager.AreAdjacent(_sourceCell, targetCell))
        {
            if (targetItem != null)
            {
                success = mergeManager.TryMerge(_draggedItem, targetItem);
                merged = success;
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
            AfterMove(merged);
        }

        _draggedItem = null;
        _sourceCell = null;
        _isDragging = false;
    }

    // Order is load-bearing on both counts.
    //
    // The clock ticks first, so an eruption this move lands before the jam check
    // judges the board. And the tick fires for EVERY move while the cyan spawn
    // fires only for merges — that asymmetry is the whole point of the redesign:
    // a slide costs you time but not space, so repositioning is finally worth doing.
    void AfterMove(bool wasMerge)
    {
        GameEvents.RaiseMoveCompleted();

        if (wasMerge)
            OnMoveCompleted?.Invoke();

        CheckForJam();
    }

    // A detonation is NOT a move: no clock tick, no spawn. The player paid for it by
    // building the red chain and by losing whatever cyan was standing next to it,
    // and charging them corruption on top would make the payoff self-defeating.
    //
    // The jam check still runs — freeing up to nine cells is the single most
    // un-jamming thing that can happen to a board.
    void AfterDetonation()
    {
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
        // A live bomb is a legal move the merge scan cannot see: it is a maxed gem,
        // so HasAnyValidMerge correctly reports it as unmergeable, and a full board
        // carrying one would read as dead. Ending the run there would hand the
        // player a game-over screen with the answer sitting on the board.
        if (bombs != null && bombs.HasArmedBomb())
        {
            if (_rescuePending)
            {
                _rescuePending = false;
                GameEvents.RaiseJamRescuePending(false);
            }
            return;
        }

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
                         && tier < Item.MaxTierFor(family)
                         // A bomb can arm below the top of its ladder, so a live
                         // bomb neighbour passes the tier test and would light up
                         // green while TryMerge refuses the drop.
                         && !neighbour.CurrentItem.IsArmedBomb)
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

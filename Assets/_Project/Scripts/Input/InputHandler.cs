using UnityEngine;
using System;

/// <summary>
/// One verb — put the next crystal on an empty cell — reachable two ways:
///
///   tap an EMPTY CELL          → the next crystal lands there
///   drag the NEXT crystal      → onto an empty cell, and it lands there
///   tap an ARMED BOMB          → detonate it
///   tap ANY CRYSTAL            → shatter it, but only while the pickaxe is armed
///
/// The drag is not a second mechanic. It exists because the queue renders three
/// real crystals in open space under the board, and that reads as "pick me up" to
/// everyone who sees it — playtest confirmed the first instinct is to drag one onto
/// the board and conclude the game is broken when nothing happens. Refusing the
/// drag taught the player nothing; accepting it costs one state variable.
///
/// Empty cells light up under the cursor for the same reason: tapping bare
/// background is not a verb anyone guesses, and it had no feedback at all.
///
/// The drag-to-MERGE path this class used to own is still gone. Fusion is automatic
/// on placement, so no move names two crystals.
/// </summary>
public class InputHandler : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    public GridManager gridManager;

    [Tooltip("The only thing this class needs to know about the rules: whether a " +
             "tap on this cell turned into a placement.")]
    [SerializeField] private PlacementController placement;

    [Tooltip("Optional. When assigned, an armed pickaxe turns a tap on a crystal " +
             "into a shatter, and a full board with charges in hand becomes a " +
             "rescue instead of a game over.")]
    [SerializeField] private PickaxeController pickaxe;

    [Tooltip("Optional. When assigned, a tap on a maxed red crystal detonates it, " +
             "and a live bomb counts as a legal move so the run does not end while " +
             "one is still on the board.")]
    [SerializeField] private BombController bombs;

    [Header("Feedback")]
    [Tooltip("Tint laid over an empty cell under the cursor.\n\n" +
             "Gold, and gold specifically. A cell's sprite renders WHITE and takes " +
             "its blue from the crystal shader underneath, so a tint can only ever " +
             "subtract — a bluish highlight just dims the cell and reads as nothing " +
             "at all. Pulling the blue channel down is what makes this legible, and " +
             "it matches the gold the HUD already uses for 'the game is talking to " +
             "you'.")]
    [SerializeField] private Color placeableTint = new Color(1f, 0.79f, 0.28f, 1f);

    public event Action OnGameOver;

    /// <summary>Raised after a crystal is placed and its fusion has resolved, before
    /// the loss check runs.</summary>
    public event Action OnMoveCompleted;

    private bool _gameOver;
    private bool _inputEnabled = true;
    private bool _rescuePending;
    private Camera _camera;

    // The NEXT crystal while it is being dragged. Borrowed from the queue, which
    // stays its owner: any placement or cancel rebuilds the preview row and pools
    // this instance, so it is only ever read between a press and its release.
    private Item _carried;

    // The empty cell currently lit up, so exactly one is ever tinted.
    private Cell _hoveredCell;

    void Awake()
    {
        _camera = Camera.main;
        if (_camera == null)
            Debug.LogError("InputHandler: no camera tagged 'MainCamera' found in the scene.");
    }

    void Update()
    {
        if (_gameOver || !_inputEnabled)
        {
            CancelCarry();
            ClearHover();
            return;
        }

        if (Input.GetMouseButtonDown(0))
            HandlePress();
        else if (Input.GetMouseButtonUp(0) && _carried != null)
            HandleRelease();
        else if (_carried != null)
            _carried.transform.position = GetMouseWorldPos();

        UpdateHover();
    }

    // Everything under the cursor, in one place: a crystal sits on top of its cell,
    // so a point over an occupied square hits both and the rules decide which the
    // gesture meant.
    void Probe(out Item item, out Cell cell)
    {
        item = null;
        cell = null;

        foreach (Collider2D col in Physics2D.OverlapPointAll(GetMouseWorldPos()))
        {
            if (col == null) continue;
            if (item == null && col.TryGetComponent(out Item hitItem)) item = hitItem;
            if (cell == null && col.TryGetComponent(out Cell hitCell)) cell = hitCell;
        }
    }

    void HandlePress()
    {
        Probe(out Item item, out Cell cell);

        // Grabbing the NEXT crystal starts a drag. Checked before the pickaxe,
        // which would otherwise swallow the press: Shatter refuses a preview and
        // returns false, so an armed player could not pick one up at all.
        if (placement != null && placement.IsNextHandle(item))
        {
            _carried = item;
            return;
        }

        // A live bomb wins over everything, including an armed pickaxe. Detonating
        // is free and shattering costs a charge, so letting the pickaxe consume this
        // tap would silently spend a rescue on the one crystal that did not need it.
        if (item != null && item.IsArmedBomb && bombs != null)
        {
            if (bombs.Detonate(item) > 0)
            {
                pickaxe?.Disarm();
                AfterBoardChange();
            }
            return;
        }

        // An armed pickaxe consumes a tap on any crystal. Shatter refuses an item
        // that is not on the grid, so the queue previews under the board are safe.
        if (item != null && pickaxe != null && pickaxe.IsArmed)
        {
            if (pickaxe.Shatter(item))
                AfterBoardChange();
            return;
        }

        // Otherwise: an empty cell is a placement. An occupied one is a misclick and
        // deliberately costs nothing — there is no penalty move in this game.
        TryPlaceAt(cell);
    }

    // A drag ended. Dropping on an empty cell places there; dropping anywhere else
    // is a change of mind, not a mistake, so the crystal simply goes home.
    void HandleRelease()
    {
        Probe(out _, out Cell cell);

        // Drop the reference first. TryPlace pops the queue, which rebuilds the
        // preview row and pools the very item being carried — holding it across
        // that call would leave a live handle on a recycled crystal.
        _carried = null;

        if (cell != null && !cell.IsOccupied())
        {
            TryPlaceAt(cell);
            return;
        }

        placement?.CancelDrag();
    }

    void TryPlaceAt(Cell cell)
    {
        if (cell == null || cell.IsOccupied() || placement == null) return;

        if (placement.TryPlace(cell))
        {
            ClearHover();          // the cell is occupied now; its tint is not ours to keep
            OnMoveCompleted?.Invoke();
            AfterBoardChange();
        }
    }

    // Light the empty cell under the cursor. Without this, the one action in the
    // game lands on bare background with no indication it is a target at all.
    void UpdateHover()
    {
        Probe(out _, out Cell cell);

        Cell target = (cell != null && !cell.IsOccupied()) ? cell : null;
        if (target == _hoveredCell) return;

        ClearHover();

        if (target != null)
        {
            target.SetHighlight(placeableTint);
            _hoveredCell = target;
        }
    }

    void ClearHover()
    {
        if (_hoveredCell != null)
            _hoveredCell.ClearHighlight();
        _hoveredCell = null;
    }

    // Give back a crystal that is still under the cursor when input is taken away
    // (game over, a freeze, a restart) rather than leaving it stranded there.
    void CancelCarry()
    {
        if (_carried == null) return;
        _carried = null;
        placement?.CancelDrag();
    }

    void OnDisable()
    {
        CancelCarry();
        ClearHover();
    }

    // Anything that changed what is on the board runs the loss check. Placement,
    // shatter and detonation all land here; only placement counts as a "move" for
    // the systems that care, which is why OnMoveCompleted is raised by the caller
    // rather than from in here.
    void AfterBoardChange()
    {
        CheckForLoss();
    }

    // The run ends when there is nowhere left to place. A live bomb or a banked
    // pickaxe charge each still free cells, so neither state is a loss — ending the
    // run with either in hand would make the tool worthless precisely when it is
    // needed.
    void CheckForLoss()
    {
        bool full = gridManager.IsFull();

        if (!full)
        {
            ClearRescuePending();
            return;
        }

        if (bombs != null && bombs.HasArmedBomb())
        {
            // The bomb is already visible and already pulsing; no prompt needed.
            ClearRescuePending();
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

    void ClearRescuePending()
    {
        if (!_rescuePending) return;
        _rescuePending = false;
        GameEvents.RaiseJamRescuePending(false);
    }

    public void ResetState()
    {
        _gameOver = false;
        _inputEnabled = true;
        ClearRescuePending();

        // A fresh run rebuilds the board, so any cell this was tinting is already
        // gone; drop the reference rather than reaching into a destroyed object.
        _carried = null;
        _hoveredCell = null;
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

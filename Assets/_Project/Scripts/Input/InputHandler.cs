using UnityEngine;
using System;

/// <summary>
/// One verb: tap.
///
/// Everything the player can do resolves from where they tapped, and the three
/// meanings never overlap because they need different things under the cursor:
///
///   tap an EMPTY CELL      → place the queue's next crystal there
///   tap an ARMED BOMB      → detonate it
///   tap ANY CRYSTAL        → shatter it, but only while the pickaxe is armed
///
/// The drag-to-merge path this class used to own is gone. Fusion is now automatic
/// on placement, so there is no move that names two crystals, no drag state, no
/// adjacency highlight, and no need to reject a drop.
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

    public event Action OnGameOver;

    /// <summary>Raised after a crystal is placed and its fusion has resolved, before
    /// the loss check runs.</summary>
    public event Action OnMoveCompleted;

    private bool _gameOver;
    private bool _inputEnabled = true;
    private bool _rescuePending;
    private Camera _camera;

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
            HandleTap();
    }

    void HandleTap()
    {
        Vector2 worldPos = GetMouseWorldPos();
        Collider2D[] hits = Physics2D.OverlapPointAll(worldPos);

        Item item = null;
        Cell cell = null;

        // A crystal sits on top of its cell, so a tap on an occupied square hits
        // both. Collect both and let the rules below decide which one the tap meant.
        foreach (Collider2D col in hits)
        {
            if (col == null) continue;
            if (item == null && col.TryGetComponent(out Item hitItem)) item = hitItem;
            if (cell == null && col.TryGetComponent(out Cell hitCell)) cell = hitCell;
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
        if (cell != null && !cell.IsOccupied() && placement != null)
        {
            if (placement.TryPlace(cell))
            {
                OnMoveCompleted?.Invoke();
                AfterBoardChange();
            }
        }
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

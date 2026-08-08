using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// Match-3 swap input, reachable two ways — whichever the player reaches for:
///
///   swipe a gem toward a neighbour   → the two swap
///   tap a gem, then tap a neighbour  → the two swap
///
/// Feedback is the whole job of this class beyond the gesture: the selected gem
/// grows so you can see what you picked (the cell highlight underneath is useless
/// now that a gem covers every cell), and EVERY swap animates. A swap that makes a
/// match slides and commits; a swap that makes nothing slides and bounces straight
/// back, so an illegal move reads as "not that one", not as a dead click. After a
/// few idle seconds a valid move pulses as a hint.
/// </summary>
public class InputHandler : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    public GridManager gridManager;

    [Tooltip("The only thing this class needs to know about the rules: whether a " +
             "swap of two cells would match, and how to commit it. Auto-found at " +
             "startup if the inspector link is missing.")]
    [SerializeField] private PlacementController placement;

    [Header("Feedback")]
    [Tooltip("How much the selected gem grows, so the picked gem is obvious.")]
    [Range(1f, 1.5f)]
    [SerializeField] private float selectedScale = 1.18f;

    [Tooltip("Seconds for a gem to slide one cell during a swap.")]
    [Range(0.05f, 0.4f)]
    [SerializeField] private float swapAnimTime = 0.12f;

    [Tooltip("How far the cursor must travel from the pressed gem, as a fraction of " +
             "one cell, before the gesture counts as a swipe rather than a tap.")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float swipeFraction = 0.35f;

    [Header("Idle hint")]
    [Tooltip("Pulse a valid move after the player has been idle this long. 0 disables it.")]
    [SerializeField] private float idleHintDelay = 4f;

    // Kept so LevelManager's existing subscriptions still bind. OnGameOver never
    // fires in the endless match-3 game — the board reshuffles instead of dead-
    // ending — but the seam is left in place for a future move/time limit.
    public event Action OnGameOver;

    /// <summary>Raised after a swap resolves into a move.</summary>
    public event Action OnMoveCompleted;

    private bool _inputEnabled = true;
    private bool _animating;
    private Camera _camera;

    // The gem waiting for its partner in the tap-tap path, the item shown selected
    // (tracked directly so its scale is always restored on the right object), and
    // where the press that may become a swipe began.
    private Cell _selected;
    private Item _selectedItem;
    private Vector3 _selectedBaseScale = Vector3.one;
    private Vector2 _pressWorld;

    // Idle-hint state: the pulsing pair, their resting scales, and the idle timer.
    private Item _hintA, _hintB;
    private Vector3 _hintABase = Vector3.one, _hintBBase = Vector3.one;
    private float _idleClock;

    void Awake()
    {
        _camera = Camera.main;
        if (_camera == null)
            Debug.LogError("InputHandler: no camera tagged 'MainCamera' found in the scene.");

        // Self-wire the references the swap needs. The scene's serialized links can
        // go stale across a rewrite (the PlacementController link came up null after
        // the match-3 conversion, which silently killed every swap), so fall back to
        // finding them rather than depending on inspector wiring alone.
        if (placement == null)
            placement = FindFirstObjectByType<PlacementController>();
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (placement == null)
            Debug.LogError("InputHandler: no PlacementController in the scene — swaps cannot work.");
    }

    void Update()
    {
        if (!_inputEnabled)
        {
            ClearSelection();
            ClearHint();
            return;
        }

        // Swallow input while a swap is sliding, so a second click cannot interleave
        // with the animation and the logical commit that follows it.
        if (_animating) { ClearHint(); return; }

        if (Input.GetMouseButtonDown(0))
            HandleDown();
        else if (Input.GetMouseButtonUp(0))
            HandleUp();

        UpdateIdleHint();
    }

    void HandleDown()
    {
        _idleClock = 0f;
        ClearHint();
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
        _idleClock = 0f;
        ClearHint();
        ClearSelection();
        if (placement == null || a == null || b == null) return;
        StartCoroutine(SwapRoutine(a, b));
    }

    // Animate the swap, then either commit it (match) or slide it back (no match).
    // Nothing about the board's logical state changes until TrySwap; the slide only
    // moves transforms, so a bounce-back is a pure visual with no state to undo.
    IEnumerator SwapRoutine(Cell a, Cell b)
    {
        Item ia = a.CurrentItem;
        Item ib = b.CurrentItem;
        if (ia == null || ib == null) yield break;

        _animating = true;

        Vector3 pa = a.transform.position;
        Vector3 pb = b.transform.position;

        bool willMatch = placement.WouldMatch(a, b);

        yield return Slide(ia.transform, pa, pb, ib.transform, pb, pa);

        if (willMatch)
        {
            // Commit: TrySwap swaps the cells (snapping the gems to the same spots
            // they just slid to) and resolves the cascade from there.
            if (placement.TrySwap(a, b))
                OnMoveCompleted?.Invoke();
            else
                SnapHome(ia, pa, ib, pb);   // defensive: a race lost the match
        }
        else
        {
            yield return Slide(ia.transform, pb, pa, ib.transform, pa, pb);
            SnapHome(ia, pa, ib, pb);
        }

        _animating = false;
    }

    IEnumerator Slide(Transform t1, Vector3 from1, Vector3 to1, Transform t2, Vector3 from2, Vector3 to2)
    {
        float dur = Mathf.Max(0.01f, swapAnimTime);
        float e = 0f;
        while (e < dur)
        {
            e += Time.deltaTime;
            float k = Mathf.Clamp01(e / dur);
            if (t1 != null) t1.position = Vector3.Lerp(from1, to1, k);
            if (t2 != null) t2.position = Vector3.Lerp(from2, to2, k);
            yield return null;
        }
        if (t1 != null) t1.position = to1;
        if (t2 != null) t2.position = to2;
    }

    static void SnapHome(Item ia, Vector3 pa, Item ib, Vector3 pb)
    {
        if (ia != null) ia.transform.position = pa;
        if (ib != null) ib.transform.position = pb;
    }

    // --- Selection ----------------------------------------------------------

    void Select(Cell cell)
    {
        ClearSelection();
        _selected = cell;
        _selectedItem = cell.CurrentItem;
        if (_selectedItem != null)
        {
            _selectedBaseScale = _selectedItem.transform.localScale;
            _selectedItem.transform.localScale = _selectedBaseScale * selectedScale;
        }
    }

    void ClearSelection()
    {
        if (_selectedItem != null)
            _selectedItem.transform.localScale = _selectedBaseScale;
        _selectedItem = null;
        _selected = null;
    }

    // --- Idle hint ----------------------------------------------------------

    // After the player sits idle, gently pulse one valid move so they always have a
    // way forward. Suppressed while selecting or mid-swap.
    void UpdateIdleHint()
    {
        if (idleHintDelay <= 0f || _selected != null || placement == null)
        {
            ClearHint();
            return;
        }

        _idleClock += Time.deltaTime;
        if (_idleClock < idleHintDelay)
        {
            ClearHint();
            return;
        }

        if (placement.TryFindHintMove(out Cell a, out Cell b) && a.IsOccupied() && b.IsOccupied())
        {
            Item na = a.CurrentItem, nb = b.CurrentItem;
            if (na != _hintA || nb != _hintB)
            {
                ClearHint();
                _hintA = na; _hintB = nb;
                _hintABase = na.transform.localScale;
                _hintBBase = nb.transform.localScale;
            }

            float p = 1f + 0.12f * Mathf.Sin(Time.time * 6f);
            if (_hintA != null) _hintA.transform.localScale = _hintABase * p;
            if (_hintB != null) _hintB.transform.localScale = _hintBBase * p;
        }
        else
        {
            ClearHint();
        }
    }

    void ClearHint()
    {
        if (_hintA != null) _hintA.transform.localScale = _hintABase;
        if (_hintB != null) _hintB.transform.localScale = _hintBBase;
        _hintA = null; _hintB = null;
    }

    void OnDisable()
    {
        StopAllCoroutines();
        _animating = false;
        ClearHint();
        ClearSelection();
    }

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
        StopAllCoroutines();
        _animating = false;
        _inputEnabled = true;
        _selected = null;
        _selectedItem = null;
        _hintA = null;
        _hintB = null;
        _idleClock = 0f;
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

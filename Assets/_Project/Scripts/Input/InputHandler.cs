using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// Match-3 swap input, reachable two ways — whichever the player reaches for:
///
///   swipe a gem toward a neighbour   → the two swap
///   tap a gem, then tap a neighbour  → the two swap
///
/// Feel is the whole job of this class beyond the gesture:
///   • the selected gem breathes (a soft pulse) so the pick is unmissable,
///   • a committed swap slides with a springy overshoot (Ease.OutBack),
///   • an illegal swap leans toward the neighbour, bounces home and shakes its
///     head — "not that one", never a dead click,
///   • after a few idle seconds a valid move pulses as a hint.
///
/// While the board is resolving (PlacementController.IsBusy) input is swallowed,
/// so the player can never interleave with the cascade.
/// </summary>
public class InputHandler : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    public GridManager gridManager;

    [Tooltip("The only thing this class needs to know about the rules: whether a " +
             "swap of two cells would match, and how to commit it. Auto-found at " +
             "startup if the inspector link is missing.")]
    [SerializeField] private PlacementController placement;

    [Header("Feel — selection")]
    [Tooltip("Resting size of the selected gem's pulse.")]
    [Range(1f, 1.5f)]
    [SerializeField] private float selectedScale = 1.14f;

    [Tooltip("How much the selected gem breathes around that size.")]
    [Range(0f, 0.2f)]
    [SerializeField] private float selectedPulse = 0.05f;

    [Header("Feel — swap")]
    [Tooltip("Seconds for a gem to slide one cell during a committed swap.")]
    [Range(0.05f, 0.4f)]
    [SerializeField] private float swapAnimTime = 0.16f;

    [Tooltip("How far a refused swap leans toward the neighbour before bouncing " +
             "home, as a fraction of the distance.")]
    [Range(0.1f, 0.6f)]
    [SerializeField] private float deniedReach = 0.35f;

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

    /// <summary>Raised after a swap commits into a move (the cascade may still be
    /// resolving when this fires).</summary>
    public event Action OnMoveCompleted;

    private bool _inputEnabled = true;
    private bool _animating;
    private Camera _camera;

    // The gem waiting for its partner in the tap-tap path, the item shown selected
    // (tracked directly so its scale is always restored on the right object), and
    // where the press that may become a swipe began.
    private Cell _selected;
    private Item _selectedItem;
    private Vector2 _pressWorld;

    // Idle-hint state: the pulsing pair and the idle timer.
    private Item _hintA, _hintB;
    private float _idleClock;

    void Awake()
    {
        _camera = Camera.main;
        if (_camera == null)
            Debug.LogError("InputHandler: no camera tagged 'MainCamera' found in the scene.");

        // Self-wire the references the swap needs. The scene's serialized links can
        // go stale across a rewrite, so fall back to finding them rather than
        // depending on inspector wiring alone.
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

        // Swallow input while a swap slides or the board cascades, so a click can
        // never interleave with an animation or the logical commit behind it.
        if (_animating || (placement != null && placement.IsBusy))
        {
            _idleClock = 0f;
            ClearHint();
            return;
        }

        if (Input.GetMouseButtonDown(0))
            HandleDown();
        else if (Input.GetMouseButtonUp(0))
            HandleUp();

        AnimateSelection();
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

    // Animate the swap, then either commit it (match) or bounce it back (no match).
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

        if (willMatch)
        {
            // A real move: slide with a springy overshoot and commit.
            yield return Slide(ia, pa, pb, ib, pb, pa, swapAnimTime, Ease.OutBack);

            if (placement.TrySwap(a, b))
                OnMoveCompleted?.Invoke();
            else
                SnapHome(ia, pa, ib, pb);   // defensive: a race lost the match
        }
        else
        {
            // Refused: lean toward the neighbour, bounce home, shake it off.
            Vector3 ta = Vector3.Lerp(pa, pb, deniedReach);
            Vector3 tb = Vector3.Lerp(pb, pa, deniedReach);

            yield return Slide(ia, pa, ta, ib, pb, tb, swapAnimTime * 0.55f, Ease.OutCubic);
            GameEvents.RaiseSwapDenied((pa + pb) * 0.5f);
            yield return Slide(ia, ta, pa, ib, tb, pb, swapAnimTime * 0.65f, Ease.OutCubic);
            yield return HeadShake(ia, pa, (pb - pa).normalized);
            SnapHome(ia, pa, ib, pb);
        }

        _animating = false;
    }

    // Shared two-gem slide with a pluggable easing curve. The pair also puffs up
    // slightly mid-slide so the move reads as an object in hand, not a translation.
    // All scaling multiplies each gem's authored BaseScale — the prefab is not 1.
    IEnumerator Slide(Item i1, Vector3 from1, Vector3 to1,
                      Item i2, Vector3 from2, Vector3 to2,
                      float duration, Func<float, float> ease)
    {
        float dur = Mathf.Max(0.01f, duration);
        float e = 0f;
        while (e < dur)
        {
            e += Time.deltaTime;
            float k = Mathf.Clamp01(e / dur);
            float p = ease(k);
            float puff = 1f + 0.07f * Mathf.Sin(k * Mathf.PI);

            if (i1 != null)
            {
                i1.transform.position = Vector3.LerpUnclamped(from1, to1, p);
                i1.transform.localScale = i1.BaseScale * puff;
            }
            if (i2 != null)
            {
                i2.transform.position = Vector3.LerpUnclamped(from2, to2, p);
                i2.transform.localScale = i2.BaseScale * puff;
            }
            yield return null;
        }
        if (i1 != null) { i1.transform.position = to1; i1.transform.localScale = i1.BaseScale; }
        if (i2 != null) { i2.transform.position = to2; i2.transform.localScale = i2.BaseScale; }
    }

    // A quick decaying wiggle along the axis the player tried to move — the gem
    // literally shakes its head "no".
    IEnumerator HeadShake(Item gem, Vector3 home, Vector3 axis)
    {
        const float dur = 0.14f;
        const float amp = 0.07f;
        float t = 0f;
        while (t < dur)
        {
            if (gem == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            gem.transform.position = home + axis * (Mathf.Sin(k * Mathf.PI * 4f) * amp * (1f - k));
            yield return null;
        }
        if (gem != null) gem.transform.position = home;
    }

    static void SnapHome(Item ia, Vector3 pa, Item ib, Vector3 pb)
    {
        if (ia != null) { ia.transform.position = pa; ia.transform.localScale = ia.BaseScale; }
        if (ib != null) { ib.transform.position = pb; ib.transform.localScale = ib.BaseScale; }
    }

    // --- Selection ----------------------------------------------------------

    void Select(Cell cell)
    {
        ClearSelection();

        // A stone refuses the hand: it shakes its head where it sits and plays
        // the denied thud. Match next to it to break it — that's the mechanic.
        Item item = cell.CurrentItem;
        if (item != null && item.IsStone)
        {
            GameEvents.RaiseSwapDenied(item.transform.position);
            StartCoroutine(HeadShake(item, item.transform.position, Vector3.right));
            return;
        }

        _selected = cell;
        _selectedItem = item;
        if (_selectedItem != null)
            GameEvents.RaiseGemSelected(_selectedItem.transform.position);
    }

    // The selected gem breathes rather than sitting at a fixed enlarged size —
    // motion draws the eye far better than scale alone.
    void AnimateSelection()
    {
        if (_selectedItem == null) return;
        float s = selectedScale + selectedPulse * Mathf.Sin(Time.time * 9f);
        _selectedItem.transform.localScale = _selectedItem.BaseScale * s;
    }

    void ClearSelection()
    {
        if (_selectedItem != null)
            _selectedItem.transform.localScale = _selectedItem.BaseScale;
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
                _hintA = na;
                _hintB = nb;
            }

            float p = 1f + 0.12f * Mathf.Sin(Time.time * 6f);
            if (_hintA != null) _hintA.transform.localScale = _hintA.BaseScale * p;
            if (_hintB != null) _hintB.transform.localScale = _hintB.BaseScale * p;
        }
        else
        {
            ClearHint();
        }
    }

    void ClearHint()
    {
        if (_hintA != null) _hintA.transform.localScale = _hintA.BaseScale;
        if (_hintB != null) _hintB.transform.localScale = _hintB.BaseScale;
        _hintA = null;
        _hintB = null;
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

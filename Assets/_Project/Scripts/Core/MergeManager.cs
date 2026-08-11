using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The match-3 board engine (Candy Crush style), rebuilt around FEEL: nothing on
/// the board ever teleports. A resolve is a staged, animated pipeline —
///
///   pop:    every matched gem swells and bursts (sparks + score fly via events),
///   fall:   the gems above drop into the gaps under gravity and land with a squash,
///   refill: fresh gems enter from ABOVE the board and fall in the same way,
///   repeat: the settled board may hold new runs — each round is a louder combo —
///
/// until stable, then <see cref="EnsurePlayable"/> guarantees a legal swap exists.
///
/// A "colour" is an Item's <see cref="Item.Tier"/>, indices 1..colorCount mapped
/// to distinct looks by <see cref="GemPalette"/>. Gems never change colour in
/// play; they are only cleared and replaced.
///
/// While <see cref="IsResolving"/> is true the input layer refuses new swaps, so
/// the pipeline never interleaves with the player.
/// </summary>
public class MergeManager : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    public GridManager gridManager;

    [Header("Match Rules")]
    [Tooltip("How many distinct gem colours are in play (GemPalette defines 6). " +
             "Fewer colours = more matches and longer cascades; 5 keeps a 6x6 " +
             "board generous without playing itself.")]
    [Range(3, 6)]
    [SerializeField] private int colorCount = 5;

    [Tooltip("Gems in a straight line needed to clear. Three is the classic rule.")]
    [Min(3)]
    [SerializeField] private int minMatch = 3;

    [Header("Feel — pop")]
    [Tooltip("Seconds a matched gem takes to swell and burst.")]
    [SerializeField] private float popTime = 0.24f;

    [Tooltip("How much a matched gem swells before it bursts.")]
    [SerializeField] private float popSwell = 1.32f;

    [Header("Feel — falls")]
    [Tooltip("Downward acceleration of falling gems, world units/s².")]
    [SerializeField] private float fallGravity = 42f;

    [Tooltip("Terminal velocity of falling gems, world units/s.")]
    [SerializeField] private float fallMaxSpeed = 18f;

    [Tooltip("How hard a gem squashes when it lands (0.12 = 12% flatter).")]
    [SerializeField] private float landSquash = 0.14f;
    [SerializeField] private float landSquashTime = 0.11f;

    [Tooltip("Breath between cascade rounds, seconds — lets each combo read.")]
    [SerializeField] private float cascadeBeat = 0.05f;

    /// <summary>How many gem colours are in play — read by the board setup.</summary>
    public int ColorCount => colorCount;

    /// <summary>True while the pop/fall/refill pipeline is running. The input
    /// layer treats this as "hands off the board".</summary>
    public bool IsResolving { get; private set; }

    private int Rows => gridManager != null ? gridManager.rows : 0;
    private int Cols => gridManager != null ? gridManager.cols : 0;

    private int RandomColor() => Random.Range(1, colorCount + 1);

    // The colour sitting in a cell, or 0 for an empty/out-of-range one. Colours are
    // >= 1, so 0 is an unambiguous "nothing here".
    private int ColorAt(int row, int col)
    {
        Cell cell = gridManager != null ? gridManager.GetCell(row, col) : null;
        return (cell != null && cell.IsOccupied()) ? cell.CurrentItem.Tier : 0;
    }

    // ----- Public board operations ------------------------------------------

    /// <summary>Swap the gems held by two cells — LOGICAL state only; whatever is
    /// animating the transforms owns the visuals. Also used internally to test a
    /// tentative swap and undo it, so it must be an exact inverse of itself.</summary>
    public void SwapItems(Cell a, Cell b)
    {
        if (a == null || b == null) return;
        Item ia = a.CurrentItem;
        Item ib = b.CurrentItem;
        a.RemoveItem();
        b.RemoveItem();
        if (ib != null) a.PlaceItem(ib, snap: false);
        if (ia != null) b.PlaceItem(ia, snap: false);
    }

    /// <summary>True when the board currently holds at least one line of
    /// <see cref="minMatch"/>+ identical gems.</summary>
    public bool HasAnyMatch() => FindMatches().Count > 0;

    /// <summary>Kick off the animated resolve pipeline. Safe to call when one is
    /// already running (the running one will consume any new matches).</summary>
    public void BeginResolve()
    {
        if (!IsResolving)
            StartCoroutine(ResolveRoutine());
    }

    /// <summary>Abort a resolve mid-flight — called when the board is about to be
    /// torn down (restart) so a cascade never animates gems that no longer exist.</summary>
    public void CancelResolve()
    {
        StopAllCoroutines();
        IsResolving = false;
    }

    /// <summary>
    /// Guarantee the board has at least one swap that would make a match. If it
    /// does not, reshuffle the existing gems in place until it does — clearing any
    /// accidental matches the shuffle creates (instantly; this runs at board setup
    /// or between moves, never mid-animation). The guard stops a pathological
    /// board from spinning forever.
    /// </summary>
    public void EnsurePlayable()
    {
        int guard = 0;
        while (!HasPossibleMove() && guard++ < 200)
        {
            ReshuffleColors();
            while (ClearMatchesInstant() > 0) { }
        }
    }

    // ----- The animated pipeline --------------------------------------------

    private IEnumerator ResolveRoutine()
    {
        IsResolving = true;
        int combo = 0;

        while (true)
        {
            HashSet<Cell> matches = FindMatches();
            if (matches.Count == 0) break;
            combo++;

            // Read the group's worth and centre BEFORE anything despawns.
            int points = 0;
            Vector3 centre = Vector3.zero;
            foreach (Cell cell in matches)
            {
                Item gem = cell.CurrentItem;
                points += (gem != null && gem.GemData != null) ? gem.GemData.scoreValue : 10;
                centre += cell.transform.position;
            }
            centre /= matches.Count;

            // One announcement per group: audio pops once (pitch climbs with
            // combo), the HUD flashes, the score text flies from the centre.
            GameEvents.RaiseMatchResolved(combo, matches.Count, points, centre);

            yield return PopMatches(matches);
            yield return DropAndRefill();

            if (cascadeBeat > 0f)
                yield return new WaitForSeconds(cascadeBeat);
        }

        EnsurePlayable();
        IsResolving = false;
    }

    // Swell every matched gem, then collapse it to nothing, then despawn — the
    // burst the eye tracks. TileMerged fires per gem at the burst so score and
    // sparks land exactly when the gem vanishes.
    private IEnumerator PopMatches(HashSet<Cell> matches)
    {
        var items = new List<Item>(matches.Count);
        foreach (Cell cell in matches)
            if (cell.CurrentItem != null)
                items.Add(cell.CurrentItem);

        float t = 0f;
        float dur = Mathf.Max(0.01f, popTime);
        const float swellPortion = 0.4f;   // first 40% swells, the rest collapses
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float scale = k < swellPortion
                ? Mathf.Lerp(1f, popSwell, Ease.OutCubic(k / swellPortion))
                : Mathf.Lerp(popSwell, 0f, Ease.InQuad((k - swellPortion) / (1f - swellPortion)));

            foreach (Item gem in items)
                if (gem != null)
                    gem.transform.localScale = gem.BaseScale * scale;

            yield return null;
        }

        foreach (Cell cell in matches)
        {
            if (!cell.IsOccupied()) continue;
            Item doomed = cell.CurrentItem;
            GameEvents.RaiseTileMerged(doomed, cell);
            cell.RemoveItem();
            gridManager.DespawnItem(doomed);   // ResetForPool restores scale to 1
        }
    }

    // One gem in flight: where it's going and how fast it's currently falling.
    private struct Faller
    {
        public Item item;
        public float targetY;
        public float speed;
        public bool landed;
    }

    // Compact every column downward (logical move first, snap: false), spawn the
    // refill stacked ABOVE the board, then animate every mover falling under one
    // shared gravity until each lands on its own cell with a squash.
    private IEnumerator DropAndRefill()
    {
        var fallers = new List<Faller>();
        float topY = gridManager.TopEdgeY;
        float cellSize = gridManager.cellSize;

        for (int c = 0; c < Cols; c++)
        {
            int writeRow = 0;
            for (int r = 0; r < Rows; r++)
            {
                Cell cell = gridManager.GetCell(r, c);
                if (cell == null || !cell.IsOccupied()) continue;

                if (r != writeRow)
                {
                    Cell dest = gridManager.GetCell(writeRow, c);
                    Item falling = cell.CurrentItem;
                    cell.RemoveItem();
                    dest.PlaceItem(falling, snap: false);
                    fallers.Add(new Faller { item = falling, targetY = dest.transform.position.y });
                }
                writeRow++;
            }

            // Refill enters from above the board, stacked so the column keeps its
            // spacing while it falls in — no gem ever appears from nowhere.
            for (int k = 0; writeRow + k < Rows; k++)
            {
                Cell cell = gridManager.GetCell(writeRow + k, c);
                Vector3 target = cell.transform.position;
                var start = new Vector3(target.x, topY + cellSize * (k + 1), target.z);
                Item spawned = gridManager.SpawnItem(cell, RandomColor(), GemFamily.Standard, start);
                if (spawned != null)
                    fallers.Add(new Faller { item = spawned, targetY = target.y });
            }
        }

        if (fallers.Count == 0) yield break;

        // All movers share one gravity; each stops at its own floor.
        bool anyAirborne = true;
        while (anyAirborne)
        {
            anyAirborne = false;
            float dt = Time.deltaTime;

            for (int i = 0; i < fallers.Count; i++)
            {
                Faller f = fallers[i];
                if (f.landed || f.item == null) continue;

                f.speed = Mathf.Min(f.speed + fallGravity * dt, fallMaxSpeed);
                Vector3 pos = f.item.transform.position;
                pos.y -= f.speed * dt;

                if (pos.y <= f.targetY)
                {
                    pos.y = f.targetY;
                    f.landed = true;
                    StartCoroutine(LandSquash(f.item));
                }

                f.item.transform.position = pos;
                fallers[i] = f;
                anyAirborne |= !f.landed;
            }

            yield return null;
        }
    }

    // The landing "thud" the eye believes: squash flat, spring back. Runs
    // per-gem so late landers squash on their own beat. Scales are relative to
    // the gem's authored BaseScale — the prefab is not scale 1.
    private IEnumerator LandSquash(Item gem)
    {
        float t = 0f;
        float dur = Mathf.Max(0.01f, landSquashTime);
        while (t < dur)
        {
            if (gem == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);   // 0 → 1 → 0
            Vector3 b = gem.BaseScale;
            gem.transform.localScale = new Vector3(b.x * (1f + landSquash * k), b.y * (1f - landSquash * k), b.z);
            yield return null;
        }
        if (gem != null) gem.transform.localScale = gem.BaseScale;
    }

    // ----- Match detection ---------------------------------------------------

    // Every cell that is part of a horizontal or vertical run of minMatch+ same
    // colour. A cell shared by an intersecting row-run and column-run appears once
    // (it is a HashSet), which is exactly the "L / T shapes clear both arms" rule.
    private HashSet<Cell> FindMatches()
    {
        var matched = new HashSet<Cell>();

        // Horizontal runs.
        for (int r = 0; r < Rows; r++)
        {
            int c = 0;
            while (c < Cols)
            {
                int color = ColorAt(r, c);
                int run = 1;
                if (color > 0)
                    while (c + run < Cols && ColorAt(r, c + run) == color) run++;

                if (color > 0 && run >= minMatch)
                    for (int k = 0; k < run; k++)
                        matched.Add(gridManager.GetCell(r, c + k));

                c += Mathf.Max(run, 1);
            }
        }

        // Vertical runs.
        for (int c = 0; c < Cols; c++)
        {
            int r = 0;
            while (r < Rows)
            {
                int color = ColorAt(r, c);
                int run = 1;
                if (color > 0)
                    while (r + run < Rows && ColorAt(r + run, c) == color) run++;

                if (color > 0 && run >= minMatch)
                    for (int k = 0; k < run; k++)
                        matched.Add(gridManager.GetCell(r + k, c));

                r += Mathf.Max(run, 1);
            }
        }

        return matched;
    }

    // Instant clear/fall/refill pass with NO animation and NO events — only for
    // EnsurePlayable's shuffle cleanup, where the board must simply become legal.
    private int ClearMatchesInstant()
    {
        HashSet<Cell> matches = FindMatches();
        if (matches.Count == 0) return 0;

        foreach (Cell cell in matches)
        {
            if (!cell.IsOccupied()) continue;
            Item doomed = cell.CurrentItem;
            cell.RemoveItem();
            gridManager.DespawnItem(doomed);
        }

        for (int c = 0; c < Cols; c++)
        {
            int writeRow = 0;
            for (int r = 0; r < Rows; r++)
            {
                Cell cell = gridManager.GetCell(r, c);
                if (cell == null || !cell.IsOccupied()) continue;

                if (r != writeRow)
                {
                    Cell dest = gridManager.GetCell(writeRow, c);
                    Item falling = cell.CurrentItem;
                    cell.RemoveItem();
                    dest.PlaceItem(falling);   // snap — this path is invisible
                }
                writeRow++;
            }

            for (int r = writeRow; r < Rows; r++)
                gridManager.SpawnItem(gridManager.GetCell(r, c), RandomColor(), GemFamily.Standard);
        }

        return matches.Count;
    }

    // ----- Playability -------------------------------------------------------

    // Is there any single adjacent swap that would create a match? Only right and
    // up neighbours are tested per cell — every adjacency is covered exactly once
    // that way (my right is my neighbour's left).
    private bool HasPossibleMove()
    {
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Cols; c++)
            {
                Cell here = gridManager.GetCell(r, c);
                if (WouldSwapMatch(here, gridManager.GetCell(r, c + 1))) return true;
                if (WouldSwapMatch(here, gridManager.GetCell(r + 1, c))) return true;
            }
        }
        return false;
    }

    /// <summary>Find the first swap on the board that would make a match, as two
    /// adjacent cells. Returns false (with null outs) when the board has no move —
    /// which should not happen after EnsurePlayable, but the input hint asks anyway.</summary>
    public bool TryFindHintMove(out Cell a, out Cell b)
    {
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Cols; c++)
            {
                Cell here = gridManager.GetCell(r, c);
                Cell right = gridManager.GetCell(r, c + 1);
                if (WouldSwapMatch(here, right)) { a = here; b = right; return true; }
                Cell up = gridManager.GetCell(r + 1, c);
                if (WouldSwapMatch(here, up)) { a = here; b = up; return true; }
            }
        }
        a = null; b = null;
        return false;
    }

    /// <summary>Would swapping these two adjacent gems create a match? A pure query:
    /// it swaps, tests, and always swaps back, leaving the board unchanged (and the
    /// transforms untouched — SwapItems is logical-only).</summary>
    public bool WouldSwapMatch(Cell a, Cell b)
    {
        if (a == null || b == null || !a.IsOccupied() || !b.IsOccupied()) return false;
        SwapItems(a, b);
        bool made = HasAnyMatch();
        SwapItems(a, b);
        return made;
    }

    // Reassign a random colour to every gem in place (no despawn/spawn). Item.Setup
    // re-colours and re-skins the existing pooled instance.
    private void ReshuffleColors()
    {
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
            {
                Cell cell = gridManager.GetCell(r, c);
                if (cell != null && cell.IsOccupied())
                    cell.CurrentItem.Setup(RandomColor(), GemFamily.Standard);
            }
    }

    void OnDisable()
    {
        // A torn-down component must not leave the board frozen mid-pipeline.
        StopAllCoroutines();
        IsResolving = false;
    }
}

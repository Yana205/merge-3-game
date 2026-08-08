using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The match-3 board engine (Candy Crush style).
///
/// A "colour" is just an Item's <see cref="Item.Tier"/> — the game uses tiers 1..N
/// of the Standard gem ladder as N distinct, non-mergeable colours. Nothing ever
/// changes an item's tier during play; gems are only cleared and replaced.
///
/// The rule is the classic one: any straight run of <see cref="minMatch"/> or more
/// same-colour gems in a row OR a column clears. Cleared gems are removed, the gems
/// above fall to fill the gaps, and fresh random gems drop in from the top. That
/// settled board can contain new runs, so clearing cascades until the board is
/// stable — and then <see cref="EnsurePlayable"/> guarantees at least one legal
/// swap still exists, reshuffling if the board has dead-ended.
///
/// This replaces the old place-and-fuse resolver. There is no ResolveAt / connected
/// group any more; the board is always full and the only move is a swap.
/// </summary>
public class MergeManager : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    public GridManager gridManager;

    [Header("Match Rules")]
    [Tooltip("How many distinct gem colours are in play. Uses tiers 1..N of the " +
             "Standard gem ladder, so keep this at or below the number of tiers " +
             "configured in GemConfig (7).")]
    [Range(3, 7)]
    [SerializeField] private int colorCount = 6;

    [Tooltip("Gems in a straight line needed to clear. Three is the classic rule.")]
    [Min(3)]
    [SerializeField] private int minMatch = 3;

    /// <summary>How many gem colours are in play — read by the board setup.</summary>
    public int ColorCount => colorCount;

    private int Rows => gridManager != null ? gridManager.rows : 0;
    private int Cols => gridManager != null ? gridManager.cols : 0;

    private int RandomColor() => Random.Range(1, colorCount + 1);

    // The colour sitting in a cell, or 0 for an empty/out-of-range one. Colours are
    // >= 1 (they are gem tiers), so 0 is an unambiguous "nothing here".
    private int ColorAt(int row, int col)
    {
        Cell cell = gridManager != null ? gridManager.GetCell(row, col) : null;
        return (cell != null && cell.IsOccupied()) ? cell.CurrentItem.Tier : 0;
    }

    // ----- Public board operations (called by PlacementController / setup) ----

    /// <summary>Swap the gems held by two cells. Also used internally to test a
    /// tentative swap and then undo it, so it must be an exact inverse of itself.</summary>
    public void SwapItems(Cell a, Cell b)
    {
        if (a == null || b == null) return;
        Item ia = a.CurrentItem;
        Item ib = b.CurrentItem;
        a.RemoveItem();
        b.RemoveItem();
        if (ib != null) a.PlaceItem(ib);
        if (ia != null) b.PlaceItem(ia);
    }

    /// <summary>True when the board currently holds at least one line of
    /// <see cref="minMatch"/>+ identical gems.</summary>
    public bool HasAnyMatch() => FindMatches().Count > 0;

    /// <summary>
    /// Clear every match on the board and let it cascade until stable, then make
    /// sure a legal move still exists. Returns the total number of gems cleared.
    /// </summary>
    public int ResolveBoard()
    {
        int total = 0;
        int step;
        while ((step = CollapseStep()) > 0)
            total += step;

        EnsurePlayable();
        return total;
    }

    /// <summary>
    /// Guarantee the board has at least one swap that would make a match. If it does
    /// not, reshuffle the existing gems until it does — clearing any accidental
    /// matches the shuffle creates. The guard stops a pathological board (too few
    /// colours) from spinning forever.
    /// </summary>
    public void EnsurePlayable()
    {
        int guard = 0;
        while (!HasPossibleMove() && guard++ < 200)
        {
            ReshuffleColors();
            while (CollapseStep() > 0) { }
        }
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

    // One clear/fall/refill pass. Returns how many gems were cleared, or 0 when
    // the board held no match (which ends the cascade in ResolveBoard).
    private int CollapseStep()
    {
        HashSet<Cell> matches = FindMatches();
        if (matches.Count == 0) return 0;

        foreach (Cell cell in matches)
            ClearCell(cell);

        ApplyGravityAndRefill();
        return matches.Count;
    }

    // Announce the clear over the bus BEFORE despawning, so ScoreController can read
    // the gem's score value and JuiceDirector/AudioDirector can fire at its position
    // and colour. Reusing TileMerged keeps every existing observer working — a match
    // clear is the merge-3 game's "a gem resolved" moment.
    private void ClearCell(Cell cell)
    {
        if (cell == null || !cell.IsOccupied()) return;
        Item doomed = cell.CurrentItem;
        GameEvents.RaiseTileMerged(doomed, cell);
        cell.RemoveItem();
        gridManager.DespawnItem(doomed);
    }

    // Gravity is toward row 0 (the bottom — CreateGrid puts row 0 at the lowest y).
    // Each column is compacted downward, then the gaps left at the top are filled
    // with fresh random gems.
    private void ApplyGravityAndRefill()
    {
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
                    dest.PlaceItem(falling);
                }
                writeRow++;
            }

            for (int r = writeRow; r < Rows; r++)
                gridManager.SpawnItem(gridManager.GetCell(r, c), RandomColor(), GemFamily.Standard);
        }
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
                if (SwapMakesMatch(here, gridManager.GetCell(r, c + 1))) return true;
                if (SwapMakesMatch(here, gridManager.GetCell(r + 1, c))) return true;
            }
        }
        return false;
    }

    // Tentatively swap, test, and always swap back — a pure query with no lasting
    // effect on the board.
    private bool SwapMakesMatch(Cell a, Cell b)
    {
        if (a == null || b == null || !a.IsOccupied() || !b.IsOccupied()) return false;
        SwapItems(a, b);
        bool made = HasAnyMatch();
        SwapItems(a, b);
        return made;
    }

    // Reassign a random colour to every gem in place (no despawn/spawn). Item.Setup
    // re-tiers and re-skins the existing pooled instance.
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
}

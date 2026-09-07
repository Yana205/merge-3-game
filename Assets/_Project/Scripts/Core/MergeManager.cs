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
/// play; they are only cleared and replaced. How many colours are in play is
/// pushed in by <see cref="StageDirector"/> as the run progresses.
///
/// Specials: a run of four leaves a Cross gem behind, a run of five a Prism.
/// A special caught in a match (or in another special's blast) goes off and
/// widens the pop set; a Prism swapped with any gem clears that gem's colour.
///
/// While <see cref="IsResolving"/> is true the input layer refuses new swaps, so
/// the pipeline never interleaves with the player.
/// </summary>
public class MergeManager : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    public GridManager gridManager;

    [Header("Match Rules")]
    [Tooltip("How many distinct gem colours are in play right now (GemPalette " +
             "defines 6). StageDirector raises this as the run progresses: fewer " +
             "colours = more matches and longer cascades.")]
    [Range(3, 6)]
    [SerializeField] private int colorCount = 4;

    [Tooltip("Gems in a straight line needed to clear. Three is the classic rule.")]
    [Min(3)]
    [SerializeField] private int minMatch = 3;

    [Header("Specials")]
    [Tooltip("Bonus points when a Cross gem goes off.")]
    [SerializeField] private int crossBonus = 60;

    [Tooltip("Bonus points when a Prism goes off.")]
    [SerializeField] private int prismBonus = 200;

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

    [Header("Stones (eye crystals)")]
    [Tooltip("Chance that one refill gem enters as a stone blocker instead. " +
             "StageDirector raises this as the run progresses.")]
    [Range(0f, 0.3f)]
    [SerializeField] private float stoneChance = 0.05f;

    [Tooltip("Never more than this many stones squatting the board at once.")]
    [SerializeField] private int maxStones = 3;

    [Tooltip("Hits a fresh stone takes to shatter (2 = crack, then break).")]
    [SerializeField] private int stoneHp = 2;

    /// <summary>How many gem colours are in play — read by the board setup.</summary>
    public int ColorCount => colorCount;

    /// <summary>True while the pop/fall/refill pipeline is running. The input
    /// layer treats this as "hands off the board".</summary>
    public bool IsResolving { get; private set; }

    private int Rows => gridManager != null ? gridManager.rows : 0;
    private int Cols => gridManager != null ? gridManager.cols : 0;

    private int RandomColor() => Random.Range(1, colorCount + 1);

    // The two cells of the player's last swap, so a match of four/five leaves its
    // special exactly where the player put the gem — the move reads as "I made it".
    private Cell _swapA, _swapB;

    // A Prism swap does not need a line: the resolve's first round clears every
    // gem of this colour (0 = every colour, for Prism + Prism) from this cell.
    private Cell _pendingPrism;
    private int _pendingPrismColor;
    private bool _prismPending;

    // One straight line of minMatch+ identical gems.
    private struct Run
    {
        public List<Cell> cells;
        public int color;
    }

    // What a special did when it went off, kept until the pop so juice can draw
    // the beams before the victims vanish.
    private struct Blast
    {
        public SpecialKind kind;
        public Vector3 origin;
        public Color colour;
        public List<Cell> cells;
    }

    // The colour sitting in a cell, or 0 for an empty/out-of-range one. Colours are
    // >= 1, so 0 is an unambiguous "nothing here".
    private int ColorAt(int row, int col)
    {
        Cell cell = gridManager != null ? gridManager.GetCell(row, col) : null;
        return (cell != null && cell.IsOccupied()) ? cell.CurrentItem.Tier : 0;
    }

    // ----- Progression hooks -------------------------------------------------

    /// <summary>Set how many palette colours refills draw from. Existing gems keep
    /// their colours — the new one simply starts arriving.</summary>
    public void SetActiveColors(int count)
    {
        colorCount = Mathf.Clamp(count, 3, GemPalette.Count);
    }

    /// <summary>Tune how often stones arrive and how tough they are.</summary>
    public void SetStonePressure(float chance, int maxOnBoard, int hp)
    {
        stoneChance = Mathf.Clamp(chance, 0f, 0.3f);
        maxStones = Mathf.Max(0, maxOnBoard);
        stoneHp = Mathf.Max(1, hp);
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

    /// <summary>Remember the player's committed swap so the next resolve can place
    /// any special it earns on the gem the player actually moved.</summary>
    public void NoteSwap(Cell a, Cell b)
    {
        _swapA = a;
        _swapB = b;
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

    /// <summary>A Prism was swapped: fire it from <paramref name="prismCell"/> at
    /// every gem of <paramref name="colour"/> (0 = all colours), then resolve.</summary>
    public void BeginPrismResolve(Cell prismCell, int colour)
    {
        _pendingPrism = prismCell;
        _pendingPrismColor = colour;
        _prismPending = prismCell != null;
        BeginResolve();
    }

    /// <summary>Abort a resolve mid-flight — called when the board is about to be
    /// torn down (restart) so a cascade never animates gems that no longer exist.</summary>
    public void CancelResolve()
    {
        StopAllCoroutines();
        IsResolving = false;
        _prismPending = false;
        _swapA = _swapB = null;
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
            List<Run> runs = FindRuns();
            var pop = new HashSet<Cell>();
            var stoneHits = new HashSet<Cell>();
            var blasts = new List<Blast>();
            var creations = new List<(Cell cell, SpecialKind kind)>();
            int bonus = 0;

            // A swapped Prism opens the round with no line at all.
            if (_prismPending)
            {
                _prismPending = false;
                Cell prismCell = _pendingPrism;
                if (prismCell != null && prismCell.IsOccupied())
                {
                    pop.Add(prismCell);
                    bonus += Fire(prismCell, _pendingPrismColor, pop, stoneHits, blasts);
                }
            }

            foreach (Run run in runs)
            {
                pop.UnionWith(run.cells);

                // Four leaves a Cross, five or more a Prism — on the gem the
                // player moved when it is part of the run, else the run's middle.
                if (run.cells.Count >= 5)
                    creations.Add((OriginOf(run), SpecialKind.Prism));
                else if (run.cells.Count == 4)
                    creations.Add((OriginOf(run), SpecialKind.Cross));
            }

            if (pop.Count == 0) break;
            combo++;

            // Any special caught in the pop goes off, and anything IT catches too.
            var queue = new Queue<Cell>();
            foreach (Cell cell in pop)
                if (cell.IsOccupied() && cell.CurrentItem.IsSpecial) queue.Enqueue(cell);
            var firedCells = new HashSet<Cell>();
            while (queue.Count > 0)
            {
                Cell cell = queue.Dequeue();
                if (!firedCells.Add(cell)) continue;
                Item gem = cell.CurrentItem;
                if (gem == null) continue;
                int before = pop.Count;
                bonus += Fire(cell, gem.Tier, pop, stoneHits, blasts);
                // Newly added cells may hold specials of their own — chain them.
                foreach (Cell added in pop)
                    if (added.IsOccupied() && added.CurrentItem.IsSpecial && !firedCells.Contains(added))
                        queue.Enqueue(added);
            }

            // The gem that becomes a special survives the pop — it IS the reward.
            var survivors = new List<(Cell, SpecialKind)>();
            foreach ((Cell cell, SpecialKind kind) in creations)
            {
                if (cell == null || !cell.IsOccupied() || firedCells.Contains(cell)) continue;
                pop.Remove(cell);
                survivors.Add((cell, kind));
            }

            // Read the group's worth and centre BEFORE anything despawns.
            int points = bonus;
            Vector3 centre = Vector3.zero;
            var colourVotes = new Dictionary<int, int>();
            foreach (Cell cell in pop)
            {
                Item gem = cell.CurrentItem;
                points += (gem != null && gem.GemData != null) ? gem.GemData.scoreValue : 10;
                centre += cell.transform.position;
                if (gem != null)
                    colourVotes[gem.Tier] = colourVotes.TryGetValue(gem.Tier, out int n) ? n + 1 : 1;
            }
            centre /= Mathf.Max(1, pop.Count);
            Color groupColour = DominantColour(colourVotes);

            // One announcement per group: audio pops once (pitch climbs with
            // combo), the HUD flashes, the score text flies from the centre.
            GameEvents.RaiseMatchResolved(combo, pop.Count, points, centre, groupColour);
            if (bonus > 0)
                GameEvents.RaiseBonusScore(bonus, centre);
            foreach (Blast blast in blasts)
                GameEvents.RaiseSpecialFired(blast.kind, blast.origin, blast.colour, blast.cells);
            foreach ((Cell cell, SpecialKind kind) in survivors)
            {
                cell.CurrentItem.SetSpecial(kind);
                GameEvents.RaiseSpecialCreated(cell.CurrentItem, cell);
            }

            // Only the first round belongs to the player's swap.
            _swapA = _swapB = null;

            yield return PopMatches(pop);
            yield return DamageStones(pop, stoneHits);
            yield return DropAndRefill();

            if (cascadeBeat > 0f)
                yield return new WaitForSeconds(cascadeBeat);
        }

        EnsurePlayable();
        IsResolving = false;
    }

    // Set a special off: widen the pop set with everything its blast covers.
    // Stones in the path are hit rather than popped. Returns the bonus earned.
    private int Fire(Cell cell, int colour, HashSet<Cell> pop, HashSet<Cell> stoneHits, List<Blast> blasts)
    {
        Item gem = cell.CurrentItem;
        if (gem == null || !gem.IsSpecial) return 0;

        var covered = new List<Cell>();
        if (gem.Special == SpecialKind.Cross)
        {
            for (int c = 0; c < Cols; c++) Cover(gridManager.GetCell(cell.row, c), pop, stoneHits, covered);
            for (int r = 0; r < Rows; r++) Cover(gridManager.GetCell(r, cell.col), pop, stoneHits, covered);
        }
        else
        {
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    Cell target = gridManager.GetCell(r, c);
                    if (target == null || !target.IsOccupied()) continue;
                    Item t = target.CurrentItem;
                    if (t.IsStone) continue;
                    if (colour == 0 || t.Tier == colour || target == cell)
                        Cover(target, pop, stoneHits, covered);
                }
        }

        blasts.Add(new Blast
        {
            kind = gem.Special,
            origin = cell.transform.position,
            colour = colour == 0 ? Color.white : GemPalette.ColorFor(colour),
            cells = covered,
        });
        return gem.Special == SpecialKind.Prism ? prismBonus : crossBonus;
    }

    private static void Cover(Cell target, HashSet<Cell> pop, HashSet<Cell> stoneHits, List<Cell> covered)
    {
        if (target == null || !target.IsOccupied()) return;
        covered.Add(target);
        if (target.CurrentItem.IsStone) stoneHits.Add(target);
        else pop.Add(target);
    }

    // Where a run's special appears: the gem the player just moved if it is in the
    // run, otherwise the run's middle. Never on a gem that is already special.
    private Cell OriginOf(Run run)
    {
        if (_swapA != null && run.cells.Contains(_swapA) && !IsSpecialCell(_swapA)) return _swapA;
        if (_swapB != null && run.cells.Contains(_swapB) && !IsSpecialCell(_swapB)) return _swapB;
        Cell middle = run.cells[run.cells.Count / 2];
        if (!IsSpecialCell(middle)) return middle;
        foreach (Cell cell in run.cells)
            if (!IsSpecialCell(cell)) return cell;
        return null;
    }

    private static bool IsSpecialCell(Cell cell)
        => cell != null && cell.IsOccupied() && cell.CurrentItem.IsSpecial;

    private static Color DominantColour(Dictionary<int, int> votes)
    {
        int best = 0, bestCount = -1;
        foreach (KeyValuePair<int, int> kv in votes)
            if (kv.Key > 0 && kv.Value > bestCount) { best = kv.Key; bestCount = kv.Value; }
        return best > 0 ? GemPalette.ColorFor(best) : new Color(1f, 0.92f, 0.55f);
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

    // ----- Stones ------------------------------------------------------------

    /// <summary>How the input layer asks "is this a blocker?" without knowing the rules.</summary>
    public static bool IsStoneCell(Cell cell)
        => cell != null && cell.IsOccupied() && cell.CurrentItem.IsStone;

    private int CountStones()
    {
        int n = 0;
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
                if (IsStoneCell(gridManager.GetCell(r, c))) n++;
        return n;
    }

    // Every stone orthogonally adjacent to this round's matches takes ONE hit —
    // a stone bordering two cleared runs still cracks once per round, so breaking
    // one is always a deliberate two-move job (or one move plus a lucky cascade).
    // Stones caught in a special's blast are hit directly.
    private IEnumerator DamageStones(HashSet<Cell> matches, HashSet<Cell> directHits)
    {
        var hit = new HashSet<Cell>(directHits);
        var steps = new (int dr, int dc)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        foreach (Cell cell in matches)
            foreach ((int dr, int dc) in steps)
            {
                Cell n = gridManager.GetCell(cell.row + dr, cell.col + dc);
                if (IsStoneCell(n)) hit.Add(n);
            }

        if (hit.Count == 0) yield break;

        var breaking = new List<Cell>();
        foreach (Cell cell in hit)
        {
            if (!IsStoneCell(cell)) continue;
            Item stone = cell.CurrentItem;
            if (stone.DamageStone())
            {
                breaking.Add(cell);
            }
            else
            {
                // Survived: the eye just went dark. Announce, and flinch so the
                // hit reads even if you missed the sprite swap.
                GameEvents.RaiseStoneDamaged(stone, cell);
                StartCoroutine(StoneFlinch(stone));
            }
        }

        if (breaking.Count == 0) yield break;

        // Shattering stones pop like gems — swell, burst, gone — and their cells
        // empty BEFORE gravity runs, so the break visibly opens a hole.
        var items = new List<Item>(breaking.Count);
        foreach (Cell cell in breaking)
            items.Add(cell.CurrentItem);

        float t = 0f;
        float dur = Mathf.Max(0.01f, popTime);
        const float swellPortion = 0.4f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float scale = k < swellPortion
                ? Mathf.Lerp(1f, popSwell, Ease.OutCubic(k / swellPortion))
                : Mathf.Lerp(popSwell, 0f, Ease.InQuad((k - swellPortion) / (1f - swellPortion)));
            foreach (Item stone in items)
                if (stone != null)
                    stone.transform.localScale = stone.BaseScale * scale;
            yield return null;
        }

        foreach (Cell cell in breaking)
        {
            if (!cell.IsOccupied()) continue;
            Item doomed = cell.CurrentItem;
            GameEvents.RaiseStoneBroken(doomed, cell);
            cell.RemoveItem();
            gridManager.DespawnItem(doomed);
        }
    }

    // A quick hurt-wiggle for a stone that took a hit and held.
    private IEnumerator StoneFlinch(Item stone)
    {
        const float dur = 0.18f;
        float t = 0f;
        Vector3 home = stone != null ? stone.transform.position : Vector3.zero;
        while (t < dur)
        {
            if (stone == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            stone.transform.position = home + Vector3.right * (Mathf.Sin(k * Mathf.PI * 5f) * 0.06f * (1f - k));
            yield return null;
        }
        if (stone != null) stone.transform.position = home;
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
                {
                    // Sometimes a refill gem is a stone instead — the eye crystal
                    // rides the same drop so it arrives like everything else.
                    if (Random.value < stoneChance && CountStones() < maxStones)
                        spawned.SetupStone(stoneHp);
                    fallers.Add(new Faller { item = spawned, targetY = target.y });
                }
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

    // Every horizontal or vertical run of minMatch+ same colour, as separate runs
    // (length matters: four makes a Cross, five a Prism). A cell shared by a row-run
    // and a column-run is in both — which is exactly the "L / T shapes clear both
    // arms" rule once the runs are unioned.
    private List<Run> FindRuns()
    {
        var runs = new List<Run>();

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
                {
                    var cells = new List<Cell>(run);
                    for (int k = 0; k < run; k++) cells.Add(gridManager.GetCell(r, c + k));
                    runs.Add(new Run { cells = cells, color = color });
                }

                c += Mathf.Max(run, 1);
            }
        }

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
                {
                    var cells = new List<Cell>(run);
                    for (int k = 0; k < run; k++) cells.Add(gridManager.GetCell(r + k, c));
                    runs.Add(new Run { cells = cells, color = color });
                }

                r += Mathf.Max(run, 1);
            }
        }

        return runs;
    }

    private HashSet<Cell> FindMatches()
    {
        var matched = new HashSet<Cell>();
        foreach (Run run in FindRuns())
            matched.UnionWith(run.cells);
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
    /// transforms untouched — SwapItems is logical-only). A Prism swaps with
    /// anything that is not a stone.</summary>
    public bool WouldSwapMatch(Cell a, Cell b)
    {
        if (a == null || b == null || !a.IsOccupied() || !b.IsOccupied()) return false;
        // Stones don't swap — they are furniture until something breaks them.
        if (a.CurrentItem.IsStone || b.CurrentItem.IsStone) return false;
        if (a.CurrentItem.Special == SpecialKind.Prism || b.CurrentItem.Special == SpecialKind.Prism) return true;
        SwapItems(a, b);
        bool made = HasAnyMatch();
        SwapItems(a, b);
        return made;
    }

    // Reassign a random colour to every gem in place (no despawn/spawn). Item.Setup
    // re-colours and re-skins the existing pooled instance. Stones keep their
    // places — a reshuffle rearranges the gems around the furniture.
    private void ReshuffleColors()
    {
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
            {
                Cell cell = gridManager.GetCell(r, c);
                if (cell != null && cell.IsOccupied() && !cell.CurrentItem.IsStone)
                    cell.CurrentItem.Setup(RandomColor(), GemFamily.Standard);
            }
    }

    /// <summary>Turn <paramref name="count"/> random occupied cells into stones —
    /// used once at board build so a fresh board opens with something to break.
    /// Never places two stones orthogonally adjacent.</summary>
    public void SeedStones(int count)
    {
        var steps = new (int dr, int dc)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        int guard = 0;
        while (count > 0 && guard++ < 200)
        {
            Cell cell = gridManager.GetCell(Random.Range(0, Rows), Random.Range(0, Cols));
            if (cell == null || !cell.IsOccupied() || cell.CurrentItem.IsStone) continue;

            bool nextToStone = false;
            foreach ((int dr, int dc) in steps)
                if (IsStoneCell(gridManager.GetCell(cell.row + dr, cell.col + dc)))
                    nextToStone = true;
            if (nextToStone) continue;

            cell.CurrentItem.SetupStone(stoneHp);
            count--;
        }
    }

    void OnDisable()
    {
        // A torn-down component must not leave the board frozen mid-pipeline.
        StopAllCoroutines();
        IsResolving = false;
        _prismPending = false;
    }
}

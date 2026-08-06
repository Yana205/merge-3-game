# The Corruption Clock

Replaces the random-crystal-per-move spawn with a player-driven pressure system.
Cyan is the player's ladder; red is a clock the player winds themselves.

Date: 2026-08-06
Branch: `feature/corruption-clock`

---

## 1. Why the current mechanic fails

The board is 6×6 = 36 cells. A merge removes one tile, a spawn adds one.
`InputHandler.AfterMove()` raises `OnMoveCompleted` after *any* successful move, and
`LevelManager.HandleMoveCompleted()` answers it with `difficulty.SpawnCountAt(score)`
gems in random empty cells.

| Move today | Tiles | Consequence |
|---|---|---|
| Merge | −1, then +1 = **0** | The board can never drain. Progress is invisible on the board. |
| Slide | 0, then +1 = **+1** | Repositioning is strictly punished. The verb is dead. |

Three failures follow:

1. **The slide verb is unusable.** The only move that costs nothing but position
   costs a cell. Optimal play is "never reposition," which removes spatial planning
   from a spatial game.
2. **A merge earns nothing you can see.** Net-zero tiles means skill shows up only in
   a HUD number, never in breathing room.
3. **The spawn is noise.** Random cell, random tier, random family means the player
   cannot plan two moves ahead, capping the skill ceiling.
4. **Red has no counterplay.** `MergeManager.cs:30` rejects a merge at
   `MaxTierFor(family)`, so a maxed red is a permanently dead cell. Red is variance,
   not a decision.

`DifficultyCurve.cs:14` already states the problem in a comment: "1 spawn per move is
equilibrium." The system was tuned to the exact point where the board cannot drain.

## 2. The design

```
CORRUPTION: a meter, 0 → 20, shown in the HUD.

  Any move            +3 corruption
  A merge             −(resulting tier) corruption
  Meter reaches 20    → one RED crystal erupts; meter resets to 0

CYAN spawns 1 gem per MERGE only. Slides spawn nothing.
RED never spawns from a dice roll. It arrives only from the meter.
```

One rule carries the game: **the tier you make is the corruption you clear.**

| Move | Corruption | Tiles |
|---|---|---|
| Slide | +3 | ±0 |
| Merge → tier 2 | +1 | ±0 |
| Merge → tier 3 | ±0 | ±0 |
| Merge → tier 5 | −2 | ±0 |

> **The rate must exceed the smallest possible drain.** This started at +2, which is
> exactly the drain of the cheapest merge (1+1 → tier 2). That made every merge
> net-zero on the clock, so a player who only ever merged would hold corruption
> still forever and never see a single red — the whole pressure system would sit
> dormant. Caught in play mode: `cheapest merge: 8 -> 8 (net 0)`. At +3 the same
> case reads `12 -> 13 (net +1)`.

Basic merging treads water. Big merges buy real relief. Shuffling costs time on the
clock but not board space. The player fights a *pace*, never a flood.

### 2.1 Cyan spawns

One cyan gem per merge, placed in a random empty cell **adjacent to the merge site**
when one exists, otherwise any random empty cell. Tier is rolled by the existing
`DifficultyCurve.PickTierAt(score)` — its geometric falloff is already tuned and still
does the right job.

Adjacency is for readability: the new gem appears where the player is already looking.

### 2.2 Red eruptions

A red erupts into a random empty cell **adjacent to an existing red** when one exists,
otherwise any random empty cell. Corruption visibly spreads from a site rather than
speckling the board, and clustering is what makes the red chain completable at all —
reds that never land near each other can never be merged.

Reds always enter at tier 1 — the existing rule, and now unconditional. The
`PickTierAt(score, family)` overload existed only to branch on a rolled family; with
family no longer rolled, the caller knows it is spawning a red and passes tier 1
directly. The overload is deleted along with `PickFamilyAt`.

If the board is full when the meter fills, the meter **holds at 20** and no red
erupts. The jam check then decides whether the run is over. The meter does not
silently discard the eruption.

### 2.3 The bomb

A red that reaches the top of its ladder no longer becomes dead weight. It becomes an
**armed bomb**:

- It sits on the board until the player taps it. There is no timer on it.
- Tapping detonates: every item in its 3×3 neighbourhood is destroyed, **including
  cyan**, plus the bomb itself. Up to 9 cells freed.
- Detonation sets corruption to 0.
- Destroyed cyan pays no score. The blast is a cost, not a harvest.

This is the whole risk/reward loop: the player chooses *where* to grow the red chain
knowing they will have to sacrifice its neighbours, and chooses *when* to spend it.

> **Bombs arm one rung below the ladder top, not at it.** Red ends at tier 5, and
> every rung doubles the reds required — arming at the literal top costs **16**
> tier-1 reds, more than a realistic run will ever produce, making the bomb a
> mechanic players only read about. One rung down costs 8. The dial is
> `BombController.armTiersBelowMax` (default 1); 0 gives the literal top.
>
> This forces one rule change: **`MergeManager` refuses to merge a live bomb.**
> Without it a player could merge two tier-4 bombs into a tier-5 red and defuse
> both — spending sixteen reds to destroy the payoff they were building toward.
> Two more places must agree with that refusal or they contradict it:
> `GridManager.HasAnyValidMerge` (a dead board would read as playable and the run
> would hang instead of ending) and the green drag highlight in `InputHandler`
> (it would light up a bomb neighbour and then refuse the drop).

Tapping a bomb is not a move: it raises no `OnMoveCompleted`, so nothing spawns
afterwards — same reasoning as `InputHandler.AfterShatter()` at line 172-178. The jam
check still runs, because freeing nine cells is exactly what un-jams a board.

### 2.4 Difficulty ramp

`DifficultyCurve` keeps its shape; the numbers change meaning.

- `CorruptionPerMove(score) = 2 + CountPassed(_corruptionThresholds, score)` — reuses
  the existing `CountPassed` helper and the `{700, 2500}` threshold shape.
- Eruption threshold is a fixed 20.
- `SpawnCountAt` is retired for cyan: cyan is always exactly 1 per merge. Escalation
  comes from the clock, not from the fodder rate.

### 2.5 Loss condition

Unchanged in shape: board full, no legal merge. But an armed bomb **is** a legal move,
so it joins the pickaxe in the guard at `InputHandler.cs:202`. Ending a run while a
live bomb sits on the board would be the exact bug the pickaxe rescue was written to
avoid.

### 2.6 The pickaxe stays

The pickaxe and the bomb overlap (both free cells) but differ in kind: the pickaxe is
a score-granted floor rescue the player cannot influence; the bomb is skill-earned and
positional. `PickaxeController` is untouched by this work.

## 3. Ready-state UX

Three states must be distinguishable at a glance, for both the SHATTER button and the
bomb crystal. Today the SHATTER button dims to `opacity: 0.35` when empty but is still
clickable — `ToggleArmed()` → `TryArm()` returns false and nothing visible happens.
A control that accepts a click and does nothing teaches the player it is broken.

### 3.1 The SHATTER button

| State | Interactive | Look |
|---|---|---|
| **Not ready** (0 charges) | **No** — `SetEnabled(false)` | Desaturated, `opacity: 0.3`, label `SHATTER` |
| **Ready** (≥1 charge, not armed) | Yes | Gold text on a slow pulse that draws the eye, label `SHATTER` |
| **Armed** | Yes (taps disarm) | Pressed 9-slice frame, gold, label `TAP A GEM` |

Implementation notes:

- `SetEnabled(false)` is the real fix, not just a class. It blocks the click, stops
  `:hover` / `:active` from firing, and gives UI Toolkit's `:disabled` pseudo-state to
  style against. `pickaxe-button--empty` stays as the visual class.
- **The pulse must be driven from C#.** UI Toolkit has no CSS keyframe animation.
  Use `_pickaxeButton.schedule.Execute(...).Every(700)` to toggle a
  `pickaxe-button--pulse` class, with a `transition` on `color` in USS so the toggle
  eases instead of snapping. The scheduled item must be stored and killed in
  `OnDisable` alongside the existing event unsubscribes, or it keeps ticking on a
  torn-down element.
- The pulse runs **only** in the ready state — never while armed, never while empty.
  A control that pulses in every state attracts nothing.
- The transition from not-ready → ready is the moment the player must notice. It gets
  one brighter flash on entry before settling into the pulse.

### 3.2 The bomb crystal

Same grammar, on the board instead of the HUD:

| State | Interactive | Look |
|---|---|---|
| Red below max tier | Draggable, mergeable | Normal red crystal |
| **Armed bomb** (red at max tier) | **Tappable** | Pulsing warm aura, distinct from every other crystal |

`CrystalAuraController` in `Scripts/FX/` already owns per-crystal glow and is the right
home for the bomb aura. The bomb must not be draggable — a tap detonates it, and a
drag-to-merge on a maxed gem is already rejected by `MergeManager.cs:30`, so allowing
the drag would only produce a silent no-op.

### 3.3 The corruption meter

A new HUD row in `GameHUD.uxml`, below `PICKAXE`, following the existing `stat-row`
pattern: a `field-label` reading `CORRUPTION` and a fill bar.

- Fill colour ramps cyan → amber → red as the meter approaches 20, so the threat is
  readable without reading a number.
- It flashes on the frame a red erupts, so the cause of a new red crystal is never
  ambiguous.

## 4. Components and boundaries

| Unit | Responsibility | Depends on |
|---|---|---|
| `CorruptionController` (new) | Owns the meter. Adds on move, drains on merge, announces when it fills. Pure observer of `GameEvents`, like `ScoreController` and `PickaxeController`. | `GameEvents` only |
| `DifficultyCurve` (edit) | Adds `CorruptionPerMove(score)`. Deletes `_redUnlockScore`, `_redSpawnChance`, `PickFamilyAt`. | nothing |
| `LevelManager` (edit) | Spawns one cyan per merge; spawns a red when corruption announces an eruption. | grid, curve, corruption |
| `BombController` (new) | Detects a red reaching max tier, marks it armed, runs the 3×3 detonation. | grid, `GameEvents` |
| `MergeManager` (edit) | Raises the merged tier on the bus so corruption can drain by it. | unchanged deps |
| `InputHandler` (edit) | Slides no longer raise `OnMoveCompleted`; tap on an armed bomb detonates; jam guard accepts a live bomb. | + bomb |
| `UIController` (edit) | Meter row, and the three-state SHATTER button. | `GameEvents` |
| `CrystalAuraController` (edit) | Armed-bomb aura. | Item |

`CorruptionController` holds no reference to the grid, the HUD, or input — it watches
the bus and announces. This mirrors `PickaxeController`'s documented shape
(`PickaxeController.cs:13-17`) so there is one pattern for run-state systems, not two.

New bus events: `MoveCompleted`, `CorruptionChanged(current, max)`,
`CorruptionErupted`, `BombArmed(Item)`, `BombDetonated(Cell, int cleared)`.

## 5. Known hazards

1. **Read before despawn.** Detonation must read every victim's `Family`/`Tier` before
   calling `DespawnItem` — `ItemFactory.Release` runs `ResetForPool` and wipes them.
   This is the same trap documented at `MergeManager.cs:37-40` that once turned every
   red merge into a standard gem.
2. **Jam check must accept a live bomb** (§2.5), or the run ends with a legal move on
   the board.
3. **The scheduled pulse must be killed in `OnDisable`.** `UIController` already pairs
   its subscribes and unsubscribes; the scheduler handle joins that pair.
4. **Corruption must reset on `StartEndlessRun`.** It resets *after* the score reset,
   for the same reason `pickaxeController.ResetRun()` does at `LevelManager.cs:206` —
   the reset-to-zero `ScoreChanged` event would otherwise walk it backwards.
5. **`GemFamily.Red` values are serialized** into `GemConfig` and onto Items. The enum
   is not touched.

## 6. Tuning dials

Starting values, expected to move once playable:

| Dial | Shipped | Effect |
|---|---|---|
| `basePerMove` | 3 | Higher = faster clock. Must stay **above 2** (see §2) |
| `threshold` | 20 | ≈7 idle moves to a red |
| `drainPerMergedTier` | 1 | Higher = big merges matter more |
| `_rateThresholds` | `{600, 2000}` | Where the clock speeds up (→ 4/move, 5/move) |
| `armTiersBelowMax` | 1 | 1 → 8 reds per bomb; 0 → 16 |
| `blastRadius` | 1 (a 3×3) | Payoff size |

**These live in the scene, not in the source.** The components were added to
`mainGame.unity` before the defaults were retuned, and Unity field initializers only
apply to newly-added components — an already-serialized component keeps its stored
values. Changing a default in the `.cs` will *not* change what the game reads. Edit
the values in the inspector, or via `SerializedObject`, and re-verify at runtime.

## 7. Verified in play mode

The project has no test assemblies, so verification was done by driving the real
event bus in play mode and reading state back. Results:

| Check | Result |
|---|---|
| Clock ticks and erupts | 10 idle moves → `0→2→…→18→` red erupts, meter resets |
| Merge drain | simulated tier-4 merge: `8 → 4` (exactly −4) |
| Rate ramp | `PerMoveAt(0/600/2000) = 3/4/5` |
| Cheapest-merge floor | `12 → 13` (net +1 — clock advances under perfect play) |
| Reds cluster | 6 reds erupted, **6/6** had a red neighbour |
| Bomb arms | tier 4 of a 5-tier ladder, `HasArmedBomb() = true` |
| Bombs unmergeable | `TryMerge(bombA, bombB) = false`, both stay armed |
| Detonation | cleared all 7 occupied cells in the 3×3; 3×3 empty after |
| Chained bombs | bomb B caught in A's blast is disarmed; `HasArmedBomb() = false` |
| Purge | corruption `5 → 0` on detonation |
| Opening board | 9 empty / 27 occupied, 63 mergeable pairs, not jammed |
| SHATTER at 0 charges | `enabled = false` + `unity-disabled` class |
| SHATTER at 2 charges | enabled, `--ready` + `--pulse` (entry flash) |
| SHATTER armed | `--armed`, pulse cleared, text `TAP A GEM` |
| SHATTER back to 0 | disabled, pulse removed |
| Meter at 15/20 | `width: 75%`, colour `RGBA(0.935, 0.525, 0.250)` |

## 8. Testing (not yet written)

EditMode, pure logic, no scene:

- `DifficultyCurve.CorruptionPerMove` steps at each threshold; a null/empty threshold
  array yields the base rate rather than throwing.
- `CorruptionController`: +2 on move, −N on a tier-N merge, clamps at 0, announces
  exactly once at ≥20 and resets, holds at 20 when eruption is refused.
- Bomb detonation clears exactly the 3×3 including corners, and is clipped correctly
  at a board edge and a corner.
- Detonation of a bomb whose neighbours are empty frees only itself.
- A red merged to max tier is reported armed; a red below max is not.

PlayMode:

- A slide spawns nothing and raises corruption.
- A merge spawns exactly one cyan and lowers corruption by the tier made.
- Board full + no merge + a live bomb ⇒ the run does **not** end.
- SHATTER button is genuinely unclickable at 0 charges (click raises no state change).

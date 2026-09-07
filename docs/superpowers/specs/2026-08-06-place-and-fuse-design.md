# Place & Fuse

Second redesign of the endless loop. Replaces both the original random-spawn model
and the corruption clock that briefly replaced it.

Date: 2026-08-06
Branch: `feature/place-and-fuse`
Supersedes: `2026-08-06-corruption-clock-design.md`

---

## 1. Why the corruption clock failed

It was an **invisible tax with no decision attached**. Its only output was "eventually
a red appears somewhere" — delayed, indirect, and impossible to route around. The
player could never spend it, trade against it, or make any choice because of it.

A resource meter earns its screen space only when the player must decide something at
the moment it moves. This one just accumulated, which is also why it read as furniture
in the side panel: you don't look at a number that never asks you anything.

Moving the bar would not have fixed it. The mechanic had to go.

The complaint underneath all of the playtest notes was the same one, and it was also
true of the ORIGINAL design: **the player does not control where crystals come from.**

## 2. The design

```
        NEXT  ▸ ●  ●  ●          three crystals, rendered under the board

  TAP AN EMPTY CELL → the NEXT crystal lands there. That is the entire verb.

  Any orthogonally-connected group of 3+ identical crystals then fuses into
  one crystal of the next tier, AT THE CELL YOU TAPPED. Fusing cascades.

  Board full = run over.
```

Nothing ever appears on its own. Every crystal on the board is somewhere the player
chose to put it, and they can see three moves of supply ahead.

### 2.1 Rules fixed without asking

- **Orthogonal (4-way) connectivity**, not the current 8-way. With diagonals on a 6×6,
  groups of 3 form almost by accident and the puzzle evaporates. 4-way is also the
  classic rule.
- **The fused crystal appears at the tapped cell**, not at the group's centroid. This
  is what makes placement a skill: the player aims the *result*, not just the input.
- **Groups of 4+ are consumed whole** into a single next-tier crystal. Simpler to read
  than leaving remainders, and it makes over-feeding a group a real (small) mistake.
- **Dragging is deleted.** Placement is the only board verb, so `InputHandler`'s drag
  path, its highlight code, and `GridManager.AreAdjacent` all stop being load-bearing.

### 2.2 The cost curve

3-to-fuse means tier *n* costs **3ⁿ⁻¹** tier-1 crystals:

| Tier | 1 | 2 | 3 | 4 | 5 | 6 | 7 |
|---|---|---|---|---|---|---|---|
| Cost | 1 | 3 | 9 | 27 | 81 | 243 | 729 |

Tier 5 already exceeds the 36-cell board, so the top of the cyan ladder is a long-run
goal rather than a five-minute one. Accepted deliberately — but it does mean tiers 6–7
will rarely be seen, and their art will go mostly unviewed.

### 2.3 Red

Red arrives in the queue like everything else, so it is always visible in advance and
the player chooses where it goes.

- Reds fuse only with reds (identity stays `(family, tier)`).
- **A red at tier 3 arms as a bomb.** Nine reds. Tap it to clear its 3×3 — cyan
  included — and free up to 9 cells.
- Below tier 3, reds are dead weight occupying cells.

Every red in the queue is therefore a real decision: bury it somewhere harmless and
eat the lost cell, or commit a 9-crystal cluster to farming a bomb.

An armed bomb never fuses (`MergeManager` and the group scan both skip it), so a
fourth red landing beside a finished bomb cannot defuse it.

### 2.4 The queue

`CrystalQueue` holds NEXT plus two previews. Entries are rolled by `DifficultyCurve`
as a `(family, tier)` pair:

- Cyan at tier 1 almost always; a small, score-scaled chance of tier 2.
- Red chance ramps from 0 at `redUnlockScore` toward `maxRedChance`. **This is the
  entire difficulty curve now** — and it is honest, because the player sees each red
  coming three moves out.

Previews are rendered as **real world-space `Item`s below the board**, not as UI
Toolkit images. They reuse the existing sprite, `GemConfig` and pooling machinery
verbatim, and they sit where the player is already looking. Because they occupy no
`Cell`, they are inert: placement needs an empty cell, and `PickaxeController.Shatter`
already refuses an item that `FindCellWithItem` cannot locate.

### 2.5 Loss and rescue

The run ends when the board is full — there is nowhere to place. An armed bomb or a
pickaxe charge both still count as legal moves, so:

```
lost = grid.IsFull() && !bombs.HasArmedBomb() && !pickaxe.HasCharge
```

The pickaxe is unchanged and keeps its role as the score-granted floor rescue.

### 2.6 Opening board

A light scatter of tier-1 cyan (default 8 of 36) so the first placements can
immediately do something, rather than 36 empty cells of aimless tapping.

## 3. Components

| Unit | Responsibility |
|---|---|
| `CrystalQueue` (new) | Owns NEXT + previews, rolls entries, renders the world-space preview row |
| `PlacementController` (new) | `TryPlace(Cell)` — take from queue, spawn, resolve fusion, announce |
| `MergeManager` (rewritten) | `ResolveAt(Cell)` — flood-fill, fuse, cascade. `TryMerge(a,b)` deleted |
| `GridManager` | `+ GetConnectedGroup(Cell)` (4-way BFS), `+ SpawnLooseItem`. Drag/pair helpers deleted |
| `DifficultyCurve` | Rolls `(family, tier)` for the queue. Everything else deleted |
| `InputHandler` | Tap only. Drag path, highlights and `OnMoveCompleted` deleted |
| `LevelManager` | Run lifecycle, opening scatter, score relay. Spawn logic deleted |
| `BombController` | Arms at red tier 3 (explicit, was "tiers below max") |
| `CorruptionController` | **Deleted** |

## 4. Removed

`CorruptionController`, the corruption HUD row and meter styles, `GameEvents`'
`MoveCompleted` / `CorruptionChanged`, all drag state and highlight code in
`InputHandler`, `GridManager.AreAdjacent` / `CountAdjacentSameTierPairs` /
`HasAnyValidMerge` / `GetRandomEmptyCellAdjacentTo` / `FindCellsWithFamily`,
`LevelManager.EnsureGuaranteedPairs`.

## 5. Hazards

1. **Read before despawn.** Fusion and detonation must read every victim's
   `Family`/`Tier` before `DespawnItem` — `ItemFactory.Release` runs `ResetForPool`
   and wipes them (`MergeManager.cs` has carried this warning since the red chain
   landed).
2. **Cascade must re-scan from the tapped cell** after each fuse, not from a stale
   group list.
3. **Preview items are in `_liveItems`**, so `CreateGrid`'s `ClearGrid` destroys them.
   The queue must re-render after a board rebuild, not before.
4. **Do not scale preview items.** `Item.ResetForPool` does not restore
   `localScale`, so a scaled preview would return to the pool small and come back as
   an undersized board crystal.
5. **Serialized values live in the scene.** Unity field initializers do not touch an
   already-serialized component; new tuning must be written through `SerializedObject`
   or the Inspector, then read back at runtime.

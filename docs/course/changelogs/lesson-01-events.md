# Lesson 1 — C# Events & Observer Pattern

- **Branch:** `feature/lesson-01-events-observer`
- **Status:** ✅ DONE
- **Date:** 2026-07-19

## Summary

Introduced a global **event bus** (`GameEvents`) and formalized the Observer
pattern across the existing systems, so gameplay code no longer calls into
scoring/UI directly. A merge now *announces itself* on the bus; `ScoreController`
listens and scores it; `LevelManager` listens for the resulting score change and
handles level completion. Also added a **direct parent→child event**
(`Item.OnDespawned`) with pooled-safe cleanup. This is purely additive — the game
loop, pooling, save system, and UI all keep working; the only thing removed is the
direct merge→score call path (`LevelManager.HandleMerged`), which the lesson
explicitly asks to replace with an event.

## Deliverable → code map

| Assignment deliverable | Where it lives |
|------------------------|----------------|
| Static `GameEvents` with ≥3 global events | `Core/GameEvents.cs` — `ScoreChanged`, `TileMerged`, `SaveRequested` |
| Replace a direct method call with a bus event | `Level/LevelManager.cs` removed `HandleMerged`→`scoreController.AddScore`; merge now flows `MergeManager.RaiseTileMerged` → `ScoreController` |
| ≥2 systems subscribe to `GameEvents` | `ScoreController` (`TileMerged`,`SaveRequested`) and `LevelManager` (`ScoreChanged`) |
| Direct Event on a child + parent subscribes via `+=` | `Item.OnDespawned` (child) — `GridManager.SpawnItem` does `item.OnDespawned += HandleItemDespawned` (parent) |
| `?.Invoke()` for ALL invocations | every raise: `GameEvents.Raise*`, `MergeManager.cs:51`, `Item.cs:98` |
| `AddListeners`/`RemoveListeners` + unsubscribe in `OnDisable`/`OnDestroy` | `ScoreController` (`OnEnable`/`OnDisable`), `LevelManager` (`Start`/`OnDestroy`) |
| Pooled objects unsubscribe on return to pool | `Item.ResetForPool()` fires then clears `OnDespawned = null` |
| AI review for missing unsubscribes | this file, "Review findings" below (fresh-context reviewer subagent) |

## What changed

- **`Core/GameEvents.cs`** *(new)* — static bus. 3 events + `Raise*` helpers (single
  home for the `?.Invoke()` rule) + `[RuntimeInitializeOnLoadMethod]` `ResetOnLoad()`
  that nulls all events before the first scene, so subscribers can't leak across
  Editor play sessions when Reload Domain is off.
- **`Core/MergeManager.cs`** — after a successful merge, raises
  `GameEvents.RaiseTileMerged(newItem, cellB)` (bus) in addition to the retained
  local `OnMerged` direct event.
- **`Core/ScoreController.cs`** — now a pure Observer: `AddListeners`/`RemoveListeners`
  in `OnEnable`/`OnDisable`; `TileMerged` handler computes points and adds score;
  `AddScore`/`ResetScore` raise `ScoreChanged` + `SaveRequested`; `SaveRequested`
  handler persists via the injected `ISaveSystem`.
- **`Core/Item.cs`** — added child event `OnDespawned`; `ResetForPool` invokes it
  (before clearing state so listeners see final tier/position) then clears subscribers.
- **`Core/GridManager.cs`** — `SpawnItem` subscribes the owner to `item.OnDespawned`;
  `HandleItemDespawned` drives a `TilesDespawnedThisSession` counter (the juice pass
  will add a second subscriber for the burst).
- **`Level/LevelManager.cs`** — `AddListeners`/`RemoveListeners` pair; subscribes to
  `GameEvents.ScoreChanged`; re-raises its `OnScoreChanged(score,target)` for the UI
  and runs a **one-shot** `_levelComplete` check; removed the direct merge-scoring
  path; kept the now-unused `mergeManager` field (annotated) to avoid scene churn.

## Event flow (before → after)

```
BEFORE:  merge → MergeManager.OnMerged → LevelManager.HandleMerged → scoreController.AddScore()   [direct call]
AFTER:   merge → GameEvents.TileMerged → ScoreController.AddScore → GameEvents.ScoreChanged ┬→ LevelManager (UI + completion)
                                                                                            └→ (Lesson 2 UIController)
         despawn → Item.OnDespawned → GridManager.HandleItemDespawned   [direct parent↔child]
```

## Editor steps

None — pure C#. When the project is opened, Unity recompiles automatically. No
scene rewiring is required (existing inspector references are unchanged; the
merge→score path is now internal to the bus). To see it: enter Play mode, merge
tiles, watch the score/target HUD update and a level complete at its target.

## AI assistance (assignment: "use an AI tool to review your event implementation")

- **Tool/prompt:** a fresh-context reviewer subagent was given all changed files and
  asked to verify every `+=` has a matching `-=` (or pooled clear), flag any bare
  `Invoke()`, check for double-subscription, and check for scoring/completion/save
  regressions and init-ordering NREs.
- **Result:** verdict **SHIP**, no HIGH-severity issues. Findings below.

## Review findings

- `[ok]` All subscriptions balanced: ScoreController `OnEnable`/`OnDisable`,
  LevelManager `Start`/`OnDestroy`, pooled `Item.OnDespawned` cleared on return.
- `[ok]` No bare `Invoke()` anywhere; every call site uses `?.Invoke()`.
- `[ok]` Score added exactly once per merge (single `RaiseTileMerged` site, single
  `TileMerged` subscriber); level-complete guarded one-shot by `_levelComplete`;
  save still fires on change; UI chain intact; static-bus reset correct.
- `[fixed]` `LevelManager.LoadLevel` fired `OnScoreChanged(0,target)` twice with a
  ScoreController wired → now the direct call only runs on the no-ScoreController
  fallback path.
- `[fixed]` `CheckLevelComplete` could instant-complete a level whose `targetScore`
  is ≤ 0 (satisfied by the load-time `ScoreChanged(0)`) → added a `targetScore > 0` guard.
- `[note]` Scoring now depends on `ScoreController` being enabled (OnEnable-based
  subscription). This is the intended Observer semantics — a disabled scorer
  shouldn't score — and the component is always active in the scene. Accepted.
- `[note]` `ResetScore()` persists `score = 0` on each level load (raises
  `SaveRequested`). This matches the **pre-existing** behavior (the old `ResetScore`
  also saved 0) and has no player-visible effect — the leaderboard uses the separate
  `ProgressManager`/`GameProgress` save. Left unchanged (out of scope for Lesson 1).
- `[note]` The non-pooled `DespawnItem` fallback (edit mode / unwired factory) skips
  `ResetForPool`, so `OnDespawned` doesn't fire there. The normal play-mode pooled
  path fires it; the fallback is a degraded/error path where despawn juice isn't
  needed. Accepted.

## Verification

Headless runs can't enter Play mode, so this was verified by static tracing +
the fresh-context review: symbol resolution confirmed (`GameEvents`, `Item`, `Cell`,
`ISaveSystem` all exist), every subscribe/unsubscribe pair matched by grep, and the
merge→score→UI→completion chain walked end to end. **When the user plays:** merging
tiles raises the score exactly as before, the HUD updates, saving occurs on each
change, and the level completes once at its target — with all cross-system calls now
routed through events.

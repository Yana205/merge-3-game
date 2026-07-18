# Course Implementation — Progress Tracker

> **Single source of truth** for the "one lesson per fresh context" loop.
> A fresh chat should: read this file → find the first lesson whose status is
> `TODO` → invoke the **`lesson-loop`** skill → implement → self-review with the
> **`lesson-review`** skill → write the changelog → branch/commit/push/merge →
> update this file → stop. See [`.claude/skills/lesson-loop/SKILL.md`](../../.claude/skills/lesson-loop/SKILL.md).

## Environment status (read this first every session)

- **Unity Editor MCP (`ai-game-developer`) is UNAUTHORIZED (HTTP 401)** in headless runs.
  → We author **all code + text assets directly** (`.cs`, `.shader`, `.uxml`, `.uss`, `.json`).
  → Anything that needs the live Editor (creating `.asset`/`.mat`/`PanelSettings`
    instances, wiring components into `mainGame.unity`, Play mode, screenshots) is
    delivered as an **`[MenuItem]` Editor automation script** the user runs once,
    **plus** written manual steps in the lesson changelog under "Editor steps".
  → If a future session finds the MCP authorized (`ping` returns `pong`), it may
    perform those steps directly instead.
- **Git flow is fully automated** (push + `gh` are authenticated): each lesson gets
  its own branch, is committed, pushed, and merged `--no-ff` into `main` so `main`
  accumulates every lesson intact. We do **not** open PRs for this course (explicit
  user request: keep every lesson stacked on `main`, no human in the loop).
- **Never `git add -A`.** ~120 `.claude/skills/*/SKILL.md` files show as modified
  (LF→CRLF only) and `.mcp.json` is untracked (holds auth). Stage **explicit paths only**.

## Lesson status

| # | Lesson | Branch | Status | Changelog |
|---|--------|--------|--------|-----------|
| 0 | Course-loop infrastructure | `chore/course-loop-infrastructure` | ✅ DONE | — |
| 1 | C# Events & Observer Pattern | `feature/lesson-01-events-observer` | 🔲 TODO | [lesson-01-events.md](changelogs/lesson-01-events.md) |
| 2 | UI Toolkit HUD | `feature/lesson-02-ui-toolkit-hud` | 🔲 TODO | [lesson-02-ui-toolkit.md](changelogs/lesson-02-ui-toolkit.md) |
| 3 | Crystal magical shader (URP HLSL) | `feature/lesson-03-crystal-shader` | 🔲 TODO | [lesson-03-crystal-shader.md](changelogs/lesson-03-crystal-shader.md) |
| 4 | Shader effects (ShaderToy port + runtime controller) | `feature/lesson-04-shader-effects` | 🔲 TODO | [lesson-04-shader-effects.md](changelogs/lesson-04-shader-effects.md) |
| 8 | Data-driven design & ScriptableObjects | `feature/lesson-08-data-driven` | 🔲 TODO | [lesson-08-data-driven.md](changelogs/lesson-08-data-driven.md) |
| J | Juice & particles polish (presentation) | `feature/juice-and-particles` | 🔲 TODO | [juice-and-particles.md](changelogs/juice-and-particles.md) |

Status legend: `🔲 TODO` · `🚧 IN PROGRESS` · `✅ DONE` · `⏸ BLOCKED (needs Editor)`.

> Lessons 5–7 are not in the assignment set the user provided; the numbering
> follows the course (Lesson 8 is "Data-Driven Design"). Keep the gap.

---

## NEXT ACTION

➡️ **Lesson 1 — C# Events & Observer Pattern.** Branch `feature/lesson-01-events-observer`.

---

## Per-lesson checklists

Each box maps to a deliverable from the assignment. Check it only when the code
(or Editor script + documented step) actually exists in the repo.

### Lesson 1 — C# Events & Observer Pattern
Part 1 — Event Bus & Direct Events
- [ ] Static `GameEvents` class with ≥3 global events (ScoreChanged, TileMerged, SaveRequested)
- [ ] Replace ≥1 direct method call with a bus event (LevelManager no longer calls `ScoreController.AddScore` on merge)
- [ ] ≥2 systems subscribe to `GameEvents` (ScoreController + LevelManager; UIController joins in Lesson 2)
- [ ] ≥1 Direct Event on a child component (`Item.OnDespawned`) with a parent subscribing via `+=`
Part 2 — Safe subscriptions & cleanup
- [ ] `?.Invoke()` for ALL invocations (no bare `Invoke()`)
- [ ] `AddListeners`/`RemoveListeners` pattern with unsubscribe in `OnDisable`/`OnDestroy`
- [ ] Pooled objects unsubscribe on return to pool (`Item.ResetForPool` clears child-event subscribers)
- [ ] AI review for missing unsubscribes (fresh-context reviewer agent; findings in changelog)

### Lesson 2 — UI Toolkit HUD
- [ ] `GameHUD.uxml` with ≥3 elements (score label, high-score label, a button)
- [ ] `PanelSettings` asset (Scale With Screen Size) on a `UIDocument` — Editor script + steps
- [ ] `GameHUD.uss` with a type selector, an `#id` selector, and a `.class` selector, linked to UXML
- [ ] `UIController.cs` queries via `Q<T>("name")` and updates the label on score change
- [ ] Button `clicked` wired to a C# method (restart/menu)
- [ ] AI assistance evidence noted in changelog (prompt + whether output worked)
- [ ] `.uxml` readable/documented in changelog

### Lesson 3 — Crystal magical shader (URP HLSL)
- [ ] Transparent setup: `RenderType=Transparent`, `Queue=Transparent`, `Blend SrcAlpha OneMinusSrcAlpha`, `ZWrite Off`
- [ ] ≥4 exposed Properties (base color, glow color, scroll speed, noise scale)
- [ ] Procedural HLSL noise function (no texture)
- [ ] Scrolling UVs (`_Time.y * _ScrollSpeed` on Y) → noise → `lerp`/`smoothstep` color blend
- [ ] Dynamic alpha (breathing) + ≥1 extra animated property (crystal-appropriate)
- [ ] Material Editor script + assignment steps (crystal cells, not a water plane)

### Lesson 4 — Shader effects
- [ ] Shader Graph effect (noise + Time → Alpha/Base Color) — **needs Editor**; documented build steps
- [ ] ShaderToy GLSL → Unity HLSL port running in-scene (`.shader`)
- [ ] C# controller drives a shader property at runtime via `renderer.material` (not shared)
- [ ] `Destroy(_mat)` in `OnDestroy()`
- [ ] Director Notes for each shader (changelog)

### Lesson 8 — Data-driven design & ScriptableObjects
- [ ] One item family → ScriptableObject with `[CreateAssetMenu]`, `[SerializeField] private` + read-only getters
- [ ] ≥3 data instances (Editor menu generator) read by existing MonoBehaviour with zero logic changes
- [ ] Tier field + per-tier/per-item weight; generic `GetItemByWeightedRandom()`
- [ ] Changing a weight (no code) visibly shifts spawn frequency
- [ ] JSON file + `[SerializeField] TextAsset` load; typed deserialization (color/enum/id → real types)
- [ ] Deliberately-broken value reported clearly (no silent default/crash)
- [ ] Bulk-generate 5–10 entries from schema; work with zero manual edits
- [ ] Validator: ≥3 checks (dup IDs, invalid color/enum, min>max, missing refs)
- [ ] Editor menu runs validator before Play; document one bug the validator caught

### Juice & particles polish (presentation)
- [ ] Merge burst / spawn pop (code-driven, subscribes to `GameEvents`/`Item.OnDespawned`)
- [ ] Score-gain popup / punch, level-complete celebration
- [ ] Screen shake / squash-stretch tween helper
- [ ] Everything decoupled through events (no new hard cross-references)

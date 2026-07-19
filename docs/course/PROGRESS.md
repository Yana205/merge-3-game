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
  → **New asset files (`.cs`/`.shader`/`.uxml`/`.uss`) are committed WITHOUT their
    `.meta`** — Unity generates the `.meta` on first Editor open (we never hand-write
    them, per `CLAUDE.md`). First thing to do after pulling: open the project in
    Unity once so it generates metas + recompiles, then commit the new `.meta` files.
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
| 1 | C# Events & Observer Pattern | `feature/lesson-01-events-observer` | ✅ DONE | [lesson-01-events.md](changelogs/lesson-01-events.md) |
| 2 | UI Toolkit HUD | `feature/lesson-02-ui-toolkit-hud` | ✅ DONE | [lesson-02-ui-toolkit.md](changelogs/lesson-02-ui-toolkit.md) |
| 3 | Crystal magical shader (URP HLSL) | `feature/lesson-03-crystal-shader` | ✅ DONE | [lesson-03-crystal-shader.md](changelogs/lesson-03-crystal-shader.md) |
| 4 | Shader effects (ShaderToy port + runtime controller) | `feature/lesson-04-shader-effects` | ✅ DONE* | [lesson-04-shader-effects.md](changelogs/lesson-04-shader-effects.md) |
| 8 | Data-driven design & ScriptableObjects | `feature/lesson-08-data-driven` | ✅ DONE | [lesson-08-data-driven.md](changelogs/lesson-08-data-driven.md) |
| J | Juice & particles polish (presentation) | `feature/juice-and-particles` | 🔲 TODO | [juice-and-particles.md](changelogs/juice-and-particles.md) |

Status legend: `🔲 TODO` · `🚧 IN PROGRESS` · `✅ DONE` · `⏸ BLOCKED (needs Editor)`.

> Lessons 5–7 are not in the assignment set the user provided; the numbering
> follows the course (Lesson 8 is "Data-Driven Design"). Keep the gap.

---

## NEXT ACTION

➡️ **Juice & particles polish pass.** Branch `feature/juice-and-particles`.
(All course lessons 1–4 + 8 ✅ merged to `main`. Final pass: code-driven juice —
merge burst / spawn pop / score punch / screen shake — decoupled through
`GameEvents` and `Item.OnDespawned`, for a presentation-ready feel.)

---

## Per-lesson checklists

Each box maps to a deliverable from the assignment. Check it only when the code
(or Editor script + documented step) actually exists in the repo.

### Lesson 1 — C# Events & Observer Pattern ✅
Part 1 — Event Bus & Direct Events
- [x] Static `GameEvents` class with ≥3 global events (ScoreChanged, TileMerged, SaveRequested)
- [x] Replace ≥1 direct method call with a bus event (LevelManager no longer calls `ScoreController.AddScore` on merge)
- [x] ≥2 systems subscribe to `GameEvents` (ScoreController + LevelManager; UIController joins in Lesson 2)
- [x] ≥1 Direct Event on a child component (`Item.OnDespawned`) with a parent subscribing via `+=`
Part 2 — Safe subscriptions & cleanup
- [x] `?.Invoke()` for ALL invocations (no bare `Invoke()`)
- [x] `AddListeners`/`RemoveListeners` pattern with unsubscribe in `OnDisable`/`OnDestroy`
- [x] Pooled objects unsubscribe on return to pool (`Item.ResetForPool` clears child-event subscribers)
- [x] AI review for missing unsubscribes (fresh-context reviewer agent; findings in changelog)

### Lesson 2 — UI Toolkit HUD ✅
- [x] `GameHUD.uxml` with ≥3 elements (score label, high-score label, a button)
- [x] `PanelSettings` asset (Scale With Screen Size) on a `UIDocument` — Editor script + steps
- [x] `GameHUD.uss` with a type selector, an `#id` selector, and a `.class` selector, linked to UXML
- [x] `UIController.cs` queries via `Q<T>("name")` and updates the label on score change
- [x] Button `clicked` wired to a C# method (restart/menu)
- [x] AI assistance evidence noted in changelog (prompt + whether output worked)
- [x] `.uxml` readable/documented in changelog

### Lesson 3 — Crystal magical shader (URP HLSL) ✅
- [x] Transparent setup: `RenderType=Transparent`, `Queue=Transparent`, `Blend SrcAlpha OneMinusSrcAlpha`, `ZWrite Off`
- [x] ≥4 exposed Properties (base color, glow color, scroll speed, noise scale)
- [x] Procedural HLSL noise function (no texture)
- [x] Scrolling UVs (`_Time.y * _ScrollSpeed` on Y) → noise → `lerp`/`smoothstep` color blend
- [x] Dynamic alpha (breathing) + ≥1 extra animated property (crystal-appropriate)
- [x] Material Editor script + assignment steps (crystal cells, not a water plane)

### Lesson 4 — Shader effects ✅
- [x] Shader Graph effect (noise + Time → Alpha/Base Color) — documented Editor build steps (⏸ `.shadergraph` can't be authored headless)
- [x] ShaderToy GLSL → Unity HLSL port running in-scene (`Shaders/CrystalAura.shader`)
- [x] C# controller drives a shader property at runtime via `renderer.material` (not shared)
- [x] `Destroy(_mat)` in `OnDestroy()`
- [x] Director Notes for each shader (changelog)

### Lesson 8 — Data-driven design & ScriptableObjects ✅
- [x] One item family → ScriptableObject with `[CreateAssetMenu]`, `[SerializeField] private` + read-only getters
- [x] ≥3 data instances (Editor menu generator) read generically (GemDatabase/GemJsonLoader)
- [x] Tier field + per-tier/per-item weight; generic `GetItemByWeightedRandom()`
- [x] Changing a weight (no code) visibly shifts spawn frequency (Sample Distribution)
- [x] JSON file + `[SerializeField] TextAsset` load; typed deserialization (color/enum/id → real types)
- [x] Deliberately-broken value reported clearly (no silent default/crash) — break test documented
- [x] Bulk-generate 5–10 entries from schema (8 gems); zero manual edits
- [x] Validator: 4 checks (dup IDs, empty id/missing ref, min>max, zero-weight rarity)
- [x] Editor menu runs validator before Play; validator bug (zero-weight Legendary) documented

### Juice & particles polish (presentation)
- [ ] Merge burst / spawn pop (code-driven, subscribes to `GameEvents`/`Item.OnDespawned`)
- [ ] Score-gain popup / punch, level-complete celebration
- [ ] Screen shake / squash-stretch tween helper
- [ ] Everything decoupled through events (no new hard cross-references)

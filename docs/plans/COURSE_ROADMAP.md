# SBS Course Roadmap — Lessons 3+

Maps each course lesson (sbsgames.dev, cohort `inter_may26`) onto a concrete feature phase
for the merge-3 game. One branch per lesson, one PR per lesson, instructor feedback lands
before the next lesson starts.

| Lesson | Topic | Plan | Branch | Status |
|--------|-------|------|--------|--------|
| 3 | Caching, Object Pooling & Generics | [LESSON3_POOLING_PLAN.md](LESSON3_POOLING_PLAN.md) | `feature/lesson3-pooling` | planned |
| 4 | Data Formats, Async, Addressables & Architecture | [LESSON4_ADDRESSABLES_PLAN.md](LESSON4_ADDRESSABLES_PLAN.md) | `feature/lesson4-addressables` | planned |
| 5+ | _paste assignment text to extend this roadmap_ | — | — | — |

## Working agreement

- Each lesson = one feature branch off `main`, small commits per phase, PR when the
  assignment checklist is fully green.
- Lesson 4 builds directly on Lesson 3's pool (`IPool<T>` / `MonoBehaviourPool<T>`), so
  merge the Lesson 3 PR before starting Lesson 4.
- The unmerged `feature/ui-menu` branch (menu, background, `BackgroundFitter`) should be
  merged before Lesson 3 starts so the cache audit covers the whole codebase.
- Submission = link to the lesson branch (or PR) on GitHub (`Yana205`).

## Why these mappings fit the merge game

- **Lesson 3 (pooling):** gems are the game's churn object — every move spawns one Item,
  every merge destroys two and spawns one. Pooling them is the textbook use case and is
  explicitly "not bullets".
- **Lesson 4 (Addressables + architecture):** the Item prefab is currently hard-wired in
  the Inspector on `GridManager`; migrating it to Addressables and inserting a
  factory/pool/service-loader layering upgrades the whole spawn pipeline built in Lesson 3.

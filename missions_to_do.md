# Missions To Do

Open work, roughly in priority order. AI tools: read this with missions_done.md to pick up where we left off.

- [ ] **Finish Lesson 4 editor steps** — wire `ServiceLoader.gridManager` + `LevelManager.serviceLoader` in the scene, retire the `ItemPoolManager` component/file, play-mode smoke test of the Addressables → pool → factory chain (blocked earlier on a Unity modal dialog).
- [ ] **Lesson 3 bonus** — Profiler screenshot showing 0 B GC Alloc during merge cycles (needs manual drag-merges in play mode).
- [ ] **SeasonalSkins Addressables group** — second group with an alternate gem sprite set + a runtime `SwapSkinAsync(string key)` method (Lesson 4 bonus).
- [ ] **Merge juice** — pooled VFX on merge (particles, punch scale), sound effects; reuse `IPool<T>` for the particle systems.
- [ ] **Audio manager** — background music + SFX with volume settings persisted via `ISaveSystem`.
- [ ] **More levels** — Level_03+ as `LevelData` assets (bigger grids, tier goals), level-select screen driven by the levels array instead of fixed buttons.
- [ ] **Lesson 5** — paste the assignment from sbsgames.dev when published; extend docs/plans/COURSE_ROADMAP.md.

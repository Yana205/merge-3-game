# Missions Done

Completed work, newest last. AI tools: read this with missions_to_do.md to pick up where we left off.

- [x] **Merge-3 core** — 5×5 grid, drag-to-merge (same tier + adjacent → next tier), level system with `LevelData` ScriptableObjects (Level_01, Level_02), score/target win condition, board lock on completion.
- [x] **20-tier gem art** — pixel-art sprites for every tier (obsidian → star gem), wired into `GemConfig` and the Item prefab (PR #3).
- [x] **Themed UI** — cave background, styled pixel buttons, title logo, overlay main menu with Play/Level 1/Level 2/Quit (PR #9 stack).
- [x] **Lesson 3: caching + pooling** — cache audit (camera, scene scans, shared sprites, TryGetComponent), generic `IPool<T>` + `MonoBehaviourPool<T>` (`where T : Component`), gem Items pooled with prewarm 32 and zero gameplay Instantiate/Destroy (PR #12).
- [x] **Level flow fixes** — Next Level button no longer loads nonexistent scenes; UI clicks fixed (legacy `StandaloneInputModule`; new Input System had no devices).
- [x] **Transitions & identity** — `ScreenFader` fade between menu/levels, "Level N" banner, per-level backgrounds (`bg_main` / `bg_dark`) via `LevelData.backgroundSprite`.
- [x] **Lesson 4: Addressables + architecture (code)** — `Item.prefab` as Addressable `GemItem` in the `Gameplay` group; async `ServiceLoader.LoadAsync()` (no async void) loads it, builds the pool, injects into `ItemFactory.Init(prefab, pool)`; `GridManager` spawns via the factory; `LevelManager` gated on `OnServicesReady`.

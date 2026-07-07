# Lesson 4 Plan — Data Formats, Async, Addressables & Game Architecture

Branch: `feature/lesson4-addressables` (off `main`, **after the Lesson 3 PR is merged** —
this lesson rebuilds the spawn pipeline on top of `IPool<T>`/`MonoBehaviourPool<T>`).
Deliverable: Item prefab loaded via Addressables, an async `ServiceLoader`, and a 4-layer
architecture where managers spawn only through a factory.

## Phase 4.1 — Addressables migration (assignment Part 1)

Migration target: the **gem Item prefab** (`Assets/_Project/Prefabs/Item.prefab`) —
currently hard-wired in the Inspector as `GridManager.itemPrefab` and (post-Lesson 3)
`ItemPoolManager.itemPrefab`. That satisfies "hard-wired with Instantiate".

- [ ] Install `com.unity.addressables` via Package Manager (commit `Packages/manifest.json`).
- [ ] Mark `Item.prefab` as Addressable, key **`GemItem`**, group **`Gameplay`**
      (named group, not Default Local Group). *(user does this in the Unity Editor;
      commit the generated `AddressableAssetsData/` files)*
- [ ] Remove the serialized prefab reference from `ItemPoolManager`/`GridManager`.
      Replace with `Addressables.LoadAssetAsync<GameObject>("GemItem")` using async/await
      (in the `ServiceLoader`, Phase 4.2).
- [ ] Check `handle.Status == AsyncOperationStatus.Succeeded` before touching
      `handle.Result`; log `handle.OperationException` on failure.
- [ ] Build Addressables (Build → New Build → Default Build Script) and confirm the gem
      spawns correctly in play mode.
- [ ] `// ADDRESSABLES MIGRATION:` comment block at the top of the loading script:
      migrated = Item.prefab; old method = Inspector-wired reference + `Instantiate`;
      new key = `GemItem`; group = `Gameplay`.

## Phase 4.2 — Async ServiceLoader (assignment Part 2)

Evolve the existing `Loader.cs` (`Assets/_Project/Scripts/Core/Loader.cs`, which already
does sync DI for `ISaveSystem` → `ScoreController`) into **`ServiceLoader.cs`** — rename
file + class together, per repo rules.

- [ ] `public async Task LoadAsync()` — inside, in order:
      1. `Addressables.LoadAssetAsync<GameObject>("GemItem")` → await → verify status.
      2. Create the pool: `new MonoBehaviourPool<Item>()` (Lesson 3) with
         `Init(prefab, 32)`.
      3. Inject into the factory: `_itemFactory.Init(prefab, pool)`.
      4. Keep the existing `ISaveSystem` → `ScoreController` wiring.
- [ ] No `async void`. Entry point is `void Start() => _ = LoadAsync();`.
- [ ] Gameplay must not start before services are ready: expose
      `public event Action OnServicesReady;` (or a `Task` property) —
      `GameManager`/`LevelManager` waits for it before loading the first level.
      Failure path: `Debug.LogError(handle.OperationException.Message)` and don't fire.
- [ ] `// SERVICE LOADER DESIGN:` comment block: assets loaded (GemItem prefab), objects
      created (`MonoBehaviourPool<Item>`, `PlayerPrefsSaveSystem`), injections
      (prefab+pool → `ItemFactory`, save system → `ScoreController`).

## Phase 4.3 — Layered architecture (assignment Part 3)

New class `ItemFactory.cs` (in `Scripts/Core/`) slots between the managers and the pool.
It replaces direct pool access from Lesson 3's `ItemPoolManager` (fold that class into
factory + pool, or keep it as the manager that owns the factory — decide during
implementation; the constraint set below is what matters).

Diagram (goes in `docs/ARCHITECTURE.md` as ASCII + a comment block in `GameManager.cs`):

```
┌─────────────────────────────────────────────────────────────┐
│ 1. CONTROLLER — GameManager / Bootstrap / ServiceLoader     │
│    Owns the game lifecycle; holds ONLY manager references.  │
└───────────────┬─────────────────────────────────────────────┘
                │ starts levels / injects services
┌───────────────▼─────────────────────────────────────────────┐
│ 2. MANAGERS — LevelManager, GridManager, MergeManager,      │
│    InputHandler, UIManager                                  │
│    Game rules: grid layout, merge logic, input, score.      │
└───────────────┬─────────────────────────────────────────────┘
                │ factory.Get() / factory.Release()  (never Instantiate)
┌───────────────▼─────────────────────────────────────────────┐
│ 3. FACTORY — ItemFactory                                    │
│    Creates configured Items; single Init(prefab, pool);     │
│    no game logic.                                           │
└───────────────┬─────────────────────────────────────────────┘
                │ pool.Get() / pool.Release()
┌───────────────▼─────────────────────────────────────────────┐
│ 4. SERVICES/DATA — MonoBehaviourPool<Item>, Addressables,   │
│    ISaveSystem, GemConfig / LevelData ScriptableObjects     │
│    Engine-facing plumbing and data; knows nothing of rules. │
└─────────────────────────────────────────────────────────────┘
```

Hard constraints from the assignment, mapped to our classes:
- [ ] `GridManager`/`MergeManager` (the "TileManager" equivalents) call
      `_itemFactory.Get()` — grep confirms zero `Instantiate` for Items in layer 2.
- [ ] `ItemFactory` has a single `Init(GameObject prefab, IPool<Item> pool)` and no game
      logic (no tier math, no scoring, no grid knowledge).
- [ ] `GameManager` (the "GameController" equivalent) holds only manager references —
      not the factory, not the pool, not the prefab.
- [ ] Each layer in the diagram has its one-sentence responsibility (see boxes above).
- [ ] Diagram submitted as `docs/ARCHITECTURE.md` (ASCII counts per the assignment).

## Phase 4.4 — Bonus

- [ ] Second Addressables group **`SeasonalSkins`** containing at least one alternate gem
      sprite set (reuse the Canva pipeline from `docs/ASSET_WORKFLOW.md` to generate a
      seasonal variant of a few tiers).
- [ ] `private string _activeSkinKey = "GemItem";` on the ServiceLoader + a
      `SwapSkinAsync(string key)` method that releases the old handle, loads the new key,
      and re-Inits the factory.
- [ ] `missions_done.md` + `missions_to_do.md` at repo root, ≥5 entries each (seed
      missions_done from the git log: grid, merge mechanic, gem art, backgrounds, menu,
      pooling; seed missions_to_do from this roadmap: addressables, skins, audio, save
      slots, juice/VFX).

## Done =
All checkboxes green → push branch → `gh pr create` → submit branch link.

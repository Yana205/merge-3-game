# Lesson 3 Plan — Caching, Object Pooling & Generics

Branch: `feature/lesson3-pooling` (off `main`, after `feature/ui-menu` is merged).
Deliverable: branch link with cache audit, `IPool<T>` + `MonoBehaviourPool<T>`, and the
gem `Item` fully pooled. Profiler shows 0 B GC Alloc during merge/spawn cycles.

## Phase 3.1 — Cache Audit (assignment Part 1)

Real problems already identified in the codebase (need ≥2; we have 4):

1. **`InputHandler.GetMouseWorldPos()` — `Camera.main` every frame**
   (`Assets/_Project/Scripts/Input/InputHandler.cs:190`). Called from `Update()` on every
   drag frame plus pointer down/up. Fix: `private Camera _camera;` assigned in `Awake()`;
   replace the lookup.
2. **`GridManager.ClearGrid()` — `FindObjectsByType<Item>` scene scan**
   (`Assets/_Project/Scripts/Core/GridManager.cs:54`). Fix: `GridManager` keeps a
   `private readonly List<Item> _liveItems` — `SpawnItem`/merge add, release removes —
   and `ClearGrid` iterates that list instead of scanning the scene. (This list becomes
   the pool's bookkeeping in Phase 3.3.)
3. **`Cell.CreateSquareSprite()` — a new 64×64 `Texture2D` per cell**
   (`Assets/_Project/Scripts/Core/Cell.cs:29`). A 5×5 grid creates up to 25 identical
   textures; `Item.GetWhiteSquare()` already shows the right pattern. Fix: cache in a
   `static Sprite _sharedSquare` exactly like `Item._whiteSquare`.
4. **`InputHandler` pointer handlers — `GetComponent<Item>/<Cell>` per hit**
   (`InputHandler.cs:44,87,89`). Fix: `col.TryGetComponent(out Item item)` — cleaner and
   avoids the null-path allocation in the editor.

Also fixable if `feature/ui-menu` is merged first: `BackgroundFitter.Fit()` re-fetches
`Camera.main` and `GetComponent<SpriteRenderer>()` on every call — cache both in `Awake()`.

Checklist:
- [ ] Fix all four (field + assign in `Awake`/`Start` + replace usages).
- [ ] `// CACHE AUDIT` comment block at the top of `InputHandler.cs`, `GridManager.cs`,
      `Cell.cs` listing what was fixed and where.
- [ ] Note in the audit block: no `new WaitForSeconds` in loops exists in this project
      (searched; nothing to fix in that category).
- [ ] Commit: `refactor: cache audit — camera, scene scans, shared sprites`

## Phase 3.2 — IPool\<T\> + MonoBehaviourPool\<T\> (assignment Part 2)

New folder `Assets/_Project/Scripts/Core/Pooling/` (one class per file, per repo rules):

- `IPool.cs` — exactly as specified:
  `public interface IPool<T> { T Get(); void Release(T item); }`
- `MonoBehaviourPool.cs` —
  `public class MonoBehaviourPool<T> : IPool<T> where T : Component`
  - `Init(GameObject prefab, int count)` — instantiates `count` inactive instances under a
    dedicated `_poolRoot` transform, pushes them onto a `Stack<T>`. `Debug.LogError` if the
    prefab lacks a `T` component.
  - `Get()` — pops (or instantiates a new one when empty → *dynamic* pool), activates,
    returns `T`.
  - `Release(T item)` — deactivates, reparents under `_poolRoot`, pushes back. Guard
    against double-release.

Learning focus: generic constraints (`where T : Component` is what allows
`Instantiate(prefab).GetComponent<T>()` and `item.gameObject`), interface-vs-concrete
ownership (consumers see only `IPool<Item>`).

Checklist:
- [ ] `IPool.cs`, `MonoBehaviourPool.cs` compile with no game-specific logic inside.
- [ ] Commit: `feat: generic IPool<T> and MonoBehaviourPool<T>`

## Phase 3.3 — Pool the gem Item (assignment Part 3)

The frequently-spawned object: **the gem `Item`** — 1 spawned per successful move
(`InputHandler.SpawnAndCheckGameOver`), 2 destroyed + 1 spawned per merge
(`MergeManager.TryMerge`). Not bullets. ✓

- `ItemPoolManager.cs` (in `Scripts/Core/`) — MonoBehaviour, owns
  `private IPool<Item> _pool` (a `MonoBehaviourPool<Item>`), `[SerializeField] GameObject
  itemPrefab`, `[SerializeField] int prewarmCount = 32`. Exposes `Item Get(Vector3 pos)`
  and `Release(Item item)`.
- Pool size reasoning (goes in the comment block): 5×5 grid = 25 cells max on board, +1
  transient during a merge swap → 32 prewarmed; **dynamic** growth allowed so bigger
  `LevelData` grids (e.g. 6×6) never break.
- `Item.ResetForPool()` — resets ≥3 fields on release: `Tier` → 0, `GemData` → null,
  `spriteRenderer.sprite` → null (color → white), `tierLabel.text` → "".
- Rewire the three call sites — after this, **no gameplay code calls
  `Instantiate`/`Destroy` for Items**:
  - `GridManager.SpawnItem` (`GridManager.cs:98`) → `_itemPoolManager.Get(...)`
  - `MergeManager.TryMerge` (`MergeManager.cs:30-34`) → 2× `Release`, 1× `Get`
  - `GridManager.ClearGrid` (`GridManager.cs:56`) → `Release` each live item
    (keep `SafeDestroy` only for the edit-mode/`DestroyImmediate` path — the pool is
    play-mode only).
- Scene wiring (user, in Unity Editor): add `ItemPoolManager` to the managers GameObject,
  assign the Item prefab, point `GridManager`/`MergeManager` at it.
- `// POOL DESIGN:` comment block in `ItemPoolManager.cs`: what is pooled, size 32 +
  reasoning, dynamic choice + why, the 3+ fields reset on Release.

Checklist:
- [ ] All three call sites rewired; grep confirms no `Instantiate(itemPrefab)` remains.
- [ ] `// POOL DESIGN:` block complete.
- [ ] Commit: `feat: pool gem items via ItemPoolManager`

## Phase 3.4 — Profiler verification (bonus)

- Window → Analysis → Profiler, CPU module, play a level, perform ~10 merges.
- Confirm `GC.Alloc` is 0 B during pool cycles (spawn/merge frames).
- Known allocator to watch: `tier.ToString()` in `Item.Setup` — if it shows up, cache the
  20 tier label strings in `GemTierTable` (tiers are fixed 1–20).
- Screenshot the Profiler frame → `docs/lesson3_profiler.png`, reference it in the PR.

## Done =
All assignment checkboxes green → push branch → `gh pr create` → submit branch link.

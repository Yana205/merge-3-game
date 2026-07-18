# Game Architecture — Merge-3

Four layers; dependencies point strictly downward. The top-level controller never
touches the pool, the prefab, or the factory internals — it holds manager references
only. (Lesson 4 assignment, Part 3.)

```
┌──────────────────────────────────────────────────────────────────┐
│ 1. CONTROLLER — ServiceLoader, GameManager, Bootstrap            │
│    Owns startup and injection. Loads the GemItem Addressable,    │
│    builds the pool + factory, injects them down, fires           │
│    OnServicesReady. Holds ONLY manager references.               │
└───────────────┬──────────────────────────────────────────────────┘
                │ injects factory / ISaveSystem; signals OnServicesReady
┌───────────────▼──────────────────────────────────────────────────┐
│ 2. MANAGERS — LevelManager, GridManager, MergeManager,           │
│    InputHandler, UIManager, MenuController, ScoreController      │
│    All game rules: level loading, grid layout, merge logic,      │
│    input, score, menus.                                          │
└───────────────┬──────────────────────────────────────────────────┘
                │ factory.Get(position) / factory.Release(item) — never Instantiate
┌───────────────▼──────────────────────────────────────────────────┐
│ 3. FACTORY — ItemFactory                                         │
│    Hands out configured Items. Exactly one Init(prefab, pool);   │
│    no game logic, no Instantiate of its own.                     │
└───────────────┬──────────────────────────────────────────────────┘
                │ pool.Get() / pool.Release() ; Addressables handle.Result
┌───────────────▼──────────────────────────────────────────────────┐
│ 4. SERVICES & DATA — MonoBehaviourPool<Item>, Addressables,      │
│    ISaveSystem (PlayerPrefs/Json), GemConfig, LevelData          │
│    Engine-facing plumbing and pure data; knows nothing of rules. │
└──────────────────────────────────────────────────────────────────┘
```

Layer responsibilities in one sentence each:

1. **Controller** — boots the game: asynchronously loads assets, constructs services, injects them into managers, and signals readiness.
2. **Managers** — implement every game rule and react to player input; they request objects from the factory and never create or destroy Items directly.
3. **Factory** — the single seam between rules and object lifetime: `Init(prefab, pool)` once, then `Get`/`Release`.
4. **Services & Data** — pooling, asset loading, persistence, and ScriptableObject data; reusable and game-logic-free.

Spawn path end to end: `ServiceLoader.LoadAsync` → `Addressables.LoadAssetAsync<GameObject>("GemItem")` → `MonoBehaviourPool<Item>.Init(prefab, 32)` → `ItemFactory.Init(prefab, pool)` → `GridManager.SetItemFactory` → gameplay calls `factory.Get()` / `factory.Release()`.

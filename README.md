# Land of the Lustrous

A crystal match-3 built in Unity 6 with Claude Code driving the Unity Editor through Unity MCP. Every mechanic, shader, UI screen and most of the art pipeline was produced from the course lessons below, one lesson per branch, reviewed and merged by PR.

**Play it:** https://yanaifraimov.itch.io/lustrous

<img width="780" height="492" alt="Land of the Lustrous board" src="https://github.com/user-attachments/assets/442b20ff-90c1-4bfc-a21a-b7b3376d1967" />

**How to play:** swap two neighbouring gems (tap-tap or swipe) to line up three or more. Matches pop, gems fall, new ones drop in from above, and every cascade raises the combo. Red eye-crystal stones block cells; match beside one twice to shatter it. The run is endless, your best score goes on the leaderboard.

## Progress

| Now | Before |
|---|---|
| <img width="400" alt="current build" src="https://github.com/user-attachments/assets/2fc4f0bb-0c7b-4011-8176-7c435a5cb06d" /> | <img width="400" alt="first playable" src="https://github.com/user-attachments/assets/1fa86576-5cb1-4d21-b238-472385f34f80" /> |

## What we took from the SBS course

| Course topic | What it became in this game | Where |
|---|---|---|
| C# events and the Observer pattern | A static `GameEvents` bus (score, match, stone, restart signals) plus a direct parent-to-child event on pooled gems. Gameplay never calls UI or audio directly. | `Scripts/Core/GameEvents.cs`, `Item.OnDespawned` |
| UI Toolkit HUD | The crystal score card: UXML layout, USS with type / id / class selectors, `Q<T>()` queries, a count-up score with a USS punch transition, restart and audio toggles. | `UIToolkit/GameHUD.uxml`, `GameHUD.uss`, `Scripts/UI/UIController.cs` |
| Object pooling and the cache audit | Gems come from a prewarmed `MonoBehaviourPool<Item>` behind an `ItemFactory`; no `Instantiate`/`Destroy` in play, no per-cell texture allocations. | `Scripts/Core/Pooling/`, `ItemFactory.cs`, `GridManager.cs` |
| Addressables, service loading and layered architecture | `ServiceLoader` loads the gem prefab asynchronously, builds pool + factory + save system and injects them downward. Four strict layers: controller, managers, factory, services. | `Scripts/Core/ServiceLoader.cs`, `docs/ARCHITECTURE.md` |
| URP HLSL shader: the "magical" surface | `MagicalCrystal`: procedural noise, scrolling UVs, base-to-glow blend, breathing alpha on every board cell. | `Shaders/MagicalCrystal.shader` |
| Shader effects and runtime control | A ShaderToy plasma ported to HLSL as the cavern aura, with a C# controller driving `_Intensity` through `renderer.material` and cleaning it up. | `Shaders/CrystalAura.shader`, `Scripts/FX/CrystalAuraController.cs` |
| Data-driven design with ScriptableObjects | Gem tiers, sprites and score values live in `GemConfig`; a `GemDefinition` / `GemDatabase` family with weighted random, JSON import and an editor validator. | `Scripts/Data/`, `Data/GemConfig.asset`, `Scripts/Editor/GemDataTools.cs` |
| Game feel: juice, particles, audio | Animated pop / fall / refill cascade, spring swaps, camera shake, spark bursts, floating score, combo popups, pitch-climbing match sound, music and SFX toggles. | `Scripts/Core/MergeManager.cs`, `Scripts/FX/JuiceDirector.cs`, `Scripts/FX/AudioDirector.cs` |
| AI as director, not typist | Claude Code with project skills (`.claude/skills/`) drives the Editor over Unity MCP: scene wiring, prefab edits, play-mode tests and screenshots, one lesson per branch with a self-review gate. | `.claude/skills/lesson-loop`, `docs/course/` |

## Each concept in one sentence

- **Event bus / Observer:** systems publish what happened and anyone can listen, so scoring, UI, audio and effects react to a match without the board knowing they exist.
- **Direct events with safe unsubscribe:** a pooled gem raises its own `OnDespawned` and clears its listeners when it returns to the pool, so recycled objects never fire stale handlers.
- **UI Toolkit:** the HUD is declared in UXML, styled in USS like a web page, and bound from C# by querying element names, keeping layout out of code.
- **Object pooling:** gems are created once, parked when cleared and reused, which removes allocation spikes and garbage-collector hitches during cascades.
- **Factory and dependency injection:** managers ask a factory for gems and a loader hands each system what it needs at startup, so no gameplay class builds its own dependencies.
- **Addressables:** the gem prefab is loaded by key at runtime instead of being hard-referenced, which is how content can be swapped or streamed later.
- **Layered architecture:** dependencies point one way, from controller to managers to factory to services, so any layer can change without touching the ones above it.
- **Procedural HLSL shader:** the crystal glow is computed from noise and time on the GPU with no texture, giving animated surfaces for free.
- **Runtime material control:** C# writes a shader property every frame through an instanced material and destroys it on teardown, so effects respond to gameplay without leaking memory.
- **ScriptableObjects and data-driven design:** gem stats are assets designers edit in the Inspector, and a JSON pipeline with validation bulk-creates them without code changes.
- **Game feel (juice):** small timed exaggerations such as squash on landing, shake on big matches and rising pitch on combos make the same rules feel far more satisfying.
- **Unity MCP with Claude Code:** the AI agent operates the real Editor to wire scenes, run play tests and take screenshots, so changes are verified in the game, not just in text.

## Development workflow

Work happens on `feature/`, `fix/` or `refactor/` branches and lands on `main` through a PR. Never commit to `main` directly. The `working-game-baseline` tag marks the last known-good state:

```
git checkout -b feature/<short-description>
git push -u origin feature/<short-description> && gh pr create --base main
```

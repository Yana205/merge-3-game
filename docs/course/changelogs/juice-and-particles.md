# Juice & Particles Polish (presentation pass)

- **Branch:** `feature/juice-and-particles`
- **Status:** ✅ DONE
- **Date:** 2026-07-19

## Summary

The final "make it whole for presentations" pass. Adds a code-driven `JuiceDirector`
that reacts to merges with a camera shake, a scale-punch on the merged crystal, and a
tinted spark burst — all decoupled through the Lesson 1 `GameEvents.TileMerged` bus, so
no gameplay system references the juice. Combined with the shader work from Lessons 3–4,
the board now reads as alive and responsive without any new hard dependencies.

## What was added

- **`Scripts/FX/JuiceDirector.cs`** — one self-contained component. On `TileMerged`:
  - **Camera shake** — brief eased `Camera.main` offset (unscaled time).
  - **Scale punch** — a single sine arch on the merged tile's `localScale` (pooled-safe:
    restores base scale and bails if the item despawns mid-punch).
  - **Spark burst** — `sparkCount` procedural sprite sparks fly outward, shrink, and
    fade, tinted by the merged gem's colour. Uses the project's generated-white-square
    technique (as in `Item.cs`), so it needs no imported texture or ParticleSystem asset.
  - Everything is toggleable + tunable in the Inspector; subscribes in `OnEnable`,
    unsubscribes in `OnDisable` (Lesson 1 discipline).

## How the whole game now feels alive (cross-lesson)

- **Cells:** `MagicalCrystal` shader (Lesson 3) makes every crystal cell scroll,
  shimmer, and breathe.
- **Background aura:** `CrystalAura` + `CrystalAuraController` (Lesson 4) glow behind the
  board and **flare on each merge** (also via `GameEvents.TileMerged`).
- **Merge moment:** `JuiceDirector` shake + punch + sparks (this pass).
- **HUD:** UI Toolkit HUD (Lesson 2) updates live via `GameEvents.ScoreChanged`.
- The despawn hook `Item.OnDespawned` (Lesson 1) remains available for per-tile effects.

## Editor steps

Pure code — no assets to create. To enable:
1. Add the **JuiceDirector** component to an always-present GameObject (e.g. the
   `GridManager` object, or a new empty `_Juice`).
2. Ensure the scene has a `Camera.main` (tagged MainCamera) for the shake.
3. Enter Play mode and merge — shake + punch + sparks fire. Tune magnitudes/counts on
   the component. (For the aura flare and crystal shimmer, follow the Lesson 3/4 steps.)

## Review findings (self-review)

- `[ok]` Fully decoupled: only listens to `GameEvents.TileMerged`; nothing references
  `JuiceDirector`. Subscribe/unsubscribe balanced (`OnEnable`/`OnDisable`).
- `[ok]` Pooled-safe punch: guards `activeInHierarchy` and always restores base scale.
- `[ok]` No leaked material/texture beyond the one shared static spark sprite (same
  pattern the project already uses for items); sparks self-destroy after their lifetime.
- `[note]` Spark visuals can't be eyeballed headless; magnitudes are conservative
  defaults and fully Inspector-tunable.

## Verification

Static: event wiring balanced; coroutines guarded against pooled-object reuse and
null cameras. Runtime (user): after adding the component, each merge shakes the camera,
punches the new crystal, and throws a colored spark burst — layered over the animated
crystal cells and the merge-reactive aura for a presentation-ready feel.

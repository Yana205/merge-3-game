# Lesson 4 — Shader Effects (ShaderToy port + runtime controller)

- **Branch:** `feature/lesson-04-shader-effects`
- **Status:** ✅ DONE (Shader Graph part = documented Editor build steps)
- **Date:** 2026-07-19

## Summary

Two crystal-themed shader deliverables plus a runtime controller, using the
Director-vs-Typist mindset: I designed the visual goal, AI handled HLSL syntax, I
verified/adjusted. A **ShaderToy plasma** is ported to URP HLSL (`CrystalAura`), and
a C# `CrystalAuraController` drives its `_Intensity` at runtime through
`renderer.material`, flaring on each merge via the Lesson 1 bus. The **Shader Graph**
effect genuinely needs the Editor (a `.shadergraph` can't be hand-authored safely),
so exact build steps are documented below.

## Deliverable → code map

| Assignment deliverable | Where it lives |
|------------------------|----------------|
| Shader Graph effect (noise + Time → Alpha/Base Color) | **Editor build steps below** (⏸ needs Editor) |
| ShaderToy GLSL → Unity HLSL port, running in scene | `Shaders/CrystalAura.shader` |
| C# controls a shader property at runtime via `renderer.material` | `Scripts/FX/CrystalAuraController.cs` — `_mat = _renderer.material; _mat.SetFloat("_Intensity", …)` |
| `Destroy(_mat)` in `OnDestroy()` | `CrystalAuraController.OnDestroy` |
| Director Notes for each shader | below |

## ShaderToy port

Source: the classic public-domain ShaderToy "plasma" (four summed `sin` waves →
cosine palette). Port changes: `iResolution`/`fragCoord` dropped (URP UV is already
0–1), `iTime` → `_Time.y`, the fixed `cos()` palette replaced by two Inspector tints,
and a new `_Intensity` uniform exposed so C# can push it. Header of
`CrystalAura.shader` keeps the original GLSL for reference.

## Runtime controller

`CrystalAuraController` (on the aura quad's Renderer):
- `Awake`: `_mat = renderer.material;` — a per-object **instance** (not `sharedMaterial`),
  which we now own.
- `Update`: `_Intensity = base + idle sin + decaying merge pulse` → `_mat.SetFloat(...)`.
- `GameEvents.TileMerged` (Lesson 1 bus) → sets the pulse, so the aura flares on merges.
- `OnEnable`/`OnDisable` subscribe/unsubscribe; `OnDestroy` calls `Destroy(_mat)`.

## Director Notes

**CrystalAura (ShaderToy port).** *Goal:* a soft, magical energy field that lives
behind the board and reacts when the player merges — brighter/wider on a merge, then
settling. *AI ask:* "port this ShaderToy plasma to URP HLSL, expose two tint colors,
a speed, a scale, and an `_Intensity` I can set from C#." *Fixed manually:* remapped
the `[-4,4]` wave sum to `[0,1]`, used `pow(w, 2 - _Intensity)` so intensity widens
the bright regions instead of just scaling brightness, and drove alpha from the wave
so the field reads as translucent energy, not a solid quad.

**Crystal Sparkle (Shader Graph, build in Editor).** *Goal:* twinkling motes drifting
over the board. *Plan:* Time → tiling/offset of a Gradient/Simple Noise node → power →
Alpha with Alpha Clip so only the bright motes show. *Why Shader Graph here:* it's the
fastest way to iterate a noise-threshold look visually, and the assignment asks
specifically for a graph.

## Shader Graph — Editor build steps (⏸ can't be authored headless)

1. **Assets ▸ Create ▸ Shader Graph ▸ URP ▸ Unlit Shader Graph**, name it `CrystalSparkle`.
2. Graph Settings: Surface = **Transparent** (or keep Opaque + enable **Alpha Clip**).
3. Add a **Time** node → **Multiply** by a `Vector1` property `Speed` → feed into a
   **Tiling And Offset** node's *Offset* (this scrolls the pattern).
4. Add a **Simple Noise** node; wire Tiling And Offset → its *UV*; expose a `Scale` property.
5. **Power** the noise (exponent ~4) to sharpen it into sparkles → **Multiply** by a
   `Color` property `SparkleTint`.
6. Connect the sharpened noise to **Alpha**, set **Alpha Clip Threshold** ~0.6 (only
   bright motes render); connect the tinted color to **Base Color**.
7. Save. Create a material from it and drop it on a quad above the board.

## AI assistance

The HLSL plasma port and the intensity-remap math were AI-generated, then verified
and tuned by hand (the `pow(w, 2-_Intensity)` mapping and the alpha-from-wave were my
adjustments after seeing the raw port read too flat).

## Review findings (self-review)

- `[ok]` Controller uses `renderer.material` (instance), not `sharedMaterial`, and
  `Destroy(_mat)` in `OnDestroy` — no leaked material.
- `[ok]` `TileMerged` subscribed in `OnEnable`, unsubscribed in `OnDisable` (Lesson 1 discipline).
- `[ok]` Shader transparent state + CBUFFER complete; `_Intensity` in `Properties` and CBUFFER.
- `[note]` Shader Graph deliverable is documented build steps (Editor-only); everything
  else is running code.

## Verification

Static: shader compiles structurally (properties ↔ CBUFFER ↔ frag), controller sets a
real property id via `Shader.PropertyToID`. Runtime (user): put the `CrystalAura`
material on a quad behind the board, add `CrystalAuraController`, enter Play mode →
the aura animates and flares on each merge; the material instance is freed on scene exit.

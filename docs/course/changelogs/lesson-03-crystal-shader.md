# Lesson 3 — Crystal Magical Shader (URP HLSL)

- **Branch:** `feature/lesson-03-crystal-shader`
- **Status:** ✅ DONE
- **Date:** 2026-07-19

## Summary

The "Magical Water Surface" assignment, retimed for our theme: a URP HLSL shader
`Merge3/MagicalCrystal` that makes the merge-3 **crystal cells** feel alive —
scrolling procedural noise, an Inspector-tinted base→glow blend, breathing
transparency, plus ripple, pulse, and vignette. No external texture; everything is
procedural. It's additive: a new shader + a generated material you opt into per cell.

## Deliverable → code map

| Assignment deliverable | Where it lives |
|------------------------|----------------|
| Transparent setup (tags, `Blend SrcAlpha OneMinusSrcAlpha`, `ZWrite Off`) | `Shaders/MagicalCrystal.shader` SubShader block |
| ≥4 Inspector properties (base color, glow color, scroll speed, noise scale) | `Properties{}` — `_BaseColor`, `_GlowColor`, `_ScrollSpeed`, `_NoiseScale` (+7 more) |
| Procedural HLSL noise (no texture) | `hash2` / `valueNoise` / `fbm` in the HLSL block |
| Scrolling UVs (`_Time.y * _ScrollSpeed` on Y) → noise | `frag`: `nUV.y -= t * _ScrollSpeed; fbm(nUV * _NoiseScale)` |
| Noise → base/glow blend via `smoothstep`/`lerp` | `smoothstep(0.35,0.75,n)` then `lerp(_BaseColor,_GlowColor,blend)` |
| Dynamic (breathing) alpha | `alpha = (_AlphaBase + breathe) * (…n…) * vignette`, `breathe = sin(...)` |
| ≥1 extra animated property (own design) | ripple (`_RippleSpeed`/`_RippleStrength`), pulse (`_PulseSpeed`/`_PulseStrength`), vignette (`_VignetteStrength`) |
| Material with sensible defaults | `Scripts/Editor/CrystalMaterialSetup.cs` creates `MagicalCrystal.mat` |
| Assign to a quad/sprite in scene | `Tools ▸ Merge3 ▸ Apply Crystal Material to Selection` |

## Animated / interactive effects (≥5)

1. **Scrolling noise** — `nUV.y -= _Time.y * _ScrollSpeed` before sampling `fbm`.
2. **Base→glow color blend** — `smoothstep` on the noise drives `lerp` between the two Inspector colors.
3. **Breathing alpha** — `sin(t * _PulseSpeed*0.5)` modulates transparency.
4. **Pulsing brightness** — `1 + sin(t * _PulseSpeed) * _PulseStrength` multiplies color.
5. **Ripple distortion** — `uv.x += sin(uv.y*12 + t*_RippleSpeed) * _RippleStrength`.
6. **Edge vignette** — radial `smoothstep` darkens + fades the cell rim.

## Extra animated property — written note (assignment)

> My extra property is the **ripple distortion** (`_RippleStrength` / `_RippleSpeed`).
> It offsets each row's horizontal UV by a sine wave over time, so the crystal
> surface looks like light bending through moving facets rather than a flat scroll.
> I chose it because a static gem cell reads as "dead"; the subtle horizontal shimmer
> sells the "enchanted crystal" fantasy and pairs with the vignette to keep the eye
> on the gem in the center. Strength is kept small (≤0.2, default 0.03) so it never
> distracts from readability during a merge.

## Editor steps (headless can't create the .mat / wire the scene)

1. Open the project in Unity so it imports/compiles `MagicalCrystal.shader`.
2. **Tools ▸ Merge3 ▸ Create Crystal Material** → creates `Shaders/MagicalCrystal.mat`
   with sensible crystal defaults.
3. Select the cell GameObject(s) (or a Quad/large Sprite) and run
   **Tools ▸ Merge3 ▸ Apply Crystal Material to Selection**.
4. Enter Play mode to watch it animate (`_Time`-driven; visible only in Play/anim).

## AI assistance (assignment allows generating the noise with AI)

The value-noise function (`hash2` + smooth-interpolated `valueNoise`, wrapped in a
2-octave `fbm`) was AI-generated and pasted above the vertex function, then adapted:
switched the hash to a signed-gradient form for smoother results and added the second
octave so the shimmer isn't obviously tiled.

## Review findings (self-review)

- `[ok]` Transparent render state complete: `RenderType/Queue=Transparent`,
  `Blend SrcAlpha OneMinusSrcAlpha`, `ZWrite Off`, `Cull Off`.
- `[ok]` Every property in `Properties{}` is declared in `CBUFFER_START(UnityPerMaterial)`
  (SRP Batcher-compatible) and used in `frag`.
- `[ok]` No texture sample; noise is fully procedural. `_Time.y` used for all animation.
- `[note]` Uses URP `Core.hlsl` + `TransformObjectToHClip`; SubShader tags
  `RenderPipeline=UniversalPipeline`. Requires the URP package (present in this project).
- `[note]` `.shader` is authored as a NEW plain-text ShaderLab file (the lesson's
  deliverable); the serialized `.mat` is generated via the Editor API, never hand-edited.

## Verification

Static: shader structure validated (properties ↔ CBUFFER ↔ frag usage), URP include
path correct, all animation reads `_Time.y`. Runtime (user): after the Editor steps,
the crystal cells scroll/shimmer/breathe in Play mode; tweaking `_GlowColor` or
`_ScrollSpeed` on the material updates the look live.

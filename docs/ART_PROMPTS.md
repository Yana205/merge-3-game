# Art Prompt Pack — Gemini image generation

Prompts for regenerating the game's art at a higher visual bar. Written for
Gemini image generation ("Nano Banana" / Gemini image models), but they work in
any diffusion tool.

Read `docs/ASSET_WORKFLOW.md` for how art gets into Unity. This file is only
about *what to ask for*.

---

## 0. Why the current art reads flat

From the menu + gameplay screenshots:

| Problem | Layer | Fix |
|---|---|---|
| Background, cell tiles and gems all sit in the same mid-dark blue-purple value band | all | Assign each layer a **separate brightness range** (below) and enforce it in every prompt |
| Gems are near-black octagons on dark tiles — tier 1 is invisible | gems | Every gem needs a bright rim-light + light interior facet, even the "dark" tiers |
| Cell tiles are noisy blue blobs with no border | board | Cells need a defined frame, flat interior, low noise |
| Grid floats in space with no container | board | Add a board frame / tray sprite behind the grid |
| Title logo is upscaled pixel text, jaggy and blurry | UI | Regenerate at native resolution, no upscaling |
| Buttons are flat solid rectangles in 3 unrelated hues (teal/purple/red) | UI | One button family, tinted per role, 9-slice safe |
| Gem PNGs have mismatched native sizes (183px vs 206px) | gems | Author every gem on the **same** canvas |

### The value ladder (the single most important rule)

Put this in every prompt. It is what makes a merge board readable:

- **Background**: 10–30% brightness, desaturated, low contrast, soft focus.
- **Board frame / cell tiles**: 25–45% brightness, low saturation, crisp edges.
- **Gems**: 55–100% brightness, high saturation, strong rim light. Always the
  brightest thing on screen.
- **UI text / HUD**: near-white or gold, highest contrast, always on top of a
  darkened plate.

---

## 1. The Style Bible block (prefix EVERY prompt with this)

Consistency across a 30-asset order comes from reusing one identical style
paragraph. Copy this verbatim at the top of each generation:

```
STYLE BIBLE — reuse exactly:
Hand-crafted pixel art, 16-bit SNES-era JRPG quality, crystal-cavern fantasy theme.
Limited palette of 32 colours. Clean 1px dark outlines (deep indigo #1B1235, never pure black).
Dithered gradients only where needed — no smooth airbrush blends, no anti-aliased soft edges.
Lighting: single key light from the upper-left, cool violet ambient bounce from below,
warm cyan rim-light on the upper-left silhouette edge.
Core palette: deep indigo #1B1235, night violet #2E1F53, dusk purple #4B3A82,
crystal cyan #6EE6E0, pale mint highlight #CFF8F2, warm gold accent #F5C542,
soft magenta glow #C77DFF.
Rendered at native pixel resolution — NO upscaling, NO blur, NO JPEG artefacts,
every pixel intentional and hard-edged.
```

### Transparency

Gemini is unreliable at true alpha. Ask for a **flat chroma background** and key
it out with Pillow (you already have that pipeline):

```
Background: solid flat pure magenta #FF00FF, completely uniform, no shadow,
no vignette, no gradient, no glow bleeding into the background.
Subject fully inside frame with 8px of empty margin on all sides.
```

Then key with:

```python
from PIL import Image
im = Image.open(src).convert("RGBA")
px = im.load()
for y in range(im.height):
    for x in range(im.width):
        r, g, b, _ = px[x, y]
        if r > 200 and b > 200 and g < 60:
            px[x, y] = (0, 0, 0, 0)
im.save(dst)
```

---

## 2. Gems — the 10 live tiers

The live ladder is `GemTierTable.cs` (10 tiers, maximally-separated hues).
Order **exactly these ten**, all on the **same 128×128 canvas**:

| Tier | Name | Hex | Silhouette (must differ per tier) |
|---|---|---|---|
| 1 | Obsidian | `#2B2B33` | rough tumbled pebble, chipped |
| 2 | Ruby | `#E7263C` | classic round brilliant cut |
| 3 | Emerald | `#16C25A` | rectangular step-cut (emerald cut) |
| 4 | Lapis | `#2E6BFF` | flat hexagonal cabochon |
| 5 | Citrine | `#FFC531` | tall pointed teardrop / pear cut |
| 6 | Amethyst | `#9B3CE0` | cluster of three raw hexagonal spires |
| 7 | Turquoise | `#17D9D0` | smooth ovoid cabochon with veining |
| 8 | Carnelian | `#FF6A1A` | marquise / pointed oval |
| 9 | Rhodochrosite | `#FF3DAE` | banded heart-shaped cut |
| 10 | Diamond | `#EAF6FF` | radiant star-cut with 8-point flare |

> **Silhouette matters more than colour.** Colour-blind players and small
> screens both kill hue distinction. If a player can identify the tier from a
> black-and-white thumbnail, the set is good.

### Per-gem prompt template

```
[STYLE BIBLE BLOCK]

Subject: a single {NAME} gemstone, {SILHOUETTE}, facing the viewer, centred.
Base colour {HEX}, with a lighter tint of the same hue on the upper-left facets
and a darker shade of the same hue on the lower-right facets.
Faceted interior: 4-7 clearly separated flat facet planes, each a distinct
solid shade — hard edges between facets, no gradient blending across a facet.
One small pure-white specular highlight (3-5 px, sharp-edged, upper-left facet).
A thin crystal-cyan #6EE6E0 rim-light tracing the upper-left silhouette edge.
A subtle inner glow of the gem's own colour near the centre.
No pedestal, no ground shadow, no sparkle particles, no text, no numbers.
Canvas exactly 128x128 pixels, gem occupies ~104x104 centred.
[TRANSPARENCY BLOCK]
```

Fill `{NAME}`, `{SILHOUETTE}`, `{HEX}` from the table.

### Tier 1 needs special handling

Obsidian at `#2B2B33` is invisible on your dark board (visible in the
screenshot). Override its prompt:

```
Subject: a rough obsidian pebble, dark charcoal #2B2B33 base, but rendered
LIGHT — the surface catches strong light: broad pale-grey #8A8AA0 highlight
planes covering roughly 40% of the visible face, a bright crystal-cyan rim-light
on the entire upper-left edge, and a cool violet bounce-light on the lower-right
edge. The stone must read clearly against a very dark background.
```

### One-shot sheet variant (cheaper, riskier)

```
[STYLE BIBLE BLOCK]

A sprite sheet of 10 fantasy gemstones arranged in a 5x2 grid on a
640x256 canvas, each gem centred in its own 128x128 cell with clear empty
margins — gems must NOT touch or overlap cell boundaries.
Reading left-to-right, top-to-bottom the gems are:
1 dark obsidian pebble, 2 red round-brilliant ruby, 3 green step-cut emerald,
4 blue hexagonal lapis cabochon, 5 yellow pear-cut citrine,
6 purple three-spire amethyst cluster, 7 cyan oval turquoise cabochon,
8 orange marquise carnelian, 9 pink banded heart rhodochrosite,
10 white radiant star-cut diamond.
Every gem: same rendering treatment, same light direction, same outline weight,
same size within its cell. Faceted, hard-edged, one white specular highlight each.
[TRANSPARENCY BLOCK]
```

Per-gem generation gives better quality and guaranteed identical canvas sizes;
the sheet gives guaranteed style consistency. Recommended: **generate the sheet
first as a style reference**, then generate each gem individually while passing
the sheet back as an image reference ("match this style exactly").

---

## 3. Board — cell tiles and frame

This is the highest-impact upgrade after the gems. The noisy blue blobs are
doing more damage than the gems are.

### Empty cell tile

```
[STYLE BIBLE BLOCK]

Subject: a single empty socket tile for a puzzle game grid — a square recessed
slot carved into dark polished stone.
Square, 96x96 pixels, filling the full canvas edge to edge (this tile will be
repeated in a grid, so the outer 2px border must tile seamlessly).
Interior: flat dark night-violet #2E1F53, very slightly darker toward the centre
to read as recessed. Almost no texture — at most a faint 2-colour dither.
Edges: a 3px chiselled bevel — the top and left inner edges 1 shade darker
(shadow, light comes from upper-left so the recess shadows there), the bottom
and right inner edges 1 shade lighter.
A 1px dusk-purple #4B3A82 outline around the whole tile.
Calm and quiet — this is a background element that must never compete with the
gem sitting on top of it.
No gem, no icon, no glow, no text.
[TRANSPARENCY BLOCK]
```

### Cell tile — highlight / valid-drop state

Same prompt with:

```
...the 1px outline is warm gold #F5C542 instead of dusk purple, and a soft
gold inner glow hugs the inner edge of the bevel. Interior still flat and dark.
```

### Board frame / tray (9-slice)

```
[STYLE BIBLE BLOCK]

Subject: an ornate rectangular frame for a puzzle game board — a carved
crystal-cavern stone tray. Only the frame border, the centre is empty.
512x512 pixels. Border thickness exactly 48px on all four sides, and the border
artwork must be IDENTICAL along each edge's midsection so the image can be
9-slice stretched — all decoration lives in the four corners only.
Corners: a small cluster of raised violet crystal shards growing out of the stone,
catching a cyan rim-light.
Edge midsections: plain chiselled dark stone, dusk purple #4B3A82, with a
1px pale mint #CFF8F2 highlight line along the top inner lip.
Interior region (the centre 416x416): fully transparent / flat magenta.
[TRANSPARENCY BLOCK]
```

---

## 4. UI panels and buttons

### Button base (9-slice, one family)

Generate **one** neutral button and tint it in Unity per role — that guarantees
the family matches, which the current teal/purple/red set does not.

```
[STYLE BIBLE BLOCK]

Subject: a single rectangular game UI button, empty — no text, no icon,
no label of any kind.
256x96 pixels. Rounded corners with a radius of 12px.
Designed for 9-slice stretching: the left and right 40px are the decorated caps,
the entire middle section is a uniform horizontal band that repeats identically
so it can stretch without distortion.
Surface: a smooth polished crystal plate, dusk purple #4B3A82 base, with a
lighter band across the upper 40% (a glassy top-light sheen) and a darker band
across the lower 25%.
A 2px pale mint #CFF8F2 outline around the whole shape.
A 1px darker inner shadow just inside the bottom edge to give it thickness.
Small angular crystal facet notches cut into the two short ends.
Flat and readable — a caption will be drawn on top of the middle, so the middle
must stay visually calm and uncluttered.
[TRANSPARENCY BLOCK]
```

Pressed state — same prompt plus:

```
...the button is in its PRESSED state: the whole plate is 15% darker, the top
sheen band is gone, and the inner shadow moves to the TOP edge as if the plate
has been pushed into the surface.
```

Disabled state — same prompt plus:

```
...the button is DISABLED: fully desaturated to grey-violet, 40% darker overall,
no sheen, no outline glow.
```

### HUD score plate (9-slice)

The current score box is a plain rounded rect with a stray "Restart" button, and
in the menu screenshot it **overlaps the main menu panel** — see §8.

```
[STYLE BIBLE BLOCK]

Subject: a UI panel plate for a game score readout — empty, no text, no numbers.
256x160 pixels, 9-slice safe: all decoration in the four corners, edges uniform.
A dark slab of polished obsidian, near-black indigo #1B1235, 85% opaque so the
background faintly shows through.
A 2px crystal-cyan #6EE6E0 border with a soft outer glow.
Top-left and top-right corners: a tiny four-point sparkle ornament in warm gold.
Interior: flat and very dark so bright text is legible on top of it.
No text, no numbers, no icons inside the panel.
[TRANSPARENCY BLOCK]
```

### Menu panel (large, 9-slice)

```
[STYLE BIBLE BLOCK]

Subject: a large vertical dialog panel for a fantasy game main menu — completely
empty inside, no buttons, no text.
512x640 pixels, 9-slice safe: decoration only in the corners and along the top
edge, side and bottom midsections uniform and repeatable.
Material: dark carved cavern stone, night violet #2E1F53, with a subtle
2-colour dither texture.
Border: a 6px chiselled stone frame in dusk purple with a 1px pale mint highlight
along the top lip and a 1px near-black shadow along the bottom lip.
Top edge centre: a decorative arch with a single embedded violet crystal, glowing
softly, flanked by two small gold filigree scrolls.
Bottom corners: small clusters of raw amethyst crystal growing inward.
Interior: flat, calm, uncluttered, slightly darker than the border so overlaid
buttons pop.
[TRANSPARENCY BLOCK]
```

---

## 5. Title logo

The current logo is upscaled pixel text — jaggy, blurry, and the "U" reads as a
"V". Generate at final display resolution and never scale it up.

```
[STYLE BIBLE BLOCK]

Subject: a game title logo reading exactly "LAND OF THE LUSTROUS" on two lines —
"LAND OF THE" on the first line in smaller letters, "LUSTROUS" on the second line
in much larger letters. Spell it precisely; the letter U must be a clear rounded
U, never a pointed V.
1600x600 pixels, rendered natively at this size — sharp, not upscaled.
Letterforms: bold chunky fantasy display type, letters carved from translucent
crystal — a crystal-cyan #6EE6E0 core with pale mint #CFF8F2 facet highlights on
the upper-left of each stroke and deep indigo #1B1235 on the lower-right.
Each letter has a clean 3px deep-indigo outline and a 4px hard drop shadow
offset down-right, plus a soft magenta #C77DFF outer glow.
A few small four-point gold sparkles scattered around the letters — sparse, they
must never sit on top of a letter and obscure it.
Slight downward arch on the second line.
[TRANSPARENCY BLOCK — pure magenta, and no magenta anywhere in the letters]
```

If the model keeps misspelling (common), generate the logo **without text** as a
crystal banner/plaque, and set the title in Unity with a TextMeshPro font +
material preset. That also lets you localise later.

---

## 6. Backgrounds (parallax layers)

Generate the game background as **three separate layers** rather than one flat
image — that alone gives you depth and a place to hang parallax and shader work.

### Layer A — far cavern wall

```
[STYLE BIBLE BLOCK]

Subject: the far back wall of a vast underground crystal cavern, seen head-on.
1920x1080 pixels, seamless left-to-right tiling.
VERY dark and low contrast — everything between 10% and 25% brightness. This is
a distant backdrop; it must never compete with foreground gameplay elements.
Rough stone strata in deep indigo and night violet, faint mineral veins,
a few tiny distant crystal glimmers.
Heavily desaturated. Soft, no sharp detail — distance haze.
No characters, no UI, no foreground objects, no strong focal point.
Solid opaque image, no transparency.
```

### Layer B — mid crystal formations

```
[STYLE BIBLE BLOCK]

Subject: a mid-distance band of large violet and cyan crystal formations
growing from the cavern floor and hanging from the ceiling, framing an empty
gap through the CENTRE of the image.
1920x1080 pixels. The centre 900x900 region must be completely empty and
transparent — the game board sits there and nothing may overlap it.
Crystals are moderately bright (35-55% brightness) with soft glow, arranged
denser at the left and right edges, thinning toward the centre.
Falling motes of light drift between them.
[TRANSPARENCY BLOCK]
```

### Layer C — foreground vignette

```
[STYLE BIBLE BLOCK]

Subject: a foreground framing overlay — dark out-of-focus rock silhouettes and
crystal shards intruding from the four corners of the frame only.
1920x1080 pixels. The centre 70% of the image is fully transparent.
Silhouettes are near-black indigo #1B1235, almost pure silhouette with only a
faint cyan rim-light on their inner edges.
Soft blurred edges to suggest they are out of focus and very close to camera.
[TRANSPARENCY BLOCK]
```

### Menu background

The menu currently reads as an empty black void. Use Layer A + Layer B, plus:

```
[STYLE BIBLE BLOCK]

Subject: a hero splash background for a crystal-cavern puzzle game main menu.
1920x1080. A grand underground geode chamber, a shaft of pale light falling from
a crack in the ceiling onto a central pedestal of glowing crystals.
The UPPER THIRD must stay dark and uncluttered (the title logo goes there) and
the CENTRE-LOWER area must stay dark and uncluttered (the menu panel goes there).
All the visual interest lives in the left and right thirds.
Brightness range 10-40%, richly coloured but low contrast.
Solid opaque image.
```

---

## 7. FX sprites (for the shader/VFX pass)

Small, cheap, and they carry most of the "juice". Generate on **pure black**
background and use Additive blending in Unity — no keying needed.

```
[STYLE BIBLE BLOCK]
Subject: a radial burst of light for a particle effect — a bright white-cyan
core fading through crystal cyan to magenta at the edges, with 8 thin spike rays.
256x256, perfectly centred and radially symmetric.
Solid pure black #000000 background (this will be rendered with additive
blending, so black becomes transparent). No outline, no pixel-art dithering here
— smooth radial falloff.
```

Also worth ordering, same treatment:

- **Four-point sparkle** — a single soft star flare, 64×64, for merge trails.
- **Soft round glow** — a plain radial gradient, 128×128, for gem auras
  (`CrystalAuraController.cs` can drive its colour per tier).
- **Shockwave ring** — a thin bright expanding ring, 256×256, for merge impact.
- **Shard fragment set** — 6 small angular crystal chips on a 192×128 sheet, for
  merge debris.
- **Light shaft / god ray** — a soft vertical cone, 128×512, additive.

### Shader ideas these unlock

- **Gem sheen**: scroll a diagonal gradient mask across the gem sprite —
  a `_SheenSpeed` + `_SheenWidth` Shader Graph on the sprite material. One node
  chain, big perceived quality jump.
- **Tier glow**: an outline/bloom pass whose intensity scales with tier, driven
  from `Item.ApplyVisuals` via a MaterialPropertyBlock (no material instancing,
  no leaks — `lesson-review` flags leaked materials for a reason).
- **Dissolve on merge**: noise-threshold dissolve with a bright cyan edge band —
  the classic merge-consume effect.
- **Cell pulse**: emissive pulse on valid-drop cells, driven by a `_Highlight`
  float rather than swapping sprites.
- **Background parallax**: scroll layers A/B/C at different rates in a single
  material with `_ScrollSpeed`, instead of moving transforms.

---

## 8. Non-art issues visible in the screenshots

These are code/scene bugs, not art problems. New art will not hide them:

1. **The CRYSTALS score HUD renders on top of the main menu** (menu screenshot,
   top-left). The HUD canvas isn't being hidden when the menu is shown —
   check `UIManager` / `MenuController` state handling.
2. **A "Restart" button is live on the main menu**, which has no game to restart.
3. **The grid is off-centre** — it sits right of centre in the gameplay
   screenshot while the background's framing is symmetric.
4. **`Assets/_Project/Art/Gems/` holds 19 tier PNGs but `GemTierTable` only
   defines 10 tiers.** Delete or re-map the orphans before ordering new art.
5. **`Assets/_Project/Scripts/sprites/Sakura_0.PNG`** — a sprite living in the
   Scripts folder, against the project's own structure rule.

---

## 9. Suggested order of work

Ranked by visual impact per unit of effort:

1. **Cell tiles + board frame** — fixes the mush. Biggest single win.
2. **Gems (10)** — silhouette-distinct, bright, same canvas.
3. **Button family + HUD plate + menu panel** — one coherent UI set.
4. **Background layers A/B/C** — depth and parallax.
5. **Title logo** (or TMP text + crystal plaque).
6. **FX sprites** — then the shader pass.

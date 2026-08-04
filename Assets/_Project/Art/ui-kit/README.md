# Crystal Cavern — UI kit

16-bit pixel UI for the merge-3. Everything is native resolution: **never scale by a
non-integer factor, never enable filtering.**

```
ui-kit/
  elements/     STANDALONE, ready to drop in — each element one transparent PNG
                at its real layout size, no drop shadow baked in
  sprites/      9-slice frames (scale to any size), icons, cursor, backdrop
  gems/         the 7-tier merge chain, 64x64, alpha
  palette/      .gpl (GIMP/Krita), .hex (Aseprite), .txt (roles)
  README.md
```

---

## 1. Fonts

Both are open-licence (SIL OFL 1.1) and free to ship in a commercial game.

| Role | Family | Download |
|---|---|---|
| Display / numerals | **Jersey 25** | fonts.google.com/specimen/Jersey+25 |
| UI labels | **Silkscreen** | fonts.google.com/specimen/Silkscreen |

Click "Get font" → "Download all" for the TTFs. In Unity, import each TTF and set
**Rendering Mode: Hinted Raster**, **Character: Unicode**, and pick a font size that is a
multiple of the font's native pixel grid (Silkscreen 8/16/24, Jersey 25 at 16/32/48) so
glyphs stay crisp.

Web fallback:

```css
@import url('https://fonts.googleapis.com/css2?family=Jersey+25&family=Silkscreen:wght@400;700&display=swap');
```

Assignments used in the title screen:

| Element | Font | Size | Tracking | Colour |
|---|---|---|---|---|
| Game title | Jersey 25 | 132px | 0.06em | `#CFF8F2` |
| Title kicker | Silkscreen 400 | 22px | 0.42em | `#6EE6E0` |
| Button label | Silkscreen 700 | 22px | 0.10em | `#A5F2EC` / `#EFFFFC` selected |
| Panel heading | Silkscreen 400 | 13px | 0.14em | `#6EE6E0` |
| Field label | Silkscreen 400 | 11px | 0.10em | `#8F6FC9` |
| Numerals | Jersey 25 | 22–30px | 0 | `#F5C542` gold, `#CFF8F2` neutral |
| Footer hint | Silkscreen 400 | 12px | 0.14em | `#8F6FC9` |

All text carries a hard 2px offset shadow in `#1B1235` — **offset only, blur 0.**

---

## 2. Colour

26 colours, listed with roles in `palette/crystal-cavern.txt`. Load the `.gpl` into
GIMP/Krita or the `.hex` into Aseprite and work inside it.

Structural rules:

- **Outline** is always `#1B1235`. Never pure black, never a lighter tint.
- **Key light** upper-left. **Bounce** is violet from below (`#8F6FC9`). **Rim** is pale
  cyan (`#EFFFFC` → `#CFF8F2`) on the upper-left silhouette edge only.
- Gradients are **ordered dither** (4×4 Bayer) between two adjacent ramp entries. No
  airbrush, no alpha blending, no anti-aliased edges.
- Three ramps carry everything: crystal `#142A33→#EFFFFC`, violet `#1B1235→#E0AEFF`,
  gold `#5C3A12→#FFF6D0`.
- `#5A4A8C` fails contrast on dark fills. Use it for recessive hex labels only, never body copy.

---

## 3. 9-slice frames

| Sprite | Size | Border (all sides) | Use |
|---|---|---|---|
| `panel_window.png` | 32×32 | **8px** | HUD windows, dialogs. Gold corner studs live in the corner slices. |
| `button_normal.png` | 24×24 | **6px** | Menu button, idle |
| `button_selected.png` | 24×24 | **6px** | Menu button, focused (cyan frame) |
| `button_pressed.png` | 24×24 | **6px** | Menu button, held (inverted bevel) |
| `button_disabled.png` | 24×24 | **6px** | Menu button, unavailable |
| `chip.png` | 16×16 | **4px** | Currency pills, small tags |
| `rule_gold.png` | 8×8 | 3-slice, tile on X | Divider under the title |

Unity: select the sprite → **Sprite Mode: Single**, **Mesh Type: Full Rect**, open
**Sprite Editor → Border** and type the value above into L/T/R/B. On the Image component
set **Image Type: Sliced**, and tick **Pixels Per Unit Multiplier** off.

Because the frames are 9-slice, a button can be any size ≥ 2× its border. Menu buttons in
the mock are **392×64**.

---

## 4. Import settings (Unity)

Apply to **every** PNG in this kit:

```
Texture Type        Sprite (2D and UI)
Sprite Mode         Single
Pixels Per Unit     64        (one gem sprite = 1 world unit)
Mesh Type           Full Rect
Filter Mode         Point (no filter)
Compression         None
Max Size            2048
Generate Mip Maps   off
sRGB                on
Alpha Is Transparency  on
```

Camera / canvas: use an integer scale factor only (2×, 3×, 4×). On a Canvas Scaler use
**Scale With Screen Size**, reference resolution **1280×720**, and round the resulting
scale factor down to the nearest integer in code — a fractional factor is what makes pixel
art shimmer.

---

## 5. Component metrics

**HUD window** — 224px wide, 16px vertical / 18px horizontal padding, 12px gap between
rows. Frame: `panel_window` 9-slice. Drop shadow: 6px right / 6px down, `#150D28`, no blur.

**Menu button** — 392×64, label centred, 14px gap between buttons. Drop shadow 6px/6px
`#150D28`. Selected state additionally shows `cursor.png` (14×18 source) at **40px left of
the button edge, vertically centred**.

**Currency pill** — `chip` 9-slice, 9px top/bottom padding, 10px left / 14px right, 8px gap
between icon and numeral, 8px gap between pills.

**Title block** — crest gem 112×112, 6px below it the kicker, then the title, then 14px
down the gold rule: `finial_diamond` — 12px gap — 420px `rule_gold` — 12px gap — finial.

**Footer bar** — full width, 46px tall, `#1B1235`, 3px `#2E1F53` top edge, 30px gap between
hints.

---

## 6a. Standalone elements

Use these when you want to drop a finished element straight onto a canvas at 1&times;.
Transparent outside the shape; no drop shadow baked in — add `6px / 6px #150D28` in engine.

| File | Size |
|---|---|
| `elements/button_392x64_normal.png` | 392×64 |
| `elements/button_392x64_selected.png` | 392×64 |
| `elements/button_392x64_pressed.png` | 392×64 |
| `elements/button_392x64_disabled.png` | 392×64 |
| `elements/panel_hud_224x136.png` | 224×136 |
| `elements/pill_132x46.png` | 132×46 |
| `elements/divider_468x12.png` | 468×12 (finial + 420 rule + finial) |
| `elements/icon_gold / icon_gem / icon_energy.png` | 32×32 |
| `elements/cursor.png` | 14×18 |
| `elements/finial_diamond.png` | 12×12 |
| `elements/rule_gold.png` | 8×8, tiles on X |

Need a different button width? Use the 9-slice in `sprites/` instead — these are fixed size.

---

## 6. Sprite inventory

| File | Size | Notes |
|---|---|---|
| `gems/gem_t1…t7.png` | 64×64 | Merge chain, tier 1 → 7. Alpha. |
| `sprites/icon_gold.png` | 32×32 | Currency |
| `sprites/icon_gem.png` | 32×32 | Currency — the chain's cut at icon scale |
| `sprites/icon_energy.png` | 32×32 | Currency |
| `sprites/cursor.png` | 14×18 | Menu selection pointer |
| `sprites/finial_diamond.png` | 12×12 | Rule terminator |
| `sprites/bg_cavern.png` | 320×180 | Backdrop. **Scale 4× to 1280×720, Point filter.** |

Naming convention for anything you add: `type_name_state.png`, lowercase, underscores.
Tiers are always `_tN`.

---

## 7. Rules for new art

1. Palette only. If you need a value that isn't there, dither two that are.
2. 1px `#1B1235` outline around every silhouette, including interior holes.
3. Key light upper-left, violet bounce below, cyan rim upper-left.
4. Glow stays **inside** the outline. Nothing bleeds into the background.
5. Draw at native size. If it doesn't read at 1×, redraw it — don't add detail at 2×.

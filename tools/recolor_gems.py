#!/usr/bin/env python3
"""
Recolor the gem-chain-alpha pixel-art set into a 7-tier colour ladder.

Usage (needs Pillow):
    python3 tools/recolor_gems.py \
        "Assets/_Project/Art/new art - claude design/gem-chain-alpha" \
        Assets/_Project/Art/Gems/ChainAlpha

Then re-import + rewire GemConfig in Unity. The tints baked into GemConfig are
sampled from the output of this script, so if you retune a tier here, update
GemConfig.tintColor and GemTierTable.Colors to match.

The source art uses TWO deliberate hue ramps:
  * gem body      -- cyan/teal, H ~168-197, 8 shading steps
  * setting/aura  -- purple,    H ~254-274, 5 shading steps
  * one gold accent (#B8862B) on t7 only

Only the BODY ramp is rotated per tier. The purple setting stays fixed so all
seven gems still read as one matched set -- the stone is the variable, the
mount is the constant.

Tier 1 is the exception: it is a bare blob with no setting, so its purple IS
the body. There we rotate both families.

Value (brightness) is preserved per pixel, so the hand-authored shading ramp
and the pixel-art highlights survive the recolour intact.
"""
import colorsys
import os
import sys
from PIL import Image

SRC_DIR = sys.argv[1]
OUT_DIR = sys.argv[2]

# Hue windows used to classify a source pixel into one of the two ramps.
CYAN_LO, CYAN_HI = 150.0, 225.0
PURPLE_LO, PURPLE_HI = 235.0, 300.0

# Reference hue at the centre of each source ramp. Per-pixel deviation from
# this reference is preserved (scaled) so the ramp keeps its internal variation
# instead of collapsing to one flat hue.
CYAN_REF = 178.0
PURPLE_REF = 258.0
SPREAD = 0.55

# tier -> (name, target hue, saturation scale, value scale, also-rotate-purple)
TIERS = [
    # t1 must out-contrast the blue-navy CELL background, not just the other
    # tiers -- it is the most common stone on the board. Darkening it to a true
    # obsidian sinks it into the cell, so it reads as a pale slate instead.
    (1, "obsidian",      220.0, 0.32, 1.38, True),
    # t2's source frame leans on the two darkest body swatches, so it needs a
    # value lift or the first merge (slate -> ruby) reads as dark-to-dark.
    (2, "ruby",          352.0, 1.20, 1.22, False),
    (3, "emerald",       142.0, 1.10, 1.00, False),
    (4, "sapphire",      205.0, 1.18, 1.00, False),
    (5, "citrine",        40.0, 1.15, 1.05, False),
    (6, "rhodochrosite", 318.0, 1.12, 1.00, False),
    (7, "diamond",       195.0, 0.20, 1.12, False),
]


def clamp(x, lo=0.0, hi=1.0):
    return max(lo, min(hi, x))


def shift(rgb, ref_hue, target_hue, sat_scale, val_scale):
    """Rotate one pixel from its source ramp onto the target hue."""
    r, g, b = [c / 255.0 for c in rgb]
    h, s, v = colorsys.rgb_to_hsv(r, g, b)
    h_deg = h * 360.0

    # Preserve this pixel's offset within the source ramp, damped by SPREAD.
    offset = ((h_deg - ref_hue + 180.0) % 360.0) - 180.0
    new_h = (target_hue + offset * SPREAD) % 360.0

    new_s = clamp(s * sat_scale)
    new_v = clamp(v * val_scale)

    nr, ng, nb = colorsys.hsv_to_rgb(new_h / 360.0, new_s, new_v)
    return (round(nr * 255), round(ng * 255), round(nb * 255))


def recolor(src_path, dst_path, target_hue, sat_scale, val_scale, rotate_purple):
    im = Image.open(src_path).convert("RGBA")
    px = im.load()
    w, h = im.size

    # Colour-keyed cache: the art is a fixed 16-colour palette, so each unique
    # source colour is converted once instead of once per pixel.
    cache = {}
    touched = {"body": 0, "structure": 0, "accent": 0}

    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            key = (r, g, b)
            if key not in cache:
                _, _, _ = 0, 0, 0
                hh = colorsys.rgb_to_hsv(r / 255.0, g / 255.0, b / 255.0)[0] * 360.0
                if CYAN_LO <= hh < CYAN_HI:
                    cache[key] = shift(key, CYAN_REF, target_hue, sat_scale, val_scale)
                    touched["body"] += 1
                elif PURPLE_LO <= hh < PURPLE_HI and rotate_purple:
                    cache[key] = shift(key, PURPLE_REF, target_hue, sat_scale, val_scale)
                    touched["body"] += 1
                elif PURPLE_LO <= hh < PURPLE_HI:
                    cache[key] = key          # setting / aura stays constant
                    touched["structure"] += 1
                else:
                    cache[key] = key          # gold accent and anything else
                    touched["accent"] += 1
            nr, ng, nb = cache[key]
            px[x, y] = (nr, ng, nb, a)

    im.save(dst_path)
    return touched, len(cache)


os.makedirs(OUT_DIR, exist_ok=True)

for tier, name, hue, sat, val, rot_purple in TIERS:
    src = os.path.join(SRC_DIR, f"gem_t{tier}.png")
    dst = os.path.join(OUT_DIR, f"tier_{tier:02d}_{name}.png")
    touched, n = recolor(src, dst, hue, sat, val, rot_purple)
    print(
        f"tier {tier} {name:15s} H={hue:5.1f} "
        f"palette={n:2d} body={touched['body']:2d} "
        f"structure={touched['structure']:2d} accent={touched['accent']:2d} "
        f"-> {os.path.basename(dst)}"
    )

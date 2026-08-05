# Shard Chain Sprite Credits

`shard_t1.png` … `shard_t5.png` — a five-step crystal-shard progression, 64x64
each, added to the project on 2026-08-05. They fill tiers 2–6 of the merge
ladder; tier 1 (`Gems/ChainAlpha/tier_01_obsidian.png`) and tier 7
(`Gems/ChainAlpha/tier_07_diamond.png`) still come from the ChainAlpha set.

- Source: **not recorded** — these arrived in the working tree without
  provenance. Fill this in before shipping or redistributing.
- License: **unconfirmed.**

See `../Gems/CREDITS.md` for the ChainAlpha set's provenance.

## Import settings

Set by the sizing pass, not by hand — re-run it if the art is regenerated:

- Texture Type: Sprite (2D and UI), Single, Point filter, Clamp, uncompressed,
  no mipmaps, alpha is transparency.
- Pixels Per Unit is **per tier**, derived from each texture's measured alpha
  bounding box so every gem lands on the 0.82 → 1.06 world-unit ramp across the
  seven tiers. A flat PPU would make the low tiers render small, because the
  artwork fills a different fraction of its 64x64 canvas at each step
  (34px at tier 2, 63px at tier 6).

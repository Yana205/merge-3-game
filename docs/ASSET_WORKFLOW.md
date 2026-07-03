# Gem Art Asset Workflow

How to get or create gem sprites for the merge-3 game and wire them into the
game's data. The game code is already art-ready: `GemConfig` (ScriptableObject)
holds 20 `GemTierData` entries (name, tint color, sprite, score); when a
`GemConfig` with sprites is assigned to the Item prefab it automatically
overrides the built-in colored-square fallback (`GemTierTable`).

**What the game needs:** up to 20 gem sprites (tier 1 → 20), square-ish,
transparent background, ideally 256–512 px. Fewer than 20 works too — see
"Tint strategy" below.

---

## Tint strategy: 20 unique sprites vs. few bases × tint

`Item.cs` currently shows sprites untinted (`spriteRenderer.color =
Color.white` when a sprite exists). Two options:

| Strategy | Art needed | Code change |
|---|---|---|
| **A. Unique sprite per tier** | 20 distinct gem sprites | none |
| **B. Tinted bases** | 4–7 grayscale/white gem shapes, reused across tiers | one line: apply `GemData.tintColor` instead of `Color.white` in `Item.ApplyVisuals` |

Strategy B is how most match-3 games ship (one crystal shape, many colors),
needs far less art, and the 20 tier tint colors already exist in
`GemTierTable.Colors`. Recommended unless you want each tier to feel like a
different mineral.

---

## Route 1 — Free CC0 packs (fastest, zero license risk)

Best sources, checked July 2026:

- **Kenney** ([kenney.nl/assets](https://kenney.nl/assets)) — *Puzzle Pack 1*
  and *Puzzle Pack 2* contain match-3 gem/jewel tiles. **CC0 (public
  domain)** — commercial use, no attribution. The safest license there is.
- **itch.io free match-3 tags** ([itch.io/game-assets/free/tag-match-3](https://itch.io/game-assets/free/tag-match-3)) —
  many free gem packs; **check each pack's license individually** (e.g.
  [Free Match 3 Game Assets](https://free-game-assets.itch.io/free-match-3-game-assets)
  has 31 PNG tiles but no stated license — ask the author before shipping).
- **OpenGameArt** ([opengameart.org](https://opengameart.org/art-search?keys=gems)) —
  filter by CC0.

### Steps

1. Download the pack (browser) and unzip.
2. Copy the gem PNGs into `Assets/_Project/Art/Gems/` (create the folder; keep
   only the sprites you use).
3. In a Claude Code session with the `ai-game-developer` MCP connected, say:
   *"import the gem sprites and wire up GemConfig"* — or invoke the
   `/gem-assets` skill. Claude will run the Unity-side pipeline below.

---

## Route 2 — Create with Canva AI (uses your Claude ↔ Canva connector)

Generate original gem art from a text prompt, no external tools. Works from
any Claude Code / claude.ai session with the Canva connector enabled.

1. **Generate**: `generate-design` with a square design type and a prompt like
   *"single faceted crystal gem icon, centered, flat vector game-art style,
   vivid emerald green, plain solid white background, no text"*. Generate one
   design per gem shape needed (4–7 for Strategy B).
2. **Pick a candidate** → `create-design-from-candidate`.
3. **Export**: `export-design` as **PNG, `transparent_background: true`**,
   512×512. (Transparent PNG export requires a Canva Pro plan; on free Canva,
   export on white and remove the background in any editor, or prompt for a
   solid magenta background and chroma-key it.)
4. **Download** the export URL into `Assets/_Project/Art/Gems/`.
5. Run the Unity-side pipeline below.

Tips for consistency across gems: reuse the exact same prompt and only swap
the color word; generate all gems in one session; ask for "flat vector, no
gradient background, centered, fills 80% of frame".

---

## Route 3 — Unity Asset Store (free tier)

Search the Asset Store for "gems match 3 free". Import via Package Manager
(`My Assets`) in the Unity Editor — this route is manual by nature. After
import, move the sprites you want into `Assets/_Project/Art/Gems/` and run the
Unity-side pipeline.

---

## Unity-side pipeline (shared by all routes)

Automated by the `/gem-assets` skill; done live in the Editor via the
`ai-game-developer` MCP — no hand-editing of `.asset`/`.prefab` files.

1. **Refresh**: `assets-refresh` so Unity imports the new PNGs.
2. **Import settings**: for each texture — Texture Type **Sprite (2D and
   UI)**, single Sprite mode (or Multiple + slicing for sprite sheets),
   Pixels Per Unit tuned so a gem fits a cell (sprite is 1×1 world units when
   PPU = texture size), compression per platform.
3. **Create config**: `Assets/_Project/Data/GemConfig.asset`
   (`ScriptableObject.CreateInstance<GemConfig>()` via `script-execute`).
4. **Fill 20 tiers**: names + tint colors + score values seeded from
   `GemTierTable`, sprites assigned per tier (cycling base shapes if using
   Strategy B).
5. **Assign to prefab**: open `Assets/_Project/Prefabs/Item.prefab`
   (`assets-prefab-open`), set its `gemConfig` field, save.
6. **Verify**: enter play mode, `screenshot-game-view`, check gems render with
   correct art and the tier label stays readable.

## License bookkeeping

Keep a `Assets/_Project/Art/Gems/CREDITS.md` noting, per file: source URL,
author, license, date downloaded. CC0 needs no attribution but the record
protects you if a pack's page changes later.

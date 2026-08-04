---
name: gem-assets
description: Get or create gem sprites for the merge-3 game and wire them into GemConfig + the Item prefab via the Unity MCP. Use when the user wants gem art imported, generated (Canva), or hooked up to the 20-tier ladder. Full background in docs/ASSET_WORKFLOW.md.
---

# Gem Assets Pipeline

Wire gem sprite art into the game. Read `docs/ASSET_WORKFLOW.md` first for
routes (free packs / Canva generation) and the tint strategy decision.

## Preconditions

- Unity Editor open with the `ai-game-developer` MCP connected
  (verify with `scene-list-opened`).
- Gem PNGs present in `Assets/_Project/Art/Gems/` — if not, help the user get
  them first (Route 1: CC0 pack like Kenney Puzzle Pack; Route 2: Canva
  `generate-design` → `export-design` PNG `transparent_background: true` →
  download the URL into that folder).

## Steps

1. **Ask the tint strategy** if not already decided: (A) one unique sprite per
   tier — no code change; (B) few grayscale bases tinted per tier — requires
   the one-line change in `Item.ApplyVisuals` to use `GemData.tintColor`
   instead of `Color.white` when a sprite is set. Recommend B for small art
   budgets.
2. `assets-refresh` to import the PNGs.
3. Set import settings per texture via `assets-modify` on the importer:
   Texture Type = Sprite (2D and UI); Pixels Per Unit such that the sprite
   maps to ~1 world unit (PPU = texture width); Sprite Mode = Multiple +
   slicing only for sheets.
4. Create `Assets/_Project/Data/GemConfig.asset` if missing, via
   `script-execute`: `ScriptableObject.CreateInstance<GemConfig>()` +
   `AssetDatabase.CreateAsset`.
5. Populate `tiers` (length 20) via `script-execute`, seeding `gemName` and
   `tintColor` from `GemTierTable.NameFor/ColorFor`, `scoreValue` scaling with
   tier, and `sprite` per tier (cycling bases for Strategy B). Save with
   `EditorUtility.SetDirty` + `AssetDatabase.SaveAssets`.
6. Assign the config to the Item prefab: `assets-prefab-open` on
   `Assets/_Project/Prefabs/Item.prefab`, set its GemConfig field
   (`gameobject-component-modify`), `assets-prefab-save` + close.
7. Verify: enter play mode (`editor-application-set-state`), take
   `screenshot-game-view`, confirm gems render with the new art and labels
   stay readable. Exit play mode.
8. Ensure `Assets/_Project/Art/Gems/CREDITS.md` records source, author,
   license per file.

## Rules

- Never hand-edit `.asset`/`.prefab`/`.meta` files — always go through the
  Unity MCP tools (they use the Editor API).
- Commit the PNGs together with their generated `.meta` files.
- Work on a feature branch (`feature/gem-art`), never on `main`.

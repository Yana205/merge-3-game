---
name: ui-buttons
description: Create styled 9-slice button sprites (Canva pixel-art kit or Kenney CC0) and apply them to the game's UI buttons. Executes docs/plans/UI_BUTTONS_PLAN.md on branch feature/ui-buttons.
---

# UI Buttons Pipeline

Read `docs/plans/UI_BUTTONS_PLAN.md` (the plan) and `docs/ASSET_WORKFLOW.md`
(Canva quirks, chroma-key slicing) before starting.

## Steps

1. `git checkout -b feature/ui-buttons` from updated `main`.
2. Confirm asset option with the user (default: Canva pixel-art UI kit to
   match the gems; alternative: Kenney UI Pack, CC0, from kenney.nl).
3. If Canva: generate ONE sheet with button frames in normal + pressed
   states (2-3 colors), magenta background, uniform borders, plain centers
   (9-slice-friendly); slice with the flood-fill keying script pattern from
   the gem pipeline.
4. Sprites → `Assets/_Project/Art/UI/` + CREDITS.md; `assets-refresh`;
   import as Sprite and set `TextureImporter.spriteBorder` (≥16 px) via
   `script-execute`.
5. Apply to `Canvas/LevelCompletePanel/NextLevelButton`: Image type Sliced +
   normal sprite; Button transition SpriteSwap + pressed sprite. Create
   `Assets/_Project/Prefabs/UI/StyledButton.prefab` for reuse.
6. Verify in play mode: trigger the level-complete panel (temporarily lower
   the target score via script-execute if needed — revert after), screenshot,
   check no corner stretching and pressed state works.
7. Commit, push, PR `feat: styled UI buttons`.

## Rules

- Unity MCP / `unity-mcp-cli` for all mutations; never hand-edit serialized
  Unity files.
- The UI_MENU_PLAN depends on these sprites — keep names generic
  (`btn_primary_normal`, `btn_primary_pressed`, ...).

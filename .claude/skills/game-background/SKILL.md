---
name: game-background
description: Create and wire a themed background for the merge-3 game (Canva pixel-art, free pack, or procedural gradient). Executes docs/plans/BACKGROUND_PLAN.md on branch feature/game-background.
---

# Game Background Pipeline

Read `docs/plans/BACKGROUND_PLAN.md` (the plan) and `docs/ASSET_WORKFLOW.md`
(shared pipeline patterns: Canva export quirks, chroma-key slicing, Unity MCP
wiring) before starting.

## Steps

1. `git checkout -b feature/game-background` from updated `main`.
2. Confirm asset option with the user (default: Canva pixel-art cave
   backdrop, `phone_wallpaper` type, dark/muted, empty center, no text —
   plain PNG export, no custom size).
3. Art → `Assets/_Project/Art/Backgrounds/` + CREDITS.md; `assets-refresh`;
   import as Sprite, compression on.
4. Check Cell/Item sorting layers/orders first; add a `Background`
   GameObject with SpriteRenderer behind everything via Unity MCP
   (`gameobject-create` + component tools — never hand-edit the scene file).
5. Scale to cover the camera frustum at 16:9 and portrait (compute from
   orthographicSize; add a small `BackgroundFitter` script under
   `Assets/_Project/Scripts/UI/` if a fixed scale doesn't hold across
   aspects). Save the scene via `scene-save`.
6. Verify in play mode with `screenshot-game-view` (landscape + portrait):
   gems and labels must stay clearly readable — darken the background tint
   if not.
7. Commit (art + meta + scene + script), push, PR `feat: themed game
   background`.

## Rules

- Unity MCP / `unity-mcp-cli` for all scene & asset mutations; never edit
  `.unity`/`.asset`/`.meta` by hand.
- If the MCP session token is expired, use
  `npx unity-mcp-cli run-tool <tool> --path "<project>" --input '{...}'`.

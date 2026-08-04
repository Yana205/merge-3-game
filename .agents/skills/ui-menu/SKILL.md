---
name: ui-menu
description: Build the main menu (Play / level select / Quit), style level-complete and game-over panels, and fix the LevelSelectUI scene-name bug. Executes docs/plans/UI_MENU_PLAN.md on branch feature/ui-menu. Requires ui-buttons assets first.
---

# UI Menu Pipeline

Read `docs/plans/UI_MENU_PLAN.md` (the plan) first. Requires the button
sprites from `feature/ui-buttons` (`Assets/_Project/Art/UI/`) — run
`/ui-buttons` first if missing.

## Steps

1. `git checkout -b feature/ui-menu` from updated `main` (or from
   `feature/ui-buttons` if unmerged).
2. Ask the user: overlay menu vs separate scene (recommend overlay in
   `mainGame.unity`), and the game's display name for the title art.
3. Title art: Canva `generate-design` type `logo`, pixel-art wordmark; plain
   PNG export; chroma-key if background baked in; →
   `Assets/_Project/Art/UI/` + CREDITS.md.
4. Read `GameManager`/`Bootstrap`/`LevelManager` startup flow first, then
   build `MenuPanel` under Canvas via Unity MCP gameobject tools: title
   Image, Play, Level 1/2, Quit buttons (StyledButton prefab). Bind onClick
   in a small `MenuController` MonoBehaviour (binding in code beats
   serializing UnityEvents through MCP).
5. Gate game start behind the menu; Play → hide menu, start level 0.
6. Style `LevelCompletePanel`; build `GameOverPanel` (Replay / Menu buttons)
   and assign it to `UIManager.gameOverPanel`.
7. Fix `LevelSelectUI.cs` fallback loading scene `"Game"` → the actual scene
   name (or `SceneManager.GetActiveScene().name`).
8. Verify each state in play mode with `screenshot-game-view`: menu on
   start → Play works → level-complete panel → game-over panel.
9. Commit in small steps; push, PR `feat: main menu and styled panels`.

## Rules

- Unity MCP / `unity-mcp-cli` for all scene mutations; `scene-save` after
  hierarchy changes; never hand-edit `.unity` files.

# Plan: UI Menu (main menu + panels)

**Execution branch:** `feature/ui-menu` · **Skill:** `/ui-menu` · **Depends on:** UI_BUTTONS_PLAN (button sprites)

## Goal

A playable menu flow: title/main menu (Play, level select, Quit), plus styled
level-complete and game-over panels, using the button assets from
`feature/ui-buttons`.

## Current state

- `LevelSelectUI.cs` has `PlayGame` / `LoadLevel(int)` / `QuitGame` but no UI
  objects call it.
- **Known bug:** its no-GameManager fallback loads scene `"Game"`, but the
  scene is `mainGame.unity` — fix the string (or better, use the active-scene
  name) during implementation.
- `Canvas/LevelCompletePanel` exists (bare); `UIManager.gameOverPanel` is
  unassigned — the panel likely needs to be created.
- Two levels exist: `Assets/_Project/Data/Level_01.asset`, `Level_02.asset`.

## Design decision (ask user at execution)

- **A. Overlay menu in `mainGame.unity` (recommended)** — a full-screen menu
  panel shown on start; Play hides it and starts the level. No scene
  management changes, no build-settings work.
- **B. Separate `mainMenu.unity` scene** — cleaner separation, but needs
  build settings, scene loading, and GameManager bootstrapping across scenes.

## Asset needs

- **Title/logo**: Canva `generate-design` with `logo` type — pixel-art
  wordmark for the game (user should confirm the game's display name first!).
  Export PNG, chroma-key if needed.
- **Panel background**: 9-sliceable pixel-art frame (same Canva UI-kit sheet
  as the buttons plan, or a solid rounded rect with the game palette).
- **Buttons**: reuse `Assets/_Project/Art/UI/` sprites from the buttons plan.

## Integration steps

1. Build the menu hierarchy via Unity MCP gameobject tools (never hand-edit
   the scene): full-screen `MenuPanel` under Canvas → title Image, `PlayBtn`,
   `Level 1` / `Level 2` buttons, `QuitBtn`; wire onClick to `LevelSelectUI`
   methods (`gameobject-component-modify` on Button.onClick, or a small
   `MenuController` MonoBehaviour that binds them in `Awake` — often easier
   than serializing UnityEvents through MCP).
2. Show menu on startup; Play → hide menu, start level 0. Check how
   `GameManager`/`Bootstrap`/`LevelManager` start the game today and gate that
   behind the menu (read those scripts first).
3. Style `LevelCompletePanel` + build `GameOverPanel` with the same frame and
   buttons (Next level / Replay / Menu), assign `UIManager.gameOverPanel`.
4. Fix the `"Game"` scene-name bug in `LevelSelectUI.cs`.

## Verification

Play mode: menu appears on start; Play starts level 1 with menu hidden;
completing a level shows the styled panel and Next Level works; screenshot
each state via `screenshot-game-view`.

## Instructions for Claude

Execute only after `feature/ui-buttons` is merged (or branch from it if
stacking). `git checkout -b feature/ui-menu`. Ask the user: (1) overlay vs
separate scene (recommend overlay), (2) the game's display name for the
title art. Commit in small steps (menu build / panel styling / bug fix);
push, PR titled `feat: main menu and styled panels`.

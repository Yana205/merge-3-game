# Merge-3 Game

Unity 6 (6000.4.7f1) merge-3 puzzle game.

## Git Workflow

- **Never commit directly to `main`.** Always create a feature branch first.
- Branch naming: `feature/<short-description>`, `fix/<short-description>`, `refactor/<short-description>`.
- At the start of any task, create and checkout the branch: `git checkout -b feature/<name>`.
- Make small, focused commits as you go.
- When the work is done, push the branch and open a PR to `main` using `gh pr create`.
- After PR merge, the branch is auto-deleted by CI.
- Tag `working-game-baseline` marks the last known-good state.

## Project Structure

```
Assets/_Project/
  Scripts/
    Core/       — GridManager, GameManager, Cell, Item, MergeManager, ScoreController, Bootstrap
    Input/      — InputHandler
    Level/      — LevelManager, LevelData
    Data/       — GemConfig, GemTierData
    UI/         — UIManager, LevelSelectUI
  Data/         — LevelData ScriptableObject assets (Level_01, Level_02)
  Prefabs/      — Cell, Item
  scenes/       — mainGame.unity
Assets/_Recovery/ — recovery scene backup
```

## Unity Rules

- All game code lives under `Assets/_Project/Scripts/`. Do not put scripts at the Assets root.
- One MonoBehaviour or class per file. File name must match the class name.
- Use `[SerializeField]` for inspector references instead of public fields when possible.
- ScriptableObjects for data (levels, gem config). Use `[CreateAssetMenu]` attribute.
- Never edit `.meta` files by hand. If you create or move a file, Unity generates the meta — commit it alongside the file.
- Never edit `.unity` scene files by hand. Describe what to change and let the user do it in the Unity Editor.
- Never edit `.asset`, `.prefab`, `.mat`, or `.shader` files by hand — these are Unity-serialized binaries/YAML.
- Do not delete or regenerate `ProjectSettings/` files — they contain editor and build configuration.
- `Library/`, `Temp/`, `Logs/`, `obj/` are gitignored and should stay that way.

## Code Style

- C# with Unity conventions: PascalCase for public members, camelCase for private fields with `_` prefix.
- Keep MonoBehaviour logic thin — extract pure C# helpers when logic gets complex.
- Null-check prefab/reference fields in `Start()` or before `Instantiate()` with `Debug.LogError` on failure.
- Use `[Header("Section")]` to organize inspector fields.

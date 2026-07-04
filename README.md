# Merge-3 Game

A merge-3 puzzle game built with Unity 6, Unity MCP and claude code.
Made assets directly to the game using claude. Full skills structure and automatic workflow of asset and mechanic agents.
https://github.com/user-attachments/assets/fa9937be-7689-4fe6-af9a-24d8004451f9

## Development Workflow

1. **Create a feature branch** from `main`:
   ```
   git checkout main && git pull
   git checkout -b feature/<short-description>
   ```

2. **Work on the branch** — make small, focused commits as you go.

3. **Push and open a PR**:
   ```
   git push -u origin feature/<short-description>
   gh pr create --base main
   ```

4. **Merge the PR** on GitHub — the feature branch is automatically deleted after merge.

### Branch Naming

| Type       | Pattern                        | Example                        |
|------------|--------------------------------|--------------------------------|
| Feature    | `feature/<description>`        | `feature/level-select-screen`  |
| Bug fix    | `fix/<description>`            | `fix/score-not-resetting`      |
| Refactor   | `refactor/<description>`       | `refactor/extract-spawn-logic` |

### Rules

- Never commit directly to `main` — always use a feature branch + PR.
- The `working-game-baseline` tag marks the last known-good state. Revert to it if needed:
  ```
  git checkout working-game-baseline
  ```

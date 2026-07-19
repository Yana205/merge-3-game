# Lesson 2 — UI Toolkit HUD

- **Branch:** `feature/lesson-02-ui-toolkit-hud`
- **Status:** ✅ DONE
- **Date:** 2026-07-19

## Summary

Added a **UI Toolkit HUD** (`UIDocument`) that *supplements* the existing
TextMeshPro/Canvas UI (nothing removed) — a crystal-styled corner card showing the
live score, the best score, and a Restart button. The controller queries elements
with `Q<T>("name")` and stays in sync by subscribing to `GameEvents.ScoreChanged`,
which is the "UIController listens to ScoreChanged" example that completes the
Observer story from Lesson 1: the HUD updates on score changes without referencing
ScoreController or LevelManager at all.

## Deliverable → code map

| Assignment deliverable | Where it lives |
|------------------------|----------------|
| UXML with ≥3 elements (score, high-score, button) | `Assets/_Project/UIToolkit/GameHUD.uxml` — `#score-label`, `#high-score-label`, `#restart-button` (+ title) |
| PanelSettings (Scale With Screen Size) on a UIDocument | created + wired by `Scripts/Editor/GameHudSetup.cs` (`Tools ▸ Merge3 ▸ Setup Game HUD`) |
| USS with type + `#id` + `.class` selectors, linked to UXML | `Assets/_Project/UIToolkit/GameHUD.uss` — `Button` (type), `#score-label` (id), `.panel-header` (class); linked via `<Style src="GameHUD.uss"/>` |
| C# queries with `Q<T>("name")` + updates label on score change | `Scripts/UI/UIController.cs` — `root.Q<Label>("score-label")` etc.; `OnScoreChanged` |
| Button `clicked` wired to a C# method | `UIController.OnRestartClicked` → `SceneManager.LoadScene(active.buildIndex)` |
| AI assistance evidence | see "AI assistance" below |
| `.uxml` readable / understandable | see "UXML structure" below |

## What changed / added

- **`UIToolkit/GameHUD.uxml`** *(new)* — root `hud-root`, a `.panel` card, `.panel-header`
  title, `#score-label`, `#high-score-label`, `#restart-button`. `<Style src>` links the USS.
- **`UIToolkit/GameHUD.uss`** *(new)* — crystal theme; contains all three selector kinds.
- **`Scripts/UI/UIController.cs`** *(new)* — `[RequireComponent(UIDocument)]`; queries by
  name in `OnEnable`, subscribes to `GameEvents.ScoreChanged`, tracks a PlayerPrefs best
  score, wires the button; unsubscribes everything in `OnDisable`.
- **`Scripts/Editor/GameHudSetup.cs`** *(new, Editor)* — `[MenuItem]` that creates the
  PanelSettings asset and adds/wires the `UIDocument` + `UIController` host in the scene.

## Editor steps (needed once — headless can't do these)

1. Open the project in Unity so it imports `GameHUD.uxml`/`.uss` and compiles the scripts.
2. Run **Tools ▸ Merge3 ▸ Setup Game HUD**. It creates
   `Assets/_Project/UIToolkit/GameHUDPanelSettings.asset` (Scale With Screen Size,
   1080×1920) and adds a **GameHUD (UIToolkit)** GameObject with `UIDocument` +
   `UIController` wired to the UXML + PanelSettings.
3. If the console warns "no ThemeStyleSheet found", create one via
   **Assets ▸ Create ▸ UI Toolkit ▸ TSS Theme File** and drag it onto the
   PanelSettings asset's *Theme Style Sheet* field (runtime panels need a theme).
4. Enter Play mode: the corner HUD shows the live score/best and the Restart button
   works. **Save the scene** to keep the host object.

## AI assistance (assignment: use AI to generate/improve UXML or USS)

- **Prompt/intent given to the AI (Claude):** "Generate a UI Toolkit HUD for a crystal
  merge-3 game: a corner card with a title, a live score label (`#score-label`), a best
  label, and a Restart button; USS must include a type selector, an id selector, and a
  class selector, crystal-themed (frosted dark card, cyan borders), and link the USS
  from the UXML."
- **Was the output usable directly?** Mostly. Two things were corrected on review:
  (1) the `<Style src>` link was moved *inside* the root `VisualElement` for maximum
  import compatibility; (2) `Q<T>` names were cross-checked against the UXML `name=`
  attributes so no query returns null. USS border/radius were written as the
  longhand per-edge properties UI Toolkit expects (no `border:` shorthand).

## UXML structure (assignment: confirm you can read it)

Every element is identifiable from its tag + `name`: a root `VisualElement`
(`hud-root`) → a `.panel` `VisualElement` containing a `Label` title (`.panel-header`),
two stat `Label`s (`score-label`, `high-score-label`), and a `Button` (`restart-button`).
`<Style src="GameHUD.uss"/>` attaches the stylesheet. No hidden or generated nodes.

## Review findings (self-review — requirement audit)

- `[ok]` All three `Q<T>` names match UXML `name=` attributes (score-label,
  high-score-label, restart-button).
- `[ok]` USS has all three selector kinds and is linked from the UXML.
- `[ok]` Event hygiene continues Lesson 1's discipline: `ScoreChanged` and
  `button.clicked` both subscribed in `OnEnable`, both removed in `OnDisable`.
- `[note]` `rootVisualElement` is read in `OnEnable`; the controller shares the
  UIDocument's GameObject so the document builds the tree first. Null-guarded with a
  clear error if a source asset is missing.
- `[note]` Best score uses a self-contained PlayerPrefs key (`Merge3_HighScore`) rather
  than coupling to `ProgressManager`; can be swapped later if a unified store is wanted.
- `[note]` PanelSettings + scene wiring are Editor-only (documented above); the
  `GameHudSetup` menu automates them.

## Verification

Static: `Q<T>` names ↔ UXML names ↔ USS selectors all cross-checked; `UIController`
compiles against `UnityEngine.UIElements`. Runtime (user): after the Editor steps,
merging tiles updates the HUD score live (proving the bus subscription), the best
score persists across runs, and Restart reloads the scene.

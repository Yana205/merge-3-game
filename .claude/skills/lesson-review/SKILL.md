---
name: lesson-review
description: Self-review gate for each merge-3 course lesson before it ships. Runs a requirement-checklist audit plus a fresh-context reviewer subagent (fresh eyes) that hunts for the specific mistakes each lesson type is prone to — missing event unsubscribes, leaked materials, unlinked USS selectors, silent JSON defaults. Invoke from the lesson-loop after implementing a lesson and before writing the changelog / merging.
---

# Lesson Review

A lightweight but real review gate. Two passes: a **requirement audit** (did we
hit every deliverable?) and a **fresh-eyes code review** (is it actually correct
and clean?). Findings go into the lesson's changelog under "Review findings", and
anything real gets fixed before the merge.

Several assignments literally require "use an AI tool to review your
implementation" — this skill *is* that deliverable, done properly.

## Pass 1 — Requirement audit (always)

Open the lesson's checklist in `docs/course/PROGRESS.md`. For each box, point at
the concrete evidence: file + symbol, or the Editor script + documented step.
A box with no evidence is not done. Note any that are Editor-only (⏸) so the
changelog can list them as manual steps.

## Pass 2 — Fresh-eyes review (code lessons)

Spawn a reviewer subagent with **fresh context** (the `Explore` or
`general-purpose` agent) so it isn't primed by the reasoning that produced the
code. Give it the diff / file list and the lesson-specific checklist below. Ask
for a terse findings list: `file:line — problem — fix`. Then verify each finding
yourself before acting (don't blindly apply).

Prompt shape:
> Review these changed files for correctness and cleanliness. This is a Unity C#
> merge-3 game. Focus on: <lesson-specific checklist>. List concrete findings as
> `file:line — problem — suggested fix`, most severe first. Do not rewrite the code.

### Lesson-specific review checklists

**Events / Observer (Lesson 1)**
- Every `+=` has a matching `-=` on the same target, in `OnDisable`/`OnDestroy`
  (or on pool return for pooled objects). List any subscription with no unsubscribe.
- All invocations use `?.Invoke()`; no bare `Invoke()`.
- No double-subscription (e.g., subscribing in both `Awake` and `OnEnable`).
- Static bus events are reset on load (domain-reload safety) so subscribers don't
  leak across Editor play sessions.
- No behavior regressions: scoring, level-complete, save still fire exactly once.

**UI Toolkit (Lesson 2)**
- Every `Q<T>("name")` matches a `name=` in the UXML (typo = silent null).
- Null-check query results before use; unregister `clicked`/callbacks on disable.
- USS actually contains all three selector kinds (type, `#id`, `.class`) and is
  linked from the UXML (or via `styleSheets`).

**Shaders (Lessons 3–4)**
- Transparent render state present when required (tags + blend + `ZWrite Off`).
- Properties declared in `Properties{}` AND in the HLSL `CBUFFER`/uniforms.
- Runtime controller uses `renderer.material` (instance), and `Destroy(_mat)` in
  `OnDestroy()` — no leaked material instances.

**Data-driven (Lesson 8)**
- Fields are `[SerializeField] private` with read-only getters (no external writes).
- Weighted-random handles zero/negative total weight without dividing by zero.
- JSON deserialization reports bad color/enum/id clearly (no silent default/crash).
- Validator covers ≥3 distinct checks and runs from an Editor menu.

## Recording findings

Append to the lesson changelog:
```
## Review findings
- [fixed]   ScoreController.cs:NN — subscribed in OnEnable but never unsubscribed → added RemoveListeners in OnDisable.
- [ok]      All 6 invocations use ?.Invoke().
- [note]    PanelSettings creation is Editor-only — documented as a manual step.
```
Use `[fixed]`, `[ok]`, `[note]`, `[wontfix: reason]`. If the reviewer found
nothing real, say so explicitly — an empty review is a valid result, but only
after Pass 1 and Pass 2 both ran.

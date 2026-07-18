---
name: lesson-loop
description: Autonomous "one lesson per fresh context" driver for the merge-3 course. Invoke at the start of any fresh chat to pick up the next unfinished course lesson, implement it into the merge-3 crystal game, self-review, changelog it, and ship it on its own branch merged to main. Use when the user says "continue the course", "next lesson", "keep implementing lessons", or starts a fresh context to resume this work.
---

# Lesson Loop

Drives the course implementation one lesson at a time, each in a clean context,
so the human (or a future you) can `/clear` and resume with zero lost state.
All durable state lives in files, not in the conversation.

## The state files

- **`docs/course/PROGRESS.md`** — the tracker. Lesson table + per-lesson
  checklists + the `NEXT ACTION` pointer. **Read it first, always.**
- **`docs/course/changelogs/<lesson>.md`** — one changelog per lesson (what
  changed, why, deliverable→code mapping, Editor steps, AI-review findings).
- **This skill** — the *procedure*. `PROGRESS.md` — the *state*.

## Environment contract

- **Unity MCP is usually unauthorized in headless runs.** Confirm with the
  `ping` skill (`unity-mcp-cli run-system-tool ping`). If it returns 401/`command
  not found`, you CANNOT drive the Editor. Then:
  - Author all code + text assets directly: `.cs`, `.shader`, `.uxml`, `.uss`, `.json`.
  - For anything needing the live Editor (`.asset`/`.mat`/`PanelSettings` creation,
    scene/prefab wiring, Play mode, screenshots), write a **`[MenuItem]` Editor
    automation script** under `Assets/_Project/Scripts/Editor/` AND document manual
    steps in the changelog. Never hand-edit `.unity`/`.asset`/`.mat`/`.prefab` YAML.
  - If `ping` succeeds, you may do those steps live via the Unity skills instead.
- **Respect `CLAUDE.md`:** scripts under `Assets/_Project/Scripts/`, one class per
  file, `[SerializeField]` over public, `[Header]`, null-checks + `Debug.LogError`.
- **Additive, not destructive.** The game already works. Add to it; only remove
  code when a lesson genuinely requires replacing a path (and say so in the changelog).

## The per-lesson procedure

1. **Orient.** Read `docs/course/PROGRESS.md`. Find the first lesson with status
   `TODO` (or resume the `IN PROGRESS` one). Read its checklist and changelog stub.
2. **Branch from main.**
   ```bash
   git checkout main && git pull --ff-only    # keep main current (skip pull if offline)
   git checkout -b <branch-from-PROGRESS.md>
   ```
   Mark the lesson `🚧 IN PROGRESS` in `PROGRESS.md` and commit that (small first commit).
3. **Read only what this lesson touches.** Don't re-read the whole codebase — keep
   context lean. The changelog stub lists the relevant files.
4. **Implement** the deliverables into the merge-3 game. Small, focused commits as
   you go. Prefer wiring through `GameEvents` (Lesson 1) so systems stay decoupled.
5. **Self-review** with the **`lesson-review`** skill (fresh-eyes reviewer subagent
   for code lessons; requirement-checklist audit for all). Fix what it finds.
6. **Write the changelog** (`docs/course/changelogs/<lesson>.md`): summary,
   deliverable→file table, every checklist item mapped to code, Editor steps for the
   user, and the AI-review findings. Tick the boxes in `PROGRESS.md`.
7. **Ship it.**
   ```bash
   git add <explicit paths only — never -A>
   git commit -m "feat(lesson-N): ..."      # end body with the Co-Authored-By trailer
   git push -u origin <branch>
   git checkout main
   git merge --no-ff <branch> -m "Merge lesson N: ..."
   git push origin main
   ```
   Set the lesson `✅ DONE` in `PROGRESS.md`, move the `NEXT ACTION` pointer,
   commit that on `main` (doc-only), push.
8. **Checkpoint & budget-gate.** If the session budget is getting low (see below),
   STOP here — do not start the next lesson. Leave `PROGRESS.md` accurate so a
   fresh context resumes cleanly. Otherwise loop to step 1.

## Budget gate (the "look at session limit" rule)

The user's rule: *if the session limit is close, don't start the next task — let a
fresh context continue.* Before step 1 of each new lesson, sanity-check remaining
budget. A full code lesson (read + implement + review + changelog + git) is
substantial. If you estimate you can't finish the NEXT lesson cleanly, stop and
report where things stand and what the next action is (it's already in `PROGRESS.md`).

Never leave the repo in a half-merged or uncommitted state at a stop point.

## Git hygiene (hard rules)

- **Explicit paths only** in `git add`. Never `-A`/`.` — it would sweep the
  CRLF-dirtied `.claude/skills/*/SKILL.md` files and untracked `.mcp.json` (auth).
- Merge `--no-ff` so each lesson is one clear merge commit on `main`.
- One branch per lesson; branch from a fresh `main` each time so lessons stack.
- End commit messages with:
  `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`

## Definition of done (per lesson)

Every checklist box for the lesson is either (a) real code in the repo, or
(b) an Editor automation script + documented manual step in the changelog for the
parts that genuinely require the Unity Editor. The branch is merged to `main` and
pushed. `PROGRESS.md` reflects reality.

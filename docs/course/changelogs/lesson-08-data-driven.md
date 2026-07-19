# Lesson 8 — Data-Driven Design & ScriptableObjects

- **Branch:** `feature/lesson-08-data-driven`
- **Status:** ✅ DONE
- **Date:** 2026-07-19

## Summary

Bar's challenge: make the gem item data purely data-driven with **item tiers +
weighted spawn chances** (his explicit "core deliverable"). Built additively so the
working game is untouched: a locked `GemDefinition` ScriptableObject, a `GemDatabase`
with a generic `GetItemByWeightedRandom()` whose weights live entirely in data, a
JSON→typed pipeline that reports bad values, and an Editor validator + bulk generator.

## Deliverable → code map

| Part | Deliverable | Where |
|------|-------------|-------|
| 1 | Item family → SO with `[CreateAssetMenu]` | `Data/GemDefinition.cs` |
| 1 | Every field `[SerializeField] private` + read-only getter | `GemDefinition` — 8 private fields, 8 getters, no public setters |
| 1 | ≥3 data instances read generically | `Tools ▸ Merge3 ▸ Generate Gems From JSON` makes 8 assets; `GemDatabase`/`GemJsonLoader` read them |
| 2 | Tier field + weight per tier/rarity | `GemDefinition.MinTier/MaxTier/Rarity`; weights in `GemDatabase.rarityWeights` |
| 2 | Generic `GetItemByWeightedRandom()` | `GemDatabase.GetItemByWeightedRandom()` — rule in code, numbers in data |
| 2 | Changing a weight shifts frequency | `GemDatabase` context-menu **Sample Distribution (1000 rolls)** logs the split |
| 3 | JSON + `[SerializeField] TextAsset` load | `Data/gems.json`, `GemJsonLoader.gemJson` |
| 3 | Typed deserialization (string→enum/Color) | `GemJsonParser.Parse` — `Enum.TryParse`, `ColorUtility.TryParseHtmlString` |
| 3 | Broken value reported, not silent/crash | parser adds to `Result.Errors`; loader/tools `Debug.LogError` |
| 3 | Bulk-generate 5–10 entries, zero manual edits | `gems.json` has 8; the generator creates all assets + DB |
| 4 | Validator with ≥3 checks | `GemDatabase.Validate()` + `GemDataTools.ValidateGemData` (JSON + DB) |
| 4 | Run validator before Play (Editor menu) | `Tools ▸ Merge3 ▸ Validate Gem Data` |

## Weighted random — how the logic stays generic

`GetItemByWeightedRandom()` sums each gem's rarity weight, rolls
`Random.Range(0,total)`, and walks the cumulative sum. The **rule** ("pick in
proportion to weight") is the only thing in code; the **numbers** (Common 60,
Uncommon 25, Rare 12, Legendary 3) live in `GemDatabase.rarityWeights` / `gems.json`.
Edit a weight, re-run *Sample Distribution*, and the percentages move — no recompile.
Guarded: an all-zero table returns `null` instead of dividing by zero / looping.

## JSON pipeline

`gems.json` uses string fields for the non-string types (`rarity`, `colorHex`).
`GemJsonParser` converts them to real `GemRarity` / `Color`, and on any bad value
(unknown rarity, malformed hex, missing/duplicate id, `minTier>maxTier`, malformed
JSON) it records a clear message and uses a safe fallback — it never throws and never
silently accepts. `GemJsonLoader` (runtime, `[SerializeField] TextAsset`) and
`GemDataTools` (Editor bulk generator) both surface these via `Debug.LogError`.

### Break test (assignment: deliberately break one value)

Set `"colorHex": "#2B2B33"` → `"notacolor"` in `gems.json` and run **Validate Gem
Data**. Output:
`Validate (json): gems[0] ('obsidian'): invalid colorHex 'notacolor' (expected e.g. #2B2B33).`
— reported clearly, not defaulted-to-white-and-forgotten.

## Validator bug note (assignment)

The check that earns its keep is **zero-weight rarity**: if `Legendary`'s weight is
left at `0`, every Legendary gem *silently never spawns*. You would not notice this
by playing — "I just haven't seen a diamond yet" looks normal — but the validator
flags `'diamond': rarity Legendary has no positive weight — it will never spawn.`
That's a data bug invisible in play but obvious to the validator. (Headless can't run
the Editor; run **Tools ▸ Merge3 ▸ Validate Gem Data** to confirm the message.)

## Editor steps

1. Open in Unity (imports `gems.json`, compiles the scripts).
2. **Tools ▸ Merge3 ▸ Generate Gems From JSON** → creates `Data/Gems/Gem_*.asset` (×8)
   and `Data/GemDatabase.asset`.
3. **Tools ▸ Merge3 ▸ Validate Gem Data** → prints VALID (or the problems).
4. Select `GemDatabase.asset` → gear menu ▸ **Sample Distribution (1000 rolls)**; edit a
   weight and re-run to watch the split change. Optionally add a `GemJsonLoader` to a
   GameObject and assign `gems.json` to see the runtime load + errors.

## Review findings (self-review — logic traced)

- `[ok]` `GetItemByWeightedRandom` guards `total <= 0` (no divide-by-zero / infinite loop)
  and has a float-rounding fallback return.
- `[ok]` All `GemDefinition` fields are `[SerializeField] private` with read-only getters;
  the only writer is `Initialize()`, documented as pipeline-only (gameplay reads getters).
- `[ok]` Parser reports duplicate id, missing id, invalid enum, invalid color, min>max,
  and malformed JSON — each with a located message; never throws.
- `[ok]` Validator covers 4 distinct checks (dup id, empty id/missing ref, min>max,
  zero-weight rarity) across JSON + DB.
- `[note]` This is an additive parallel system; it does not replace `GemConfig`/`LevelData`
  (kept intact per "don't remove too much"). Wiring it into the live spawn path is a
  future step if desired.

## Verification

Static: weighted-pick math and parser branches traced by hand; `Enum.TryParse` /
`ColorUtility.TryParseHtmlString` are the correct typed converters; guards verified.
Runtime (user): after Generate, *Sample Distribution* prints ~60/25/12/3% and shifts
when a weight changes; the break test prints the color error.

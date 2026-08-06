using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves fusion after a crystal lands.
///
/// The rule is the classic one: any ORTHOGONALLY connected group of three or more
/// identical crystals collapses into a single crystal of the next tier, placed at the
/// cell the player tapped. That result can itself complete a new group, so fusion
/// cascades until the board is stable.
///
/// Two details carry most of the design:
///
///  - The result lands at the TAPPED cell, not at the group's centre. This is what
///    makes placement a skill rather than a lottery — the player is aiming the
///    outcome, not just feeding a pile.
///  - Groups of four or more are consumed whole into one crystal. Leaving remainders
///    would be more "efficient" but much harder to read, and over-feeding a group
///    should cost something.
///
/// Replaces the old two-crystal drag merge. Dragging is gone entirely; there is no
/// TryMerge(a, b) any more because there is no move that names two crystals.
/// </summary>
public class MergeManager : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    public GridManager gridManager;

    [Tooltip("Identical crystals that must be touching before they fuse. Three is " +
             "the classic rule; two turns the board into a chain reaction and " +
             "four makes the early game a slog.")]
    [Min(2)]
    [SerializeField] private int groupSize = 3;

    // Direct (local) event: fired once per fusion with the resulting crystal and its
    // cell. Kept for owners that hold a MergeManager reference and want a
    // tightly-scoped hook (juice, sound, screen shake) without going global.
    public event System.Action<Item, Cell> OnMerged;

    /// <summary>
    /// Fuse everything that the crystal now sitting in <paramref name="origin"/>
    /// completes, cascading. Returns how many fusions happened — 0 when the
    /// placement completed nothing.
    /// </summary>
    public int ResolveAt(Cell origin)
    {
        if (gridManager == null || origin == null) return 0;

        int fusions = 0;

        // Re-scan from the tapped cell after every fuse rather than walking a list
        // captured up front: the crystal sitting there is a NEW, higher-tier one
        // each time round, so the group it belongs to is a different group.
        while (true)
        {
            if (!origin.IsOccupied()) break;

            Item seed = origin.CurrentItem;
            if (seed.IsArmedBomb) break;

            // Top of this family's ladder — nothing left to fuse into.
            if (seed.Tier >= Item.MaxTierFor(seed.Family)) break;

            List<Cell> group = gridManager.GetConnectedGroup(origin);
            if (group.Count < groupSize) break;

            // Read the identity BEFORE any despawn. DespawnItem returns the item to
            // the pool via ItemFactory.Release, which runs ResetForPool and wipes
            // tier and family — reading seed.Family afterwards yields the reset
            // default, which would turn every red fusion into a cyan crystal.
            int newTier = seed.Tier + 1;
            GemFamily family = seed.Family;

            foreach (Cell cell in group)
            {
                Item doomed = cell.CurrentItem;
                cell.RemoveItem();
                gridManager.DespawnItem(doomed);
            }

            Item fused = gridManager.SpawnItem(origin, newTier, family);
            if (fused == null)
            {
                Debug.LogError("MergeManager: SpawnItem returned null while fusing.");
                break;
            }

            fusions++;

            OnMerged?.Invoke(fused, origin);

            // ...and the global bus so any system can react (ScoreController adds the
            // score here — MergeManager no longer needs to know scoring exists).
            GameEvents.RaiseTileMerged(fused, origin);
        }

        return fusions;
    }
}

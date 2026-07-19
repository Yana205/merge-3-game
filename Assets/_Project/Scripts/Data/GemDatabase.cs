using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A data-driven catalogue of <see cref="GemDefinition"/>s plus the spawn odds per
/// rarity. The picking rule is generic and fixed in code
/// (<see cref="GetItemByWeightedRandom"/> = "pick in proportion to weight"); the
/// actual weight numbers live entirely in <see cref="rarityWeights"/>, so designers
/// retune odds by editing data, never code.
/// Create via <b>Assets ▸ Create ▸ Merge Game ▸ Gem Database</b>.
/// </summary>
[CreateAssetMenu(fileName = "GemDatabase", menuName = "Merge Game/Gem Database")]
public class GemDatabase : ScriptableObject
{
    [System.Serializable]
    public struct RarityWeight
    {
        public GemRarity rarity;
        [Min(0)] public float weight;
    }

    [SerializeField] private GemDefinition[] gems;

    [Tooltip("Spawn weight per rarity. Change these (no code change) to shift how often each rarity appears.")]
    [SerializeField]
    private RarityWeight[] rarityWeights =
    {
        new RarityWeight { rarity = GemRarity.Common,    weight = 60f },
        new RarityWeight { rarity = GemRarity.Uncommon,  weight = 25f },
        new RarityWeight { rarity = GemRarity.Rare,      weight = 12f },
        new RarityWeight { rarity = GemRarity.Legendary, weight = 3f  },
    };

    public IReadOnlyList<GemDefinition> Gems => gems;

    public float WeightFor(GemRarity rarity)
    {
        if (rarityWeights != null)
            foreach (var rw in rarityWeights)
                if (rw.rarity == rarity)
                    return Mathf.Max(0f, rw.weight);
        return 0f;
    }

    /// <summary>
    /// Generic weighted pick — "choose a gem in proportion to its rarity's weight."
    /// Returns null only when no gem has positive weight (guarded so there is no
    /// divide-by-zero and no infinite loop on an all-zero table).
    /// </summary>
    public GemDefinition GetItemByWeightedRandom()
    {
        if (gems == null || gems.Length == 0)
            return null;

        float total = 0f;
        foreach (GemDefinition g in gems)
            if (g != null)
                total += WeightFor(g.Rarity);

        if (total <= 0f)
            return null;

        float roll = Random.Range(0f, total);
        float acc = 0f;
        foreach (GemDefinition g in gems)
        {
            if (g == null) continue;
            acc += WeightFor(g.Rarity);
            if (roll < acc)
                return g;
        }
        return gems[gems.Length - 1]; // float rounding fallback
    }

    public GemDefinition GetById(string id)
    {
        if (gems == null || string.IsNullOrEmpty(id))
            return null;
        foreach (GemDefinition g in gems)
            if (g != null && g.GemId == id)
                return g;
        return null;
    }

    // Filled by the JSON pipeline / Editor generator, not by gameplay.
    public void SetGems(GemDefinition[] newGems) => gems = newGems;
    public void SetRarityWeights(RarityWeight[] weights) => rarityWeights = weights;

    /// <summary>
    /// Data-integrity checks shared by the runtime loader and the Editor validator.
    /// Empty result = valid. Covers: duplicate ids, empty id / missing reference,
    /// minTier &gt; maxTier, and a rarity with no positive weight (would never spawn).
    /// </summary>
    public List<string> Validate()
    {
        var problems = new List<string>();
        if (gems == null || gems.Length == 0)
        {
            problems.Add("Database has no gems.");
            return problems;
        }

        var seen = new HashSet<string>();
        for (int i = 0; i < gems.Length; i++)
        {
            GemDefinition g = gems[i];
            if (g == null)
            {
                problems.Add($"gems[{i}] is a missing reference.");
                continue;
            }
            if (string.IsNullOrEmpty(g.GemId))
                problems.Add($"gems[{i}] ('{g.name}') has an empty id.");
            else if (!seen.Add(g.GemId))
                problems.Add($"Duplicate gem id '{g.GemId}'.");

            if (g.MinTier > g.MaxTier)
                problems.Add($"'{g.GemId}': minTier ({g.MinTier}) > maxTier ({g.MaxTier}).");

            if (WeightFor(g.Rarity) <= 0f)
                problems.Add($"'{g.GemId}': rarity {g.Rarity} has no positive weight — it will never spawn.");
        }
        return problems;
    }

    // Right-click the asset ▸ "Sample Distribution": proves that editing a weight
    // (no code change) shifts how often each rarity/gem appears over many rolls.
    [ContextMenu("Sample Distribution (1000 rolls)")]
    private void SampleDistribution()
    {
        const int n = 1000;
        var counts = new Dictionary<string, int>();
        for (int i = 0; i < n; i++)
        {
            GemDefinition g = GetItemByWeightedRandom();
            string key = g != null ? $"{g.GemId} ({g.Rarity})" : "<null>";
            counts.TryGetValue(key, out int c);
            counts[key] = c + 1;
        }

        var sb = new System.Text.StringBuilder($"GemDatabase distribution over {n} weighted rolls:\n");
        foreach (var kv in counts)
            sb.AppendLine($"  {kv.Key}: {kv.Value}  ({kv.Value * 100f / n:F1}%)");
        Debug.Log(sb.ToString());
    }
}

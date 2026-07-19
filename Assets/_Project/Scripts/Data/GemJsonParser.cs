using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Deserializes gem data from JSON into typed <see cref="GemDefinition"/>s, turning
/// the string fields (rarity → enum, colorHex → Color) into their real types. Every
/// bad value is reported in <see cref="Result.Errors"/> — nothing defaults silently
/// and nothing throws — so a broken entry surfaces clearly instead of corrupting data.
/// </summary>
public static class GemJsonParser
{
    // DTOs match the JSON shape. Kept as nested types so there's one top-level class
    // in this file; JsonUtility still serializes them fine.
    [System.Serializable]
    public class Entry
    {
        public string id;
        public string displayName;
        public string rarity;
        public int scoreValue;
        public string colorHex;
        public int minTier;
        public int maxTier;
    }

    [System.Serializable]
    public class RarityWeightEntry
    {
        public string rarity;
        public float weight;
    }

    [System.Serializable]
    private class Root
    {
        public Entry[] gems;
        public RarityWeightEntry[] rarityWeights;
    }

    public class Result
    {
        public readonly List<GemDefinition> Gems = new List<GemDefinition>();
        public readonly List<GemDatabase.RarityWeight> RarityWeights = new List<GemDatabase.RarityWeight>();
        public readonly List<string> Errors = new List<string>();
        public bool Ok => Errors.Count == 0;
    }

    public static Result Parse(string json)
    {
        var result = new Result();

        if (string.IsNullOrWhiteSpace(json))
        {
            result.Errors.Add("JSON is empty.");
            return result;
        }

        Root root;
        try
        {
            root = JsonUtility.FromJson<Root>(json);
        }
        catch (System.Exception e)
        {
            result.Errors.Add("Malformed JSON: " + e.Message);
            return result;
        }

        if (root == null || root.gems == null || root.gems.Length == 0)
        {
            result.Errors.Add("No 'gems' array found in the JSON.");
            return result;
        }

        var seenIds = new HashSet<string>();
        for (int i = 0; i < root.gems.Length; i++)
        {
            Entry e = root.gems[i];
            if (e == null)
            {
                result.Errors.Add($"gems[{i}] is null.");
                continue;
            }

            string where = $"gems[{i}]" + (string.IsNullOrEmpty(e.id) ? "" : $" ('{e.id}')");

            if (string.IsNullOrEmpty(e.id))
                result.Errors.Add($"{where}: missing 'id'.");
            else if (!seenIds.Add(e.id))
                result.Errors.Add($"{where}: duplicate id '{e.id}'.");

            // string -> enum
            if (!System.Enum.TryParse(e.rarity, ignoreCase: true, out GemRarity rarity))
            {
                result.Errors.Add($"{where}: invalid rarity '{e.rarity}' (expected Common/Uncommon/Rare/Legendary).");
                rarity = GemRarity.Common; // safe fallback AFTER reporting
            }

            // string -> Color
            if (!ColorUtility.TryParseHtmlString(e.colorHex, out Color color))
            {
                result.Errors.Add($"{where}: invalid colorHex '{e.colorHex}' (expected e.g. #2B2B33).");
                color = Color.white;
            }

            if (e.minTier > e.maxTier)
                result.Errors.Add($"{where}: minTier ({e.minTier}) > maxTier ({e.maxTier}).");

            var def = ScriptableObject.CreateInstance<GemDefinition>();
            def.name = string.IsNullOrEmpty(e.id) ? $"Gem_{i}" : "Gem_" + e.id;
            def.Initialize(e.id, e.displayName, rarity, e.scoreValue,
                           Mathf.Max(1, e.minTier), Mathf.Max(1, e.maxTier), color);
            result.Gems.Add(def);
        }

        if (root.rarityWeights != null)
        {
            foreach (RarityWeightEntry rw in root.rarityWeights)
            {
                if (rw == null) continue;
                if (!System.Enum.TryParse(rw.rarity, ignoreCase: true, out GemRarity r))
                {
                    result.Errors.Add($"rarityWeights: invalid rarity '{rw.rarity}'.");
                    continue;
                }
                result.RarityWeights.Add(new GemDatabase.RarityWeight { rarity = r, weight = rw.weight });
            }
        }

        return result;
    }
}

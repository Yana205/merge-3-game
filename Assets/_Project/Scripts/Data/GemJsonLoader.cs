using UnityEngine;

/// <summary>
/// Loads gem data from a JSON <see cref="TextAsset"/> at runtime and builds a
/// <see cref="GemDatabase"/> from it. Demonstrates the data pipeline end to end:
/// a designer edits gems.json, this deserializes it via <see cref="GemJsonParser"/>,
/// reports any bad values, and exposes a ready-to-query database.
/// </summary>
public class GemJsonLoader : MonoBehaviour
{
    [Tooltip("The gems.json TextAsset to load at runtime.")]
    [SerializeField] private TextAsset gemJson;

    [Tooltip("Optional: reuse an existing database asset instead of building one in memory.")]
    [SerializeField] private GemDatabase targetDatabase;

    [Tooltip("Log a weighted-distribution sample on load, to eyeball the spawn odds.")]
    [SerializeField] private bool sampleOnLoad = false;

    public GemDatabase Database { get; private set; }

    void Awake() => Load();

    public void Load()
    {
        if (gemJson == null)
        {
            Debug.LogError("GemJsonLoader: no gemJson TextAsset assigned.");
            return;
        }

        GemJsonParser.Result r = GemJsonParser.Parse(gemJson.text);

        // Report every problem clearly rather than defaulting silently.
        foreach (string err in r.Errors)
            Debug.LogError($"GemJsonLoader ({gemJson.name}): {err}");

        if (r.Gems.Count == 0)
        {
            Debug.LogError($"GemJsonLoader: no usable gems parsed from {gemJson.name}.");
            return;
        }
        if (!r.Ok)
            Debug.LogWarning($"GemJsonLoader: {r.Errors.Count} error(s); continuing with {r.Gems.Count} gem(s) that parsed.");

        Database = targetDatabase != null ? targetDatabase : ScriptableObject.CreateInstance<GemDatabase>();
        Database.SetGems(r.Gems.ToArray());
        if (r.RarityWeights.Count > 0)
            Database.SetRarityWeights(r.RarityWeights.ToArray());

        Debug.Log($"GemJsonLoader: loaded {r.Gems.Count} gems from {gemJson.name}.");

        if (sampleOnLoad)
            LogSample(1000);
    }

    private void LogSample(int n)
    {
        if (Database == null) return;
        var counts = new System.Collections.Generic.Dictionary<GemRarity, int>();
        for (int i = 0; i < n; i++)
        {
            GemDefinition g = Database.GetItemByWeightedRandom();
            if (g == null) continue;
            counts.TryGetValue(g.Rarity, out int c);
            counts[g.Rarity] = c + 1;
        }
        var sb = new System.Text.StringBuilder($"GemJsonLoader: {n}-roll rarity distribution:\n");
        foreach (var kv in counts)
            sb.AppendLine($"  {kv.Key}: {kv.Value * 100f / n:F1}%");
        Debug.Log(sb.ToString());
    }
}

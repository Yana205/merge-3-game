#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor commands for the data-driven gem pipeline:
///  • <b>Tools ▸ Merge3 ▸ Generate Gems From JSON</b> — bulk-creates a GemDefinition
///    asset per JSON entry plus a GemDatabase that references them (no manual edits).
///  • <b>Tools ▸ Merge3 ▸ Validate Gem Data</b> — runs before Play: parses the JSON
///    (invalid color/enum, duplicate id, min&gt;max) and validates the generated
///    database (missing refs, duplicate ids, zero-weight rarities).
/// </summary>
public static class GemDataTools
{
    private const string DataFolder = "Assets/_Project/Data";
    private const string GemsFolder = DataFolder + "/Gems";
    private const string JsonPath   = DataFolder + "/gems.json";
    private const string DbPath     = DataFolder + "/GemDatabase.asset";

    [MenuItem("Tools/Merge3/Generate Gems From JSON")]
    public static void GenerateFromJson()
    {
        TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(JsonPath);
        if (json == null)
        {
            Debug.LogError($"GemDataTools: could not find {JsonPath}. Let Unity import it first.");
            return;
        }

        GemJsonParser.Result r = GemJsonParser.Parse(json.text);
        foreach (string err in r.Errors)
            Debug.LogError("GemDataTools (parse): " + err);
        if (r.Gems.Count == 0)
        {
            Debug.LogError("GemDataTools: no gems parsed — nothing generated.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(GemsFolder))
            AssetDatabase.CreateFolder(DataFolder, "Gems");

        var defs = new List<GemDefinition>();
        foreach (GemDefinition parsed in r.Gems)
        {
            string path = $"{GemsFolder}/{parsed.name}.asset";
            GemDefinition asset = AssetDatabase.LoadAssetAtPath<GemDefinition>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<GemDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }
            // Copy the typed values parsed from JSON into the persistent asset.
            asset.Initialize(parsed.GemId, parsed.DisplayName, parsed.Rarity, parsed.ScoreValue,
                             parsed.MinTier, parsed.MaxTier, parsed.TintColor, parsed.Sprite);
            EditorUtility.SetDirty(asset);
            defs.Add(asset);
        }

        GemDatabase db = AssetDatabase.LoadAssetAtPath<GemDatabase>(DbPath);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<GemDatabase>();
            AssetDatabase.CreateAsset(db, DbPath);
        }
        db.SetGems(defs.ToArray());
        if (r.RarityWeights.Count > 0)
            db.SetRarityWeights(r.RarityWeights.ToArray());
        EditorUtility.SetDirty(db);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = db;
        Debug.Log($"GemDataTools: generated {defs.Count} gem assets + {DbPath} " +
                  $"({r.Errors.Count} parse error(s)).");
    }

    [MenuItem("Tools/Merge3/Validate Gem Data")]
    public static void ValidateGemData()
    {
        int problems = 0;

        // 1) JSON-level checks (invalid color/enum strings, duplicate id, min>max).
        TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(JsonPath);
        if (json == null)
        {
            Debug.LogError($"GemDataTools: no {JsonPath} to validate.");
        }
        else
        {
            GemJsonParser.Result r = GemJsonParser.Parse(json.text);
            foreach (string err in r.Errors)
            {
                Debug.LogError("Validate (json): " + err);
                problems++;
            }
        }

        // 2) Database-level checks (missing refs, duplicate ids, zero-weight rarity).
        GemDatabase db = AssetDatabase.LoadAssetAtPath<GemDatabase>(DbPath);
        if (db == null)
        {
            Debug.LogWarning($"GemDataTools: no {DbPath} yet — run 'Generate Gems From JSON' first.");
        }
        else
        {
            foreach (string p in db.Validate())
            {
                Debug.LogError("Validate (db): " + p);
                problems++;
            }
        }

        if (problems == 0)
            Debug.Log("GemDataTools: gem data is VALID.");
        else
            Debug.LogError($"GemDataTools: INVALID — {problems} problem(s) found; fix before Play.");
    }
}
#endif

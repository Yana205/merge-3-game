#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// One-click setup for the UI Toolkit HUD, so the parts that need the live Editor
/// (creating a PanelSettings asset, adding a UIDocument to the scene, wiring the
/// UXML + controller) don't have to be done by hand.
///
/// Run: <b>Tools ▸ Merge3 ▸ Setup Game HUD</b>, then save the scene.
/// </summary>
public static class GameHudSetup
{
    private const string UxmlPath = "Assets/_Project/UIToolkit/GameHUD.uxml";
    private const string PanelSettingsPath = "Assets/_Project/UIToolkit/GameHUDPanelSettings.asset";
    private const string HostName = "GameHUD (UIToolkit)";

    [MenuItem("Tools/Merge3/Setup Game HUD")]
    public static void Setup()
    {
        // 1) The UXML must already be imported.
        VisualTreeAsset uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
        if (uxml == null)
        {
            Debug.LogError($"GameHudSetup: could not find {UxmlPath}. Let Unity import the file first.");
            return;
        }

        // 2) PanelSettings asset (Scale With Screen Size) — create if missing.
        PanelSettings panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
        if (panel == null)
        {
            panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1080, 1920);   // portrait mobile
            panel.match = 0.5f;

            // Runtime panels need a theme style sheet. Reuse one if the project has
            // it; otherwise tell the user to create the default (one menu click).
            ThemeStyleSheet theme = FindFirstAsset<ThemeStyleSheet>("t:ThemeStyleSheet");
            if (theme != null)
                panel.themeStyleSheet = theme;
            else
                Debug.LogWarning("GameHudSetup: no ThemeStyleSheet found. Create one via " +
                                 "Assets ▸ Create ▸ UI Toolkit ▸ TSS Theme File and assign it to " +
                                 PanelSettingsPath + " (the HUD renders once a theme is set).");

            AssetDatabase.CreateAsset(panel, PanelSettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"GameHudSetup: created {PanelSettingsPath}.");
        }

        // 3) Scene host with UIDocument + UIController.
        GameObject host = GameObject.Find(HostName);
        if (host == null)
            host = new GameObject(HostName);

        UIDocument doc = host.GetComponent<UIDocument>();
        if (doc == null)
            doc = host.AddComponent<UIDocument>();
        doc.panelSettings = panel;
        doc.visualTreeAsset = uxml;

        if (host.GetComponent<UIController>() == null)
            host.AddComponent<UIController>();

        Selection.activeGameObject = host;
        EditorUtility.SetDirty(host);
        EditorSceneManager.MarkSceneDirty(host.scene);
        Debug.Log("GameHudSetup: HUD wired. Enter Play mode to see it, then save the scene to keep it.");
    }

    private static T FindFirstAsset<T>(string filter) where T : Object
    {
        string[] guids = AssetDatabase.FindAssets(filter);
        if (guids == null || guids.Length == 0) return null;
        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }
}
#endif

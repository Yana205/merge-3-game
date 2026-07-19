#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Creates the Material for the <c>Merge3/MagicalCrystal</c> shader (a .mat is
/// serialized YAML, so it is generated through the API rather than hand-written)
/// and offers a one-click "apply to the selected cells" command.
///
/// Run: <b>Tools ▸ Merge3 ▸ Create Crystal Material</b>, then select your cell
/// objects and run <b>Tools ▸ Merge3 ▸ Apply Crystal Material to Selection</b>.
/// </summary>
public static class CrystalMaterialSetup
{
    private const string ShaderName = "Merge3/MagicalCrystal";
    private const string MaterialPath = "Assets/_Project/Shaders/MagicalCrystal.mat";

    [MenuItem("Tools/Merge3/Create Crystal Material")]
    public static Material CreateMaterial()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError($"CrystalMaterialSetup: shader '{ShaderName}' not found. Let Unity import/compile MagicalCrystal.shader first.");
            return null;
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            mat = new Material(shader) { name = "MagicalCrystal" };
            // Sensible crystal defaults (also the shader's defaults, set explicitly
            // so the .mat reads clearly in the Inspector).
            mat.SetColor("_BaseColor", new Color(0.12f, 0.35f, 0.75f, 1f));
            mat.SetColor("_GlowColor", new Color(0.55f, 0.95f, 1.0f, 1f));
            mat.SetFloat("_ScrollSpeed", 0.35f);
            mat.SetFloat("_NoiseScale", 5.0f);
            mat.SetFloat("_PulseSpeed", 2.0f);
            mat.SetFloat("_PulseStrength", 0.15f);
            mat.SetFloat("_RippleSpeed", 1.5f);
            mat.SetFloat("_RippleStrength", 0.03f);
            mat.SetFloat("_AlphaBase", 0.7f);
            mat.SetFloat("_AlphaBreathe", 0.25f);
            mat.SetFloat("_VignetteStrength", 0.6f);

            AssetDatabase.CreateAsset(mat, MaterialPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"CrystalMaterialSetup: created {MaterialPath}.");
        }
        else
        {
            Debug.Log($"CrystalMaterialSetup: {MaterialPath} already exists.");
        }

        Selection.activeObject = mat;
        return mat;
    }

    [MenuItem("Tools/Merge3/Apply Crystal Material to Selection")]
    public static void ApplyToSelection()
    {
        Material mat = CreateMaterial();
        if (mat == null) return;

        int applied = 0;
        foreach (GameObject go in Selection.gameObjects)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                Undo.RecordObject(r, "Apply Crystal Material");
                r.sharedMaterial = mat;
                EditorUtility.SetDirty(r);
                applied++;
            }
        }

        if (applied == 0)
            Debug.LogWarning("CrystalMaterialSetup: no Renderer found on the selection. Select the cell GameObjects (SpriteRenderer/MeshRenderer) and try again.");
        else
            Debug.Log($"CrystalMaterialSetup: applied MagicalCrystal to {applied} renderer(s). Enter Play mode to see the animation.");
    }
}
#endif

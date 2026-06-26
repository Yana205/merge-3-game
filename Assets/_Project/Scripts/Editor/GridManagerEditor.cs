using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GridManager))]
public class GridManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GridManager gm = (GridManager)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Editor Grid Preview", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Generate Grid", GUILayout.Height(30)))
            {
                Undo.RegisterFullObjectHierarchyUndo(gm.gameObject, "Generate Grid");
                gm.ClearGrid();
                gm.CreateGrid(gm.rows, gm.cols);
            }

            if (GUILayout.Button("Clear Grid", GUILayout.Height(30)))
            {
                Undo.RegisterFullObjectHierarchyUndo(gm.gameObject, "Clear Grid");
                gm.ClearGrid();
            }
        }
    }
}

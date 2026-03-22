using UnityEditor;
using UnityEngine;

/// <summary>
/// Plain-English purpose:
/// Custom inspector for TerrainGridAuthoring. Shows grid stats, recalculate + fill/clear
/// convenience buttons, and a link to open the dedicated TerrainGridAuthoringWindow.
/// </summary>
[CustomEditor(typeof(TerrainGridAuthoring))]
public class TerrainGridAuthoringEditor : Editor
{
    private SerializedProperty femaleTileSizeProperty;
    private SerializedProperty gridSizeInCellsProperty;
    private SerializedProperty drawGridGizmosProperty;
    private SerializedProperty gizmoPerformanceModeProperty;

    private void OnEnable()
    {
        femaleTileSizeProperty        = serializedObject.FindProperty("femaleTileSize");
        gridSizeInCellsProperty       = serializedObject.FindProperty("gridSizeInCells");
        drawGridGizmosProperty        = serializedObject.FindProperty("drawGridGizmos");
        gizmoPerformanceModeProperty  = serializedObject.FindProperty("gizmoPerformanceMode");
    }

    public override void OnInspectorGUI()
    {
        TerrainGridAuthoring authoring = (TerrainGridAuthoring)target;

        serializedObject.Update();

        EditorGUILayout.LabelField("Grid Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(femaleTileSizeProperty, new GUIContent("Female Tile Size", "Width and depth of one subtile cell in world units. Must match all furniture prefabs (default 0.5)."));
        EditorGUILayout.PropertyField(drawGridGizmosProperty, new GUIContent("Draw Grid Gizmos"));
        using (new EditorGUI.DisabledScope(!drawGridGizmosProperty.boolValue))
        {
            EditorGUILayout.PropertyField(gizmoPerformanceModeProperty,
                new GUIContent("  Performance Mode", "Draw one cube per full tile instead of per subtile — much faster on large grids."));
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(gridSizeInCellsProperty, new GUIContent("Grid Size (subtile cells)"));
        }

        serializedObject.ApplyModifiedProperties();
        authoring.EnsureValidData();

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Grid Summary", EditorStyles.boldLabel);

        Vector2Int cellSize = authoring.GridSizeInCells;
        int totalCells = cellSize.x * cellSize.y;
        int enabledCells = authoring.GetEnabledCellCount();
        EditorGUILayout.LabelField("Enabled Subtile Cells", $"{enabledCells} / {totalCells}");

        Vector2Int fullSize = authoring.FullTileGridSize;
        int totalFull = fullSize.x * fullSize.y;
        int walkable = authoring.GetWalkableFullTileCount();
        EditorGUILayout.LabelField("Walkable Full Tiles", $"{walkable} / {totalFull}");

        EditorGUILayout.Space(8);

        if (GUILayout.Button("Open Terrain Grid Authoring Tool"))
        {
            TerrainGridAuthoringWindow.Open(authoring);
        }

        EditorGUILayout.Space(4);

        if (GUILayout.Button("Recalculate from Colliders"))
        {
            Undo.RecordObject(authoring, "Recalculate Terrain Grid");
            authoring.RecalculateFromColliders();
            authoring.EnsureValidData();
            EditorUtility.SetDirty(authoring);
        }

        if (GUILayout.Button("Auto-Detect Terrain Sections"))
        {
            Undo.RecordObject(authoring, "Auto-Detect Terrain Sections");
            authoring.AutoDetectSections();
            EditorUtility.SetDirty(authoring);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Fill All"))
        {
            Undo.RecordObject(authoring, "Fill Terrain Grid");
            authoring.FillAll(true);
            EditorUtility.SetDirty(authoring);
        }
        if (GUILayout.Button("Clear All"))
        {
            Undo.RecordObject(authoring, "Clear Terrain Grid");
            authoring.FillAll(false);
            EditorUtility.SetDirty(authoring);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "Enabled cells = 0.5\u00d70.5 female subtiles available for furniture snapping.\n" +
            "A full 1\u00d71 tile is walkable when ALL of its subtile cells are enabled.\n" +
            "Use the Terrain Grid Authoring Tool for drag-to-paint cell editing.",
            MessageType.Info);
    }
}

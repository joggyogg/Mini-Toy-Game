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
    private SerializedProperty cullOffscreenGizmosProperty;

    private int cachedEnabledCells = -1;
    private int cachedWalkableTiles = -1;
    private bool statsDirty = true;

    private void OnEnable()
    {
        femaleTileSizeProperty = serializedObject.FindProperty("femaleTileSize");
        gridSizeInCellsProperty = serializedObject.FindProperty("gridSizeInCells");
        drawGridGizmosProperty = serializedObject.FindProperty("drawGridGizmos");
        gizmoPerformanceModeProperty = serializedObject.FindProperty("gizmoPerformanceMode");
        cullOffscreenGizmosProperty = serializedObject.FindProperty("cullOffscreenGizmos");
        statsDirty = true;
    }

    public override void OnInspectorGUI()
    {
        TerrainGridAuthoring authoring = (TerrainGridAuthoring)target;

        serializedObject.Update();

        EditorGUILayout.LabelField("Grid Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(femaleTileSizeProperty, new GUIContent("Female Tile Size", "Width and depth of one subtile cell in world units. Must match all furniture prefabs (default 0.5)."));
        EditorGUILayout.PropertyField(drawGridGizmosProperty, new GUIContent("Draw Grid Gizmos"));
        EditorGUILayout.PropertyField(gizmoPerformanceModeProperty, new GUIContent("Performance Mode", "When enabled, only full-tile overlays are drawn in gizmos (subtile gizmos hidden)."));
        EditorGUILayout.PropertyField(cullOffscreenGizmosProperty, new GUIContent("Cull Offscreen Gizmos", "Skip drawing gizmos when they are outside the scene camera frustum."));

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
        Vector2Int fullSize = authoring.FullTileGridSize;
        int totalFull = fullSize.x * fullSize.y;

        if (statsDirty)
        {
            cachedEnabledCells = authoring.GetEnabledCellCount();
            cachedWalkableTiles = authoring.GetWalkableFullTileCount();
            statsDirty = false;
        }

        EditorGUILayout.LabelField("Enabled Subtile Cells", $"{cachedEnabledCells} / {totalCells}");
        EditorGUILayout.LabelField("Walkable Full Tiles", $"{cachedWalkableTiles} / {totalFull}");

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
            statsDirty = true;
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Fill All"))
        {
            Undo.RecordObject(authoring, "Fill Terrain Grid");
            authoring.FillAll(true);
            EditorUtility.SetDirty(authoring);
            statsDirty = true;
        }
        if (GUILayout.Button("Clear All"))
        {
            Undo.RecordObject(authoring, "Clear Terrain Grid");
            authoring.FillAll(false);
            EditorUtility.SetDirty(authoring);
            statsDirty = true;
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

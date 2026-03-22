using UnityEditor;
using UnityEngine;

/// <summary>
/// Plain-English purpose:
/// This custom inspector keeps the PlaceableGridAuthoring component readable.
/// It shows the important summary fields and gives quick access to the dedicated grid authoring window.
/// </summary>
[CustomEditor(typeof(PlaceableGridAuthoring))]
public class PlaceableGridAuthoringEditor : Editor
{
    private SerializedProperty femaleTileSizeProperty;
    private SerializedProperty deriveMaleGridFromCollidersProperty;
    private SerializedProperty maleGridSizeInCellsProperty;
    private SerializedProperty objectHeightProperty;
    private SerializedProperty femaleGridRootGroupProperty;
    private SerializedProperty drawGridGizmosProperty;

    private void OnEnable()
    {
        femaleTileSizeProperty = serializedObject.FindProperty("femaleTileSize");
        deriveMaleGridFromCollidersProperty = serializedObject.FindProperty("deriveMaleGridFromColliders");
        maleGridSizeInCellsProperty = serializedObject.FindProperty("maleGridSizeInCells");
        objectHeightProperty = serializedObject.FindProperty("objectHeight");
        femaleGridRootGroupProperty = serializedObject.FindProperty("femaleGridRootGroup");
        drawGridGizmosProperty = serializedObject.FindProperty("drawGridGizmos");
    }

    public override void OnInspectorGUI()
    {
        PlaceableGridAuthoring authoring = (PlaceableGridAuthoring)target;

        serializedObject.Update();

        // The inspector is intentionally small. The heavier editing work happens in the separate editor window.
        EditorGUILayout.LabelField("Grid Summary", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(femaleTileSizeProperty, new GUIContent("Female Tile Size"));
        EditorGUILayout.PropertyField(objectHeightProperty, new GUIContent("Object Height"));
        EditorGUILayout.PropertyField(drawGridGizmosProperty, new GUIContent("Draw Grid Gizmos"));
        EditorGUILayout.PropertyField(deriveMaleGridFromCollidersProperty, new GUIContent("Derive Male Grid From Colliders"));

        using (new EditorGUI.DisabledScope(deriveMaleGridFromCollidersProperty.boolValue))
        {
            EditorGUILayout.PropertyField(maleGridSizeInCellsProperty, new GUIContent("Male Grid Size In Cells"));
        }

        EditorGUILayout.LabelField("Enabled Male Tiles", authoring.GetEnabledMaleCellCount().ToString());
        EditorGUILayout.LabelField("Has Female Hierarchy", (femaleGridRootGroupProperty != null).ToString());

        serializedObject.ApplyModifiedProperties();
        authoring.EnsureValidData();

        EditorGUILayout.Space();

        if (GUILayout.Button("Open Grid Authoring Tool"))
        {
            PlaceableGridAuthoringWindow.Open(authoring);
        }

        if (GUILayout.Button("Recalculate Male Grid From Colliders"))
        {
            // Record Undo before modifying serialized gameplay data.
            Undo.RecordObject(authoring, "Recalculate Male Grid");
            authoring.RecalculateMaleGridFromColliders();
            authoring.EnsureValidData();
            EditorUtility.SetDirty(authoring);
        }

        if (GUILayout.Button("Add Female Layer"))
        {
            Undo.RecordObject(authoring, "Add Female Layer");

            // Add a new layer to the root group when using the compact inspector.
            authoring.AddFemaleGridLayer(authoring.FemaleGridRootGroup, authoring.FemaleGridRootGroup.Children.Count);
            authoring.EnsureValidData();
            EditorUtility.SetDirty(authoring);
        }

        EditorGUILayout.HelpBox(
            "Use the editor window for drag painting. You can now paint both the male grid footprint and female grid layers. Female layer heights are measured upward from the male-grid base on the underside of the object. The inspector is intentionally kept compact so multi-layer shelf objects stay manageable.",
            MessageType.Info);
    }
}
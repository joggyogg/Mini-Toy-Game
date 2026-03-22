using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom inspector for TerrainNoiseGenerator.
/// Draws all serialized fields then adds a "Generate Terrain" button
/// that records undo state, runs the generator, and marks the object dirty.
/// </summary>
[CustomEditor(typeof(TerrainNoiseGenerator))]
public class TerrainNoiseGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8);

        if (GUILayout.Button("Generate Terrain", GUILayout.Height(32)))
        {
            var generator = (TerrainNoiseGenerator)target;
            TerrainGridAuthoring grid = generator.GetComponent<TerrainGridAuthoring>();

            if (grid != null)
            {
                Undo.RecordObject(grid, "Generate Noise Terrain");
                EditorUtility.SetDirty(grid);
            }

            generator.Generate();
            EditorUtility.SetDirty(generator);
        }
    }
}

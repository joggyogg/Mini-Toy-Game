using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TerrainSplatMapper))]
public class TerrainSplatMapperEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8);

        if (GUILayout.Button("Repaint Splatmaps"))
        {
            TerrainSplatMapper mapper = (TerrainSplatMapper)target;
            Terrain[] terrains = mapper.GetComponentInParent<TerrainGridAuthoring>()
                ?.GetComponentsInChildren<Terrain>();

            if (terrains == null || terrains.Length == 0)
            {
                Debug.LogWarning("[SplatMapper] No child Terrain components found.");
                return;
            }

            foreach (Terrain t in terrains)
                mapper.PaintSplatmaps(t);

            Debug.Log($"[SplatMapper] Repainted {terrains.Length} terrain(s).");
        }
    }
}

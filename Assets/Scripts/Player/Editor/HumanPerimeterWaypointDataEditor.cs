using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom inspector and scene-view gizmo overlay for HumanPerimeterWaypointData.
/// Shows a "Generate Waypoints" button and draws waypoint positions + facing arrows in the scene.
/// </summary>
[CustomEditor(typeof(HumanPerimeterWaypointData))]
public class HumanPerimeterWaypointDataEditor : Editor
{
    private static readonly Color PerimeterColor = new Color(0f, 0.8f, 1f, 0.8f);
    private static readonly Color InnerFootprintColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
    private static readonly Color EdgeWaypointColor = Color.yellow;
    private static readonly Color CornerWaypointColor = new Color(1f, 0.5f, 0f, 1f);
    private static readonly Color FacingArrowColor = new Color(0.2f, 1f, 0.2f, 0.9f);

    private const float DiscRadius = 1.5f;
    private const float ArrowLength = 5f;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        HumanPerimeterWaypointData data = (HumanPerimeterWaypointData)target;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Waypoint Summary", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Total Waypoints", data.Count.ToString());

        int corners = 0;
        int edges = 0;
        for (int i = 0; i < data.Count; i++)
        {
            var wp = data.GetWaypoint(i);
            if (wp.isCorner) corners++;
            else edges++;
        }
        EditorGUILayout.LabelField("Corner Waypoints", corners.ToString());
        EditorGUILayout.LabelField("Edge Waypoints", edges.ToString());

        EditorGUILayout.Space(4);

        if (GUILayout.Button("Generate Waypoints"))
        {
            Undo.RecordObject(data, "Generate Human Perimeter Waypoints");
            data.GenerateWaypoints();
            EditorUtility.SetDirty(data);
            SceneView.RepaintAll();
        }

        if (GUILayout.Button("Open Waypoint Editor Window"))
        {
            HumanWaypointEditorWindow.Open(data);
        }

        if (data.Count > 0)
        {
            if (GUILayout.Button("Clear Waypoints"))
            {
                Undo.RecordObject(data, "Clear Human Perimeter Waypoints");
                // Access the serialized list and clear it
                SerializedProperty waypointsProp = serializedObject.FindProperty("waypoints");
                if (waypointsProp != null)
                {
                    waypointsProp.ClearArray();
                    serializedObject.ApplyModifiedProperties();
                }
                EditorUtility.SetDirty(data);
                SceneView.RepaintAll();
            }
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "Generate Waypoints creates a ring of positions around the Model Terrain Parent.\n" +
            "Yellow = edge waypoints, Orange = corner waypoints.\n" +
            "Green arrows show the facing direction at each waypoint.",
            MessageType.Info);

        // ── Preset Save / Load ────────────────────────────────────────────────
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Preset", EditorStyles.boldLabel);

        if (GUILayout.Button("Save to New Preset Asset"))
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Waypoint Preset", "HumanWaypointPreset", "asset",
                "Choose where to save the waypoint preset.");
            if (!string.IsNullOrEmpty(path))
            {
                var preset = ScriptableObject.CreateInstance<HumanWaypointPreset>();
                data.SaveToPreset(preset);
                AssetDatabase.CreateAsset(preset, path);
                AssetDatabase.SaveAssets();
                EditorGUIUtility.PingObject(preset);
                Debug.Log($"[HumanWaypoints] Preset saved to {path}");
            }
        }

        if (GUILayout.Button("Save to Existing Preset Asset"))
        {
            string path = EditorUtility.OpenFilePanel("Pick Preset Asset", "Assets", "asset");
            if (!string.IsNullOrEmpty(path))
            {
                path = FileUtil.GetProjectRelativePath(path);
                var preset = AssetDatabase.LoadAssetAtPath<HumanWaypointPreset>(path);
                if (preset != null)
                {
                    Undo.RecordObject(preset, "Update Waypoint Preset");
                    data.SaveToPreset(preset);
                    EditorUtility.SetDirty(preset);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[HumanWaypoints] Preset updated at {path}");
                }
                else
                {
                    Debug.LogWarning("[HumanWaypoints] Selected file is not a HumanWaypointPreset.");
                }
            }
        }

        if (GUILayout.Button("Load from Preset Asset"))
        {
            string path = EditorUtility.OpenFilePanel("Pick Preset Asset", "Assets", "asset");
            if (!string.IsNullOrEmpty(path))
            {
                path = FileUtil.GetProjectRelativePath(path);
                var preset = AssetDatabase.LoadAssetAtPath<HumanWaypointPreset>(path);
                if (preset != null)
                {
                    Undo.RecordObject(data, "Load Waypoint Preset");
                    data.LoadFromPreset(preset);
                    EditorUtility.SetDirty(data);
                    SceneView.RepaintAll();
                    Debug.Log($"[HumanWaypoints] Preset loaded from {path}");
                }
                else
                {
                    Debug.LogWarning("[HumanWaypoints] Selected file is not a HumanWaypointPreset.");
                }
            }
        }
    }

    private void OnSceneGUI()
    {
        HumanPerimeterWaypointData data = (HumanPerimeterWaypointData)target;
        if (data.Count == 0) return;

        // Draw the inner model terrain footprint rectangle
        Bounds footprint = data.GetModelTerrainFootprint();
        DrawXZRect(footprint.center, footprint.extents.x, footprint.extents.z, InnerFootprintColor);

        // Draw the room terrain perimeter rectangle (where waypoints live)
        if (data.TryGetRoomPerimeterRect(out Vector3 roomMin, out Vector3 roomMax))
        {
            Vector3 roomCenter = (roomMin + roomMax) * 0.5f;
            float roomHalfX = (roomMax.x - roomMin.x) * 0.5f;
            float roomHalfZ = (roomMax.z - roomMin.z) * 0.5f;
            DrawXZRect(roomCenter, roomHalfX, roomHalfZ, PerimeterColor);
        }

        // Draw each waypoint
        for (int i = 0; i < data.Count; i++)
        {
            var wp = data.GetWaypoint(i);
            Color discColor = wp.isCorner ? CornerWaypointColor : EdgeWaypointColor;

            Handles.color = discColor;
            Handles.DrawSolidDisc(wp.worldPosition, Vector3.up, DiscRadius);

            // Facing arrow
            Handles.color = FacingArrowColor;
            Vector3 arrowEnd = wp.worldPosition + wp.facingRotation * Vector3.forward * ArrowLength;
            Handles.DrawLine(wp.worldPosition, arrowEnd);
            Handles.ConeHandleCap(0, arrowEnd, wp.facingRotation, DiscRadius * 0.6f, EventType.Repaint);

            // Label
            Handles.color = Color.white;
            Handles.Label(wp.worldPosition + Vector3.up * 2f, $"#{i}" + (wp.isCorner ? " (corner)" : ""));
        }

        // Draw lines connecting adjacent waypoints
        Handles.color = new Color(0f, 0.8f, 1f, 0.3f);
        for (int i = 0; i < data.Count; i++)
        {
            int next = (i + 1) % data.Count;
            Handles.DrawDottedLine(data.GetWaypoint(i).worldPosition, data.GetWaypoint(next).worldPosition, 4f);
        }
    }

    private static void DrawXZRect(Vector3 center, float halfX, float halfZ, Color color)
    {
        float y = center.y;
        Vector3 tl = new Vector3(center.x - halfX, y, center.z + halfZ);
        Vector3 tr = new Vector3(center.x + halfX, y, center.z + halfZ);
        Vector3 br = new Vector3(center.x + halfX, y, center.z - halfZ);
        Vector3 bl = new Vector3(center.x - halfX, y, center.z - halfZ);

        Handles.color = color;
        Handles.DrawPolyLine(tl, tr, br, bl, tl);
    }
}

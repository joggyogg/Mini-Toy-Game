using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor window for visually editing human perimeter waypoints.
/// Shows a top-down map of the Room Terrain with waypoints drawn on it.
/// Click to select, Ctrl/Shift-click to multi-select, then adjust facing rotation.
/// </summary>
public class HumanWaypointEditorWindow : EditorWindow
{
    // ── Layout constants ──────────────────────────────────────────────────────────
    private const float LeftPanelWidth = 260f;
    private const float WaypointDiscRadius = 8f;
    private const float ArrowLength = 18f;
    private const float MapPadding = 40f;

    // ── Colors ────────────────────────────────────────────────────────────────────
    private static readonly Color TerrainFillColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    private static readonly Color TerrainBorderColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    private static readonly Color ModelFootprintColor = new Color(1f, 0.6f, 0.2f, 0.4f);
    private static readonly Color ModelFootprintBorder = new Color(1f, 0.6f, 0.2f, 0.8f);
    private static readonly Color PerimeterRectColor = new Color(0f, 0.8f, 1f, 0.5f);
    private static readonly Color EdgeWaypointColor = Color.yellow;
    private static readonly Color CornerWaypointColor = new Color(1f, 0.5f, 0f, 1f);
    private static readonly Color SelectedWaypointColor = new Color(0.2f, 0.8f, 1f, 1f);
    private static readonly Color FacingArrowColor = new Color(0.2f, 1f, 0.2f, 0.9f);
    private static readonly Color ConnectionLineColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);

    // ── State ─────────────────────────────────────────────────────────────────────
    private HumanPerimeterWaypointData targetData;
    private HashSet<int> selectedIndices = new HashSet<int>();
    private float rotationYawDegrees;
    private Vector2 leftPanelScroll;

    // ── Public entry point ────────────────────────────────────────────────────────

    [MenuItem("Window/Mini Toy Game/Human Waypoint Editor")]
    public static void Open()
    {
        var window = GetWindow<HumanWaypointEditorWindow>("Human Waypoints");
        window.minSize = new Vector2(700f, 450f);
        window.Show();
    }

    public static void Open(HumanPerimeterWaypointData data)
    {
        var window = GetWindow<HumanWaypointEditorWindow>("Human Waypoints");
        window.minSize = new Vector2(700f, 450f);
        window.targetData = data;
        window.selectedIndices.Clear();
        window.Show();
    }

    // ── Unity messages ────────────────────────────────────────────────────────────

    private void OnSelectionChange()
    {
        if (Selection.activeGameObject == null) return;
        var data = Selection.activeGameObject.GetComponent<HumanPerimeterWaypointData>();
        if (data != null)
        {
            targetData = data;
            selectedIndices.Clear();
            Repaint();
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        DrawLeftPanel();
        DrawMapPanel();
        EditorGUILayout.EndHorizontal();
    }

    // ── Left panel ────────────────────────────────────────────────────────────────

    private void DrawLeftPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(LeftPanelWidth));
        leftPanelScroll = EditorGUILayout.BeginScrollView(leftPanelScroll);

        EditorGUILayout.LabelField("Human Waypoint Editor", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        // Target picker
        HumanPerimeterWaypointData newTarget = (HumanPerimeterWaypointData)EditorGUILayout.ObjectField(
            "Waypoint Data", targetData, typeof(HumanPerimeterWaypointData), true);
        if (newTarget != targetData)
        {
            targetData = newTarget;
            selectedIndices.Clear();
        }

        if (targetData == null)
        {
            EditorGUILayout.HelpBox("Assign a HumanPerimeterWaypointData component (from Room Terrain).", MessageType.Info);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Waypoints", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Total", targetData.Count.ToString());

        if (GUILayout.Button("Generate Waypoints"))
        {
            Undo.RecordObject(targetData, "Generate Waypoints");
            targetData.GenerateWaypoints();
            EditorUtility.SetDirty(targetData);
            selectedIndices.Clear();
            SceneView.RepaintAll();
            Repaint();
        }

        // ── Preset Save / Load ────────────────────────────────────────────────
        EditorGUILayout.Space(4);
        if (GUILayout.Button("Save to New Preset"))
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Waypoint Preset", "HumanWaypointPreset", "asset",
                "Choose where to save the waypoint preset.");
            if (!string.IsNullOrEmpty(path))
            {
                var preset = ScriptableObject.CreateInstance<HumanWaypointPreset>();
                targetData.SaveToPreset(preset);
                AssetDatabase.CreateAsset(preset, path);
                AssetDatabase.SaveAssets();
                EditorGUIUtility.PingObject(preset);
            }
        }

        if (GUILayout.Button("Load from Preset"))
        {
            string path = EditorUtility.OpenFilePanel("Pick Preset Asset", "Assets", "asset");
            if (!string.IsNullOrEmpty(path))
            {
                path = FileUtil.GetProjectRelativePath(path);
                var preset = AssetDatabase.LoadAssetAtPath<HumanWaypointPreset>(path);
                if (preset != null)
                {
                    Undo.RecordObject(targetData, "Load Waypoint Preset");
                    targetData.LoadFromPreset(preset);
                    EditorUtility.SetDirty(targetData);
                    selectedIndices.Clear();
                    SceneView.RepaintAll();
                    Repaint();
                }
            }
        }

        EditorGUILayout.Space(8);

        // ── Selection info ────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Selection", EditorStyles.boldLabel);

        if (selectedIndices.Count == 0)
        {
            EditorGUILayout.HelpBox("Click a waypoint on the map to select it.\nCtrl+Click or Shift+Click for multi-select.", MessageType.None);
        }
        else
        {
            EditorGUILayout.LabelField("Selected", selectedIndices.Count.ToString());

            // Show current rotation (use first selected as reference)
            int firstSelected = -1;
            foreach (int idx in selectedIndices) { firstSelected = idx; break; }

            bool firstIsCorner = false;
            if (firstSelected >= 0 && firstSelected < targetData.Count)
            {
                var wp = targetData.GetWaypoint(firstSelected);
                firstIsCorner = wp.isCorner;
                float currentYaw = wp.facingRotation.eulerAngles.y;
                EditorGUILayout.LabelField("Reference Yaw (°)", currentYaw.ToString("F1"));
            }

            EditorGUILayout.Space(4);

            if (firstIsCorner)
            {
                EditorGUILayout.LabelField("Corner Facings", EditorStyles.miniBoldLabel);

                var refWp = targetData.GetWaypoint(firstSelected);
                float yawA = refWp.cornerFacingA.eulerAngles.y;
                float yawDiag = refWp.facingRotation.eulerAngles.y;
                float yawB = refWp.cornerFacingB.eulerAngles.y;

                float newYawA = EditorGUILayout.Slider("A  Yaw (prev edge)", yawA, 0f, 360f);
                float newYawDiag = EditorGUILayout.Slider("Diag Yaw (corner)", yawDiag, 0f, 360f);
                float newYawB = EditorGUILayout.Slider("B  Yaw (next edge)", yawB, 0f, 360f);

                if (!Mathf.Approximately(newYawA, yawA) ||
                    !Mathf.Approximately(newYawDiag, yawDiag) ||
                    !Mathf.Approximately(newYawB, yawB))
                {
                    Undo.RecordObject(targetData, "Set Corner Facings");
                    foreach (int idx in selectedIndices)
                    {
                        if (targetData.GetWaypoint(idx).isCorner)
                        {
                            targetData.SetCornerFacingA(idx, Quaternion.Euler(0f, newYawA, 0f));
                            targetData.SetWaypointFacing(idx, Quaternion.Euler(0f, newYawDiag, 0f));
                            targetData.SetCornerFacingB(idx, Quaternion.Euler(0f, newYawB, 0f));
                        }
                    }
                    EditorUtility.SetDirty(targetData);
                    SceneView.RepaintAll();
                    Repaint();
                }
            }
            else
            {
                EditorGUILayout.LabelField("Set Facing (Y rotation)", EditorStyles.miniBoldLabel);

                rotationYawDegrees = EditorGUILayout.Slider("Yaw (°)", rotationYawDegrees, 0f, 360f);

                if (GUILayout.Button("Apply Rotation to Selected"))
                {
                    ApplyRotationToSelected(rotationYawDegrees);
                }
            }

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Select All"))
            {
                selectedIndices.Clear();
                for (int i = 0; i < targetData.Count; i++) selectedIndices.Add(i);
                Repaint();
            }

            if (GUILayout.Button("Deselect All"))
            {
                selectedIndices.Clear();
                Repaint();
            }
        }

        // ── Waypoint list ─────────────────────────────────────────────────────
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Waypoint List", EditorStyles.boldLabel);

        for (int i = 0; i < targetData.Count; i++)
        {
            var wp = targetData.GetWaypoint(i);
            bool isSelected = selectedIndices.Contains(i);
            string label;
            if (wp.isCorner)
                label = $"#{i} (corner) A={wp.cornerFacingA.eulerAngles.y:F0}° D={wp.facingRotation.eulerAngles.y:F0}° B={wp.cornerFacingB.eulerAngles.y:F0}°";
            else
                label = $"#{i}  yaw={wp.facingRotation.eulerAngles.y:F0}°";

            GUIStyle style = isSelected ? EditorStyles.boldLabel : EditorStyles.label;
            Color prevBg = GUI.backgroundColor;
            if (isSelected) GUI.backgroundColor = new Color(0.3f, 0.6f, 1f, 0.5f);

            EditorGUILayout.BeginHorizontal("box");
            GUI.backgroundColor = prevBg;

            if (GUILayout.Button(label, style))
            {
                HandleListClick(i);
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // ── Map panel (right side) ────────────────────────────────────────────────────

    private void DrawMapPanel()
    {
        Rect mapArea = GUILayoutUtility.GetRect(100, 10000, 100, 10000);

        if (targetData == null || targetData.Count == 0)
        {
            EditorGUI.DrawRect(mapArea, new Color(0.15f, 0.15f, 0.15f, 1f));
            GUI.Label(new Rect(mapArea.center.x - 80, mapArea.center.y - 10, 160, 20), "No waypoints to display");
            return;
        }

        // Get world-space bounds
        if (!targetData.TryGetRoomTerrainFullRect(out Vector3 worldMin, out Vector3 worldMax))
        {
            EditorGUI.DrawRect(mapArea, new Color(0.15f, 0.15f, 0.15f, 1f));
            GUI.Label(new Rect(mapArea.center.x - 80, mapArea.center.y - 10, 160, 20), "No Room Terrain found");
            return;
        }

        // Background
        EditorGUI.DrawRect(mapArea, new Color(0.12f, 0.12f, 0.12f, 1f));

        // Compute world → screen mapping (fit terrain rect into mapArea with padding)
        float worldWidth = worldMax.x - worldMin.x;
        float worldHeight = worldMax.z - worldMin.z;
        Rect innerArea = new Rect(
            mapArea.x + MapPadding,
            mapArea.y + MapPadding,
            mapArea.width - MapPadding * 2,
            mapArea.height - MapPadding * 2);

        float scaleX = innerArea.width / worldWidth;
        float scaleZ = innerArea.height / worldHeight;
        float scale = Mathf.Min(scaleX, scaleZ);

        // Center the map
        float usedWidth = worldWidth * scale;
        float usedHeight = worldHeight * scale;
        float offsetX = innerArea.x + (innerArea.width - usedWidth) * 0.5f;
        float offsetY = innerArea.y + (innerArea.height - usedHeight) * 0.5f;

        // World XZ to screen pixel (Z is flipped so +Z = top of screen)
        System.Func<Vector3, Vector2> worldToScreen = (Vector3 w) =>
        {
            float sx = offsetX + (w.x - worldMin.x) * scale;
            float sy = offsetY + (worldMax.z - w.z) * scale; // flip Z
            return new Vector2(sx, sy);
        };

        // Draw terrain fill
        Rect terrainScreenRect = new Rect(offsetX, offsetY, usedWidth, usedHeight);
        EditorGUI.DrawRect(terrainScreenRect, TerrainFillColor);
        DrawRectOutline(terrainScreenRect, TerrainBorderColor);

        // Draw model terrain footprint
        Bounds modelFoot = targetData.GetModelTerrainFootprint();
        Vector2 modelMin2D = worldToScreen(new Vector3(modelFoot.min.x, 0, modelFoot.max.z));
        Vector2 modelMax2D = worldToScreen(new Vector3(modelFoot.max.x, 0, modelFoot.min.z));
        Rect modelRect = Rect.MinMaxRect(modelMin2D.x, modelMin2D.y, modelMax2D.x, modelMax2D.y);
        EditorGUI.DrawRect(modelRect, ModelFootprintColor);
        DrawRectOutline(modelRect, ModelFootprintBorder);

        // Draw perimeter inset rect
        if (targetData.TryGetRoomPerimeterRect(out Vector3 periMin, out Vector3 periMax))
        {
            Vector2 pMin2D = worldToScreen(new Vector3(periMin.x, 0, periMax.z));
            Vector2 pMax2D = worldToScreen(new Vector3(periMax.x, 0, periMin.z));
            Rect periRect = Rect.MinMaxRect(pMin2D.x, pMin2D.y, pMax2D.x, pMax2D.y);
            DrawRectOutline(periRect, PerimeterRectColor);
        }

        // Draw connection lines
        Handles.BeginGUI();
        Handles.color = ConnectionLineColor;
        for (int i = 0; i < targetData.Count; i++)
        {
            int next = (i + 1) % targetData.Count;
            Vector2 a = worldToScreen(targetData.GetWaypoint(i).worldPosition);
            Vector2 b = worldToScreen(targetData.GetWaypoint(next).worldPosition);
            Handles.DrawLine(new Vector3(a.x, a.y, 0), new Vector3(b.x, b.y, 0));
        }

        // Draw waypoints
        for (int i = 0; i < targetData.Count; i++)
        {
            var wp = targetData.GetWaypoint(i);
            Vector2 screenPos = worldToScreen(wp.worldPosition);
            bool isSelected = selectedIndices.Contains(i);

            // Disc
            Color discColor = isSelected ? SelectedWaypointColor
                : wp.isCorner ? CornerWaypointColor
                : EdgeWaypointColor;

            Handles.color = discColor;
            Handles.DrawSolidDisc(new Vector3(screenPos.x, screenPos.y, 0), Vector3.forward, WaypointDiscRadius);

            // Facing arrow(s)
            if (wp.isCorner)
            {
                // Draw 3 arrows for corner: A (red), Diagonal (green), B (blue)
                DrawFacingArrow(screenPos, wp.cornerFacingA, new Color(1f, 0.3f, 0.3f, 0.9f));
                DrawFacingArrow(screenPos, wp.facingRotation, FacingArrowColor);
                DrawFacingArrow(screenPos, wp.cornerFacingB, new Color(0.3f, 0.5f, 1f, 0.9f));
            }
            else
            {
                DrawFacingArrow(screenPos, wp.facingRotation, FacingArrowColor);
            }

            // Label
            Handles.color = Color.white;
            Handles.Label(new Vector3(screenPos.x + WaypointDiscRadius + 2, screenPos.y - 6, 0),
                $"#{i}", isSelected ? EditorStyles.whiteBoldLabel : EditorStyles.whiteLabel);
        }

        Handles.EndGUI();

        // ── Handle clicks on the map ──────────────────────────────────────────
        HandleMapClicks(mapArea, worldToScreen, worldMin, worldMax, scale, offsetX, offsetY);
    }

    // ── Click handling ────────────────────────────────────────────────────────────

    private void HandleMapClicks(Rect mapArea, System.Func<Vector3, Vector2> worldToScreen,
        Vector3 worldMin, Vector3 worldMax, float scale, float offsetX, float offsetY)
    {
        Event e = Event.current;
        if (e.type != EventType.MouseDown || e.button != 0) return;
        if (!mapArea.Contains(e.mousePosition)) return;

        Vector2 mousePos = e.mousePosition;

        // Find closest waypoint to click
        int closestIdx = -1;
        float closestDist = WaypointDiscRadius + 4f; // click tolerance

        for (int i = 0; i < targetData.Count; i++)
        {
            Vector2 wpScreen = worldToScreen(targetData.GetWaypoint(i).worldPosition);
            float dist = Vector2.Distance(mousePos, wpScreen);
            if (dist < closestDist)
            {
                closestDist = dist;
                closestIdx = i;
            }
        }

        bool ctrl = e.control || e.command;
        bool shift = e.shift;

        if (closestIdx >= 0)
        {
            if (ctrl || shift)
            {
                // Toggle selection
                if (selectedIndices.Contains(closestIdx))
                    selectedIndices.Remove(closestIdx);
                else
                    selectedIndices.Add(closestIdx);
            }
            else
            {
                // Single select
                selectedIndices.Clear();
                selectedIndices.Add(closestIdx);
            }

            // Sync rotation slider to first selected
            if (selectedIndices.Count > 0)
            {
                int first = -1;
                foreach (int idx in selectedIndices) { first = idx; break; }
                if (first >= 0 && first < targetData.Count)
                    rotationYawDegrees = targetData.GetWaypoint(first).facingRotation.eulerAngles.y;
            }
        }
        else if (!ctrl && !shift)
        {
            selectedIndices.Clear();
        }

        e.Use();
        Repaint();
    }

    private void HandleListClick(int index)
    {
        Event e = Event.current;
        bool ctrl = e != null && (e.control || e.command);
        bool shift = e != null && e.shift;

        if (ctrl || shift)
        {
            if (selectedIndices.Contains(index))
                selectedIndices.Remove(index);
            else
                selectedIndices.Add(index);
        }
        else
        {
            selectedIndices.Clear();
            selectedIndices.Add(index);
        }

        if (selectedIndices.Count > 0 && index < targetData.Count)
            rotationYawDegrees = targetData.GetWaypoint(index).facingRotation.eulerAngles.y;

        Repaint();
    }

    // ── Rotation application ──────────────────────────────────────────────────────

    private void ApplyRotationToSelected(float yawDegrees)
    {
        if (targetData == null || selectedIndices.Count == 0) return;

        Undo.RecordObject(targetData, "Set Waypoint Rotation");
        Quaternion rot = Quaternion.Euler(0f, yawDegrees, 0f);

        foreach (int idx in selectedIndices)
        {
            targetData.SetWaypointFacing(idx, rot);
        }

        EditorUtility.SetDirty(targetData);
        SceneView.RepaintAll();
        Repaint();
    }

    // ── Drawing helpers ───────────────────────────────────────────────────────────
    private static void DrawFacingArrow(Vector2 screenPos, Quaternion rotation, Color color)
    {
        Vector3 fwd3D = rotation * Vector3.forward;
        Vector2 fwd2D = new Vector2(fwd3D.x, -fwd3D.z).normalized;
        Vector2 arrowEnd = screenPos + fwd2D * ArrowLength;

        Handles.color = color;
        Handles.DrawLine(new Vector3(screenPos.x, screenPos.y, 0), new Vector3(arrowEnd.x, arrowEnd.y, 0));

        Vector2 perpendicular = new Vector2(-fwd2D.y, fwd2D.x);
        Vector2 headA = arrowEnd - fwd2D * 5f + perpendicular * 3f;
        Vector2 headB = arrowEnd - fwd2D * 5f - perpendicular * 3f;
        Handles.DrawLine(new Vector3(arrowEnd.x, arrowEnd.y, 0), new Vector3(headA.x, headA.y, 0));
        Handles.DrawLine(new Vector3(arrowEnd.x, arrowEnd.y, 0), new Vector3(headB.x, headB.y, 0));
    }
    private static void DrawRectOutline(Rect r, Color color)
    {
        Handles.BeginGUI();
        Handles.color = color;
        Vector3 tl = new Vector3(r.xMin, r.yMin, 0);
        Vector3 tr = new Vector3(r.xMax, r.yMin, 0);
        Vector3 br = new Vector3(r.xMax, r.yMax, 0);
        Vector3 bl = new Vector3(r.xMin, r.yMax, 0);
        Handles.DrawLine(tl, tr);
        Handles.DrawLine(tr, br);
        Handles.DrawLine(br, bl);
        Handles.DrawLine(bl, tl);
        Handles.EndGUI();
    }
}

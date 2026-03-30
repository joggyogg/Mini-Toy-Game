using UnityEngine;
using UnityEditor;

public class AssetResizerWindow : EditorWindow
{
    // Width / Height / Depth (all independently editable)
    private float _desiredWidth  = 1f;  // X
    private float _desiredHeight = 1f;  // Y
    private float _desiredDepth  = 1f;  // Z

    private bool _lockProportions = false;
    // Which axis drives proportional scaling: 0=W, 1=H, 2=D
    private int  _lockedAxis = 0;

    [MenuItem("Tools/Asset Resizer")]
    public static void Open() => GetWindow<AssetResizerWindow>("Asset Resizer");

    private void OnSelectionChange() => Repaint();

    private void OnGUI()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            EditorGUILayout.HelpBox("Select a GameObject in the scene or hierarchy.", MessageType.Info);
            return;
        }

        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            EditorGUILayout.HelpBox("No Renderer found on the selected object.", MessageType.Warning);
            return;
        }

        // Combine all child renderer bounds into one world-space box.
        Bounds worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            worldBounds.Encapsulate(renderers[i].bounds);

        // Back out lossyScale to find what the mesh measures at scale (1,1,1).
        Vector3 ls = go.transform.lossyScale;
        Vector3 nat = new Vector3(
            ls.x != 0f ? worldBounds.size.x / ls.x : 0f,
            ls.y != 0f ? worldBounds.size.y / ls.y : 0f,
            ls.z != 0f ? worldBounds.size.z / ls.z : 0f
        );

        // ── Current measurements ──────────────────────────────────────────────
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField(go.name, EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        DrawReadOnlySize("Current world size (units)", worldBounds.size);
        EditorGUILayout.Space(2);
        DrawReadOnlySize("Natural size at scale (1,1,1)", nat);
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("Grid reference: 1 tile = 1 world unit  |  subtile = 0.5", EditorStyles.miniLabel);
        EditorGUILayout.Space(8);

        // ── Target size ───────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Target size (world units)", EditorStyles.boldLabel);

        // Lock proportions toggle
        bool newLock = EditorGUILayout.Toggle("Lock proportions", _lockProportions);
        if (newLock != _lockProportions)
        {
            _lockProportions = newLock;
            // When turning lock on, snap the other two axes to match current driver
            if (_lockProportions && nat[_lockedAxis] > 0f)
            {
                float ratio = GetDesired(_lockedAxis) / nat[_lockedAxis];
                SetDerived(_lockedAxis, nat, ratio);
            }
        }

        if (_lockProportions)
            _lockedAxis = GUILayout.Toolbar(_lockedAxis, new[] { "Drive W", "Drive H", "Drive D" });

        EditorGUILayout.Space(4);

        // Input row
        EditorGUILayout.BeginHorizontal();

        // W
        EditorGUILayout.LabelField("W (X)", GUILayout.Width(40));
        using (new EditorGUI.DisabledScope(_lockProportions && _lockedAxis != 0))
        {
            float newW = Mathf.Max(0.001f, EditorGUILayout.FloatField(_desiredWidth));
            if (!Mathf.Approximately(newW, _desiredWidth))
            {
                _desiredWidth = newW;
                if (_lockProportions && nat.x > 0f)
                    SetDerived(0, nat, newW / nat.x);
            }
        }

        GUILayout.Space(6);

        // H
        EditorGUILayout.LabelField("H (Y)", GUILayout.Width(40));
        using (new EditorGUI.DisabledScope(_lockProportions && _lockedAxis != 1))
        {
            float newH = Mathf.Max(0.001f, EditorGUILayout.FloatField(_desiredHeight));
            if (!Mathf.Approximately(newH, _desiredHeight))
            {
                _desiredHeight = newH;
                if (_lockProportions && nat.y > 0f)
                    SetDerived(1, nat, newH / nat.y);
            }
        }

        GUILayout.Space(6);

        // D
        EditorGUILayout.LabelField("D (Z)", GUILayout.Width(40));
        using (new EditorGUI.DisabledScope(_lockProportions && _lockedAxis != 2))
        {
            float newD = Mathf.Max(0.001f, EditorGUILayout.FloatField(_desiredDepth));
            if (!Mathf.Approximately(newD, _desiredDepth))
            {
                _desiredDepth = newD;
                if (_lockProportions && nat.z > 0f)
                    SetDerived(2, nat, newD / nat.z);
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField($"Footprint: {_desiredWidth:F2} × {_desiredDepth:F2} tiles", EditorStyles.miniLabel);
        EditorGUILayout.Space(4);

        // Preset rows — presets only show for the active (driver) axis when locked
        bool showW = !_lockProportions || _lockedAxis == 0;
        bool showH = !_lockProportions || _lockedAxis == 1;
        bool showD = !_lockProportions || _lockedAxis == 2;

        if (showW)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("W", EditorStyles.miniLabel, GUILayout.Width(10));
            foreach (float t in new[] { 0.5f, 1f, 2f, 3f, 4f })
            {
                if (GUILayout.Button($"{t}t"))
                {
                    _desiredWidth = t;
                    if (_lockProportions && nat.x > 0f) SetDerived(0, nat, t / nat.x);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        if (showH)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("H", EditorStyles.miniLabel, GUILayout.Width(10));
            foreach (float t in new[] { 0.5f, 1f, 2f, 3f, 4f })
            {
                if (GUILayout.Button($"{t}t"))
                {
                    _desiredHeight = t;
                    if (_lockProportions && nat.y > 0f) SetDerived(1, nat, t / nat.y);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        if (showD)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("D", EditorStyles.miniLabel, GUILayout.Width(10));
            foreach (float t in new[] { 0.5f, 1f, 2f, 3f, 4f })
            {
                if (GUILayout.Button($"{t}t"))
                {
                    _desiredDepth = t;
                    if (_lockProportions && nat.z > 0f) SetDerived(2, nat, t / nat.z);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(8);

        // ── Compute localScale ────────────────────────────────────────────────
        Vector3 targetLossyScale = new Vector3(
            nat.x > 0f ? _desiredWidth  / nat.x : ls.x,
            nat.y > 0f ? _desiredHeight / nat.y : ls.y,
            nat.z > 0f ? _desiredDepth  / nat.z : ls.z
        );

        Vector3 parentLS = go.transform.parent != null ? go.transform.parent.lossyScale : Vector3.one;
        Vector3 newLocalScale = new Vector3(
            parentLS.x != 0f ? targetLossyScale.x / parentLS.x : targetLossyScale.x,
            parentLS.y != 0f ? targetLossyScale.y / parentLS.y : targetLossyScale.y,
            parentLS.z != 0f ? targetLossyScale.z / parentLS.z : targetLossyScale.z
        );

        EditorGUILayout.LabelField($"New localScale: ({newLocalScale.x:F4}, {newLocalScale.y:F4}, {newLocalScale.z:F4})");
        EditorGUILayout.Space(4);

        if (GUILayout.Button("Apply Scale", GUILayout.Height(28)))
        {
            Undo.RecordObject(go.transform, "Resize Asset to World Units");
            go.transform.localScale = newLocalScale;
        }
    }

    // Returns the current desired value for the given axis index.
    private float GetDesired(int axis) => axis == 0 ? _desiredWidth : axis == 1 ? _desiredHeight : _desiredDepth;

    // Applies a uniform ratio to the two axes that are NOT the driver.
    private void SetDerived(int driverAxis, Vector3 nat, float ratio)
    {
        if (driverAxis != 0) _desiredWidth  = nat.x * ratio;
        if (driverAxis != 1) _desiredHeight = nat.y * ratio;
        if (driverAxis != 2) _desiredDepth  = nat.z * ratio;
    }

    private static void DrawReadOnlySize(string label, Vector3 size)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.LabelField($"W (X): {size.x:F3}    H (Y): {size.y:F3}    D (Z): {size.z:F3}");
        EditorGUI.indentLevel--;
    }
}

using UnityEditor;
using UnityEngine;

/// <summary>
/// Plain-English purpose:
/// Custom inspector for DecorateMinimapUI.
/// Shows a rebuild button so designers can force a texture refresh without entering play mode.
/// </summary>
[CustomEditor(typeof(DecorateMinimapUI))]
public class DecorateMinimapEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "Layer Up/Down buttons cycle through unique female-layer heights found on placed furniture.\n" +
            "Left-drag to reposition furniture. Right-click a footprint to rotate 90\u00b0.",
            MessageType.Info);
    }
}

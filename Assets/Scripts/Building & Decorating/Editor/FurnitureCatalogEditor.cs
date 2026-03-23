using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(FurnitureCatalog))]
public class FurnitureCatalogEditor : Editor
{
    private SerializedProperty _folderProp;

    private void OnEnable()
    {
        _folderProp = serializedObject.FindProperty("definitionsFolder");
        // Auto-refresh whenever the inspector opens so the list is always current.
        ((FurnitureCatalog)target).RefreshFromFolder();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space(4);

        // Folder field
        EditorGUILayout.LabelField("Source Folder", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(_folderProp, GUIContent.none);
        if (GUILayout.Button("Browse", GUILayout.Width(58)))
        {
            string chosen = EditorUtility.OpenFolderPanel(
                "Choose definitions folder",
                Application.dataPath, "");
            if (!string.IsNullOrEmpty(chosen))
            {
                // Convert absolute path back to project-relative "Assets/..."
                if (chosen.StartsWith(Application.dataPath))
                    chosen = "Assets" + chosen.Substring(Application.dataPath.Length).Replace('\\', '/');
                _folderProp.stringValue = chosen;
                serializedObject.ApplyModifiedProperties();
                ((FurnitureCatalog)target).RefreshFromFolder();
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        // Refresh button
        if (GUILayout.Button("Refresh from Folder", GUILayout.Height(24)))
            ((FurnitureCatalog)target).RefreshFromFolder();

        EditorGUILayout.Space(4);

        // Read-only list preview
        var catalog = (FurnitureCatalog)target;
        EditorGUILayout.LabelField($"Definitions found: {catalog.Items.Count}", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            foreach (var def in catalog.Items)
                EditorGUILayout.ObjectField(def != null ? def.DisplayName : "(null)", def,
                    typeof(FurnitureDefinition), false);
        }

        serializedObject.ApplyModifiedProperties();
    }
}

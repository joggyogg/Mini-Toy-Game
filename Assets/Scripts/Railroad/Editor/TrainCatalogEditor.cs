using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TrainCatalog))]
public class TrainCatalogEditor : Editor
{
    private SerializedProperty _folderProp;

    private void OnEnable()
    {
        _folderProp = serializedObject.FindProperty("definitionsFolder");
        ((TrainCatalog)target).RefreshFromFolder();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space(4);

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
                if (chosen.StartsWith(Application.dataPath))
                    chosen = "Assets" + chosen.Substring(Application.dataPath.Length).Replace('\\', '/');
                _folderProp.stringValue = chosen;
                serializedObject.ApplyModifiedProperties();
                ((TrainCatalog)target).RefreshFromFolder();
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        if (GUILayout.Button("Refresh from Folder", GUILayout.Height(24)))
            ((TrainCatalog)target).RefreshFromFolder();

        EditorGUILayout.Space(4);

        var catalog = (TrainCatalog)target;
        EditorGUILayout.LabelField($"Definitions found: {catalog.Items.Count}", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            foreach (var def in catalog.Items)
                EditorGUILayout.ObjectField(def != null ? def.DisplayName : "(null)", def,
                    typeof(TrainDefinition), false);
        }

        serializedObject.ApplyModifiedProperties();
    }
}

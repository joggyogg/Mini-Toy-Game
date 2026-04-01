using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// A ScriptableObject that auto-populates from every TrainDefinition asset
/// found inside a chosen project folder. Hit "Refresh from Folder" in the
/// inspector — no manual list management needed.
/// </summary>
[CreateAssetMenu(menuName = "Mini Toy Game/Railroad/Train Catalog", fileName = "TrainCatalog")]
public class TrainCatalog : ScriptableObject
{
    [Tooltip("Project-relative folder to scan, e.g. Assets/Data/Railroad/Definitions")]
    [SerializeField] private string definitionsFolder = "Assets/Data/Railroad/Definitions";

    [HideInInspector]
    [SerializeField] private List<TrainDefinition> items = new List<TrainDefinition>();

    public string DefinitionsFolder => definitionsFolder;

    /// <summary>All definitions in this catalog in scan order.</summary>
    public IReadOnlyList<TrainDefinition> Items => items;

#if UNITY_EDITOR
    /// <summary>Scans <see cref="definitionsFolder"/> and rebuilds the items list.</summary>
    public void RefreshFromFolder()
    {
        items.Clear();
        string[] guids = AssetDatabase.FindAssets("t:TrainDefinition", new[] { definitionsFolder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TrainDefinition def = AssetDatabase.LoadAssetAtPath<TrainDefinition>(path);
            if (def != null) items.Add(def);
        }
        EditorUtility.SetDirty(this);
    }
#endif
}

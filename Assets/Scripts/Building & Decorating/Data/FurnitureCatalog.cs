using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// A ScriptableObject that auto-populates from every FurnitureDefinition asset
/// found inside a chosen project folder. Hit "Refresh from Folder" in the
/// inspector (or it refreshes automatically when you select the asset in the
/// editor) — no manual list management needed.
/// </summary>
[CreateAssetMenu(menuName = "Mini Toy Game/Building/Furniture Catalog", fileName = "FurnitureCatalog")]
public class FurnitureCatalog : ScriptableObject
{
    [Tooltip("Project-relative folder to scan, e.g. Assets/Data/Building/Defintions")]
    [SerializeField] private string definitionsFolder = "Assets/Data/Building/Defintions";

    [HideInInspector]
    [SerializeField] private List<FurnitureDefinition> items = new List<FurnitureDefinition>();

    public string DefinitionsFolder => definitionsFolder;

    /// <summary>All definitions in this catalog in scan order.</summary>
    public IReadOnlyList<FurnitureDefinition> Items => items;

    /// <summary>Returns all definitions belonging to the given category.</summary>
    public IEnumerable<FurnitureDefinition> GetByCategory(string category)
    {
        foreach (FurnitureDefinition def in items)
            if (def != null && def.Category == category)
                yield return def;
    }

    /// <summary>Returns distinct category names in first-appearance order.</summary>
    public IEnumerable<string> GetCategories()
    {
        var seen = new HashSet<string>();
        foreach (FurnitureDefinition def in items)
            if (def != null && seen.Add(def.Category))
                yield return def.Category;
    }

#if UNITY_EDITOR
    /// <summary>Scans <see cref="definitionsFolder"/> and rebuilds the items list.</summary>
    public void RefreshFromFolder()
    {
        items.Clear();
        string[] guids = AssetDatabase.FindAssets("t:FurnitureDefinition", new[] { definitionsFolder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            FurnitureDefinition def = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(path);
            if (def != null) items.Add(def);
        }
        EditorUtility.SetDirty(this);
    }
#endif
}

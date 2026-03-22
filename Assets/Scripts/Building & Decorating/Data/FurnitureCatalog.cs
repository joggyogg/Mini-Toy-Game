using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plain-English purpose:
/// A ScriptableObject that acts as a named list of FurnitureDefinitions.
/// Assign it to DecorateCatalogUI to populate the in-game furniture menu.
///
/// Create one via Assets > Create > Mini Toy Game > Building > Furniture Catalog.
/// </summary>
[CreateAssetMenu(menuName = "Mini Toy Game/Building/Furniture Catalog", fileName = "FurnitureCatalog")]
public class FurnitureCatalog : ScriptableObject
{
    [SerializeField] private List<FurnitureDefinition> items = new List<FurnitureDefinition>();

    /// <summary>All definitions in this catalog in the order they were added.</summary>
    public IReadOnlyList<FurnitureDefinition> Items => items;

    /// <summary>Returns all definitions belonging to the given category.</summary>
    public IEnumerable<FurnitureDefinition> GetByCategory(string category)
    {
        foreach (FurnitureDefinition def in items)
        {
            if (def != null && def.Category == category)
            {
                yield return def;
            }
        }
    }

    /// <summary>Returns the distinct category names present in this catalog, in first-appearance order.</summary>
    public IEnumerable<string> GetCategories()
    {
        var seen = new HashSet<string>();
        foreach (FurnitureDefinition def in items)
        {
            if (def != null && seen.Add(def.Category))
            {
                yield return def.Category;
            }
        }
    }
}

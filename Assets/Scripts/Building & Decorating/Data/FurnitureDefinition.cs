using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// One colour/style variant of a piece of furniture.
/// Add multiple of these to FurnitureDefinition.variants to give a piece swappable prefabs
/// (e.g. red, blue, natural wood). Leave the list empty to keep the old single-prefab behaviour.
/// </summary>
[System.Serializable]
public sealed class FurnitureVariant
{
    [Tooltip("Short label shown on the variant sub-button tooltip (e.g. 'Red', 'Blue', 'Oak').")]
    public string variantName;

    [Tooltip("Icon shown on the variant sub-button. Falls back to the parent definition icon when blank.")]
    public Sprite icon;

    [Tooltip("Prefab for this variant. Must have a PlaceableGridAuthoring component.")]
    public PlaceableGridAuthoring prefab;
}

/// <summary>
/// Plain-English purpose:
/// This asset describes one furniture option that can appear in the decorate menu.
///
/// The menu should list FurnitureDefinition assets instead of directly scanning prefabs in the project or scene.
/// That keeps UI data, display information, and placement prefab references in one place.
/// </summary>
[CreateAssetMenu(menuName = "Mini Toy Game/Building/Furniture Definition", fileName = "FurnitureDefinition")]
public class FurnitureDefinition : ScriptableObject
{
    [Header("Display")]
    [SerializeField] private string displayName = "New Furniture";
    [SerializeField] private Sprite icon;
    [SerializeField] private string category = "General";

    [Header("Placement")]
    [SerializeField] private PlaceableGridAuthoring placeableAuthoringPrefab;
    [FormerlySerializedAs("placeablePrefab")]
    [SerializeField, HideInInspector] private GameObject legacyPlaceablePrefab;
    [SerializeField] private bool allowRotation = true;
    [SerializeField] private int defaultRotationQuarterTurns;

    [Header("Colour / Style Variants")]
    [Tooltip("Optional prefab variants (e.g. colour swaps). Requires 2 or more entries to show sub-buttons. " +
             "Leave empty to use the single prefab above.")]
    [SerializeField] private List<FurnitureVariant> variants = new List<FurnitureVariant>();

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public Sprite Icon => icon;
    public string Category => string.IsNullOrWhiteSpace(category) ? "General" : category;
    public GameObject PlaceablePrefab => placeableAuthoringPrefab != null ? placeableAuthoringPrefab.gameObject : legacyPlaceablePrefab;
    public bool AllowRotation => allowRotation;
    public int DefaultRotationQuarterTurns => Mathf.Abs(defaultRotationQuarterTurns) % 4;

    /// <summary>True when this definition has 2 or more variants and should show a variant sub-panel in the UI.</summary>
    public bool HasVariants => variants != null && variants.Count > 1;

    /// <summary>All defined variants. May be empty for single-prefab definitions.</summary>
    public IReadOnlyList<FurnitureVariant> Variants => variants;

    private void OnValidate()
    {
        // Older assets used a plain GameObject reference. If that data exists, migrate it forward so the inspector
        // now accepts the prefab's PlaceableGridAuthoring component directly.
        if (placeableAuthoringPrefab == null && legacyPlaceablePrefab != null)
        {
            placeableAuthoringPrefab = legacyPlaceablePrefab.GetComponent<PlaceableGridAuthoring>();
        }
    }

    /// <summary>
    /// Convenience helper for the placement systems. Furniture is only placeable when it points at a prefab that has
    /// the authoring component describing its male grid and optional female layers.
    /// </summary>
    public bool TryGetPlaceableAuthoring(out PlaceableGridAuthoring authoring)
    {
        authoring = placeableAuthoringPrefab;
        if (authoring != null)
        {
            return true;
        }

        if (legacyPlaceablePrefab == null)
        {
            return false;
        }

        authoring = legacyPlaceablePrefab.GetComponent<PlaceableGridAuthoring>();
        return authoring != null;
    }

    /// <summary>
    /// Returns the PlaceableGridAuthoring for a specific variant index.
    /// Falls back to the base prefab when variantIndex is out of range or the variant has no prefab.
    /// </summary>
    public bool TryGetPlaceableAuthoring(int variantIndex, out PlaceableGridAuthoring authoring)
    {
        if (variants != null && variantIndex >= 0 && variantIndex < variants.Count
            && variants[variantIndex].prefab != null)
        {
            authoring = variants[variantIndex].prefab;
            return true;
        }
        return TryGetPlaceableAuthoring(out authoring);
    }
}

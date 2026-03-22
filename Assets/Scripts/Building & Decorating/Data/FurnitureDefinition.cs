using UnityEngine;
using UnityEngine.Serialization;

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

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public Sprite Icon => icon;
    public string Category => string.IsNullOrWhiteSpace(category) ? "General" : category;
    public GameObject PlaceablePrefab => placeableAuthoringPrefab != null ? placeableAuthoringPrefab.gameObject : legacyPlaceablePrefab;
    public bool AllowRotation => allowRotation;
    public int DefaultRotationQuarterTurns => Mathf.Abs(defaultRotationQuarterTurns) % 4;

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
}

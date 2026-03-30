using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plain-English purpose:
/// The in-game furniture catalog panel. Shows one button per FurnitureDefinition in the assigned catalog.
/// When the player clicks an item the OnDefinitionSelected event fires and FurnitureSpawner
/// calls SpawnFurniture to place it near the player.
///
/// Setup: Add this MonoBehaviour to the root of a UI canvas panel that contains a ScrollRect.
/// Assign the catalog, a FurnitureCatalogButton prefab, and the scroll content transform in the inspector.
/// </summary>
public class DecorateCatalogUI : MonoBehaviour
{
    [SerializeField] private FurnitureCatalog catalog;
    [SerializeField] private FurnitureCatalogButton buttonPrefab;
    [SerializeField] private Transform buttonContainer;
    [SerializeField] private ScrollRect scrollRect;

    /// <summary>Fired when the player selects a furniture item.
    /// variantIndex is -1 for single-prefab definitions, or the chosen index into FurnitureDefinition.Variants.</summary>
    public event Action<FurnitureDefinition, int> OnDefinitionSelected;

    private readonly List<FurnitureCatalogButton> spawnedButtons = new List<FurnitureCatalogButton>();

    private void OnEnable()
    {
        Populate(catalog);
    }

    private void OnDisable()
    {
        ClearButtons();
    }

    /// <summary>
    /// Replaces the current catalog and rebuilds the button list.
    /// Safe to call at runtime when the panel is already visible.
    /// </summary>
    public void SetCatalog(FurnitureCatalog newCatalog)
    {
        catalog = newCatalog;
        if (gameObject.activeInHierarchy)
        {
            Populate(catalog);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private void Populate(FurnitureCatalog source)
    {
        ClearButtons();

        if (source == null || buttonPrefab == null || buttonContainer == null) return;

        foreach (FurnitureDefinition def in source.Items)
        {
            if (def == null) continue;

            FurnitureCatalogButton btn = Instantiate(buttonPrefab, buttonContainer);
            btn.Initialise(def, HandleVariantSelected);
            spawnedButtons.Add(btn);
        }

        // Reset scroll to top when the list is rebuilt.
        if (scrollRect != null)
        {
            scrollRect.normalizedPosition = new Vector2(0f, 1f);
        }
    }

    private void ClearButtons()
    {
        foreach (FurnitureCatalogButton btn in spawnedButtons)
        {
            if (btn != null) Destroy(btn.gameObject);
        }
        spawnedButtons.Clear();
    }

    private void HandleVariantSelected(FurnitureDefinition def, int variantIndex)
    {
        OnDefinitionSelected?.Invoke(def, variantIndex);
    }
}

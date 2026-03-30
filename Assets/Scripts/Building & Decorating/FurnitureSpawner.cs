using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plain-English purpose:
/// The single entry point for entering and leaving decorate mode.
/// It wires together:
///   - PlayerMotor — grid-locked tile movement
///   - TerrainGridAuthoring — the floor grid that anchors everything
///   - DecorateCatalogUI  — the furniture picker panel
///
/// Attach this to a persistent manager GameObject. Assign all references in the inspector.
/// Call EnterDecorateMode() / ExitDecorateMode() from game logic (e.g. a button or trigger).
/// </summary>
public class FurnitureSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMotor playerMotor;
    [SerializeField] private TerrainGridAuthoring terrain;
    [SerializeField] private FurnitureCatalog catalog;

    [Header("UI")]
    [SerializeField] private DecorateCatalogUI catalogUI;

    // State
    private bool inDecorateMode;
    private Camera activeCamera;
    private readonly List<PlacedFurnitureRecord> placedFurniture = new List<PlacedFurnitureRecord>();

    public bool IsInDecorateMode => inDecorateMode;

    /// <summary>All furniture placed during this decorate session, in placement order.</summary>
    public IReadOnlyList<PlacedFurnitureRecord> PlacedFurniture => placedFurniture;

    // ── Public API ────────────────────────────────────────────────────────────────

    public void EnterDecorateMode(Camera camera = null)
    {
        if (inDecorateMode) return;
        inDecorateMode = true;
        activeCamera = camera;

        if (playerMotor == null) Debug.LogError("[FurnitureSpawner] PlayerMotor is not assigned!", this);
        if (terrain == null) Debug.LogError("[FurnitureSpawner] TerrainGridAuthoring is not assigned!", this);
        if (catalogUI == null) Debug.LogWarning("[FurnitureSpawner] CatalogUI is not assigned \u2014 furniture menu won't show.", this);

        if (catalogUI != null)
        {
            if (catalog != null) catalogUI.SetCatalog(catalog);
            catalogUI.gameObject.SetActive(true);
            catalogUI.OnDefinitionSelected += HandleVariantSelected;
        }

        Debug.Log("[FurnitureSpawner] EnterDecorateMode complete.");
    }

    public void ExitDecorateMode()
    {
        if (!inDecorateMode) return;
        inDecorateMode = false;

        if (playerMotor != null) playerMotor.ExitDecorateMode();

        if (catalogUI != null)
        {
            catalogUI.OnDefinitionSelected -= HandleVariantSelected;
            catalogUI.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Spawns a furniture piece near the player and registers it in the placement list.
    /// variantIndex selects a colour/style variant (-1 or out of range = use the base prefab).
    /// Does nothing if no valid placement position is found.
    /// </summary>
    public void SpawnFurniture(FurnitureDefinition definition, int variantIndex = -1)
    {
        if (definition == null) return;
        if (!definition.TryGetPlaceableAuthoring(variantIndex, out PlaceableGridAuthoring authoringPrefab)) return;
        if (terrain == null) return;

        Camera cam = activeCamera != null ? activeCamera : Camera.main;

        // Raycast from the centre of the camera's view to find a terrain hit point.
        Vector3 searchOrigin;
        if (cam != null)
        {
            Ray centerRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(centerRay, out RaycastHit hit, 1000f))
                searchOrigin = hit.point;
            else
                searchOrigin = cam.transform.position + cam.transform.forward * 20f;
        }
        else
        {
            searchOrigin = playerMotor != null ? playerMotor.transform.position : transform.position;
        }

        PlacementCandidate candidate = default;
        bool placed = PlacementSolver.TryFindSpawnPosition(
            terrain, searchOrigin, authoringPrefab, placedFurniture, out candidate, cam);

        if (!placed)
        {
            Debug.LogWarning($"[FurnitureSpawner] No free space found to place '{definition.DisplayName}'.");
            return;
        }

        GameObject instance = Instantiate(authoringPrefab.gameObject, candidate.WorldPosition, candidate.Rotation);
        PlaceableGridAuthoring instanceAuthoring = instance.GetComponent<PlaceableGridAuthoring>();

        var record = new PlacedFurnitureRecord(instanceAuthoring, definition, candidate);
        placedFurniture.Add(record);
    }

    /// <summary>
    /// Removes a placed furniture record and destroys its scene instance.
    /// </summary>
    public void RemoveFurniture(PlacedFurnitureRecord record)
    {
        if (record == null) return;
        placedFurniture.Remove(record);
        if (record.Instance != null) Destroy(record.Instance.gameObject);
    }

    // ── Private ───────────────────────────────────────────────────────────────────

    private void OnDestroy()
    {
        if (catalogUI != null) catalogUI.OnDefinitionSelected -= HandleVariantSelected;
    }

    private void HandleVariantSelected(FurnitureDefinition definition, int variantIndex)
    {
        SpawnFurniture(definition, variantIndex);
    }
}

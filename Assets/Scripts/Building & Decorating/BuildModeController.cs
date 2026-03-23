using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plain-English purpose:
/// The single entry point for entering and leaving decorate mode.
/// It wires together:
///   - PlayerMotor — grid-locked tile movement
///   - TerrainGridAuthoring — the floor grid that anchors everything
///   - DecorateCatalogUI  — the furniture picker panel
///   - DecorateMinimapUI  — the overhead grid + drag-to-move panel
///
/// Attach this to a persistent manager GameObject. Assign all references in the inspector.
/// Call EnterDecorateMode() / ExitDecorateMode() from game logic (e.g. a button or trigger).
/// </summary>
public class BuildModeController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMotor playerMotor;
    [SerializeField] private TerrainGridAuthoring terrain;
    [SerializeField] private FurnitureCatalog catalog;

    [Header("UI")]
    [SerializeField] private DecorateCatalogUI catalogUI;
    [SerializeField] private DecorateMinimapUI minimapUI;

    // State
    private bool inDecorateMode;
    private readonly List<PlacedFurnitureRecord> placedFurniture = new List<PlacedFurnitureRecord>();

    public bool IsInDecorateMode => inDecorateMode;

    /// <summary>All furniture placed during this decorate session, in placement order.</summary>
    public IReadOnlyList<PlacedFurnitureRecord> PlacedFurniture => placedFurniture;

    // ── Public API ────────────────────────────────────────────────────────────────

    public void EnterDecorateMode()
    {
        if (inDecorateMode) return;
        inDecorateMode = true;

        if (playerMotor == null) Debug.LogError("[BuildModeController] PlayerMotor is not assigned!", this);
        if (terrain == null) Debug.LogError("[BuildModeController] TerrainGridAuthoring is not assigned!", this);
        if (catalogUI == null) Debug.LogWarning("[BuildModeController] CatalogUI is not assigned — furniture menu won't show.", this);
        if (minimapUI == null) Debug.LogWarning("[BuildModeController] MinimapUI is not assigned — minimap won't show.", this);

        // Keep free movement active while decorating (experimental).
        // if (playerMotor != null && terrain != null)
        // {
        //     playerMotor.EnterDecorateMode(terrain);
        // }

        if (catalogUI != null)
        {
            if (catalog != null) catalogUI.SetCatalog(catalog);
            catalogUI.gameObject.SetActive(true);
            catalogUI.OnDefinitionSelected += HandleVariantSelected;
        }

        if (minimapUI != null)
        {
            minimapUI.gameObject.SetActive(true);
            minimapUI.Initialise(terrain, placedFurniture, playerMotor);
        }

        Debug.Log("[BuildModeController] EnterDecorateMode complete.");
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

        if (minimapUI != null) minimapUI.gameObject.SetActive(false);
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

        Vector3 playerPos = playerMotor != null ? playerMotor.transform.position : transform.position;

        // Try the currently viewed surface layer first, then fall back to the terrain floor.
        bool placed = false;
        PlacementCandidate candidate = default;

        if (minimapUI != null &&
            minimapUI.TryGetCurrentSurfaceInfo(out IReadOnlyDictionary<Vector2Int, bool> surfaceCells, out float surfaceY))
        {
            placed = PlacementSolver.TryFindSpawnOnSurface(
                terrain, playerPos, authoringPrefab, placedFurniture, surfaceCells, surfaceY, out candidate);
        }

        if (!placed)
        {
            placed = PlacementSolver.TryFindSpawnPosition(
                terrain, playerPos, authoringPrefab, placedFurniture, out candidate);
        }

        if (!placed)
        {
            Debug.LogWarning($"[BuildModeController] No free space found to place '{definition.DisplayName}'.");
            return;
        }

        GameObject instance = Instantiate(authoringPrefab.gameObject, candidate.WorldPosition, candidate.Rotation);
        PlaceableGridAuthoring instanceAuthoring = instance.GetComponent<PlaceableGridAuthoring>();

        var record = new PlacedFurnitureRecord(instanceAuthoring, definition, candidate);
        placedFurniture.Add(record);

        if (minimapUI != null) minimapUI.OnFurniturePlaced(record);
    }

    /// <summary>
    /// Removes a placed furniture record and destroys its scene instance.
    /// </summary>
    public void RemoveFurniture(PlacedFurnitureRecord record)
    {
        if (record == null) return;
        placedFurniture.Remove(record);
        if (record.Instance != null) Destroy(record.Instance.gameObject);
        if (minimapUI != null) minimapUI.OnFurnitureRemoved();
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

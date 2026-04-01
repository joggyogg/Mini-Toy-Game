using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data manager for placed furniture. Spawns pieces, tracks placement records,
/// and handles removal. Lifecycle wiring (catalog UI, panel enable/disable)
/// is handled by HumanFurniturePlacer via OnEnable/OnDisable.
/// </summary>
public class FurnitureSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMotor playerMotor;
    [SerializeField] private TerrainGridAuthoring terrain;

    // State
    private readonly List<PlacedFurnitureRecord> placedFurniture = new List<PlacedFurnitureRecord>();

    /// <summary>All furniture placed during this decorate session, in placement order.</summary>
    public IReadOnlyList<PlacedFurnitureRecord> PlacedFurniture => placedFurniture;

    // ── Public API ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Spawns a furniture piece near the camera center and registers it in the placement list.
    /// variantIndex selects a colour/style variant (-1 or out of range = use the base prefab).
    /// Does nothing if no valid placement position is found.
    /// </summary>
    public void SpawnFurniture(FurnitureDefinition definition, int variantIndex = -1, Camera cam = null)
    {
        if (definition == null) return;
        if (!definition.TryGetPlaceableAuthoring(variantIndex, out PlaceableGridAuthoring authoringPrefab)) return;
        if (terrain == null) return;

        if (cam == null) cam = Camera.main;

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
}

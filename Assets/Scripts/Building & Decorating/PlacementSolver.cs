using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plain-English purpose:
/// Static utility that finds a valid spawn position for a piece of furniture on a TerrainGridAuthoring grid.
///
/// The search works in two passes:
///   1. An occupancy set is built from all PlaceableGridAuthoring instances currently in the scene,
///      projected onto the terrain subtile coordinate space.
///   2. A BFS shell spiral starting from the player's nearest full tile finds the first origin cell
///      where every enabled male-grid cell of the furniture lands on an enabled, non-occupied terrain cell.
///
/// Only the default rotation (0°) is attempted. The player can rotate after placement using the minimap.
/// </summary>
public static class PlacementSolver
{
    /// <summary>
    /// Tries to find a non-overlapping terrain cell origin for <paramref name="furniturePrefab"/>'s male grid,
    /// starting from the full tile nearest to <paramref name="playerWorldPos"/>.
    /// </summary>
    /// <param name="terrain">The terrain grid that is the placement surface.</param>
    /// <param name="playerWorldPos">World position used as the BFS search origin.</param>
    /// <param name="furniturePrefab">Prefab whose male grid defines the required footprint.</param>
    /// <param name="existingPlacements">
    /// Optional list of already-placed objects whose terrain cells should be treated as occupied.
    /// Pass null to skip occupancy checks (useful before any furniture exists).
    /// </param>
    /// <param name="result">The candidate placement when the method returns true.</param>
    /// <returns>True when a valid position was found.</returns>
    public static bool TryFindSpawnPosition(
        TerrainGridAuthoring terrain,
        Vector3 playerWorldPos,
        PlaceableGridAuthoring furniturePrefab,
        IReadOnlyList<PlacedFurnitureRecord> existingPlacements,
        out PlacementCandidate result)
    {
        result = default;

        if (terrain == null || furniturePrefab == null) return false;

        Vector2Int maleSize = furniturePrefab.MaleGridSizeInCells;
        if (maleSize.x <= 0 || maleSize.y <= 0) return false;

        // Build the local enabled-cell shape of the male grid (relative positions of enabled cells).
        var maleShape = BuildMaleShape(furniturePrefab, maleSize);
        if (maleShape.Count == 0) return false;

        // Build a set of currently-occupied terrain cells.
        HashSet<Vector2Int> occupied = BuildOccupancySet(terrain, existingPlacements);

        // Find the starting full tile.
        if (!terrain.TryGetNearestWalkableFullTile(playerWorldPos, out Vector2Int startFull)) return false;

        int s = terrain.SubtilesPerFullTile;
        Vector2Int startCell = new Vector2Int(startFull.x * s, startFull.y * s);
        Vector2Int cellSize = terrain.GridSizeInCells;
        int targetLevel = terrain.GetTileLevel(startFull.x, startFull.y);

        // BFS spiral.
        int maxRadius = cellSize.x + cellSize.y;
        for (int radius = 0; radius <= maxRadius; radius++)
        {
            for (int dz = -radius; dz <= radius; dz++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    // Visit only the perimeter of each shell (skip interior — already checked).
                    if (radius > 0 && Mathf.Abs(dx) != radius && Mathf.Abs(dz) != radius) continue;

                    int originX = startCell.x + dx;
                    int originZ = startCell.y + dz;

                    if (FitsAtOrigin(terrain, maleShape, originX, originZ, occupied, targetLevel))
                    {
                        if (!terrain.TryGetCellCenterWorld(originX, originZ, out Vector3 cell00Center)) continue;

                        // Place the pivot so that:
                        //   XZ: male cell (0,0) center aligns with terrain cell (originX, originZ) center.
                        //   Y:  the collider floor (maleGridFloorLocalY below the pivot) sits on the terrain surface.
                        Vector2 maleOffset = furniturePrefab.MaleGridOriginLocalOffset;
                        float tileSize = furniturePrefab.FemaleTileSize;
                        Vector3 spawnPos = cell00Center
                            - Vector3.right   * (maleOffset.x + 0.5f * tileSize)
                            - Vector3.forward * (maleOffset.y + 0.5f * tileSize)
                            + Vector3.up * (-furniturePrefab.MaleGridFloorLocalY);

                        result = new PlacementCandidate(spawnPos, Quaternion.identity, new Vector2Int(originX, originZ));
                        return true;
                    }
                }
            }
        }

        return false;
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private static List<Vector2Int> BuildMaleShape(PlaceableGridAuthoring authoring, Vector2Int maleSize)
    {
        var shape = new List<Vector2Int>();
        for (int z = 0; z < maleSize.y; z++)
        {
            for (int x = 0; x < maleSize.x; x++)
            {
                if (authoring.GetMaleCell(x, z))
                {
                    shape.Add(new Vector2Int(x, z));
                }
            }
        }
        return shape;
    }

    private static bool FitsAtOrigin(
        TerrainGridAuthoring terrain,
        List<Vector2Int> maleShape,
        int originX, int originZ,
        HashSet<Vector2Int> occupied,
        int targetLevel)
    {
        int s = terrain.SubtilesPerFullTile;
        foreach (Vector2Int cell in maleShape)
        {
            int tx = originX + cell.x;
            int tz = originZ + cell.y;

            if (!terrain.GetCell(tx, tz)) return false;
            if (occupied.Contains(new Vector2Int(tx, tz))) return false;

            // Keep floor placement on the same terrain level as the player's start tile.
            int ftx = tx / s;  int ftz = tz / s;
            if (terrain.GetTileLevel(ftx, ftz) != targetLevel) return false;
        }
        return true;
    }

    private static Vector3 ComputeMaleFootprintCenter(TerrainGridAuthoring terrain, List<Vector2Int> maleShape, int originX, int originZ)
    {
        // Average world position of all male cells.
        Vector3 sum = Vector3.zero;
        foreach (Vector2Int cell in maleShape)
        {
            if (terrain.TryGetCellCenterWorld(originX + cell.x, originZ + cell.y, out Vector3 center))
            {
                sum += center;
            }
        }
        return sum / maleShape.Count;
    }

    private static HashSet<Vector2Int> BuildOccupancySet(
        TerrainGridAuthoring terrain,
        IReadOnlyList<PlacedFurnitureRecord> existingPlacements)
    {
        var occupied = new HashSet<Vector2Int>();
        if (existingPlacements == null) return occupied;

        foreach (PlacedFurnitureRecord record in existingPlacements)
        {
            if (record.Instance == null) continue;
            AddFurnitureOccupancy(terrain, record.Instance, occupied);
        }

        return occupied;
    }

    private static void AddFurnitureOccupancy(
        TerrainGridAuthoring terrain,
        PlaceableGridAuthoring furniture,
        HashSet<Vector2Int> occupied)
    {
        Vector2Int maleSize = furniture.MaleGridSizeInCells;
        float tileSize = furniture.FemaleTileSize;
        Transform t = furniture.transform;

        Vector2 gridOffset = furniture.MaleGridOriginLocalOffset;

        for (int z = 0; z < maleSize.y; z++)
        {
            for (int x = 0; x < maleSize.x; x++)
            {
                if (!furniture.GetMaleCell(x, z)) continue;

                // Compute the world-space centre of this male cell, accounting for the pivot-to-grid-origin offset.
                float lx = gridOffset.x + (x + 0.5f) * tileSize;
                float lz = gridOffset.y + (z + 0.5f) * tileSize;
                Vector3 worldCellCenter = t.position + t.right * lx + t.forward * lz;

                if (terrain.TryWorldToCell(worldCellCenter, out Vector2Int terrainCell))
                {
                    occupied.Add(terrainCell);
                }
            }
        }
    }

    // ── Surface placement ─────────────────────────────────────────────────────────

    /// <summary>
    /// Tries to find a non-overlapping spawn position on a furniture female-grid surface.
    /// Uses the same BFS shell spiral as the terrain solver, but constrains legal origins
    /// to cells where the entire male footprint falls on enabled, unoccupied surface cells.
    /// </summary>
    public static bool TryFindSpawnOnSurface(
        TerrainGridAuthoring terrain,
        Vector3 playerWorldPos,
        PlaceableGridAuthoring furniturePrefab,
        IReadOnlyList<PlacedFurnitureRecord> existingPlacements,
        IReadOnlyDictionary<Vector2Int, bool> surfaceEnabledMap,
        float worldSurfaceY,
        out PlacementCandidate result)
    {
        result = default;
        if (terrain == null || furniturePrefab == null) return false;
        if (surfaceEnabledMap == null || surfaceEnabledMap.Count == 0) return false;

        Vector2Int maleSize = furniturePrefab.MaleGridSizeInCells;
        if (maleSize.x <= 0 || maleSize.y <= 0) return false;

        var maleShape = BuildMaleShape(furniturePrefab, maleSize);
        if (maleShape.Count == 0) return false;

        // Occupied cells: only pieces already sitting at this surface height.
        HashSet<Vector2Int> occupied = BuildSurfaceOccupancySet(terrain, existingPlacements, worldSurfaceY);

        // BFS starting cell: project player world pos straight to a terrain subtile cell.
        if (!terrain.TryWorldToCell(playerWorldPos, out Vector2Int startCell))
        {
            if (!terrain.TryGetNearestWalkableFullTile(playerWorldPos, out Vector2Int startFull)) return false;
            int s2 = terrain.SubtilesPerFullTile;
            startCell = new Vector2Int(startFull.x * s2, startFull.y * s2);
        }

        // Use surface bounding box to cap the BFS radius.
        int minX = int.MaxValue, maxX = int.MinValue, minZ = int.MaxValue, maxZ = int.MinValue;
        foreach (Vector2Int k in surfaceEnabledMap.Keys)
        {
            if (k.x < minX) minX = k.x; if (k.x > maxX) maxX = k.x;
            if (k.y < minZ) minZ = k.y; if (k.y > maxZ) maxZ = k.y;
        }
        int maxRadius = (maxX - minX) + (maxZ - minZ) + maleSize.x + maleSize.y + 4;

        for (int radius = 0; radius <= maxRadius; radius++)
        {
            for (int dz = -radius; dz <= radius; dz++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (radius > 0 && Mathf.Abs(dx) != radius && Mathf.Abs(dz) != radius) continue;

                    int originX = startCell.x + dx;
                    int originZ = startCell.y + dz;

                    if (!FitsOnSurface(maleShape, originX, originZ, surfaceEnabledMap, occupied)) continue;
                    if (!terrain.TryGetCellCenterWorld(originX, originZ, out Vector3 cell00Center)) continue;

                    Vector2 maleOffset = furniturePrefab.MaleGridOriginLocalOffset;
                    float tileSize = furniturePrefab.FemaleTileSize;
                    Vector3 spawnPos = cell00Center
                        - Vector3.right   * (maleOffset.x + 0.5f * tileSize)
                        - Vector3.forward * (maleOffset.y + 0.5f * tileSize);
                    spawnPos.y = worldSurfaceY - furniturePrefab.MaleGridFloorLocalY;

                    result = new PlacementCandidate(spawnPos, Quaternion.identity, new Vector2Int(originX, originZ));
                    return true;
                }
            }
        }

        return false;
    }

    private static bool FitsOnSurface(
        List<Vector2Int> maleShape,
        int originX, int originZ,
        IReadOnlyDictionary<Vector2Int, bool> surfaceMap,
        HashSet<Vector2Int> occupied)
    {
        foreach (Vector2Int cell in maleShape)
        {
            Vector2Int tc = new Vector2Int(originX + cell.x, originZ + cell.y);
            if (!surfaceMap.TryGetValue(tc, out bool enabled) || !enabled) return false;
            if (occupied.Contains(tc)) return false;
        }
        return true;
    }

    private static HashSet<Vector2Int> BuildSurfaceOccupancySet(
        TerrainGridAuthoring terrain,
        IReadOnlyList<PlacedFurnitureRecord> placements,
        float worldSurfaceY)
    {
        var occupied = new HashSet<Vector2Int>();
        if (placements == null) return occupied;

        const float yTolerance = 0.1f;
        foreach (PlacedFurnitureRecord record in placements)
        {
            if (record.Instance == null) continue;
            PlaceableGridAuthoring auth = record.Instance;
            float baseY = auth.transform.position.y + auth.MaleGridFloorLocalY;
            if (Mathf.Abs(baseY - worldSurfaceY) > yTolerance) continue;
            AddFurnitureOccupancy(terrain, auth, occupied);
        }
        return occupied;
    }
}

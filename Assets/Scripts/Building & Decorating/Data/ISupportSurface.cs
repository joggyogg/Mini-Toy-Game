using UnityEngine;

/// <summary>
/// Common contract for any surface that can receive furniture placement.
/// Implemented by TerrainGridAuthoring (world floors) and furniture top surfaces.
/// </summary>
public interface ISupportSurface
{
    float FemaleTileSize { get; }
    Vector2Int GridSizeInCells { get; }
    Transform SurfaceTransform { get; }

    bool GetCell(int xIndex, int zIndex);
    bool TryWorldToCell(Vector3 worldPosition, out Vector2Int cell);
    bool TryGetCellCenterWorld(int xIndex, int zIndex, out Vector3 worldCenter);
    bool TryGetOverlappingCellRange(Bounds worldBounds, out Vector2Int minCell, out Vector2Int maxCell);
}

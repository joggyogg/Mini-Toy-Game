using UnityEngine;

/// <summary>
/// Grades terrain around newly placed rail knots to form a raised ballast bed.
/// - Bed band (3 tiles wide): raised 1 level (half a tile / 0.5 world units) above the terrain.
/// - Blend band (1 tile each side): smoothly slopes from the raised bed back down to natural terrain.
///
/// Cross-section profile (looking along the track):
///   natural ──╲__________╱── natural
///              ↑ 1 blend  ↑ 1 blend
///          3 tiles raised bed
///
/// Uses the same TerrainGridAuthoring corner-height system as TerrainEditController.
/// </summary>
public class RailTerrainGrader : MonoBehaviour
{
    [SerializeField] private TerrainGridAuthoring terrainGrid;

    [Tooltip("Half-width of the flat raised bed in tiles (total bed width = 2 * bedHalfWidth + 1, centered on track).")]
    [SerializeField] private int bedHalfWidth = 1;

    [Tooltip("Width in tiles of the blend slope on each side of the bed.")]
    [SerializeField] private int blendWidth = 1;

    [Tooltip("How many levels the bed is raised above the surrounding terrain (1 level = 0.5 world units).")]
    [SerializeField] private int bedRaiseLevels = 1;

    /// <summary>The bed raise amount in world units.</summary>
    public float BedRaiseWorldHeight => bedRaiseLevels * TerrainGridAuthoring.LevelHeight;

    /// <summary>
    /// Snaps a world position's X/Z to the center of the nearest full tile.
    /// Y is unchanged. Returns the original position if outside the grid.
    /// </summary>
    public Vector3 SnapToTileCenter(Vector3 worldPos)
    {
        if (terrainGrid == null) return worldPos;
        if (!terrainGrid.TryWorldToFullTile(worldPos, out Vector2Int tile)) return worldPos;
        if (!terrainGrid.TryGetFullTileCenterWorld(tile.x, tile.y, out Vector3 center)) return worldPos;
        center.y = worldPos.y;
        return center;
    }

    private Terrain[] childTerrains;

    private void Awake()
    {
        childTerrains = terrainGrid != null
            ? terrainGrid.GetComponentsInChildren<Terrain>()
            : System.Array.Empty<Terrain>();
    }

    /// <summary>
    /// Returns the world-space Y the spline should sit at for a given terrain hit point.
    /// This is the terrain's current height at that point plus the bed raise.
    /// </summary>
    public float GetBedWorldHeight(Vector3 worldPos)
    {
        int baseLevel = Mathf.Max(0, Mathf.RoundToInt(worldPos.y / TerrainGridAuthoring.LevelHeight));
        return (baseLevel + bedRaiseLevels) * TerrainGridAuthoring.LevelHeight;
    }

    /// <summary>
    /// Grades terrain along a line segment between two world points.
    /// Samples at roughly 1 point per tile along the segment.
    /// </summary>
    public void GradeAlongSegment(Vector3 from, Vector3 to)
    {
        float dist = Vector3.Distance(from, to);
        int sampleCount = Mathf.Max(2, Mathf.CeilToInt(dist / 1f)); // ~1 sample per tile

        for (int i = 0; i <= sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            Vector3 point = Vector3.Lerp(from, to, t);
            GradeAroundPoint(point);
        }
    }

    /// <summary>
    /// Grades terrain around a world-space point (typically a spline knot position).
    /// The terrain under the point is the base; the bed is raised above it.
    /// </summary>
    public void GradeAroundPoint(Vector3 worldPos)
    {
        if (terrainGrid == null) return;

        if (!terrainGrid.TryWorldToFullTile(worldPos, out Vector2Int centerTile))
            return;

        // Base level = current terrain height at this point (in integer levels).
        int baseLevel = Mathf.Max(0, Mathf.RoundToInt(worldPos.y / TerrainGridAuthoring.LevelHeight));
        // Raised bed level = base + raise amount (default: +1 level = 0.5 world units).
        int bedLevel = baseLevel + bedRaiseLevels;

        // The bed is 3 tiles wide: center tile ± bedHalfWidth (default 1).
        // bedHalfWidth=1 → tiles at -1, 0, +1 from center → 3 tiles total.
        // In corner space, that's corners from (center - bedHalfWidth) to (center + bedHalfWidth + 1).
        float bedCornerRadius = bedHalfWidth + 0.5f;   // 1.5 tiles from center
        float totalRadius = bedCornerRadius + blendWidth; // 2.5 tiles from center

        Vector2Int fullSize = terrainGrid.FullTileGridSize;
        int totalRadiusCeil = Mathf.CeilToInt(totalRadius);

        int minCx = Mathf.Max(0, centerTile.x - totalRadiusCeil);
        int maxCx = Mathf.Min(fullSize.x, centerTile.x + totalRadiusCeil + 1);
        int minCz = Mathf.Max(0, centerTile.y - totalRadiusCeil);
        int maxCz = Mathf.Min(fullSize.y, centerTile.y + totalRadiusCeil + 1);

        // Snapshot original corner heights so blend can lerp to the original.
        int snapW = maxCx - minCx + 1;
        int snapH = maxCz - minCz + 1;
        int[] originalHeights = new int[snapW * snapH];
        for (int z = 0; z < snapH; z++)
            for (int x = 0; x < snapW; x++)
                originalHeights[z * snapW + x] = terrainGrid.GetCornerHeight(minCx + x, minCz + z);

        float centerCxF = centerTile.x + 0.5f;
        float centerCzF = centerTile.y + 0.5f;

        for (int cz = minCz; cz <= maxCz; cz++)
        {
            for (int cx = minCx; cx <= maxCx; cx++)
            {
                float dx = cx - centerCxF;
                float dz = cz - centerCzF;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);

                if (dist <= bedCornerRadius)
                {
                    // Inside the raised bed: set to bedLevel.
                    terrainGrid.SetCornerHeight(cx, cz, bedLevel);
                }
                else if (dist <= totalRadius)
                {
                    // Blend zone: slope from bedLevel down to original terrain height.
                    float blendT = (dist - bedCornerRadius) / blendWidth;
                    blendT = blendT * blendT * (3f - 2f * blendT); // smoothstep

                    int snapIdx = (cz - minCz) * snapW + (cx - minCx);
                    int original = originalHeights[snapIdx];
                    int blended = Mathf.RoundToInt(Mathf.Lerp(bedLevel, original, blendT));
                    terrainGrid.SetCornerHeight(cx, cz, Mathf.Max(0, blended));
                }
            }
        }

        // Sync enabled cells and bake to Unity Terrain.
        RectInt dirtyTiles = new RectInt(
            minCx - 1, minCz - 1,
            (maxCx - minCx) + 3, (maxCz - minCz) + 3
        );
        terrainGrid.SyncEnabledCellsFromShapes(dirtyTiles);

        foreach (Terrain t in childTerrains)
        {
            if (t != null) terrainGrid.ApplyToTerrainPartial(t, dirtyTiles);
        }

        var splatMapper = terrainGrid.GetComponent<TerrainSplatMapper>();
        if (splatMapper != null)
            foreach (Terrain t in childTerrains)
                if (t != null) splatMapper.PaintSplatmapsPartial(t, dirtyTiles, terrainGrid);
    }

    /// <summary>
    /// Reverses the grading around a world point — lowers the bed back down.
    /// Mirror of GradeAroundPoint: bed corners are lowered by bedRaiseLevels,
    /// blend zone is smoothly lowered back toward the lower level.
    /// </summary>
    public void UngradeAroundPoint(Vector3 worldPos)
    {
        if (terrainGrid == null) return;

        if (!terrainGrid.TryWorldToFullTile(worldPos, out Vector2Int centerTile))
            return;

        float bedCornerRadius = bedHalfWidth + 0.5f;
        float totalRadius = bedCornerRadius + blendWidth;

        Vector2Int fullSize = terrainGrid.FullTileGridSize;
        int totalRadiusCeil = Mathf.CeilToInt(totalRadius);

        int minCx = Mathf.Max(0, centerTile.x - totalRadiusCeil);
        int maxCx = Mathf.Min(fullSize.x, centerTile.x + totalRadiusCeil + 1);
        int minCz = Mathf.Max(0, centerTile.y - totalRadiusCeil);
        int maxCz = Mathf.Min(fullSize.y, centerTile.y + totalRadiusCeil + 1);

        float centerCxF = centerTile.x + 0.5f;
        float centerCzF = centerTile.y + 0.5f;

        for (int cz = minCz; cz <= maxCz; cz++)
        {
            for (int cx = minCx; cx <= maxCx; cx++)
            {
                float dx = cx - centerCxF;
                float dz = cz - centerCzF;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);

                if (dist <= bedCornerRadius)
                {
                    int current = terrainGrid.GetCornerHeight(cx, cz);
                    int lowered = Mathf.Max(0, current - bedRaiseLevels);
                    terrainGrid.SetCornerHeight(cx, cz, lowered);
                }
                else if (dist <= totalRadius)
                {
                    float blendT = (dist - bedCornerRadius) / blendWidth;
                    blendT = blendT * blendT * (3f - 2f * blendT);

                    int current = terrainGrid.GetCornerHeight(cx, cz);
                    int lowered = Mathf.Max(0, current - bedRaiseLevels);
                    int blended = Mathf.RoundToInt(Mathf.Lerp(lowered, current, blendT));
                    terrainGrid.SetCornerHeight(cx, cz, Mathf.Max(0, blended));
                }
            }
        }

        RectInt dirtyTiles = new RectInt(
            minCx - 1, minCz - 1,
            (maxCx - minCx) + 3, (maxCz - minCz) + 3
        );
        terrainGrid.SyncEnabledCellsFromShapes(dirtyTiles);

        foreach (Terrain t in childTerrains)
        {
            if (t != null) terrainGrid.ApplyToTerrainPartial(t, dirtyTiles);
        }

        var splatMapper2 = terrainGrid.GetComponent<TerrainSplatMapper>();
        if (splatMapper2 != null)
            foreach (Terrain t in childTerrains)
                if (t != null) splatMapper2.PaintSplatmapsPartial(t, dirtyTiles, terrainGrid);
    }

    /// <summary>
    /// Reverses terrain grading along a line segment. Mirror of GradeAlongSegment.
    /// </summary>
    public void UngradeAlongSegment(Vector3 from, Vector3 to)
    {
        float dist = Vector3.Distance(from, to);
        int sampleCount = Mathf.Max(2, Mathf.CeilToInt(dist / 1f));

        for (int i = 0; i <= sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            Vector3 point = Vector3.Lerp(from, to, t);
            UngradeAroundPoint(point);
        }
    }
}

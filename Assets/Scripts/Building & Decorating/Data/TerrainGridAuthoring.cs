using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Shape of a full terrain tile, derived from its four corner heights.
/// Each tile's shape is a pure function of corner heights — never stored separately.
/// </summary>
public enum TileShape
{
    Flat,            // all 4 corners equal
    SlopeNorth,      // N edge high (NW+NE high, SW+SE low)
    SlopeSouth,      // S edge high (SW+SE high, NW+NE low)
    SlopeEast,       // E edge high (NE+SE high, NW+SW low)
    SlopeWest,       // W edge high (NW+SW high, NE+SE low)
    SlopeNorthEast,  // NE concave (3 high: NW+NE+SE, SW low)
    SlopeNorthWest,  // NW concave (3 high: NW+NE+SW, SE low)
    SlopeSouthEast,  // SE concave (3 high: NE+SW+SE, NW low)
    SlopeSouthWest,  // SW concave (3 high: NW+SW+SE, NE low)
    CornerNE,        // single NE corner high (bilinear: h = L + fx*fz)
    CornerNW,        // single NW corner high (bilinear: h = L + (1-fx)*fz)
    CornerSE,        // single SE corner high (bilinear: h = L + fx*(1-fz))
    CornerSW,        // single SW corner high (bilinear: h = L + (1-fx)*(1-fz))
    SaddleNESW,      // diagonal saddle: NE+SW high, NW+SE low
    SaddleNWSE,      // diagonal saddle: NW+SE high, NE+SW low
}

/// <summary>
/// Plain-English purpose:
/// Attach this to a terrain or floor object to define the grid that furniture snaps onto
/// and that the player and NPCs walk along in decorate mode.
///
/// This is the terrain-side counterpart to PlaceableGridAuthoring. It implements ISupportSurface
/// so the placement solver and minimap can treat it consistently with furniture top surfaces.
///
/// One enabled cell = one 0.5 x 0.5 female subtile available for furniture snapping.
/// One full tile   = a 2 x 2 block of subtiles = 1 x 1 world unit = one walkable player step.
/// </summary>
[DisallowMultipleComponent]
public class TerrainGridAuthoring : MonoBehaviour, ISupportSurface
{
    private const float MinTileSize = 0.1f;
    private const float FullTileWorldSize = 1f;
    private const float GizmoThickness = 0.02f;
    private const float FullTileGizmoThickness = 0.025f;

    private static readonly Color FemalePrimaryLight = new Color(0.93f, 0.36f, 0.72f, 0.50f);
    private static readonly Color FemalePrimaryDark = new Color(0.99f, 0.0f, 0.45f, 0.50f);
    private static readonly Color FemaleSecondaryLight = new Color(1f, 0.79f, 0.46f, 0.50f);
    private static readonly Color FemaleSecondaryDark = new Color(1f, 0.63f, 0.0f, 0.50f);
    private static readonly Color WalkableFullTileColor = new Color(0.2f, 0.9f, 0.3f, 0.18f);
    private static readonly Color DisabledCellOutlineColor = new Color(0.5f, 0.5f, 0.5f, 0.15f);

    // Size of one female subtile cell in world units. Should match the femaleTileSize used on furniture prefabs.
    [Min(MinTileSize)]
    [SerializeField] private float femaleTileSize = 0.5f;

    // Total grid dimensions in subtile cells. Set by RecalculateFromColliders or manually in the editor window.
    [SerializeField] private Vector2Int gridSizeInCells = new Vector2Int(4, 4);

    // Local-space offset from transform.position to the (0,0) cell corner along the authoring axes.
    // Stored so world-to-cell math works at runtime without a collider rescan.
    [SerializeField] private Vector3 gridOriginLocalOffset;

    // Flat row-major mask. True = subtile is available for furniture placement and contributes to full-tile walkability.
    [SerializeField] private List<bool> enabledCells = new List<bool>();

    [SerializeField] private bool drawGridGizmos = true;
    [SerializeField] private bool gizmoPerformanceMode = true;
    [SerializeField] private bool cullOffscreenGizmos = true;

    // ── Terraform data ───────────────────────────────────────────────────────────

    /// <summary>World units per one terrain height level.</summary>
    public const float LevelHeight = 0.5f;

    // Per-corner integer height level (0 = ground). Row-major with CornerGridSize = (FullTileGridSize + 1).
    // Corner (cx, cz) sits at the SW corner of tile (cx, cz). The extra +1 row/col covers NE edges.
    [SerializeField] private int[] cornerHeights = new int[0];

    // ── ISupportSurface ──────────────────────────────────────────────────────────

    public float FemaleTileSize => femaleTileSize;
    public Vector2Int GridSizeInCells => gridSizeInCells;
    public Transform SurfaceTransform => transform;

    public bool GetCell(int xIndex, int zIndex)
    {
        int flatIndex = GetFlatIndex(xIndex, zIndex);
        if (flatIndex < 0 || flatIndex >= enabledCells.Count)
            return false;

        if (!enabledCells[flatIndex])
            return false;

        // Slope tiles cannot be used for furniture placement or walkability.
        int s = SubtilesPerFullTile;
        if (ComputeTileShape(xIndex / s, zIndex / s) != TileShape.Flat)
            return false;

        return true;
    }

    public bool TryWorldToCell(Vector3 worldPosition, out Vector2Int cell)
    {
        cell = default;
        Vector3 offset = worldPosition - transform.position;
        float localX = Vector3.Dot(offset, transform.right) - gridOriginLocalOffset.x;
        float localZ = Vector3.Dot(offset, transform.forward) - gridOriginLocalOffset.z;

        int xIndex = Mathf.FloorToInt(localX / femaleTileSize);
        int zIndex = Mathf.FloorToInt(localZ / femaleTileSize);

        if (xIndex < 0 || xIndex >= gridSizeInCells.x || zIndex < 0 || zIndex >= gridSizeInCells.y)
        {
            return false;
        }

        cell = new Vector2Int(xIndex, zIndex);
        return true;
    }

    public bool TryGetCellCenterWorld(int xIndex, int zIndex, out Vector3 worldCenter)
    {
        worldCenter = default;

        if (xIndex < 0 || xIndex >= gridSizeInCells.x || zIndex < 0 || zIndex >= gridSizeInCells.y)
        {
            return false;
        }

        float localX = gridOriginLocalOffset.x + (xIndex + 0.5f) * femaleTileSize;
        float localZ = gridOriginLocalOffset.z + (zIndex + 0.5f) * femaleTileSize;

        // Add the terrain height for this cell's full tile so that gizmos, drag positioning,
        // and any caller that uses the returned Y are all at the correct elevated position.
        // Slope tiles are transitional — we leave them at base height.
        int s   = SubtilesPerFullTile;
        int ftx = xIndex / s;
        int ftz = zIndex / s;
        float heightY = (ComputeTileShape(ftx, ftz) == TileShape.Flat)
            ? GetTileLevel(ftx, ftz) * LevelHeight
            : 0f;

        worldCenter = transform.position
            + transform.right   * localX
            + transform.up      * (gridOriginLocalOffset.y + heightY)
            + transform.forward * localZ;

        return true;
    }

    public bool TryGetOverlappingCellRange(Bounds worldBounds, out Vector2Int minCell, out Vector2Int maxCell)
    {
        minCell = default;
        maxCell = default;

        Vector3 origin = transform.position;
        Vector3 right = transform.right;
        Vector3 forward = transform.forward;
        Vector3 c = worldBounds.center;
        Vector3 e = worldBounds.extents;

        float minLocalX = float.MaxValue;
        float maxLocalX = float.MinValue;
        float minLocalZ = float.MaxValue;
        float maxLocalZ = float.MinValue;

        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sy = -1; sy <= 1; sy += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 corner = c + new Vector3(e.x * sx, e.y * sy, e.z * sz);
                    Vector3 offset = corner - origin;
                    float lx = Vector3.Dot(offset, right) - gridOriginLocalOffset.x;
                    float lz = Vector3.Dot(offset, forward) - gridOriginLocalOffset.z;
                    if (lx < minLocalX) minLocalX = lx;
                    if (lx > maxLocalX) maxLocalX = lx;
                    if (lz < minLocalZ) minLocalZ = lz;
                    if (lz > maxLocalZ) maxLocalZ = lz;
                }
            }
        }

        int minX = Mathf.Clamp(Mathf.FloorToInt(minLocalX / femaleTileSize), 0, gridSizeInCells.x - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt(maxLocalX / femaleTileSize) - 1, 0, gridSizeInCells.x - 1);
        int minZ = Mathf.Clamp(Mathf.FloorToInt(minLocalZ / femaleTileSize), 0, gridSizeInCells.y - 1);
        int maxZ = Mathf.Clamp(Mathf.CeilToInt(maxLocalZ / femaleTileSize) - 1, 0, gridSizeInCells.y - 1);

        if (minX > maxX || minZ > maxZ)
        {
            return false;
        }

        minCell = new Vector2Int(minX, minZ);
        maxCell = new Vector2Int(maxX, maxZ);
        return true;
    }

    // ── Full-tile walkability API ─────────────────────────────────────────────────

    /// <summary>
    /// How many subtile cells fit along one axis of a full 1x1 world tile.
    /// With femaleTileSize = 0.5 this is 2, so each full tile is a 2x2 subtile block.
    /// </summary>
    public int SubtilesPerFullTile => Mathf.Max(1, Mathf.RoundToInt(FullTileWorldSize / femaleTileSize));

    /// <summary>Grid dimensions expressed in full 1x1 world tiles.</summary>
    public Vector2Int FullTileGridSize
    {
        get
        {
            int s = SubtilesPerFullTile;
            return new Vector2Int(gridSizeInCells.x / s, gridSizeInCells.y / s);
        }
    }

    /// <summary>
    /// Returns true when every subtile cell in the full tile at (tx, tz) is enabled.
    /// Used by PlayerMotor to decide whether a tile is walkable in decorate mode.
    /// </summary>
    public bool IsFullTileWalkable(int tx, int tz)
    {
        int s = SubtilesPerFullTile;
        Vector2Int fullSize = FullTileGridSize;

        if (tx < 0 || tx >= fullSize.x || tz < 0 || tz >= fullSize.y)
        {
            return false;
        }

        for (int sz = 0; sz < s; sz++)
        {
            for (int sx = 0; sx < s; sx++)
            {
                if (!GetCell(tx * s + sx, tz * s + sz))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Converts a world position to the full tile it falls inside.
    /// Returns false when the position is outside the grid.
    /// </summary>
    public bool TryWorldToFullTile(Vector3 worldPosition, out Vector2Int fullTile)
    {
        fullTile = default;
        Vector3 offset = worldPosition - transform.position;
        float localX = Vector3.Dot(offset, transform.right) - gridOriginLocalOffset.x;
        float localZ = Vector3.Dot(offset, transform.forward) - gridOriginLocalOffset.z;

        int tx = Mathf.FloorToInt(localX / FullTileWorldSize);
        int tz = Mathf.FloorToInt(localZ / FullTileWorldSize);
        Vector2Int fullSize = FullTileGridSize;

        if (tx < 0 || tx >= fullSize.x || tz < 0 || tz >= fullSize.y)
        {
            return false;
        }

        fullTile = new Vector2Int(tx, tz);
        return true;
    }

    /// <summary>
    /// Returns the world-space center of a full tile at (tx, tz) on the surface plane.
    /// </summary>
    public bool TryGetFullTileCenterWorld(int tx, int tz, out Vector3 worldCenter)
    {
        worldCenter = default;
        Vector2Int fullSize = FullTileGridSize;

        if (tx < 0 || tx >= fullSize.x || tz < 0 || tz >= fullSize.y)
        {
            return false;
        }

        float localX = gridOriginLocalOffset.x + (tx + 0.5f) * FullTileWorldSize;
        float localZ = gridOriginLocalOffset.z + (tz + 0.5f) * FullTileWorldSize;

        worldCenter = transform.position
            + transform.right * localX
            + transform.up * gridOriginLocalOffset.y
            + transform.forward * localZ;

        return true;
    }

    /// <summary>
    /// Finds the nearest walkable full tile to a world position using a BFS shell search.
    /// Used by PlayerMotor when entering decorate mode to snap the player to the grid.
    /// </summary>
    public bool TryGetNearestWalkableFullTile(Vector3 worldPosition, out Vector2Int fullTile)
    {
        fullTile = default;
        Vector2Int fullSize = FullTileGridSize;

        if (fullSize.x == 0 || fullSize.y == 0)
        {
            return false;
        }

        // Clamp the starting cell to the grid boundary.
        Vector3 offset = worldPosition - transform.position;
        float localX = Vector3.Dot(offset, transform.right) - gridOriginLocalOffset.x;
        float localZ = Vector3.Dot(offset, transform.forward) - gridOriginLocalOffset.z;
        int startTx = Mathf.Clamp(Mathf.FloorToInt(localX / FullTileWorldSize), 0, fullSize.x - 1);
        int startTz = Mathf.Clamp(Mathf.FloorToInt(localZ / FullTileWorldSize), 0, fullSize.y - 1);

        // Check directly under the position first for the common case.
        if (IsFullTileWalkable(startTx, startTz))
        {
            fullTile = new Vector2Int(startTx, startTz);
            return true;
        }

        // Expand outward shell by shell.
        int maxRadius = fullSize.x + fullSize.y;
        for (int radius = 1; radius <= maxRadius; radius++)
        {
            for (int dz = -radius; dz <= radius; dz++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Mathf.Abs(dx) != radius && Mathf.Abs(dz) != radius)
                    {
                        continue;
                    }

                    int tx = startTx + dx;
                    int tz = startTz + dz;

                    if (tx < 0 || tx >= fullSize.x || tz < 0 || tz >= fullSize.y)
                    {
                        continue;
                    }

                    if (IsFullTileWalkable(tx, tz))
                    {
                        fullTile = new Vector2Int(tx, tz);
                        return true;
                    }
                }
            }
        }

        return false;
    }

    // ── Authoring utilities ───────────────────────────────────────────────────────

    private void Reset()
    {
        RecalculateFromColliders();
        EnsureValidData();
    }

    private void OnValidate()
    {
        EnsureValidData();
    }

    public void EnsureValidData()
    {
        femaleTileSize = Mathf.Max(MinTileSize, femaleTileSize);
        gridSizeInCells.x = Mathf.Max(1, gridSizeInCells.x);
        gridSizeInCells.y = Mathf.Max(1, gridSizeInCells.y);
        ResizeEnabledCells();
        ResizeCornerData();
    }

    /// <summary>
    /// Measures all child colliders and sets the grid to cover their combined XZ footprint.
    /// Cells are enabled only where collider footprints exist.
    /// </summary>
    public void RecalculateFromColliders()
    {
        if (!TryGetProjectedColliderBounds(out ProjectedColliderBounds bounds))
        {
            EnsureValidData();
            FillAll(false);
            return;
        }

        femaleTileSize = Mathf.Max(MinTileSize, femaleTileSize);
        gridSizeInCells.x = Mathf.Max(1, Mathf.CeilToInt(bounds.SizeX / femaleTileSize));
        gridSizeInCells.y = Mathf.Max(1, Mathf.CeilToInt(bounds.SizeZ / femaleTileSize));
        gridOriginLocalOffset = new Vector3(bounds.MinX, bounds.MinY, bounds.MinZ);
        ResizeEnabledCells();
        ResizeCornerData();
        FillEnabledCellsFromColliders();
    }

    public void FillAll(bool value)
    {
        ResizeEnabledCells();
        for (int i = 0; i < enabledCells.Count; i++)
        {
            enabledCells[i] = value;
        }
    }

    public bool DrawGridGizmos => drawGridGizmos;
    public bool GizmoPerformanceMode => gizmoPerformanceMode;
    public bool CullOffscreenGizmos => cullOffscreenGizmos;
    public Vector3 GridOriginLocalOffset => gridOriginLocalOffset;

    public int GetEnabledCellCount()
    {
        int count = 0;
        for (int i = 0; i < enabledCells.Count; i++)
        {
            if (enabledCells[i])
            {
                count++;
            }
        }

        return count;
    }

    public int GetWalkableFullTileCount()
    {
        Vector2Int fullSize = FullTileGridSize;
        int count = 0;
        for (int tz = 0; tz < fullSize.y; tz++)
        {
            for (int tx = 0; tx < fullSize.x; tx++)
            {
                if (IsFullTileWalkable(tx, tz))
                {
                    count++;
                }
            }
        }

        return count;
    }

    public void SetCell(int xIndex, int zIndex, bool value)
    {
        int flatIndex = GetFlatIndex(xIndex, zIndex);
        if (flatIndex < 0 || flatIndex >= enabledCells.Count)
        {
            return;
        }

        enabledCells[flatIndex] = value;
    }

    // ── Terraform accessors (corner-height based) ──────────────────────────────

    /// <summary>Grid dimensions for the corner array: (FullTileGridSize.x + 1, FullTileGridSize.y + 1).</summary>
    public Vector2Int CornerGridSize
    {
        get
        {
            Vector2Int ft = FullTileGridSize;
            return new Vector2Int(ft.x + 1, ft.y + 1);
        }
    }

    public int GetCornerHeight(int cx, int cz)
    {
        int idx = GetCornerIndex(cx, cz);
        if (idx < 0 || idx >= cornerHeights.Length) return 0;
        return cornerHeights[idx];
    }

    public void SetCornerHeight(int cx, int cz, int level)
    {
        int idx = GetCornerIndex(cx, cz);
        if (idx < 0 || idx >= cornerHeights.Length) return;
        cornerHeights[idx] = Mathf.Max(0, level);
    }

    /// <summary>
    /// The base level of tile (tx, tz) = min of its 4 corner heights.
    /// Backwards-compatible replacement for the old per-tile stored height.
    /// </summary>
    public int GetTileLevel(int tx, int tz)
    {
        int sw = GetCornerHeight(tx,     tz);
        int se = GetCornerHeight(tx + 1, tz);
        int nw = GetCornerHeight(tx,     tz + 1);
        int ne = GetCornerHeight(tx + 1, tz + 1);
        return Mathf.Min(sw, Mathf.Min(se, Mathf.Min(nw, ne)));
    }

    /// <summary>Backwards-compatible wrapper — returns GetTileLevel.</summary>
    public int GetTileHeight(int tx, int tz) => GetTileLevel(tx, tz);

    // ── 4-bit shape lookup table ─────────────────────────────────────────────────
    // Bit layout: NW=bit3, NE=bit2, SW=bit1, SE=bit0 (1 = corner is above base level)
    private static readonly TileShape[] ShapeLookup = new TileShape[16]
    {
        /* 0000 */ TileShape.Flat,
        /* 0001 */ TileShape.CornerSE,
        /* 0010 */ TileShape.CornerSW,
        /* 0011 */ TileShape.SlopeSouth,
        /* 0100 */ TileShape.CornerNE,
        /* 0101 */ TileShape.SlopeEast,
        /* 0110 */ TileShape.SaddleNESW,
        /* 0111 */ TileShape.SlopeSouthEast,
        /* 1000 */ TileShape.CornerNW,
        /* 1001 */ TileShape.SaddleNWSE,
        /* 1010 */ TileShape.SlopeWest,
        /* 1011 */ TileShape.SlopeSouthWest,
        /* 1100 */ TileShape.SlopeNorth,
        /* 1101 */ TileShape.SlopeNorthEast,
        /* 1110 */ TileShape.SlopeNorthWest,
        /* 1111 */ TileShape.Flat,
    };

    /// <summary>Derives tile shape from 4 corner heights via a 4-bit lookup.</summary>
    public TileShape ComputeTileShape(int tx, int tz)
    {
        int baseLevel = GetTileLevel(tx, tz);
        int nw = (GetCornerHeight(tx,     tz + 1) > baseLevel) ? 1 : 0;
        int ne = (GetCornerHeight(tx + 1, tz + 1) > baseLevel) ? 1 : 0;
        int sw = (GetCornerHeight(tx,     tz)     > baseLevel) ? 1 : 0;
        int se = (GetCornerHeight(tx + 1, tz)     > baseLevel) ? 1 : 0;
        int bits = (nw << 3) | (ne << 2) | (sw << 1) | se;
        return ShapeLookup[bits];
    }

    /// <summary>Backwards-compatible wrapper — returns ComputeTileShape.</summary>
    public TileShape GetTileShape(int tx, int tz) => ComputeTileShape(tx, tz);

    // ── Multi-layer helpers for the decorate system ──────────────────────────────

    /// <summary>
    /// Returns sorted distinct integer height levels that have at least one flat tile.
    /// Level 0 is always included (the ground floor layer).
    /// Used by DecorateMinimapUI to build the available-layers list.
    /// </summary>
    public IReadOnlyList<int> GetFlatHeightLevels()
    {
        var levels = new HashSet<int> { 0 };
        Vector2Int fullSize = FullTileGridSize;
        for (int tz = 0; tz < fullSize.y; tz++)
            for (int tx = 0; tx < fullSize.x; tx++)
                if (GetTileShape(tx, tz) == TileShape.Flat)
                    levels.Add(GetTileHeight(tx, tz));
        var sorted = new List<int>(levels);
        sorted.Sort();
        return sorted;
    }

    /// <summary>
    /// World Y (absolute) for a given terrain height level.
    /// Includes gridOriginLocalOffset.y so the result matches TryGetCellCenterWorld's Y
    /// for a flat tile at level 0, and each additional level adds LevelHeight on top.
    /// </summary>
    public float GetTerrainLevelWorldY(int level)
        => transform.position.y + gridOriginLocalOffset.y + level * LevelHeight;

    /// <summary>
    /// Returns true when subtile (xSubtile, zSubtile) is flat AND its owning full tile
    /// is at the given height level. Used by DecorateMinimapUI to filter cells per layer.
    /// </summary>
    public bool GetCellAtLevel(int xSubtile, int zSubtile, int level)
    {
        if (!GetCell(xSubtile, zSubtile)) return false;
        int s = SubtilesPerFullTile;
        int tx = xSubtile / s;
        int tz = zSubtile / s;
        return GetTileLevel(tx, tz) == level;
    }

    /// <summary>
    /// After changing corner heights, call this to sync the enabledCells mask:
    /// slope tiles → all 4 subtiles disabled; flat tiles → all 4 subtiles enabled.
    /// </summary>
    public void SyncEnabledCellsFromShapes()
    {
        Vector2Int fullSize = FullTileGridSize;
        int s = SubtilesPerFullTile;
        for (int tz = 0; tz < fullSize.y; tz++)
        {
            for (int tx = 0; tx < fullSize.x; tx++)
            {
                bool flat = ComputeTileShape(tx, tz) == TileShape.Flat;
                for (int sz = 0; sz < s; sz++)
                    for (int sx = 0; sx < s; sx++)
                        SetCell(tx * s + sx, tz * s + sz, flat);
            }
        }
    }

    /// <summary>
    /// Bakes corner heights into the Unity Terrain heightmap using bilinear interpolation.
    /// Each heightmap pixel is mapped to a world position via the terrain's transform and size,
    /// then converted to grid tile coordinates so the heightmap aligns with the tile grid
    /// regardless of terrain size or position. Pixels outside the grid are set to height 0.
    /// </summary>
    public void ApplyToTerrain(Terrain unityTerrain)
    {
        if (unityTerrain == null) return;
        TerrainData td = unityTerrain.terrainData;
        if (td == null) return;

        int res = td.heightmapResolution;
        Vector2Int fullSize = FullTileGridSize;

        Vector3 terrainPos = unityTerrain.transform.position;
        Vector3 terrainSize = td.size; // (width, height, length) in world units

        float terrainHeight = terrainSize.y;
        float normalPerLevel = (terrainHeight > 0f) ? LevelHeight / terrainHeight : 0f;

        // Grid-to-world conversion basis.
        Vector3 gridPos = transform.position;
        Vector3 gridRight = transform.right;
        Vector3 gridForward = transform.forward;

        float[,] heights = td.GetHeights(0, 0, res, res);

        for (int hz = 0; hz < res; hz++)
        {
            for (int hx = 0; hx < res; hx++)
            {
                // World position of this heightmap pixel.
                float worldX = terrainPos.x + ((float)hx / (res - 1)) * terrainSize.x;
                float worldZ = terrainPos.z + ((float)hz / (res - 1)) * terrainSize.z;

                // Project into grid local space.
                Vector3 offset = new Vector3(worldX - gridPos.x, 0f, worldZ - gridPos.z);
                float localX = Vector3.Dot(offset, gridRight) - gridOriginLocalOffset.x;
                float localZ = Vector3.Dot(offset, gridForward) - gridOriginLocalOffset.z;

                // Convert to tile-space floating point.
                float tileFX = localX / FullTileWorldSize;
                float tileFZ = localZ / FullTileWorldSize;

                // Pixels outside the grid get height 0.
                if (tileFX < 0f || tileFX > fullSize.x || tileFZ < 0f || tileFZ > fullSize.y)
                {
                    heights[hz, hx] = 0f;
                    continue;
                }

                int tx = Mathf.Clamp(Mathf.FloorToInt(tileFX), 0, fullSize.x - 1);
                int tz = Mathf.Clamp(Mathf.FloorToInt(tileFZ), 0, fullSize.y - 1);

                float fx = Mathf.Clamp01(tileFX - tx);
                float fz = Mathf.Clamp01(tileFZ - tz);

                // Bilinear interpolation of the 4 corner heights.
                float hSW = GetCornerHeight(tx,     tz);
                float hSE = GetCornerHeight(tx + 1, tz);
                float hNW = GetCornerHeight(tx,     tz + 1);
                float hNE = GetCornerHeight(tx + 1, tz + 1);

                float h = hSW * (1f - fx) * (1f - fz)
                        + hSE * fx         * (1f - fz)
                        + hNW * (1f - fx)  * fz
                        + hNE * fx         * fz;

                heights[hz, hx] = Mathf.Clamp01(h * normalPerLevel);
            }
        }

        td.SetHeights(0, 0, heights);
    }

    /// <summary>
    /// Partial heightmap bake: only updates the heightmap pixels that overlap the given
    /// tile rectangle (expanded by 1 tile for slope-neighbour influence). Much faster
    /// than a full ApplyToTerrain when editing a single tile at runtime.
    /// </summary>
    public void ApplyToTerrainPartial(Terrain unityTerrain, RectInt tileRegion)
    {
        if (unityTerrain == null) return;
        TerrainData td = unityTerrain.terrainData;
        if (td == null) return;

        int res = td.heightmapResolution;
        Vector2Int fullSize = FullTileGridSize;

        // Expand by 1 tile on each side for slope-neighbour influence, clamp to grid.
        int minTx = Mathf.Max(0, tileRegion.xMin - 1);
        int minTz = Mathf.Max(0, tileRegion.yMin - 1);
        int maxTx = Mathf.Min(fullSize.x, tileRegion.xMax + 1);
        int maxTz = Mathf.Min(fullSize.y, tileRegion.yMax + 1);

        Vector3 terrainPos = unityTerrain.transform.position;
        Vector3 terrainSize = td.size;
        float terrainHeight = terrainSize.y;
        float normalPerLevel = (terrainHeight > 0f) ? LevelHeight / terrainHeight : 0f;

        Vector3 gridPos = transform.position;
        Vector3 gridRight = transform.right;
        Vector3 gridForward = transform.forward;

        // Compute the heightmap pixel bounding-box from the 4 corners of the tile region.
        float pxMin = float.MaxValue, pxMax = float.MinValue;
        float pzMin = float.MaxValue, pzMax = float.MinValue;
        for (int cz = 0; cz <= 1; cz++)
        {
            for (int cx = 0; cx <= 1; cx++)
            {
                float lx = (cx == 0 ? minTx : maxTx) * FullTileWorldSize + gridOriginLocalOffset.x;
                float lz = (cz == 0 ? minTz : maxTz) * FullTileWorldSize + gridOriginLocalOffset.z;
                float wx = gridPos.x + gridRight.x * lx + gridForward.x * lz;
                float wz = gridPos.z + gridRight.z * lx + gridForward.z * lz;
                float px = (wx - terrainPos.x) / terrainSize.x * (res - 1);
                float pz = (wz - terrainPos.z) / terrainSize.z * (res - 1);
                if (px < pxMin) pxMin = px; if (px > pxMax) pxMax = px;
                if (pz < pzMin) pzMin = pz; if (pz > pzMax) pzMax = pz;
            }
        }

        // Clamp to heightmap bounds with 1-pixel padding for rounding.
        int hxMin = Mathf.Max(0, Mathf.FloorToInt(pxMin) - 1);
        int hxMax = Mathf.Min(res - 1, Mathf.CeilToInt(pxMax) + 1);
        int hzMin = Mathf.Max(0, Mathf.FloorToInt(pzMin) - 1);
        int hzMax = Mathf.Min(res - 1, Mathf.CeilToInt(pzMax) + 1);

        int w = hxMax - hxMin + 1;
        int h = hzMax - hzMin + 1;
        if (w <= 0 || h <= 0) return;

        float[,] heights = td.GetHeights(hxMin, hzMin, w, h);

        for (int dz = 0; dz < h; dz++)
        {
            for (int dx = 0; dx < w; dx++)
            {
                int hx = hxMin + dx;
                int hz = hzMin + dz;

                float worldX = terrainPos.x + ((float)hx / (res - 1)) * terrainSize.x;
                float worldZ = terrainPos.z + ((float)hz / (res - 1)) * terrainSize.z;

                Vector3 offset = new Vector3(worldX - gridPos.x, 0f, worldZ - gridPos.z);
                float localX = Vector3.Dot(offset, gridRight) - gridOriginLocalOffset.x;
                float localZ = Vector3.Dot(offset, gridForward) - gridOriginLocalOffset.z;

                float tileFX = localX / FullTileWorldSize;
                float tileFZ = localZ / FullTileWorldSize;

                if (tileFX < 0f || tileFX > fullSize.x || tileFZ < 0f || tileFZ > fullSize.y)
                {
                    heights[dz, dx] = 0f;
                    continue;
                }

                int tx = Mathf.Clamp(Mathf.FloorToInt(tileFX), 0, fullSize.x - 1);
                int tz = Mathf.Clamp(Mathf.FloorToInt(tileFZ), 0, fullSize.y - 1);
                float fx = Mathf.Clamp01(tileFX - tx);
                float fz = Mathf.Clamp01(tileFZ - tz);

                float hSW = GetCornerHeight(tx,     tz);
                float hSE = GetCornerHeight(tx + 1, tz);
                float hNW = GetCornerHeight(tx,     tz + 1);
                float hNE = GetCornerHeight(tx + 1, tz + 1);

                float hVal = hSW * (1f - fx) * (1f - fz)
                           + hSE * fx         * (1f - fz)
                           + hNW * (1f - fx)  * fz
                           + hNE * fx         * fz;

                heights[dz, dx] = Mathf.Clamp01(hVal * normalPerLevel);
            }
        }

        td.SetHeights(hxMin, hzMin, heights);
    }

    /// <summary>
    /// Partial enabled-cell sync: only updates tiles within the given rectangle.
    /// Use after editing a small number of corners at runtime.
    /// </summary>
    public void SyncEnabledCellsFromShapes(RectInt tileRegion)
    {
        Vector2Int fullSize = FullTileGridSize;
        int s = SubtilesPerFullTile;
        int minTx = Mathf.Max(0, tileRegion.xMin);
        int maxTx = Mathf.Min(fullSize.x, tileRegion.xMax);
        int minTz = Mathf.Max(0, tileRegion.yMin);
        int maxTz = Mathf.Min(fullSize.y, tileRegion.yMax);
        for (int tz = minTz; tz < maxTz; tz++)
        {
            for (int tx = minTx; tx < maxTx; tx++)
            {
                bool flat = ComputeTileShape(tx, tz) == TileShape.Flat;
                for (int sz = 0; sz < s; sz++)
                    for (int sx = 0; sx < s; sx++)
                        SetCell(tx * s + sx, tz * s + sz, flat);
            }
        }
    }

    // ── Terraform internal ───────────────────────────────────────────────────────

    private int GetCornerIndex(int cx, int cz)
    {
        Vector2Int cs = CornerGridSize;
        if (cx < 0 || cx >= cs.x || cz < 0 || cz >= cs.y) return -1;
        return cz * cs.x + cx;
    }

    private int GetFullTileIndex(int tx, int tz)
    {
        Vector2Int fullSize = FullTileGridSize;
        if (tx < 0 || tx >= fullSize.x || tz < 0 || tz >= fullSize.y) return -1;
        return tz * fullSize.x + tx;
    }

    private void ResizeCornerData()
    {
        Vector2Int cs = CornerGridSize;
        int required = cs.x * cs.y;

        if (cornerHeights.Length != required)
        {
            int[] newHeights = new int[required];
            for (int i = 0; i < Mathf.Min(cornerHeights.Length, required); i++)
                newHeights[i] = cornerHeights[i];
            cornerHeights = newHeights;
        }
    }

    // ── Collider projection ──────────────────────────────────────────────────────

    // Mirrors the exact same technique used in PlaceableGridAuthoring so the two systems stay consistent.

    private struct ProjectedColliderBounds
    {
        public float MinX, MaxX;
        public float MinY, MaxY;
        public float MinZ, MaxZ;
        public float SizeX => MaxX - MinX;
        public float SizeY => MaxY - MinY;
        public float SizeZ => MaxZ - MinZ;
    }

    private bool TryGetProjectedColliderBounds(out ProjectedColliderBounds projectedBounds)
    {
        Collider[] colliders = GetComponentsInChildren<Collider>();
        bool hasBounds = false;
        projectedBounds = default;

        foreach (Collider col in colliders)
        {
            if (!col.enabled)
            {
                continue;
            }

            if (!TryGetColliderLocalBounds(col, out Bounds localBounds))
            {
                continue;
            }

            Vector3 origin = transform.position;
            Vector3 right = transform.right;
            Vector3 up = transform.up;
            Vector3 forward = transform.forward;
            Vector3 center = localBounds.center;
            Vector3 extents = localBounds.extents;

            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 sourceCorner = center + Vector3.Scale(extents, new Vector3(sx, sy, sz));
                        Vector3 worldCorner = col.transform.TransformPoint(sourceCorner);
                        Vector3 offset = worldCorner - origin;
                        float px = Vector3.Dot(offset, right);
                        float py = Vector3.Dot(offset, up);
                        float pz = Vector3.Dot(offset, forward);

                        if (!hasBounds)
                        {
                            projectedBounds.MinX = projectedBounds.MaxX = px;
                            projectedBounds.MinY = projectedBounds.MaxY = py;
                            projectedBounds.MinZ = projectedBounds.MaxZ = pz;
                            hasBounds = true;
                        }
                        else
                        {
                            if (px < projectedBounds.MinX) projectedBounds.MinX = px;
                            if (px > projectedBounds.MaxX) projectedBounds.MaxX = px;
                            if (py < projectedBounds.MinY) projectedBounds.MinY = py;
                            if (py > projectedBounds.MaxY) projectedBounds.MaxY = py;
                            if (pz < projectedBounds.MinZ) projectedBounds.MinZ = pz;
                            if (pz > projectedBounds.MaxZ) projectedBounds.MaxZ = pz;
                        }
                    }
                }
            }
        }

        return hasBounds;
    }

    private bool TryGetColliderProjectedBounds(Collider col, out ProjectedColliderBounds projectedBounds)
    {
        projectedBounds = default;

        if (col == null || !col.enabled)
        {
            return false;
        }

        if (!TryGetColliderLocalBounds(col, out Bounds localBounds))
        {
            return false;
        }

        Vector3 origin = transform.position;
        Vector3 right = transform.right;
        Vector3 up = transform.up;
        Vector3 forward = transform.forward;
        Vector3 center = localBounds.center;
        Vector3 extents = localBounds.extents;
        bool hasBounds = false;

        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sy = -1; sy <= 1; sy += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 sourceCorner = center + Vector3.Scale(extents, new Vector3(sx, sy, sz));
                    Vector3 worldCorner = col.transform.TransformPoint(sourceCorner);
                    Vector3 offset = worldCorner - origin;
                    float px = Vector3.Dot(offset, right);
                    float py = Vector3.Dot(offset, up);
                    float pz = Vector3.Dot(offset, forward);

                    if (!hasBounds)
                    {
                        projectedBounds.MinX = projectedBounds.MaxX = px;
                        projectedBounds.MinY = projectedBounds.MaxY = py;
                        projectedBounds.MinZ = projectedBounds.MaxZ = pz;
                        hasBounds = true;
                    }
                    else
                    {
                        if (px < projectedBounds.MinX) projectedBounds.MinX = px;
                        if (px > projectedBounds.MaxX) projectedBounds.MaxX = px;
                        if (py < projectedBounds.MinY) projectedBounds.MinY = py;
                        if (py > projectedBounds.MaxY) projectedBounds.MaxY = py;
                        if (pz < projectedBounds.MinZ) projectedBounds.MinZ = pz;
                        if (pz > projectedBounds.MaxZ) projectedBounds.MaxZ = pz;
                    }
                }
            }
        }

        return hasBounds;
    }

    private void FillEnabledCellsFromColliders()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>();
        var projected = new List<ProjectedColliderBounds>(colliders.Length);

        foreach (Collider col in colliders)
        {
            if (TryGetColliderProjectedBounds(col, out ProjectedColliderBounds bounds))
            {
                projected.Add(bounds);
            }
        }

        if (projected.Count == 0)
        {
            FillAll(false);
            return;
        }

        for (int z = 0; z < gridSizeInCells.y; z++)
        {
            for (int x = 0; x < gridSizeInCells.x; x++)
            {
                float localX = gridOriginLocalOffset.x + (x + 0.5f) * femaleTileSize;
                float localZ = gridOriginLocalOffset.z + (z + 0.5f) * femaleTileSize;
                bool enabled = false;

                for (int i = 0; i < projected.Count; i++)
                {
                    ProjectedColliderBounds p = projected[i];
                    if (localX >= p.MinX && localX <= p.MaxX && localZ >= p.MinZ && localZ <= p.MaxZ)
                    {
                        enabled = true;
                        break;
                    }
                }

                SetCell(x, z, enabled);
            }
        }
    }

    private static bool TryGetColliderLocalBounds(Collider col, out Bounds localBounds)
    {
        localBounds = default;

        switch (col)
        {
            case BoxCollider box:
                localBounds = new Bounds(box.center, box.size);
                return true;

            case SphereCollider sphere:
            {
                float diameter = sphere.radius * 2f;
                localBounds = new Bounds(sphere.center, new Vector3(diameter, diameter, diameter));
                return true;
            }

            case CapsuleCollider capsule:
            {
                Vector3 size = Vector3.one * (capsule.radius * 2f);
                size[capsule.direction] = Mathf.Max(capsule.height, size[capsule.direction]);
                localBounds = new Bounds(capsule.center, size);
                return true;
            }

            case MeshCollider mesh when mesh.sharedMesh != null:
                localBounds = mesh.sharedMesh.bounds;
                return true;

            case TerrainCollider terrain when terrain.terrainData != null:
            {
                // TerrainData.size is in the terrain's local space (origin = bottom-left corner).
                Vector3 size = terrain.terrainData.size;
                localBounds = new Bounds(size * 0.5f, size);
                return true;
            }

            default:
            {
                // Generic fallback: convert world-space AABB bounds into the collider's local space.
                if (col.bounds.size.sqrMagnitude > 0f)
                {
                    Bounds world = col.bounds;
                    Vector3 localCenter = col.transform.InverseTransformPoint(world.center);
                    // Extents stay the same magnitude regardless of rotation for an AABB approximation.
                    localBounds = new Bounds(localCenter, world.size);
                    return true;
                }
                return false;
            }
        }
    }

    // ── Internal helpers ─────────────────────────────────────────────────────────

    private int GetFlatIndex(int xIndex, int zIndex)
    {
        if (xIndex < 0 || xIndex >= gridSizeInCells.x || zIndex < 0 || zIndex >= gridSizeInCells.y)
        {
            return -1;
        }

        return zIndex * gridSizeInCells.x + xIndex;
    }

    private void ResizeEnabledCells()
    {
        int required = gridSizeInCells.x * gridSizeInCells.y;

        while (enabledCells.Count < required)
        {
            enabledCells.Add(true);
        }

        while (enabledCells.Count > required)
        {
            enabledCells.RemoveAt(enabledCells.Count - 1);
        }
    }

    // ── Gizmos ───────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (!drawGridGizmos)
        {
            return;
        }

        Camera gizmoCamera = GetGizmoCamera();
        if (!IsVisibleInCamera(gizmoCamera, GetGridWorldBounds()))
        {
            return;
        }

        if (!gizmoPerformanceMode)
        {
            DrawSubtileCellGizmos(gizmoCamera);
        }
        DrawWalkableFullTileOverlays(gizmoCamera);
    }

    private void DrawSubtileCellGizmos(Camera gizmoCamera)
    {
        int _s = SubtilesPerFullTile;
        for (int z = 0; z < gridSizeInCells.y; z++)
        {
            for (int x = 0; x < gridSizeInCells.x; x++)
            {
                float localX = gridOriginLocalOffset.x + (x + 0.5f) * femaleTileSize;
                float localZ = gridOriginLocalOffset.z + (z + 0.5f) * femaleTileSize;
                int _ftx = x / _s;  int _ftz = z / _s;
                float _hY = (ComputeTileShape(_ftx, _ftz) == TileShape.Flat)
                    ? GetTileLevel(_ftx, _ftz) * LevelHeight : 0f;
                Vector3 worldCenter = transform.position
                    + transform.right * localX
                    + transform.up * (gridOriginLocalOffset.y + _hY)
                    + transform.forward * localZ;
                Vector3 worldSize = new Vector3(femaleTileSize, GizmoThickness, femaleTileSize);

                if (!IsVisibleInCamera(gizmoCamera, new Bounds(worldCenter, worldSize)))
                {
                    continue;
                }

                Matrix4x4 prevMatrix = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(worldCenter, transform.rotation, Vector3.one);

                if (!GetCell(x, z))
                {
                    Gizmos.matrix = prevMatrix;
                    continue;
                }

                GetSubtileColors(x, z, out Color fill, out Color outline);
                Gizmos.color = fill;
                Gizmos.DrawCube(Vector3.zero, worldSize);
                Gizmos.color = outline;
                Gizmos.DrawWireCube(Vector3.zero, worldSize);

                Gizmos.matrix = prevMatrix;
            }
        }
    }

    private void DrawWalkableFullTileOverlays(Camera gizmoCamera)
    {
        Vector2Int fullSize = FullTileGridSize;

        for (int tz = 0; tz < fullSize.y; tz++)
        {
            for (int tx = 0; tx < fullSize.x; tx++)
            {
                if (!IsFullTileWalkable(tx, tz))
                {
                    continue;
                }

                float localX = gridOriginLocalOffset.x + (tx + 0.5f) * FullTileWorldSize;
                float localZ = gridOriginLocalOffset.z + (tz + 0.5f) * FullTileWorldSize;
                float _hY = GetTileLevel(tx, tz) * LevelHeight;
                Vector3 worldCenter = transform.position
                    + transform.right * localX
                    + transform.up * (gridOriginLocalOffset.y + _hY + FullTileGizmoThickness * 0.5f)
                    + transform.forward * localZ;
                Vector3 worldSize = new Vector3(FullTileWorldSize, FullTileGizmoThickness, FullTileWorldSize);

                if (!IsVisibleInCamera(gizmoCamera, new Bounds(worldCenter, worldSize)))
                {
                    continue;
                }

                Matrix4x4 prevMatrix = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(worldCenter, transform.rotation, Vector3.one);
                Gizmos.color = WalkableFullTileColor;
                Gizmos.DrawCube(Vector3.zero, worldSize);
                Gizmos.matrix = prevMatrix;
            }
        }
    }

    private void GetSubtileColors(int xIndex, int zIndex, out Color fill, out Color outline)
    {
        int s = SubtilesPerFullTile;
        int tileX = xIndex / s;
        int tileZ = zIndex / s;
        int subtileX = xIndex % s;
        int subtileZ = zIndex % s;

        bool primary = ((tileX + tileZ) & 1) == 0;
        bool light = ((subtileX + subtileZ) & 1) == 0;

        fill = primary
            ? (light ? FemalePrimaryLight : FemalePrimaryDark)
            : (light ? FemaleSecondaryLight : FemaleSecondaryDark);

        outline = Color.Lerp(fill, Color.black, 0.3f);
        outline.a = 0.95f;
    }

    private Camera GetGizmoCamera()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && SceneView.lastActiveSceneView != null)
        {
            return SceneView.lastActiveSceneView.camera;
        }
#endif
        return Camera.current;
    }

    private Bounds GetGridWorldBounds()
    {
        float width = gridSizeInCells.x * femaleTileSize;
        float depth = gridSizeInCells.y * femaleTileSize;
        float centerX = gridOriginLocalOffset.x + width * 0.5f;
        float centerZ = gridOriginLocalOffset.z + depth * 0.5f;

        Vector3 worldCenter = transform.position
            + transform.right * centerX
            + transform.up * gridOriginLocalOffset.y
            + transform.forward * centerZ;

        return new Bounds(worldCenter, new Vector3(width, 1f, depth));
    }

    private bool IsVisibleInCamera(Camera camera, Bounds worldBounds)
    {
        if (!cullOffscreenGizmos) return true;
        if (camera == null) return true;

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
        return GeometryUtility.TestPlanesAABB(planes, worldBounds);
    }
}

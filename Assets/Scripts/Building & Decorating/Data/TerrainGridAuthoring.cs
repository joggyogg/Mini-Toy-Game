using System.Collections.Generic;
using UnityEngine;

/// <summary>Shape of a full terrain tile for terraforming.</summary>
public enum TileShape
{
    Flat,
    SlopeNorth,      // 1-dir: north edge high
    SlopeSouth,      // 1-dir: south edge high
    SlopeEast,       // 1-dir: east edge high
    SlopeWest,       // 1-dir: west edge high
    SlopeNorthEast,  // 2-dir corner (adjacent)
    SlopeNorthWest,  // 2-dir corner (adjacent)
    SlopeSouthEast,  // 2-dir corner (adjacent)
    SlopeSouthWest,  // 2-dir corner (adjacent)
    SlopeNS,         // 2-dir opposite: N+S high, vault ridge running E-W
    SlopeEW,         // 2-dir opposite: E+W high, vault ridge running N-S
    SlopeNSE,        // 3-dir: N+S+E high
    SlopeNSW,        // 3-dir: N+S+W high
    SlopeNEW,        // 3-dir: N+E+W high
    SlopeSEW,        // 3-dir: S+E+W high
    SlopePyramid,    // 4-dir surrounded: all edges high, bowl/concave from center
    PyramidUp,        // standalone: center high, edges low, convex peak
    CornerNE,         // single NE corner high (bilinear: h = L + fx*fz)
    CornerNW,         // single NW corner high (bilinear: h = L + (1-fx)*fz)
    CornerSE,         // single SE corner high (bilinear: h = L + fx*(1-fz))
    CornerSW,         // single SW corner high (bilinear: h = L + (1-fx)*(1-fz))
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

    // ── Terraform data ───────────────────────────────────────────────────────────

    /// <summary>World units per one terrain height level.</summary>
    public const float LevelHeight = 0.5f;

    // Per-full-tile integer height level (0 = ground). Row-major with FullTileGridSize.
    [SerializeField] private int[] tileHeights = new int[0];

    // Per-full-tile shape. Parallel array to tileHeights.
    [SerializeField] private TileShape[] tileShapes = new TileShape[0];

    // ── ISupportSurface ──────────────────────────────────────────────────────────

    public float FemaleTileSize => femaleTileSize;
    public Vector2Int GridSizeInCells => gridSizeInCells;
    public Transform SurfaceTransform => transform;

    public bool GetCell(int xIndex, int zIndex)
    {
        int flatIndex = GetFlatIndex(xIndex, zIndex);
        if (flatIndex < 0 || flatIndex >= enabledCells.Count)
        {
            return false;
        }

        return enabledCells[flatIndex];
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
        float heightY = (GetTileShape(ftx, ftz) == TileShape.Flat)
            ? GetTileHeight(ftx, ftz) * LevelHeight
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
        ResizeTileData();
    }

    /// <summary>
    /// Measures all child colliders and sets the grid to cover their combined XZ footprint.
    /// All cells are set to enabled after recalculation.
    /// </summary>
    public void RecalculateFromColliders()
    {
        if (!TryGetProjectedColliderBounds(out ProjectedColliderBounds bounds))
        {
            EnsureValidData();
            return;
        }

        femaleTileSize = Mathf.Max(MinTileSize, femaleTileSize);
        gridSizeInCells.x = Mathf.Max(1, Mathf.CeilToInt(bounds.SizeX / femaleTileSize));
        gridSizeInCells.y = Mathf.Max(1, Mathf.CeilToInt(bounds.SizeZ / femaleTileSize));
        gridOriginLocalOffset = new Vector3(bounds.MinX, bounds.MinY, bounds.MinZ);
        ResizeEnabledCells();
        ResizeTileData();
        FillAll(true);
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

    // ── Terraform accessors ──────────────────────────────────────────────────────

    public int GetTileHeight(int tx, int tz)
    {
        int idx = GetFullTileIndex(tx, tz);
        if (idx < 0 || idx >= tileHeights.Length) return 0;
        return tileHeights[idx];
    }

    public void SetTileHeight(int tx, int tz, int level)
    {
        int idx = GetFullTileIndex(tx, tz);
        if (idx < 0 || idx >= tileHeights.Length) return;
        tileHeights[idx] = Mathf.Max(0, level);
    }

    public TileShape GetTileShape(int tx, int tz)
    {
        int idx = GetFullTileIndex(tx, tz);
        if (idx < 0 || idx >= tileShapes.Length) return TileShape.Flat;
        return tileShapes[idx];
    }

    public void SetTileShape(int tx, int tz, TileShape shape)
    {
        int idx = GetFullTileIndex(tx, tz);
        if (idx < 0 || idx >= tileShapes.Length) return;
        tileShapes[idx] = shape;
    }

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
        return GetTileHeight(tx, tz) == level;
    }

    /// <summary>
    /// After changing tileShapes, call this to sync the enabledCells mask:
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
                bool flat = GetTileShape(tx, tz) == TileShape.Flat;
                for (int sz = 0; sz < s; sz++)
                    for (int sx = 0; sx < s; sx++)
                        SetCell(tx * s + sx, tz * s + sz, flat);
            }
        }
    }

    /// <summary>
    /// Bakes tileHeights and tileShapes into the Unity Terrain heightmap.
    /// The terrain's heightmapResolution must be &gt;= (FullTileGridSize + 1) in each axis.
    /// Heights are normalised by terrainData.size.y so 1 level = LevelHeight world units.
    /// </summary>
    public void ApplyToTerrain(Terrain unityTerrain)
    {
        if (unityTerrain == null) return;
        TerrainData td = unityTerrain.terrainData;
        if (td == null) return;

        int res = td.heightmapResolution; // e.g. 513 for a default terrain
        Vector2Int fullSize = FullTileGridSize;

        // How many heightmap samples per full tile.
        // res-1 samples span the whole terrain width; we want one sample per tile corner.
        float samplesPerTileX = (res - 1f) / Mathf.Max(1, fullSize.x);
        float samplesPerTileZ = (res - 1f) / Mathf.Max(1, fullSize.y);

        if (samplesPerTileX < 1f || samplesPerTileZ < 1f)
        {
            Debug.LogWarning("[TerrainGridAuthoring] Heightmap resolution too low for tile grid size. Increase TerrainData.heightmapResolution.");
        }

        float terrainHeight = td.size.y;
        float normalPerLevel = (terrainHeight > 0f) ? LevelHeight / terrainHeight : 0f;

        // Unity SetHeights: array is [z, x], values 0..1.
        float[,] heights = td.GetHeights(0, 0, res, res);

        for (int hz = 0; hz < res; hz++)
        {
            for (int hx = 0; hx < res; hx++)
            {
                // Which full tile does this heightmap corner sit at?
                // Corners are at integer tile boundaries: tile (tx,tz) occupies corners tx..tx+1.
                float tileFX = hx / samplesPerTileX;
                float tileFZ = hz / samplesPerTileZ;

                int tx = Mathf.Clamp(Mathf.FloorToInt(tileFX), 0, fullSize.x - 1);
                int tz = Mathf.Clamp(Mathf.FloorToInt(tileFZ), 0, fullSize.y - 1);

                // Fractional position within the tile (0 = west/south edge, 1 = east/north edge).
                float fx = tileFX - tx;
                float fz = tileFZ - tz;

                int level = GetTileHeight(tx, tz);
                TileShape shape = GetTileShape(tx, tz);

                float h;
                // stored level = LOW edge; high edges reach level+1.
                // Corner formulas use bilinear of 4 corners (SW,SE,NW,NE):
                //   a corner is level+1 if ANY of its two adjacent edge-directions is high.
                // Pyramid: Chebyshev distance from tile center.
                switch (shape)
                {
                    case TileShape.SlopeNorth:
                        // SW=L SE=L NW=L+1 NE=L+1  →  L + fz
                        h = (level + fz) * normalPerLevel;
                        break;
                    case TileShape.SlopeSouth:
                        // SW=L+1 SE=L+1 NW=L NE=L  →  L + (1-fz)
                        h = (level + 1f - fz) * normalPerLevel;
                        break;
                    case TileShape.SlopeEast:
                        // SW=L SE=L+1 NW=L NE=L+1  →  L + fx
                        h = (level + fx) * normalPerLevel;
                        break;
                    case TileShape.SlopeWest:
                        // SW=L+1 SE=L NW=L+1 NE=L  →  L + (1-fx)
                        h = (level + 1f - fx) * normalPerLevel;
                        break;
                    case TileShape.SlopeNorthEast:
                        // SW=L SE=L+1 NW=L+1 NE=L+1  →  L + fx + fz - fx*fz
                        h = (level + fx + fz - fx * fz) * normalPerLevel;
                        break;
                    case TileShape.SlopeNorthWest:
                        // SW=L+1 SE=L NW=L+1 NE=L+1  →  L + 1 - fx*(1-fz)
                        h = (level + 1f - fx * (1f - fz)) * normalPerLevel;
                        break;
                    case TileShape.SlopeSouthEast:
                        // SW=L+1 SE=L+1 NW=L NE=L+1  →  L + 1 - fz*(1-fx)
                        h = (level + 1f - fz * (1f - fx)) * normalPerLevel;
                        break;
                    case TileShape.SlopeSouthWest:
                        // SW=L+1 SE=L+1 NW=L+1 NE=L  →  L + 1 - fx*fz
                        h = (level + 1f - fx * fz) * normalPerLevel;
                        break;
                    case TileShape.SlopeNS:
                        // N edge = S edge = L+1; vaults from centre with ridge along E-W
                        h = (level + Mathf.Max(fz, 1f - fz)) * normalPerLevel;
                        break;
                    case TileShape.SlopeEW:
                        // E edge = W edge = L+1; vaults from centre with ridge along N-S
                        h = (level + Mathf.Max(fx, 1f - fx)) * normalPerLevel;
                        break;
                    case TileShape.SlopeNSE:
                        // N, S, E edges = L+1; W side low
                        h = (level + Mathf.Max(fz, Mathf.Max(1f - fz, fx))) * normalPerLevel;
                        break;
                    case TileShape.SlopeNSW:
                        // N, S, W edges = L+1; E side low
                        h = (level + Mathf.Max(fz, Mathf.Max(1f - fz, 1f - fx))) * normalPerLevel;
                        break;
                    case TileShape.SlopeNEW:
                        // N, E, W edges = L+1; S side low
                        h = (level + Mathf.Max(fz, Mathf.Max(fx, 1f - fx))) * normalPerLevel;
                        break;
                    case TileShape.SlopeSEW:
                        // S, E, W edges = L+1; N side low
                        h = (level + Mathf.Max(1f - fz, Mathf.Max(fx, 1f - fx))) * normalPerLevel;
                        break;
                    case TileShape.SlopePyramid:
                        // All 4 edges L+1, centre L — concave bowl (inverted pyramid)
                        h = (level + Mathf.Max(Mathf.Abs(2f * fx - 1f), Mathf.Abs(2f * fz - 1f))) * normalPerLevel;
                        break;
                    case TileShape.PyramidUp:
                        // Centre L+1, all 4 edges L — convex peak (normal pyramid)
                        h = (level + 1f - Mathf.Max(Mathf.Abs(2f * fx - 1f), Mathf.Abs(2f * fz - 1f))) * normalPerLevel;
                        break;
                    case TileShape.CornerNE:
                        // Only NE corner = L+1; bilinear: h = L + fx*fz
                        h = (level + fx * fz) * normalPerLevel;
                        break;
                    case TileShape.CornerNW:
                        // Only NW corner = L+1; bilinear: h = L + (1-fx)*fz
                        h = (level + (1f - fx) * fz) * normalPerLevel;
                        break;
                    case TileShape.CornerSE:
                        // Only SE corner = L+1; bilinear: h = L + fx*(1-fz)
                        h = (level + fx * (1f - fz)) * normalPerLevel;
                        break;
                    case TileShape.CornerSW:
                        // Only SW corner = L+1; bilinear: h = L + (1-fx)*(1-fz)
                        h = (level + (1f - fx) * (1f - fz)) * normalPerLevel;
                        break;
                    default:
                        h = level * normalPerLevel;
                        break;
                }

                heights[hz, hx] = Mathf.Clamp01(h);
            }
        }

        td.SetHeights(0, 0, heights);
    }

    // ── Terraform internal ───────────────────────────────────────────────────────

    private int GetFullTileIndex(int tx, int tz)
    {
        Vector2Int fullSize = FullTileGridSize;
        if (tx < 0 || tx >= fullSize.x || tz < 0 || tz >= fullSize.y) return -1;
        return tz * fullSize.x + tx;
    }

    private void ResizeTileData()
    {
        Vector2Int fullSize = FullTileGridSize;
        int required = fullSize.x * fullSize.y;

        if (tileHeights.Length != required)
        {
            int[] newHeights = new int[required];
            for (int i = 0; i < Mathf.Min(tileHeights.Length, required); i++)
                newHeights[i] = tileHeights[i];
            tileHeights = newHeights;
        }

        if (tileShapes.Length != required)
        {
            TileShape[] newShapes = new TileShape[required];
            for (int i = 0; i < Mathf.Min(tileShapes.Length, required); i++)
                newShapes[i] = tileShapes[i];
            tileShapes = newShapes;
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

        DrawSubtileCellGizmos();
        DrawWalkableFullTileOverlays();
    }

    private void DrawSubtileCellGizmos()
    {
        int _s = SubtilesPerFullTile;
        for (int z = 0; z < gridSizeInCells.y; z++)
        {
            for (int x = 0; x < gridSizeInCells.x; x++)
            {
                float localX = gridOriginLocalOffset.x + (x + 0.5f) * femaleTileSize;
                float localZ = gridOriginLocalOffset.z + (z + 0.5f) * femaleTileSize;
                int _ftx = x / _s;  int _ftz = z / _s;
                float _hY = (GetTileShape(_ftx, _ftz) == TileShape.Flat)
                    ? GetTileHeight(_ftx, _ftz) * LevelHeight : 0f;
                Vector3 worldCenter = transform.position
                    + transform.right * localX
                    + transform.up * (gridOriginLocalOffset.y + _hY)
                    + transform.forward * localZ;
                Vector3 worldSize = new Vector3(femaleTileSize, GizmoThickness, femaleTileSize);

                Matrix4x4 prevMatrix = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(worldCenter, transform.rotation, Vector3.one);

                if (GetCell(x, z))
                {
                    GetSubtileColors(x, z, out Color fill, out Color outline);
                    Gizmos.color = fill;
                    Gizmos.DrawCube(Vector3.zero, worldSize);
                    Gizmos.color = outline;
                    Gizmos.DrawWireCube(Vector3.zero, worldSize);
                }
                else
                {
                    Gizmos.color = DisabledCellOutlineColor;
                    Gizmos.DrawWireCube(Vector3.zero, worldSize);
                }

                Gizmos.matrix = prevMatrix;
            }
        }
    }

    private void DrawWalkableFullTileOverlays()
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
                float _hY = GetTileHeight(tx, tz) * LevelHeight;
                Vector3 worldCenter = transform.position
                    + transform.right * localX
                    + transform.up * (gridOriginLocalOffset.y + _hY + FullTileGizmoThickness * 0.5f)
                    + transform.forward * localZ;
                Vector3 worldSize = new Vector3(FullTileWorldSize, FullTileGizmoThickness, FullTileWorldSize);

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
}

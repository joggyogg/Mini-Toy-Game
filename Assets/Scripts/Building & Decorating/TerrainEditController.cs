using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Runtime controller for the terraform tool. Owns Raise and Lower operations
/// on the TerrainGridAuthoring corner-height data layer. After each mutation it
/// syncs the enabled-cell mask and bakes heights into the Unity Terrain.
///
/// Wire up in the Inspector: assign TerrainGridAuthoring and the Unity Terrain.
/// </summary>
public class TerrainEditController : MonoBehaviour
{
    [SerializeField] private TerrainGridAuthoring terrainGrid;
    [SerializeField] private PlayerMotor          playerMotor;

    // All child terrains discovered at Awake from the terrainGrid parent.
    private Terrain[] childTerrains;

    // Snapshots of the original heightmaps taken at Awake so we can restore them
    // when play mode ends. TerrainData.SetHeights() modifies the asset on disk
    // directly; without this the changes would be permanent after stopping play.
    private Dictionary<Terrain, float[,]> originalHeights;

    private void Awake()
    {
        childTerrains = terrainGrid != null
            ? terrainGrid.GetComponentsInChildren<Terrain>()
            : System.Array.Empty<Terrain>();

        originalHeights = new Dictionary<Terrain, float[,]>(childTerrains.Length);
        foreach (Terrain t in childTerrains)
        {
            if (t == null || t.terrainData == null) continue;
            int res = t.terrainData.heightmapResolution;
            originalHeights[t] = t.terrainData.GetHeights(0, 0, res, res);
        }

#if UNITY_EDITOR
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
        RestoreOriginalHeights();
    }

#if UNITY_EDITOR
    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
            RestoreOriginalHeights();
    }
#endif

    private void RestoreOriginalHeights()
    {
        if (originalHeights == null) return;
        foreach (var kvp in originalHeights)
        {
            if (kvp.Key != null && kvp.Key.terrainData != null)
                kvp.Key.terrainData.SetHeights(0, 0, kvp.Value);
        }
        originalHeights = null;
    }

    // ── Public API ────────────────────────────────────────────────────────────────

    public void EnterTerraformMode() { }
    public void ExitTerraformMode() { }

    /// <summary>
    /// Raise the full tile at (tx,tz) by one level: increments all 4 corners by 1.
    /// Shared corners with adjacent tiles mean neighbours get slopes automatically.
    /// </summary>
    public void RaiseTile(int tx, int tz)
    {
        if (terrainGrid == null) return;
        AdjustTileCorners(tx, tz, +1);
        ApplyAndRefresh(DirtyRect(tx, tz));
    }

    /// <summary>
    /// Lower the full tile at (tx,tz) by one level: decrements all 4 corners by 1 (min 0).
    /// </summary>
    public void LowerTile(int tx, int tz)
    {
        if (terrainGrid == null) return;
        AdjustTileCorners(tx, tz, -1);
        ApplyAndRefresh(DirtyRect(tx, tz));
    }

    /// <summary>
    /// Flatten the full tile at (tx,tz) so all 4 corners equal targetHeight.
    /// </summary>
    public void FlattenTile(int tx, int tz, int targetHeight)
    {
        if (terrainGrid == null) return;
        int h = Mathf.Max(0, targetHeight);
        terrainGrid.SetCornerHeight(tx,     tz,     h);
        terrainGrid.SetCornerHeight(tx + 1, tz,     h);
        terrainGrid.SetCornerHeight(tx,     tz + 1, h);
        terrainGrid.SetCornerHeight(tx + 1, tz + 1, h);
        ApplyAndRefresh(DirtyRect(tx, tz));
    }

    // ── Brush-based batch operations ─────────────────────────────────────────────

    /// <summary>
    /// Raise all non-edge full tiles whose world center falls within <paramref name="radius"/>
    /// of <paramref name="worldCenter"/>. Applies terrain bake once for the whole batch.
    /// </summary>
    public void RaiseTilesInRadius(Vector3 worldCenter, float radius, int strength = 1)
    {
        EditTilesInRadius(worldCenter, radius, EditMode.Raise, 0, strength);
    }

    /// <summary>
    /// Lower all non-edge full tiles whose world center falls within <paramref name="radius"/>
    /// of <paramref name="worldCenter"/>. Applies terrain bake once for the whole batch.
    /// </summary>
    public void LowerTilesInRadius(Vector3 worldCenter, float radius, int strength = 1)
    {
        EditTilesInRadius(worldCenter, radius, EditMode.Lower, 0, strength);
    }

    /// <summary>
    /// Flatten all non-edge full tiles whose world center falls within <paramref name="radius"/>
    /// of <paramref name="worldCenter"/> to <paramref name="targetHeight"/>.
    /// Applies terrain bake once for the whole batch.
    /// </summary>
    public void FlattenTilesInRadius(Vector3 worldCenter, float radius, int targetHeight)
    {
        EditTilesInRadius(worldCenter, radius, EditMode.Flatten, targetHeight);
    }

    /// <summary>
    /// Smooth all corners within <paramref name="radius"/> of <paramref name="worldCenter"/>.
    /// Each corner moves toward the average of itself and its cardinal neighbours.
    /// </summary>
    public void SmoothTilesInRadius(Vector3 worldCenter, float radius)
    {
        if (terrainGrid == null) return;

        float radiusSq = radius * radius;
        Vector2Int fullSize = terrainGrid.FullTileGridSize;
        int cornerW = fullSize.x + 1;
        int cornerH = fullSize.y + 1;

        if (!terrainGrid.TryWorldToFullTile(worldCenter, out Vector2Int centerTile)) return;
        if (!terrainGrid.TryGetFullTileCenterWorld(centerTile.x, centerTile.y, out Vector3 tileCenterWorld)) return;

        int range = Mathf.CeilToInt(radius) + 2;
        int minCx = Mathf.Max(1, centerTile.x - range);
        int maxCx = Mathf.Min(cornerW - 2, centerTile.x + range + 1);
        int minCz = Mathf.Max(1, centerTile.y - range);
        int maxCz = Mathf.Min(cornerH - 2, centerTile.y + range + 1);

        // Padded snapshot so neighbour reads never go out of bounds.
        int padMinCx = Mathf.Max(0, minCx - 1);
        int padMaxCx = Mathf.Min(cornerW - 1, maxCx + 1);
        int padMinCz = Mathf.Max(0, minCz - 1);
        int padMaxCz = Mathf.Min(cornerH - 1, maxCz + 1);
        int snapW = padMaxCx - padMinCx + 1;
        int snapH = padMaxCz - padMinCz + 1;
        int[,] snap = new int[snapW, snapH];
        for (int sz = 0; sz < snapH; sz++)
            for (int sx = 0; sx < snapW; sx++)
                snap[sx, sz] = terrainGrid.GetCornerHeight(padMinCx + sx, padMinCz + sz);

        bool edited = false;
        for (int cz = minCz; cz <= maxCz; cz++)
        {
            for (int cx = minCx; cx <= maxCx; cx++)
            {
                // Corner world position (corner cx,cz is the SW corner of tile cx,cz).
                float wx = tileCenterWorld.x + (cx - centerTile.x - 0.5f);
                float wz = tileCenterWorld.z + (cz - centerTile.y - 0.5f);
                float dx = wx - worldCenter.x;
                float dz = wz - worldCenter.z;
                if (dx * dx + dz * dz > radiusSq) continue;

                int si = cx - padMinCx;
                int sj = cz - padMinCz;
                int sum = snap[si, sj];
                int count = 1;
                if (si > 0)         { sum += snap[si - 1, sj]; count++; }
                if (si < snapW - 1) { sum += snap[si + 1, sj]; count++; }
                if (sj > 0)         { sum += snap[si, sj - 1]; count++; }
                if (sj < snapH - 1) { sum += snap[si, sj + 1]; count++; }

                int avg = Mathf.RoundToInt((float)sum / count);
                terrainGrid.SetCornerHeight(cx, cz, Mathf.Max(0, avg));
                edited = true;
            }
        }

        if (edited)
        {
            RectInt dirty = new RectInt(minCx - 2, minCz - 2, (maxCx - minCx) + 5, (maxCz - minCz) + 5);
            ApplyAndRefresh(dirty);
        }
    }

    /// <summary>Returns the height level of the full tile at the given world position, or 0 if outside.</summary>
    public int SampleTileLevel(Vector3 worldPosition)
    {
        if (terrainGrid == null) return 0;
        if (!terrainGrid.TryWorldToFullTile(worldPosition, out Vector2Int tile)) return 0;
        return terrainGrid.GetTileLevel(tile.x, tile.y);
    }

    private enum EditMode { Raise, Lower, Flatten }

    private void EditTilesInRadius(Vector3 worldCenter, float radius, EditMode mode, int flattenHeight, int strength = 1)
    {
        if (terrainGrid == null) return;

        float radiusSq = radius * radius;
        Vector2Int fullSize = terrainGrid.FullTileGridSize;

        // Find the tile range that could be within radius.
        if (!terrainGrid.TryWorldToFullTile(worldCenter, out Vector2Int centerTile))
            return;

        // Conservative tile search range based on radius (tiles are 1 world unit).
        int range = Mathf.CeilToInt(radius) + 1;
        int minTx = Mathf.Max(0, centerTile.x - range);
        int maxTx = Mathf.Min(fullSize.x - 1, centerTile.x + range);
        int minTz = Mathf.Max(0, centerTile.y - range);
        int maxTz = Mathf.Min(fullSize.y - 1, centerTile.y + range);

        int edited = 0;

        for (int tz = minTz; tz <= maxTz; tz++)
        {
            for (int tx = minTx; tx <= maxTx; tx++)
            {
                if (terrainGrid.IsFullTileEdge(tx, tz)) continue;

                if (!terrainGrid.TryGetFullTileCenterWorld(tx, tz, out Vector3 tileCenter))
                    continue;

                // Distance check on XZ plane only.
                float dx = tileCenter.x - worldCenter.x;
                float dz = tileCenter.z - worldCenter.z;
                if (dx * dx + dz * dz > radiusSq) continue;

                switch (mode)
                {
                    case EditMode.Raise:
                        AdjustTileCorners(tx, tz, +strength);
                        break;
                    case EditMode.Lower:
                        AdjustTileCorners(tx, tz, -strength);
                        break;
                    case EditMode.Flatten:
                        int h = Mathf.Max(0, flattenHeight);
                        terrainGrid.SetCornerHeight(tx,     tz,     h);
                        terrainGrid.SetCornerHeight(tx + 1, tz,     h);
                        terrainGrid.SetCornerHeight(tx,     tz + 1, h);
                        terrainGrid.SetCornerHeight(tx + 1, tz + 1, h);
                        break;
                }
                edited++;
            }
        }

        if (edited > 0)
        {
            // Single merged dirty rect covering the whole brush area + neighbour margin.
            RectInt dirty = new RectInt(minTx - 1, minTz - 1, (maxTx - minTx) + 3, (maxTz - minTz) + 3);
            ApplyAndRefresh(dirty);
        }
    }

    // ── Internal ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a RectInt covering the edited tile plus its 3×3 neighbourhood
    /// (because shared corners affect neighbour shapes/slopes).
    /// </summary>
    private static RectInt DirtyRect(int tx, int tz)
    {
        return new RectInt(tx - 1, tz - 1, 3, 3);
    }

    private void AdjustTileCorners(int tx, int tz, int delta)
    {
        // SW
        int h0 = terrainGrid.GetCornerHeight(tx, tz);
        terrainGrid.SetCornerHeight(tx, tz, Mathf.Max(0, h0 + delta));
        // SE
        int h1 = terrainGrid.GetCornerHeight(tx + 1, tz);
        terrainGrid.SetCornerHeight(tx + 1, tz, Mathf.Max(0, h1 + delta));
        // NW
        int h2 = terrainGrid.GetCornerHeight(tx, tz + 1);
        terrainGrid.SetCornerHeight(tx, tz + 1, Mathf.Max(0, h2 + delta));
        // NE
        int h3 = terrainGrid.GetCornerHeight(tx + 1, tz + 1);
        terrainGrid.SetCornerHeight(tx + 1, tz + 1, Mathf.Max(0, h3 + delta));
    }

    private void ApplyAndRefresh(RectInt dirtyTiles)
    {
        terrainGrid.SyncEnabledCellsFromShapes(dirtyTiles);
        foreach (Terrain t in childTerrains)
        {
            if (t != null) terrainGrid.ApplyToTerrainPartial(t, dirtyTiles);
        }
    }
}

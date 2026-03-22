using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Runtime controller for the terraform tool. Owns Raise, Dig, and Slope operations
/// on the TerrainGridAuthoring data layer. After each mutation it syncs the enabled-cell
/// mask, bakes heights into the Unity Terrain, and tells the minimap to redraw.
///
/// Wire up in the Inspector: assign TerrainGridAuthoring, the Unity Terrain, and the
/// TerraformMinimapUI panel. Call EnterTerraformMode / ExitTerraformMode from a HUD button.
/// </summary>
public class TerrainEditController : MonoBehaviour
{
    [SerializeField] private TerrainGridAuthoring terrainGrid;
    [SerializeField] private TerraformMinimapUI   minimapUI;
    [SerializeField] private PlayerMotor          playerMotor;
    // Optional: if DecorateMinimapUI is also open it will stay in sync with terrain edits.
    [SerializeField] private DecorateMinimapUI    decorateMinimap;

    // Snapshot of the original heightmaps taken at Awake so we can restore them
    // when play mode ends. TerrainData.SetHeights() modifies the asset on disk
    // directly; without this the changes would be permanent after stopping play.
    private Dictionary<Terrain, float[,]> originalHeightsMap = new Dictionary<Terrain, float[,]>();

    private bool HasSections => terrainGrid != null && terrainGrid.TerrainSections.Count > 0;

    private void Awake()
    {
        if (HasSections)
        {
            foreach (var section in terrainGrid.TerrainSections)
            {
                if (section == null || section.terrain == null || section.terrain.terrainData == null) continue;
                TerrainData td = section.terrain.terrainData;
                int res = td.heightmapResolution;
                originalHeightsMap[section.terrain] = td.GetHeights(0, 0, res, res);
            }
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
        // Fallback restore for non-editor (built game) or if the editor hook didn't fire.
        RestoreOriginalHeights();
    }

#if UNITY_EDITOR
    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        // ExitingPlayMode fires while the scene is still fully alive — all references
        // are valid here, unlike OnDestroy which fires during teardown.
        if (state == PlayModeStateChange.ExitingPlayMode)
            RestoreOriginalHeights();
    }
#endif

    private void RestoreOriginalHeights()
    {
        if (originalHeightsMap.Count == 0) return;
        foreach (var kvp in originalHeightsMap)
        {
            if (kvp.Key != null && kvp.Key.terrainData != null)
                kvp.Key.terrainData.SetHeights(0, 0, kvp.Value);
        }
        originalHeightsMap.Clear(); // prevent double-restore from both hook and OnDestroy
    }

    // ── Public API ────────────────────────────────────────────────────────────────

    public void EnterTerraformMode()
    {
        if (minimapUI == null) return;
        minimapUI.gameObject.SetActive(true);
        minimapUI.Initialise(terrainGrid, null, this, motor: playerMotor);
    }

    public void ExitTerraformMode()
    {
        if (minimapUI != null) minimapUI.gameObject.SetActive(false);
    }

    /// <summary>Raise the full tile at (tx,tz) by one level (0.5 world units).
    /// If the tile is a slope, erase it and raise from its floor level.</summary>
    public void RaiseTile(int tx, int tz)
    {
        if (terrainGrid == null) return;
        int current = terrainGrid.GetTileHeight(tx, tz);
        if (terrainGrid.GetTileShape(tx, tz) != TileShape.Flat)
        {
            // Erase slope; height is already the low edge, so +1 = one level above that floor.
            terrainGrid.SetTileShape(tx, tz, TileShape.Flat);
            terrainGrid.SetTileHeight(tx, tz, current + 1);
        }
        else
        {
            terrainGrid.SetTileHeight(tx, tz, current + 1);
        }
        var dirty = CascadeSlopeRefresh(tx, tz);
        ApplyAndRefresh(dirty);
    }

    /// <summary>Lower the full tile at (tx,tz) by one level, minimum 0.
    /// If the tile is a slope, erase it and leave height at its floor level (no further dig).</summary>
    public void LowerTile(int tx, int tz)
    {
        if (terrainGrid == null) return;
        int current = terrainGrid.GetTileHeight(tx, tz);
        if (terrainGrid.GetTileShape(tx, tz) != TileShape.Flat)
        {
            // First dig just erases the slope; height stays at the stored low edge.
            terrainGrid.SetTileShape(tx, tz, TileShape.Flat);
        }
        else
        {
            terrainGrid.SetTileHeight(tx, tz, Mathf.Max(0, current - 1));
        }
        var dirty = CascadeSlopeRefresh(tx, tz);
        ApplyAndRefresh(dirty);
    }

    /// <summary>
    /// BFS cascade: re-evaluates the changed tile (if non-flat) plus all neighbour slope tiles.
    /// Spreads outward only when a tile actually changes shape or height, so it stops
    /// as soon as the grid has stabilised — no unnecessary work beyond the affected region.
    /// Returns the bounding RectInt of all tiles that were dirtied (origin ± neighbours).
    /// </summary>
    private RectInt CascadeSlopeRefresh(int originX, int originZ)
    {
        // Start dirty bounds at origin ± 1 — the origin height/shape changed and its
        // immediate neighbours are directly affected even if cascade doesn't spread further.
        int minTx = originX - 1, minTz = originZ - 1;
        int maxTx = originX + 1, maxTz = originZ + 1;

        var queue   = new Queue<Vector2Int>();
        var inQueue = new HashSet<Vector2Int>();

        // Seed: the 3x3 block around the changed origin (non-flat tiles only).
        for (int dz = -1; dz <= 1; dz++)
        for (int dx = -1; dx <= 1; dx++)
        {
            int nx = originX + dx, nz = originZ + dz;
            if (terrainGrid.GetTileShape(nx, nz) == TileShape.Flat) continue;
            var p = new Vector2Int(nx, nz);
            queue.Enqueue(p); inQueue.Add(p);
        }

        while (queue.Count > 0)
        {
            var pos = queue.Dequeue();
            inQueue.Remove(pos);
            int tx = pos.x, tz = pos.y;

            TileShape oldShape  = terrainGrid.GetTileShape(tx, tz);
            int       oldHeight = terrainGrid.GetTileHeight(tx, tz);

            ApplySlopeShape(tx, tz);

            // Only spread if something actually changed.
            if (terrainGrid.GetTileShape(tx, tz) == oldShape &&
                terrainGrid.GetTileHeight(tx, tz) == oldHeight) continue;

            // Expand dirty bounds to cover this changed tile and its neighbours.
            if (tx - 1 < minTx) minTx = tx - 1;
            if (tz - 1 < minTz) minTz = tz - 1;
            if (tx + 1 > maxTx) maxTx = tx + 1;
            if (tz + 1 > maxTz) maxTz = tz + 1;

            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0) continue;
                int nnx = tx + dx, nnz = tz + dz;
                if (terrainGrid.GetTileShape(nnx, nnz) == TileShape.Flat) continue;
                var np = new Vector2Int(nnx, nnz);
                if (!inQueue.Contains(np)) { queue.Enqueue(np); inQueue.Add(np); }
            }
        }

        return new RectInt(minTx, minTz, maxTx - minTx + 1, maxTz - minTz + 1);
    }

    /// <summary>
    /// Paint a slope on (tx,tz). Requires at least one neighbour that is either a slope
    /// tile or a flat tile raised above this tile — returns false (no-op) if none exist.
    /// </summary>
    public bool TryPaintSlope(int tx, int tz)
    {
        if (terrainGrid == null) return false;

        // Require at least one neighbour that is either a slope tile or a flat tile
        // raised above this tile's level. Lone slopes with no context are not allowed.
        int myLevel = terrainGrid.GetTileHeight(tx, tz);
        bool hasContext = false;
        for (int dz = -1; dz <= 1 && !hasContext; dz++)
        for (int dx = -1; dx <= 1 && !hasContext; dx++)
        {
            if (dx == 0 && dz == 0) continue;
            int nx = tx + dx, nz = tz + dz;
            TileShape ns = terrainGrid.GetTileShape(nx, nz);
            if (ns != TileShape.Flat) { hasContext = true; break; }
            if (terrainGrid.GetTileHeight(nx, nz) > myLevel) { hasContext = true; break; }
        }
        if (!hasContext) return false;

        ApplySlopeShape(tx, tz);

        // Cascade: spreads outward until no further tiles change.
        var dirty = CascadeSlopeRefresh(tx, tz);
        ApplyAndRefresh(dirty);
        return true;
    }

    /// <summary>Remove slope — revert to Flat at the tile's current height level.</summary>
    public void ClearSlope(int tx, int tz)
    {
        if (terrainGrid == null) return;
        terrainGrid.SetTileShape(tx, tz, TileShape.Flat);

        // Cascade to update neighbours that were leaning on this slope.
        var dirty = CascadeSlopeRefresh(tx, tz);
        ApplyAndRefresh(dirty);
    }

    // ── Internal ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the height of the face of tile (tx,tz) that is visible from the asking tile.
    /// faceDx,faceDz = direction from (tx,tz) toward the asking tile (which face we're looking at).
    /// Only returns h+1 when that specific face is actually a high edge of the shape.
    /// </summary>
    private int GetEffectiveTopHeight(int tx, int tz, int faceDx, int faceDz)
    {
        TileShape shape = terrainGrid.GetTileShape(tx, tz);
        int h = terrainGrid.GetTileHeight(tx, tz);
        if (shape == TileShape.Flat) return h;
        // Only a HIGH face counts as a directional source.
        // A non-high (low) face returns -1 so it is never > myLevel (which is always >= 0),
        // preventing a slope tile's floor height from triggering wrong directions on neighbours.
        return IsHighFace(shape, faceDx, faceDz) ? h + 1 : -1;
    }

    /// <summary>
    /// Returns true if the face of this shape in direction (dx,dz) is a high edge.
    /// dx,dz: direction from the tile toward the observer (i.e. which face the observer sees).
    /// </summary>
    private static bool IsHighFace(TileShape shape, int dx, int dz)
    {
        // Diagonal face: high if either cardinal component is high
        if (dx != 0 && dz != 0)
            return IsHighFace(shape, dx, 0) || IsHighFace(shape, 0, dz);
        switch (shape)
        {
            case TileShape.SlopeNorth:     return dz == +1;
            case TileShape.SlopeSouth:     return dz == -1;
            case TileShape.SlopeEast:      return dx == +1;
            case TileShape.SlopeWest:      return dx == -1;
            case TileShape.SlopeNorthEast: return dz == +1 || dx == +1;
            case TileShape.SlopeNorthWest: return dz == +1 || dx == -1;
            case TileShape.SlopeSouthEast: return dz == -1 || dx == +1;
            case TileShape.SlopeSouthWest: return dz == -1 || dx == -1;
            case TileShape.SlopeNS:        return dz != 0;
            case TileShape.SlopeEW:        return dx != 0;
            case TileShape.SlopeNSE:       return dz != 0 || dx == +1;
            case TileShape.SlopeNSW:       return dz != 0 || dx == -1;
            case TileShape.SlopeNEW:       return dz == +1 || dx != 0;
            case TileShape.SlopeSEW:       return dz == -1 || dx != 0;
            case TileShape.SlopePyramid:   return true;   // all edges high
            case TileShape.PyramidUp:      return false;  // all edges low (centre is high)
            // Corner shapes have only a single elevated corner — no full edge is uniformly high.
            case TileShape.CornerNE:
            case TileShape.CornerNW:
            case TileShape.CornerSE:
            case TileShape.CornerSW:       return false;
            default:                       return false;  // Flat
        }
    }

    /// <summary>
    /// Sets the slope shape (and stored low-edge height) for (tx,tz) based on its
    /// current neighbours, using a strict priority hierarchy:
    ///   1. Cardinal FLAT tiles higher than me  (strongest)
    ///   2. Cardinal SLOPE tiles whose high face points at me
    ///   3. Diagonal FLAT tiles higher than me  (weakest — corner fill only)
    /// Only the highest tier that produces any signal is used; lower tiers are ignored.
    /// No signal at all → standalone PyramidUp.
    /// </summary>
    private void ApplySlopeShape(int tx, int tz)
    {
        int myLevel = terrainGrid.GetTileHeight(tx, tz);

        // ── Tier 1: cardinal FLAT neighbours ──────────────────────────────────
        int[] cdx = {  0,  0, 1, -1 };
        int[] cdz = {  1, -1, 0,  0 };
        // face toward us: N-nbr's south face = (0,-1), etc.
        int[] fdx = {  0,  0, -1, +1 };
        int[] fdz = { -1, +1,  0,  0 };

        bool t1N = false, t1S = false, t1E = false, t1W = false;
        int best1 = myLevel;
        {
            // N
            int nx = tx, nz = tz + 1;
            if (terrainGrid.GetTileShape(nx, nz) == TileShape.Flat && terrainGrid.GetTileHeight(nx, nz) > myLevel)
            { t1N = true; best1 = Mathf.Max(best1, terrainGrid.GetTileHeight(nx, nz)); }
            // S
            nx = tx; nz = tz - 1;
            if (terrainGrid.GetTileShape(nx, nz) == TileShape.Flat && terrainGrid.GetTileHeight(nx, nz) > myLevel)
            { t1S = true; best1 = Mathf.Max(best1, terrainGrid.GetTileHeight(nx, nz)); }
            // E
            nx = tx + 1; nz = tz;
            if (terrainGrid.GetTileShape(nx, nz) == TileShape.Flat && terrainGrid.GetTileHeight(nx, nz) > myLevel)
            { t1E = true; best1 = Mathf.Max(best1, terrainGrid.GetTileHeight(nx, nz)); }
            // W
            nx = tx - 1; nz = tz;
            if (terrainGrid.GetTileShape(nx, nz) == TileShape.Flat && terrainGrid.GetTileHeight(nx, nz) > myLevel)
            { t1W = true; best1 = Mathf.Max(best1, terrainGrid.GetTileHeight(nx, nz)); }
        }
        bool anyT1 = t1N || t1S || t1E || t1W;

        // ── Tier 2: cardinal SLOPE neighbours whose stored height (floor) > myLevel ──
        // This catches outer-ring slopes that are adjacent to inner-ring slopes
        // elevated above them, even when the inner slope's low face points outward.
        bool t2N = false, t2S = false, t2E = false, t2W = false;
        int best2 = myLevel;      // max nh among T2 sources
        int min2  = int.MaxValue; // min nh among T2 sources
        if (!anyT1)
        {
            int nh;
            // N
            nh = terrainGrid.GetTileHeight(tx, tz + 1);
            if (terrainGrid.GetTileShape(tx, tz + 1) != TileShape.Flat && nh > myLevel)
            { t2N = true; best2 = Mathf.Max(best2, nh); min2 = Mathf.Min(min2, nh); }
            // S
            nh = terrainGrid.GetTileHeight(tx, tz - 1);
            if (terrainGrid.GetTileShape(tx, tz - 1) != TileShape.Flat && nh > myLevel)
            { t2S = true; best2 = Mathf.Max(best2, nh); min2 = Mathf.Min(min2, nh); }
            // E
            nh = terrainGrid.GetTileHeight(tx + 1, tz);
            if (terrainGrid.GetTileShape(tx + 1, tz) != TileShape.Flat && nh > myLevel)
            { t2E = true; best2 = Mathf.Max(best2, nh); min2 = Mathf.Min(min2, nh); }
            // W
            nh = terrainGrid.GetTileHeight(tx - 1, tz);
            if (terrainGrid.GetTileShape(tx - 1, tz) != TileShape.Flat && nh > myLevel)
            { t2W = true; best2 = Mathf.Max(best2, nh); min2 = Mathf.Min(min2, nh); }
        }
        bool anyT2 = t2N || t2S || t2E || t2W;

        // ── Tier C: combined corner fill ──────────────────────────────────────
        // Fires when T1+T2 are empty. Merges two sources:
        //  (a) cardinal SLOPE neighbours whose storedHeight == myLevel (outer-ring joins)
        //  (b) diagonal FLAT neighbours at level > myLevel (inner-ring joins)
        // Both sets contribute N/S/E/W flags simultaneously so a corner tile that has
        // one same-level slope neighbour and one diagonal flat source still gets
        // highCount==2 and emits the correct CornerXX shape at any build order.
        bool tcN = false, tcS = false, tcE = false, tcW = false;
        int bestC = myLevel + 1; // default: storedHeight stays at myLevel
        if (!anyT1 && !anyT2)
        {
            // (a) cardinal same-level slopes
            if (terrainGrid.GetTileShape(tx, tz + 1) != TileShape.Flat && terrainGrid.GetTileHeight(tx, tz + 1) == myLevel) tcN = true;
            if (terrainGrid.GetTileShape(tx, tz - 1) != TileShape.Flat && terrainGrid.GetTileHeight(tx, tz - 1) == myLevel) tcS = true;
            if (terrainGrid.GetTileShape(tx + 1, tz) != TileShape.Flat && terrainGrid.GetTileHeight(tx + 1, tz) == myLevel) tcE = true;
            if (terrainGrid.GetTileShape(tx - 1, tz) != TileShape.Flat && terrainGrid.GetTileHeight(tx - 1, tz) == myLevel) tcW = true;
            // (b) diagonal flat tiles
            int[] ddx2 = { +1, -1, +1, -1 };
            int[] ddz2 = { +1, +1, -1, -1 };
            for (int i = 0; i < 4; i++)
            {
                int nx = tx + ddx2[i], nz = tz + ddz2[i];
                if (terrainGrid.GetTileShape(nx, nz) != TileShape.Flat) continue;
                int nLevel = terrainGrid.GetTileHeight(nx, nz);
                if (nLevel <= myLevel) continue;
                if (ddz2[i] > 0) tcN = true;
                if (ddz2[i] < 0) tcS = true;
                if (ddx2[i] > 0) tcE = true;
                if (ddx2[i] < 0) tcW = true;
                bestC = Mathf.Max(bestC, nLevel); // diagonal flat drives stored height
            }
        }
        bool anyTC = tcN || tcS || tcE || tcW;

        // ── Resolve into final high flags and best level ───────────────────────
        bool highN, highS, highE, highW;
        int best;
        if (anyT1)
        {
            highN = t1N; highS = t1S; highE = t1E; highW = t1W;
            best = best1;
        }
        else if (anyT2)
        {
            highN = t2N; highS = t2S; highE = t2E; highW = t2W;
            // 2+ sources at same level → corner fill: match their level (storedHeight = min_nh)
            // 1 source → step down from it (storedHeight = nh - 1)
            int t2Count = (t2N?1:0) + (t2S?1:0) + (t2E?1:0) + (t2W?1:0);
            best = (t2Count >= 2) ? (min2 + 1) : best2;
        }
        else if (anyTC)
        {
            // Use the highest diagonal neighbour to determine direction.
            // This correctly handles two adjacent pyramids: each corner tile picks the
            // direction toward its OWN pyramid's inner ring (which is the highest diagonal),
            // even when the other pyramid's outer corner also fires a Tc flag sideways.
            int dNE = terrainGrid.GetTileHeight(tx + 1, tz + 1);
            int dNW = terrainGrid.GetTileHeight(tx - 1, tz + 1);
            int dSE = terrainGrid.GetTileHeight(tx + 1, tz - 1);
            int dSW = terrainGrid.GetTileHeight(tx - 1, tz - 1);
            int diagMax = Mathf.Max(dNE, Mathf.Max(dNW, Mathf.Max(dSE, dSW)));

            if (diagMax > myLevel)
            {
                // Only commit if there is a UNIQUE highest diagonal — no ambiguity.
                TileShape diagShape = TileShape.Flat; // Flat used as "no unique winner"
                if      (dSW == diagMax && dSW > dSE && dSW > dNW && dSW > dNE) diagShape = TileShape.CornerSW;
                else if (dSE == diagMax && dSE > dSW && dSE > dNW && dSE > dNE) diagShape = TileShape.CornerSE;
                else if (dNW == diagMax && dNW > dSW && dNW > dSE && dNW > dNE) diagShape = TileShape.CornerNW;
                else if (dNE == diagMax && dNE > dSW && dNE > dSE && dNE > dNW) diagShape = TileShape.CornerNE;

                if (diagShape != TileShape.Flat)
                {
                    // storedHeight stays at myLevel — Tc never changes the floor level.
                    terrainGrid.SetTileShape(tx, tz, diagShape);
                    return;
                }
            }

            // Tie or no higher diagonal — fall back to flag-based corner logic below.
            highN = tcN; highS = tcS; highE = tcE; highW = tcW;
            best = bestC;
        }
        else
        {
            // No signal at all — standalone convex peak.
            terrainGrid.SetTileShape(tx, tz, TileShape.PyramidUp);
            return;
        }

        int highCount = (highN?1:0) + (highS?1:0) + (highE?1:0) + (highW?1:0);

        TileShape shape;
        // Tier C can ONLY produce a Corner shape: exactly 2 flags that are non-opposite.
        // Anything else (≤1 flag, 3-4 flags, or opposite pair N+S / E+W) means there is
        // not enough coherent directional context from same-level slopes/diagonals alone.
        // Fall back to a standalone peak rather than forcing a wrong cardinal shape.
        if (anyTC && !anyT1 && !anyT2)
        {
            bool isValidCorner = highCount == 2 && !((highN && highS) || (highE && highW));
            if (!isValidCorner)
            {
                terrainGrid.SetTileShape(tx, tz, TileShape.PyramidUp);
                return;
            }
            if      (highN && highE) shape = TileShape.CornerNE;
            else if (highN && highW) shape = TileShape.CornerNW;
            else if (highS && highE) shape = TileShape.CornerSE;
            else                     shape = TileShape.CornerSW;
        }
        else
        {
            if      (highCount >= 4)           shape = TileShape.SlopePyramid;
            else if (highCount == 3 && !highW) shape = TileShape.SlopeNSE;
            else if (highCount == 3 && !highE) shape = TileShape.SlopeNSW;
            else if (highCount == 3 && !highS) shape = TileShape.SlopeNEW;
            else if (highCount == 3 && !highN) shape = TileShape.SlopeSEW;
            else if (highN && highS)           shape = TileShape.SlopeNS;
            else if (highE && highW)           shape = TileShape.SlopeEW;
            else if (highN && highE)           shape = TileShape.SlopeNorthEast;
            else if (highN && highW)           shape = TileShape.SlopeNorthWest;
            else if (highS && highE)           shape = TileShape.SlopeSouthEast;
            else if (highS && highW)           shape = TileShape.SlopeSouthWest;
            else if (highN)  shape = TileShape.SlopeNorth;
            else if (highS)  shape = TileShape.SlopeSouth;
            else if (highE)  shape = TileShape.SlopeEast;
            else             shape = TileShape.SlopeWest;
        }

        terrainGrid.SetTileHeight(tx, tz, best - 1);
        terrainGrid.SetTileShape(tx, tz, shape);
    }

    private void ApplyAndRefresh(RectInt dirtyTiles)
    {
        terrainGrid.SyncEnabledCellsFromShapes();
        if (HasSections) terrainGrid.ApplyToTerrainsRect(dirtyTiles);
        if (minimapUI != null) minimapUI.Rebuild();
        if (decorateMinimap != null && decorateMinimap.gameObject.activeInHierarchy) decorateMinimap.Rebuild();
    }

    private void ApplyAndRefresh()
    {
        terrainGrid.SyncEnabledCellsFromShapes();
        if (HasSections) terrainGrid.ApplyToTerrains();
        if (minimapUI != null) minimapUI.Rebuild();
        if (decorateMinimap != null && decorateMinimap.gameObject.activeInHierarchy) decorateMinimap.Rebuild();
    }
}

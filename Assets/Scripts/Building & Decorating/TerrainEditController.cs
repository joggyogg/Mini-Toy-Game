using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Runtime controller for the terraform tool. Owns Raise and Lower operations
/// on the TerrainGridAuthoring corner-height data layer. After each mutation it
/// syncs the enabled-cell mask, bakes heights into the Unity Terrain, and tells
/// the minimap to redraw.
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

    public void EnterTerraformMode()
    {
        if (minimapUI == null) return;
        minimapUI.gameObject.SetActive(true);
        minimapUI.Initialise(terrainGrid, this, motor: playerMotor);
    }

    public void ExitTerraformMode()
    {
        if (minimapUI != null) minimapUI.gameObject.SetActive(false);
    }

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
        if (minimapUI != null) minimapUI.Rebuild();
        if (decorateMinimap != null && decorateMinimap.gameObject.activeInHierarchy) decorateMinimap.Rebuild();
    }
}

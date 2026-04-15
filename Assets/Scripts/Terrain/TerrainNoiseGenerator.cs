using System;
using UnityEngine;

/// <summary>
/// Generates multi-octave Perlin noise terrain on a TerrainGridAuthoring grid.
/// Heights are eroded so no two adjacent tiles differ by more than one level,
/// guaranteeing that every height transition can be represented with a slope tile
/// and no vertical cliffs are produced.
///
/// Attach to the same GameObject as TerrainGridAuthoring (or any GameObject — assign
/// the reference manually). Call Generate() at runtime or use the Inspector button.
/// </summary>
[RequireComponent(typeof(TerrainGridAuthoring))]
public class TerrainNoiseGenerator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TerrainGridAuthoring terrainGrid;
    [SerializeField] private Terrain[] terrains;

    [Header("Noise")]
    [SerializeField] private int     seed        = 0;
    [SerializeField] private float   scale       = 15f;
    [SerializeField] private int     octaves     = 4;
    [SerializeField] [Range(0f, 1f)]
    private float   persistence  = 0.5f;
    [SerializeField] private float   lacunarity  = 2f;
    [SerializeField] private Vector2 offset      = Vector2.zero;

    [Header("Height Range")]
    [SerializeField] private int minLevel = 0;
    [SerializeField] private int maxLevel = 6;

    // ── Public API ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs the full generation pipeline:
    ///  1. Sample multi-octave noise → raw integer levels.
    ///  2. BFS erosion  → no adjacent pair differs by more than 1 level.
    ///  3. Write heights + shapes into the grid.
    ///  4. Bake to all terrain sections.
    /// </summary>
    public void Generate()
    {
        if (terrainGrid == null)
        {
            Debug.LogError("[TerrainNoiseGenerator] TerrainGridAuthoring is not assigned.", this);
            return;
        }

        terrainGrid.EnsureValidData();
        Vector2Int fullSize = terrainGrid.FullTileGridSize;

        int w = fullSize.x;
        int h = fullSize.y;
        int[] levels = new int[w * h];

        // ── Step 1: Sample noise ──────────────────────────────────────────────────

        float safeScale = Mathf.Max(0.001f, scale);

        // Build per-octave random offsets from seed so the same seed always
        // produces the same map regardless of world position.
        var rng = new System.Random(seed);
        float[] octOffX = new float[octaves];
        float[] octOffZ = new float[octaves];
        for (int o = 0; o < octaves; o++)
        {
            octOffX[o] = (float)(rng.NextDouble() * 200000 - 100000) + offset.x;
            octOffZ[o] = (float)(rng.NextDouble() * 200000 - 100000) + offset.y;
        }

        float noiseMin = float.MaxValue;
        float noiseMax = float.MinValue;
        float[] rawNoise = new float[w * h];

        for (int tz = 0; tz < h; tz++)
        {
            for (int tx = 0; tx < w; tx++)
            {
                if (!terrainGrid.IsFullTileWalkable(tx, tz)) continue;

                float amplitude = 1f;
                float frequency = 1f;
                float value     = 0f;
                float maxAmp    = 0f;

                for (int o = 0; o < octaves; o++)
                {
                    float sampleX = (tx / safeScale) * frequency + octOffX[o];
                    float sampleZ = (tz / safeScale) * frequency + octOffZ[o];
                    value  += Mathf.PerlinNoise(sampleX, sampleZ) * amplitude;
                    maxAmp += amplitude;
                    amplitude *= persistence;
                    frequency *= lacunarity;
                }

                float n = value / maxAmp; // normalised 0..1
                rawNoise[tz * w + tx] = n;
                if (n < noiseMin) noiseMin = n;
                if (n > noiseMax) noiseMax = n;
            }
        }

        // Remap the full noise range to minLevel..maxLevel for maximum detail.
        float noiseRange = noiseMax - noiseMin;
        int   levelRange = Mathf.Max(1, maxLevel - minLevel);

        for (int tz = 0; tz < h; tz++)
        {
            for (int tx = 0; tx < w; tx++)
            {
                if (!terrainGrid.IsFullTileWalkable(tx, tz)) continue;
                float n = noiseRange > 0.0001f
                    ? (rawNoise[tz * w + tx] - noiseMin) / noiseRange
                    : 0f;
                levels[tz * w + tx] = Mathf.Clamp(
                    Mathf.RoundToInt(n * levelRange) + minLevel,
                    minLevel, maxLevel);
            }
        }

        // ── Step 2: Erosion (no-cliff guarantee) ─────────────────────────────────
        // Repeatedly sweep until no tile was reduced. Each pass clamps a tile down
        // if any cardinal neighbor is more than 1 level lower than it.
        // Converges in O(maxLevel) passes — typically 3-6 sweeps.

        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int tz = 0; tz < h; tz++)
            {
                for (int tx = 0; tx < w; tx++)
                {
                    if (!terrainGrid.IsFullTileWalkable(tx, tz)) continue;
                    int cur = levels[tz * w + tx];

                    // Check all 4 cardinal neighbours; reduce if neighbor is more than 1 below us.
                    if (CoveredLevel(levels, w, h, tx, tz - 1) < cur - 1 ||
                        CoveredLevel(levels, w, h, tx, tz + 1) < cur - 1 ||
                        CoveredLevel(levels, w, h, tx - 1, tz) < cur - 1 ||
                        CoveredLevel(levels, w, h, tx + 1, tz) < cur - 1)
                    {
                        // Clamp to the minimum neighbor + 1.
                        int minNeighbor = Mathf.Min(
                            Mathf.Min(CoveredLevel(levels, w, h, tx,     tz - 1),
                                      CoveredLevel(levels, w, h, tx,     tz + 1)),
                            Mathf.Min(CoveredLevel(levels, w, h, tx - 1, tz),
                                      CoveredLevel(levels, w, h, tx + 1, tz)));
                        levels[tz * w + tx] = minNeighbor + 1;
                        changed = true;
                    }
                }
            }
        }

        // ── Step 3: Write heights ─────────────────────────────────────────────────

        for (int tz = 0; tz < h; tz++)
            for (int tx = 0; tx < w; tx++)
                if (terrainGrid.IsFullTileWalkable(tx, tz))
                {
                    int lvl = levels[tz * w + tx];
                    terrainGrid.SetCornerHeight(tx,     tz,     lvl);
                    terrainGrid.SetCornerHeight(tx + 1, tz,     lvl);
                    terrainGrid.SetCornerHeight(tx,     tz + 1, lvl);
                    terrainGrid.SetCornerHeight(tx + 1, tz + 1, lvl);
                }

        // ── Step 4: Apply ─────────────────────────────────────────────────────────

        terrainGrid.SyncEnabledCellsFromShapes();

        if (terrains != null)
        {
            foreach (Terrain t in terrains)
                if (t != null) terrainGrid.ApplyToTerrain(t);

            TerrainWFCGenerator.StitchTerrainNeighbors(terrains);

            var splatMapper = terrainGrid.GetComponent<TerrainSplatMapper>();
            if (splatMapper != null)
                foreach (Terrain t in terrains)
                    if (t != null) splatMapper.PaintSplatmaps(t);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the level at (tx,tz) in the working array, or int.MaxValue if the
    /// tile is out of bounds or uncovered (so it never triggers a cliff reduction).
    /// </summary>
    private int CoveredLevel(int[] levels, int w, int h, int tx, int tz)
    {
        if (tx < 0 || tx >= w || tz < 0 || tz >= h) return int.MaxValue;
        if (!terrainGrid.IsFullTileWalkable(tx, tz)) return int.MaxValue;
        return levels[tz * w + tx];
    }

    /// <summary>Returns true when the neighbor at (tx,tz) is covered, at expectedLevel, and has the given shape.</summary>
    private bool ShapeEquals(int[] levels, int w, int h, int tx, int tz, TileShape shape, int expectedLevel)
    {
        if (tx < 0 || tx >= w || tz < 0 || tz >= h) return false;
        if (!terrainGrid.IsFullTileWalkable(tx, tz)) return false;
        if (levels[tz * w + tx] != expectedLevel) return false;
        return terrainGrid.GetTileShape(tx, tz) == shape;
    }

    /// <summary>Returns true when the neighbor at (tx,tz) is covered and exactly one level above myLevel.</summary>
    private bool NeighborIsHigher(int[] levels, int w, int h, int tx, int tz, int myLevel)
    {
        if (tx < 0 || tx >= w || tz < 0 || tz >= h) return false;
        if (!terrainGrid.IsFullTileWalkable(tx, tz)) return false;
        return levels[tz * w + tx] == myLevel + 1;
    }

    // ── Unity lifecycle ───────────────────────────────────────────────────────────

    private void Reset()
    {
        terrainGrid = GetComponent<TerrainGridAuthoring>();
    }
}

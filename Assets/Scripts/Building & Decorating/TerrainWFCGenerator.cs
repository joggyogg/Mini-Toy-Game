using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configuration for WFC terrain generation.
/// The base terrain is flat; all shaping comes from gradient lines.
/// </summary>
[System.Serializable]
public struct TerrainWFCConfig
{
    [Tooltip("Random seed for offsetting the Perlin sample position.")]
    public int seed;

    [Tooltip("Height buffer: the lowest terrain point will be offset to this height (in tiles).")]
    public int heightBuffer;

    [Tooltip("Number of smoothing passes after WFC constraint propagation (0 = off).")]
    [Range(0, 10)]
    public int smoothingPasses;

    [Tooltip("Blend factor per smoothing pass (0 = no smoothing, 1 = full average).")]
    [Range(0f, 1f)]
    public float smoothingStrength;

    public static TerrainWFCConfig Default => new TerrainWFCConfig
    {
        seed = 42,
        heightBuffer = 2,
        smoothingPasses = 2,
        smoothingStrength = 0.5f,
    };
}

/// <summary>
/// Static generator: populates a TerrainGridAuthoring's corner-height grid using
/// Perlin noise followed by WFC-style constraint propagation (|Δh| ≤ 1 between
/// L∞-neighbour corners). Guarantees smooth, cliff-free terrain.
/// </summary>
public static class TerrainWFCGenerator
{
    private const int MaxRelaxationPasses = 200;

    /// <summary>
    /// Generate terrain corner heights from gradient lines and enforce the WFC constraint.
    /// Optionally bakes the result into Unity Terrains.
    /// </summary>
    public static void Generate(TerrainGridAuthoring grid, TerrainWFCConfig config, Terrain unityTerrain = null)
        => Generate(grid, config, unityTerrain != null ? new[] { unityTerrain } : null, null);

    public static void Generate(TerrainGridAuthoring grid, TerrainWFCConfig config, Terrain[] terrains)
        => Generate(grid, config, terrains, null);

    public static void Generate(TerrainGridAuthoring grid, TerrainWFCConfig config, Terrain[] terrains, List<GradientLine> gradientLines)
    {
        if (grid == null) return;

        grid.EnsureValidData();
        Vector2Int cs = grid.CornerGridSize;
        int w = cs.x;
        int h = cs.y;

        // Use seed to offset the sampling position so different seeds give different landscapes.
        var rng = new System.Random(config.seed);
        float offsetX = (float)(rng.NextDouble() * 10000.0);
        float offsetZ = (float)(rng.NextDouble() * 10000.0);

        int[,] heights = new int[w, h];

        // Base terrain is flat (0). All shaping comes from gradient lines.
        bool hasGradients = gradientLines != null && gradientLines.Count > 0;

        for (int cz = 0; cz < h; cz++)
        {
            for (int cx = 0; cx < w; cx++)
            {
                Vector2 cornerTilePos = new Vector2(cx, cz);
                float totalHeight = hasGradients
                    ? EvaluateGradientLines(cornerTilePos, gradientLines, offsetX, offsetZ)
                    : 0f;
                heights[cx, cz] = Mathf.RoundToInt(totalHeight);
            }
        }

        // ── Step 2: WFC constraint propagation ───────────────────────────────
        // Enforce |h_a – h_b| ≤ 1 for all L∞-neighbour pairs.
        // We only ever lower heights (clamp to max-neighbour + 1), so it always converges.
        EnforceConstraints(heights, w, h);

        // ── Step 2b: Remove isolated peaks ───────────────────────────────────
        // Pull down any corner that is strictly higher than ALL its neighbours,
        // eliminating single-corner spike pyramids.
        RemoveIsolatedPeaks(heights, w, h);

        // ── Step 2b2: Post-generation smoothing ──────────────────────────────
        // Smooth out harsh transitions (especially from gradient line boundaries)
        // by averaging neighbours, then re-enforce constraints.
        if (config.smoothingPasses > 0 && config.smoothingStrength > 0f)
        {
            SmoothHeights(heights, w, h, config.smoothingPasses, config.smoothingStrength);
            EnforceConstraints(heights, w, h);
        }

        // ── Step 2c: Compute tile coverage from terrain objects ───────────
        // A tile is "covered" when its center lies within at least one Terrain.
        Vector2Int ftSize = grid.FullTileGridSize;
        bool[,] tileCovered = ComputeTileCoverage(grid, terrains, ftSize);

        // Mark tiles adjacent to void as paint-locked so the terraform tool can't override WFC corners.
        MarkPaintLockedTiles(grid, tileCovered, ftSize);

        // ── Step 2d: Normalize heights so lowest point sits at heightBuffer ────
        // Only consider corners that do NOT border a void; void-border corners
        // will be forced to 0 afterwards so they must not influence the offset.
        int minHeight = int.MaxValue;
        for (int cz = 0; cz < h; cz++)
            for (int cx = 0; cx < w; cx++)
                if (!IsCornerBorderingVoid(cx, cz, ftSize, tileCovered))
                    minHeight = Mathf.Min(minHeight, heights[cx, cz]);

        if (minHeight != int.MaxValue && minHeight != config.heightBuffer)
        {
            int offsetToApply = minHeight - config.heightBuffer;
            for (int cz = 0; cz < h; cz++)
                for (int cx = 0; cx < w; cx++)
                    heights[cx, cz] -= offsetToApply;
        }

        // ── Step 2e: Enclose terrain — set void-border vertices to height 0 ──
        // Any corner that touches at least one void tile (out of bounds or
        // uncovered by a Terrain object) is forced to 0 so the terrain drops
        // to the floor at outer edges AND inner void boundaries.
        for (int cz = 0; cz < h; cz++)
            for (int cx = 0; cx < w; cx++)
                if (IsCornerBorderingVoid(cx, cz, ftSize, tileCovered))
                    heights[cx, cz] = 0;

        // ── Step 3: Write to grid ────────────────────────────────────────────
        for (int cz = 0; cz < h; cz++)
            for (int cx = 0; cx < w; cx++)
                grid.SetCornerHeight(cx, cz, heights[cx, cz]);

        grid.SyncEnabledCellsFromShapes();

        if (terrains != null)
            foreach (Terrain t in terrains)
                if (t != null) grid.ApplyToTerrain(t);

        StitchTerrainNeighbors(terrains);
    }
    public static void Clear(TerrainGridAuthoring grid, Terrain unityTerrain = null)
        => Clear(grid, unityTerrain != null ? new[] { unityTerrain } : null);

    public static void Clear(TerrainGridAuthoring grid, Terrain[] terrains)
    {
        if (grid == null) return;
        grid.EnsureValidData();
        Vector2Int cs = grid.CornerGridSize;
        for (int cz = 0; cz < cs.y; cz++)
            for (int cx = 0; cx < cs.x; cx++)
                grid.SetCornerHeight(cx, cz, 0);

        grid.SyncEnabledCellsFromShapes();

        if (terrains != null)
            foreach (Terrain t in terrains)
                if (t != null) grid.ApplyToTerrain(t);

        StitchTerrainNeighbors(terrains);
    }

    private static void EnforceConstraints(int[,] heights, int w, int h)
    {
        for (int pass = 0; pass < MaxRelaxationPasses; pass++)
        {
            bool changed = false;

            for (int cz = 0; cz < h; cz++)
            {
                for (int cx = 0; cx < w; cx++)
                {
                    int current = heights[cx, cz];
                    int maxNeighbour = 0;

                    for (int dz = -1; dz <= 1; dz++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dz == 0) continue;
                            int nx = cx + dx;
                            int nz = cz + dz;
                            if (nx < 0 || nx >= w || nz < 0 || nz >= h) continue;
                            int nh = heights[nx, nz];
                            if (nh > maxNeighbour) maxNeighbour = nh;
                        }
                    }

                    int limit = maxNeighbour + 1;
                    if (current > limit)
                    {
                        heights[cx, cz] = limit;
                        changed = true;
                    }
                }
            }

            if (!changed) break;
        }
    }

    // ── Isolated peak removal ─────────────────────────────────────────────────

    private static void RemoveIsolatedPeaks(int[,] heights, int w, int h)
    {
        // A corner is an isolated peak if it is strictly higher than every
        // L∞-neighbour. Pull it down to the max neighbour height.
        for (int cz = 0; cz < h; cz++)
        {
            for (int cx = 0; cx < w; cx++)
            {
                int current = heights[cx, cz];
                if (current == 0) continue;

                int maxNeighbour = 0;
                bool isolated = true;

                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int nx = cx + dx;
                        int nz = cz + dz;
                        if (nx < 0 || nx >= w || nz < 0 || nz >= h) continue;
                        int nh = heights[nx, nz];
                        if (nh >= current) { isolated = false; break; }
                        if (nh > maxNeighbour) maxNeighbour = nh;
                    }
                    if (!isolated) break;
                }

                if (isolated)
                    heights[cx, cz] = maxNeighbour;
            }
        }
    }

    // ── Perlin noise ─────────────────────────────────────────────────────────────

    private static float SampleOctavePerlin(float x, float z, int octaves, float persistence, float lacunarity)
    {
        float total = 0f;
        float amp = 1f;
        float freq = 1f;
        float maxValue = 0f;

        for (int i = 0; i < octaves; i++)
        {
            total += Mathf.PerlinNoise(x * freq, z * freq) * amp;
            maxValue += amp;
            amp *= persistence;
            freq *= lacunarity;
        }

        return Mathf.Clamp01(total / maxValue);
    }

    // ── Tile coverage helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Builds a bool grid marking which full tiles are physically covered by at
    /// least one Terrain object. If no terrains are provided, all tiles are
    /// assumed covered (falls back to grid-boundary-only enclosure).
    /// </summary>
    private static bool[,] ComputeTileCoverage(TerrainGridAuthoring grid, Terrain[] terrains, Vector2Int ftSize)
    {
        bool[,] covered = new bool[ftSize.x, ftSize.y];

        if (terrains == null || terrains.Length == 0)
        {
            // No terrain info → treat every tile as covered.
            for (int tz = 0; tz < ftSize.y; tz++)
                for (int tx = 0; tx < ftSize.x; tx++)
                    covered[tx, tz] = true;
            return covered;
        }

        for (int tz = 0; tz < ftSize.y; tz++)
        {
            for (int tx = 0; tx < ftSize.x; tx++)
            {
                if (!grid.TryGetFullTileCenterWorld(tx, tz, out Vector3 center)) continue;
                foreach (Terrain t in terrains)
                {
                    if (t == null || t.terrainData == null) continue;
                    Vector3 tPos  = t.transform.position;
                    Vector3 tSize = t.terrainData.size;
                    if (center.x >= tPos.x && center.x <= tPos.x + tSize.x &&
                        center.z >= tPos.z && center.z <= tPos.z + tSize.z)
                    {
                        covered[tx, tz] = true;
                        break;
                    }
                }
            }
        }
        return covered;
    }

    /// <summary>
    /// Marks tiles that are adjacent to a void (out-of-bounds or not covered by a Terrain) as
    /// paint-locked on the grid. These are the tiles whose WFC-corners are forced to 0 and must
    /// not be modified by the player.
    /// </summary>
    private static void MarkPaintLockedTiles(TerrainGridAuthoring grid, bool[,] tileCovered, Vector2Int ftSize)
    {
        int[] dx = { -1, 1,  0, 0 };
        int[] dz = {  0, 0, -1, 1 };

        for (int tz = 0; tz < ftSize.y; tz++)
        {
            for (int tx = 0; tx < ftSize.x; tx++)
            {
                bool edge = false;
                for (int i = 0; i < 4; i++)
                {
                    int nx = tx + dx[i];
                    int nz = tz + dz[i];
                    if (nx < 0 || nx >= ftSize.x || nz < 0 || nz >= ftSize.y) { edge = true; break; }
                    if (!tileCovered[nx, nz]) { edge = true; break; }
                }
                grid.SetTilePaintLocked(tx, tz, edge);
            }
        }
    }

    /// <summary>
    /// Returns true when corner (cx, cz) has fewer than 4 covered adjacent tiles.
    /// A corner touches tiles (cx-1,cz-1), (cx,cz-1), (cx-1,cz), (cx,cz).
    /// </summary>
    private static bool IsCornerBorderingVoid(int cx, int cz, Vector2Int ftSize, bool[,] tileCovered)
    {
        for (int dz = -1; dz <= 0; dz++)
        {
            for (int dx = -1; dx <= 0; dx++)
            {
                int tx = cx + dx;
                int tz = cz + dz;
                if (tx < 0 || tx >= ftSize.x || tz < 0 || tz >= ftSize.y)
                    return true;
                if (!tileCovered[tx, tz])
                    return true;
            }
        }
        return false;
    }

    // ── Post-generation smoothing ────────────────────────────────────────────────

    private static void SmoothHeights(int[,] heights, int w, int h, int passes, float strength)
    {
        int[,] buffer = new int[w, h];

        for (int pass = 0; pass < passes; pass++)
        {
            for (int cz = 0; cz < h; cz++)
            {
                for (int cx = 0; cx < w; cx++)
                {
                    float sum = 0f;
                    int count = 0;

                    for (int dz = -1; dz <= 1; dz++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dz == 0) continue;
                            int nx = cx + dx;
                            int nz = cz + dz;
                            if (nx < 0 || nx >= w || nz < 0 || nz >= h) continue;
                            sum += heights[nx, nz];
                            count++;
                        }
                    }

                    if (count > 0)
                    {
                        float avg = sum / count;
                        float blended = Mathf.Lerp(heights[cx, cz], avg, strength);
                        buffer[cx, cz] = Mathf.RoundToInt(blended);
                    }
                    else
                    {
                        buffer[cx, cz] = heights[cx, cz];
                    }
                }
            }

            // Copy buffer back
            for (int cz = 0; cz < h; cz++)
                for (int cx = 0; cx < w; cx++)
                    heights[cx, cz] = buffer[cx, cz];
        }
    }

    // ── Gradient line evaluation ─────────────────────────────────────────────────

    private static float EvaluateFalloff(float normalizedDist, EaseMode ease)
    {
        float t = Mathf.Clamp01(normalizedDist);
        switch (ease)
        {
            case EaseMode.EaseIn:    t = t * t; break;
            case EaseMode.EaseOut:   t = 1f - (1f - t) * (1f - t); break;
            case EaseMode.EaseInOut: t = t * t * (3f - 2f * t); break;
        }
        return 1f - t;
    }

    /// <summary>
    /// Evaluates all gradient lines at a corner position and returns the combined height.
    /// Each line samples its own noise, weighted by falloff, then blended toward flat (0).
    /// </summary>
    private static float EvaluateGradientLines(
        Vector2 cornerTilePos, List<GradientLine> gradientLines, float offsetX, float offsetZ)
    {
        float weightedSum = 0f;
        float totalWeight = 0f;

        for (int i = 0; i < gradientLines.Count; i++)
        {
            GradientLine line = gradientLines[i];
            if (line.points == null || line.points.Count < 2) continue;

            line.ProjectPoint(cornerTilePos, out float t, out float dist);
            if (dist > line.influenceHalfWidth) continue;

            float normalizedDist = dist / Mathf.Max(0.001f, line.influenceHalfWidth);
            float weight = EvaluateFalloff(normalizedDist, line.falloffEase);
            if (weight <= 0f) continue;

            // Sample parameters along the line
            float amplitude = GradientLine.LerpParam(line.amplitudeStart, line.amplitudeEnd, t, line.amplitudeEase, line.amplitudeCustomCurve);
            float period = GradientLine.LerpParam(line.periodStart, line.periodEnd, t, line.periodEase, line.periodCustomCurve);
            float heightContrib = GradientLine.LerpParam(line.heightContributionStart, line.heightContributionEnd, t, line.heightContributionEase, line.heightContributionCustomCurve);
            int octaves = Mathf.Clamp(Mathf.RoundToInt(GradientLine.LerpParam(line.octavesStart, line.octavesEnd, t, line.octavesEase, line.octavesCustomCurve)), 1, 8);
            float persistence = Mathf.Clamp01(GradientLine.LerpParam(line.persistenceStart, line.persistenceEnd, t, line.persistenceEase, line.persistenceCustomCurve));
            float lacunarity = Mathf.Max(1f, GradientLine.LerpParam(line.lacunarityStart, line.lacunarityEnd, t, line.lacunarityEase, line.lacunarityCustomCurve));
            float baseHeight = GradientLine.LerpParam(line.baseHeightStart, line.baseHeightEnd, t, line.baseHeightEase, line.baseHeightCustomCurve);

            float overrideScale = Mathf.Max(0.001f, line.perlinScale * period);
            float noiseVal = SampleOctavePerlin(
                cornerTilePos.x * overrideScale + offsetX,
                cornerTilePos.y * overrideScale + offsetZ,
                octaves, persistence, lacunarity);

            float height = noiseVal * amplitude * heightContrib + baseHeight;

            weightedSum += height * weight;
            totalWeight += weight;
        }

        if (totalWeight > 0f)
        {
            float gradVal = weightedSum / totalWeight;
            float blend = Mathf.Clamp01(totalWeight);
            return gradVal * blend;
        }
        return 0f;
    }

    // ── Terrain chunk stitching ──────────────────────────────────────────────────

    /// <summary>
    /// Auto-detects adjacency between terrain chunks and calls SetNeighbors + Flush
    /// to eliminate seams at chunk boundaries.
    /// </summary>
    public static void StitchTerrainNeighbors(Terrain[] terrains)
    {
        if (terrains == null) return;

        foreach (Terrain t in terrains)
        {
            if (t == null || t.terrainData == null) continue;

            Vector3 pos = t.transform.position;
            Vector3 size = t.terrainData.size;
            Terrain left = null, right = null, top = null, bottom = null;

            foreach (Terrain other in terrains)
            {
                if (other == t || other == null || other.terrainData == null) continue;
                Vector3 oPos = other.transform.position;
                Vector3 oSize = other.terrainData.size;

                float tol = 0.1f;
                bool sameZ = Mathf.Abs(oPos.z - pos.z) < tol && Mathf.Abs(oSize.z - size.z) < tol;
                bool sameX = Mathf.Abs(oPos.x - pos.x) < tol && Mathf.Abs(oSize.x - size.x) < tol;

                if (sameZ && Mathf.Abs(oPos.x - (pos.x - oSize.x)) < tol) left = other;
                if (sameZ && Mathf.Abs(oPos.x - (pos.x + size.x)) < tol) right = other;
                if (sameX && Mathf.Abs(oPos.z - (pos.z + size.z)) < tol) top = other;
                if (sameX && Mathf.Abs(oPos.z - (pos.z - oSize.z)) < tol) bottom = other;
            }

            t.SetNeighbors(left, top, right, bottom);
        }

        foreach (Terrain t in terrains)
            if (t != null) t.Flush();
    }
}

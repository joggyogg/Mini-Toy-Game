using UnityEngine;

/// <summary>
/// A single noise layer for terrain generation.
/// Represents one scale of Perlin noise with its own frequency and amplitude contribution.
/// </summary>
[System.Serializable]
public struct NoiseLayer
{
    [Tooltip("Perlin noise scale (lower = larger features). E.g., 0.01 for huge smooth hills.")]
    public float perlinScale;

    [Tooltip("Height contribution of this layer (added to final terrain). E.g., 3 for large hills.")]
    public int heightContribution;

    [Tooltip("Number of octaves for fractal noise within this layer.")]
    [Range(1, 8)]
    public int octaves;

    [Tooltip("Amplitude decay per octave.")]
    [Range(0f, 1f)]
    public float persistence;

    [Tooltip("Frequency growth per octave.")]
    public float lacunarity;

    public static NoiseLayer Default => new NoiseLayer
    {
        perlinScale = 0.12f,
        heightContribution = 4,
        octaves = 3,
        persistence = 0.45f,
        lacunarity = 2.0f,
    };

    /// <summary>Example: Large smooth hills.</summary>
    public static NoiseLayer LargeHills => new NoiseLayer
    {
        perlinScale = 0.01f,
        heightContribution = 4,
        octaves = 2,
        persistence = 0.5f,
        lacunarity = 2.0f,
    };

    /// <summary>Example: Small fine details.</summary>
    public static NoiseLayer FineDetails => new NoiseLayer
    {
        perlinScale = 0.2f,
        heightContribution = 1,
        octaves = 3,
        persistence = 0.5f,
        lacunarity = 2.0f,
    };
}

/// <summary>
/// Configuration for Perlin-noise-based WFC terrain generation.
/// Supports multiple noise layers that combine to create complex terrain.
/// </summary>
[System.Serializable]
public struct TerrainWFCConfig
{
    [Tooltip("List of noise layers to combine. E.g., large smooth hills + fine details.")]
    public NoiseLayer[] noiseLayers;

    [Tooltip("Random seed for offsetting the Perlin sample position.")]
    public int seed;

    [Tooltip("Height buffer: the lowest terrain point will be offset to this height (in tiles). Higher = terrain floats higher. Set to 2 for 1 tile above ground.")]
    public int heightBuffer;

    public static TerrainWFCConfig Default => new TerrainWFCConfig
    {
        noiseLayers = new[] { NoiseLayer.Default },
        seed = 42,
        heightBuffer = 2,
    };

    /// <summary>Example: Large hills with fine details.</summary>
    public static TerrainWFCConfig HillsWithDetails => new TerrainWFCConfig
    {
        noiseLayers = new[] { NoiseLayer.LargeHills, NoiseLayer.FineDetails },
        seed = 42,
        heightBuffer = 2,
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
    /// Generate terrain corner heights from Perlin noise and enforce the WFC constraint.
    /// Optionally bakes the result into a Unity Terrain.
    /// </summary>
    public static void Generate(TerrainGridAuthoring grid, TerrainWFCConfig config, Terrain unityTerrain = null)
        => Generate(grid, config, unityTerrain != null ? new[] { unityTerrain } : null);

    public static void Generate(TerrainGridAuthoring grid, TerrainWFCConfig config, Terrain[] terrains)
    {
        if (grid == null) return;
        if (config.noiseLayers == null || config.noiseLayers.Length == 0)
        {
            Debug.LogWarning("TerrainWFCConfig has no noise layers. Using default layer.");
            config.noiseLayers = new[] { NoiseLayer.Default };
        }

        grid.EnsureValidData();
        Vector2Int cs = grid.CornerGridSize;
        int w = cs.x;
        int h = cs.y;

        // ── Step 1: Sample multiple noise layers at each corner ────────────────
        // Use seed to offset the sampling position so different seeds give different landscapes.
        var rng = new System.Random(config.seed);
        float offsetX = (float)(rng.NextDouble() * 10000.0);
        float offsetZ = (float)(rng.NextDouble() * 10000.0);

        int[,] heights = new int[w, h];

        // Sample all layers and accumulate their contributions
        for (int cz = 0; cz < h; cz++)
        {
            for (int cx = 0; cx < w; cx++)
            {
                float totalHeight = 0f;

                foreach (NoiseLayer layer in config.noiseLayers)
                {
                    float scale = Mathf.Max(0.001f, layer.perlinScale);
                    int octaves = Mathf.Max(1, layer.octaves);
                    float persistence = Mathf.Clamp01(layer.persistence);
                    float lacunarity = Mathf.Max(1f, layer.lacunarity);
                    int heightContribution = Mathf.Max(1, layer.heightContribution);

                    float noiseValue = SampleOctavePerlin(
                        cx * scale + offsetX,
                        cz * scale + offsetZ,
                        octaves, persistence, lacunarity);

                    totalHeight += noiseValue * heightContribution;
                }

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

        // ── Step 2c: Normalize heights so lowest point sits at heightBuffer ────
        // Find the minimum height, then offset all heights so the minimum becomes heightBuffer.
        int minHeight = int.MaxValue;
        for (int cz = 0; cz < h; cz++)
            for (int cx = 0; cx < w; cx++)
                minHeight = Mathf.Min(minHeight, heights[cx, cz]);

        if (minHeight != int.MaxValue && minHeight > config.heightBuffer)
        {
            int offsetToApply = minHeight - config.heightBuffer;
            for (int cz = 0; cz < h; cz++)
                for (int cx = 0; cx < w; cx++)
                    heights[cx, cz] -= offsetToApply;
        }

        // ── Step 3: Write to grid ────────────────────────────────────────────
        for (int cz = 0; cz < h; cz++)
            for (int cx = 0; cx < w; cx++)
                grid.SetCornerHeight(cx, cz, heights[cx, cz]);

        grid.SyncEnabledCellsFromShapes();

        if (terrains != null)
            foreach (Terrain t in terrains)
                if (t != null) grid.ApplyToTerrain(t);
    }

    /// <summary>
    /// Clears all corner heights to 0.
    /// </summary>
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
    }

    // ── Constraint propagation ───────────────────────────────────────────────────

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
}

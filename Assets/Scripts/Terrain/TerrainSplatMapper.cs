using UnityEngine;

/// <summary>
/// Auto-paints terrain splatmaps based on slope angle.
/// Flat areas get grass, moderate slopes get dirt, steep slopes get rock.
/// Attach to the same GameObject as <see cref="TerrainGridAuthoring"/>.
/// Designed to support many layers — add more via the Inspector.
/// </summary>
public class TerrainSplatMapper : MonoBehaviour
{
    // ── Per-layer slope mapping ──────────────────────────────────────────────────

    [System.Serializable]
    public class SlopeLayer
    {
        public TerrainLayer terrainLayer;
        [Tooltip("Slope angle (degrees) where this layer starts to appear.")]
        [Range(0f, 90f)] public float minAngle;
        [Tooltip("Slope angle (degrees) where this layer is fully opaque.")]
        [Range(0f, 90f)] public float maxAngle;
    }

    // ── Inspector fields ─────────────────────────────────────────────────────────

    [Header("Terrain Layers (order matters — first match wins residual)")]
    [SerializeField] private SlopeLayer[] slopeLayers = new SlopeLayer[]
    {
        new SlopeLayer { minAngle = 0f,  maxAngle = 15f  }, // Grass
        new SlopeLayer { minAngle = 20f, maxAngle = 35f  }, // Dirt
        new SlopeLayer { minAngle = 40f, maxAngle = 90f  }, // Rock
    };

    [Header("Noise")]
    [Tooltip("Perlin noise added to slope angle before evaluating layers. Breaks up uniform bands.")]
    [Range(0f, 15f)]
    [SerializeField] private float noiseAmplitude = 8f;

    [Tooltip("World-space scale of the noise pattern. Smaller = more zoomed in.")]
    [SerializeField] private float noiseScale = 0.05f;

    [Tooltip("Offset seed so each terrain gets a different pattern.")]
    [SerializeField] private Vector2 noiseOffset = new Vector2(1000f, 1000f);

    // ── Public API ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Full splatmap repaint for a single terrain. Call after SetHeights.
    /// </summary>
    public void PaintSplatmaps(Terrain terrain)
    {
        if (terrain == null || slopeLayers == null || slopeLayers.Length == 0) return;
        TerrainData td = terrain.terrainData;
        if (td == null) return;

        AssignLayers(td);

        int alphaRes = td.alphamapResolution;
        int heightRes = td.heightmapResolution;
        int layerCount = slopeLayers.Length;
        float[,,] alphamaps = new float[alphaRes, alphaRes, layerCount];
        float[,] heights = td.GetHeights(0, 0, heightRes, heightRes);
        Vector3 terrainSize = td.size;

        // Debug: check what we're working with.
        float minH = float.MaxValue, maxH = float.MinValue;
        float maxSlope = 0f;
        for (int z = 0; z < heightRes; z += heightRes / 8)
            for (int x = 0; x < heightRes; x += heightRes / 8)
            {
                float v = heights[z, x];
                if (v < minH) minH = v;
                if (v > maxH) maxH = v;
            }

        for (int az = 0; az < alphaRes; az++)
        {
            for (int ax = 0; ax < alphaRes; ax++)
            {
                float normX = (float)ax / (alphaRes - 1);
                float normZ = (float)az / (alphaRes - 1);

                float slopeAngle = ComputeSlopeFromHeights(heights, heightRes, terrainSize, normX, normZ);
                if (slopeAngle > maxSlope) maxSlope = slopeAngle;
                AddNoise(ref slopeAngle, terrain.transform.position, terrainSize, normX, normZ);
                ComputeWeights(slopeAngle, alphamaps, az, ax, layerCount);
            }
        }

        Debug.Log($"[SplatMapper] terrain={terrain.name} heightRes={heightRes} alphaRes={alphaRes} " +
                  $"terrainSize={terrainSize} layers={layerCount} heightRange=[{minH:F6}, {maxH:F6}] maxSlope={maxSlope:F2}°");

        td.SetAlphamaps(0, 0, alphamaps);
    }
    /// Partial splatmap repaint covering the alphamap pixels that overlap the given
    /// tile rectangle. Mirrors the ApplyToTerrainPartial approach.
    /// </summary>
    public void PaintSplatmapsPartial(Terrain terrain, RectInt tileRegion, TerrainGridAuthoring grid)
    {
        if (terrain == null || slopeLayers == null || slopeLayers.Length == 0 || grid == null) return;
        TerrainData td = terrain.terrainData;
        if (td == null) return;

        AssignLayers(td);

        int alphaRes = td.alphamapResolution;
        int heightRes = td.heightmapResolution;
        int layerCount = slopeLayers.Length;
        Vector2Int fullSize = grid.FullTileGridSize;
        float[,] heights = td.GetHeights(0, 0, heightRes, heightRes);
        Vector3 terrainSize = td.size;

        // Expand by 1 tile for slope-neighbour influence.
        int minTx = Mathf.Max(0, tileRegion.xMin - 1);
        int minTz = Mathf.Max(0, tileRegion.yMin - 1);
        int maxTx = Mathf.Min(fullSize.x, tileRegion.xMax + 1);
        int maxTz = Mathf.Min(fullSize.y, tileRegion.yMax + 1);

        Vector3 terrainPos = terrain.transform.position;
        Vector3 gridPos = grid.transform.position;
        Vector3 gridRight = grid.transform.right;
        Vector3 gridForward = grid.transform.forward;
        Vector3 originOffset = grid.GridOriginLocalOffset;
        float tileWorldSize = grid.FullTileWorldSizeValue;

        // Compute alphamap pixel bounding box from the tile region corners.
        float pxMin = float.MaxValue, pxMax = float.MinValue;
        float pzMin = float.MaxValue, pzMax = float.MinValue;
        for (int cz = 0; cz <= 1; cz++)
        {
            for (int cx = 0; cx <= 1; cx++)
            {
                float lx = (cx == 0 ? minTx : maxTx) * tileWorldSize + originOffset.x;
                float lz = (cz == 0 ? minTz : maxTz) * tileWorldSize + originOffset.z;
                float wx = gridPos.x + gridRight.x * lx + gridForward.x * lz;
                float wz = gridPos.z + gridRight.z * lx + gridForward.z * lz;
                float px = (wx - terrainPos.x) / terrainSize.x * (alphaRes - 1);
                float pz = (wz - terrainPos.z) / terrainSize.z * (alphaRes - 1);
                if (px < pxMin) pxMin = px; if (px > pxMax) pxMax = px;
                if (pz < pzMin) pzMin = pz; if (pz > pzMax) pzMax = pz;
            }
        }

        int axMin = Mathf.Max(0, Mathf.FloorToInt(pxMin) - 1);
        int axMax = Mathf.Min(alphaRes - 1, Mathf.CeilToInt(pxMax) + 1);
        int azMin = Mathf.Max(0, Mathf.FloorToInt(pzMin) - 1);
        int azMax = Mathf.Min(alphaRes - 1, Mathf.CeilToInt(pzMax) + 1);

        int w = axMax - axMin + 1;
        int h = azMax - azMin + 1;
        if (w <= 0 || h <= 0) return;

        float[,,] alphamaps = new float[h, w, layerCount];

        for (int dz = 0; dz < h; dz++)
        {
            for (int dx = 0; dx < w; dx++)
            {
                float normX = (float)(axMin + dx) / (alphaRes - 1);
                float normZ = (float)(azMin + dz) / (alphaRes - 1);

                float slopeAngle = ComputeSlopeFromHeights(heights, heightRes, terrainSize, normX, normZ);
                AddNoise(ref slopeAngle, terrainPos, terrainSize, normX, normZ);
                ComputeWeights(slopeAngle, alphamaps, dz, dx, layerCount);
            }
        }

        td.SetAlphamaps(axMin, azMin, alphamaps);
    }

    // ── Internals ────────────────────────────────────────────────────────────────

    private void AssignLayers(TerrainData td)
    {
        TerrainLayer[] existing = td.terrainLayers;
        bool match = existing != null && existing.Length == slopeLayers.Length;
        if (match)
        {
            for (int i = 0; i < slopeLayers.Length; i++)
                if (existing[i] != slopeLayers[i].terrainLayer) { match = false; break; }
        }
        if (match) return;

        TerrainLayer[] layers = new TerrainLayer[slopeLayers.Length];
        for (int i = 0; i < slopeLayers.Length; i++)
            layers[i] = slopeLayers[i].terrainLayer;
        td.terrainLayers = layers;
    }

    /// <summary>
    /// Compute slope angle in degrees from the raw heightmap using finite differences.
    /// This avoids GetInterpolatedNormal() which returns stale data right after SetHeights().
    /// </summary>
    private static float ComputeSlopeFromHeights(float[,] heights, int res, Vector3 terrainSize, float normX, float normZ)
    {
        // Map normalized coords to heightmap pixel space.
        float hxF = normX * (res - 1);
        float hzF = normZ * (res - 1);
        int hx = Mathf.Clamp(Mathf.RoundToInt(hxF), 0, res - 1);
        int hz = Mathf.Clamp(Mathf.RoundToInt(hzF), 0, res - 1);

        // Sample neighbours (clamped at edges).
        int hxL = Mathf.Max(0, hx - 1);
        int hxR = Mathf.Min(res - 1, hx + 1);
        int hzD = Mathf.Max(0, hz - 1);
        int hzU = Mathf.Min(res - 1, hz + 1);

        // Central differences in heightmap-normalized [0..1] space.
        float dhdx = (heights[hz, hxR] - heights[hz, hxL]) / (hxR - hxL);
        float dhdz = (heights[hzU, hx] - heights[hzD, hx]) / (hzU - hzD);

        // Convert to world-space gradients.
        // heights are stored as fraction of terrainSize.y; pixel spacing is terrainSize.x/(res-1) etc.
        float pixelSpacingX = terrainSize.x / (res - 1);
        float pixelSpacingZ = terrainSize.z / (res - 1);
        float worldDhdx = (dhdx * terrainSize.y) / pixelSpacingX;
        float worldDhdz = (dhdz * terrainSize.y) / pixelSpacingZ;

        float slopeRad = Mathf.Atan(Mathf.Sqrt(worldDhdx * worldDhdx + worldDhdz * worldDhdz));
        return slopeRad * Mathf.Rad2Deg;
    }

    private void AddNoise(ref float slopeAngle, Vector3 terrainPos, Vector3 terrainSize, float normX, float normZ)
    {
        if (noiseAmplitude > 0f)
        {
            float worldX = terrainPos.x + normX * terrainSize.x;
            float worldZ = terrainPos.z + normZ * terrainSize.z;
            float noise = Mathf.PerlinNoise(
                worldX * noiseScale + noiseOffset.x,
                worldZ * noiseScale + noiseOffset.y);
            slopeAngle += (noise - 0.5f) * 2f * noiseAmplitude;
        }
    }

    [Header("Blending")]
    [Tooltip("Degrees of smooth falloff outside each layer's [min, max] range.")]
    [Range(1f, 15f)]
    [SerializeField] private float blendWidth = 12f;

    private void ComputeWeights(float slopeAngle, float[,,] alphamaps, int az, int ax, int layerCount)
    {
        float totalWeight = 0f;

        for (int i = 0; i < layerCount; i++)
        {
            SlopeLayer sl = slopeLayers[i];
            float w;

            if (slopeAngle >= sl.minAngle && slopeAngle <= sl.maxAngle)
            {
                // Inside the layer's full-strength zone.
                w = 1f;
            }
            else if (slopeAngle < sl.minAngle)
            {
                // Below the range — fade out over blendWidth degrees.
                float t = Mathf.Clamp01((sl.minAngle - slopeAngle) / blendWidth);
                w = 1f - t * t * (3f - 2f * t);
            }
            else
            {
                // Above the range — fade out over blendWidth degrees.
                float t = Mathf.Clamp01((slopeAngle - sl.maxAngle) / blendWidth);
                w = 1f - t * t * (3f - 2f * t);
            }

            alphamaps[az, ax, i] = w;
            totalWeight += w;
        }

        // Normalize; if no layer claims this pixel, assign it all to the first layer.
        if (totalWeight > 0f)
        {
            for (int i = 0; i < layerCount; i++)
                alphamaps[az, ax, i] /= totalWeight;
        }
        else if (layerCount > 0)
        {
            alphamaps[az, ax, 0] = 1f;
        }
    }
}

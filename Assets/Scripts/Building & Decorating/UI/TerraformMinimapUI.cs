using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Minimap panel for the terraform tool. Displays an overhead view of the terrain with
/// height-tinted tiles. The player can paint-drag with three tools:
///   Raise   — increment the full tile's height level by 1.
///   Dig     — decrement the full tile's height level by 1 (min 0).
///   Flatten — click to sample a tile's height, drag to set other tiles to that height.
///
/// Also shows the same layer list (from furniture female tiles) as DecorateMinimapUI,
/// and draws a white player-position marker.
///
/// Setup: Attach to a RawImage GameObject. Assign all serialized fields.
/// Call Initialise() from TerrainEditController.EnterTerraformMode().
/// </summary>
[RequireComponent(typeof(RawImage))]
public class TerraformMinimapUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    // ── Inspector ──────────────────────────────────────────────────────────────────
    [SerializeField] private int pixelsPerCell = 4;
    [SerializeField] private int viewHalfSize  = 5;

    [SerializeField] private Button layerUpButton;
    [SerializeField] private Button layerDownButton;
    [SerializeField] private TMPro.TMP_Text layerLabel;

    [SerializeField] private Button raiseButton;
    [SerializeField] private Button digButton;
    [SerializeField] private Button flattenButton;

    // ── Colours ───────────────────────────────────────────────────────────────────
    // Base tile colours (same checkerboard as decorate minimap).
    private static readonly Color32 PrimaryLightBase   = new Color32(237, 92,  184, 255);
    private static readonly Color32 PrimaryDarkBase    = new Color32(252, 0,   115, 255);
    private static readonly Color32 SecondaryLightBase = new Color32(255, 201, 117, 255);
    private static readonly Color32 SecondaryDarkBase  = new Color32(255, 161, 0,   255);
    private static readonly Color32 DisabledColor      = new Color32(28,  28,  28,  255);
    // Edge tiles (adjacent to void) are locked from painting — shown as a dark red-brown.
    private static readonly Color32 EdgeLockedColor    = new Color32(80,  30,  30,  255);
    // Slope tiles are shown as a desaturated mid-gray tint.
    private static readonly Color32 SlopeOverlayColor  = new Color32(140, 130, 120, 220);
    // Player marker.
    private static readonly Color32 PlayerMarkerColor  = new Color32(255, 255, 255, 255);

    // ── State ─────────────────────────────────────────────────────────────────────
    private TerrainGridAuthoring terrain;
    private TerrainEditController controller;
    private IReadOnlyList<PlacedFurnitureRecord> placedFurniture;
    private PlayerMotor playerMotor;

    private Texture2D minimapTexture;
    private RawImage  rawImage;
    private Color32[] pixelBuffer;
    private int       textureDim;

    private Vector2Int viewOriginSubtile;
    private Vector2Int lastPlayerFullTile;

    // Layer list — same logic as DecorateMinimapUI.
    private List<float> availableLayers = new List<float>();
    private int currentLayerIndex;

    private enum Tool { Raise, Dig, Flatten }
    private Tool activeTool = Tool.Raise;

    private bool isPainting;
    private int flattenTargetHeight;

    // ── Lifecycle ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        rawImage = GetComponent<RawImage>();
    }

    private void OnEnable()
    {
        if (layerUpButton   != null) layerUpButton.onClick.AddListener(LayerUp);
        if (layerDownButton != null) layerDownButton.onClick.AddListener(LayerDown);
        if (raiseButton   != null) raiseButton.onClick.AddListener(() => SetTool(Tool.Raise));
        if (digButton     != null) digButton.onClick.AddListener(() => SetTool(Tool.Dig));
        if (flattenButton != null) flattenButton.onClick.AddListener(() => SetTool(Tool.Flatten));
    }

    private void OnDisable()
    {
        if (layerUpButton   != null) layerUpButton.onClick.RemoveListener(LayerUp);
        if (layerDownButton != null) layerDownButton.onClick.RemoveListener(LayerDown);
        if (raiseButton   != null) raiseButton.onClick.RemoveAllListeners();
        if (digButton     != null) digButton.onClick.RemoveAllListeners();
        if (flattenButton != null) flattenButton.onClick.RemoveAllListeners();
    }

    private void Update()
    {
        if (playerMotor == null || terrain == null) return;
        Vector2Int current = playerMotor.IsInDecorateMode
            ? playerMotor.CurrentFullTile
            : terrain.TryGetNearestWalkableFullTile(playerMotor.transform.position, out Vector2Int t0)
                ? t0
                : lastPlayerFullTile;
        if (current != lastPlayerFullTile)
        {
            lastPlayerFullTile = current;
            Rebuild();
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Call from TerrainEditController.EnterTerraformMode(). placedFurniture may be null
    /// if no decorate session has happened yet; the layer list will then show only Floor.
    /// </summary>
    public void Initialise(TerrainGridAuthoring terrainGrid,
                           TerrainEditController ctrl,
                           IReadOnlyList<PlacedFurnitureRecord> furniture = null,
                           PlayerMotor motor = null)
    {
        terrain       = terrainGrid;
        controller    = ctrl;
        placedFurniture = furniture;
        playerMotor   = motor;

        if (playerMotor != null)
        {
            lastPlayerFullTile = playerMotor.IsInDecorateMode
                ? playerMotor.CurrentFullTile
                : terrain.TryGetNearestWalkableFullTile(playerMotor.transform.position, out Vector2Int t0) ? t0 : default;
        }

        // Build layer list early so we can pick the layer the player is standing on.
        RefreshAvailableLayers();
        currentLayerIndex = FindLayerForPlayerTile();

        Rebuild();
    }

    public void Rebuild()
    {
        if (terrain == null) return;
        RefreshAvailableLayers();
        RefreshViewOrigin();
        EnsureTexture();
        DrawTerrain();
        DrawPlayerMarker();
        minimapTexture.SetPixels32(pixelBuffer);
        minimapTexture.Apply(false);
        UpdateLayerLabel();
        UpdateLayerButtonStates();
        UpdateToolButtonVisuals();
    }

    // ── Tool selection ────────────────────────────────────────────────────────────

    private void SetTool(Tool t)
    {
        activeTool = t;
        UpdateToolButtonVisuals();
    }

    private void UpdateToolButtonVisuals()
    {
        // Highlight the active tool button by changing its color block.
        SetButtonHighlight(raiseButton,   activeTool == Tool.Raise);
        SetButtonHighlight(digButton,     activeTool == Tool.Dig);
        SetButtonHighlight(flattenButton, activeTool == Tool.Flatten);
    }

    private static void SetButtonHighlight(Button btn, bool active)
    {
        if (btn == null) return;
        ColorBlock cb = btn.colors;
        cb.normalColor = active ? new Color(1f, 0.78f, 0.2f) : Color.white;
        btn.colors = cb;
    }

    // ── Layer navigation ──────────────────────────────────────────────────────────

    private int FindLayerForPlayerTile()
    {
        if (terrain == null || availableLayers.Count == 0) return 0;
        Vector2Int tile = lastPlayerFullTile;
        int level = terrain.GetTileLevel(tile.x, tile.y);
        if (level == 0) return 0; // Floor is always layer index 0
        float worldY = terrain.GetTerrainLevelWorldY(level);
        float quantized = Mathf.Round(worldY / 0.5f) * 0.5f;
        int best = 0;
        float bestDist = Mathf.Abs(availableLayers[0] - quantized);
        for (int i = 1; i < availableLayers.Count; i++)
        {
            float d = Mathf.Abs(availableLayers[i] - quantized);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return best;
    }

    private void LayerUp()
    {
        if (currentLayerIndex < availableLayers.Count - 1)
        {
            currentLayerIndex++;
            Rebuild();
        }
    }

    private void LayerDown()
    {
        if (currentLayerIndex > 0)
        {
            currentLayerIndex--;
            Rebuild();
        }
    }

    private void RefreshAvailableLayers()
    {
        availableLayers.Clear();
        availableLayers.Add(0f);

        var seen = new HashSet<float>();
        seen.Add(0f);

        if (placedFurniture != null)
        {
            foreach (PlacedFurnitureRecord record in placedFurniture)
            {
                if (record.Instance == null) continue;
                PlaceableGridAuthoring auth = record.Instance;
                Transform t = auth.transform;

                foreach (FemaleGridLayer layer in auth.EnumerateFemaleLayers())
                {
                    float worldSY = t.position.y + auth.MaleGridFloorLocalY + layer.LocalHeight;
                    float quantized = Mathf.Round(worldSY / 0.5f) * 0.5f;
                    if (seen.Add(quantized))
                        availableLayers.Add(quantized);
                }
            }
        }

        // Add terrain height levels beyond ground floor.
        if (terrain != null)
        {
            foreach (int level in terrain.GetFlatHeightLevels())
            {
                if (level == 0) continue;
                float worldY = terrain.GetTerrainLevelWorldY(level);
                float quantized = Mathf.Round(worldY / 0.5f) * 0.5f;
                if (seen.Add(quantized))
                    availableLayers.Add(quantized);
            }
        }

        availableLayers.Sort();
        currentLayerIndex = Mathf.Clamp(currentLayerIndex, 0, availableLayers.Count - 1);
    }

    private void UpdateLayerLabel()
    {
        if (layerLabel == null) return;
        layerLabel.text = currentLayerIndex == 0
            ? "Floor"
            : $"Height {availableLayers[currentLayerIndex]:0.0} m";
    }

    private void UpdateLayerButtonStates()
    {
        if (layerUpButton   != null) layerUpButton.interactable   = currentLayerIndex < availableLayers.Count - 1;
        if (layerDownButton != null) layerDownButton.interactable = currentLayerIndex > 0;
    }

    // ── Drawing ───────────────────────────────────────────────────────────────────

    private void EnsureTexture()
    {
        int dim = ViewSizeCells * pixelsPerCell;
        if (minimapTexture == null || minimapTexture.width != dim)
        {
            if (minimapTexture != null) Destroy(minimapTexture);
            minimapTexture = new Texture2D(dim, dim, TextureFormat.RGBA32, false);
            minimapTexture.filterMode = FilterMode.Point;
            rawImage.texture = minimapTexture;
            textureDim  = dim;
            pixelBuffer = new Color32[dim * dim];
        }
    }

    private void DrawTerrain()
    {
        int s        = terrain.SubtilesPerFullTile;
        int viewSize = ViewSizeCells;
        Vector2Int gridSize = terrain.GridSizeInCells;

        // Map the currently viewed layer's world Y to a terrain height level integer.
        float level0Y = terrain.GetTerrainLevelWorldY(0);
        float currentLayerWorldY = availableLayers.Count > 0 ? availableLayers[currentLayerIndex] : 0f;
        int viewTerrainLevel = Mathf.RoundToInt((currentLayerWorldY - level0Y) / TerrainGridAuthoring.LevelHeight);

        for (int vz = 0; vz < viewSize; vz++)
        {
            for (int vx = 0; vx < viewSize; vx++)
            {
                int cx = viewOriginSubtile.x + vx;
                int cz = viewOriginSubtile.y + vz;

                bool inBounds = cx >= 0 && cz >= 0 && cx < gridSize.x && cz < gridSize.y;
                Color32 color;

                if (!inBounds)
                {
                    color = DisabledColor;
                }
                else
                {
                    int tileX    = cx / s; int tileZ    = cz / s;
                    int subtileX = cx % s; int subtileZ = cz % s;
                    int tileHeight = terrain.GetTileLevel(tileX, tileZ);
                    TileShape tileShape = terrain.ComputeTileShape(tileX, tileZ);
                    int levelsBelow = viewTerrainLevel - tileHeight;

                    if (levelsBelow < 0)
                    {
                        // Above the current viewing layer — blank.
                        color = DisabledColor;
                    }
                    else if (terrain.IsFullTileEdge(tileX, tileZ))
                    {
                        // Edge tile (adjacent to void): locked from painting.
                        color = EdgeLockedColor;
                    }
                    else if (tileShape != TileShape.Flat)
                    {
                        // Slope — dimmed grey.
                        color = DimColor(SlopeOverlayColor, LayerBrightness(levelsBelow));
                    }
                    else
                    {
                        // Flat tile: pink/orange checkerboard, brightness by depth below current layer.
                        bool primary = ((tileX + tileZ)       & 1) == 0;
                        bool light   = ((subtileX + subtileZ) & 1) == 0;
                        Color32 baseColor = primary
                            ? (light ? PrimaryLightBase   : PrimaryDarkBase)
                            : (light ? SecondaryLightBase : SecondaryDarkBase);
                        color = DimColor(baseColor, LayerBrightness(levelsBelow));
                    }
                }

                FillViewPixels(vx, vz, color);
            }
        }
    }

    // Multiplies colour channels by t (0..1). Dims tiles below the current viewing layer.
    private static Color32 DimColor(Color32 c, float t)
        => new Color32((byte)(c.r * t), (byte)(c.g * t), (byte)(c.b * t), c.a);

    // 1.0 = current layer; 0.40 = 1 below; 0.20 = 2 below; 0.12 = 3+ below.
    private static float LayerBrightness(int levelsBelow)
        => levelsBelow <= 0 ? 1f : levelsBelow == 1 ? 0.40f : levelsBelow == 2 ? 0.20f : 0.12f;

    private void DrawPlayerMarker()
    {
        if (playerMotor == null || terrain == null) return;
        Vector2Int pft;
        if (playerMotor.IsInDecorateMode)
        {
            pft = playerMotor.CurrentFullTile;
        }
        else
        {
            if (!terrain.TryGetNearestWalkableFullTile(playerMotor.transform.position, out pft)) return;
        }

        int s = terrain.SubtilesPerFullTile;
        for (int sz = 0; sz < s; sz++)
            for (int sx = 0; sx < s; sx++)
                FillCellPixels(pft.x * s + sx, pft.y * s + sz, PlayerMarkerColor);
    }

    // ── Pixel helpers ─────────────────────────────────────────────────────────────

    private int ViewDiameter  => viewHalfSize * 2 + 1;
    private int ViewSizeCells => ViewDiameter * terrain.SubtilesPerFullTile;

    private void FillCellPixels(int cellX, int cellZ, Color32 color)
    {
        FillViewPixels(cellX - viewOriginSubtile.x, cellZ - viewOriginSubtile.y, color);
    }

    private void FillViewPixels(int vx, int vz, Color32 color)
    {
        int viewSize = ViewSizeCells;
        if (vx < 0 || vx >= viewSize || vz < 0 || vz >= viewSize) return;
        int px = vx * pixelsPerCell;
        int py = vz * pixelsPerCell;
        for (int dy = 0; dy < pixelsPerCell; dy++)
            for (int dx = 0; dx < pixelsPerCell; dx++)
                pixelBuffer[(py + dy) * textureDim + (px + dx)] = color;
    }

    private void RefreshViewOrigin()
    {
        if (terrain == null) return;
        int s = terrain.SubtilesPerFullTile;
        Vector2Int pft = (playerMotor != null && playerMotor.IsInDecorateMode)
            ? playerMotor.CurrentFullTile
            : (playerMotor != null && terrain.TryGetNearestWalkableFullTile(playerMotor.transform.position, out Vector2Int t0))
                ? t0
                : lastPlayerFullTile;
        viewOriginSubtile = new Vector2Int((pft.x - viewHalfSize) * s, (pft.y - viewHalfSize) * s);
    }

    // ── Pointer → full tile ───────────────────────────────────────────────────────

    private bool TryScreenToFullTile(Vector2 screenPos, out Vector2Int fullTile)
    {
        fullTile = default;
        if (rawImage == null) return false;

        RectTransform rt = rawImage.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, null, out Vector2 localPoint))
            return false;

        Rect rect = rt.rect;
        float u = (localPoint.x - rect.xMin) / rect.width;
        float v = (localPoint.y - rect.yMin) / rect.height;

        int viewSize = ViewSizeCells;
        int vx = Mathf.FloorToInt(u * viewSize);
        int vz = Mathf.FloorToInt(v * viewSize);

        // Subtile absolute coords.
        int cx = viewOriginSubtile.x + vx;
        int cz = viewOriginSubtile.y + vz;

        Vector2Int gridSize = terrain.GridSizeInCells;
        if (cx < 0 || cx >= gridSize.x || cz < 0 || cz >= gridSize.y) return false;

        // Convert subtile → full tile.
        int s = terrain.SubtilesPerFullTile;
        fullTile = new Vector2Int(cx / s, cz / s);

        Vector2Int fullSize = terrain.FullTileGridSize;
        return fullTile.x >= 0 && fullTile.x < fullSize.x && fullTile.y >= 0 && fullTile.y < fullSize.y;
    }

    // ── Paint interaction ─────────────────────────────────────────────────────────

    public void OnPointerDown(PointerEventData eventData)
    {
        isPainting = true;

        // For Flatten: lock the target height from the first tile clicked.
        if (activeTool == Tool.Flatten && terrain != null)
        {
            if (TryScreenToFullTile(eventData.position, out Vector2Int origin))
                flattenTargetHeight = terrain.GetTileLevel(origin.x, origin.y);
        }

        PaintAt(eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPainting = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isPainting) PaintAt(eventData.position);
    }

    private void PaintAt(Vector2 screenPos)
    {
        if (controller == null || terrain == null) return;
        if (!TryScreenToFullTile(screenPos, out Vector2Int tile)) return;

        // Edge tiles (adjacent to void) have WFC-enforced corners forced to 0 — never allow painting them.
        if (terrain.IsFullTileEdge(tile.x, tile.y)) return;

        switch (activeTool)
        {
            case Tool.Raise:
                controller.RaiseTile(tile.x, tile.y);
                break;
            case Tool.Dig:
                controller.LowerTile(tile.x, tile.y);
                break;
            case Tool.Flatten:
                controller.FlattenTile(tile.x, tile.y, flattenTargetHeight);
                break;
        }
    }
}

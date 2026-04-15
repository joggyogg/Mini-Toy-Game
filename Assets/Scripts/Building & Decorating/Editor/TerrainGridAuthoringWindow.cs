using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Plain-English purpose:
/// Dedicated editor window for painting TerrainGridAuthoring cells.
/// Left panel: stats + controls (snap-to-full-tile toggle, fill/clear buttons).
/// Right panel: drag-to-paint subtile grid. Thicker rules at every full-tile boundary.
///
/// Cell colors use the same female palette as PlaceableGridAuthoringWindow so the
/// visual language stays consistent.
/// </summary>
public class TerrainGridAuthoringWindow : EditorWindow
{
    // ── Layout constants ──────────────────────────────────────────────────────────
    private const float CellButtonSize = 26f;
    private const float FullTileCellSize = 26f;   // pixel size per full tile in performance mode
    private const float FullTileWorldSize = 1f;
    private const float LeftPanelWidth = 220f;

    // ── Cell colours (identical palette to PlaceableGridAuthoringWindow female) ──
    private static readonly Color FemalePrimaryLight = new Color(0.93f, 0.36f, 0.72f, 0.95f);
    private static readonly Color FemalePrimaryDark = new Color(0.99f, 0.0f, 0.45f, 0.95f);
    private static readonly Color FemaleSecondaryLight = new Color(1f, 0.79f, 0.46f, 0.95f);
    private static readonly Color FemaleSecondaryDark = new Color(1f, 0.63f, 0.0f, 0.95f);
    private static readonly Color InactiveCellColor = new Color(0.22f, 0.22f, 0.22f, 1f);
    private static readonly Color PartialTileColor  = new Color(0.55f, 0.22f, 0.42f, 0.95f); // some-but-not-all subtiles enabled
    private static readonly Color GridLineColor = new Color(0f, 0f, 0f, 0.35f);
    private static readonly Color FullTileBorderColor = new Color(0f, 0f, 0f, 0.7f);

    // ── State ─────────────────────────────────────────────────────────────────────
    private TerrainGridAuthoring targetAuthoring;
    private bool snapToFullTile = false;
    private bool performanceMode = false;  // draw full tiles instead of subtiles
    private Vector2 scrollPosition;

    // Cached grid texture – rebuilt only when cell data changes (_textureDirty = true).
    private Texture2D _gridTexture;
    private bool _textureDirty = true;
    private bool _lastTexPerf;

    // Drag-paint state
    private bool isDragPainting;
    private bool dragPaintValue;
    private Vector2Int lastPaintedCell = new Vector2Int(int.MinValue, int.MinValue);

    // WFC generation state — reads/writes from targetAuthoring for persistence.
    // Transient UI-only state (scroll) lives here.
    private Vector2 generationScrollPosition = Vector2.zero;

    // Tab selection (0 = Tile Editing, 1 = Terrain Generation)
    private int selectedTab = 0;

    // ── Chunk map state ───────────────────────────────────────────────────────────
    private const float ChunkMapHeight = 300f;
    private const float ChunkMapPadding = 10f;
    private static readonly Color ChunkFillColor = new Color(0.35f, 0.55f, 0.35f, 0.6f);
    private static readonly Color ChunkBorderColor = new Color(0.2f, 0.4f, 0.2f, 1f);
    private static readonly Color ChunkMapBgColor = new Color(0.18f, 0.18f, 0.18f, 1f);
    private static readonly Color GradientLineColor = new Color(0.2f, 0.7f, 1f, 0.9f);
    private static readonly Color GradientLineSelectedColor = new Color(1f, 0.9f, 0.2f, 1f);
    private static readonly Color InfluenceBandColor = new Color(0.2f, 0.7f, 1f, 0.12f);
    private static readonly Color InfluenceBandSelectedColor = new Color(1f, 0.9f, 0.2f, 0.12f);

    // ── Gradient line drawing / selection state ───────────────────────────────────
    private enum MapTool { Select, Draw }
    private MapTool currentMapTool = MapTool.Select;
    private int selectedLineIndex = -1;
    private bool isDrawingLine = false;
    private bool isRedrawingLine = false;
    private int redrawLineIndex = -1;
    private List<Vector2> drawingPoints = new List<Vector2>();

    // Cached chunk layout (recomputed each frame — cheap)
    private struct ChunkInfo { public Terrain terrain; public Rect tileRect; }
    private Rect chunkBoundsInTiles;

    // ── Public entry point ────────────────────────────────────────────────────────

    [MenuItem("Window/Mini Toy Game/Terrain Grid Authoring")]
    public static void Open()
    {
        GetWindow<TerrainGridAuthoringWindow>("Terrain Grid");
    }

    public static void Open(TerrainGridAuthoring authoring)
    {
        var window = GetWindow<TerrainGridAuthoringWindow>("Terrain Grid");
        window.SetTarget(authoring);
    }

    // ── Unity messages ────────────────────────────────────────────────────────────

    private void OnSelectionChange()
    {
        if (Selection.activeGameObject == null) return;
        var authoring = Selection.activeGameObject.GetComponent<TerrainGridAuthoring>();
        if (authoring != null)
        {
            SetTarget(authoring);
        }
    }

    private void OnDestroy()
    {
        if (_gridTexture != null)
        {
            DestroyImmediate(_gridTexture);
            _gridTexture = null;
        }
    }

    private void OnGUI()
    {
        DrawTargetHeader();

        if (targetAuthoring == null)
        {
            EditorGUILayout.HelpBox("Select a GameObject with a TerrainGridAuthoring component, or click below.", MessageType.Info);
            if (GUILayout.Button("Use Selected Object"))
            {
                TryPickFromSelection();
            }
            return;
        }

        // Tab buttons
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        selectedTab = GUILayout.Toolbar(selectedTab, new[] { "Tile Editing", "Terrain Generation" }, EditorStyles.toolbarButton);
        EditorGUILayout.EndHorizontal();

        if (selectedTab == 0)
        {
            // Tab 0: Tile Editing
            EditorGUILayout.BeginHorizontal();
            DrawTileEditingPanel();
            DrawRightPanel();
            EditorGUILayout.EndHorizontal();
        }
        else if (selectedTab == 1)
        {
            // Tab 1: Terrain Generation
            EditorGUILayout.BeginVertical();
            DrawTerrainGenerationPanel();
            EditorGUILayout.EndVertical();
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private void SetTarget(TerrainGridAuthoring authoring)
    {
        if (targetAuthoring == authoring) return;
        StopDragPainting();
        targetAuthoring = authoring;
        _textureDirty = true;
        Repaint();
    }

    private void TryPickFromSelection()
    {
        if (Selection.activeGameObject != null)
        {
            var authoring = Selection.activeGameObject.GetComponent<TerrainGridAuthoring>();
            if (authoring != null) SetTarget(authoring);
        }
    }

    // ── Header ────────────────────────────────────────────────────────────────────

    private void DrawTargetHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Use Selected", EditorStyles.toolbarButton, GUILayout.Width(90f)))
        {
            TryPickFromSelection();
        }

        string label = targetAuthoring != null ? targetAuthoring.name : "(none)";
        GUILayout.Label(label, EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        EditorGUILayout.EndHorizontal();
    }

    // ── Tile Editing Panel ────────────────────────────────────────────────────────

    private void DrawTileEditingPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(LeftPanelWidth));

        EditorGUILayout.LabelField("Grid Summary", EditorStyles.boldLabel);
        Vector2Int cellSize = targetAuthoring.GridSizeInCells;
        EditorGUILayout.LabelField("Subtile cells", $"{cellSize.x} \u00d7 {cellSize.y}");
        EditorGUILayout.LabelField("Enabled cells", $"{targetAuthoring.GetEnabledCellCount()} / {cellSize.x * cellSize.y}");

        Vector2Int fullSize = targetAuthoring.FullTileGridSize;
        EditorGUILayout.LabelField("Full tiles", $"{fullSize.x} \u00d7 {fullSize.y}");
        EditorGUILayout.LabelField("Walkable tiles", $"{targetAuthoring.GetWalkableFullTileCount()} / {fullSize.x * fullSize.y}");

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Paint Options", EditorStyles.boldLabel);
        snapToFullTile = EditorGUILayout.Toggle(new GUIContent("Snap to full tile", "Paint entire 1\u00d71 full-tile blocks rather than individual subtile cells."), snapToFullTile);
        performanceMode = EditorGUILayout.Toggle(new GUIContent("Performance Mode", "Render one cell per full tile instead of per subtile. Greatly reduces draw calls on large grids."), performanceMode);

        EditorGUILayout.Space(4);
        if (GUILayout.Button("Fill All"))
        {
            RecordChange("Fill Terrain Grid");
            targetAuthoring.FillAll(true);
            MarkDirty();
        }
        if (GUILayout.Button("Clear All"))
        {
            RecordChange("Clear Terrain Grid");
            targetAuthoring.FillAll(false);
            MarkDirty();
        }

        EditorGUILayout.Space(4);
        if (GUILayout.Button("Recalculate from Colliders"))
        {
            RecordChange("Recalculate Terrain Grid");
            targetAuthoring.RecalculateFromColliders();
            MarkDirty();
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            performanceMode
                ? "Performance Mode: one cell = one full tile.\nPink/orange = fully enabled, purple = partial, dark = disabled.\nLeft-click or drag to toggle full tiles."
                : "Pink/orange cells = enabled subtiles.\nLeft-click or drag to toggle cells.\nGreen overlay shows walkable 1\u00d71 tiles in the scene.",
            MessageType.None);

        EditorGUILayout.EndVertical();
    }

    // ── Terrain Generation Panel (revamped) ─────────────────────────────────────

    private void DrawTerrainGenerationPanel()
    {
        generationScrollPosition = EditorGUILayout.BeginScrollView(generationScrollPosition);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.LabelField("Terrain Generation (WFC)", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        // Read persisted config from the component
        TerrainWFCConfig cfg = targetAuthoring.WfcConfig;
        bool changed = false;

        // Show discovered child terrains.
        Terrain[] childTerrains = targetAuthoring.GetComponentsInChildren<Terrain>();
        string terrainInfo = childTerrains.Length == 0
            ? "None found (add Terrain children)"
            : $"{childTerrains.Length} child terrain(s)";
        EditorGUILayout.LabelField("Child Terrains", terrainInfo);

        EditorGUILayout.Space(4);
        int newSeed = EditorGUILayout.IntField("Seed", cfg.seed);
        if (newSeed != cfg.seed) { cfg.seed = newSeed; changed = true; }

        int newBuf = EditorGUILayout.IntField("Height Buffer (Tiles)", cfg.heightBuffer);
        if (newBuf != cfg.heightBuffer) { cfg.heightBuffer = newBuf; changed = true; }

        EditorGUILayout.Space(8);

        // ── Terrain Chunk Map ─────────────────────────────────────────────
        DrawChunkMap(childTerrains);

        EditorGUILayout.Space(4);

        // ── Drawing toolbar ──────────────────────────────────────────────
        DrawMapToolbar();

        EditorGUILayout.Space(4);

        // ── Selected line properties ─────────────────────────────────────
        DrawSelectedLineInspector();

        EditorGUILayout.Space(8);

        // ── Post-Generation Smoothing ────────────────────────────────────
        EditorGUILayout.LabelField("Post-Generation Smoothing", EditorStyles.boldLabel);
        int newSmPasses = EditorGUILayout.IntSlider("Smoothing Passes", cfg.smoothingPasses, 0, 10);
        if (newSmPasses != cfg.smoothingPasses) { cfg.smoothingPasses = newSmPasses; changed = true; }
        float newSmStr = EditorGUILayout.Slider("Smoothing Strength", cfg.smoothingStrength, 0f, 1f);
        if (newSmStr != cfg.smoothingStrength) { cfg.smoothingStrength = newSmStr; changed = true; }

        EditorGUILayout.Space(8);

        // ── Generate / Clear ─────────────────────────────────────────────
        if (GUILayout.Button("Generate Terrain"))
        {
            targetAuthoring.WfcConfig = cfg;
            TerrainWFCGenerator.Generate(targetAuthoring, cfg, childTerrains, targetAuthoring.GradientLines);
            MarkDirtyNoUndo();
        }
        if (GUILayout.Button("Randomise Seed & Generate"))
        {
            cfg.seed = Random.Range(0, 99999);
            changed = true;
            targetAuthoring.WfcConfig = cfg;
            TerrainWFCGenerator.Generate(targetAuthoring, cfg, childTerrains, targetAuthoring.GradientLines);
            MarkDirtyNoUndo();
        }
        if (GUILayout.Button("Clear Terrain"))
        {
            TerrainWFCGenerator.Clear(targetAuthoring, childTerrains);
            MarkDirtyNoUndo();
        }

        // Write back if any field changed
        if (changed)
        {
            RecordChange("Edit WFC Config");
            targetAuthoring.WfcConfig = cfg;
            MarkDirty();
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
    }

    // ── Terrain Chunk Map ─────────────────────────────────────────────────────────

    private List<ChunkInfo> ComputeChunkLayout(Terrain[] terrains)
    {
        var chunks = new List<ChunkInfo>();
        if (targetAuthoring == null) return chunks;

        Vector3 gridPos = targetAuthoring.transform.position;
        Vector3 gridRight = targetAuthoring.transform.right;
        Vector3 gridForward = targetAuthoring.transform.forward;
        Vector3 originOffset = targetAuthoring.GridOriginLocalOffset;

        foreach (Terrain t in terrains)
        {
            if (t == null || t.terrainData == null) continue;
            Vector3 tPos = t.transform.position;
            Vector3 tSize = t.terrainData.size;

            // Project terrain min/max into grid tile-space
            float minTX = float.MaxValue, minTZ = float.MaxValue;
            float maxTX = float.MinValue, maxTZ = float.MinValue;

            for (int ci = 0; ci < 4; ci++)
            {
                float wx = tPos.x + (ci % 2 == 0 ? 0f : tSize.x);
                float wz = tPos.z + (ci < 2 ? 0f : tSize.z);

                Vector3 offset = new Vector3(wx - gridPos.x, 0f, wz - gridPos.z);
                float localX = Vector3.Dot(offset, gridRight) - originOffset.x;
                float localZ = Vector3.Dot(offset, gridForward) - originOffset.z;

                float tx = localX / FullTileWorldSize;
                float tz = localZ / FullTileWorldSize;

                if (tx < minTX) minTX = tx;
                if (tx > maxTX) maxTX = tx;
                if (tz < minTZ) minTZ = tz;
                if (tz > maxTZ) maxTZ = tz;
            }

            chunks.Add(new ChunkInfo
            {
                terrain = t,
                tileRect = Rect.MinMaxRect(minTX, minTZ, maxTX, maxTZ)
            });
        }

        return chunks;
    }

    private Rect ComputeChunkBounds(List<ChunkInfo> chunks)
    {
        if (chunks.Count == 0)
        {
            // Fall back to full tile grid size
            Vector2Int ft = targetAuthoring.FullTileGridSize;
            return new Rect(0, 0, ft.x, ft.y);
        }

        float minX = float.MaxValue, minZ = float.MaxValue;
        float maxX = float.MinValue, maxZ = float.MinValue;
        foreach (var c in chunks)
        {
            if (c.tileRect.xMin < minX) minX = c.tileRect.xMin;
            if (c.tileRect.yMin < minZ) minZ = c.tileRect.yMin;
            if (c.tileRect.xMax > maxX) maxX = c.tileRect.xMax;
            if (c.tileRect.yMax > maxZ) maxZ = c.tileRect.yMax;
        }
        return Rect.MinMaxRect(minX, minZ, maxX, maxZ);
    }

    /// <summary>Convert tile-space (X,Z) to pixel position inside the map rect.</summary>
    private Vector2 TileToPixel(Vector2 tilePos, Rect mapRect, Rect bounds)
    {
        float scaleX = (mapRect.width - ChunkMapPadding * 2) / Mathf.Max(0.1f, bounds.width);
        float scaleZ = (mapRect.height - ChunkMapPadding * 2) / Mathf.Max(0.1f, bounds.height);
        float scale = Mathf.Min(scaleX, scaleZ);

        float offsetX = mapRect.x + ChunkMapPadding + (mapRect.width - ChunkMapPadding * 2 - bounds.width * scale) * 0.5f;
        float offsetZ = mapRect.y + ChunkMapPadding + (mapRect.height - ChunkMapPadding * 2 - bounds.height * scale) * 0.5f;

        float px = offsetX + (tilePos.x - bounds.x) * scale;
        // Flip Z so +Z is up on screen
        float pz = offsetZ + (bounds.yMax - tilePos.y - bounds.y) * scale;
        // Actually: screen Y increases downward, tile Z increases upward.
        pz = offsetZ + (bounds.height - (tilePos.y - bounds.y)) * scale;

        return new Vector2(px, pz);
    }

    /// <summary>Convert pixel position inside the map rect to tile-space (X,Z).</summary>
    private Vector2 PixelToTile(Vector2 pixel, Rect mapRect, Rect bounds)
    {
        float scaleX = (mapRect.width - ChunkMapPadding * 2) / Mathf.Max(0.1f, bounds.width);
        float scaleZ = (mapRect.height - ChunkMapPadding * 2) / Mathf.Max(0.1f, bounds.height);
        float scale = Mathf.Min(scaleX, scaleZ);

        float offsetX = mapRect.x + ChunkMapPadding + (mapRect.width - ChunkMapPadding * 2 - bounds.width * scale) * 0.5f;
        float offsetZ = mapRect.y + ChunkMapPadding + (mapRect.height - ChunkMapPadding * 2 - bounds.height * scale) * 0.5f;

        float tx = (pixel.x - offsetX) / scale + bounds.x;
        float tz = bounds.yMax - (pixel.y - offsetZ) / scale;

        return new Vector2(tx, tz);
    }

    private void DrawChunkMap(Terrain[] terrains)
    {
        // Reserve a fixed-height area
        Rect mapRect = GUILayoutUtility.GetRect(10f, ChunkMapHeight, GUILayout.ExpandWidth(true));

        if (Event.current.type == EventType.Layout) return;

        var chunks = ComputeChunkLayout(terrains);
        chunkBoundsInTiles = ComputeChunkBounds(chunks);

        // Background
        EditorGUI.DrawRect(mapRect, ChunkMapBgColor);

        // Draw each terrain chunk
        foreach (var chunk in chunks)
        {
            Vector2 minPx = TileToPixel(new Vector2(chunk.tileRect.xMin, chunk.tileRect.yMin), mapRect, chunkBoundsInTiles);
            Vector2 maxPx = TileToPixel(new Vector2(chunk.tileRect.xMax, chunk.tileRect.yMax), mapRect, chunkBoundsInTiles);

            float left = Mathf.Min(minPx.x, maxPx.x);
            float top = Mathf.Min(minPx.y, maxPx.y);
            float right = Mathf.Max(minPx.x, maxPx.x);
            float bottom = Mathf.Max(minPx.y, maxPx.y);

            Rect chunkPixelRect = Rect.MinMaxRect(left, top, right, bottom);
            EditorGUI.DrawRect(chunkPixelRect, ChunkFillColor);

            // Border
            EditorGUI.DrawRect(new Rect(chunkPixelRect.x, chunkPixelRect.y, chunkPixelRect.width, 1f), ChunkBorderColor);
            EditorGUI.DrawRect(new Rect(chunkPixelRect.x, chunkPixelRect.yMax - 1f, chunkPixelRect.width, 1f), ChunkBorderColor);
            EditorGUI.DrawRect(new Rect(chunkPixelRect.x, chunkPixelRect.y, 1f, chunkPixelRect.height), ChunkBorderColor);
            EditorGUI.DrawRect(new Rect(chunkPixelRect.xMax - 1f, chunkPixelRect.y, 1f, chunkPixelRect.height), ChunkBorderColor);

            // Label
            string label = chunk.terrain != null ? chunk.terrain.name : "?";
            GUI.Label(chunkPixelRect, label, EditorStyles.centeredGreyMiniLabel);
        }

        // Draw gradient lines for the active layer
        DrawGradientLinesOnMap(mapRect);

        // Handle map input (drawing / selecting)
        HandleChunkMapInput(mapRect);
    }

    // ── Gradient line rendering on the map ────────────────────────────────────────

    private void DrawGradientLinesOnMap(Rect mapRect)
    {
        var lines = targetAuthoring.GradientLines;
        if (lines == null) return;

        for (int i = 0; i < lines.Count; i++)
        {
            GradientLine line = lines[i];
            if (line.points == null || line.points.Count < 2) continue;

            bool isSelected = (i == selectedLineIndex);
            Color lineColor = isSelected ? GradientLineSelectedColor : GradientLineColor;
            Color bandColor = isSelected ? InfluenceBandSelectedColor : InfluenceBandColor;
            float lineWidth = isSelected ? 3f : 2f;

            // Draw influence band (approximate with offset polylines)
            DrawInfluenceBand(line, mapRect, bandColor);

            // Draw the polyline
            Vector2 prev = TileToPixel(line.points[0], mapRect, chunkBoundsInTiles);
            for (int p = 1; p < line.points.Count; p++)
            {
                Vector2 curr = TileToPixel(line.points[p], mapRect, chunkBoundsInTiles);
                DrawLineSegment(prev, curr, lineColor, lineWidth);
                prev = curr;
            }

            // Draw start/end markers
            Vector2 startPx = TileToPixel(line.points[0], mapRect, chunkBoundsInTiles);
            Vector2 endPx = TileToPixel(line.points[line.points.Count - 1], mapRect, chunkBoundsInTiles);
            EditorGUI.DrawRect(new Rect(startPx.x - 3, startPx.y - 3, 6, 6), lineColor);
            EditorGUI.DrawRect(new Rect(endPx.x - 3, endPx.y - 3, 6, 6), lineColor);
        }

        // Draw in-progress freehand line
        if (isDrawingLine && drawingPoints.Count >= 2)
        {
            Vector2 prev = TileToPixel(drawingPoints[0], mapRect, chunkBoundsInTiles);
            for (int p = 1; p < drawingPoints.Count; p++)
            {
                Vector2 curr = TileToPixel(drawingPoints[p], mapRect, chunkBoundsInTiles);
                DrawLineSegment(prev, curr, GradientLineColor, 2f);
                prev = curr;
            }
        }
    }

    private void DrawInfluenceBand(GradientLine line, Rect mapRect, Color bandColor)
    {
        // Approximate the influence band by drawing offset lines on both sides
        if (line.points.Count < 2) return;

        // Compute pixel scale factor
        float scaleX = (mapRect.width - ChunkMapPadding * 2) / Mathf.Max(0.1f, chunkBoundsInTiles.width);
        float scaleZ = (mapRect.height - ChunkMapPadding * 2) / Mathf.Max(0.1f, chunkBoundsInTiles.height);
        float pixelScale = Mathf.Min(scaleX, scaleZ);
        float bandWidthPx = line.influenceHalfWidth * pixelScale;

        // Draw a simple rect band approximation along each segment
        for (int i = 0; i < line.points.Count - 1; i++)
        {
            Vector2 aPx = TileToPixel(line.points[i], mapRect, chunkBoundsInTiles);
            Vector2 bPx = TileToPixel(line.points[i + 1], mapRect, chunkBoundsInTiles);

            Vector2 dir = (bPx - aPx);
            float len = dir.magnitude;
            if (len < 0.5f) continue;
            dir /= len;
            Vector2 perp = new Vector2(-dir.y, dir.x);

            // Draw a thin rect for this segment's influence band
            Vector2 center = (aPx + bPx) * 0.5f;
            Rect bandRect = new Rect(
                center.x - len * 0.5f - 1f,
                center.y - bandWidthPx,
                len + 2f,
                bandWidthPx * 2f);

            // Simple axis-aligned approximation — for non-axis-aligned segments, just draw a wide rect
            float minPx = Mathf.Min(aPx.x, bPx.x) - bandWidthPx;
            float maxPx = Mathf.Max(aPx.x, bPx.x) + bandWidthPx;
            float minPy = Mathf.Min(aPx.y, bPx.y) - bandWidthPx;
            float maxPy = Mathf.Max(aPx.y, bPx.y) + bandWidthPx;
            EditorGUI.DrawRect(Rect.MinMaxRect(minPx, minPy, maxPx, maxPy), bandColor);
        }
    }

    private static void DrawLineSegment(Vector2 a, Vector2 b, Color color, float width)
    {
        // Use EditorGUI.DrawRect to approximate line segments with thin rects
        Vector2 dir = b - a;
        float len = dir.magnitude;
        if (len < 0.1f) return;

        // For nearly horizontal/vertical lines, draw a thin rect. For diagonal, draw a series of tiny rects.
        float steps = Mathf.Max(1f, len / 2f);
        int stepCount = Mathf.CeilToInt(steps);
        float hw = width * 0.5f;

        for (int s = 0; s < stepCount; s++)
        {
            float t0 = (float)s / stepCount;
            float t1 = (float)(s + 1) / stepCount;
            Vector2 p0 = Vector2.Lerp(a, b, t0);
            Vector2 p1 = Vector2.Lerp(a, b, t1);
            float px = Mathf.Min(p0.x, p1.x) - hw;
            float py = Mathf.Min(p0.y, p1.y) - hw;
            float w = Mathf.Max(Mathf.Abs(p1.x - p0.x), width);
            float h = Mathf.Max(Mathf.Abs(p1.y - p0.y), width);
            EditorGUI.DrawRect(new Rect(px, py, w, h), color);
        }
    }

    // ── Map input handling ────────────────────────────────────────────────────────

    private void HandleChunkMapInput(Rect mapRect)
    {
        Event e = Event.current;
        int controlId = GUIUtility.GetControlID(FocusType.Passive);

        if (currentMapTool == MapTool.Draw)
        {
            HandleDrawInput(e, controlId, mapRect);
        }
        else // Select
        {
            HandleSelectInput(e, controlId, mapRect);
        }
    }

    private void HandleDrawInput(Event e, int controlId, Rect mapRect)
    {
        switch (e.GetTypeForControl(controlId))
        {
            case EventType.MouseDown:
                if (e.button == 0 && mapRect.Contains(e.mousePosition))
                {
                    GUIUtility.hotControl = controlId;
                    isDrawingLine = true;
                    drawingPoints.Clear();
                    drawingPoints.Add(PixelToTile(e.mousePosition, mapRect, chunkBoundsInTiles));
                    e.Use();
                }
                break;

            case EventType.MouseDrag:
                if (isDrawingLine && e.button == 0)
                {
                    Vector2 tilePos = PixelToTile(e.mousePosition, mapRect, chunkBoundsInTiles);
                    // Only add if moved enough in tile-space
                    if (drawingPoints.Count == 0 || Vector2.Distance(tilePos, drawingPoints[drawingPoints.Count - 1]) > 0.1f)
                    {
                        drawingPoints.Add(tilePos);
                    }
                    Repaint();
                    e.Use();
                }
                break;

            case EventType.MouseUp:
                if (isDrawingLine && e.button == 0)
                {
                    GUIUtility.hotControl = 0;
                    isDrawingLine = false;

                    if (drawingPoints.Count >= 2)
                    {
                        // Simplify the polyline
                        var simplified = GradientLine.SimplifyPolyline(drawingPoints, 0.3f);
                        if (simplified.Count >= 2)
                        {
                            if (isRedrawingLine && redrawLineIndex >= 0 && redrawLineIndex < targetAuthoring.GradientLines.Count)
                            {
                                RecordChange("Redraw Gradient Line");
                                targetAuthoring.GradientLines[redrawLineIndex].points = simplified;
                                selectedLineIndex = redrawLineIndex;
                                MarkDirty();
                            }
                            else
                            {
                                RecordChange("Draw Gradient Line");
                                var newLine = new GradientLine
                                {
                                    points = simplified,
                                    influenceHalfWidth = 5f,
                                    falloffEase = EaseMode.Linear,
                                };
                                targetAuthoring.AddGradientLine(newLine);
                                selectedLineIndex = targetAuthoring.GradientLines.Count - 1;
                                MarkDirty();
                            }
                        }
                    }
                    isRedrawingLine = false;
                    redrawLineIndex = -1;
                    drawingPoints.Clear();
                    e.Use();
                }
                break;
        }
    }

    private void HandleSelectInput(Event e, int controlId, Rect mapRect)
    {
        switch (e.GetTypeForControl(controlId))
        {
            case EventType.MouseDown:
                if (e.button == 0 && mapRect.Contains(e.mousePosition))
                {
                    Vector2 tilePos = PixelToTile(e.mousePosition, mapRect, chunkBoundsInTiles);
                    int closestIdx = -1;
                    float closestDist = float.MaxValue;

                    var lines = targetAuthoring.GradientLines;
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (lines[i].points == null || lines[i].points.Count < 2) continue;

                        lines[i].ProjectPoint(tilePos, out float t, out float dist);
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            closestIdx = i;
                        }
                    }

                    // Select if within reasonable distance (influence width or 3 tiles, whichever is larger)
                    if (closestIdx >= 0)
                    {
                        float threshold = Mathf.Max(3f, lines[closestIdx].influenceHalfWidth);
                        if (closestDist <= threshold)
                            selectedLineIndex = closestIdx;
                        else
                            selectedLineIndex = -1;
                    }
                    else
                    {
                        selectedLineIndex = -1;
                    }

                    Repaint();
                    e.Use();
                }
                break;
        }
    }

    // ── Map toolbar ──────────────────────────────────────────────────────────────

    private void DrawMapToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        int toolIdx = (int)currentMapTool;
        int newToolIdx = GUILayout.Toolbar(toolIdx, new[] { "Select", "Draw" }, EditorStyles.toolbarButton, GUILayout.Width(160));
        if (newToolIdx != toolIdx) currentMapTool = (MapTool)newToolIdx;

        GUILayout.FlexibleSpace();

        GUI.enabled = selectedLineIndex >= 0 && selectedLineIndex < targetAuthoring.GradientLines.Count;
        if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton, GUILayout.Width(70)))
        {
            RecordChange("Duplicate Gradient Line");
            var original = targetAuthoring.GradientLines[selectedLineIndex];
            var copy = original.Duplicate();
            targetAuthoring.AddGradientLine(copy);
            selectedLineIndex = targetAuthoring.GradientLines.Count - 1;
            MarkDirty();
        }
        if (GUILayout.Button("Redraw Selected", EditorStyles.toolbarButton, GUILayout.Width(110)))
        {
            redrawLineIndex = selectedLineIndex;
            isRedrawingLine = true;
            isDrawingLine = false;
            drawingPoints.Clear();
            currentMapTool = MapTool.Draw;
        }
        if (GUILayout.Button("Delete Selected", EditorStyles.toolbarButton, GUILayout.Width(100)))
        {
            RecordChange("Delete Gradient Line");
            targetAuthoring.RemoveGradientLine(selectedLineIndex);
            selectedLineIndex = -1;
            MarkDirty();
        }
        GUI.enabled = true;

        if (GUILayout.Button("Clear All Lines", EditorStyles.toolbarButton, GUILayout.Width(100)))
        {
            RecordChange("Clear All Gradient Lines");
            while (targetAuthoring.GradientLines.Count > 0)
                targetAuthoring.RemoveGradientLine(0);
            selectedLineIndex = -1;
            MarkDirty();
        }

        EditorGUILayout.EndHorizontal();

        // Info label
        int lineCount = targetAuthoring.GradientLines.Count;
        EditorGUILayout.LabelField($"{lineCount} gradient line(s)", EditorStyles.miniLabel);
    }

    // ── Selected line inspector ──────────────────────────────────────────────────

    private void DrawSelectedLineInspector()
    {
        var lines = targetAuthoring.GradientLines;
        if (selectedLineIndex < 0 || selectedLineIndex >= lines.Count) return;

        GradientLine line = lines[selectedLineIndex];

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"Gradient Line #{selectedLineIndex + 1}", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        line.influenceHalfWidth = Mathf.Max(0.5f, EditorGUILayout.FloatField("Influence Half-Width (tiles)", line.influenceHalfWidth));
        line.falloffEase = (EaseMode)EditorGUILayout.EnumPopup("Falloff", line.falloffEase);

        EditorGUILayout.Space(4);
        line.perlinScale = EditorGUILayout.FloatField("Perlin Scale", line.perlinScale);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Parameters (Start \u2192 End along line)", EditorStyles.miniLabel);

        DrawParamField("Amplitude", ref line.amplitudeStart, ref line.amplitudeEnd, ref line.amplitudeEase, ref line.amplitudeCustomCurve);
        DrawParamField("Period", ref line.periodStart, ref line.periodEnd, ref line.periodEase, ref line.periodCustomCurve);
        DrawParamField("Height Contribution", ref line.heightContributionStart, ref line.heightContributionEnd, ref line.heightContributionEase, ref line.heightContributionCustomCurve);
        DrawParamField("Octaves", ref line.octavesStart, ref line.octavesEnd, ref line.octavesEase, ref line.octavesCustomCurve);
        DrawParamField("Persistence", ref line.persistenceStart, ref line.persistenceEnd, ref line.persistenceEase, ref line.persistenceCustomCurve);
        DrawParamField("Lacunarity", ref line.lacunarityStart, ref line.lacunarityEnd, ref line.lacunarityEase, ref line.lacunarityCustomCurve);
        DrawParamField("Base Height", ref line.baseHeightStart, ref line.baseHeightEnd, ref line.baseHeightEase, ref line.baseHeightCustomCurve);

        if (EditorGUI.EndChangeCheck())
        {
            RecordChange("Edit Gradient Line");
            MarkDirty();
        }

        EditorGUILayout.EndVertical();
    }

    private static void DrawParamField(string label, ref float startVal, ref float endVal, ref EaseMode ease, ref AnimationCurve customCurve)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(130));
        EditorGUILayout.LabelField("Start", GUILayout.Width(32));
        startVal = EditorGUILayout.FloatField(startVal, GUILayout.Width(50));
        EditorGUILayout.LabelField("\u2192", GUILayout.Width(16));
        EditorGUILayout.LabelField("End", GUILayout.Width(26));
        endVal = EditorGUILayout.FloatField(endVal, GUILayout.Width(50));
        ease = (EaseMode)EditorGUILayout.EnumPopup(ease, GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();

        if (ease == EaseMode.Custom)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(134);
            EditorGUILayout.LabelField("Curve", GUILayout.Width(40));
            customCurve = EditorGUILayout.CurveField(customCurve);
            EditorGUILayout.EndHorizontal();
        }
    }

    // ── Right panel (scrollable paint grid) ───────────────────────────────────────

    private void DrawRightPanel()
    {
        EditorGUILayout.BeginVertical();
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        if (performanceMode)
        {
            Vector2Int ftSize = targetAuthoring.FullTileGridSize;
            float pixelW = ftSize.x * FullTileCellSize;
            float pixelH = ftSize.y * FullTileCellSize;
            Rect gridRect = GUILayoutUtility.GetRect(pixelW, pixelH, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
            if (Event.current.type != EventType.Layout)
            {
                if (Event.current.type == EventType.Repaint)
                {
                    EnsureGridTexture(ftSize, true);
                    GUI.DrawTexture(gridRect, _gridTexture, ScaleMode.StretchToFill, false);
                    DrawTileSeparators(gridRect, ftSize.x, ftSize.y, FullTileCellSize, FullTileCellSize);
                }
                HandlePaintInputPerf(gridRect, ftSize);
            }
        }
        else
        {
            Vector2Int gridSize = targetAuthoring.GridSizeInCells;
            float pixelW = gridSize.x * CellButtonSize;
            float pixelH = gridSize.y * CellButtonSize;
            Rect gridRect = GUILayoutUtility.GetRect(pixelW, pixelH, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
            if (Event.current.type != EventType.Layout)
            {
                if (Event.current.type == EventType.Repaint)
                {
                    EnsureGridTexture(gridSize, false);
                    GUI.DrawTexture(gridRect, _gridTexture, ScaleMode.StretchToFill, false);
                    int s = targetAuthoring.SubtilesPerFullTile;
                    if (s > 1)
                    {
                        Vector2Int ftSize = targetAuthoring.FullTileGridSize;
                        DrawTileSeparators(gridRect, ftSize.x, ftSize.y, s * CellButtonSize, s * CellButtonSize);
                    }
                }
                HandlePaintInput(gridRect, gridSize);
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // ── Texture-based rendering (one GPU draw call for the whole grid) ─────────────

    private void EnsureGridTexture(Vector2Int texSize, bool perfMode)
    {
        bool sizeOk = _gridTexture != null
                   && _gridTexture.width  == texSize.x
                   && _gridTexture.height == texSize.y;

        if (!sizeOk || _textureDirty || _lastTexPerf != perfMode)
        {
            if (!sizeOk)
            {
                if (_gridTexture != null) DestroyImmediate(_gridTexture);
                _gridTexture = new Texture2D(texSize.x, texSize.y, TextureFormat.RGBA32, false);
                _gridTexture.filterMode = FilterMode.Point;
                _gridTexture.wrapMode   = TextureWrapMode.Clamp;
            }

            var pixels = new Color32[texSize.x * texSize.y];
            if (perfMode) FillTexturePerf(pixels, texSize);
            else          FillTextureSubtile(pixels, texSize);
            _gridTexture.SetPixels32(pixels);
            _gridTexture.Apply(false);

            _textureDirty = false;
            _lastTexPerf  = perfMode;
        }
    }

    private void FillTexturePerf(Color32[] pixels, Vector2Int ftSize)
    {
        int s        = targetAuthoring.SubtilesPerFullTile;
        int totalSub = s * s;
        for (int fz = 0; fz < ftSize.y; fz++)
        {
            for (int fx = 0; fx < ftSize.x; fx++)
            {
                int baseX = fx * s, baseZ = fz * s, enabledSub = 0;
                for (int sz = 0; sz < s; sz++)
                    for (int sx = 0; sx < s; sx++)
                        if (targetAuthoring.GetCell(baseX + sx, baseZ + sz))
                            enabledSub++;

                Color c = enabledSub == totalSub ? GetFullTileColor(fx, fz)
                        : enabledSub == 0        ? InactiveCellColor
                        :                          (Color)PartialTileColor;
                pixels[fz * ftSize.x + fx] = c;
            }
        }
    }

    private void FillTextureSubtile(Color32[] pixels, Vector2Int gridSize)
    {
        int s = targetAuthoring.SubtilesPerFullTile;
        for (int z = 0; z < gridSize.y; z++)
        {
            for (int x = 0; x < gridSize.x; x++)
            {
                Color c = targetAuthoring.GetCell(x, z) ? GetCellColor(x, z, s) : InactiveCellColor;
                pixels[z * gridSize.x + x] = c;
            }
        }
    }

    // Draws thick separator lines at every full-tile boundary (very few rects).
    private static void DrawTileSeparators(Rect gridRect, int tileCountX, int tileCountY, float stepX, float stepY)
    {
        for (int tx = 1; tx < tileCountX; tx++)
        {
            float px = gridRect.xMin + tx * stepX;
            EditorGUI.DrawRect(new Rect(px - 1f, gridRect.yMin, 2f, gridRect.height), FullTileBorderColor);
        }
        for (int tz = 1; tz < tileCountY; tz++)
        {
            float py = gridRect.yMin + tz * stepY;
            EditorGUI.DrawRect(new Rect(gridRect.xMin, py - 1f, gridRect.width, 2f), FullTileBorderColor);
        }
    }

    private void HandlePaintInputPerf(Rect gridRect, Vector2Int fullTileSize)
    {
        Event e = Event.current;
        int controlId = GUIUtility.GetControlID(FocusType.Passive);

        switch (e.GetTypeForControl(controlId))
        {
            case EventType.MouseDown:
                if (e.button == 0 && gridRect.Contains(e.mousePosition))
                {
                    if (TryGetFullTileAtPosition(e.mousePosition, gridRect, fullTileSize, out Vector2Int fullTile))
                    {
                        GUIUtility.hotControl = controlId;
                        RecordChange("Paint Terrain Grid");
                        int s = targetAuthoring.SubtilesPerFullTile;
                        bool newValue = !targetAuthoring.GetCell(fullTile.x * s, fullTile.y * s);
                        isDragPainting = true;
                        dragPaintValue = newValue;
                        lastPaintedCell = new Vector2Int(int.MinValue, int.MinValue);
                        ApplyPaintFullTileDirect(fullTile);
                        MarkDirty();
                        e.Use();
                    }
                }
                break;

            case EventType.MouseDrag:
                if (isDragPainting && e.button == 0)
                {
                    if (TryGetFullTileAtPosition(e.mousePosition, gridRect, fullTileSize, out Vector2Int dragTile))
                    {
                        if (dragTile != lastPaintedCell)
                        {
                            ApplyPaintFullTileDirect(dragTile);
                            MarkDirty();
                        }
                    }
                    e.Use();
                }
                break;

            case EventType.MouseUp:
                if (isDragPainting && e.button == 0)
                {
                    GUIUtility.hotControl = 0;
                    StopDragPainting();
                    e.Use();
                }
                break;
        }
    }

    private void ApplyPaintFullTileDirect(Vector2Int fullTile)
    {
        if (fullTile == lastPaintedCell) return;
        lastPaintedCell = fullTile;
        int s = targetAuthoring.SubtilesPerFullTile;
        int baseX = fullTile.x * s;
        int baseZ = fullTile.y * s;
        for (int sz = 0; sz < s; sz++)
            for (int sx = 0; sx < s; sx++)
                targetAuthoring.SetCell(baseX + sx, baseZ + sz, dragPaintValue);
    }

    private static bool TryGetFullTileAtPosition(Vector2 mousePos, Rect gridRect, Vector2Int fullTileSize, out Vector2Int fullTile)
    {
        fullTile = default;
        if (!gridRect.Contains(mousePos)) return false;

        int fxIndex = Mathf.FloorToInt((mousePos.x - gridRect.xMin) / FullTileCellSize);
        int fzIndex = fullTileSize.y - 1 - Mathf.FloorToInt((mousePos.y - gridRect.yMin) / FullTileCellSize);

        if (fxIndex < 0 || fxIndex >= fullTileSize.x || fzIndex < 0 || fzIndex >= fullTileSize.y) return false;

        fullTile = new Vector2Int(fxIndex, fzIndex);
        return true;
    }

    private static Color GetFullTileColor(int fx, int fz)
    {
        return ((fx + fz) & 1) == 0 ? FemalePrimaryLight : FemaleSecondaryLight;
    }

    private static Color GetCellColor(int x, int z, int subtilesPerFullTile)
    {
        int fullTileX = x / subtilesPerFullTile;
        int fullTileZ = z / subtilesPerFullTile;
        bool isPrimary = ((fullTileX + fullTileZ) & 1) == 0;

        int subtileX = x % subtilesPerFullTile;
        int subtileZ = z % subtilesPerFullTile;
        bool isLight = ((subtileX + subtileZ) & 1) == 0;

        if (isPrimary)
        {
            return isLight ? FemalePrimaryLight : FemalePrimaryDark;
        }
        else
        {
            return isLight ? FemaleSecondaryLight : FemaleSecondaryDark;
        }
    }

    // ── Paint input ───────────────────────────────────────────────────────────────

    private void HandlePaintInput(Rect gridRect, Vector2Int gridSize)
    {
        Event e = Event.current;
        int controlId = GUIUtility.GetControlID(FocusType.Passive);

        switch (e.GetTypeForControl(controlId))
        {
            case EventType.MouseDown:
                if (e.button == 0 && gridRect.Contains(e.mousePosition))
                {
                    if (TryGetCellAtPosition(e.mousePosition, gridRect, gridSize, out Vector2Int cell))
                    {
                        GUIUtility.hotControl = controlId;
                        RecordChange("Paint Terrain Grid");
                        bool newValue = !targetAuthoring.GetCell(cell.x, cell.y);
                        isDragPainting = true;
                        dragPaintValue = newValue;
                        lastPaintedCell = new Vector2Int(int.MinValue, int.MinValue);
                        ApplyPaint(cell);
                        MarkDirty();
                        e.Use();
                    }
                }
                break;

            case EventType.MouseDrag:
                if (isDragPainting && e.button == 0)
                {
                    if (TryGetCellAtPosition(e.mousePosition, gridRect, gridSize, out Vector2Int dragCell))
                    {
                        if (dragCell != lastPaintedCell)
                        {
                            ApplyPaint(dragCell);
                            MarkDirty();
                        }
                    }
                    e.Use();
                }
                break;

            case EventType.MouseUp:
                if (isDragPainting && e.button == 0)
                {
                    GUIUtility.hotControl = 0;
                    StopDragPainting();
                    e.Use();
                }
                break;
        }
    }

    private void ApplyPaint(Vector2Int cell)
    {
        if (snapToFullTile)
        {
            PaintFullTile(cell);
        }
        else
        {
            targetAuthoring.SetCell(cell.x, cell.y, dragPaintValue);
            lastPaintedCell = cell;
        }
    }

    private void PaintFullTile(Vector2Int cell)
    {
        int s = targetAuthoring.SubtilesPerFullTile;
        int ftX = (cell.x / s) * s;
        int ftZ = (cell.y / s) * s;

        // Mark the full tile as the last painted unit to prevent re-painting on drag within it.
        Vector2Int fullTileKey = new Vector2Int(ftX, ftZ);
        if (fullTileKey == lastPaintedCell) return;
        lastPaintedCell = fullTileKey;

        for (int sz = 0; sz < s; sz++)
        {
            for (int sx = 0; sx < s; sx++)
            {
                targetAuthoring.SetCell(ftX + sx, ftZ + sz, dragPaintValue);
            }
        }
    }

    // ── Geometry ──────────────────────────────────────────────────────────────────

    private static bool TryGetCellAtPosition(Vector2 mousePos, Rect gridRect, Vector2Int gridSize, out Vector2Int cell)
    {
        cell = default;
        if (!gridRect.Contains(mousePos)) return false;

        int xIndex = Mathf.FloorToInt((mousePos.x - gridRect.xMin) / CellButtonSize);
        int zIndex = gridSize.y - 1 - Mathf.FloorToInt((mousePos.y - gridRect.yMin) / CellButtonSize);

        if (xIndex < 0 || xIndex >= gridSize.x || zIndex < 0 || zIndex >= gridSize.y) return false;

        cell = new Vector2Int(xIndex, zIndex);
        return true;
    }

    // ── Undo / dirty ─────────────────────────────────────────────────────────────

    private void StopDragPainting()
    {
        isDragPainting = false;
        lastPaintedCell = new Vector2Int(int.MinValue, int.MinValue);
    }

    private void RecordChange(string actionName)
    {
        Undo.RecordObject(targetAuthoring, actionName);
    }

    private void MarkDirty()
    {
        targetAuthoring.EnsureValidData();
        EditorUtility.SetDirty(targetAuthoring);
        _textureDirty = true;
        Repaint();
    }

    /// <summary>
    /// Marks the target dirty for scene-saving without recording an undo snapshot.
    /// Used for bulk operations (Generate / Clear) where the massive serialized arrays
    /// (enabledCells, cornerHeights, paintLockedTiles) make Undo.RecordObject extremely
    /// slow. These operations are deterministic and easily re-done, so undo is not needed.
    /// </summary>
    private void MarkDirtyNoUndo()
    {
        targetAuthoring.EnsureValidData();
        Undo.ClearUndo(targetAuthoring);
        EditorUtility.SetDirty(targetAuthoring);
        _textureDirty = true;
        Repaint();
    }

}

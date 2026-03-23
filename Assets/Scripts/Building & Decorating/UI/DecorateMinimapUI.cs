using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Plain-English purpose:
/// An overhead grid minimap for the decorate mode. Shows the terrain and any placed furniture
/// as coloured flat footprints. The player can:
///   - Drag furniture to reposition it (left mouse / primary touch).
///   - Right-click a piece to rotate it 90 degrees clockwise.
///   - Use the Layer Up / Layer Down buttons to view furniture on different surface heights.
///
/// Drawing: Each terrain subtile cell = pixelsPerCell × pixelsPerCell pixels on a Texture2D
/// displayed through a RawImage. The texture is rebuilt whenever the scene changes.
///
/// Setup: Attach to the RawImage GameObject. Assign buttons and the image reference.
/// Call Initialise() from BuildModeController after the panel is activated.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class DecorateMinimapUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    // ── Inspector ──────────────────────────────────────────────────────────────────
    [SerializeField] private int pixelsPerCell = 4;
    [SerializeField] private int viewHalfSize = 5;   // visible window = (viewHalfSize*2+1) full tiles in each axis
    [SerializeField] private Button layerUpButton;
    [SerializeField] private Button layerDownButton;
    [SerializeField] private TMPro.TMP_Text layerLabel;

    // ── Colours ───────────────────────────────────────────────────────────────────
    private static readonly Color32 DisabledCellColor      = new Color32(28,  28,  28,  255);
    private static readonly Color32 DisabledDimColor       = new Color32(28,  28,  28,  80);
    // Pink (primary) full-tiles — matches FemalePrimaryLight / FemalePrimaryDark gizmo colours.
    private static readonly Color32 PrimaryLightColor      = new Color32(237, 92,  184, 255);
    private static readonly Color32 PrimaryDarkColor       = new Color32(252, 0,   115, 255);
    // Orange (secondary) full-tiles — matches FemaleSecondaryLight / FemaleSecondaryDark.
    private static readonly Color32 SecondaryLightColor    = new Color32(255, 201, 117, 255);
    private static readonly Color32 SecondaryDarkColor     = new Color32(255, 161, 0,   255);
    // Dimmed versions used when viewing a non-floor layer.
    private static readonly Color32 PrimaryLightDimColor   = new Color32(83,  32,  64,  255);
    private static readonly Color32 PrimaryDarkDimColor    = new Color32(88,  0,   40,  255);
    private static readonly Color32 SecondaryLightDimColor = new Color32(89,  70,  41,  255);
    private static readonly Color32 SecondaryDarkDimColor  = new Color32(89,  56,  0,   255);
    // Slope tiles — neutral grey, dimmed by depth below the current viewing layer.
    private static readonly Color32 SlopeColor             = new Color32(90,  85,  80,  255);
    private static readonly Color32 FurnitureColor         = new Color32(220, 180, 80,  230);
    private static readonly Color32 FurnitureSelectedColor = new Color32(255, 100, 60,  255);
    private static readonly Color32 PlayerMarkerColor      = new Color32(255, 255, 255, 255);
    private static readonly Color32 SurfaceUnavailableColor = new Color32(35,  35,  45,  255);

    // ── State ─────────────────────────────────────────────────────────────────────
    private TerrainGridAuthoring terrain;
    private IReadOnlyList<PlacedFurnitureRecord> placedFurniture;
    private Texture2D minimapTexture;
    private RawImage rawImage;
    private PlayerMotor playerMotor;

    // Layer system
    private List<float> availableLayers = new List<float>();   // sorted unique heights; index 0 = floor (0f)
    private int currentLayerIndex;                              // 0 = floor/terrain level

    // Windowed view — 11×11 full tiles centred on the player.
    private Vector2Int viewOriginSubtile;   // subtile-space top-left of the visible window
    private Vector2Int lastPlayerFullTile;  // cached tile used to detect movement

    // Drag state
    private PlacedFurnitureRecord dragging;
    private Vector2Int dragOffsetCells;      // click point relative to furniture origin in cell space
    private bool isDragging;
    // True when the dragged piece is a host (female-at-layer) and should move on the floor,
    // with all pieces sitting on its surface following along.
    private bool dragIsFloorMove;

    // Debug overlay state (shown via OnGUI while dragging)
    private string debugDragStatus = "";

    // Surface layer — populated each Rebuild when currentLayerIndex > 0.
    // Maps terrain-subtile cell → info about the female-grid cell at the current layer height.
    private readonly Dictionary<Vector2Int, SurfaceCell> surfaceMap = new Dictionary<Vector2Int, SurfaceCell>();

    private struct SurfaceCell
    {
        public PlacedFurnitureRecord Host;
        public FemaleGridLayer       Layer;
        public bool                  Enabled;
        public float                 WorldSurfaceY;  // absolute world Y of this surface
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        rawImage = GetComponent<RawImage>();
    }

    private void OnEnable()
    {
        if (layerUpButton != null) layerUpButton.onClick.AddListener(LayerUp);
        if (layerDownButton != null) layerDownButton.onClick.AddListener(LayerDown);
    }

    private void OnDisable()
    {
        if (layerUpButton != null) layerUpButton.onClick.RemoveListener(LayerUp);
        if (layerDownButton != null) layerDownButton.onClick.RemoveListener(LayerDown);
    }

    private void Update()
    {
        if (playerMotor == null || terrain == null) return;
        Vector2Int current = playerMotor.IsInDecorateMode
            ? playerMotor.CurrentFullTile
            : terrain.TryGetNearestWalkableFullTile(playerMotor.transform.position, out Vector2Int nearestTile)
                ? nearestTile
                : lastPlayerFullTile;
        if (current != lastPlayerFullTile)
        {
            lastPlayerFullTile = current;
            Rebuild();
        }

        if (isDragging && dragging != null)
            TryNudgeDragging();
    }

    /// <summary>
    /// Moves the selected piece one subtile cell (0.5 world units) in the pressed arrow direction.
    /// The move is only applied if the new position passes all placement validation checks.
    /// </summary>
    private void TryNudgeDragging()
    {
        Vector2Int nudge = Vector2Int.zero;
        if      (Input.GetKeyDown(KeyCode.UpArrow))    nudge = new Vector2Int( 0,  1);
        else if (Input.GetKeyDown(KeyCode.DownArrow))  nudge = new Vector2Int( 0, -1);
        else if (Input.GetKeyDown(KeyCode.LeftArrow))  nudge = new Vector2Int(-1,  0);
        else if (Input.GetKeyDown(KeyCode.RightArrow)) nudge = new Vector2Int( 1,  0);
        if (nudge == Vector2Int.zero) return;

        PlaceableGridAuthoring auth = dragging.Instance;
        if (auth == null) return;

        // Derive the current terrain-cell origin of the piece (same math as OnPointerDown).
        Vector2 maleOffset = auth.MaleGridOriginLocalOffset;
        float tileSize = auth.FemaleTileSize;
        Transform t = auth.transform;
        Vector3 worldCell00 = t.position
            + t.right   * (maleOffset.x + 0.5f * tileSize)
            + t.forward * (maleOffset.y + 0.5f * tileSize);
        if (!terrain.TryWorldToCell(worldCell00, out Vector2Int currentOrigin)) return;

        Vector2Int newOrigin = currentOrigin + nudge;

        if (dragIsFloorMove)
        {
            List<PlacedFurnitureRecord> children = GatherAllChildren(dragging);
            if (DragOnFloor(auth, newOrigin, children, out Vector3 delta) && delta != Vector3.zero)
                MoveChildrenByDelta(children, delta);
        }
        else if (currentLayerIndex > 0 && surfaceMap.Count > 0)
        {
            DragOnSurface(auth, newOrigin);
        }
        else
        {
            DragOnFloor(auth, newOrigin, null, out _);
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────────

    /// <summary>Called by BuildModeController when decorate mode is entered.</summary>
    public void Initialise(TerrainGridAuthoring terrainGrid, IReadOnlyList<PlacedFurnitureRecord> furniture, PlayerMotor motor)
    {
        terrain = terrainGrid;
        placedFurniture = furniture;
        playerMotor = motor;
        if (playerMotor != null)
        {
            lastPlayerFullTile = playerMotor.IsInDecorateMode
                ? playerMotor.CurrentFullTile
                : terrain.TryGetNearestWalkableFullTile(playerMotor.transform.position, out Vector2Int t0) ? t0 : default;
        }
        RefreshAvailableLayers();
        currentLayerIndex = FindLayerForPlayerTile();
        Rebuild();
    }

    /// <summary>Call after a new furniture piece was placed to refresh the minimap.</summary>
    public void OnFurniturePlaced(PlacedFurnitureRecord record)
    {
        RefreshAvailableLayers();
        Rebuild();
    }

    /// <summary>Call after a furniture piece was removed to refresh the minimap and prune empty layers.</summary>
    public void OnFurnitureRemoved()
    {
        RefreshAvailableLayers();   // may reduce availableLayers; currentLayerIndex is clamped inside
        Rebuild();
    }

    /// <summary>Redraws the entire minimap from current terrain + furniture state.</summary>
    public void Rebuild()
    {
        if (terrain == null) return;

        RefreshAvailableLayers(); // always keep layer list in sync (handles drag-off, removal, etc.)
        RefreshViewOrigin();
        BuildSurfaceMap();       // must be first so IsRecordOnLayer/HitTest can use it
        EnsureTexture();
        DrawTerrain();
        DrawSurfaceCells();      // overlay enabled/disabled female-grid cells when on a higher layer
        DrawFurniture();
        DrawPlayerMarker();
        minimapTexture.Apply();

        UpdateLayerLabel();
        UpdateLayerButtonStates();
    }

    // ── Layer buttons ─────────────────────────────────────────────────────────────

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

    private float CurrentLayerHeight => availableLayers.Count > 0 ? availableLayers[currentLayerIndex] : 0f;

    // ── IPointerDownHandler ───────────────────────────────────────────────────────

    public void OnPointerDown(PointerEventData eventData)
    {
        if (terrain == null || placedFurniture == null) return;

        // Right-click = rotate; left-click = drag.
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            PlacedFurnitureRecord hit = HitTest(eventData.position, out _);
            if (hit != null)
            {
                RotateFurniture90(hit);
            }
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            PlacedFurnitureRecord hit = HitTest(eventData.position, out Vector2Int hitCell);
            if (hit != null)
            {
                // Compute current origin so the drag offset is based on where the furniture actually is.
                PlaceableGridAuthoring auth = hit.Instance;
                Vector2 maleOffset = auth.MaleGridOriginLocalOffset;
                float tileSize = auth.FemaleTileSize;
                Transform t = auth.transform;
                Vector3 worldCell00 = t.position + t.right * (maleOffset.x + 0.5f * tileSize) + t.forward * (maleOffset.y + 0.5f * tileSize);
                terrain.TryWorldToCell(worldCell00, out Vector2Int currentOrigin);

                // Determine whether the hit piece sits on terrain (floor piece that
                // carries children) or on another furniture's female surface (surface piece).
                // This check is purely physical — independent of which minimap layer is viewed.
                dragIsFloorMove = !IsSittingOnFurniture(hit);

                dragging = hit;
                dragOffsetCells = hitCell - currentOrigin;
                isDragging = true;
            }
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
        dragging = null;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || dragging == null || terrain == null) return;
        if (!TryScreenToTerrainCell(eventData.position, out Vector2Int pointerCell)) return;

        PlaceableGridAuthoring auth = dragging.Instance;
        if (auth == null) return;

        Vector2Int newOrigin = pointerCell - dragOffsetCells;

        if (dragIsFloorMove)
        {
            List<PlacedFurnitureRecord> children = GatherAllChildren(dragging);
            debugDragStatus = $"floorMove host='{auth.name}' origin={newOrigin} children={children.Count}";
            if (DragOnFloor(auth, newOrigin, children, out Vector3 delta) && delta != Vector3.zero)
                MoveChildrenByDelta(children, delta);
        }
        else if (currentLayerIndex > 0 && surfaceMap.Count > 0)
        {
            debugDragStatus = $"surfaceDrag piece='{auth.name}' layerIdx={currentLayerIndex}";
            DragOnSurface(auth, newOrigin);
        }
        else
        {
            debugDragStatus = $"floorDrag piece='{auth.name}' origin={newOrigin}";
            DragOnFloor(auth, newOrigin, null, out _);
        }
    }

    // Returns true and outputs the world-space position delta if the host was successfully moved.
    // alsoExclude: records whose XZ projections should be ignored in the occupancy test (children).
    // Always checks whether the footprint can snap to another furniture surface first.
    private bool DragOnFloor(PlaceableGridAuthoring auth, Vector2Int newOrigin,
                             ICollection<PlacedFurnitureRecord> alsoExclude, out Vector3 delta)
    {
        delta = Vector3.zero;

        // Cross-layer surface snap: if every male cell lands on an enabled female-grid cell of
        // some other piece, elevate to that surface Y instead of staying on the terrain floor.
        // This is checked for all drags, including host-pieces with children (alsoExclude != null).
        // Children are passed as exclusions so they can't act as hosts for their own parent.
        if (TryFindSurfaceUnderFootprint(auth, newOrigin, dragging, alsoExclude, out float snapSurfaceY))
        {
            if (!terrain.TryGetCellCenterWorld(newOrigin.x, newOrigin.y, out Vector3 snapCell00)) return false;
            Vector3 oldPosSurf = auth.transform.position;
            Vector2 snapOffset = auth.MaleGridOriginLocalOffset;
            float snapTile = auth.FemaleTileSize;
            Vector3 snapPos = snapCell00
                - auth.transform.right   * (snapOffset.x + 0.5f * snapTile)
                - auth.transform.forward * (snapOffset.y + 0.5f * snapTile);
            snapPos.y = snapSurfaceY - auth.MaleGridFloorLocalY;
            auth.transform.position = snapPos;
            delta = auth.transform.position - oldPosSurf;
            Rebuild();
            return true;
        }

        // Build occupied cells from every placed piece except the one being dragged and its children.
        // On floor-layer drags, only consider pieces on the same terrain level.
        var occupied = new HashSet<Vector2Int>();
        int sourceLevel = 0;
        bool hasSourceLevel = currentLayerIndex != 0 || TryGetPieceTerrainLevel(auth, out sourceLevel);
        foreach (PlacedFurnitureRecord record in placedFurniture)
        {
            if (record == dragging || record.Instance == null) continue;
            if (alsoExclude != null && alsoExclude.Contains(record)) continue;
            if (currentLayerIndex == 0)
            {
                if (!hasSourceLevel) return false;
                if (!TryGetPieceTerrainLevel(record.Instance, out int recLevel)) continue;
                if (recLevel != sourceLevel) continue;
            }
            Vector2Int recSize = record.Instance.MaleGridSizeInCells;
            float recTileSize = record.Instance.FemaleTileSize;
            Vector2 recOffset = record.Instance.MaleGridOriginLocalOffset;
            Transform rt = record.Instance.transform;
            for (int rz = 0; rz < recSize.y; rz++)
            {
                for (int rx = 0; rx < recSize.x; rx++)
                {
                    if (!record.Instance.GetMaleCell(rx, rz)) continue;
                    float lx = recOffset.x + (rx + 0.5f) * recTileSize;
                    float lz = recOffset.y + (rz + 0.5f) * recTileSize;
                    Vector3 cellWorld = rt.position + rt.right * lx + rt.forward * lz;
                    if (terrain.TryWorldToCell(cellWorld, out Vector2Int tc))
                        occupied.Add(tc);
                }
            }
        }

        // Compute which terrain cells the piece would occupy at newOrigin, honouring rotation.
        List<Vector2Int> placedCells = GetRotatedCellsForOrigin(auth, newOrigin);
        if (placedCells == null) return false;
        int subtPerFull = terrain.SubtilesPerFullTile;
        foreach (Vector2Int tc in placedCells)
        {
            if (!terrain.GetCell(tc.x, tc.y)) return false;
            if (occupied.Contains(tc)) return false;
            // On the floor layer, keep movement on the dragged piece's current terrain level.
            // This avoids hardcoding "level 0" as the only valid floor.
            if (currentLayerIndex == 0)
            {
                int ftx = tc.x / subtPerFull;
                int ftz = tc.y / subtPerFull;
                if (!hasSourceLevel) return false;
                if (terrain.GetTileLevel(ftx, ftz) != sourceLevel) return false;
            }
        }

        if (!terrain.TryGetCellCenterWorld(newOrigin.x, newOrigin.y, out Vector3 cell00Center)) return false;

        Vector3 oldPos = auth.transform.position;
        Vector2 maleOffset = auth.MaleGridOriginLocalOffset;
        float tileSize = auth.FemaleTileSize;
        // TryGetCellCenterWorld now returns the correct elevated Y for raised tiles,
        // so the Y arithmetic here is correct for all height levels.
        auth.transform.position = cell00Center
            - auth.transform.right   * (maleOffset.x + 0.5f * tileSize)
            - auth.transform.forward * (maleOffset.y + 0.5f * tileSize)
            + Vector3.up * (-auth.MaleGridFloorLocalY);
        delta = auth.transform.position - oldPos;
        Rebuild();
        return true;
    }

    private void DragOnSurface(PlaceableGridAuthoring auth, Vector2Int newOrigin)
    {
        // Compute which terrain cells this piece would occupy at newOrigin (rotation-aware).
        List<Vector2Int> placedCells = GetRotatedCellsForOrigin(auth, newOrigin);
        float worldSurfaceY = float.NaN;
        bool surfaceValid = placedCells != null;

        if (surfaceValid)
        {
            foreach (Vector2Int tc in placedCells)
            {
                if (!surfaceMap.TryGetValue(tc, out SurfaceCell sc) || !sc.Enabled)
                    { surfaceValid = false; break; }
                if (float.IsNaN(worldSurfaceY))
                    worldSurfaceY = sc.WorldSurfaceY;
            }
        }

        if (!surfaceValid || float.IsNaN(worldSurfaceY))
        {
            // Piece has moved off the surface — try placing it on the terrain floor
            // (or on another surface via TryFindSurfaceUnderFootprint inside DragOnFloor).
            DragOnFloor(auth, newOrigin, null, out _);
            return;
        }

        // Occupancy check — if blocked by another piece on the surface, don't move.
        HashSet<Vector2Int> occupied = BuildSurfaceOccupancy(worldSurfaceY, dragging);
        foreach (Vector2Int tc in placedCells)
            if (occupied.Contains(tc)) return;

        // Move — XZ from terrain cell centre, Y sits collider floor exactly on surface.
        if (terrain.TryGetCellCenterWorld(newOrigin.x, newOrigin.y, out Vector3 cell00Center))
        {
            Vector2 maleOffset = auth.MaleGridOriginLocalOffset;
            float tileSize = auth.FemaleTileSize;
            Vector3 newPos = cell00Center
                - auth.transform.right   * (maleOffset.x + 0.5f * tileSize)
                - auth.transform.forward * (maleOffset.y + 0.5f * tileSize);
            newPos.y = worldSurfaceY - auth.MaleGridFloorLocalY;
            auth.transform.position = newPos;
            Rebuild();
        }
    }

    // Returns the terrain level currently under this piece (based on the first enabled male cell).
    private bool TryGetPieceTerrainLevel(PlaceableGridAuthoring auth, out int level)
    {
        level = 0;
        Vector2Int maleSize = auth.MaleGridSizeInCells;
        float tileSize = auth.FemaleTileSize;
        Vector2 maleOffset = auth.MaleGridOriginLocalOffset;
        Transform t = auth.transform;
        int s = terrain.SubtilesPerFullTile;

        for (int z = 0; z < maleSize.y; z++)
        {
            for (int x = 0; x < maleSize.x; x++)
            {
                if (!auth.GetMaleCell(x, z)) continue;
                float lx = maleOffset.x + (x + 0.5f) * tileSize;
                float lz = maleOffset.y + (z + 0.5f) * tileSize;
                Vector3 world = t.position + t.right * lx + t.forward * lz;
                if (!terrain.TryWorldToCell(world, out Vector2Int tc)) continue;
                level = terrain.GetTileLevel(tc.x / s, tc.y / s);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns true if 'piece' is physically sitting on another furniture's female-grid surface
    /// (as opposed to sitting on the terrain). Checks Y-height match AND XZ cell overlap.
    /// </summary>
    private bool IsSittingOnFurniture(PlacedFurnitureRecord piece)
    {
        PlaceableGridAuthoring auth = piece.Instance;
        if (auth == null) return false;
        float baseY = auth.transform.position.y + auth.MaleGridFloorLocalY;

        // Get one male cell of this piece for XZ overlap testing.
        Vector2Int? pieceCell = null;
        Vector2Int maleSize = auth.MaleGridSizeInCells;
        float tileSize = auth.FemaleTileSize;
        Vector2 maleOffset = auth.MaleGridOriginLocalOffset;
        Transform pt = auth.transform;
        for (int z = 0; z < maleSize.y && !pieceCell.HasValue; z++)
            for (int x = 0; x < maleSize.x && !pieceCell.HasValue; x++)
            {
                if (!auth.GetMaleCell(x, z)) continue;
                float lx = maleOffset.x + (x + 0.5f) * tileSize;
                float lz = maleOffset.y + (z + 0.5f) * tileSize;
                Vector3 w = pt.position + pt.right * lx + pt.forward * lz;
                if (terrain.TryWorldToCell(w, out Vector2Int tc))
                    pieceCell = tc;
            }
        if (!pieceCell.HasValue) return false;

        const float yTol = 0.1f;
        foreach (PlacedFurnitureRecord host in placedFurniture)
        {
            if (host == piece || host.Instance == null) continue;
            PlaceableGridAuthoring hostAuth = host.Instance;
            foreach (FemaleGridLayer layer in hostAuth.EnumerateFemaleLayers())
            {
                float worldSY = hostAuth.transform.position.y + hostAuth.MaleGridFloorLocalY + layer.LocalHeight;
                if (Mathf.Abs(baseY - worldSY) > yTol) continue;
                HashSet<Vector2Int> femaleCells = GetFemaleLayerCells(hostAuth, layer, enabledOnly: false);
                if (ContainsCellOrNeighbor(femaleCells, pieceCell.Value))
                    return true;
            }
        }
        return false;
    }

    /// <summary>Builds the set of terrain XZ cells occupied by pieces sitting at the given world Y.</summary>
    private HashSet<Vector2Int> BuildSurfaceOccupancy(float worldSurfaceY, PlacedFurnitureRecord excluded)
    {
        var occupied = new HashSet<Vector2Int>();
        const float yTolerance = 0.1f;

        foreach (PlacedFurnitureRecord record in placedFurniture)
        {
            if (record == excluded || record.Instance == null) continue;

            PlaceableGridAuthoring auth = record.Instance;
            float baseY = auth.transform.position.y + auth.MaleGridFloorLocalY;
            if (Mathf.Abs(baseY - worldSurfaceY) > yTolerance) continue;

            Vector2Int maleSize = auth.MaleGridSizeInCells;
            float tileSize = auth.FemaleTileSize;
            Vector2 maleOffset = auth.MaleGridOriginLocalOffset;
            Transform t = auth.transform;

            for (int z = 0; z < maleSize.y; z++)
            {
                for (int x = 0; x < maleSize.x; x++)
                {
                    if (!auth.GetMaleCell(x, z)) continue;
                    float lx = maleOffset.x + (x + 0.5f) * tileSize;
                    float lz = maleOffset.y + (z + 0.5f) * tileSize;
                    Vector3 cellWorld = t.position + t.right * lx + t.forward * lz;
                    if (terrain.TryWorldToCell(cellWorld, out Vector2Int tc))
                        occupied.Add(tc);
                }
            }
        }

        return occupied;
    }

    // ── Rotation-aware grid projection helpers ───────────────────────────────────

    /// <summary>
    /// Computes the terrain cells 'auth' would occupy if its male-grid (0,0) were placed at
    /// terrain cell 'newOrigin', honouring the piece's current world rotation.
    /// Returns null if the cell-centre lookup fails or any cell projects outside the terrain.
    /// </summary>
    private List<Vector2Int> GetRotatedCellsForOrigin(PlaceableGridAuthoring auth, Vector2Int newOrigin)
    {
        if (!terrain.TryGetCellCenterWorld(newOrigin.x, newOrigin.y, out Vector3 cell00Center))
            return null;
        Vector2 maleOffset = auth.MaleGridOriginLocalOffset;
        float tileSize = auth.FemaleTileSize;
        Transform t = auth.transform;
        // Pivot position when cell (0,0) sits at newOrigin.
        Vector3 pivot = cell00Center
            - t.right   * (maleOffset.x + 0.5f * tileSize)
            - t.forward * (maleOffset.y + 0.5f * tileSize);
        var result = new List<Vector2Int>();
        Vector2Int maleSize = auth.MaleGridSizeInCells;
        for (int z = 0; z < maleSize.y; z++)
            for (int x = 0; x < maleSize.x; x++)
            {
                if (!auth.GetMaleCell(x, z)) continue;
                float lx = maleOffset.x + (x + 0.5f) * tileSize;
                float lz = maleOffset.y + (z + 0.5f) * tileSize;
                Vector3 worldCenter = pivot + t.right * lx + t.forward * lz;
                if (!terrain.TryWorldToCell(worldCenter, out Vector2Int tc)) return null;
                result.Add(tc);
            }
        return result;
    }

    /// <summary>
    /// Returns the terrain cells covered by 'layer' on the given piece,
    /// using the piece's current world rotation. Pass enabledOnly = true to skip
    /// cells the furniture author has disabled (holes in the surface pattern).
    /// </summary>
    private HashSet<Vector2Int> GetFemaleLayerCells(PlaceableGridAuthoring auth, FemaleGridLayer layer,
        bool enabledOnly)
    {
        var cells = new HashSet<Vector2Int>();
        Transform t = auth.transform;
        Vector2 maleOffset = auth.MaleGridOriginLocalOffset;
        float tileSize = auth.FemaleTileSize;
        Vector2Int gridSize = layer.GridSizeInCells;
        for (int fz = 0; fz < gridSize.y; fz++)
            for (int fx = 0; fx < gridSize.x; fx++)
            {
                if (enabledOnly && !layer.GetCell(fx, fz)) continue;
                float lx = maleOffset.x + (fx + 0.5f) * tileSize;
                float lz = maleOffset.y + (fz + 0.5f) * tileSize;
                Vector3 worldCenter = t.position + t.right * lx + t.forward * lz;
                if (terrain.TryWorldToCell(worldCenter, out Vector2Int tc))
                    cells.Add(tc);
            }
        return cells;
    }

    // ── Cross-layer surface-snap helper ──────────────────────────────────────────

    /// <summary>
    /// Checks whether all active male cells of 'auth' placed at 'newOrigin' would fall entirely
    /// within the enabled female-grid cells of a single host piece (any layer, any height).
    /// If so, returns true and sets snappedSurfaceY to the world Y of that surface.
    /// Also validates that those cells aren't already occupied by other pieces.
    /// </summary>
    private bool TryFindSurfaceUnderFootprint(PlaceableGridAuthoring auth, Vector2Int newOrigin,
        PlacedFurnitureRecord draggedRecord, ICollection<PlacedFurnitureRecord> alsoExclude,
        out float snappedSurfaceY)
    {
        snappedSurfaceY = float.NaN;

        // Collect the terrain cells the dragged piece would occupy at newOrigin (rotation-aware).
        List<Vector2Int> footprintList = GetRotatedCellsForOrigin(auth, newOrigin);
        if (footprintList == null || footprintList.Count == 0)
        {
            debugDragStatus += " | snap: footprint empty";
            return false;
        }
        var footprint = new HashSet<Vector2Int>(footprintList);
        bool hasDragLevel = TryGetPieceTerrainLevel(auth, out int dragLevel);

        foreach (PlacedFurnitureRecord host in placedFurniture)
        {
            if (host == draggedRecord || host.Instance == null) continue;
            // Skip children of the dragged piece — they can't act as a surface host for their
            // own parent (that would cause the piece to snap onto something it's carrying).
            if (alsoExclude != null && alsoExclude.Contains(host)) continue;

            // For floor drags, only snap onto hosts on the same terrain level.
            if (currentLayerIndex == 0)
            {
                if (!hasDragLevel) continue;
                if (!TryGetPieceTerrainLevel(host.Instance, out int hostLevel)) continue;
                if (hostLevel != dragLevel) continue;
            }

            PlaceableGridAuthoring hostAuth = host.Instance;
            Transform t = hostAuth.transform;

            bool hasAnyLayer = false;
            foreach (FemaleGridLayer layer in hostAuth.EnumerateFemaleLayers())
            {
                hasAnyLayer = true;
                float worldSY = t.position.y + hostAuth.MaleGridFloorLocalY + layer.LocalHeight;

                // Build the enabled-cell set for this female layer (rotation-aware).
                HashSet<Vector2Int> enabledCells = GetFemaleLayerCells(hostAuth, layer, enabledOnly: true);

                if (enabledCells.Count == 0)
                {
                    debugDragStatus += $" | host '{hostAuth.name}' layer '{layer.Name}' 0 enabled";
                    continue;
                }

                // All footprint cells must be within enabled cells of this layer.
                bool allFit = true;
                foreach (Vector2Int tc in footprint)
                {
                    if (!ContainsCellOrNeighbor(enabledCells, tc))
                    {
                        debugDragStatus += $" | cell {tc} not in '{hostAuth.name}' (has {enabledCells.Count})";
                        allFit = false;
                        break;
                    }
                }
                if (!allFit) continue;

                // Surface must not be blocked by other pieces already sitting there.
                HashSet<Vector2Int> occupied = BuildSurfaceOccupancy(worldSY, draggedRecord);
                bool anyBlocked = false;
                foreach (Vector2Int tc in footprint)
                    if (occupied.Contains(tc)) { anyBlocked = true; break; }
                if (anyBlocked) continue;

                snappedSurfaceY = worldSY;
                debugDragStatus += $" | SNAPPED to '{hostAuth.name}' surfY={worldSY:F2}";
                return true;
            }

            if (!hasAnyLayer)
                debugDragStatus += $" | host '{hostAuth.name}' NO layers (floorY={hostAuth.MaleGridFloorLocalY:F3})";
        }
        debugDragStatus += $" | no host matched (placed={placedFurniture.Count})";
        return false;
    }

    // ── Host-floor-drag helpers ───────────────────────────────────────────────────

    /// <summary>World Y of the host's female layer at the current layer height.</summary>
    private float ComputeHostSurfaceY(PlacedFurnitureRecord host)
    {
        float targetWorldY = CurrentLayerHeight;
        PlaceableGridAuthoring auth = host.Instance;
        Transform t = auth.transform;

        foreach (FemaleGridLayer layer in auth.EnumerateFemaleLayers())
        {
            float worldSY = t.position.y + auth.MaleGridFloorLocalY + layer.LocalHeight;
            if (Mathf.Abs(worldSY - targetWorldY) < 0.01f)
                return worldSY;
        }
        return float.NaN;
    }

    /// <summary>Terrain XZ cells covered by the host's female layer at the given surface Y.</summary>
    private HashSet<Vector2Int> ComputeHostFemaleCells(PlacedFurnitureRecord host, float surfaceY)
    {
        var cells = new HashSet<Vector2Int>();
        float targetWorldY = CurrentLayerHeight;
        PlaceableGridAuthoring auth = host.Instance;
        Transform t = auth.transform;

        foreach (FemaleGridLayer layer in auth.EnumerateFemaleLayers())
        {
            float worldSY = t.position.y + auth.MaleGridFloorLocalY + layer.LocalHeight;
            if (Mathf.Abs(worldSY - targetWorldY) > 0.01f) continue;
            cells.UnionWith(GetFemaleLayerCells(auth, layer, enabledOnly: false));
        }
        return cells;
    }

    /// <summary>
    /// Returns all placed pieces (excluding the host) whose male grid intersects the host's female
    /// cells and whose collider floor sits at the host's surface height.
    /// </summary>
    private List<PlacedFurnitureRecord> GatherChildren(
        HashSet<Vector2Int> hostFemaleCells, float hostSurfaceY, PlacedFurnitureRecord host)
    {
        var result = new List<PlacedFurnitureRecord>();
        if (float.IsNaN(hostSurfaceY) || hostFemaleCells.Count == 0) return result;
        const float yTolerance = 0.1f;

        foreach (PlacedFurnitureRecord record in placedFurniture)
        {
            if (record == host || record.Instance == null) continue;
            PlaceableGridAuthoring auth = record.Instance;
            float baseY = auth.transform.position.y + auth.MaleGridFloorLocalY;
            if (Mathf.Abs(baseY - hostSurfaceY) > yTolerance) continue;

            // Check if any of its male cells fall on the host's female cells.
            Vector2Int maleSize = auth.MaleGridSizeInCells;
            float tileSize = auth.FemaleTileSize;
            Vector2 maleOffset = auth.MaleGridOriginLocalOffset;
            Transform t = auth.transform;
            bool isChild = false;

            for (int z = 0; z < maleSize.y && !isChild; z++)
            {
                for (int x = 0; x < maleSize.x && !isChild; x++)
                {
                    if (!auth.GetMaleCell(x, z)) continue;
                    float lx = maleOffset.x + (x + 0.5f) * tileSize;
                    float lz = maleOffset.y + (z + 0.5f) * tileSize;
                    Vector3 cellWorld = t.position + t.right * lx + t.forward * lz;
                    if (terrain.TryWorldToCell(cellWorld, out Vector2Int tc) && hostFemaleCells.Contains(tc))
                        isChild = true;
                }
            }

            if (isChild) result.Add(record);
        }
        return result;
    }

    private static void MoveChildrenByDelta(List<PlacedFurnitureRecord> children, Vector3 delta)
    {
        foreach (PlacedFurnitureRecord child in children)
            if (child.Instance != null)
                child.Instance.transform.position += delta;
    }

    // World-to-cell projection can land on an adjacent cell near boundaries due to float precision.
    // Treat immediate neighbours as equivalent when matching support relationships.
    private static bool ContainsCellOrNeighbor(HashSet<Vector2Int> cells, Vector2Int tc)
    {
        if (cells.Contains(tc)) return true;
        if (cells.Contains(new Vector2Int(tc.x + 1, tc.y))) return true;
        if (cells.Contains(new Vector2Int(tc.x - 1, tc.y))) return true;
        if (cells.Contains(new Vector2Int(tc.x, tc.y + 1))) return true;
        if (cells.Contains(new Vector2Int(tc.x, tc.y - 1))) return true;
        if (cells.Contains(new Vector2Int(tc.x + 1, tc.y + 1))) return true;
        if (cells.Contains(new Vector2Int(tc.x + 1, tc.y - 1))) return true;
        if (cells.Contains(new Vector2Int(tc.x - 1, tc.y + 1))) return true;
        if (cells.Contains(new Vector2Int(tc.x - 1, tc.y - 1))) return true;
        return false;
    }

    /// <summary>
    /// Returns all placed pieces (excluding host) that sit on ANY of the host's female layers,
    /// regardless of which layer is currently viewed.
    /// </summary>
    /// <summary>
    /// Returns every piece in the transitive closure of "sits on this piece or anything stacked on it".
    /// Breadth-first: direct children of the root host, then their children, etc.
    /// Works for any number of stacking layers.
    /// </summary>
    private List<PlacedFurnitureRecord> GatherAllChildren(PlacedFurnitureRecord host)
    {
        var result   = new List<PlacedFurnitureRecord>();
        var visited  = new HashSet<PlacedFurnitureRecord> { host };
        var queue    = new Queue<PlacedFurnitureRecord>();
        queue.Enqueue(host);

        while (queue.Count > 0)
        {
            PlacedFurnitureRecord current = queue.Dequeue();
            PlaceableGridAuthoring currentAuth = current.Instance;
            if (currentAuth == null) continue;

            Transform ct = currentAuth.transform;

            // Build the set of (femaleCells, worldSurfaceY) for every layer of this piece.
            var layers = new List<(HashSet<Vector2Int> cells, float surfaceY)>();
            foreach (FemaleGridLayer layer in currentAuth.EnumerateFemaleLayers())
            {
                float worldSY = ct.position.y + currentAuth.MaleGridFloorLocalY + layer.LocalHeight;
                HashSet<Vector2Int> cells = GetFemaleLayerCells(currentAuth, layer, enabledOnly: false);
                debugDragStatus += $" | layer '{layer.Name}' cells={cells.Count} y={worldSY:F2}";
                if (cells.Count > 0)
                    layers.Add((cells, worldSY));
            }
            if (layers.Count == 0)
            {
                debugDragStatus += $" | '{currentAuth.name}' has 0 layers with cells";
                continue;
            }

            const float yTolerance = 0.1f;

            foreach (PlacedFurnitureRecord record in placedFurniture)
            {
                if (visited.Contains(record) || record.Instance == null) continue;

                PlaceableGridAuthoring auth = record.Instance;
                float baseY = auth.transform.position.y + auth.MaleGridFloorLocalY;

                HashSet<Vector2Int> matchedCells = null;
                foreach (var (cells, surfaceY) in layers)
                {
                    if (Mathf.Abs(baseY - surfaceY) < yTolerance)
                    {
                        matchedCells = cells;
                        break;
                    }
                }
                if (matchedCells == null) continue;

                // Check if any male cell of this record overlaps the matched female cells.
                Vector2Int maleSize = auth.MaleGridSizeInCells;
                float tileSize      = auth.FemaleTileSize;
                Vector2 maleOffset  = auth.MaleGridOriginLocalOffset;
                Transform t         = auth.transform;
                bool isChild        = false;

                for (int z = 0; z < maleSize.y && !isChild; z++)
                    for (int x = 0; x < maleSize.x && !isChild; x++)
                    {
                        if (!auth.GetMaleCell(x, z)) continue;
                        float lx = maleOffset.x + (x + 0.5f) * tileSize;
                        float lz = maleOffset.y + (z + 0.5f) * tileSize;
                        Vector3 cellWorld = t.position + t.right * lx + t.forward * lz;
                        if (terrain.TryWorldToCell(cellWorld, out Vector2Int tc) && ContainsCellOrNeighbor(matchedCells, tc))
                            isChild = true;
                    }

                if (!isChild) continue;

                visited.Add(record);
                result.Add(record);
                // Enqueue so its own children are also gathered.
                queue.Enqueue(record);
            }
        }
        return result;
    }

    // ── Private drawing ───────────────────────────────────────────────────────────

    private void EnsureTexture()
    {
        int dim = ViewSizeCells * pixelsPerCell;
        if (minimapTexture == null || minimapTexture.width != dim || minimapTexture.height != dim)
        {
            if (minimapTexture != null) Destroy(minimapTexture);
            minimapTexture = new Texture2D(dim, dim, TextureFormat.RGBA32, false);
            minimapTexture.filterMode = FilterMode.Point;
            rawImage.texture = minimapTexture;
        }
    }

    private int ViewDiameter => viewHalfSize * 2 + 1;
    private int ViewSizeCells => ViewDiameter * terrain.SubtilesPerFullTile;

    private void RefreshViewOrigin()
    {
        if (playerMotor == null || terrain == null) return;
        int s = terrain.SubtilesPerFullTile;
        Vector2Int pft = playerMotor.IsInDecorateMode
            ? playerMotor.CurrentFullTile
            : terrain.TryGetNearestWalkableFullTile(playerMotor.transform.position, out Vector2Int nearestTile)
                ? nearestTile
                : lastPlayerFullTile;
        viewOriginSubtile = new Vector2Int((pft.x - viewHalfSize) * s, (pft.y - viewHalfSize) * s);
    }

    // ── Surface map ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Populates surfaceMap with every female-grid cell (from all placed furniture)
    /// whose authored LocalHeight matches the current layer height.
    /// Each entry maps a terrain-subtile cell index to metadata about the surface cell.
    /// </summary>
    private void BuildSurfaceMap()
    {
        surfaceMap.Clear();
        if (currentLayerIndex == 0) return;

        float targetHeight = CurrentLayerHeight;

        if (placedFurniture != null)
        {
            foreach (PlacedFurnitureRecord record in placedFurniture)
            {
                if (record.Instance == null) continue;

                PlaceableGridAuthoring auth = record.Instance;
                Transform t = auth.transform;
                float tileSize = auth.FemaleTileSize;
                Vector2 maleOffset = auth.MaleGridOriginLocalOffset;

                foreach (FemaleGridLayer layer in auth.EnumerateFemaleLayers())
                {
                    // Compare world surface Y of this layer against the current world-Y target.
                    float worldSurfaceY = t.position.y + auth.MaleGridFloorLocalY + layer.LocalHeight;
                    if (Mathf.Abs(worldSurfaceY - targetHeight) > 0.01f) continue;

                    // Project each female cell through the piece's rotation into terrain coordinates.
                    Vector2Int gridSize = layer.GridSizeInCells;
                    for (int fz = 0; fz < gridSize.y; fz++)
                    {
                        for (int fx = 0; fx < gridSize.x; fx++)
                        {
                            float lx = maleOffset.x + (fx + 0.5f) * tileSize;
                            float lz = maleOffset.y + (fz + 0.5f) * tileSize;
                            Vector3 worldCenter = t.position + t.right * lx + t.forward * lz;
                            if (!terrain.TryWorldToCell(worldCenter, out Vector2Int tc)) continue;
                            surfaceMap[tc] = new SurfaceCell
                            {
                                Host          = record,
                                Layer         = layer,
                                Enabled       = layer.GetCell(fx, fz),
                                WorldSurfaceY = worldSurfaceY,
                            };
                        }
                    }
                }
            }
        }

        // Populate from terrain tiles at this height level.
        // Iterate at the full-tile level to avoid any dependency on per-subtile enabledCells state.
        // Furniture surfaces (added above) take priority — only fill cells not already claimed.
        if (terrain != null)
        {
            // Compute which integer terrain level matches the current layer's world Y.
            float level0Y = terrain.GetTerrainLevelWorldY(0);
            float relativeY = targetHeight - level0Y;
            int expectedLevel = Mathf.RoundToInt(relativeY / TerrainGridAuthoring.LevelHeight);

            if (expectedLevel > 0)
            {
                Vector2Int fullSize = terrain.FullTileGridSize;
                int s = terrain.SubtilesPerFullTile;

                for (int ftz = 0; ftz < fullSize.y; ftz++)
                {
                    for (int ftx = 0; ftx < fullSize.x; ftx++)
                    {
                        if (terrain.GetTileShape(ftx, ftz) != TileShape.Flat) continue;
                        if (terrain.GetTileHeight(ftx, ftz) != expectedLevel) continue;

                        // Use the exact world Y from the tile rather than targetHeight to
                        // ensure furniture is placed at precisely the right elevation.
                        float tileSurfaceY = terrain.GetTerrainLevelWorldY(expectedLevel);

                        // All 4 subtiles of this full tile are valid placement cells.
                        for (int sz = 0; sz < s; sz++)
                        {
                            for (int sx = 0; sx < s; sx++)
                            {
                                Vector2Int key = new Vector2Int(ftx * s + sx, ftz * s + sz);
                                if (!surfaceMap.ContainsKey(key))
                                {
                                    surfaceMap[key] = new SurfaceCell
                                    {
                                        Host          = null,
                                        Layer         = default,
                                        Enabled       = true,
                                        WorldSurfaceY = tileSurfaceY,
                                    };
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>Returns the world Y of the current surface, or NaN if no surface exists.</summary>
    private float GetSurfaceWorldY()
    {
        foreach (SurfaceCell sc in surfaceMap.Values)
            return sc.WorldSurfaceY;
        return float.NaN;
    }

    /// <summary>
    /// Returns the enabled/disabled map of female-grid cells for the currently viewed layer, and
    /// their common world Y. Returns false when the floor layer is active or no surface exists.
    /// Intended for use by placement logic that wants to snap new furniture to this surface.
    /// </summary>
    public bool TryGetCurrentSurfaceInfo(
        out IReadOnlyDictionary<Vector2Int, bool> enabledCells,
        out float worldSurfaceY)
    {
        enabledCells   = null;
        worldSurfaceY  = 0f;
        if (currentLayerIndex == 0 || surfaceMap.Count == 0) return false;

        var map = new Dictionary<Vector2Int, bool>(surfaceMap.Count);
        float y = 0f;
        bool first = true;
        foreach (KeyValuePair<Vector2Int, SurfaceCell> kvp in surfaceMap)
        {
            map[kvp.Key] = kvp.Value.Enabled;
            if (first) { y = kvp.Value.WorldSurfaceY; first = false; }
        }
        enabledCells  = map;
        worldSurfaceY = y;
        return true;
    }

    // ── Private drawing ───────────────────────────────────────────────────────────

    private void DrawSurfaceCells()
    {
        if (surfaceMap.Count == 0) return;
        int s = terrain.SubtilesPerFullTile;

        foreach (KeyValuePair<Vector2Int, SurfaceCell> kvp in surfaceMap)
        {
            Vector2Int tc = kvp.Key;
            SurfaceCell sc = kvp.Value;

            // Don't draw disabled surface cells — let the dimmed terrain show through.
            // This prevents void areas of L-shaped (or otherwise shaped) furniture tops
            // from appearing as solid black rectangles.
            if (!sc.Enabled) continue;

            // Reuse the same pink/orange alternating pattern as the terrain floor.
            int tileX    = tc.x / s; int tileZ    = tc.y / s;
            int subtileX = tc.x % s; int subtileZ = tc.y % s;
            bool primary = ((tileX    + tileZ)    & 1) == 0;
            bool light   = ((subtileX + subtileZ) & 1) == 0;
            Color32 color = primary
                ? (light ? PrimaryLightColor   : PrimaryDarkColor)
                : (light ? SecondaryLightColor  : SecondaryDarkColor);

            FillCellPixels(tc.x, tc.y, color);
        }
    }

    private void DrawTerrain()
    {
        int s = terrain.SubtilesPerFullTile;
        int viewSize = ViewSizeCells;
        Vector2Int gridSize = terrain.GridSizeInCells;

        // Map the currently viewed layer's world Y to a terrain height level integer.
        float level0Y = terrain.GetTerrainLevelWorldY(0);
        int viewTerrainLevel = Mathf.RoundToInt((CurrentLayerHeight - level0Y) / TerrainGridAuthoring.LevelHeight);

        for (int vz = 0; vz < viewSize; vz++)
        {
            for (int vx = 0; vx < viewSize; vx++)
            {
                int tx = viewOriginSubtile.x + vx;
                int tz = viewOriginSubtile.y + vz;

                bool inBounds = tx >= 0 && tz >= 0 && tx < gridSize.x && tz < gridSize.y;
                Color32 color;

                if (!inBounds)
                {
                    color = DisabledCellColor;
                }
                else
                {
                    int tileX    = tx / s;  int tileZ    = tz / s;
                    int subtileX = tx % s;  int subtileZ = tz % s;
                    int tileHeight = terrain.GetTileHeight(tileX, tileZ);
                    TileShape tileShape = terrain.GetTileShape(tileX, tileZ);
                    int levelsBelow = viewTerrainLevel - tileHeight;

                    if (levelsBelow < 0)
                    {
                        // Above the current viewing layer — blank.
                        color = DisabledCellColor;
                    }
                    else if (tileShape != TileShape.Flat)
                    {
                        // Slope — not a placeable surface; dimmed grey.
                        color = DimColor(SlopeColor, LayerBrightness(levelsBelow));
                    }
                    else if (!terrain.GetCell(tx, tz))
                    {
                        // Disabled flat cell (void / manually excluded).
                        color = DisabledCellColor;
                    }
                    else
                    {
                        // Enabled flat tile: pink/orange checkerboard, brightness by depth.
                        bool primary = ((tileX    + tileZ)    & 1) == 0;
                        bool light   = ((subtileX + subtileZ) & 1) == 0;
                        Color32 baseColor = primary
                            ? (light ? PrimaryLightColor   : PrimaryDarkColor)
                            : (light ? SecondaryLightColor : SecondaryDarkColor);
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

    private void DrawFurniture()
    {
        if (placedFurniture == null) return;

        float targetHeight = CurrentLayerHeight;

        foreach (PlacedFurnitureRecord record in placedFurniture)
        {
            if (record.Instance == null) continue;
            bool isOnLayer = IsRecordOnLayer(record, targetHeight);
            if (!isOnLayer) continue;

            bool selected = (record == dragging);
            Color32 furColor = selected ? FurnitureSelectedColor : FurnitureColor;

            PlaceableGridAuthoring auth = record.Instance;
            Vector2Int maleSize = auth.MaleGridSizeInCells;
            float tileSize = auth.FemaleTileSize;
            Vector2 maleOffset = auth.MaleGridOriginLocalOffset;
            Transform t = auth.transform;

            // Project each male cell's actual world position to terrain cells dynamically.
            // This stays correct even after dragging, rotating, or external moves.
            for (int z = 0; z < maleSize.y; z++)
            {
                for (int x = 0; x < maleSize.x; x++)
                {
                    if (!auth.GetMaleCell(x, z)) continue;
                    float lx = maleOffset.x + (x + 0.5f) * tileSize;
                    float lz = maleOffset.y + (z + 0.5f) * tileSize;
                    Vector3 worldCenter = t.position + t.right * lx + t.forward * lz;
                    if (terrain.TryWorldToCell(worldCenter, out Vector2Int terrainCell))
                        FillCellPixels(terrainCell.x, terrainCell.y, furColor);
                }
            }
        }
    }

    // Fills the minimap pixels for a terrain-absolute subtile cell by translating into view space.
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
        {
            for (int dx = 0; dx < pixelsPerCell; dx++)
            {
                minimapTexture.SetPixel(px + dx, py + dy, color);
            }
        }
    }

    // ── Layer helpers ─────────────────────────────────────────────────────────────

    private int FindLayerForPlayerTile()
    {
        if (terrain == null || availableLayers.Count == 0) return 0;
        Vector2Int tile = lastPlayerFullTile;
        int level = terrain.GetTileLevel(tile.x, tile.y);
        if (level == 0) return 0;
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

    private void RefreshAvailableLayers()
    {
        availableLayers.Clear();
        availableLayers.Add(0f);   // floor is always layer 0

        var seen = new HashSet<float>();
        seen.Add(0f);

        if (placedFurniture != null)
        {
            foreach (PlacedFurnitureRecord record in placedFurniture)
            {
                if (record.Instance == null) continue;
                PlaceableGridAuthoring auth = record.Instance;
                Transform t = auth.transform;

                // Use WORLD surface Y so stacked pieces (whose local heights repeat) produce
                // distinct layer entries (e.g. table on table gives 1.0 m AND 2.0 m layers).
                foreach (FemaleGridLayer layer in auth.EnumerateFemaleLayers())
                {
                    float worldSY = t.position.y + auth.MaleGridFloorLocalY + layer.LocalHeight;
                    float quantized = Mathf.Round(worldSY / 0.5f) * 0.5f;
                    if (seen.Add(quantized))
                        availableLayers.Add(quantized);
                }
            }
        }

        // Add terrain height levels beyond the ground floor.
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

        // Clamp current index.
        currentLayerIndex = Mathf.Clamp(currentLayerIndex, 0, availableLayers.Count - 1);
    }

    private bool IsRecordOnLayer(PlacedFurnitureRecord record, float targetHeight)
    {
        PlaceableGridAuthoring auth = record.Instance;
        Transform t = auth.transform;
        float baseY = t.position.y + auth.MaleGridFloorLocalY;

        if (targetHeight == 0f)
        {
            // Floor layer: only show pieces whose collider floor sits at terrain level 0.
            float level0Y = terrain.GetTerrainLevelWorldY(0);
            return Mathf.Abs(baseY - level0Y) < 0.1f;
        }

        // Show host furniture whose female surfaces sit at the target WORLD height.
        foreach (FemaleGridLayer layer in auth.EnumerateFemaleLayers())
        {
            float worldSY = baseY + layer.LocalHeight;
            if (Mathf.Abs(worldSY - targetHeight) < 0.01f) return true;
        }

        // Show pieces whose collider floor matches this layer's height (sitting on terrain or surface at this Y).
        if (Mathf.Abs(baseY - targetHeight) < 0.1f) return true;

        // Also show pieces that are physically sitting on the surface at this world height.
        float worldSurfaceY = GetSurfaceWorldY();
        if (!float.IsNaN(worldSurfaceY))
        {
            if (Mathf.Abs(baseY - worldSurfaceY) < 0.1f) return true;
        }

        return false;
    }

    private void UpdateLayerLabel()
    {
        if (layerLabel == null) return;
        if (currentLayerIndex == 0)
        {
            layerLabel.text = "Floor";
        }
        else
        {
            layerLabel.text = $"Height {CurrentLayerHeight:0.0} m";
        }
    }

    private void UpdateLayerButtonStates()
    {
        if (layerUpButton != null) layerUpButton.interactable = (currentLayerIndex < availableLayers.Count - 1);
        if (layerDownButton != null) layerDownButton.interactable = (currentLayerIndex > 0);
    }

    // ── Pointer / cell mapping ────────────────────────────────────────────────────

    private bool TryScreenToTerrainCell(Vector2 screenPos, out Vector2Int cell)
    {
        cell = default;
        if (rawImage == null) return false;

        RectTransform rt = rawImage.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, null, out Vector2 localPoint))
        {
            return false;
        }

        // Convert local-space point to [0..1] UV, then into a view-relative subtile index.
        Rect rect = rt.rect;
        float u = (localPoint.x - rect.xMin) / rect.width;
        float v = (localPoint.y - rect.yMin) / rect.height;

        int viewSize = ViewSizeCells;
        int vx = Mathf.FloorToInt(u * viewSize);
        int vz = Mathf.FloorToInt(v * viewSize);

        // Convert view-relative → terrain absolute.
        int cx = viewOriginSubtile.x + vx;
        int cz = viewOriginSubtile.y + vz;

        Vector2Int gridSize = terrain.GridSizeInCells;
        if (cx < 0 || cx >= gridSize.x || cz < 0 || cz >= gridSize.y) return false;

        cell = new Vector2Int(cx, cz);
        return true;
    }

    private PlacedFurnitureRecord HitTest(Vector2 screenPos, out Vector2Int hitCell)
    {
        hitCell = default;
        if (!TryScreenToTerrainCell(screenPos, out Vector2Int cell)) return null;
        hitCell = cell;

        if (placedFurniture == null) return null;

        float targetHeight    = CurrentLayerHeight;
        float worldSurfaceY   = GetSurfaceWorldY();
        bool  onNonFloorLayer = currentLayerIndex > 0 && !float.IsNaN(worldSurfaceY);
        int playerFloorLevel = terrain.GetTileLevel(lastPlayerFullTile.x, lastPlayerFullTile.y);
        float playerFloorWorldY = terrain.GetTerrainLevelWorldY(playerFloorLevel);

        PlacedFurnitureRecord hostHit    = null;
        float                 lowestBaseY = float.MaxValue;
        float                 bestFloorDist = float.MaxValue;

        foreach (PlacedFurnitureRecord record in placedFurniture)
        {
            if (record.Instance == null) continue;
            if (!IsRecordOnLayer(record, targetHeight)) continue;

            PlaceableGridAuthoring auth = record.Instance;
            Vector2Int maleSize = auth.MaleGridSizeInCells;
            float tileSize = auth.FemaleTileSize;
            Vector2 maleOffset = auth.MaleGridOriginLocalOffset;
            Transform t = auth.transform;

            // Get the world-space centre of the clicked terrain cell.
            if (!terrain.TryGetCellCenterWorld(cell.x, cell.y, out Vector3 clickedCellWorld)) continue;

            // Project into the piece's local XZ space (rotation-aware).
            Vector3 localOffset = clickedCellWorld - t.position;
            float localX = Vector3.Dot(localOffset, t.right);
            float localZ = Vector3.Dot(localOffset, t.forward);

            // Determine which male-grid cell index that corresponds to.
            int mx = Mathf.FloorToInt((localX - maleOffset.x) / tileSize);
            int mz = Mathf.FloorToInt((localZ - maleOffset.y) / tileSize);

            if (mx < 0 || mx >= maleSize.x || mz < 0 || mz >= maleSize.y) continue;
            if (!auth.GetMaleCell(mx, mz)) continue;

            float baseY = t.position.y + auth.MaleGridFloorLocalY;

            if (onNonFloorLayer)
            {
                // Prefer pieces sitting ON the current surface over host pieces below.
                if (Mathf.Abs(baseY - worldSurfaceY) < 0.1f)
                    return record;   // surface-sitting piece – return immediately
                hostHit = record;    // host surface piece – remember but keep searching
            }
            else
            {
                // Floor layer: prefer the piece whose base sits closest to the player's
                // current terrain level. This keeps interactions on elevated terrain from
                // accidentally selecting furniture on lower levels that share XZ.
                float floorDist = Mathf.Abs(baseY - playerFloorWorldY);
                if (floorDist < bestFloorDist - 0.001f ||
                    (Mathf.Abs(floorDist - bestFloorDist) < 0.001f && baseY < lowestBaseY))
                {
                    bestFloorDist = floorDist;
                    lowestBaseY = baseY;
                    hostHit = record;
                }
            }
        }

        return hostHit;
    }

    private void RotateFurniture90(PlacedFurnitureRecord record)
    {
        if (record.Instance == null) return;
        Transform t = record.Instance.transform;

        // Capture each child's position in the host's local frame BEFORE rotation so it can be
        // re-expressed in the rotated frame afterward (keeping it on the same surface cell).
        List<PlacedFurnitureRecord> children = GatherAllChildren(record);
        var childLocalOffsets = new Vector3[children.Count];
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i].Instance == null) continue;
            Vector3 worldOffset = children[i].Instance.transform.position - t.position;
            // Decompose into the host's local axes. Y is world-up so it is rotation-invariant.
            childLocalOffsets[i] = new Vector3(
                Vector3.Dot(worldOffset, t.right),
                worldOffset.y,
                Vector3.Dot(worldOffset, t.forward));
        }

        // Rotate the host.
        t.Rotate(Vector3.up, 90f, Space.World);

        // Reconstruct each child's world position using the NOW-ROTATED host axes.
        // The (localX, localZ) components are preserved in the new frame, so the child
        // stays on the same logical surface cell relative to the table.
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i].Instance == null) continue;
            Vector3 loc = childLocalOffsets[i];
            children[i].Instance.transform.position =
                t.position + t.right * loc.x + t.forward * loc.z + Vector3.up * loc.y;
        }

        Rebuild();
    }

    // Draws a white marker over the full-tile the player currently occupies.
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

    private void OnGUI()
    {
        if (!isDragging || string.IsNullOrEmpty(debugDragStatus)) return;

        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.Box(new Rect(16f, 16f, 900f, 50f), GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(new Rect(24f, 24f, 884f, 34f), debugDragStatus);
    }
}

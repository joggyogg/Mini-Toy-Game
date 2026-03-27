using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 3D furniture placement controller for human (perspective) mode.
/// Left-click a placed furniture collider to select + drag it along the terrain grid.
/// Right-click to rotate 90° clockwise.
/// Uses Physics.Raycast through the human camera instead of the 2D minimap.
/// </summary>
public class HumanFurniturePlacer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera sourceCamera;
    [SerializeField] private TerrainGridAuthoring terrain;
    [SerializeField] private FurnitureSpawner buildController;

    [Header("Raycasting")]
    [SerializeField] private LayerMask furnitureAndTerrainLayer = ~0;

    [Header("Visual Feedback")]
    [SerializeField] private Color validTint   = new Color(0.5f, 1f, 0.5f, 1f);
    [SerializeField] private Color invalidTint = new Color(1f, 0.4f, 0.4f, 1f);

    // ── State ──────────────────────────────────────────────────────────────────
    private PlacedFurnitureRecord selected;
    private bool isDragging;
    private bool dragIsFloorMove;
    private Vector2Int dragOffsetCells;
    private Vector3 lastValidPosition;
    private List<PlacedFurnitureRecord> draggedChildren = new List<PlacedFurnitureRecord>();
    private Dictionary<Renderer, Color[]> originalColors = new Dictionary<Renderer, Color[]>();

    /// <summary>True while the player is actively dragging a piece of furniture.</summary>
    public bool IsDragging => isDragging;

    // ── Unity Messages ─────────────────────────────────────────────────────────

    private void OnDisable()
    {
        CancelDrag();
    }

    private void Update()
    {
        if (sourceCamera == null || terrain == null || buildController == null) return;
        if (Mouse.current == null) return;

        // Don't interact when pointer is over UI (catalog buttons, etc.)
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        // Right-click → rotate
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            TryRotateUnderCursor();
            return;
        }

        // Left-click-down → try to pick
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryPickUnderCursor();
        }

        // Left held → drag
        if (isDragging && Mouse.current.leftButton.isPressed)
        {
            DragUpdate();
        }

        // Left released → commit
        if (isDragging && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            CommitDrag();
        }
    }

    // ── Picking ────────────────────────────────────────────────────────────────

    private void TryPickUnderCursor()
    {
        Ray ray = sourceCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, furnitureAndTerrainLayer))
        {
            Deselect();
            return;
        }

        PlaceableGridAuthoring auth = hit.collider.GetComponentInParent<PlaceableGridAuthoring>();
        if (auth == null)
        {
            Deselect();
            return;
        }

        PlacedFurnitureRecord record = FindRecord(auth);
        if (record == null)
        {
            Deselect();
            return;
        }

        // Compute current origin cell of the piece
        Vector2Int currentOrigin = GetPieceOriginCell(auth);

        // Compute which terrain cell was clicked
        if (!terrain.TryWorldToCell(hit.point, out Vector2Int hitCell))
        {
            Deselect();
            return;
        }

        // Begin drag
        if (selected != record) SetSelection(record);
        dragOffsetCells = hitCell - currentOrigin;
        dragIsFloorMove = !IsSittingOnFurniture(record);
        isDragging = true;
        lastValidPosition = auth.transform.position;

        // Gather children that ride on this piece
        draggedChildren = GatherAllChildren(record);
    }

    private void TryRotateUnderCursor()
    {
        Ray ray = sourceCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, furnitureAndTerrainLayer))
            return;

        PlaceableGridAuthoring auth = hit.collider.GetComponentInParent<PlaceableGridAuthoring>();
        if (auth == null) return;

        PlacedFurnitureRecord record = FindRecord(auth);
        if (record == null) return;

        RotateFurniture90(record);
    }

    // ── Dragging ───────────────────────────────────────────────────────────────

    private void DragUpdate()
    {
        if (selected == null || selected.Instance == null) { CancelDrag(); return; }

        Ray ray = sourceCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        // Raycast onto a horizontal plane at the piece's current floor Y
        PlaceableGridAuthoring auth = selected.Instance;
        float floorWorldY = auth.transform.position.y + auth.MaleGridFloorLocalY;
        Plane dragPlane = new Plane(Vector3.up, new Vector3(0f, floorWorldY, 0f));

        if (!dragPlane.Raycast(ray, out float enter)) return;
        Vector3 worldHit = ray.GetPoint(enter);

        if (!terrain.TryWorldToCell(worldHit, out Vector2Int hitCell)) return;
        Vector2Int newOrigin = hitCell - dragOffsetCells;

        // Save child positions before potentially moving the parent
        var childPositionsBefore = new Vector3[draggedChildren.Count];
        for (int i = 0; i < draggedChildren.Count; i++)
        {
            if (draggedChildren[i].Instance != null)
                childPositionsBefore[i] = draggedChildren[i].Instance.transform.position;
        }

        Vector3 posBefore = auth.transform.position;

        bool valid;
        Vector3 delta;
        if (dragIsFloorMove)
            valid = TryDragOnFloor(auth, newOrigin, draggedChildren, out delta);
        else
            valid = TryDragOnSurface(auth, newOrigin, out delta);

        if (valid)
        {
            lastValidPosition = auth.transform.position;
            // Move children by the same delta
            if (dragIsFloorMove)
            {
                for (int i = 0; i < draggedChildren.Count; i++)
                {
                    if (draggedChildren[i].Instance != null)
                        draggedChildren[i].Instance.transform.position = childPositionsBefore[i] + delta;
                }
            }
            ApplyTint(validTint);
        }
        else
        {
            // Revert
            auth.transform.position = posBefore;
            ApplyTint(invalidTint);
        }
    }

    private void CommitDrag()
    {
        isDragging = false;
        draggedChildren.Clear();
        ClearTint();
    }

    private void CancelDrag()
    {
        if (isDragging && selected != null && selected.Instance != null)
            selected.Instance.transform.position = lastValidPosition;
        isDragging = false;
        draggedChildren.Clear();
        ClearTint();
        Deselect();
    }

    // ── Floor Drag ─────────────────────────────────────────────────────────────

    private bool TryDragOnFloor(PlaceableGridAuthoring auth, Vector2Int newOrigin,
        ICollection<PlacedFurnitureRecord> alsoExclude, out Vector3 delta)
    {
        delta = Vector3.zero;
        IReadOnlyList<PlacedFurnitureRecord> placed = buildController.PlacedFurniture;

        // Cross-layer: if every male cell lands on an enabled female cell, snap to surface
        if (TryFindSurfaceUnderFootprint(auth, newOrigin, selected, alsoExclude, out float snapY))
        {
            if (!terrain.TryGetCellCenterWorld(newOrigin.x, newOrigin.y, out Vector3 snapCell00))
                return false;
            Vector3 oldPos = auth.transform.position;
            Vector2 off = auth.MaleGridOriginLocalOffset;
            float ts = auth.FemaleTileSize;
            Transform t = auth.transform;
            Vector3 snapPos = snapCell00
                - t.right   * (off.x + 0.5f * ts)
                - t.forward * (off.y + 0.5f * ts);
            snapPos.y = snapY - auth.MaleGridFloorLocalY;
            auth.transform.position = snapPos;
            delta = snapPos - oldPos;
            return true;
        }

        // Build occupancy set excluding dragged piece + children
        var occupied = new HashSet<Vector2Int>();
        bool hasLevel = TryGetPieceTerrainLevel(auth, out int sourceLevel);

        foreach (PlacedFurnitureRecord record in placed)
        {
            if (record == selected || record.Instance == null) continue;
            if (alsoExclude != null && alsoExclude.Contains(record)) continue;
            if (hasLevel)
            {
                if (!TryGetPieceTerrainLevel(record.Instance, out int recLevel)) continue;
                if (recLevel != sourceLevel) continue;
            }
            AddMaleCellsToSet(record.Instance, occupied);
        }

        // Validate at newOrigin
        List<Vector2Int> cells = GetRotatedCellsForOrigin(auth, newOrigin);
        if (cells == null) return false;

        int s = terrain.SubtilesPerFullTile;
        foreach (Vector2Int tc in cells)
        {
            if (!terrain.GetCell(tc.x, tc.y)) return false;
            if (occupied.Contains(tc)) return false;
            if (hasLevel)
            {
                if (terrain.GetTileLevel(tc.x / s, tc.y / s) != sourceLevel) return false;
            }
        }

        // Move
        if (!terrain.TryGetCellCenterWorld(newOrigin.x, newOrigin.y, out Vector3 cell00))
            return false;

        Vector3 oldP = auth.transform.position;
        Vector2 maleOff = auth.MaleGridOriginLocalOffset;
        float tileSize = auth.FemaleTileSize;
        auth.transform.position = cell00
            - auth.transform.right   * (maleOff.x + 0.5f * tileSize)
            - auth.transform.forward * (maleOff.y + 0.5f * tileSize)
            + Vector3.up * (-auth.MaleGridFloorLocalY);
        delta = auth.transform.position - oldP;
        return true;
    }

    // ── Surface Drag ───────────────────────────────────────────────────────────

    private bool TryDragOnSurface(PlaceableGridAuthoring auth, Vector2Int newOrigin, out Vector3 delta)
    {
        delta = Vector3.zero;

        // Find a surface all male cells fit on
        if (!TryFindSurfaceUnderFootprint(auth, newOrigin, selected, null, out float surfaceY))
        {
            // Fell off surface — try floor
            return TryDragOnFloor(auth, newOrigin, null, out delta);
        }

        // Check occupancy at that surface
        HashSet<Vector2Int> occupied = BuildSurfaceOccupancy(surfaceY, selected);
        List<Vector2Int> cells = GetRotatedCellsForOrigin(auth, newOrigin);
        if (cells == null) return false;
        foreach (Vector2Int tc in cells)
            if (occupied.Contains(tc)) return false;

        // Move
        if (!terrain.TryGetCellCenterWorld(newOrigin.x, newOrigin.y, out Vector3 cell00))
            return false;

        Vector3 oldP = auth.transform.position;
        Vector2 maleOff = auth.MaleGridOriginLocalOffset;
        float tileSize = auth.FemaleTileSize;
        Vector3 newPos = cell00
            - auth.transform.right   * (maleOff.x + 0.5f * tileSize)
            - auth.transform.forward * (maleOff.y + 0.5f * tileSize);
        newPos.y = surfaceY - auth.MaleGridFloorLocalY;
        auth.transform.position = newPos;
        delta = auth.transform.position - oldP;
        return true;
    }

    // ── Rotation ───────────────────────────────────────────────────────────────

    private void RotateFurniture90(PlacedFurnitureRecord record)
    {
        if (record.Instance == null) return;
        if (HasRotationOverlap(record)) return;

        Transform t = record.Instance.transform;

        List<PlacedFurnitureRecord> children = GatherAllChildren(record);
        var childLocalOffsets = new Vector3[children.Count];
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i].Instance == null) continue;
            Vector3 worldOffset = children[i].Instance.transform.position - t.position;
            childLocalOffsets[i] = new Vector3(
                Vector3.Dot(worldOffset, t.right),
                worldOffset.y,
                Vector3.Dot(worldOffset, t.forward));
        }

        t.Rotate(Vector3.up, 90f, Space.World);

        for (int i = 0; i < children.Count; i++)
        {
            if (children[i].Instance == null) continue;
            Vector3 loc = childLocalOffsets[i];
            children[i].Instance.transform.position =
                t.position + t.right * loc.x + t.forward * loc.z + Vector3.up * loc.y;
        }
    }

    private bool HasRotationOverlap(PlacedFurnitureRecord record)
    {
        PlaceableGridAuthoring auth = record.Instance;
        Transform t = auth.transform;
        IReadOnlyList<PlacedFurnitureRecord> placed = buildController.PlacedFurniture;

        Quaternion rot90 = Quaternion.AngleAxis(90f, Vector3.up);
        Vector3 newRight   = rot90 * t.right;
        Vector3 newForward = rot90 * t.forward;

        // Compute rotated footprint cells
        var rotatedCells = new HashSet<Vector2Int>();
        Vector2Int maleSize  = auth.MaleGridSizeInCells;
        Vector2 maleOffset   = auth.MaleGridOriginLocalOffset;
        float tileSize       = auth.FemaleTileSize;
        float recordBaseY    = t.position.y + auth.MaleGridFloorLocalY;

        int s = terrain.SubtilesPerFullTile;
        // Determine terrain level
        Vector2Int originCell = GetPieceOriginCell(auth);
        int targetLevel = terrain.GetTileLevel(originCell.x / s, originCell.y / s);

        for (int z = 0; z < maleSize.y; z++)
            for (int x = 0; x < maleSize.x; x++)
            {
                if (!auth.GetMaleCell(x, z)) continue;
                float lx = maleOffset.x + (x + 0.5f) * tileSize;
                float lz = maleOffset.y + (z + 0.5f) * tileSize;
                Vector3 worldCenter = t.position + newRight * lx + newForward * lz;
                if (!terrain.TryWorldToCell(worldCenter, out Vector2Int tc)) return true;
                if (!terrain.GetCell(tc.x, tc.y)) return true;
                if (terrain.GetTileLevel(tc.x / s, tc.y / s) != targetLevel) return true;
                rotatedCells.Add(tc);
            }

        if (rotatedCells.Count == 0) return false;

        const float elevatedThreshold = 0.15f;
        foreach (PlacedFurnitureRecord other in placed)
        {
            if (other == record || other.Instance == null) continue;
            float otherBaseY = other.Instance.transform.position.y + other.Instance.MaleGridFloorLocalY;
            if (otherBaseY > recordBaseY + elevatedThreshold) continue;

            PlaceableGridAuthoring otherAuth = other.Instance;
            Transform ot = otherAuth.transform;
            Vector2Int otherSize  = otherAuth.MaleGridSizeInCells;
            Vector2 otherOffset   = otherAuth.MaleGridOriginLocalOffset;
            float otherTile       = otherAuth.FemaleTileSize;

            for (int z = 0; z < otherSize.y; z++)
                for (int x = 0; x < otherSize.x; x++)
                {
                    if (!otherAuth.GetMaleCell(x, z)) continue;
                    float lx = otherOffset.x + (x + 0.5f) * otherTile;
                    float lz = otherOffset.y + (z + 0.5f) * otherTile;
                    Vector3 wc = ot.position + ot.right * lx + ot.forward * lz;
                    if (terrain.TryWorldToCell(wc, out Vector2Int tc) && rotatedCells.Contains(tc))
                        return true;
                }
        }
        return false;
    }

    // ── Shared Helpers (adapted from DecorateMinimapUI) ────────────────────────

    private PlacedFurnitureRecord FindRecord(PlaceableGridAuthoring auth)
    {
        IReadOnlyList<PlacedFurnitureRecord> placed = buildController.PlacedFurniture;
        for (int i = 0; i < placed.Count; i++)
            if (placed[i].Instance == auth) return placed[i];
        return null;
    }

    private Vector2Int GetPieceOriginCell(PlaceableGridAuthoring auth)
    {
        Transform t = auth.transform;
        Vector2 off = auth.MaleGridOriginLocalOffset;
        float ts = auth.FemaleTileSize;
        Vector3 cell00World = t.position + t.right * (off.x + 0.5f * ts) + t.forward * (off.y + 0.5f * ts);
        terrain.TryWorldToCell(cell00World, out Vector2Int origin);
        return origin;
    }

    private List<Vector2Int> GetRotatedCellsForOrigin(PlaceableGridAuthoring auth, Vector2Int newOrigin)
    {
        if (!terrain.TryGetCellCenterWorld(newOrigin.x, newOrigin.y, out Vector3 cell00Center))
            return null;

        Vector2 maleOffset = auth.MaleGridOriginLocalOffset;
        float tileSize = auth.FemaleTileSize;
        Transform t = auth.transform;

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

    private bool TryGetPieceTerrainLevel(PlaceableGridAuthoring auth, out int level)
    {
        level = 0;
        Vector2Int maleSize = auth.MaleGridSizeInCells;
        float tileSize = auth.FemaleTileSize;
        Vector2 maleOffset = auth.MaleGridOriginLocalOffset;
        Transform t = auth.transform;
        int s = terrain.SubtilesPerFullTile;

        for (int z = 0; z < maleSize.y; z++)
            for (int x = 0; x < maleSize.x; x++)
            {
                if (!auth.GetMaleCell(x, z)) continue;
                float lx = maleOffset.x + (x + 0.5f) * tileSize;
                float lz = maleOffset.y + (z + 0.5f) * tileSize;
                Vector3 w = t.position + t.right * lx + t.forward * lz;
                if (!terrain.TryWorldToCell(w, out Vector2Int tc)) continue;
                level = terrain.GetTileLevel(tc.x / s, tc.y / s);
                return true;
            }
        return false;
    }

    private bool TryFindSurfaceUnderFootprint(PlaceableGridAuthoring auth, Vector2Int newOrigin,
        PlacedFurnitureRecord draggedRecord, ICollection<PlacedFurnitureRecord> alsoExclude,
        out float snappedSurfaceY)
    {
        snappedSurfaceY = float.NaN;
        IReadOnlyList<PlacedFurnitureRecord> placed = buildController.PlacedFurniture;

        List<Vector2Int> footprintList = GetRotatedCellsForOrigin(auth, newOrigin);
        if (footprintList == null || footprintList.Count == 0) return false;
        var footprint = new HashSet<Vector2Int>(footprintList);

        bool hasDragLevel = TryGetPieceTerrainLevel(auth, out int dragLevel);

        foreach (PlacedFurnitureRecord host in placed)
        {
            if (host == draggedRecord || host.Instance == null) continue;
            if (alsoExclude != null && alsoExclude.Contains(host)) continue;
            if (hasDragLevel && !TryGetPieceTerrainLevel(host.Instance, out int hostLevel)) continue;
            if (hasDragLevel)
            {
                TryGetPieceTerrainLevel(host.Instance, out int hostLvl);
                if (hostLvl != dragLevel) continue;
            }

            PlaceableGridAuthoring hostAuth = host.Instance;
            Transform ht = hostAuth.transform;

            foreach (FemaleGridLayer layer in hostAuth.EnumerateFemaleLayers())
            {
                float worldSY = ht.position.y + hostAuth.MaleGridFloorLocalY + layer.LocalHeight;
                HashSet<Vector2Int> enabledCells = GetFemaleLayerCells(hostAuth, layer);

                if (enabledCells.Count == 0) continue;

                bool allFit = true;
                foreach (Vector2Int tc in footprint)
                {
                    if (!enabledCells.Contains(tc)) { allFit = false; break; }
                }
                if (!allFit) continue;

                HashSet<Vector2Int> occupied = BuildSurfaceOccupancy(worldSY, draggedRecord);
                bool anyBlocked = false;
                foreach (Vector2Int tc in footprint)
                    if (occupied.Contains(tc)) { anyBlocked = true; break; }
                if (anyBlocked) continue;

                snappedSurfaceY = worldSY;
                return true;
            }
        }
        return false;
    }

    private HashSet<Vector2Int> GetFemaleLayerCells(PlaceableGridAuthoring auth, FemaleGridLayer layer)
    {
        var cells = new HashSet<Vector2Int>();
        Transform t = auth.transform;
        Vector2 maleOffset = auth.MaleGridOriginLocalOffset;
        float tileSize = auth.FemaleTileSize;
        Vector2Int gridSize = layer.GridSizeInCells;
        for (int fz = 0; fz < gridSize.y; fz++)
            for (int fx = 0; fx < gridSize.x; fx++)
            {
                if (!layer.GetCell(fx, fz)) continue;
                float lx = maleOffset.x + (fx + 0.5f) * tileSize;
                float lz = maleOffset.y + (fz + 0.5f) * tileSize;
                Vector3 wc = t.position + t.right * lx + t.forward * lz;
                if (terrain.TryWorldToCell(wc, out Vector2Int tc))
                    cells.Add(tc);
            }
        return cells;
    }

    private HashSet<Vector2Int> BuildSurfaceOccupancy(float worldSurfaceY, PlacedFurnitureRecord excluded)
    {
        var occupied = new HashSet<Vector2Int>();
        const float yTol = 0.1f;
        IReadOnlyList<PlacedFurnitureRecord> placed = buildController.PlacedFurniture;

        foreach (PlacedFurnitureRecord record in placed)
        {
            if (record == excluded || record.Instance == null) continue;
            float baseY = record.Instance.transform.position.y + record.Instance.MaleGridFloorLocalY;
            if (Mathf.Abs(baseY - worldSurfaceY) > yTol) continue;
            AddMaleCellsToSet(record.Instance, occupied);
        }
        return occupied;
    }

    private void AddMaleCellsToSet(PlaceableGridAuthoring auth, HashSet<Vector2Int> set)
    {
        Vector2Int maleSize = auth.MaleGridSizeInCells;
        float tileSize = auth.FemaleTileSize;
        Vector2 maleOffset = auth.MaleGridOriginLocalOffset;
        Transform t = auth.transform;

        for (int z = 0; z < maleSize.y; z++)
            for (int x = 0; x < maleSize.x; x++)
            {
                if (!auth.GetMaleCell(x, z)) continue;
                float lx = maleOffset.x + (x + 0.5f) * tileSize;
                float lz = maleOffset.y + (z + 0.5f) * tileSize;
                Vector3 cellWorld = t.position + t.right * lx + t.forward * lz;
                if (terrain.TryWorldToCell(cellWorld, out Vector2Int tc))
                    set.Add(tc);
            }
    }

    private bool IsSittingOnFurniture(PlacedFurnitureRecord piece)
    {
        PlaceableGridAuthoring auth = piece.Instance;
        if (auth == null) return false;
        float baseY = auth.transform.position.y + auth.MaleGridFloorLocalY;
        IReadOnlyList<PlacedFurnitureRecord> placed = buildController.PlacedFurniture;

        // Get one male cell for XZ overlap testing
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
        foreach (PlacedFurnitureRecord host in placed)
        {
            if (host == piece || host.Instance == null) continue;
            PlaceableGridAuthoring hostAuth = host.Instance;
            foreach (FemaleGridLayer layer in hostAuth.EnumerateFemaleLayers())
            {
                float worldSY = hostAuth.transform.position.y + hostAuth.MaleGridFloorLocalY + layer.LocalHeight;
                if (Mathf.Abs(baseY - worldSY) > yTol) continue;
                HashSet<Vector2Int> femaleCells = GetFemaleLayerCells(hostAuth, layer);
                if (femaleCells.Contains(pieceCell.Value))
                    return true;
            }
        }
        return false;
    }

    private List<PlacedFurnitureRecord> GatherAllChildren(PlacedFurnitureRecord host)
    {
        IReadOnlyList<PlacedFurnitureRecord> placed = buildController.PlacedFurniture;
        var result  = new List<PlacedFurnitureRecord>();
        var visited = new HashSet<PlacedFurnitureRecord> { host };
        var queue   = new Queue<PlacedFurnitureRecord>();
        queue.Enqueue(host);

        while (queue.Count > 0)
        {
            PlacedFurnitureRecord current = queue.Dequeue();
            PlaceableGridAuthoring currentAuth = current.Instance;
            if (currentAuth == null) continue;
            Transform ct = currentAuth.transform;

            var layers = new List<(HashSet<Vector2Int> cells, float surfaceY)>();
            foreach (FemaleGridLayer layer in currentAuth.EnumerateFemaleLayers())
            {
                float worldSY = ct.position.y + currentAuth.MaleGridFloorLocalY + layer.LocalHeight;
                HashSet<Vector2Int> cells = GetFemaleLayerCellsAll(currentAuth, layer);
                if (cells.Count > 0)
                    layers.Add((cells, worldSY));
            }
            if (layers.Count == 0) continue;

            const float yTol = 0.1f;

            foreach (PlacedFurnitureRecord record in placed)
            {
                if (visited.Contains(record) || record.Instance == null) continue;

                PlaceableGridAuthoring auth = record.Instance;
                float baseY = auth.transform.position.y + auth.MaleGridFloorLocalY;

                HashSet<Vector2Int> matchedCells = null;
                foreach (var (cells, surfaceY) in layers)
                {
                    if (Mathf.Abs(baseY - surfaceY) < yTol) { matchedCells = cells; break; }
                }
                if (matchedCells == null) continue;

                Vector2Int maleSize = auth.MaleGridSizeInCells;
                float tileSize = auth.FemaleTileSize;
                Vector2 maleOffset = auth.MaleGridOriginLocalOffset;
                Transform at = auth.transform;
                bool isChild = false;

                for (int z = 0; z < maleSize.y && !isChild; z++)
                    for (int x = 0; x < maleSize.x && !isChild; x++)
                    {
                        if (!auth.GetMaleCell(x, z)) continue;
                        float lx = maleOffset.x + (x + 0.5f) * tileSize;
                        float lz = maleOffset.y + (z + 0.5f) * tileSize;
                        Vector3 cellW = at.position + at.right * lx + at.forward * lz;
                        if (terrain.TryWorldToCell(cellW, out Vector2Int tc) && matchedCells.Contains(tc))
                            isChild = true;
                    }

                if (!isChild) continue;
                visited.Add(record);
                result.Add(record);
                queue.Enqueue(record);
            }
        }
        return result;
    }

    /// <summary>Returns ALL female layer cells (enabled and disabled) for child detection.</summary>
    private HashSet<Vector2Int> GetFemaleLayerCellsAll(PlaceableGridAuthoring auth, FemaleGridLayer layer)
    {
        var cells = new HashSet<Vector2Int>();
        Transform t = auth.transform;
        Vector2 maleOffset = auth.MaleGridOriginLocalOffset;
        float tileSize = auth.FemaleTileSize;
        Vector2Int gridSize = layer.GridSizeInCells;
        for (int fz = 0; fz < gridSize.y; fz++)
            for (int fx = 0; fx < gridSize.x; fx++)
            {
                float lx = maleOffset.x + (fx + 0.5f) * tileSize;
                float lz = maleOffset.y + (fz + 0.5f) * tileSize;
                Vector3 wc = t.position + t.right * lx + t.forward * lz;
                if (terrain.TryWorldToCell(wc, out Vector2Int tc))
                    cells.Add(tc);
            }
        return cells;
    }

    // ── Selection Visual Feedback ──────────────────────────────────────────────

    private void SetSelection(PlacedFurnitureRecord record)
    {
        if (selected != record) ClearTint();
        selected = record;
    }

    private void Deselect()
    {
        ClearTint();
        selected = null;
        isDragging = false;
        draggedChildren.Clear();
    }

    private void ApplyTint(Color color)
    {
        if (selected == null || selected.Instance == null) return;
        Renderer[] renderers = selected.Instance.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            if (!originalColors.ContainsKey(r))
            {
                Material[] mats = r.materials;
                Color[] cols = new Color[mats.Length];
                for (int i = 0; i < mats.Length; i++)
                    cols[i] = mats[i].HasProperty("_Color") ? mats[i].color : Color.white;
                originalColors[r] = cols;
            }
            Material[] ms = r.materials;
            for (int i = 0; i < ms.Length; i++)
            {
                if (ms[i].HasProperty("_Color"))
                    ms[i].color = color;
            }
        }
    }

    private void ClearTint()
    {
        foreach (var kvp in originalColors)
        {
            if (kvp.Key == null) continue;
            Material[] mats = kvp.Key.materials;
            for (int i = 0; i < mats.Length && i < kvp.Value.Length; i++)
            {
                if (mats[i].HasProperty("_Color"))
                    mats[i].color = kvp.Value[i];
            }
        }
        originalColors.Clear();
    }
}

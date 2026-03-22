using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plain-English purpose:
/// Attach this component to any placeable object, such as a table or shelf, to describe how it snaps onto female tiles
/// and which female grid layers it exposes for other objects to snap onto.
///
/// This component stores the gameplay data. The editor scripts provide the custom UI used to edit that data.
/// </summary>
[DisallowMultipleComponent]
public class PlaceableGridAuthoring : MonoBehaviour
{
    private const float MinTileSize = 0.1f;
    private const float MinLowestFemaleLayerHeight = 0.5f;
    private const float GizmoThickness = 0.02f;
    private const float FullTileWorldSize = 1f;
    private const float MaleGizmoVerticalOffset = 0.01f;
    private static readonly Color FemalePrimaryLight = new Color(0.93f, 0.36f, 0.72f, 0.6f);
    private static readonly Color FemalePrimaryDark = new Color(0.99f, 0.0f, 0.45f, 0.6f);
    private static readonly Color FemaleSecondaryLight = new Color(1f, 0.79f, 0.46f, 0.6f);
    private static readonly Color FemaleSecondaryDark = new Color(1f, 0.63f, 0.0f, 0.6f);
    private static readonly Color MalePrimaryLight = new Color(0.39f, 0.79f, 0.89f, 0.6f);
    private static readonly Color MalePrimaryDark = new Color(0.12f, 0.72f, 0.88f, 0.6f);
    private static readonly Color MaleSecondaryLight = new Color(0.47f, 0.38f, 0.92f, 0.6f);
    private static readonly Color MaleSecondaryDark = new Color(0.26f, 0.0f, 0.95f, 0.6f);

    // Size of one female tile cell in world units. The current plan uses 0.5 x 0.5 support cells.
    [Min(MinTileSize)]
    [SerializeField] private float femaleTileSize = 0.5f;

    // When enabled, the component measures colliders and uses that to set the single male grid size.
    [SerializeField] private bool deriveMaleGridFromColliders = true;

    // Width and length of the single male grid in cell units.
    [SerializeField] private Vector2Int maleGridSizeInCells = new Vector2Int(2, 2);

    // Local-space XZ offset from the transform pivot to the (0,0) male cell corner.
    // Stored when the male grid is computed from colliders so runtime placement can correctly align the furniture.
    [SerializeField] private Vector2 maleGridOriginLocalOffset;

    // Local Y offset from the transform pivot down to the lowest point of all colliders (projected onto the
    // furniture's up axis). Used at spawn time to sit the male grid floor exactly on the terrain surface.
    [SerializeField] private float maleGridFloorLocalY;

    // Flattened male-grid mask stored row-by-row. True means that male tile exists on the underside footprint.
    [SerializeField] private List<bool> enabledMaleCells = new List<bool>();

    // Logical height used later for clearance checks when placing this object on shelves or other female layers.
    [Min(MinTileSize)]
    [SerializeField] private float objectHeight = 1f;

    // Lets the user turn the scene gizmo overlays on or off for this object.
    [SerializeField] private bool drawGridGizmos = true;

    // Female grids are organized into a hierarchy of ordered groups. Group spacing rules determine computed layer heights.
    [SerializeReference] private FemaleGridGroup femaleGridRootGroup = new FemaleGridGroup();

    public float FemaleTileSize => femaleTileSize;
    public bool DeriveMaleGridFromColliders => deriveMaleGridFromColliders;
    public Vector2Int MaleGridSizeInCells => maleGridSizeInCells;
    public Vector2 MaleGridOriginLocalOffset => maleGridOriginLocalOffset;
    public float MaleGridFloorLocalY => maleGridFloorLocalY;
    public float ObjectHeight => objectHeight;
    public FemaleGridGroup FemaleGridRootGroup => femaleGridRootGroup;
    public bool DrawGridGizmos => drawGridGizmos;

    private struct ProjectedColliderBounds
    {
        public float MinX;
        public float MaxX;
        public float MinY;
        public float MaxY;
        public float MinZ;
        public float MaxZ;

        public float SizeX => MaxX - MinX;
        public float SizeY => MaxY - MinY;
        public float SizeZ => MaxZ - MinZ;
    }

    private struct ProjectedColliderFootprint
    {
        public float MinX;
        public float MaxX;
        public float MinZ;
        public float MaxZ;
    }

    private struct LocalProjectedBounds
    {
        public float MinX;
        public float MaxX;
        public float MinY;
        public float MaxY;
        public float MinZ;
        public float MaxZ;
    }

    private void Reset()
    {
        // Try to create sensible defaults the first time the component is added.
        RecalculateMaleGridFromColliders();

        if (femaleGridRootGroup.Children.Count == 0)
        {
            femaleGridRootGroup.Name = "Master Group";
            femaleGridRootGroup.Spacing = 0.5f;
            AddFemaleGridLayer(femaleGridRootGroup, 0);
        }

        EnsureValidData();
    }

    private void OnValidate()
    {
        // Keep serialized data in a valid state whenever values are changed in the inspector.
        if (deriveMaleGridFromColliders)
        {
            RecalculateMaleGridFromColliders();
        }

        EnsureValidData();
    }

    public void EnsureValidData()
    {
        // Clamp values so the editor tool always works with safe dimensions.
        femaleTileSize = Mathf.Max(MinTileSize, femaleTileSize);
        maleGridSizeInCells.x = Mathf.Max(1, maleGridSizeInCells.x);
        maleGridSizeInCells.y = Mathf.Max(1, maleGridSizeInCells.y);
        objectHeight = Mathf.Max(MinTileSize, objectHeight);
        ResizeMaleGridCells();

        EnsureValidHierarchy();
    }

    public void RecalculateMaleGridFromColliders()
    {
        deriveMaleGridFromColliders = true;

        // The male grid is defined in world-sized placement cells, so we must measure the collider footprint
        // after transform scale has been applied. Using pure local-space bounds would incorrectly keep a scaled
        // 1x1x1 cube at 2x2 cells forever.
        if (!TryGetProjectedColliderBounds(out ProjectedColliderBounds projectedBounds))
        {
            maleGridSizeInCells = Vector2Int.Max(Vector2Int.one, maleGridSizeInCells);
            maleGridOriginLocalOffset = Vector2.zero;
            maleGridFloorLocalY = 0f;
            return;
        }

        int width = Mathf.Max(1, Mathf.CeilToInt(projectedBounds.SizeX / femaleTileSize));
        int length = Mathf.Max(1, Mathf.CeilToInt(projectedBounds.SizeZ / femaleTileSize));
        maleGridSizeInCells = new Vector2Int(width, length);
        maleGridOriginLocalOffset = new Vector2(projectedBounds.MinX, projectedBounds.MinZ);
        maleGridFloorLocalY = projectedBounds.MinY;

        ApplyMaleGridFromColliderFootprints(projectedBounds);
        EnsureValidHierarchy();
    }

    private void ApplyMaleGridFromColliderFootprints(ProjectedColliderBounds projectedBounds)
    {
        ResizeMaleGridCells();
        FillMaleGridInternal(false);

        List<ProjectedColliderFootprint> colliderFootprints = GetProjectedColliderFootprints();
        if (colliderFootprints.Count == 0)
        {
            FillMaleGridInternal(true);
            return;
        }

        const float epsilon = 0.0001f;

        for (int z = 0; z < maleGridSizeInCells.y; z++)
        {
            float cellMinZ = projectedBounds.MinZ + (z * femaleTileSize);
            float cellMaxZ = cellMinZ + femaleTileSize;

            for (int x = 0; x < maleGridSizeInCells.x; x++)
            {
                float cellMinX = projectedBounds.MinX + (x * femaleTileSize);
                float cellMaxX = cellMinX + femaleTileSize;
                bool overlapsAnyCollider = false;

                for (int footprintIndex = 0; footprintIndex < colliderFootprints.Count; footprintIndex++)
                {
                    ProjectedColliderFootprint colliderFootprint = colliderFootprints[footprintIndex];
                    bool overlapsX = colliderFootprint.MaxX > cellMinX + epsilon && colliderFootprint.MinX < cellMaxX - epsilon;
                    bool overlapsZ = colliderFootprint.MaxZ > cellMinZ + epsilon && colliderFootprint.MinZ < cellMaxZ - epsilon;
                    if (!overlapsX || !overlapsZ)
                    {
                        continue;
                    }

                    overlapsAnyCollider = true;
                    break;
                }

                int flatIndex = GetFlatIndex(maleGridSizeInCells, x, z);
                if (flatIndex >= 0 && flatIndex < enabledMaleCells.Count)
                {
                    enabledMaleCells[flatIndex] = overlapsAnyCollider;
                }
            }
        }
    }

    private List<ProjectedColliderFootprint> GetProjectedColliderFootprints()
    {
        List<ProjectedColliderFootprint> colliderFootprints = new List<ProjectedColliderFootprint>();
        Collider[] colliders = GetComponentsInChildren<Collider>();

        foreach (Collider colliderComponent in colliders)
        {
            if (!colliderComponent.enabled)
            {
                continue;
            }

            if (TryGetColliderProjectedBounds(colliderComponent, out LocalProjectedBounds projectedBounds))
            {
                colliderFootprints.Add(new ProjectedColliderFootprint
                {
                    MinX = projectedBounds.MinX,
                    MaxX = projectedBounds.MaxX,
                    MinZ = projectedBounds.MinZ,
                    MaxZ = projectedBounds.MaxZ
                });
            }
        }

        return colliderFootprints;
    }

    private bool TryGetProjectedColliderBounds(out ProjectedColliderBounds projectedBounds)
    {
        // This measures collider geometry along the author's local right/up/forward axes in world units while
        // preserving transform scale. Using collider.bounds is unreliable for prefab assets, so we project the
        // collider's own local geometry into authoring space instead.
        Collider[] colliders = GetComponentsInChildren<Collider>();
        bool hasBounds = false;
        projectedBounds = default;

        foreach (Collider colliderComponent in colliders)
        {
            if (!colliderComponent.enabled)
            {
                continue;
            }

            if (!TryGetColliderProjectedBounds(colliderComponent, out LocalProjectedBounds colliderBounds))
            {
                continue;
            }

            if (!hasBounds)
            {
                projectedBounds.MinX = colliderBounds.MinX;
                projectedBounds.MaxX = colliderBounds.MaxX;
                projectedBounds.MinY = colliderBounds.MinY;
                projectedBounds.MaxY = colliderBounds.MaxY;
                projectedBounds.MinZ = colliderBounds.MinZ;
                projectedBounds.MaxZ = colliderBounds.MaxZ;
                hasBounds = true;
                continue;
            }

            projectedBounds.MinX = Mathf.Min(projectedBounds.MinX, colliderBounds.MinX);
            projectedBounds.MaxX = Mathf.Max(projectedBounds.MaxX, colliderBounds.MaxX);
            projectedBounds.MinY = Mathf.Min(projectedBounds.MinY, colliderBounds.MinY);
            projectedBounds.MaxY = Mathf.Max(projectedBounds.MaxY, colliderBounds.MaxY);
            projectedBounds.MinZ = Mathf.Min(projectedBounds.MinZ, colliderBounds.MinZ);
            projectedBounds.MaxZ = Mathf.Max(projectedBounds.MaxZ, colliderBounds.MaxZ);
        }

        return hasBounds;
    }

    private bool TryGetColliderProjectedBounds(Collider colliderComponent, out LocalProjectedBounds projectedBounds)
    {
        projectedBounds = default;

        if (colliderComponent == null)
        {
            return false;
        }

        if (!TryGetColliderSourceBounds(colliderComponent, out Bounds sourceBounds))
        {
            return false;
        }

        bool hasBounds = false;
        Vector3 extents = sourceBounds.extents;
        Vector3 center = sourceBounds.center;
        Vector3 origin = transform.position;
        Vector3 right = transform.right;
        Vector3 up = transform.up;
        Vector3 forward = transform.forward;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 sourceCorner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    Vector3 worldCorner = colliderComponent.transform.TransformPoint(sourceCorner);
                    Vector3 offset = worldCorner - origin;
                    float projectedX = Vector3.Dot(offset, right);
                    float projectedY = Vector3.Dot(offset, up);
                    float projectedZ = Vector3.Dot(offset, forward);

                    if (!hasBounds)
                    {
                        projectedBounds.MinX = projectedBounds.MaxX = projectedX;
                        projectedBounds.MinY = projectedBounds.MaxY = projectedY;
                        projectedBounds.MinZ = projectedBounds.MaxZ = projectedZ;
                        hasBounds = true;
                        continue;
                    }

                    projectedBounds.MinX = Mathf.Min(projectedBounds.MinX, projectedX);
                    projectedBounds.MaxX = Mathf.Max(projectedBounds.MaxX, projectedX);
                    projectedBounds.MinY = Mathf.Min(projectedBounds.MinY, projectedY);
                    projectedBounds.MaxY = Mathf.Max(projectedBounds.MaxY, projectedY);
                    projectedBounds.MinZ = Mathf.Min(projectedBounds.MinZ, projectedZ);
                    projectedBounds.MaxZ = Mathf.Max(projectedBounds.MaxZ, projectedZ);
                }
            }
        }

        return hasBounds;
    }

    private bool TryGetColliderSourceBounds(Collider colliderComponent, out Bounds localBounds)
    {
        localBounds = default;

        switch (colliderComponent)
        {
            case BoxCollider boxCollider:
            {
                Bounds boxBounds = new Bounds(boxCollider.center, boxCollider.size);
                localBounds = boxBounds;
                return true;
            }

            case SphereCollider sphereCollider:
            {
                float diameter = sphereCollider.radius * 2f;
                Bounds sphereBounds = new Bounds(sphereCollider.center, new Vector3(diameter, diameter, diameter));
                localBounds = sphereBounds;
                return true;
            }

            case CapsuleCollider capsuleCollider:
            {
                Vector3 capsuleSize = Vector3.one * (capsuleCollider.radius * 2f);
                capsuleSize[capsuleCollider.direction] = Mathf.Max(capsuleCollider.height, capsuleSize[capsuleCollider.direction]);
                Bounds capsuleBounds = new Bounds(capsuleCollider.center, capsuleSize);
                localBounds = capsuleBounds;
                return true;
            }

            case MeshCollider meshCollider when meshCollider.sharedMesh != null:
            {
                localBounds = meshCollider.sharedMesh.bounds;
                return true;
            }
        }

        return false;
    }

    public FemaleGridGroup AddFemaleGridGroup(FemaleGridGroup parentGroup, int insertIndex)
    {
        parentGroup ??= femaleGridRootGroup;

        FemaleGridGroup group = new FemaleGridGroup
        {
            Name = $"Group {GetTotalGroupCount() + 1}",
            Spacing = parentGroup.Spacing
        };

        insertIndex = Mathf.Clamp(insertIndex, 0, parentGroup.Children.Count);
        parentGroup.Children.Insert(insertIndex, FemaleGridHierarchyEntry.CreateGroup(group));
        EnsureValidHierarchy();
        return group;
    }

    public FemaleGridLayer AddFemaleGridLayer(FemaleGridGroup parentGroup, int insertIndex)
    {
        parentGroup ??= femaleGridRootGroup;

        // New layers default to a fully enabled matrix so the user starts from a usable support surface.
        FemaleGridLayer layer = new FemaleGridLayer();
        layer.Name = $"Female Layer {GetTotalLayerCount() + 1}";
        layer.Validate(maleGridSizeInCells);
        layer.Fill(true);
        insertIndex = Mathf.Clamp(insertIndex, 0, parentGroup.Children.Count);
        parentGroup.Children.Insert(insertIndex, FemaleGridHierarchyEntry.CreateLayer(layer));
        EnsureValidHierarchy();
        return layer;
    }

    public FemaleGridLayer DuplicateFemaleGridLayer(FemaleGridGroup parentGroup, int entryIndex)
    {
        if (parentGroup == null || entryIndex < 0 || entryIndex >= parentGroup.Children.Count)
        {
            return null;
        }

        FemaleGridHierarchyEntry sourceEntry = parentGroup.Children[entryIndex];
        if (!sourceEntry.IsLayer)
        {
            return null;
        }

        FemaleGridLayer duplicateLayer = sourceEntry.Layer.CreateCopy();
        duplicateLayer.Name = $"{sourceEntry.Layer.Name} Copy";
        parentGroup.Children.Insert(entryIndex + 1, FemaleGridHierarchyEntry.CreateLayer(duplicateLayer));
        EnsureValidHierarchy();
        return duplicateLayer;
    }

    public bool RemoveHierarchyEntry(FemaleGridGroup parentGroup, int entryIndex)
    {
        if (parentGroup == null || entryIndex < 0 || entryIndex >= parentGroup.Children.Count)
        {
            return false;
        }

        parentGroup.Children.RemoveAt(entryIndex);
        EnsureValidHierarchy();
        return true;
    }

    public bool MoveHierarchyEntry(FemaleGridGroup sourceParent, int sourceIndex, FemaleGridGroup targetParent, int targetIndex)
    {
        if (sourceParent == null || targetParent == null)
        {
            return false;
        }

        if (sourceIndex < 0 || sourceIndex >= sourceParent.Children.Count)
        {
            return false;
        }

        FemaleGridHierarchyEntry movingEntry = sourceParent.Children[sourceIndex];
        if (movingEntry.IsGroup && movingEntry.Group == targetParent)
        {
            return false;
        }

        sourceParent.Children.RemoveAt(sourceIndex);
        if (sourceParent == targetParent && sourceIndex < targetIndex)
        {
            targetIndex--;
        }

        targetIndex = Mathf.Clamp(targetIndex, 0, targetParent.Children.Count);
        targetParent.Children.Insert(targetIndex, movingEntry);
        EnsureValidHierarchy();
        return true;
    }

    public IEnumerable<FemaleGridLayer> EnumerateFemaleLayers()
    {
        foreach (FemaleGridLayer layer in EnumerateLayersRecursive(femaleGridRootGroup))
        {
            yield return layer;
        }
    }

    /// <summary>
    /// Returns the local height of every female grid layer defined on this authoring component,
    /// in the order they are enumerated. Used by the minimap to determine which surface layers
    /// exist and how tall they are.
    /// </summary>
    public List<float> GetFemaleLayerLocalHeights()
    {
        var result = new List<float>();
        foreach (FemaleGridLayer layer in EnumerateFemaleLayers())
        {
            result.Add(layer.LocalHeight);
        }
        return result;
    }

    public void EnsureValidHierarchy()
    {
        if (femaleGridRootGroup == null)
        {
            femaleGridRootGroup = new FemaleGridGroup();
        }

        if (string.IsNullOrWhiteSpace(femaleGridRootGroup.Name))
        {
            femaleGridRootGroup.Name = "Master Group";
        }

        femaleGridRootGroup.Validate(maleGridSizeInCells, true);
        // Use the root group's authored start height so each prefab can position its first layer independently.
        AssignComputedHeights(femaleGridRootGroup, femaleGridRootGroup.StartHeight);
    }

    public bool GetMaleCell(int xIndex, int zIndex)
    {
        int flatIndex = GetFlatIndex(maleGridSizeInCells, xIndex, zIndex);
        if (flatIndex < 0 || flatIndex >= enabledMaleCells.Count)
        {
            return false;
        }

        return enabledMaleCells[flatIndex];
    }

    public void SetMaleCell(int xIndex, int zIndex, bool enabled)
    {
        deriveMaleGridFromColliders = false;

        int flatIndex = GetFlatIndex(maleGridSizeInCells, xIndex, zIndex);
        if (flatIndex < 0 || flatIndex >= enabledMaleCells.Count)
        {
            return;
        }

        enabledMaleCells[flatIndex] = enabled;
    }

    public void FillMaleGrid(bool enabled)
    {
        deriveMaleGridFromColliders = false;

        ResizeMaleGridCells();

        FillMaleGridInternal(enabled);
    }

    private void FillMaleGridInternal(bool enabled)
    {
        ResizeMaleGridCells();

        for (int i = 0; i < enabledMaleCells.Count; i++)
        {
            enabledMaleCells[i] = enabled;
        }
    }

    public int GetEnabledMaleCellCount()
    {
        int enabledCount = 0;

        for (int i = 0; i < enabledMaleCells.Count; i++)
        {
            if (enabledMaleCells[i])
            {
                enabledCount++;
            }
        }

        return enabledCount;
    }

    private void OnDrawGizmosSelected()
    {
        // Gizmos are only a visual authoring aid. They do not affect gameplay logic directly.
        if (!drawGridGizmos)
        {
            return;
        }

        DrawMaleGridGizmos();
        DrawFemaleGridGizmos();
    }

    private void DrawMaleGridGizmos()
    {
        // Blue filled tiles show the single male grid on the underside of the object.
        if (!TryGetProjectedColliderBounds(out ProjectedColliderBounds projectedBounds))
        {
            return;
        }

        for (int z = 0; z < maleGridSizeInCells.y; z++)
        {
            for (int x = 0; x < maleGridSizeInCells.x; x++)
            {
                if (!GetMaleCell(x, z))
                {
                    continue;
                }

                Vector3 worldCenter = GetWorldCellCenter(projectedBounds.MinX, projectedBounds.MinZ, projectedBounds.MinY - MaleGizmoVerticalOffset, x, z);
                Vector3 worldSize = new Vector3(femaleTileSize, GizmoThickness, femaleTileSize);
                GetTileColors(x, z, true, out Color fillColor, out Color outlineColor);
                DrawFilledTileGizmo(worldCenter, worldSize, transform.rotation, fillColor, outlineColor);
            }
        }
    }

    private void DrawFemaleGridGizmos()
    {
        // Pink filled tiles show active female tiles on each authored support layer.
        if (!TryGetProjectedColliderBounds(out ProjectedColliderBounds projectedBounds))
        {
            return;
        }

        foreach (FemaleGridLayer layer in EnumerateFemaleLayers())
        {
            if (layer == null)
            {
                continue;
            }

            Vector2Int gridSize = layer.GridSizeInCells;

            for (int z = 0; z < gridSize.y; z++)
            {
                for (int x = 0; x < gridSize.x; x++)
                {
                    if (!layer.GetCell(x, z))
                    {
                        continue;
                    }

                    Vector3 worldCenter = GetWorldCellCenter(projectedBounds.MinX, projectedBounds.MinZ, projectedBounds.MinY + layer.LocalHeight, x, z);
                    Vector3 worldSize = new Vector3(femaleTileSize, GizmoThickness, femaleTileSize);
                    GetTileColors(x, z, false, out Color fillColor, out Color outlineColor);
                    DrawFilledTileGizmo(worldCenter, worldSize, transform.rotation, fillColor, outlineColor);
                }
            }
        }
    }

    private void GetTileColors(int xIndex, int zIndex, bool isMale, out Color fillColor, out Color outlineColor)
    {
        int subtilesPerFullTile = Mathf.Max(1, Mathf.RoundToInt(FullTileWorldSize / femaleTileSize));
        int tileX = xIndex / subtilesPerFullTile;
        int tileZ = zIndex / subtilesPerFullTile;
        int subtileX = xIndex % subtilesPerFullTile;
        int subtileZ = zIndex % subtilesPerFullTile;

        bool usePrimaryPalette = ((tileX + tileZ) & 1) == 0;
        bool useLightVariant = ((subtileX + subtileZ) & 1) == 0;

        if (isMale)
        {
            fillColor = usePrimaryPalette
                ? (useLightVariant ? MalePrimaryLight : MalePrimaryDark)
                : (useLightVariant ? MaleSecondaryLight : MaleSecondaryDark);
        }
        else
        {
            fillColor = usePrimaryPalette
                ? (useLightVariant ? FemalePrimaryLight : FemalePrimaryDark)
                : (useLightVariant ? FemaleSecondaryLight : FemaleSecondaryDark);
        }

        outlineColor = Color.Lerp(fillColor, Color.black, 0.3f);
        outlineColor.a = 0.95f;
    }

    private static void DrawFilledTileGizmo(Vector3 worldCenter, Vector3 worldSize, Quaternion worldRotation, Color fillColor, Color outlineColor)
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(worldCenter, worldRotation, Vector3.one);
        Gizmos.color = fillColor;
        Gizmos.DrawCube(Vector3.zero, worldSize);
        Gizmos.color = outlineColor;
        Gizmos.DrawWireCube(Vector3.zero, worldSize);
        Gizmos.matrix = previousMatrix;
    }

    private Vector3 GetWorldCellCenter(float minX, float minZ, float baseY, int xIndex, int zIndex)
    {
        float xOffset = minX + (xIndex * femaleTileSize) + (femaleTileSize * 0.5f);
        float zOffset = minZ + (zIndex * femaleTileSize) + (femaleTileSize * 0.5f);
        float yOffset = baseY + (GizmoThickness * 0.5f);
        return transform.position + (transform.right * xOffset) + (transform.up * yOffset) + (transform.forward * zOffset);
    }

    private void ResizeMaleGridCells()
    {
        int requiredCount = maleGridSizeInCells.x * maleGridSizeInCells.y;

        while (enabledMaleCells.Count < requiredCount)
        {
            enabledMaleCells.Add(true);
        }

        while (enabledMaleCells.Count > requiredCount)
        {
            enabledMaleCells.RemoveAt(enabledMaleCells.Count - 1);
        }
    }

    private static int GetFlatIndex(Vector2Int gridSize, int xIndex, int zIndex)
    {
        if (xIndex < 0 || xIndex >= gridSize.x || zIndex < 0 || zIndex >= gridSize.y)
        {
            return -1;
        }

        return zIndex * gridSize.x + xIndex;
    }

    private void AssignComputedHeights(FemaleGridGroup group, float startHeight)
    {
        float currentHeight = startHeight;

        for (int i = 0; i < group.Children.Count; i++)
        {
            FemaleGridHierarchyEntry child = group.Children[i];

            if (child.IsLayer)
            {
                child.Layer.ComputedHeight = currentHeight;
                currentHeight += group.Spacing;
                continue;
            }

            AssignComputedHeights(child.Group, currentHeight);
            currentHeight += group.Spacing;
        }
    }

    private IEnumerable<FemaleGridLayer> EnumerateLayersRecursive(FemaleGridGroup group)
    {
        foreach (FemaleGridHierarchyEntry child in group.Children)
        {
            if (child.IsLayer)
            {
                yield return child.Layer;
                continue;
            }

            foreach (FemaleGridLayer nestedLayer in EnumerateLayersRecursive(child.Group))
            {
                yield return nestedLayer;
            }
        }
    }

    private int GetTotalLayerCount()
    {
        int count = 0;
        foreach (FemaleGridLayer _ in EnumerateFemaleLayers())
        {
            count++;
        }

        return count;
    }

    private int GetTotalGroupCount()
    {
        return CountGroupsRecursive(femaleGridRootGroup);
    }

    private int CountGroupsRecursive(FemaleGridGroup group)
    {
        int count = 1;

        foreach (FemaleGridHierarchyEntry child in group.Children)
        {
            if (child.IsGroup)
            {
                count += CountGroupsRecursive(child.Group);
            }
        }

        return count;
    }
}

[Serializable]
public class FemaleGridHierarchyEntry
{
    [SerializeField] private FemaleGridEntryKind kind = FemaleGridEntryKind.Layer;
    [SerializeReference] private FemaleGridLayer layer;
    [SerializeReference] private FemaleGridGroup group;

    public bool IsLayer => kind == FemaleGridEntryKind.Layer && layer != null;
    public bool IsGroup => kind == FemaleGridEntryKind.Group && group != null;
    public FemaleGridLayer Layer => layer;
    public FemaleGridGroup Group => group;

    public static FemaleGridHierarchyEntry CreateLayer(FemaleGridLayer layer)
    {
        return new FemaleGridHierarchyEntry
        {
            kind = FemaleGridEntryKind.Layer,
            layer = layer
        };
    }

    public static FemaleGridHierarchyEntry CreateGroup(FemaleGridGroup group)
    {
        return new FemaleGridHierarchyEntry
        {
            kind = FemaleGridEntryKind.Group,
            group = group
        };
    }
}

public enum FemaleGridEntryKind
{
    Layer,
    Group
}

[Serializable]
/// <summary>
/// Plain-English purpose:
/// A female-grid group is an ordered container of layers or more groups. The group's spacing value controls the
/// vertical gap between each child entry inside that group.
/// </summary>
public class FemaleGridGroup
{
    [SerializeField] private string name = "Group";
    [Min(0.01f)]
    [SerializeField] private float startHeight = 0.5f;
    [Min(0.1f)]
    [SerializeField] private float spacing = 0.5f;
    [SerializeReference] private List<FemaleGridHierarchyEntry> children = new List<FemaleGridHierarchyEntry>();

    public string Name
    {
        get => name;
        set => name = string.IsNullOrWhiteSpace(value) ? "Group" : value;
    }

    // The height of this group's first layer above the male grid. Only applied when this group
    // is the root group; nested groups inherit their start position from the parent's progression.
    public float StartHeight
    {
        get => startHeight;
        set => startHeight = Mathf.Max(0.01f, value);
    }

    public float Spacing
    {
        get => spacing;
        set => spacing = Mathf.Max(0.1f, value);
    }

    public List<FemaleGridHierarchyEntry> Children => children;

    public void Validate(Vector2Int maleGridSizeInCells, bool isRoot = false)
    {
        startHeight = Mathf.Max(0.01f, startHeight);
        spacing = Mathf.Max(0.1f, spacing);

        if (isRoot && string.IsNullOrWhiteSpace(name))
        {
            name = "Master Group";
        }

        for (int i = children.Count - 1; i >= 0; i--)
        {
            FemaleGridHierarchyEntry child = children[i];
            if (child == null || (!child.IsLayer && !child.IsGroup))
            {
                children.RemoveAt(i);
                continue;
            }

            if (child.IsLayer)
            {
                child.Layer.Validate(maleGridSizeInCells);
                continue;
            }

            child.Group.Validate(maleGridSizeInCells);
        }
    }
}

[Serializable]
/// <summary>
/// Plain-English purpose:
/// This stores one female grid layer on an object, including its computed height above the male-grid base, size, clearance rules,
/// and the on/off cell matrix that says which female tiles can receive another object.
/// </summary>
public class FemaleGridLayer
{
    [SerializeField] private string name = "Female Layer";
    // Height is computed from the spacing rules of parent groups.
    [SerializeField] private float computedHeight = 1f;

    // When enabled, this layer automatically follows the male grid width and length.
    [SerializeField] private bool useMaleGridSize = true;

    // Width and length of this female grid layer in cell units.
    [SerializeField] private Vector2Int gridSizeInCells = new Vector2Int(2, 2);

    // Open-above means there is no authored ceiling limit directly above this layer.
    [SerializeField] private bool openAbove = true;

    // When openAbove is false, this is the maximum height a child object may occupy above this layer.
    [Min(0.1f)]
    [SerializeField] private float clearanceAbove = 1f;

    // Flattened cell list stored row-by-row. True means the female tile is available for placement.
    [SerializeField] private List<bool> enabledCells = new List<bool>();

    public string Name
    {
        get => name;
        set => name = string.IsNullOrWhiteSpace(value) ? "Female Layer" : value;
    }

    public float LocalHeight
    {
        get => computedHeight;
    }

    public float ComputedHeight
    {
        get => computedHeight;
        set => computedHeight = value;
    }

    public bool UseMaleGridSize
    {
        get => useMaleGridSize;
        set => useMaleGridSize = value;
    }

    public Vector2Int GridSizeInCells
    {
        get => gridSizeInCells;
        set => gridSizeInCells = value;
    }

    public bool OpenAbove
    {
        get => openAbove;
        set => openAbove = value;
    }

    public float ClearanceAbove
    {
        get => clearanceAbove;
        set => clearanceAbove = Mathf.Max(0.1f, value);
    }

    public void Validate(Vector2Int maleGridSizeInCells)
    {
        // Keep the serialized matrix shape aligned with the current configured dimensions.
        if (useMaleGridSize)
        {
            gridSizeInCells = maleGridSizeInCells;
        }

        gridSizeInCells.x = Mathf.Max(1, gridSizeInCells.x);
        gridSizeInCells.y = Mathf.Max(1, gridSizeInCells.y);
        clearanceAbove = Mathf.Max(0.1f, clearanceAbove);
        ResizeEnabledCells();
    }

    public bool GetCell(int xIndex, int zIndex)
    {
        int flatIndex = GetFlatIndex(gridSizeInCells, xIndex, zIndex);
        if (flatIndex < 0 || flatIndex >= enabledCells.Count)
        {
            return false;
        }

        return enabledCells[flatIndex];
    }

    public void SetCell(int xIndex, int zIndex, bool enabled)
    {
        int flatIndex = GetFlatIndex(gridSizeInCells, xIndex, zIndex);
        if (flatIndex < 0 || flatIndex >= enabledCells.Count)
        {
            return;
        }

        enabledCells[flatIndex] = enabled;
    }

    public void Fill(bool enabled)
    {
        // Useful for quickly turning a whole layer on or off before painting details.
        ResizeEnabledCells();

        for (int i = 0; i < enabledCells.Count; i++)
        {
            enabledCells[i] = enabled;
        }
    }

    public FemaleGridLayer CreateCopy()
    {
        FemaleGridLayer copy = new FemaleGridLayer
        {
            name = name,
            computedHeight = computedHeight,
            useMaleGridSize = useMaleGridSize,
            gridSizeInCells = gridSizeInCells,
            openAbove = openAbove,
            clearanceAbove = clearanceAbove,
            enabledCells = new List<bool>(enabledCells)
        };

        return copy;
    }

    private void ResizeEnabledCells()
    {
        // The matrix is stored as a flat list, so we resize the list to width * length.
        int requiredCount = gridSizeInCells.x * gridSizeInCells.y;

        while (enabledCells.Count < requiredCount)
        {
            enabledCells.Add(true);
        }

        while (enabledCells.Count > requiredCount)
        {
            enabledCells.RemoveAt(enabledCells.Count - 1);
        }
    }

    private static int GetFlatIndex(Vector2Int gridSize, int xIndex, int zIndex)
    {
        // Converts 2D grid coordinates into a single list index.
        if (xIndex < 0 || xIndex >= gridSize.x || zIndex < 0 || zIndex >= gridSize.y)
        {
            return -1;
        }

        return zIndex * gridSize.x + xIndex;
    }
}
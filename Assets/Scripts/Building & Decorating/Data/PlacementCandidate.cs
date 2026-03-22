using UnityEngine;

/// <summary>
/// Plain-English purpose:
/// A snapshot of one valid placement position produced by PlacementSolver.
/// Passed to BuildModeController to know where and how to instantiate furniture.
/// </summary>
public readonly struct PlacementCandidate
{
    /// <summary>World-space position at which the furniture should be placed.</summary>
    public readonly Vector3 WorldPosition;

    /// <summary>World-space rotation the furniture should use at spawn.</summary>
    public readonly Quaternion Rotation;

    /// <summary>
    /// The bottom-left terrain subtile cell that lines up with the furniture's male grid (0,0) cell.
    /// Stored so BuildModeController can later check occupancy without re-projecting world positions.
    /// </summary>
    public readonly Vector2Int TerrainCellOrigin;

    public PlacementCandidate(Vector3 worldPosition, Quaternion rotation, Vector2Int terrainCellOrigin)
    {
        WorldPosition = worldPosition;
        Rotation = rotation;
        TerrainCellOrigin = terrainCellOrigin;
    }
}

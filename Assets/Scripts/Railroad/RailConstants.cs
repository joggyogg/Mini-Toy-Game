using UnityEngine;

/// <summary>
/// Shared constants for the railroad drawing and snapping system.
/// </summary>
public static class RailConstants
{
    /// <summary>Minimum turning radius in world units (tiles). Curves tighter than this are rejected.</summary>
    public const float MinTurnRadius = 5f;

    /// <summary>Maximum range (tiles) from the rail origin to search for candidates.</summary>
    public const int MaxCurveRange = 100;

    /// <summary>Radius (tiles) around the cursor within which candidate dots are generated.</summary>
    public const int CandidateViewRadius = 5;

    /// <summary>Maximum arc sweep in degrees per curve segment. Caps at 90° to prevent U-turns.</summary>
    public const float MaxArcAngleDeg = 90f;

    public const float MinSegmentLength = 1f;
    public const float MaxStraightLength = 100f;

    /// <summary>Cardinal directions in world XZ.</summary>
    public static readonly Vector3[] Cardinals =
    {
        Vector3.forward,  // North (+Z)
        Vector3.back,     // South (-Z)
        Vector3.right,    // East  (+X)
        Vector3.left      // West  (-X)
    };
}

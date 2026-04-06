using UnityEngine;

/// <summary>
/// Shared constants for the railroad drawing and snapping system.
/// </summary>
public static class RailConstants
{
    /// <summary>
    /// Preset turning radii in world units.
    /// Index 0 = straight, 1 = gentle, 2 = medium, 3 = tight.
    /// </summary>
    public static readonly float[] TurnRadii = { float.PositiveInfinity, 128f, 80f, 48f };

    /// <summary>Available arc sweep angles in degrees for curved segments.</summary>
    public static readonly float[] CurveAnglesDeg = { 22.5f, 45f, 67.5f, 90f };

    public const float MinSegmentLength = 1f;
    public const float MaxStraightLength = 100f;

    /// <summary>Maximum arc sweep per segment (radians). Keeps single-Bezier approximation accurate.</summary>
    public static readonly float MaxArcAngleRad = Mathf.PI * 0.5f; // 90°

    /// <summary>Cardinal directions in world XZ.</summary>
    public static readonly Vector3[] Cardinals =
    {
        Vector3.forward,  // North (+Z)
        Vector3.back,     // South (-Z)
        Vector3.right,    // East  (+X)
        Vector3.left      // West  (-X)
    };
}

using UnityEngine;

/// <summary>
/// Shared constants for the railroad drawing and snapping system.
/// </summary>
public static class RailConstants
{
    /// <summary>
    /// Preset turning radii in world units. Scaled for a 16-unit bogie spacing at a 3:1 minimum ratio.
    /// Index 0 = straight, 1 = gentle (8×), 2 = medium (5×), 3 = tight (3× — minimum safe).
    /// </summary>
    public static readonly float[] TurnRadii = { float.PositiveInfinity, 128f, 80f, 48f };

    /// <summary>Cursor angle thresholds (degrees from forward) for selecting turn radius.</summary>
    public const float StraightThresholdDeg = 5f;
    public const float GentleThresholdDeg   = 15f;
    public const float TightThresholdDeg    = 30f;

    public const float MinSegmentLength = 1f;
    public const float MaxSegmentLength = 10f;

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

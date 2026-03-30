using UnityEngine;

/// <summary>Shared gizmo drawing utilities for the railroad system.</summary>
public static class RailGizmos
{
    /// <summary>Draws a 3-axis diamond (octahedron wireframe) centred at the given point.</summary>
    public static void DrawDiamond(Vector3 centre, float size)
    {
        Vector3 u = Vector3.up      * size;
        Vector3 r = Vector3.right   * size;
        Vector3 f = Vector3.forward * size;
        // XY plane ring
        Gizmos.DrawLine(centre + u, centre + r);
        Gizmos.DrawLine(centre + r, centre - u);
        Gizmos.DrawLine(centre - u, centre - r);
        Gizmos.DrawLine(centre - r, centre + u);
        // ZY plane ring
        Gizmos.DrawLine(centre + u, centre + f);
        Gizmos.DrawLine(centre + f, centre - u);
        Gizmos.DrawLine(centre - u, centre - f);
        Gizmos.DrawLine(centre - f, centre + u);
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

/// <summary>
/// Scene-level container for the rail network's spline data.
/// Wraps a SplineContainer and provides a simple API for adding knots and finishing splines.
/// Attach to a GameObject in the scene (like TerrainGridAuthoring).
/// </summary>
[RequireComponent(typeof(SplineContainer))]
public class RailNetworkAuthoring : MonoBehaviour
{
    private SplineContainer container;
    private int activeSplineIndex = -1;

    public SplineContainer Container
    {
        get
        {
            if (container == null)
                container = GetComponent<SplineContainer>();
            return container;
        }
    }

    public int SplineCount => Container.Splines.Count;

    public bool IsDrawing => activeSplineIndex >= 0
        && activeSplineIndex < Container.Splines.Count
        && Container.Splines[activeSplineIndex].Count > 0;

    private void Awake()
    {
        container = GetComponent<SplineContainer>();

        // SplineContainer starts with one empty spline by default. Clear it.
        Container.Splines = new List<Spline>();
        activeSplineIndex = -1;
    }

    /// <summary>
    /// Appends a knot at the given world position to the active spline.
    /// If no spline is active, starts a new one.
    /// Returns the spline-local position used.
    /// </summary>
    public float3 AddKnot(Vector3 worldPos)
    {
        if (activeSplineIndex < 0)
            StartNewSpline();

        float3 localPos = transform.InverseTransformPoint(worldPos);
        Container.Splines[activeSplineIndex].Add(localPos, TangentMode.AutoSmooth);
        return localPos;
    }

    /// <summary>
    /// Appends a knot with explicit tangent handles (world space).
    /// Uses TangentMode.Broken for independent tangent control.
    /// </summary>
    public float3 AddKnotExplicit(Vector3 worldPos, Vector3 worldTangentIn, Vector3 worldTangentOut)
    {
        if (activeSplineIndex < 0)
            StartNewSpline();

        float3 localPos = transform.InverseTransformPoint(worldPos);
        float3 localTanIn = (float3)transform.InverseTransformVector(worldTangentIn);
        float3 localTanOut = (float3)transform.InverseTransformVector(worldTangentOut);

        var knot = new BezierKnot(localPos, localTanIn, localTanOut, quaternion.identity);
        Container.Splines[activeSplineIndex].Add(knot, TangentMode.Broken);
        return localPos;
    }

    /// <summary>
    /// Updates the TangentOut of the last knot on the active spline (world space).
    /// </summary>
    public void SetLastKnotTangentOut(Vector3 worldTangentOut)
    {
        if (activeSplineIndex < 0) return;
        var spline = Container.Splines[activeSplineIndex];
        if (spline.Count == 0) return;

        int idx = spline.Count - 1;
        var knot = spline[idx];
        knot.TangentOut = (float3)transform.InverseTransformVector(worldTangentOut);
        spline[idx] = knot;
    }

    /// <summary>
    /// Returns the world position of the last knot on the active spline, or null if not drawing.
    /// </summary>
    public Vector3? GetLastKnotWorld()
    {
        if (activeSplineIndex < 0) return null;
        var spline = Container.Splines[activeSplineIndex];
        if (spline.Count == 0) return null;
        float3 local = spline[spline.Count - 1].Position;
        return transform.TransformPoint(local);
    }

    /// <summary>
    /// Finishes the current spline. The next AddKnot call will start a new one.
    /// Removes the spline if it has fewer than 2 knots (not a valid path).
    /// </summary>
    public void FinishCurrentSpline()
    {
        if (activeSplineIndex >= 0 && activeSplineIndex < Container.Splines.Count)
        {
            var spline = Container.Splines[activeSplineIndex];
            if (spline.Count < 2)
            {
                // Remove invalid single-knot spline
                var list = new List<Spline>(Container.Splines);
                list.RemoveAt(activeSplineIndex);
                Container.Splines = list;
            }
        }
        activeSplineIndex = -1;
    }

    /// <summary>
    /// Returns the world-space position on a given spline at parametric t [0..1].
    /// </summary>
    public Vector3 EvaluatePositionWorld(int splineIndex, float t)
    {
        // SplineContainer.EvaluatePosition already returns world space.
        return (Vector3)Container.EvaluatePosition(splineIndex, t);
    }

    /// <summary>
    /// Returns the index of the last finished spline (with 2+ knots, not the active one).
    /// Returns -1 if no finished spline exists.
    /// </summary>
    public int GetLastFinishedSplineIndex()
    {
        for (int i = Container.Splines.Count - 1; i >= 0; i--)
        {
            if (i == activeSplineIndex) continue;
            if (Container.Splines[i].Count >= 2) return i;
        }
        return -1;
    }

    private void StartNewSpline()
    {
        var list = new List<Spline>(Container.Splines);
        list.Add(new Spline());
        Container.Splines = list;
        activeSplineIndex = list.Count - 1;
    }
}

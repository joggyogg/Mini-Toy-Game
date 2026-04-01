using System;
using UnityEngine;

/// <summary>
/// A stretch of track between two RailNodes.
/// Wraps exactly one Unity Spline (by index) in the SplineContainer.
/// </summary>
[Serializable]
public class RailSegment
{
    [NonSerialized] public RailNode startNode;
    [NonSerialized] public RailNode endNode;

    /// <summary>Index into SplineContainer.Splines.</summary>
    public int splineIndex;

    public RailSegment(RailNode start, RailNode end, int splineIdx)
    {
        startNode = start;
        endNode = end;
        splineIndex = splineIdx;
    }

    /// <summary>Given one endpoint, returns the other.</summary>
    public RailNode OtherEnd(RailNode from)
    {
        if (from == startNode) return endNode;
        if (from == endNode) return startNode;
        return null;
    }

    /// <summary>Returns true if this segment connects the given node.</summary>
    public bool Connects(RailNode node) => node == startNode || node == endNode;
}

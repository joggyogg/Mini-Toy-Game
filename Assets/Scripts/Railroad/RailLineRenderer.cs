using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages per-segment track visuals. Each RailSegment gets its own child
/// GameObject with dual rails + cross-ties. Supports per-segment material
/// swaps for highlighting (junction switch feedback, deletion preview).
/// </summary>
public class RailLineRenderer : MonoBehaviour
{
    [SerializeField] private int samplesPerSegment = 20;
    [SerializeField] private float railWidth = 0.06f;
    [SerializeField] private float tieWidth = 0.08f;
    [SerializeField] private float heightOffset = 0.05f;
    [SerializeField] private Color railColor = Color.black;
    [SerializeField] private Color tieColor = new Color(0.45f, 0.3f, 0.15f); // brown

    [Tooltip("Half the gauge: each rail is offset this far from center. 0.5 = 1 tile total width.")]
    [SerializeField] private float gaugeHalf = 0.5f;

    [Tooltip("World-space distance between cross-ties.")]
    [SerializeField] private float tieSpacing = 1f;

    private Material railMaterial;
    private Material tieMaterial;

    /// <summary>Per-segment visual data keyed by spline index.</summary>
    private readonly Dictionary<int, SegmentVisual> segmentVisuals = new();

    private class SegmentVisual
    {
        public GameObject root;
        public LineRenderer leftRail;
        public LineRenderer rightRail;
        public readonly List<LineRenderer> ties = new();
    }

    private void Awake()
    {
        railMaterial = new Material(Shader.Find("Sprites/Default"));
        tieMaterial  = new Material(Shader.Find("Sprites/Default"));
    }

    /// <summary>
    /// Full rebuild from the RailGraph. Destroys all existing visuals and
    /// creates new ones for each segment in the graph.
    /// </summary>
    public void RebuildFromGraph(RailGraph graph)
    {
        DestroyAllVisuals();
        if (graph == null || graph.Network == null) return;

        var network = graph.Network;
        for (int i = 0; i < graph.Segments.Count; i++)
        {
            var seg = graph.Segments[i];
            if (seg.splineIndex >= 0 && seg.splineIndex < network.SplineCount)
                BuildVisualForSpline(network, seg.splineIndex);
        }
    }

    /// <summary>
    /// Legacy rebuild that works directly from RailNetworkAuthoring (no graph).
    /// Used during drawing before a segment is finalized in the graph.
    /// </summary>
    public void RebuildFromSplines(RailNetworkAuthoring network)
    {
        DestroyAllVisuals();
        if (network == null) return;

        for (int s = 0; s < network.SplineCount; s++)
        {
            if (network.Container.Splines[s].Count >= 2)
                BuildVisualForSpline(network, s);
        }
    }

    /// <summary>
    /// Adds or rebuilds the visual for a single spline index.
    /// </summary>
    public void RebuildSegmentVisual(RailNetworkAuthoring network, int splineIndex)
    {
        DestroyVisual(splineIndex);
        if (network == null) return;
        if (splineIndex < 0 || splineIndex >= network.SplineCount) return;
        if (network.Container.Splines[splineIndex].Count < 2) return;
        BuildVisualForSpline(network, splineIndex);
    }

    /// <summary>
    /// Destroys the visual for a single spline index.
    /// </summary>
    public void DestroySegmentVisual(int splineIndex)
    {
        DestroyVisual(splineIndex);
    }

    /// <summary>
    /// Sets the rail color for a specific segment (for highlight/delete preview).
    /// </summary>
    public void SetSegmentColor(int splineIndex, Color color)
    {
        if (!segmentVisuals.TryGetValue(splineIndex, out var vis)) return;
        vis.leftRail.startColor = color;
        vis.leftRail.endColor = color;
        vis.rightRail.startColor = color;
        vis.rightRail.endColor = color;
    }

    /// <summary>
    /// Resets a segment's rail color back to default.
    /// </summary>
    public void ResetSegmentColor(int splineIndex)
    {
        SetSegmentColor(splineIndex, railColor);
    }

    // ─── Internal ────────────────────────────────────────────────────────

    private void BuildVisualForSpline(RailNetworkAuthoring network, int splineIndex)
    {
        int knotCount = network.Container.Splines[splineIndex].Count;
        if (knotCount < 2) return;

        var vis = new SegmentVisual();
        vis.root = new GameObject($"SegmentVisual_{splineIndex}");
        vis.root.transform.SetParent(transform);

        // Sample the spline.
        int totalSamples = (knotCount - 1) * samplesPerSegment + 1;
        var centers = new Vector3[totalSamples];
        var rights  = new Vector3[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / (totalSamples - 1);
            Vector3 pos = network.EvaluatePositionWorld(splineIndex, t);
            pos.y += heightOffset;
            centers[i] = pos;
        }

        for (int i = 0; i < totalSamples; i++)
        {
            Vector3 fwd;
            if (i < totalSamples - 1) fwd = centers[i + 1] - centers[i];
            else fwd = centers[i] - centers[i - 1];
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            rights[i] = Vector3.Cross(Vector3.up, fwd.normalized);
        }

        var leftPositions  = new Vector3[totalSamples];
        var rightPositions = new Vector3[totalSamples];
        for (int i = 0; i < totalSamples; i++)
        {
            leftPositions[i]  = centers[i] - rights[i] * gaugeHalf;
            rightPositions[i] = centers[i] + rights[i] * gaugeHalf;
        }

        // Left rail.
        vis.leftRail = CreateRailLine(vis.root.transform, "Left Rail");
        vis.leftRail.positionCount = totalSamples;
        vis.leftRail.SetPositions(leftPositions);

        // Right rail.
        vis.rightRail = CreateRailLine(vis.root.transform, "Right Rail");
        vis.rightRail.positionCount = totalSamples;
        vis.rightRail.SetPositions(rightPositions);

        // Cross-ties via arc-length interpolation.
        float[] arcLen = new float[totalSamples];
        arcLen[0] = 0f;
        for (int i = 1; i < totalSamples; i++)
            arcLen[i] = arcLen[i - 1] + Vector3.Distance(centers[i], centers[i - 1]);

        float totalLength = arcLen[totalSamples - 1];
        int tieCount = Mathf.FloorToInt(totalLength / tieSpacing) + 1;

        for (int ti = 0; ti < tieCount; ti++)
        {
            float targetDist = ti * tieSpacing;
            int lo = 0, hi = totalSamples - 1;
            while (lo < hi - 1)
            {
                int mid = (lo + hi) >> 1;
                if (arcLen[mid] < targetDist) lo = mid; else hi = mid;
            }
            float segLen = arcLen[hi] - arcLen[lo];
            float frac = segLen > 0f ? (targetDist - arcLen[lo]) / segLen : 0f;
            Vector3 left  = Vector3.Lerp(leftPositions[lo],  leftPositions[hi],  frac);
            Vector3 right = Vector3.Lerp(rightPositions[lo], rightPositions[hi], frac);
            var tieLR = CreateTie(vis.root.transform, left, right);
            vis.ties.Add(tieLR);
        }

        segmentVisuals[splineIndex] = vis;
    }

    private LineRenderer CreateRailLine(Transform parent, string label)
    {
        var obj = new GameObject(label);
        obj.transform.SetParent(parent);
        var lr = obj.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.startWidth = railWidth;
        lr.endWidth = railWidth;
        lr.material = railMaterial;
        lr.startColor = railColor;
        lr.endColor = railColor;
        lr.positionCount = 0;
        return lr;
    }

    private LineRenderer CreateTie(Transform parent, Vector3 left, Vector3 right)
    {
        var obj = new GameObject("Tie");
        obj.transform.SetParent(parent);
        var lr = obj.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.startWidth = tieWidth;
        lr.endWidth = tieWidth;
        lr.material = tieMaterial;
        lr.startColor = tieColor;
        lr.endColor = tieColor;
        lr.positionCount = 2;
        lr.SetPosition(0, left);
        lr.SetPosition(1, right);
        return lr;
    }

    private void DestroyVisual(int splineIndex)
    {
        if (segmentVisuals.TryGetValue(splineIndex, out var vis))
        {
            if (vis.root != null) Destroy(vis.root);
            segmentVisuals.Remove(splineIndex);
        }
    }

    private void DestroyAllVisuals()
    {
        foreach (var kvp in segmentVisuals)
        {
            if (kvp.Value.root != null) Destroy(kvp.Value.root);
        }
        segmentVisuals.Clear();
    }
}

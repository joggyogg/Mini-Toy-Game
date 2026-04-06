using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

/// <summary>
/// Topology layer on top of RailNetworkAuthoring.
/// Tracks nodes (connection points) and segments (track between two nodes).
/// Each segment maps 1:1 to a Unity Spline in the SplineContainer.
/// </summary>
public class RailGraph : MonoBehaviour
{
    [SerializeField] private RailNetworkAuthoring network;
    [SerializeField] private GameObject switchBoxPrefab;

    private readonly List<RailNode> nodes = new();
    private readonly List<RailSegment> segments = new();
    private readonly Dictionary<RailNode, JunctionSwitchBox> switchBoxInstances = new();

    public IReadOnlyList<RailNode> Nodes => nodes;
    public IReadOnlyList<RailSegment> Segments => segments;
    public RailNetworkAuthoring Network => network;

    private void Reset()
    {
        if (network == null) network = GetComponent<RailNetworkAuthoring>();
    }

    private void Awake()
    {
        if (network == null) network = GetComponent<RailNetworkAuthoring>();
    }

    // ─── Node management ─────────────────────────────────────────────────

    /// <summary>
    /// Finds an existing node within tolerance of the given position, or null.
    /// </summary>
    public RailNode FindNodeAtPosition(Vector3 pos, float tolerance = 0.5f)
    {
        float tolSq = tolerance * tolerance;
        RailNode best = null;
        float bestSq = float.MaxValue;
        for (int i = 0; i < nodes.Count; i++)
        {
            Vector3 diff = nodes[i].worldPosition - pos;
            diff.y = 0f;
            float dSq = diff.sqrMagnitude;
            if (dSq < tolSq && dSq < bestSq)
            {
                bestSq = dSq;
                best = nodes[i];
            }
        }
        return best;
    }

    /// <summary>
    /// Finds an existing node at the position, or creates a new one.
    /// </summary>
    public RailNode GetOrCreateNode(Vector3 worldPos, float tolerance = 0.5f)
    {
        RailNode existing = FindNodeAtPosition(worldPos, tolerance);
        if (existing != null) return existing;

        var node = new RailNode(worldPos, WorldToTile(worldPos));
        nodes.Add(node);
        return node;
    }

    /// <summary>
    /// Returns all junction nodes (3+ connections).
    /// </summary>
    public List<RailNode> GetJunctionNodes()
    {
        var result = new List<RailNode>();
        for (int i = 0; i < nodes.Count; i++)
            if (nodes[i].IsJunction)
                result.Add(nodes[i]);
        return result;
    }

    // ─── Segment management ──────────────────────────────────────────────

    /// <summary>
    /// Registers a segment between two nodes for the given spline index.
    /// Wires up node connections with exit directions.
    /// </summary>
    public RailSegment RegisterSegment(RailNode from, RailNode to, int splineIndex,
                                        Vector3 fromExitDir, Vector3 toExitDir)
    {
        var seg = new RailSegment(from, to, splineIndex);
        segments.Add(seg);
        from.AddConnection(seg, fromExitDir);
        to.AddConnection(seg, toExitDir);

        Debug.Log($"[RailGraph] Segment registered: spline {splineIndex} | " +
                  $"Nodes: {nodes.Count}, Segments: {segments.Count} | " +
                  $"From ({from.worldPosition}) [{from.ConnectionCount} conn] → " +
                  $"To ({to.worldPosition}) [{to.ConnectionCount} conn]");

        // Manage switch boxes for nodes that became/updated junctions.
        UpdateSwitchBox(from);
        UpdateSwitchBox(to);

        return seg;
    }

    /// <summary>
    /// Removes a segment from the graph, disconnecting it from both nodes.
    /// Also removes the spline from the container and reindexes remaining segments.
    /// Orphaned nodes (0 connections) are removed.
    /// </summary>
    public void RemoveSegment(RailSegment seg)
    {
        int removedIndex = seg.splineIndex;

        // Disconnect from nodes.
        seg.startNode?.RemoveConnection(seg);
        seg.endNode?.RemoveConnection(seg);

        // Remove from segment list.
        segments.Remove(seg);

        // Remove the spline from the container.
        if (network != null)
            network.RemoveSpline(removedIndex);

        // Reindex: all segments with splineIndex > removedIndex need decrementing.
        for (int i = 0; i < segments.Count; i++)
        {
            if (segments[i].splineIndex > removedIndex)
                segments[i].splineIndex--;
        }

        // Notify locomotives so their stored spline indices stay in sync.
        Locomotive.NotifySplineRemoved(removedIndex);

        // Update switch boxes for nodes that may have lost junction status.
        if (seg.startNode != null) UpdateSwitchBox(seg.startNode);
        if (seg.endNode != null) UpdateSwitchBox(seg.endNode);

        // Clean up orphaned nodes.
        CleanupOrphanedNodes();
    }

    /// <summary>
    /// Finds the segment that owns the given spline index, or null.
    /// </summary>
    public RailSegment FindSegmentBySpline(int splineIndex)
    {
        for (int i = 0; i < segments.Count; i++)
            if (segments[i].splineIndex == splineIndex)
                return segments[i];
        return null;
    }

    /// <summary>
    /// Given a node, returns all segments connected to it.
    /// </summary>
    public List<RailSegment> GetSegmentsAtNode(RailNode node)
    {
        return new List<RailSegment>(node.connections);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private void CleanupOrphanedNodes()
    {
        for (int i = nodes.Count - 1; i >= 0; i--)
        {
            if (nodes[i].ConnectionCount == 0)
            {
                DestroySwitchBox(nodes[i]);
                nodes.RemoveAt(i);
            }
        }
    }

    // ─── Switch Box Lifecycle ────────────────────────────────────────────

    /// <summary>
    /// Creates, updates, or destroys the switch box for a node depending on
    /// whether it is currently a junction (3+ connections).
    /// </summary>
    private void UpdateSwitchBox(RailNode node)
    {
        if (node.IsJunction)
        {
            if (!switchBoxInstances.ContainsKey(node))
                SpawnSwitchBox(node);
            else
                switchBoxInstances[node].Refresh(node);
        }
        else
        {
            DestroySwitchBox(node);
        }
    }

    private void SpawnSwitchBox(RailNode node)
    {
        Vector3 pos = node.worldPosition + new Vector3(2f, 0f, 2f);
        GameObject root;

        if (switchBoxPrefab != null)
        {
            // ── Prefab path ───────────────────────────────────────────────────
            // Your prefab must contain:
            //   • A Canvas (anywhere in the hierarchy)
            //       └─ A RawImage child that fills the canvas
            // Everything else — model, scale, canvas position — you control in the prefab.
            root = Instantiate(switchBoxPrefab, pos, Quaternion.identity, transform);
            root.name = $"SwitchBox_{node.GetHashCode()}";

            // Force world-space and add billboard if not already there.
            var canvas = root.GetComponentInChildren<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                if (canvas.GetComponent<WorldCanvasFaceCamera>() == null)
                    canvas.gameObject.AddComponent<WorldCanvasFaceCamera>();
            }

            // Add JunctionGateUI onto the RawImage if not already present.
            var rawImage = root.GetComponentInChildren<UnityEngine.UI.RawImage>();
            if (rawImage != null && rawImage.GetComponent<JunctionGateUI>() == null)
                rawImage.gameObject.AddComponent<JunctionGateUI>();
        }
        else
        {
            // ── Procedural fallback (no prefab assigned) ──────────────────────
            root = new GameObject($"SwitchBox_{node.GetHashCode()}");
            root.transform.SetParent(transform, false);
            root.transform.position = pos;

            // Physical box placeholder.
            GameObject boxMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            boxMesh.name = "BoxMesh";
            boxMesh.transform.SetParent(root.transform, false);
            boxMesh.transform.localScale = new Vector3(0.6f, 0.8f, 0.2f);

            // World-space canvas.
            GameObject canvasObj = new GameObject("Canvas");
            canvasObj.transform.SetParent(root.transform, false);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform canvasRT = canvas.GetComponent<RectTransform>();
            canvasRT.sizeDelta = new Vector2(100, 100);
            canvasRT.localScale = Vector3.one * 0.25f;
            canvasRT.localPosition = new Vector3(0, 14f, 0);
            canvasObj.AddComponent<WorldCanvasFaceCamera>();

            // RawImage.
            GameObject imgObj = new GameObject("GateImage");
            imgObj.transform.SetParent(canvasObj.transform, false);
            var rawImage = imgObj.AddComponent<UnityEngine.UI.RawImage>();
            RectTransform imgRT = rawImage.GetComponent<RectTransform>();
            imgRT.anchorMin = Vector2.zero;
            imgRT.anchorMax = Vector2.one;
            imgRT.offsetMin = Vector2.zero;
            imgRT.offsetMax = Vector2.zero;
            rawImage.color = Color.white;
            imgObj.AddComponent<JunctionGateUI>();
        }

        // JunctionSwitchBox always on root — finds GateUI itself.
        var box = root.AddComponent<JunctionSwitchBox>();
        box.Initialize(node);

        switchBoxInstances[node] = box;
    }

    private void DestroySwitchBox(RailNode node)
    {
        if (switchBoxInstances.TryGetValue(node, out var box))
        {
            if (box != null) Destroy(box.gameObject);
            switchBoxInstances.Remove(node);
        }
    }

    /// <summary>Returns the switch box for a node, or null.</summary>
    public JunctionSwitchBox GetSwitchBox(RailNode node)
    {
        switchBoxInstances.TryGetValue(node, out var box);
        return box;
    }

    // ─── Segment Splitting ─────────────────────────────────────────────

    /// <summary>
    /// Splits the segment that owns the given spline at the given world position.
    /// Creates a new node at the split point and two new segments replacing the original.
    /// Returns the new junction node, or null if the split failed.
    /// </summary>
    public RailNode SplitSegmentAtPosition(int splineIndex, Vector3 worldPos)
    {
        RailSegment seg = FindSegmentBySpline(splineIndex);
        if (seg == null)
        {
            Debug.LogWarning($"[RailGraph] SplitSegment: no segment for spline {splineIndex}");
            return null;
        }

        var container = network.Container;
        var spline = container.Splines[splineIndex];
        var xform = network.transform;

        // ── Find split knot index ──────────────────────────────────────
        int splitKnotIdx = -1;
        float bestKnotDist = float.MaxValue;
        for (int k = 0; k < spline.Count; k++)
        {
            Vector3 kw = xform.TransformPoint((Vector3)spline[k].Position);
            Vector3 diff = kw - worldPos;
            diff.y = 0f;
            float d = diff.magnitude;
            if (d < bestKnotDist)
            {
                bestKnotDist = d;
                splitKnotIdx = k;
            }
        }

        bool atExistingKnot = bestKnotDist < 0.6f;

        if (!atExistingKnot)
        {
            // Insert a new knot on the spline at this position.
            splitKnotIdx = InsertKnotOnSpline(splineIndex, worldPos);
            if (splitKnotIdx < 0) return null;
        }

        // Don't split at the very first or last knot — those are already nodes.
        if (splitKnotIdx <= 0 || splitKnotIdx >= spline.Count - 1)
        {
            return GetOrCreateNode(worldPos);
        }

        // ── Collect split info ──────────────────────────────────────────
        RailNode origStart = seg.startNode;
        RailNode origEnd = seg.endNode;
        Vector3 origStartExit = origStart.GetExitDirection(seg);
        Vector3 origEndExit = origEnd.GetExitDirection(seg);

        // Get the knot at the split point.
        BezierKnot splitKnot = spline[splitKnotIdx];
        Vector3 splitWorldPos = xform.TransformPoint((Vector3)splitKnot.Position);

        // Exit direction for first half at split: tangent pointing toward start.
        Vector3 splitExitFirst = xform.TransformVector((Vector3)(float3)splitKnot.TangentIn).normalized;
        if (splitExitFirst.sqrMagnitude < 0.01f)
            splitExitFirst = (origStart.worldPosition - splitWorldPos).normalized;

        // Exit direction for second half at split: tangent pointing toward end.
        Vector3 splitExitSecond = xform.TransformVector((Vector3)(float3)splitKnot.TangentOut).normalized;
        if (splitExitSecond.sqrMagnitude < 0.01f)
            splitExitSecond = (origEnd.worldPosition - splitWorldPos).normalized;

        // ── Build knot lists for the two halves ────────────────────────
        var firstKnots = new List<BezierKnot>();
        for (int k = 0; k <= splitKnotIdx; k++)
            firstKnots.Add(spline[k]);

        var secondKnots = new List<BezierKnot>();
        for (int k = splitKnotIdx; k < spline.Count; k++)
            secondKnots.Add(spline[k]);

        // ── Remove old segment (removes spline, reindexes) ────────────
        RemoveSegment(seg);

        // ── Create split node ──────────────────────────────────────────
        RailNode splitNode = new RailNode(splitWorldPos, WorldToTile(splitWorldPos));
        nodes.Add(splitNode);

        // ── Add two new splines ────────────────────────────────────────
        int firstIdx = network.AddSplineFromKnots(firstKnots);
        int secondIdx = network.AddSplineFromKnots(secondKnots);

        // ── Register new segments ──────────────────────────────────────
        RegisterSegment(origStart, splitNode, firstIdx, origStartExit, splitExitFirst);
        RegisterSegment(splitNode, origEnd, secondIdx, splitExitSecond, origEndExit);

        Debug.Log($"[RailGraph] Split spline at {splitWorldPos}: created node with " +
                  $"first half (spline {firstIdx}) and second half (spline {secondIdx})");

        return splitNode;
    }

    /// <summary>
    /// Inserts a new knot on an existing spline at the given world position.
    /// Returns the index of the inserted knot, or -1 on failure.
    /// </summary>
    private int InsertKnotOnSpline(int splineIndex, Vector3 worldPos)
    {
        var container = network.Container;
        var spline = container.Splines[splineIndex];
        var xform = network.transform;

        // Find which span (pair of adjacent knots) the position falls between.
        float bestDist = float.MaxValue;
        int insertAfter = 0;

        for (int k = 0; k < spline.Count - 1; k++)
        {
            Vector3 kStart = xform.TransformPoint((Vector3)spline[k].Position);
            Vector3 kEnd = xform.TransformPoint((Vector3)spline[k + 1].Position);

            Vector3 segDir = kEnd - kStart;
            float segLen = segDir.magnitude;
            if (segLen < 0.01f) continue;
            segDir /= segLen;

            float proj = Vector3.Dot(worldPos - kStart, segDir);
            proj = Mathf.Clamp(proj, 0f, segLen);
            Vector3 closest = kStart + segDir * proj;

            Vector3 diff = closest - worldPos;
            diff.y = 0f;
            float dist = diff.magnitude;

            if (dist < bestDist)
            {
                bestDist = dist;
                insertAfter = k;
            }
        }

        // Compute tangent from adjacent knots.
        Vector3 prevWorld = xform.TransformPoint((Vector3)spline[insertAfter].Position);
        Vector3 nextWorld = xform.TransformPoint((Vector3)spline[insertAfter + 1].Position);

        Vector3 tangentDir = (nextWorld - prevWorld).normalized;
        float distToPrev = Vector3.Distance(worldPos, prevWorld);
        float distToNext = Vector3.Distance(worldPos, nextWorld);
        float tangentLen = Mathf.Min(distToPrev, distToNext) / 3f;

        Vector3 tangentIn = -tangentDir * tangentLen;
        Vector3 tangentOut = tangentDir * tangentLen;

        float3 localPos = (float3)xform.InverseTransformPoint(worldPos);
        float3 localTanIn = (float3)xform.InverseTransformVector(tangentIn);
        float3 localTanOut = (float3)xform.InverseTransformVector(tangentOut);

        var knot = new BezierKnot(localPos, localTanIn, localTanOut, quaternion.identity);

        int insertIdx = insertAfter + 1;
        spline.Insert(insertIdx, knot, TangentMode.Broken);

        // Adjust TangentOut of the previous knot to point toward the new knot.
        var prevKnot = spline[insertAfter];
        prevKnot.TangentOut = (float3)xform.InverseTransformVector(tangentDir * (distToPrev / 3f));
        spline[insertAfter] = prevKnot;

        // Adjust TangentIn of the next knot to point toward the new knot.
        int nextIdx = insertIdx + 1;
        if (nextIdx < spline.Count)
        {
            var nextKnot = spline[nextIdx];
            nextKnot.TangentIn = (float3)xform.InverseTransformVector(-tangentDir * (distToNext / 3f));
            spline[nextIdx] = nextKnot;
        }

        return insertIdx;
    }

    private static Vector2Int WorldToTile(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x + 0.5f),
            Mathf.FloorToInt(worldPos.z + 0.5f));
    }
}

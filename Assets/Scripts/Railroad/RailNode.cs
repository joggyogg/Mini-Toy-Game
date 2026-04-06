using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A point in the rail graph where one or more segments meet.
/// Max 12 connections (4 direction groups × 3 exits each).
/// Not a MonoBehaviour — pure data owned by RailGraph.
///
/// Direction-group gate system:
///   Each junction has 4 direction groups (N/S/E/W-ish, rotated by junctionOrientation).
///   Connections are classified into the nearest group based on their exit angle.
///   Each group independently controls which of its exits is open (gateIndex).
///   A train arriving from group G exits via the opposite group's ((G+2)%4) active gate.
/// </summary>
[Serializable]
public class RailNode
{
    public const int MaxConnections = 12;
    public const int NumDirectionGroups = 4;

    private static int _nextJunctionId = 1;

    public Vector3 worldPosition;
    public Vector2Int tileCoord;
    public int junctionId = -1; // assigned when node first becomes a junction

    [NonSerialized] public readonly List<RailSegment> connections = new();
    [NonSerialized] public readonly List<Vector3> exitDirections = new();

    /// <summary>Which direction group (0-3) each connection belongs to. Parallel to connections.</summary>
    [NonSerialized] public readonly List<int> connectionGroupMap = new();

    /// <summary>Explicit group hint per connection (-1 = auto-classify from exitDirection).</summary>
    [NonSerialized] public readonly List<int> connectionGroupHints = new();

    /// <summary>
    /// World-space position sampled a fixed distance along each exit's spline.
    /// Used to sort exits left-to-right within a direction group.
    /// Parallel to connections.
    /// </summary>
    [NonSerialized] public readonly List<Vector3> connectionSortPoints = new();

    /// <summary>
    /// Angle in degrees of the junction's principal axis (group 0 center).
    /// Groups are at orientation, +90, +180, +270.
    /// </summary>
    public float junctionOrientation;

    /// <summary>
    /// Active gate index per direction group.
    /// gateIndices[g] selects which exit within group g is currently open.
    /// </summary>
    public readonly int[] gateIndices = new int[NumDirectionGroups];

    public RailNode(Vector3 worldPos, Vector2Int tile)
    {
        worldPosition = worldPos;
        tileCoord = tile;
    }

    public int ConnectionCount => connections.Count;
    public bool CanAddConnection => connections.Count < MaxConnections;
    public bool IsJunction => connections.Count >= 3;

    // ─── Legacy compat ──────────────────────────────────────────────────
    // activeSwitchIndex kept as a facade for any remaining references.
    public int activeSwitchIndex
    {
        get => gateIndices[0];
        set => gateIndices[0] = value;
    }

    // ─── Direction Group Helpers ─────────────────────────────────────────

    /// <summary>
    /// Returns the center angle (degrees, XZ plane, 0 = +Z) of direction group g.
    /// Groups use absolute world cardinals: 0=North(0°), 1=East(90°), 2=South(180°), 3=West(270°).
    /// </summary>
    public float GroupCenterAngle(int g)
    {
        return NormalizeAngle(g * 90f);
    }

    /// <summary>
    /// Classifies an exit direction (world XZ) into the nearest direction group (0-3).
    /// </summary>
    public int ClassifyToGroup(Vector3 exitDir)
    {
        float angle = Mathf.Atan2(exitDir.x, exitDir.z) * Mathf.Rad2Deg; // 0 = +Z
        int bestGroup = 0;
        float bestDelta = float.MaxValue;
        for (int g = 0; g < NumDirectionGroups; g++)
        {
            float center = GroupCenterAngle(g);
            float delta = Mathf.Abs(AngleDelta(angle, center));
            if (delta < bestDelta)
            {
                bestDelta = delta;
                bestGroup = g;
            }
        }
        return bestGroup;
    }

    /// <summary>
    /// Returns all connections in the given direction group, ordered spatially
    /// left-to-right by the lateral position of the segment's far endpoint
    /// relative to this node and the group's forward axis.
    /// </summary>
    public List<RailSegment> GetGroupExits(int groupIndex)
    {
        var result = new List<RailSegment>();
        for (int i = 0; i < connections.Count; i++)
        {
            if (connectionGroupMap[i] == groupIndex)
                result.Add(connections[i]);
        }
        // "Right" vector perpendicular to the group's forward in XZ.
        float centerRad = GroupCenterAngle(groupIndex) * Mathf.Deg2Rad;
        Vector3 groupRight = new Vector3(Mathf.Cos(centerRad), 0f, -Mathf.Sin(centerRad));

        result.Sort((a, b) =>
        {
            int idxA = connections.IndexOf(a);
            int idxB = connections.IndexOf(b);
            Vector3 ptA = (idxA >= 0 && idxA < connectionSortPoints.Count)
                ? connectionSortPoints[idxA] : worldPosition;
            Vector3 ptB = (idxB >= 0 && idxB < connectionSortPoints.Count)
                ? connectionSortPoints[idxB] : worldPosition;
            float latA = Vector3.Dot(ptA - worldPosition, groupRight);
            float latB = Vector3.Dot(ptB - worldPosition, groupRight);
            return latA.CompareTo(latB);
        });
        return result;
    }

    /// <summary>
    /// Returns the number of exits in the given group.
    /// </summary>
    public int GetGroupExitCount(int groupIndex)
    {
        int count = 0;
        for (int i = 0; i < connectionGroupMap.Count; i++)
            if (connectionGroupMap[i] == groupIndex)
                count++;
        return count;
    }

    /// <summary>
    /// Given the segment the train arrived on, returns the segment it should
    /// exit via based on the direction-group gate state.
    ///
    /// 1. Find which group the arriving segment belongs to.
    /// 2. Look at the opposite group ((g+2)%4).
    /// 3. Return the exit at the opposite group's active gate index.
    ///
    /// For 2-connection nodes this is simply the other segment (no junction logic).
    /// </summary>
    public RailSegment GetSwitchedExit(RailSegment arriving)
    {
        if (connections.Count <= 1) return null;

        // Simple pass-through for non-junctions.
        if (connections.Count == 2)
        {
            return connections[0] == arriving ? connections[1] : connections[0];
        }

        // Find arriving group.
        int arrIdx = connections.IndexOf(arriving);
        if (arrIdx < 0) return null;
        int arrGroup = connectionGroupMap[arrIdx];

        // Opposite group.
        int exitGroup = (arrGroup + 2) % NumDirectionGroups;

        // Get exits in that group.
        var exits = GetGroupExits(exitGroup);
        if (exits.Count == 0)
        {
            // No exits in opposite group — fall back to any other group with exits.
            for (int g = 0; g < NumDirectionGroups; g++)
            {
                if (g == arrGroup) continue;
                exits = GetGroupExits(g);
                if (exits.Count > 0)
                {
                    exitGroup = g;
                    break;
                }
            }
            if (exits.Count == 0) return null;
        }

        int gate = gateIndices[exitGroup] % exits.Count;
        return exits[gate];
    }

    /// <summary>
    /// Cycles the gate for a specific direction group.
    /// Returns the new gate index.
    /// </summary>
    public int CycleGate(int groupIndex)
    {
        if (groupIndex < 0 || groupIndex >= NumDirectionGroups) return 0;
        int count = GetGroupExitCount(groupIndex);
        if (count < 2) return 0;
        gateIndices[groupIndex] = (gateIndices[groupIndex] + 1) % count;
        return gateIndices[groupIndex];
    }

    /// <summary>
    /// Legacy CycleSwitch — cycles ALL groups that have 2+ exits.
    /// Kept for external compatibility.
    /// </summary>
    public int CycleSwitch()
    {
        for (int g = 0; g < NumDirectionGroups; g++)
            CycleGate(g);
        return gateIndices[0];
    }

    // ─── Connection Management ──────────────────────────────────────────

    public void AddConnection(RailSegment segment, Vector3 exitDir, int groupHint = -1, Vector3 sortPoint = default)
    {
        if (connections.Count >= MaxConnections)
            throw new InvalidOperationException($"RailNode at {worldPosition} already has {MaxConnections} connections.");
        connections.Add(segment);
        exitDirections.Add(exitDir.normalized);
        connectionGroupMap.Add(0); // temporary; reclassified below
        connectionGroupHints.Add(groupHint);
        // If no sort point provided, fall back to a point 3 units along exitDir.
        connectionSortPoints.Add(sortPoint == default ? worldPosition + exitDir.normalized * 3f : sortPoint);
        ReclassifyGroups();
    }

    public void RemoveConnection(RailSegment segment)
    {
        int idx = connections.IndexOf(segment);
        if (idx < 0) return;
        connections.RemoveAt(idx);
        exitDirections.RemoveAt(idx);
        connectionGroupMap.RemoveAt(idx);
        connectionGroupHints.RemoveAt(idx);
        connectionSortPoints.RemoveAt(idx);
        ReclassifyGroups();
    }

    /// <summary>
    /// Returns the exit direction for a specific segment at this node,
    /// or Vector3.zero if not found.
    /// </summary>
    public Vector3 GetExitDirection(RailSegment segment)
    {
        int idx = connections.IndexOf(segment);
        return idx >= 0 ? exitDirections[idx] : Vector3.zero;
    }

    // ─── Orientation & Classification ───────────────────────────────────

    /// <summary>
    /// Reclassifies all connections into direction groups using absolute world cardinals.
    /// junctionOrientation is always 0 (North) — groups are fixed N/E/S/W.
    /// </summary>
    public void ReclassifyGroups()
    {
        // Orientation is always 0 — absolute world cardinals.
        junctionOrientation = 0f;

        if (connections.Count < 3)
        {
            // Not a junction — reset group map silently.
            for (int i = 0; i < connectionGroupMap.Count; i++)
                connectionGroupMap[i] = 0;
            return;
        }

        // Save the currently-active segment for each group so we can
        // restore gate indices after the sort order may have changed.
        RailSegment[] prevActive = new RailSegment[NumDirectionGroups];
        bool hadJunction = connections.Count > 3; // junction existed before this add
        if (hadJunction)
        {
            for (int g = 0; g < NumDirectionGroups; g++)
            {
                var exits = GetGroupExits(g);
                if (exits.Count > 0)
                    prevActive[g] = exits[gateIndices[g] % exits.Count];
            }
        }

        // Classify each connection into the nearest direction group,
        // honouring explicit group hints when provided.
        // Hints from the drawing system reflect which cardinal arm an exit
        // was built from, which is more accurate than raw exit angles for
        // curved exits near group boundaries. Join hints are reclassified
        // from the junction's perspective in FinishSpline before arriving here.
        bool freshJunction = connections.Count == 3 && !hadJunction;
        for (int i = 0; i < connections.Count; i++)
        {
            int hint = i < connectionGroupHints.Count ? connectionGroupHints[i] : -1;
            connectionGroupMap[i] = (hint >= 0 && hint < NumDirectionGroups)
                ? hint
                : ClassifyToGroup(exitDirections[i]);
        }

        // Restore gate indices: find the previously-active segment's new
        // position in its (possibly reordered) group.
        for (int g = 0; g < NumDirectionGroups; g++)
        {
            int count = GetGroupExitCount(g);
            if (count == 0)
            {
                gateIndices[g] = 0;
            }
            else if (hadJunction && prevActive[g] != null)
            {
                var exits = GetGroupExits(g);
                int idx = exits.IndexOf(prevActive[g]);
                gateIndices[g] = idx >= 0 ? idx : 0;
            }
            else
            {
                gateIndices[g] = gateIndices[g] % count;
            }
        }

        // Assign junction ID and log details when a node first becomes a junction.
        if (freshJunction && junctionId < 0)
        {
            junctionId = _nextJunctionId++;
        }
        if (IsJunction)
        {
            LogJunctionState();
        }
    }

    private static readonly string[] GroupColorNames = { "Yellow(N)", "Green(E)", "Blue(S)", "Red(W)" };

    private void LogJunctionState()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"[Junction #{junctionId}] pos={worldPosition} | {connections.Count} exits:");
        for (int i = 0; i < connections.Count; i++)
        {
            int g = connectionGroupMap[i];
            string colorName = (g >= 0 && g < GroupColorNames.Length) ? GroupColorNames[g] : $"Group{g}";
            Vector3 dir = exitDirections[i];
            sb.Append($"\n  exit[{i}] → {colorName} dir=({dir.x:F2}, {dir.z:F2}) spline={connections[i].splineIndex}");
        }
        Debug.Log(sb.ToString());
    }

    // ─── Angle Utilities ────────────────────────────────────────────────

    private float ExitAngle(RailSegment seg)
    {
        int idx = connections.IndexOf(seg);
        if (idx < 0) return 0f;
        return Mathf.Atan2(exitDirections[idx].x, exitDirections[idx].z) * Mathf.Rad2Deg;
    }

    private static float NormalizeAngle(float deg)
    {
        deg %= 360f;
        if (deg < 0f) deg += 360f;
        return deg;
    }

    /// <summary>Signed shortest angular distance from a to b in degrees (-180..180].</summary>
    private static float AngleDelta(float a, float b)
    {
        float d = NormalizeAngle(b - a);
        if (d > 180f) d -= 360f;
        return d;
    }
}

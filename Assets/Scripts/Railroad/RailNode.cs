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

    public Vector3 worldPosition;
    public Vector2Int tileCoord;

    [NonSerialized] public readonly List<RailSegment> connections = new();
    [NonSerialized] public readonly List<Vector3> exitDirections = new();

    /// <summary>Which direction group (0-3) each connection belongs to. Parallel to connections.</summary>
    [NonSerialized] public readonly List<int> connectionGroupMap = new();

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
    /// </summary>
    public float GroupCenterAngle(int g)
    {
        return NormalizeAngle(junctionOrientation + g * 90f);
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
    /// Returns all connections in the given direction group, ordered by clockwise
    /// angle from the group's center.
    /// </summary>
    public List<RailSegment> GetGroupExits(int groupIndex)
    {
        var result = new List<RailSegment>();
        for (int i = 0; i < connections.Count; i++)
        {
            if (connectionGroupMap[i] == groupIndex)
                result.Add(connections[i]);
        }
        // Sort by signed angle from group center: most counter-clockwise (negative) first,
        // most clockwise (positive) last. This matches the left-to-right pip layout in the UI.
        float center = GroupCenterAngle(groupIndex);
        result.Sort((a, b) =>
        {
            float dA = AngleDelta(center, ExitAngle(a)); // signed -180..180
            float dB = AngleDelta(center, ExitAngle(b));
            return dA.CompareTo(dB);
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

    public void AddConnection(RailSegment segment, Vector3 exitDir)
    {
        if (connections.Count >= MaxConnections)
            throw new InvalidOperationException($"RailNode at {worldPosition} already has {MaxConnections} connections.");
        connections.Add(segment);
        exitDirections.Add(exitDir.normalized);
        connectionGroupMap.Add(0); // temporary; reclassified below
        ReclassifyGroups();
    }

    public void RemoveConnection(RailSegment segment)
    {
        int idx = connections.IndexOf(segment);
        if (idx < 0) return;
        connections.RemoveAt(idx);
        exitDirections.RemoveAt(idx);
        connectionGroupMap.RemoveAt(idx);
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
    /// Recomputes junctionOrientation from the connections and reclassifies
    /// all connections into direction groups.
    /// </summary>
    public void ReclassifyGroups()
    {
        if (connections.Count < 3)
        {
            // Not a junction — reset group map silently.
            for (int i = 0; i < connectionGroupMap.Count; i++)
                connectionGroupMap[i] = 0;
            return;
        }

        // Only compute orientation the first time the junction forms (3 connections).
        // For 4+ connections we keep the existing orientation so adding tracks
        // never scrambles the already-established group layout.
        if (connections.Count == 3)
            ComputeOrientation();

        // Classify each connection into the nearest direction group.
        for (int i = 0; i < connections.Count; i++)
        {
            connectionGroupMap[i] = ClassifyToGroup(exitDirections[i]);
        }

        // Clamp gate indices to valid range.
        for (int g = 0; g < NumDirectionGroups; g++)
        {
            int count = GetGroupExitCount(g);
            if (count == 0)
                gateIndices[g] = 0;
            else
                gateIndices[g] = gateIndices[g] % count;
        }
    }

    /// <summary>
    /// Auto-detects the junction orientation from the most-opposite pair of exits.
    /// </summary>
    private void ComputeOrientation()
    {
        if (exitDirections.Count < 2) return;

        float bestOpposite = -1f;
        int bestA = 0, bestB = 1;

        for (int a = 0; a < exitDirections.Count; a++)
        {
            for (int b = a + 1; b < exitDirections.Count; b++)
            {
                // Dot product: perfectly opposite = -1, so we look for the most negative.
                float dot = Vector3.Dot(exitDirections[a], exitDirections[b]);
                float opposition = -dot; // higher = more opposite
                if (opposition > bestOpposite)
                {
                    bestOpposite = opposition;
                    bestA = a;
                    bestB = b;
                }
            }
        }

        // Bisector angle: average the two directions (flip one to same hemisphere first).
        Vector3 dirA = exitDirections[bestA];
        Vector3 dirB = -exitDirections[bestB]; // flip so both point "same way"
        Vector3 bisector = (dirA + dirB).normalized;

        if (bisector.sqrMagnitude < 0.001f)
            bisector = exitDirections[bestA]; // fallback

        junctionOrientation = Mathf.Atan2(bisector.x, bisector.z) * Mathf.Rad2Deg;
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

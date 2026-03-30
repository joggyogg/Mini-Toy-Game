using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to the Room Terrain (or any floor object near the model terrain).
/// Stores a ring of waypoints around the model terrain's perimeter.
/// Use the "Generate Waypoints" button in the inspector to rebuild them.
/// </summary>
public class HumanPerimeterWaypointData : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform modelTerrainParent;

    [Header("Generation Settings")]
    [SerializeField] private int waypointsPerEdge = 5;
    [Tooltip("How far inward from the Room Terrain edge to place waypoints (so the human isn't right at the cliff).")]
    [SerializeField] private float edgeInset = 5f;
    [SerializeField] private float raycastHeight = 200f;

    [Header("Generated Data (read-only)")]
    [SerializeField] private List<HumanWaypoint> waypoints = new List<HumanWaypoint>();

    [Serializable]
    public struct HumanWaypoint
    {
        public Vector3 worldPosition;
        public Quaternion facingRotation;
        public bool isCorner;
        /// <summary>Corner substep 0 facing (previous-edge direction). Only used when isCorner is true.</summary>
        public Quaternion cornerFacingA;
        /// <summary>Corner substep 2 facing (next-edge direction). Only used when isCorner is true.</summary>
        public Quaternion cornerFacingB;
    }

    public IReadOnlyList<HumanWaypoint> Waypoints => waypoints;
    public int Count => waypoints.Count;

    public HumanWaypoint GetWaypoint(int index)
    {
        return waypoints[index % waypoints.Count];
    }

    public int GetNearestIndex(Vector3 worldPos)
    {
        if (waypoints.Count == 0) return 0;

        int best = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < waypoints.Count; i++)
        {
            float dist = Vector3.Distance(worldPos, waypoints[i].worldPosition);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }
        return best;
    }

    /// <summary>
    /// Rebuilds the waypoint ring around the edges of the Room Terrain (the terrain this component is on).
    /// The human faces inward toward the Model Terrain Parent.
    /// Call from the editor "Generate Waypoints" button.
    /// </summary>
    public void GenerateWaypoints()
    {
        waypoints.Clear();

        if (modelTerrainParent == null)
        {
            Debug.LogError("[HumanPerimeterWaypointData] modelTerrainParent is not assigned!", this);
            return;
        }

        // Get the Room Terrain bounds from the Terrain component on THIS game object
        Terrain roomTerrain = GetComponent<Terrain>();
        if (roomTerrain == null)
        {
            Debug.LogError("[HumanPerimeterWaypointData] No Terrain component found on this GameObject! Attach this to the Room Terrain.", this);
            return;
        }

        Vector3 terrainPos = roomTerrain.transform.position;
        Vector3 terrainSize = roomTerrain.terrainData.size;

        // Room Terrain XZ rectangle, inset from the edges
        float minX = terrainPos.x + edgeInset;
        float maxX = terrainPos.x + terrainSize.x - edgeInset;
        float minZ = terrainPos.z + edgeInset;
        float maxZ = terrainPos.z + terrainSize.z - edgeInset;

        // Four corners of the room edge (clockwise from top-left: +Z is "up")
        Vector3 tl = new Vector3(minX, 0f, maxZ);
        Vector3 tr = new Vector3(maxX, 0f, maxZ);
        Vector3 br = new Vector3(maxX, 0f, minZ);
        Vector3 bl = new Vector3(minX, 0f, minZ);

        Vector3[] corners = { tl, tr, br, bl };

        // Walk perimeter clockwise: for each edge, emit corner then evenly spaced edge waypoints
        for (int edge = 0; edge < 4; edge++)
        {
            Vector3 start = corners[edge];
            Vector3 end = corners[(edge + 1) % 4];

            // Corner waypoint
            AddWaypoint(start, true);

            // Edge waypoints (evenly spaced between corners, exclusive of corners themselves)
            for (int i = 1; i <= waypointsPerEdge; i++)
            {
                float t = (float)i / (waypointsPerEdge + 1);
                Vector3 pos = Vector3.Lerp(start, end, t);
                AddWaypoint(pos, false);
            }
        }
    }

    private void AddWaypoint(Vector3 xzPosition, bool isCorner)
    {
        // Raycast downward to find floor height
        float floorY = xzPosition.y;
        Vector3 rayOrigin = new Vector3(xzPosition.x, raycastHeight, xzPosition.z);
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastHeight * 2f))
        {
            floorY = hit.point.y;
        }

        Vector3 worldPos = new Vector3(xzPosition.x, floorY, xzPosition.z);

        waypoints.Add(new HumanWaypoint
        {
            worldPosition = worldPos,
            facingRotation = Quaternion.identity,
            isCorner = isCorner,
            cornerFacingA = Quaternion.identity,
            cornerFacingB = Quaternion.identity
        });
    }

    private Bounds ComputeModelTerrainFootprint()
    {
        Renderer[] renderers = modelTerrainParent.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            // Fallback: use the transform position with a small default size
            return new Bounds(modelTerrainParent.position, Vector3.one * 10f);
        }

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            combined.Encapsulate(renderers[i].bounds);
        }
        return combined;
    }

    // Expose for the editor gizmo drawing
    public Bounds GetModelTerrainFootprint()
    {
        if (modelTerrainParent == null) return new Bounds(transform.position, Vector3.one);
        return ComputeModelTerrainFootprint();
    }

    /// <summary>Returns the Room Terrain's XZ bounds (inset by edgeInset), or a fallback if no Terrain component.</summary>
    public bool TryGetRoomPerimeterRect(out Vector3 min, out Vector3 max)
    {
        Terrain roomTerrain = GetComponent<Terrain>();
        if (roomTerrain == null)
        {
            min = max = transform.position;
            return false;
        }
        Vector3 pos = roomTerrain.transform.position;
        Vector3 size = roomTerrain.terrainData.size;
        min = new Vector3(pos.x + edgeInset, pos.y, pos.z + edgeInset);
        max = new Vector3(pos.x + size.x - edgeInset, pos.y, pos.z + size.z - edgeInset);
        return true;
    }

    public float EdgeInset => edgeInset;

    /// <summary>Sets the facing rotation of a single waypoint by index. Editor use only.</summary>
    public void SetWaypointFacing(int index, Quaternion rotation)
    {
        if (index < 0 || index >= waypoints.Count) return;
        var wp = waypoints[index];
        wp.facingRotation = rotation;
        waypoints[index] = wp;
    }

    /// <summary>Sets corner substep 0 (previous-edge) facing. Editor use only.</summary>
    public void SetCornerFacingA(int index, Quaternion rotation)
    {
        if (index < 0 || index >= waypoints.Count) return;
        var wp = waypoints[index];
        wp.cornerFacingA = rotation;
        waypoints[index] = wp;
    }

    /// <summary>Sets corner substep 2 (next-edge) facing. Editor use only.</summary>
    public void SetCornerFacingB(int index, Quaternion rotation)
    {
        if (index < 0 || index >= waypoints.Count) return;
        var wp = waypoints[index];
        wp.cornerFacingB = rotation;
        waypoints[index] = wp;
    }

    /// <summary>Returns the Room Terrain world-space XZ bounds (no inset), or fallback.</summary>
    public bool TryGetRoomTerrainFullRect(out Vector3 min, out Vector3 max)
    {
        Terrain roomTerrain = GetComponent<Terrain>();
        if (roomTerrain == null)
        {
            min = max = transform.position;
            return false;
        }
        Vector3 pos = roomTerrain.transform.position;
        Vector3 size = roomTerrain.terrainData.size;
        min = new Vector3(pos.x, pos.y, pos.z);
        max = new Vector3(pos.x + size.x, pos.y, pos.z + size.z);
        return true;
    }

    // ── Preset Save / Load ────────────────────────────────────────────────────────

    /// <summary>Copies current settings and waypoints into a preset asset.</summary>
    public void SaveToPreset(HumanWaypointPreset preset)
    {
        if (preset == null) return;
        preset.waypointsPerEdge = waypointsPerEdge;
        preset.edgeInset = edgeInset;
        preset.waypoints.Clear();
        foreach (var wp in waypoints)
        {
            preset.waypoints.Add(new HumanWaypointPreset.SavedWaypoint
            {
                worldPosition = wp.worldPosition,
                facingRotation = wp.facingRotation,
                isCorner = wp.isCorner,
                cornerFacingA = wp.cornerFacingA,
                cornerFacingB = wp.cornerFacingB
            });
        }
    }

    /// <summary>Restores settings and waypoints from a preset asset.</summary>
    public void LoadFromPreset(HumanWaypointPreset preset)
    {
        if (preset == null) return;
        waypointsPerEdge = preset.waypointsPerEdge;
        edgeInset = preset.edgeInset;
        waypoints.Clear();
        foreach (var saved in preset.waypoints)
        {
            waypoints.Add(new HumanWaypoint
            {
                worldPosition = saved.worldPosition,
                facingRotation = saved.facingRotation,
                isCorner = saved.isCorner,
                cornerFacingA = saved.cornerFacingA,
                cornerFacingB = saved.cornerFacingB
            });
        }
    }
}

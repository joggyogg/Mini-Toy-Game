using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores a saved configuration of human perimeter waypoints as a reusable asset.
/// Create via Assets → Create → Mini Toy Game → Human Waypoint Preset,
/// or use the "Save to Preset" button in the HumanPerimeterWaypointData inspector.
/// </summary>
[CreateAssetMenu(fileName = "HumanWaypointPreset", menuName = "Mini Toy Game/Human Waypoint Preset")]
public class HumanWaypointPreset : ScriptableObject
{
    [Header("Generation Settings")]
    public int waypointsPerEdge = 5;
    public float edgeInset = 5f;

    [Header("Waypoints")]
    public List<SavedWaypoint> waypoints = new List<SavedWaypoint>();

    [Serializable]
    public struct SavedWaypoint
    {
        public Vector3 worldPosition;
        public Quaternion facingRotation;
        public bool isCorner;
        public Quaternion cornerFacingA;
        public Quaternion cornerFacingB;
    }
}

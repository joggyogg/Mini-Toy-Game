using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Attach to the invisible "Human Body" cylinder.
/// Moves between waypoints defined by HumanPerimeterWaypointData.
/// A/D step one waypoint at a time (discrete, wraps around).
/// Corner waypoints have three sub-steps: prev-edge facing, diagonal
/// (original corner angle), then next-edge facing. Each D press advances
/// one sub-step before actually moving to the next waypoint.
/// The transform is smoothly interpolated between waypoints.
/// </summary>
public class HumanBodyController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HumanPerimeterWaypointData waypointData;

    [Header("Height")]
    [Tooltip("Offset above the waypoint floor position to place the cylinder pivot. For a Y-scale-30 cylinder (60 units tall, pivot at center), set this to 30 so the base sits on the floor.")]
    [SerializeField] private float pivotHeightOffset = 30f;

    [Header("Smoothing")]
    [SerializeField] private float positionSmoothTime = 0.2f;
    [SerializeField] private float rotationSharpness = 8f;

    private int currentIndex;
    private int cornerSubStep;        // 0 = prev-edge facing, 1 = diagonal, 2 = next-edge facing
    private Vector3 positionVelocity;
    private bool isActive;

    /// <summary>
    /// World position at the very top of the cylinder (eye/head level).
    /// For a Y-scale-30 cylinder: pivot + 30 = top.
    /// </summary>
    public Vector3 EyeWorldPosition => transform.position + Vector3.up * pivotHeightOffset;

    /// <summary>Current smoothed facing rotation.</summary>
    public Quaternion CurrentFacingRotation => transform.rotation;

    /// <summary>Whether the human body is currently active and accepting input.</summary>
    public bool IsActive => isActive;

    /// <summary>Whether the current waypoint is a corner waypoint.</summary>
    public bool IsAtCorner => isActive && waypointData != null && waypointData.Count > 0
        && waypointData.GetWaypoint(currentIndex).isCorner;

    /// <summary>Whether the body is at a corner AND on the diagonal (middle) substep.</summary>
    public bool IsAtCornerDiagonal => IsAtCorner && cornerSubStep == 1;

    /// <summary>
    /// Snap to the nearest waypoint to the given world position and begin accepting input.
    /// </summary>
    public void Activate(Vector3 referenceWorldPos)
    {
        if (waypointData == null || waypointData.Count == 0)
        {
            Debug.LogError("[HumanBodyController] waypointData is null or has no waypoints!", this);
            return;
        }

        currentIndex = waypointData.GetNearestIndex(referenceWorldPos);
        cornerSubStep = 0;
        isActive = true;
        positionVelocity = Vector3.zero;

        // Snap immediately on first activation (no lerp)
        var wp = waypointData.GetWaypoint(currentIndex);
        transform.position = wp.worldPosition + Vector3.up * pivotHeightOffset;
        transform.rotation = GetTargetRotation();
    }

    /// <summary>Stop accepting input.</summary>
    public void Deactivate()
    {
        isActive = false;
    }

    private void Update()
    {
        if (!isActive) return;
        if (waypointData == null || waypointData.Count == 0) return;
        if (Keyboard.current == null) return;

        bool atCorner = waypointData.GetWaypoint(currentIndex).isCorner;

        // D = forward / clockwise
        if (Keyboard.current.dKey.wasPressedThisFrame)
        {
            if (atCorner && cornerSubStep < 2)
            {
                cornerSubStep++;
            }
            else
            {
                currentIndex = (currentIndex + 1) % waypointData.Count;
                cornerSubStep = 0;
            }
        }

        // A = backward / counter-clockwise
        if (Keyboard.current.aKey.wasPressedThisFrame)
        {
            if (atCorner && cornerSubStep > 0)
            {
                cornerSubStep--;
            }
            else
            {
                int prevIndex = (currentIndex - 1 + waypointData.Count) % waypointData.Count;
                currentIndex = prevIndex;
                // Land on the far substep when backing into a corner
                cornerSubStep = waypointData.GetWaypoint(currentIndex).isCorner ? 2 : 0;
            }
        }
    }

    private void LateUpdate()
    {
        if (!isActive) return;
        if (waypointData == null || waypointData.Count == 0) return;

        var wp = waypointData.GetWaypoint(currentIndex);
        Vector3 targetPos = wp.worldPosition + Vector3.up * pivotHeightOffset;

        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref positionVelocity, positionSmoothTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, GetTargetRotation(), rotationSharpness * Time.deltaTime);
    }

    /// <summary>
    /// Returns the rotation this body should face, accounting for corner sub-steps.
    /// Substep 0 = cornerFacingA (previous-edge direction).
    /// Substep 1 = facingRotation (the corner's own diagonal facing).
    /// Substep 2 = cornerFacingB (next-edge direction).
    /// </summary>
    private Quaternion GetTargetRotation()
    {
        var wp = waypointData.GetWaypoint(currentIndex);
        if (!wp.isCorner)
            return wp.facingRotation;

        return cornerSubStep switch
        {
            0 => wp.cornerFacingA,
            2 => wp.cornerFacingB,
            _ => wp.facingRotation   // substep 1 = diagonal
        };
    }
}

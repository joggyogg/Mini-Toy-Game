using UnityEngine;

/// <summary>
/// MonoBehaviour placed on a world-space object beside a junction.
/// Holds a reference to the RailNode and renders a radial gate UI.
/// Players click quadrants to cycle gates for each direction group.
/// </summary>
public class JunctionSwitchBox : MonoBehaviour
{
    private RailNode node;
    private JunctionGateUI gateUI;

    /// <summary>The junction node this switch box controls.</summary>
    public RailNode Node => node;

    public void Initialize(RailNode railNode)
    {
        node = railNode;

        // Find the gate UI that was already added during spawn.
        gateUI = GetComponentInChildren<JunctionGateUI>();
        if (gateUI != null)
            gateUI.Initialize(node);
    }

    public void Refresh(RailNode railNode)
    {
        node = railNode;
        if (gateUI != null)
            gateUI.Initialize(node);
    }

    /// <summary>
    /// Cycles the gate for the given direction group on the underlying node.
    /// Returns the new gate index.
    /// </summary>
    public int CycleGate(int groupIndex)
    {
        if (node == null) return 0;
        int result = node.CycleGate(groupIndex);
        if (gateUI != null) gateUI.SetDirty();
        return result;
    }

    /// <summary>
    /// Given a click direction (world XZ relative to junction center),
    /// figures out which quadrant/group was clicked and cycles that gate.
    /// Returns the group index that was cycled, or -1 if nothing happened.
    /// </summary>
    public int CycleGateFromDirection(Vector3 clickDir)
    {
        if (node == null) return -1;
        int group = node.ClassifyToGroup(clickDir);
        int count = node.GetGroupExitCount(group);
        if (count < 2) return -1;
        CycleGate(group);
        return group;
    }
}

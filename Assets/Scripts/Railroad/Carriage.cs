using UnityEngine;

/// <summary>
/// A single carriage in a train consist. Linked from the Locomotive via
/// firstCarriage, and chained forward via nextCarriage.
///
/// Each frame Tick() receives the leader vehicle's rear-coupler arc-length.
/// This carriage subtracts its frontCouplerOffset to find its own front-bogie
/// distance, positions itself on the spline, then passes its own rear-coupler
/// distance to the next carriage in the chain.
///
/// Coupler gap between vehicles is fully implicit:
///   leaderRearCouplerOffset + this.frontCouplerOffset
/// </summary>
public class Carriage : MonoBehaviour
{
    public enum ForwardAxis { PosZ, NegZ, PosX, NegX }

    [Header("Bogies")]
    [Tooltip("Arc-length distance between front and rear bogies.")]
    [SerializeField] private float bogieSpacing = 8f;
    [Tooltip("Height above the spline surface.")]
    [SerializeField] private float heightOffset = 0.05f;
    [Tooltip("Which local axis of the model points forward.")]
    [SerializeField] private ForwardAxis forwardAxis = ForwardAxis.PosZ;

    [Header("Couplers")]
    [Tooltip("Arc-length distance from the front bogie to the front coupler hook.")]
    [SerializeField] private float frontCouplerOffset = 0.3f;
    [Tooltip("Arc-length distance from the rear bogie to the rear coupler hook.")]
    [SerializeField] private float rearCouplerOffset = 0.3f;

    [Header("Chain")]
    [Tooltip("Next carriage in the consist. Leave empty to end the chain.")]
    [SerializeField] private Carriage nextCarriage;

    // Exposed so Locomotive.OnValidate() can walk the linked list.
    public Carriage NextCarriage => nextCarriage;

    // ─── Runtime State ───────────────────────────────────────────────────

    private Locomotive loco;
    private float distance;   // front bogie arc-length along spline

    private static readonly Quaternion[] AxisCorrections =
    {
        Quaternion.identity,           // +Z
        Quaternion.Euler(0, 180, 0),   // -Z
        Quaternion.Euler(0, -90, 0),   // +X
        Quaternion.Euler(0, 90, 0),    // -X
    };

    // Arc-length of this carriage's rear coupler, passed to nextCarriage.
    private float RearCouplerDistance => distance - bogieSpacing - rearCouplerOffset;

    // ─── Called by Locomotive / previous Carriage ─────────────────────────

    public void Initialize(Locomotive locomotive, float leaderRearCouplerDist)
    {
        loco     = locomotive;
        distance = leaderRearCouplerDist - frontCouplerOffset;
        UpdatePosition();
        nextCarriage?.Initialize(loco, RearCouplerDistance);
    }

    public void Tick(float leaderRearCouplerDist)
    {
        distance = leaderRearCouplerDist - frontCouplerOffset;
        UpdatePosition();
        nextCarriage?.Tick(RearCouplerDistance);
    }

    // ─── Positioning ─────────────────────────────────────────────────────

    private void UpdatePosition()
    {
        Vector3 frontPos = loco.EvaluateWorldPos(distance);
        Vector3 rearPos  = loco.EvaluateWorldPos(distance - bogieSpacing);

        Vector3 fwd = frontPos - rearPos;
        if (fwd.sqrMagnitude < 0.0001f) return;

        transform.position = (frontPos + rearPos) * 0.5f;
        transform.rotation = Quaternion.LookRotation(fwd, Vector3.up)
                           * AxisCorrections[(int)forwardAxis];
    }

    // ─── Gizmos ──────────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        Vector3 frontPos, rearPos;

        if (loco != null)
        {
            frontPos = loco.EvaluateWorldPos(distance);
            rearPos  = loco.EvaluateWorldPos(distance - bogieSpacing);
        }
        else
        {
            // Prefab editing: estimate from local transform.
            Vector3 fwd    = ModelForward();
            Vector3 centre = transform.position + Vector3.up * heightOffset;
            frontPos = centre + fwd * (bogieSpacing * 0.5f);
            rearPos  = centre - fwd * (bogieSpacing * 0.5f);
        }

        // Bogies
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(frontPos, 0.1f);
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(rearPos, 0.1f);
        Gizmos.color = Color.white;
        Gizmos.DrawLine(frontPos, rearPos);

        Vector3 dir = (frontPos - rearPos).sqrMagnitude > 0.0001f
            ? (frontPos - rearPos).normalized
            : Vector3.forward;

        // Front coupler diamond
        Vector3 frontCoupler = frontPos + dir * frontCouplerOffset;
        Gizmos.color = Color.cyan;
        RailGizmos.DrawDiamond(frontCoupler, 0.09f);
        Gizmos.DrawLine(frontPos, frontCoupler);

        // Rear coupler diamond
        Vector3 rearCoupler = rearPos - dir * rearCouplerOffset;
        Gizmos.color = Color.cyan;
        RailGizmos.DrawDiamond(rearCoupler, 0.09f);
        Gizmos.DrawLine(rearPos, rearCoupler);
    }

    private Vector3 ModelForward()
    {
        return forwardAxis switch
        {
            ForwardAxis.PosZ => transform.forward,
            ForwardAxis.NegZ => -transform.forward,
            ForwardAxis.PosX => transform.right,
            ForwardAxis.NegX => -transform.right,
            _                => transform.forward,
        };
    }
}

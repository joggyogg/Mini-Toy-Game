using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// Drives a locomotive along a rail spline and tows a linked chain of Carriages.
///
/// Movement uses a 256-sample distance-to-t LUT for truly uniform arc-length speed.
/// Carriages are linked via firstCarriage -> Carriage.nextCarriage -> ...
/// Each frame the rear coupler distance is passed down the chain so every car
/// positions itself relative to the vehicle ahead.
///
/// Inspector chain preview: OnValidate() walks the linked list and writes into
/// chainPreview so the full consist is visible in the Locomotive inspector.
/// </summary>
public class Locomotive : MonoBehaviour
{
    public enum ForwardAxis { PosZ, NegZ, PosX, NegX }

    [Header("Movement")]
    [SerializeField] private float speed = 3f;

    [Header("Bogies")]
    [Tooltip("Arc-length distance between front and rear bogies.")]
    [SerializeField] private float bogieSpacing = 16f;
    [Tooltip("Height above the spline surface.")]
    [SerializeField] private float heightOffset = 0.05f;
    [Tooltip("Which local axis of the model points forward.")]
    [SerializeField] private ForwardAxis forwardAxis = ForwardAxis.PosZ;

    [Tooltip("Vertical offset of the model body above the bogie midpoint. Use this to align the mesh visually on the track.")]
    [SerializeField] private float modelYOffset = 0f;

    [Header("Coupler")]
    [Tooltip("Arc-length distance from the rear bogie to the rear coupler hook.")]
    [SerializeField] private float rearCouplerOffset = 0.3f;

    [Header("Consist")]
    [Tooltip("First carriage in the chain. Each carriage links to the next.")]
    [SerializeField] private Carriage firstCarriage;

    [Header("Chain Preview (read-only)")]
    [SerializeField] private List<Carriage> chainPreview = new();

    // ─── Runtime State ───────────────────────────────────────────────────

    private SplineContainer splineContainer;
    private int splineIndex;
    private float splineLength;
    private float distance;   // front bogie arc-length along spline
    private bool isMoving;

    internal const int LutSamples = 256;
    internal float[] LutDistances;
    internal float[] LutT;

    private static readonly Quaternion[] AxisCorrections =
    {
        Quaternion.identity,           // +Z
        Quaternion.Euler(0, 180, 0),   // -Z
        Quaternion.Euler(0, -90, 0),   // +X
        Quaternion.Euler(0, 90, 0),    // -X
    };

    // Arc-length of the rear coupler hook, passed as the leader distance to firstCarriage.
    private float RearCouplerDistance => distance - bogieSpacing - rearCouplerOffset;

    // ─── Public API ──────────────────────────────────────────────────────

    public void PlaceOnSpline(RailNetworkAuthoring network, int splineIdx)
    {
        splineContainer = network.Container;
        splineIndex     = splineIdx;

        BuildDistanceLUT();

        distance = bogieSpacing;
        isMoving = true;
        UpdatePosition();

        firstCarriage?.Initialize(this, RearCouplerDistance);
    }

    // ─── LUT ─────────────────────────────────────────────────────────────

    private void BuildDistanceLUT()
    {
        LutDistances    = new float[LutSamples + 1];
        LutT            = new float[LutSamples + 1];
        LutDistances[0] = 0f;
        LutT[0]         = 0f;

        Vector3 prev = (Vector3)splineContainer.EvaluatePosition(splineIndex, 0f);
        for (int i = 1; i <= LutSamples; i++)
        {
            float t   = (float)i / LutSamples;
            Vector3 p = (Vector3)splineContainer.EvaluatePosition(splineIndex, t);
            LutDistances[i] = LutDistances[i - 1] + Vector3.Distance(prev, p);
            LutT[i]         = t;
            prev            = p;
        }
        splineLength = LutDistances[LutSamples];
    }

    // Binary-search the LUT to convert an arc-length distance to a spline t value.
    internal float DistanceToT(float dist)
    {
        dist = Mathf.Clamp(dist, 0f, splineLength);
        int lo = 0, hi = LutSamples;
        while (lo < hi - 1)
        {
            int mid = (lo + hi) >> 1;
            if (LutDistances[mid] < dist) lo = mid;
            else hi = mid;
        }
        float segLen = LutDistances[hi] - LutDistances[lo];
        float frac   = segLen > 0f ? (dist - LutDistances[lo]) / segLen : 0f;
        return Mathf.Lerp(LutT[lo], LutT[hi], frac);
    }

    // Evaluates a world-space position at the given arc-length, with height offset applied.
    internal Vector3 EvaluateWorldPos(float dist)
    {
        Vector3 p = (Vector3)splineContainer.EvaluatePosition(splineIndex, DistanceToT(dist));
        p.y += heightOffset;
        return p;
    }

    // ─── Update ──────────────────────────────────────────────────────────

    private void Update()
    {
        if (!isMoving || splineContainer == null) return;

        distance += speed * Time.deltaTime;
        if (distance >= splineLength) { distance = splineLength; isMoving = false; }

        UpdatePosition();
        firstCarriage?.Tick(RearCouplerDistance);
    }

    private void UpdatePosition()
    {
        Vector3 frontPos = EvaluateWorldPos(distance);
        Vector3 rearPos  = EvaluateWorldPos(distance - bogieSpacing);

        Vector3 fwd = frontPos - rearPos;
        if (fwd.sqrMagnitude < 0.0001f) return;

        transform.position = (frontPos + rearPos) * 0.5f + Vector3.up * modelYOffset;
        transform.rotation = Quaternion.LookRotation(fwd, Vector3.up)
                           * AxisCorrections[(int)forwardAxis];
    }

    // ─── Inspector chain preview ─────────────────────────────────────────

    private void OnValidate()
    {
        chainPreview.Clear();
        Carriage c = firstCarriage;
        int guard  = 64; // prevent freeze from accidental circular references
        while (c != null && guard-- > 0)
        {
            chainPreview.Add(c);
            c = c.NextCarriage;
        }
    }

    // ─── Gizmos ──────────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        Vector3 frontPos, rearPos;

        if (splineContainer != null && LutDistances != null)
        {
            frontPos = EvaluateWorldPos(distance);
            rearPos  = EvaluateWorldPos(distance - bogieSpacing);
        }
        else
        {
            // Prefab editing: bogies sit at model position minus modelYOffset.
            Vector3 fwd    = ModelForward();
            Vector3 centre = transform.position + Vector3.up * (heightOffset - modelYOffset);
            frontPos = centre + fwd * (bogieSpacing * 0.5f);
            rearPos  = centre - fwd * (bogieSpacing * 0.5f);
        }

        // Bogies
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(frontPos, 0.12f);
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(rearPos, 0.12f);
        Gizmos.color = Color.white;
        Gizmos.DrawLine(frontPos, rearPos);

        // Rear coupler diamond
        Vector3 dir     = (frontPos - rearPos).normalized;
        Vector3 coupler = rearPos - dir * rearCouplerOffset;
        Gizmos.color = Color.cyan;
        RailGizmos.DrawDiamond(coupler, 0.1f);
        Gizmos.DrawLine(rearPos, coupler);
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

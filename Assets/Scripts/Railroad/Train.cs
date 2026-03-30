using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

/// <summary>
/// Drives a train along a rail spline with truly uniform speed.
///
/// At spawn, a distance-to-t lookup table is built by sampling the spline.
/// Each frame, front/rear bogie distances are converted to parametric t via
/// binary search, then evaluated to world positions via SplineContainer.
/// </summary>
public class Train : MonoBehaviour
{
    public enum ForwardAxis { PosZ, NegZ, PosX, NegX }

    [Header("Movement")]
    [SerializeField] private float speed = 3f;

    [Header("Configuration")]
    [Tooltip("Arc-length distance between front and rear bogies.")]
    [SerializeField] private float bogieSpacing = 0.5f;
    [Tooltip("Height above the spline surface.")]
    [SerializeField] private float heightOffset = 0.05f;
    [Tooltip("Which local axis of the model points forward.")]
    [SerializeField] private ForwardAxis forwardAxis = ForwardAxis.PosZ;

    private SplineContainer splineContainer;
    private int splineIndex;
    private float splineLength;
    private float distance; // front bogie world-space distance along spline
    private bool isMoving;

    // Distance-to-t lookup table (world-space arc lengths).
    private const int LutSamples = 256;
    private float[] lutDistances;
    private float[] lutT;

    private static readonly Quaternion[] AxisCorrections =
    {
        Quaternion.identity,           // +Z
        Quaternion.Euler(0, 180, 0),   // -Z
        Quaternion.Euler(0, -90, 0),   // +X
        Quaternion.Euler(0, 90, 0),    // -X
    };

    public void PlaceOnSpline(RailNetworkAuthoring network, int splineIdx)
    {
        splineContainer = network.Container;
        splineIndex = splineIdx;

        BuildDistanceLUT();

        distance = bogieSpacing;
        isMoving = true;
        UpdatePosition();
    }

    private void BuildDistanceLUT()
    {
        lutDistances = new float[LutSamples + 1];
        lutT = new float[LutSamples + 1];

        lutDistances[0] = 0f;
        lutT[0] = 0f;

        Vector3 prevPos = (Vector3)splineContainer.EvaluatePosition(splineIndex, 0f);

        for (int i = 1; i <= LutSamples; i++)
        {
            float t = (float)i / LutSamples;
            Vector3 pos = (Vector3)splineContainer.EvaluatePosition(splineIndex, t);
            lutDistances[i] = lutDistances[i - 1] + Vector3.Distance(prevPos, pos);
            lutT[i] = t;
            prevPos = pos;
        }

        splineLength = lutDistances[LutSamples];
    }

    private float DistanceToT(float dist)
    {
        dist = Mathf.Clamp(dist, 0f, splineLength);

        // Binary search for the segment containing dist.
        int lo = 0, hi = LutSamples;
        while (lo < hi - 1)
        {
            int mid = (lo + hi) >> 1;
            if (lutDistances[mid] < dist) lo = mid;
            else hi = mid;
        }

        float segLen = lutDistances[hi] - lutDistances[lo];
        float frac = segLen > 0f ? (dist - lutDistances[lo]) / segLen : 0f;
        return Mathf.Lerp(lutT[lo], lutT[hi], frac);
    }

    private void Update()
    {
        if (!isMoving || splineContainer == null) return;

        distance += speed * Time.deltaTime;

        if (distance >= splineLength)
        {
            distance = splineLength;
            isMoving = false;
        }

        UpdatePosition();
    }

    private void UpdatePosition()
    {
        float frontT = DistanceToT(distance);
        float rearT = DistanceToT(distance - bogieSpacing);

        Vector3 frontPos = (Vector3)splineContainer.EvaluatePosition(splineIndex, frontT);
        Vector3 rearPos = (Vector3)splineContainer.EvaluatePosition(splineIndex, rearT);
        frontPos.y += heightOffset;
        rearPos.y += heightOffset;

        Vector3 fwd = frontPos - rearPos;
        if (fwd.sqrMagnitude < 0.0001f) return;

        transform.position = (frontPos + rearPos) * 0.5f;
        transform.rotation = Quaternion.LookRotation(fwd, Vector3.up)
                           * AxisCorrections[(int)forwardAxis];
    }

    private void OnDrawGizmosSelected()
    {
        if (splineContainer == null || lutDistances == null) return;

        float frontT = DistanceToT(distance);
        float rearT = DistanceToT(distance - bogieSpacing);

        Vector3 frontPos = (Vector3)splineContainer.EvaluatePosition(splineIndex, frontT);
        Vector3 rearPos = (Vector3)splineContainer.EvaluatePosition(splineIndex, rearT);
        frontPos.y += heightOffset;
        rearPos.y += heightOffset;

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(frontPos, 0.05f);
        Gizmos.DrawSphere(rearPos, 0.05f);
        Gizmos.DrawLine(frontPos, rearPos);
    }
}

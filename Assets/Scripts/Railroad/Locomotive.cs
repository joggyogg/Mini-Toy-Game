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

    private static readonly List<Locomotive> allTrains = new();

    /// <summary>All active locomotives. Used for collision/queuing checks.</summary>
    public static IReadOnlyList<Locomotive> AllTrains => allTrains;

    private SplineContainer splineContainer;
    private RailGraph railGraph;
    private float distance;   // front bogie cumulative arc-length along full chain
    private bool isMoving;
    private bool isForwardGear = true;
    private bool isRotated;    // true after RotateTrain() flips the consist 180°

    private const int LutSamples = 256;

    // Chain of spline segments the train has entered, in travel order.
    private struct SegmentLink
    {
        public RailSegment segment;     // null if no graph was provided
        public int splineIndex;
        public float chainStart;        // global distance where this link begins
        public float length;            // arc-length of the spline
        public bool reversed;           // true = traveling endNode→startNode (t: 1→0)
        public float[] lutDistances;
        public float[] lutT;
    }

    private readonly List<SegmentLink> chain = new();
    private int headLink;               // index in chain of the front-most segment

    private static readonly Quaternion[] AxisCorrections =
    {
        Quaternion.identity,           // +Z
        Quaternion.Euler(0, 180, 0),   // -Z
        Quaternion.Euler(0, -90, 0),   // +X
        Quaternion.Euler(0, 90, 0),    // -X
    };

    // Arc-length of the rear coupler hook, passed as the leader distance to firstCarriage.
    private float RearCouplerDistance => distance - bogieSpacing - rearCouplerOffset;

    /// <summary>Spline index of the segment the locomotive's front bogie is currently on.</summary>
    public int CurrentSplineIndex => chain.Count > 0 ? chain[headLink].splineIndex : -1;

    /// <summary>Whether the current head link is reversed.</summary>
    public bool CurrentReversed => chain.Count > 0 && chain[headLink].reversed;

    /// <summary>Front bogie arc-length distance along the current segment chain.</summary>
    public float FrontDistance => distance;

    /// <summary>Tail end arc-length (rear of last carriage, or rear coupler of loco if no carriages).</summary>
    public float TailDistance => distance - bogieSpacing - TotalConsistLength();

    /// <summary>Exposed bogie spacing for spawn overlap estimation.</summary>
    public float BogieSpacingPublic => bogieSpacing;

    /// <summary>
    /// Estimates the total consist length from the prefab chain (works before instantiation).
    /// </summary>
    public float EstimateConsistLength()
    {
        float len = 0f;
        float leaderRear = rearCouplerOffset;
        Carriage c = firstCarriage;
        int guard = 64;
        while (c != null && guard-- > 0)
        {
            len += leaderRear + c.FrontCouplerOffset + c.BogieSpacing;
            leaderRear = c.RearCouplerOffset;
            c = c.NextCarriage;
        }
        return len;
    }

    private float TotalConsistLength()
    {
        float len = rearCouplerOffset;
        Carriage c = firstCarriage;
        int guard = 64;
        while (c != null && guard-- > 0)
        {
            len += c.FrontCouplerOffset + c.BogieSpacing + c.RearCouplerOffset;
            c = c.NextCarriage;
        }
        return len;
    }

    private void OnEnable() => allTrains.Add(this);
    private void OnDisable() => allTrains.Remove(this);

    /// <summary>
    /// Called when a spline is removed and indices shift down.
    /// Updates all stored spline indices in the chain.
    /// </summary>
    public static void NotifySplineRemoved(int removedIndex)
    {
        for (int i = 0; i < allTrains.Count; i++)
        {
            allTrains[i].ReindexAfterRemoval(removedIndex);
        }
    }

    private void ReindexAfterRemoval(int removedIndex)
    {
        for (int i = 0; i < chain.Count; i++)
        {
            var link = chain[i];
            if (link.splineIndex == removedIndex)
            {
                // This train's segment was removed — stop it.
                isMoving = false;
                Debug.LogWarning($"[Locomotive] Spline {removedIndex} was removed while train was on it.");
            }
            else if (link.splineIndex > removedIndex)
            {
                link.splineIndex--;
                chain[i] = link;
            }
        }
    }

    // ─── Public API ──────────────────────────────────────────────────────

    public void PlaceOnSpline(RailNetworkAuthoring network, int splineIdx, RailGraph graph = null, float startDistance = -1f)
    {
        splineContainer = network.Container;
        railGraph       = graph;

        // Build the first link in the segment chain.
        chain.Clear();
        headLink = 0;

        RailSegment seg = null;
        if (graph != null)
        {
            for (int i = 0; i < graph.Segments.Count; i++)
            {
                if (graph.Segments[i].splineIndex == splineIdx)
                { seg = graph.Segments[i]; break; }
            }
        }

        chain.Add(BuildLink(seg, splineIdx, 0f, reversed: false));

        // Instantiate the entire carriage chain upfront before positioning.
        InstantiateConsist();

        // Position: use explicit start distance or default to front of consist.
        float consistLength = ComputeConsistLength();
        float minDist = bogieSpacing + consistLength;
        if (startDistance >= 0f)
            distance = Mathf.Max(startDistance, minDist);
        else
            distance = minDist;

        // Clamp to spline length.
        float segLen = chain[0].length;
        if (distance > segLen)
            distance = segLen;

        isMoving = true;
        UpdatePosition();

        firstCarriage?.Initialize(this, RearCouplerDistance);
    }

    /// <summary>Whether the train is currently moving.</summary>
    public bool IsMoving
    {
        get => isMoving;
        set => isMoving = value;
    }

    /// <summary>True = wheels drive in the loco's facing direction. False = reverse gear.</summary>
    public bool IsForwardGear
    {
        get => isForwardGear;
        set => isForwardGear = value;
    }

    /// <summary>
    /// Rotates the entire consist 180° on the track. If the tail would clip
    /// past the end of the rail, the whole train is pushed forward to fit.
    /// </summary>
    public void RotateTrain()
    {
        if (chain.Count == 0) return;

        isRotated = !isRotated;

        // Flip the reversed flag on the current head link so the locomotive
        // now faces the opposite direction on the same segment.
        var head = chain[headLink];

        // Compute the distance from the segment start that the front bogie is at.
        float localDist = distance - head.chainStart;

        // After flipping, the front bogie distance from the NEW start is (length - localDist).
        float flippedLocal = head.length - localDist;

        // Rebuild the chain with a single link, reversed relative to the old direction.
        bool newReversed = !head.reversed;
        chain.Clear();
        headLink = 0;
        chain.Add(BuildLink(head.segment, head.splineIndex, 0f, newReversed));

        distance = flippedLocal;

        // Ensure the entire consist fits. Push forward if tail would clip.
        float consistLength = ComputeConsistLength();
        float minNeeded = bogieSpacing + consistLength;
        if (distance < minNeeded)
            distance = minNeeded;

        // Clamp to segment length.
        float segLen = chain[0].length;
        if (distance > segLen)
            distance = segLen;

        UpdatePosition();
        firstCarriage?.Tick(RearCouplerDistance);
    }

    /// <summary>
    /// Total arc-length behind the locomotive's rear bogie needed to fit all carriages.
    /// </summary>
    private float ComputeConsistLength()
    {
        float length = 0f;
        float leaderRearOffset = rearCouplerOffset;
        Carriage c = firstCarriage;
        int guard = 64;
        while (c != null && guard-- > 0)
        {
            length += leaderRearOffset + c.FrontCouplerOffset + c.BogieSpacing;
            leaderRearOffset = c.RearCouplerOffset;
            c = c.NextCarriage;
        }
        return length;
    }

    // ─── Consist Instantiation ────────────────────────────────────────────

    /// <summary>
    /// Walk the prefab chain, instantiate every carriage, and re-wire the
    /// linked list so Initialize() operates purely on scene objects.
    /// </summary>
    private void InstantiateConsist()
    {
        if (firstCarriage == null) return;

        // 1. Collect prefab references.
        var prefabs = new List<Carriage>();
        Carriage cur = firstCarriage;
        int guard = 64;
        while (cur != null && guard-- > 0)
        {
            prefabs.Add(cur);
            cur = cur.NextCarriage;
        }

        // 2. Instantiate all carriages (nothing is positioned yet).
        var instances = new List<Carriage>(prefabs.Count);
        for (int i = 0; i < prefabs.Count; i++)
        {
            bool isPrefab = !prefabs[i].gameObject.scene.isLoaded;
            instances.Add(isPrefab ? Instantiate(prefabs[i]) : prefabs[i]);
        }

        // 3. Re-wire the linked list on the instantiated objects.
        for (int i = 0; i < instances.Count - 1; i++)
            instances[i].SetNextCarriage(instances[i + 1]);
        instances[^1].SetNextCarriage(null);

        firstCarriage = instances[0];
    }

    // ─── LUT / Segment Chain ─────────────────────────────────────────────

    private SegmentLink BuildLink(RailSegment seg, int splineIdx, float chainStart, bool reversed)
    {
        var lutD = new float[LutSamples + 1];
        var lutT = new float[LutSamples + 1];
        lutD[0] = 0f;
        lutT[0] = 0f;

        Vector3 prev = (Vector3)splineContainer.EvaluatePosition(splineIdx, 0f);
        for (int i = 1; i <= LutSamples; i++)
        {
            float t   = (float)i / LutSamples;
            Vector3 p = (Vector3)splineContainer.EvaluatePosition(splineIdx, t);
            lutD[i] = lutD[i - 1] + Vector3.Distance(prev, p);
            lutT[i] = t;
            prev    = p;
        }

        return new SegmentLink
        {
            segment      = seg,
            splineIndex  = splineIdx,
            chainStart   = chainStart,
            length       = lutD[LutSamples],
            reversed     = reversed,
            lutDistances = lutD,
            lutT         = lutT
        };
    }

    private static float LinkDistanceToT(in SegmentLink link, float localDist)
    {
        localDist = Mathf.Clamp(localDist, 0f, link.length);
        int lo = 0, hi = LutSamples;
        while (lo < hi - 1)
        {
            int mid = (lo + hi) >> 1;
            if (link.lutDistances[mid] < localDist) lo = mid;
            else hi = mid;
        }
        float segLen = link.lutDistances[hi] - link.lutDistances[lo];
        float frac   = segLen > 0f ? (localDist - link.lutDistances[lo]) / segLen : 0f;
        return Mathf.Lerp(link.lutT[lo], link.lutT[hi], frac);
    }

    /// <summary>
    /// Evaluates a world-space position at the given cumulative arc-length
    /// distance along the full segment chain, with height offset applied.
    /// </summary>
    internal Vector3 EvaluateWorldPos(float dist)
    {
        if (chain.Count == 0) return transform.position;

        // Clamp to chain bounds.
        float minDist = chain[0].chainStart;
        float maxDist = chain[^1].chainStart + chain[^1].length;
        dist = Mathf.Clamp(dist, minDist, maxDist);

        // Find the link this distance falls into.
        int idx = 0;
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            if (dist >= chain[i].chainStart) { idx = i; break; }
        }

        var link = chain[idx];
        float localDist = dist - link.chainStart;
        // For reversed links, flip the lookup so t maps correctly.
        float lutDist = link.reversed ? (link.length - localDist) : localDist;
        float t = LinkDistanceToT(link, lutDist);

        Vector3 p = (Vector3)splineContainer.EvaluatePosition(link.splineIndex, t);
        p.y += heightOffset;
        return p;
    }

    // ─── Update ──────────────────────────────────────────────────────────

    private float stopCheckTimer;
    private const float StopCheckInterval = 1f;

    private void Update()
    {
        if (splineContainer == null) return;

        // If stopped at end of track, check once per second for newly built segments.
        if (!isMoving)
        {
            stopCheckTimer += Time.deltaTime;
            if (stopCheckTimer < StopCheckInterval) return;
            stopCheckTimer = 0f;

            if (TryTransition())
                isMoving = true;
            else
                return;
        }

        distance += speed * Time.deltaTime * (isForwardGear ? 1f : -1f);

        // Queue behind other trains on the same spline (1 tile gap).
        const float queueGap = 1f;
        int mySpline = CurrentSplineIndex;
        bool myReversed = CurrentReversed;

        for (int i = 0; i < allTrains.Count; i++)
        {
            Locomotive other = allTrains[i];
            if (other == this || other.CurrentSplineIndex != mySpline) continue;
            if (other.CurrentReversed != myReversed) continue;

            // "Ahead" means larger distance when forward gear, smaller when reverse.
            if (isForwardGear)
            {
                // Other's tail is ahead of our front — stop 1 tile behind it.
                float stopAt = other.TailDistance - queueGap;
                if (other.TailDistance > TailDistance && distance > stopAt)
                {
                    distance = stopAt;
                    isMoving = false;
                }
            }
            else
            {
                // Reverse gear: other's front is behind (lower distance) than ours.
                float myTail = TailDistance;
                float stopAt = other.FrontDistance + queueGap + (distance - myTail);
                if (other.FrontDistance < FrontDistance && distance < stopAt)
                {
                    distance = stopAt;
                    isMoving = false;
                }
            }
        }

        // Check if we've passed the end of the current head segment.
        var head = chain[headLink];
        float headEnd = head.chainStart + head.length;
        float headStart = head.chainStart;

        if (isForwardGear && distance >= headEnd)
        {
            if (!TryTransition())
            {
                distance = headEnd;
                isMoving = false;
            }
        }
        else if (!isForwardGear && distance <= headStart + bogieSpacing)
        {
            // In reverse gear, stop at the beginning of the chain.
            distance = headStart + bogieSpacing;
            isMoving = false;
        }

        UpdatePosition();
        firstCarriage?.Tick(RearCouplerDistance);
    }

    /// <summary>
    /// Attempts to move the train onto the next connected segment at the current node.
    /// </summary>
    private bool TryTransition()
    {
        if (railGraph == null || headLink >= chain.Count) return false;

        var head = chain[headLink];
        if (head.segment == null) return false;

        // Determine which node we arrived at.
        RailNode arrivalNode = head.reversed ? head.segment.startNode : head.segment.endNode;

        // Use the node's switch state to pick the exit segment.
        RailSegment nextSeg = arrivalNode.GetSwitchedExit(head.segment);

        if (nextSeg == null) return false;

        // If we entered at the new segment's startNode → forward (t: 0→1).
        // If we entered at its endNode → reversed (t: 1→0).
        bool reversed = (arrivalNode == nextSeg.endNode);

        float newChainStart = head.chainStart + head.length;
        chain.Add(BuildLink(nextSeg, nextSeg.splineIndex, newChainStart, reversed));
        headLink = chain.Count - 1;

        Debug.Log($"[Locomotive] Transitioned to spline {nextSeg.splineIndex} " +
                  $"(reversed={reversed}) at node {arrivalNode.worldPosition}");
        return true;
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

        if (splineContainer != null && chain.Count > 0)
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

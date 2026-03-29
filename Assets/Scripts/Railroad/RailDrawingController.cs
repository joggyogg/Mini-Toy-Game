using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Result of a snap calculation: snapped endpoint, exit tangent direction,
/// Bezier tangent handles, and sampled arc points for preview / grading.
/// </summary>
public struct SnapResult
{
    public Vector3 position;
    public Vector3 exitDirection;
    public Vector3 tangentOut;     // world tangent-out for the START knot
    public Vector3 tangentIn;      // world tangent-in  for the END   knot
    public Vector3[] arcPoints;    // sampled path points (Y = lastKnot.Y)
    public bool isValid;
}

/// <summary>
/// Handles mouse input in Conductor mode to place radius-constrained spline knots.
///
/// Knot 1:  Free placement on terrain.
/// Knot 2:  Snaps to nearest cardinal direction (N/S/E/W), 1-10 tiles.
/// Knot 3+: Cursor lateral offset selects turning radius (straight / 8 / 5 / 3 tiles).
///          Arc preview shows the constrained path in real-time.
///
/// Right-click / Escape: Finish the current spline.
/// </summary>
public class RailDrawingController : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField] private Camera sourceCamera;
    [SerializeField] private LayerMask terrainLayer = ~0;

    [Header("References")]
    [SerializeField] private RailNetworkAuthoring network;
    [SerializeField] private RailLineRenderer railLineRenderer;
    [SerializeField] private RailTerrainGrader terrainGrader;

    [Header("Train Spawning")]
    [Tooltip("Prefab with a Train component. Press T in Conductor mode to spawn.")]
    [SerializeField] private GameObject trainPrefab;

    [Header("Preview")]
    [SerializeField] private float previewLineWidth = 0.08f;
    [SerializeField] private Color previewColor = Color.cyan;
    [SerializeField] private int previewArcSamples = 20;

    private LineRenderer previewLine;
    private bool isDrawing;
    private int knotCount;
    private Vector3 lastDirection;

    private void Reset()
    {
        AutoWireSelf();
    }

    [ContextMenu("Auto-Wire Self")]
    private void AutoWireSelf()
    {
        if (network == null) network = GetComponent<RailNetworkAuthoring>();
        if (railLineRenderer == null) railLineRenderer = GetComponent<RailLineRenderer>();
        if (terrainGrader == null) terrainGrader = GetComponent<RailTerrainGrader>();
    }

    private void Awake()
    {
        var previewObj = new GameObject("Rail Preview Line");
        previewObj.transform.SetParent(transform);
        previewLine = previewObj.AddComponent<LineRenderer>();
        previewLine.useWorldSpace = true;
        previewLine.startWidth = previewLineWidth;
        previewLine.endWidth = previewLineWidth;
        previewLine.material = new Material(Shader.Find("Sprites/Default"));
        previewLine.startColor = previewColor;
        previewLine.endColor = previewColor;
        previewLine.positionCount = 0;
    }

    private void OnEnable()
    {
        isDrawing = false;
        knotCount = 0;
        lastDirection = Vector3.zero;
        if (previewLine != null) previewLine.positionCount = 0;
    }

    private void OnDisable()
    {
        if (isDrawing && network != null)
        {
            network.FinishCurrentSpline();
            isDrawing = false;
        }
        knotCount = 0;
        lastDirection = Vector3.zero;
        if (previewLine != null) previewLine.positionCount = 0;
    }

    private void Update()
    {
        if (Mouse.current == null) return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            if (previewLine != null) previewLine.positionCount = 0;
            return;
        }

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = sourceCamera.ScreenPointToRay(mousePos);

        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, terrainLayer))
        {
            if (previewLine != null) previewLine.positionCount = 0;
            return;
        }

        UpdatePreview(hit.point);

        if (Mouse.current.leftButton.wasPressedThisFrame)
            PlaceKnot(hit.point);

        if (Mouse.current.rightButton.wasPressedThisFrame
            || (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame))
            FinishSpline();

        // T key: spawn train on last finished spline.
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            SpawnTrain();
    }

    // ─── Train Spawning ──────────────────────────────────────────────────

    private void SpawnTrain()
    {
        if (trainPrefab == null || network == null) return;

        int idx = network.GetLastFinishedSplineIndex();
        if (idx < 0) return;

        GameObject instance = Instantiate(trainPrefab);
        Train train = instance.GetComponent<Train>();
        if (train != null)
            train.PlaceOnSpline(network, idx);
    }

    // ─── Knot Placement ─────────────────────────────────────────────────

    private void PlaceKnot(Vector3 cursorPos)
    {
        if (network == null) return;

        knotCount++;

        if (knotCount == 1)
            PlaceFirstKnot(cursorPos);
        else if (knotCount == 2)
            PlaceCardinalKnot(cursorPos);
        else
            PlaceConstrainedKnot(cursorPos);
    }

    private void PlaceFirstKnot(Vector3 terrainHit)
    {
        Vector3 worldPos = terrainHit;
        if (terrainGrader != null)
            worldPos.y = terrainGrader.GetBedWorldHeight(terrainHit);

        network.AddKnotExplicit(worldPos, Vector3.zero, Vector3.zero);
        isDrawing = true;

        if (terrainGrader != null)
            terrainGrader.GradeAroundPoint(terrainHit);

        if (railLineRenderer != null)
            railLineRenderer.RebuildFromSplines(network);
    }

    private void PlaceCardinalKnot(Vector3 cursorPos)
    {
        Vector3 prevKnot = network.GetLastKnotWorld().Value;
        SnapResult snap = SnapSecondKnot(prevKnot, cursorPos);

        if (!snap.isValid) { knotCount--; return; }

        // snap.position.y is already at bed height (set by SnapSecondKnot).
        Vector3 worldPos = snap.position;

        network.SetLastKnotTangentOut(snap.tangentOut);
        network.AddKnotExplicit(worldPos, snap.tangentIn, Vector3.zero);
        lastDirection = snap.exitDirection;

        if (terrainGrader != null && snap.arcPoints != null)
        {
            float baseY = snap.position.y - terrainGrader.BedRaiseWorldHeight;
            for (int i = 0; i < snap.arcPoints.Length; i++)
            {
                Vector3 gp = snap.arcPoints[i];
                gp.y = baseY;
                terrainGrader.GradeAroundPoint(gp);
            }
        }

        if (railLineRenderer != null)
            railLineRenderer.RebuildFromSplines(network);
    }

    private void PlaceConstrainedKnot(Vector3 cursorPos)
    {
        Vector3 prevKnot = network.GetLastKnotWorld().Value;
        SnapResult snap = SnapToRadius(prevKnot, lastDirection, cursorPos);

        if (!snap.isValid) { knotCount--; return; }

        // snap.position.y is already at bed height (set by SnapToRadius).
        Vector3 worldPos = snap.position;

        network.SetLastKnotTangentOut(snap.tangentOut);
        network.AddKnotExplicit(worldPos, snap.tangentIn, Vector3.zero);
        lastDirection = snap.exitDirection;

        // Grade along arc sample points.
        if (terrainGrader != null && snap.arcPoints != null)
        {
            float baseY = snap.position.y - terrainGrader.BedRaiseWorldHeight;
            for (int i = 0; i < snap.arcPoints.Length; i++)
            {
                Vector3 gp = snap.arcPoints[i];
                gp.y = baseY;
                terrainGrader.GradeAroundPoint(gp);
            }
        }

        if (railLineRenderer != null)
            railLineRenderer.RebuildFromSplines(network);
    }

    // ─── Snapping ────────────────────────────────────────────────────────

    private SnapResult SnapSecondKnot(Vector3 firstKnot, Vector3 cursorPos)
    {
        Vector3 offset = cursorPos - firstKnot;
        offset.y = 0f;

        if (offset.sqrMagnitude < 0.01f)
            return new SnapResult { isValid = false };

        Vector3 dir = offset.normalized;
        float bestDot = float.NegativeInfinity;
        Vector3 bestCardinal = Vector3.forward;
        foreach (var card in RailConstants.Cardinals)
        {
            float d = Vector3.Dot(dir, card);
            if (d > bestDot) { bestDot = d; bestCardinal = card; }
        }

        float dist = Mathf.Clamp(
            Vector3.Dot(offset, bestCardinal),
            RailConstants.MinSegmentLength,
            RailConstants.MaxSegmentLength);

        Vector3 snappedPos = firstKnot + bestCardinal * dist;
        snappedPos.y = firstKnot.y;

        float tangentLen = dist / 3f;
        Vector3 tangent = bestCardinal * tangentLen;

        // Sample ~1 point per tile so grading covers the full segment.
        int samples = Mathf.Max(2, Mathf.CeilToInt(dist) + 1);
        Vector3[] pts = new Vector3[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / (samples - 1);
            pts[i] = Vector3.Lerp(firstKnot, snappedPos, t);
        }

        return new SnapResult
        {
            position = snappedPos,
            exitDirection = bestCardinal,
            tangentOut = tangent,
            tangentIn = -tangent,
            arcPoints = pts,
            isValid = true
        };
    }

    private SnapResult SnapToRadius(Vector3 lastKnot, Vector3 forward, Vector3 cursorPos)
    {
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 offset = cursorPos - lastKnot;
        offset.y = 0f;

        float forwardDist = Vector3.Dot(offset, forward);
        float lateralDist = Vector3.Dot(offset, right);

        if (forwardDist < RailConstants.MinSegmentLength)
            return new SnapResult { isValid = false };

        float angle = Mathf.Atan2(Mathf.Abs(lateralDist), Mathf.Abs(forwardDist)) * Mathf.Rad2Deg;

        // ── Straight ──
        if (angle < RailConstants.StraightThresholdDeg)
        {
            float dist = Mathf.Clamp(forwardDist,
                RailConstants.MinSegmentLength, RailConstants.MaxSegmentLength);
            Vector3 pos = lastKnot + forward * dist;
            pos.y = lastKnot.y;
            float tLen = dist / 3f;
            Vector3 tan = forward * tLen;

            // Sample ~1 point per tile so grading covers the full segment.
            int straightSamples = Mathf.Max(2, Mathf.CeilToInt(dist) + 1);
            Vector3[] pts = new Vector3[straightSamples];
            for (int i = 0; i < straightSamples; i++)
            {
                float t = (float)i / (straightSamples - 1);
                pts[i] = Vector3.Lerp(lastKnot, pos, t);
            }

            return new SnapResult
            {
                position = pos,
                exitDirection = forward,
                tangentOut = tan,
                tangentIn = -tan,
                arcPoints = pts,
                isValid = true
            };
        }

        // ── Curved ──
        float radius;
        if (angle < RailConstants.GentleThresholdDeg)
            radius = RailConstants.TurnRadii[1]; // 8
        else if (angle < RailConstants.TightThresholdDeg)
            radius = RailConstants.TurnRadii[2]; // 5
        else
            radius = RailConstants.TurnRadii[3]; // 3

        float sign = lateralDist >= 0f ? 1f : -1f;

        // All curves are 90° quarter-circles; radius determines arc size.
        float theta = Mathf.PI * 0.5f;

        // Arc geometry.
        Vector3 center = lastKnot + right * sign * radius;
        Vector3 radiusVec = lastKnot - center;
        Quaternion rotation = Quaternion.AngleAxis(sign * theta * Mathf.Rad2Deg, Vector3.up);

        Vector3 endpoint = center + rotation * radiusVec;
        endpoint.y = lastKnot.y;

        Vector3 exitForward = (rotation * forward).normalized;

        // Bezier tangent handle length for circular-arc approximation.
        float d = (4f / 3f) * Mathf.Tan(theta / 4f) * radius;

        // Sample arc for preview and grading.
        int samples = Mathf.Max(2, previewArcSamples);
        Vector3[] points = new Vector3[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / (samples - 1);
            Quaternion rot = Quaternion.AngleAxis(sign * theta * t * Mathf.Rad2Deg, Vector3.up);
            points[i] = center + rot * radiusVec;
            points[i].y = lastKnot.y;
        }

        return new SnapResult
        {
            position = endpoint,
            exitDirection = exitForward,
            tangentOut = forward * d,
            tangentIn = -exitForward * d,
            arcPoints = points,
            isValid = true
        };
    }

    // ─── Preview ─────────────────────────────────────────────────────────

    private void UpdatePreview(Vector3 cursorWorldPos)
    {
        if (previewLine == null) return;

        if (!isDrawing || network == null)
        {
            previewLine.positionCount = 0;
            return;
        }

        Vector3? lastKnot = network.GetLastKnotWorld();
        if (!lastKnot.HasValue)
        {
            previewLine.positionCount = 0;
            return;
        }

        SnapResult snap;
        if (knotCount == 1)
            snap = SnapSecondKnot(lastKnot.Value, cursorWorldPos);
        else
            snap = SnapToRadius(lastKnot.Value, lastDirection, cursorWorldPos);

        if (!snap.isValid || snap.arcPoints == null || snap.arcPoints.Length < 2)
        {
            previewLine.positionCount = 0;
            return;
        }

        previewLine.positionCount = snap.arcPoints.Length;
        for (int i = 0; i < snap.arcPoints.Length; i++)
        {
            Vector3 p = snap.arcPoints[i];
            p.y += 0.05f;
            previewLine.SetPosition(i, p);
        }
    }

    // ─── Finish ──────────────────────────────────────────────────────────

    private void FinishSpline()
    {
        if (network == null) return;

        network.FinishCurrentSpline();
        isDrawing = false;
        knotCount = 0;
        lastDirection = Vector3.zero;

        if (previewLine != null) previewLine.positionCount = 0;

        if (railLineRenderer != null)
            railLineRenderer.RebuildFromSplines(network);
    }

}

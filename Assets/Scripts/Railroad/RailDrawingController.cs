using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Splines;
using UnityEngine.UI;

/// <summary>
/// Result of a snap calculation: snapped endpoint, exit tangent direction,
/// Bezier tangent handles, and sampled arc points for preview / grading.
/// </summary>
public struct CompletionKnot
{
    public Vector3 position;
    public Vector3 tangentIn;
    public Vector3 tangentOut;
    public Vector3[] gradePoints;   // arc sample points for terrain grading
}

public struct SnapResult
{
    public Vector3 position;
    public Vector3 exitDirection;
    public Vector3 tangentOut;     // world tangent-out for the START knot
    public Vector3 tangentIn;      // world tangent-in  for the END   knot
    public Vector3[] arcPoints;    // sampled path points (Y = lastKnot.Y)
    public bool isValid;
    public bool isJoin;            // true if this candidate connects to an existing node
    public RailNode joinNode;      // the existing node we'd connect to (null if not a join)
    public int joinSplineIndex;    // spline index for mid-spline join (-1 = N/A)
    public bool isMidSplineJoin;   // true = need to split existing segment to create junction
    public int cardinalGroup;      // 0=N, 1=E, 2=S, 3=W — which cardinal arm built this candidate
    public bool isParallelReturn;  // true if this curve continues the arc at the same radius
    public bool isStraightenReturn; // true if this candidate reverts the curve back to straight
    public bool isJunctionMirror;   // true if this candidate mirrors an existing junction exit
    public CompletionKnot[] completionKnots; // non-null = multi-segment auto-completion
}

/// <summary>
/// Handles mouse input in Conductor mode to place spline knots.
///
/// Knot 1:  Free placement on terrain (snapped to tile center).
/// Knot 2:  Snaps to nearest cardinal direction (N/S/E/W), 1-10 tiles.
/// Knot 3+: Candidate-based placement. All possible next positions (1 straight +
///          3 radii × 4 angles × 2 sides = 25 candidates) are shown as blue dots.
///          The dot closest to the cursor turns green with a track preview.
///          Left-click commits the highlighted candidate.
///
/// Right-click / Escape: Finish the current spline.
/// </summary>
public class RailDrawingController : MonoBehaviour
{
    public enum ConductorTool { Draw, Delete, Select, Switch, Place }

    [Header("Tool")]
    [SerializeField] private ConductorTool activeTool = ConductorTool.Draw;
    public ConductorTool ActiveTool
    {
        get => activeTool;
        set
        {
            if (isDrawing && value != ConductorTool.Draw)
                FinishSpline();
            activeTool = value;
            UpdateToolButtonVisuals();
        }
    }

    [Header("Tool Buttons (optional)")]
    [SerializeField] private Button drawButton;
    [SerializeField] private Button deleteButton;
    [SerializeField] private Button selectButton;
    [SerializeField] private Button switchButton;

    [Header("Train Catalog")]
    [SerializeField] private TrainCatalog trainCatalog;
    [SerializeField] private TrainCatalogButton trainButtonPrefab;
    [SerializeField] private Transform trainButtonContainer;
    [SerializeField] private Button toggleTrainUIButton;

    [Header("Raycast")]
    [SerializeField] private Camera sourceCamera;
    [SerializeField] private LayerMask terrainLayer = ~0;

    [Header("References")]
    [SerializeField] private RailNetworkAuthoring network;
    [SerializeField] private RailGraph railGraph;
    [SerializeField] private RailLineRenderer railLineRenderer;
    [SerializeField] private RailTerrainGrader terrainGrader;

    [Header("Curve Constraints")]
    [SerializeField, Tooltip("Minimum turning radius in tiles. Curves tighter than this are rejected.")]
    private float minTurnRadius = 20f;

    [Header("Preview")]
    [SerializeField] private float previewLineWidth = 0.08f;
    [SerializeField] private Color previewColor = Color.green;
    [SerializeField] private int previewArcSamples = 20;

    [Header("Candidate Markers")]
    [SerializeField] private Color candidateColor = new Color(0.2f, 0.4f, 1f, 0.9f);
    [SerializeField] private Color selectedColor  = new Color(0.1f, 1f, 0.2f, 0.9f);
    [SerializeField] private Color joinColor      = new Color(0.6f, 0.2f, 1f, 0.9f);
    [SerializeField] private float markerRadius = 0.35f;

    private LineRenderer previewLine;
    private bool isDrawing;
    private int knotCount;
    private Vector3 lastDirection;
    private Vector3 previousKnotPos; // position of the knot before the current last knot

    // Track start/end positions and directions for graph registration.
    private Vector3 drawingStartPos;
    private Vector3 drawingStartExitDir;
    private Vector3 drawingEndExitDir;
    private int drawingStartGroupHint = -1;
    private int drawingEndGroupHint = -1;

    // When branching from a mid-curve knot, this holds the second tangent
    // continuation direction so candidates are generated for both sides.
    private Vector3 midKnotAltDirection;

    // ─── Candidate System ────────────────────────────────────────────────

    private const int MaxCandidates = 500; // straights + all reachable curve tile centers
    private readonly List<SnapResult> candidates = new();
    private int selectedCandidate = -1;
    private readonly List<GameObject> markerPool = new();
    private Material candidateMat;
    private Material selectedMat;
    private Material joinMat;
    private Material parallelReturnMat;
    private Mesh starMesh;
    private Mesh sphereMesh;
    private Mesh cylinderMesh;
    private Material deleteMat;
    private Material deleteSelectedMat;
    private Material selectMat;
    private Material selectSelectedMat;
    private Material switchMat;
    private Material switchActiveMat;

    // Cardinal direction-group materials for candidate dots.
    private Material northMat;  // group 0 – Yellow
    private Material eastMat;   // group 1 – Green
    private Material southMat;  // group 2 – Blue
    private Material westMat;   // group 3 – Orange/Red

    // ─── Delete Mode ─────────────────────────────────────────────────────

    private struct DeleteKnotRef
    {
        public int splineIndex;
        public int knotIndex;
        public Vector3 worldPos;
    }
    private readonly List<DeleteKnotRef> deleteKnotRefs = new();
    private int selectedDeleteKnot = -1;

    // ─── Select Mode ────────────────────────────────────────────────────

    private struct SelectEndpointRef
    {
        public int splineIndex;
        public int knotIndex;
        public int knotCount;       // total knots in the spline
        public Vector3 worldPos;
        public Vector3 exitDir;     // direction the track heads away from this knot
        public Vector3 exitDir2;    // second tangent direction (mid-knot only; zero otherwise)
        public bool isMidKnot;      // true when knot has two valid exit directions
        public bool isMidSplineSample; // true = not at a knot, needs SplitSegmentAtPosition
    }
    private readonly List<SelectEndpointRef> selectRefs = new();
    private int selectedSelectEndpoint = -1;

    // ─── Switch Mode ───────────────────────────────────────────────────

    private readonly List<RailNode> switchJunctions = new();
    private int selectedSwitchJunction = -1;

    // Path lines showing the active route through each junction.
    private readonly List<LineRenderer> switchPathLines = new();
    private Material switchPathMat;
    [Header("Switch Path Preview")]
    [SerializeField] private float switchPathWidth = 0.5f;
    [SerializeField] private float switchPathLength = 5f;
    private const float SwitchPathYOffset = 0.15f;
    private const int SwitchPathSamples = 30;

    // ─── Join Arm Lines ─────────────────────────────────────────────────

    private readonly List<LineRenderer> joinArmLines = new();
    private Material joinArmMat;
    private const float JoinArmLineWidth = 0.12f;
    private const float JoinArmYOffset = 0.3f;

    // ─── Place Mode ─────────────────────────────────────────────────────

    private TrainDefinition selectedTrainDef;
    private struct PlaceKnotRef
    {
        public int splineIndex;
        public int knotIndex;
        public Vector3 worldPos;
    }
    private readonly List<PlaceKnotRef> placeKnotRefs = new();
    private int selectedPlaceKnot = -1;
    private Material placeMat;
    private Material placeSelectedMat;
    private readonly List<TrainCatalogButton> trainCatalogButtons = new();
    private readonly List<Locomotive> spawnedTrains = new();
    private bool trainWorldUIVisible = true;

    // ─── Mid-Spline Join ────────────────────────────────────────────────

    private RailNode pendingJoinNode;

    private readonly List<Vector3> joinableNodeRefs = new();

    private struct SplineSample
    {
        public Vector3 worldPos;
        public int splineIndex;
        public Vector3 tangentDir; // normalized world-space tangent at this sample
        public float t;            // parametric position [0..1] on the spline
    }
    private readonly List<SplineSample> splineSamples = new();

    /// <summary>
    /// A single cubic Bézier segment in world-space XZ, with a precomputed AABB.
    /// </summary>
    private struct BezierSegXZ
    {
        public float P0x, P0z, P1x, P1z, P2x, P2z, P3x, P3z;
        public float minX, maxX, minZ, maxZ; // AABB of the 4 control points
    }

    // ─── Init ────────────────────────────────────────────────────────────

    private void Reset() { AutoWireSelf(); }

    [ContextMenu("Auto-Wire Self")]
    private void AutoWireSelf()
    {
        if (network == null) network = GetComponent<RailNetworkAuthoring>();
        if (railGraph == null) railGraph = GetComponent<RailGraph>();
        if (railLineRenderer == null) railLineRenderer = GetComponent<RailLineRenderer>();
        if (terrainGrader == null) terrainGrader = GetComponent<RailTerrainGrader>();
    }

    private void Awake()
    {
        AutoWireSelf();

        // Preview line for the selected candidate's track.
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

        // Materials for candidate dots (Unlit/Color so they're always visible).
        var markerShader = Shader.Find("Unlit/Color");
        candidateMat = new Material(markerShader);
        candidateMat.color = candidateColor;
        selectedMat = new Material(markerShader);
        selectedMat.color = selectedColor;
        joinMat = new Material(markerShader);
        joinMat.color = joinColor;
        parallelReturnMat = new Material(markerShader);
        parallelReturnMat.color = new Color(1f, 0.95f, 0.4f, 0.95f); // bright gold
        starMesh = BuildStarMesh(5, 0.5f, 0.22f, 0.15f);
        var tempSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphereMesh = tempSphere.GetComponent<MeshFilter>().sharedMesh;
        DestroyImmediate(tempSphere);
        deleteMat = new Material(markerShader);
        deleteMat.color = new Color(1f, 0.2f, 0.2f, 0.9f);
        deleteSelectedMat = new Material(markerShader);
        deleteSelectedMat.color = Color.white;
        selectMat = new Material(markerShader);
        selectMat.color = new Color(1f, 0.6f, 0.1f, 0.9f);
        selectSelectedMat = new Material(markerShader);
        selectSelectedMat.color = new Color(1f, 1f, 0.3f, 0.9f);
        switchMat = new Material(markerShader);
        switchMat.color = new Color(0.6f, 0.2f, 1f, 0.9f);
        switchActiveMat = new Material(markerShader);
        switchActiveMat.color = new Color(0.8f, 0.3f, 1f, 0.9f);

        // Cardinal direction-group materials (match JunctionGateUI.GroupColors).
        northMat = new Material(markerShader);
        northMat.color = new Color(1.00f, 0.85f, 0.10f, 0.9f);  // Yellow
        eastMat = new Material(markerShader);
        eastMat.color = new Color(0.10f, 0.70f, 0.15f, 0.9f);   // Green
        southMat = new Material(markerShader);
        southMat.color = new Color(0.10f, 0.50f, 0.85f, 0.9f);  // Blue
        westMat = new Material(markerShader);
        westMat.color = new Color(0.95f, 0.30f, 0.10f, 0.9f);   // Orange/Red
        switchPathMat = new Material(Shader.Find("Sprites/Default"));
        switchPathMat.color = new Color(0.1f, 1f, 0.2f, 0.9f);
        joinArmMat = new Material(Shader.Find("Sprites/Default"));
        joinArmMat.color = new Color(0.6f, 0.2f, 1f, 0.7f); // purple, semi-transparent
        placeMat = new Material(markerShader);
        placeMat.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        placeSelectedMat = new Material(markerShader);
        placeSelectedMat.color = new Color(1f, 1f, 1f, 0.9f);

        // Pre-create marker pool.
        cylinderMesh = Resources.GetBuiltinResource<Mesh>("New-Cylinder.fbx");
        for (int i = 0; i < MaxCandidates; i++)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = $"CandidateMarker_{i}";
            marker.transform.SetParent(transform);
            marker.transform.localScale = new Vector3(markerRadius, 0.15f, markerRadius);
            // Remove collider so it doesn't interfere with terrain raycasts.
            var col = marker.GetComponent<Collider>();
            if (col != null) Destroy(col);
            // Cache the default cylinder mesh.
            if (cylinderMesh == null) cylinderMesh = marker.GetComponent<MeshFilter>().sharedMesh;
            marker.GetComponent<Renderer>().sharedMaterial = candidateMat;
            marker.SetActive(false);
            markerPool.Add(marker);
        }
    }

    private void OnEnable()
    {
        isDrawing = false;
        knotCount = 0;
        lastDirection = Vector3.zero;
        HideAllMarkers();
        if (previewLine != null) previewLine.positionCount = 0;

        if (drawButton != null)       drawButton.onClick.AddListener(() => ActiveTool = ConductorTool.Draw);
        if (deleteButton != null)     deleteButton.onClick.AddListener(() => ActiveTool = ConductorTool.Delete);
        if (selectButton != null)     selectButton.onClick.AddListener(() => ActiveTool = ConductorTool.Select);
        if (switchButton != null)     switchButton.onClick.AddListener(() => ActiveTool = ConductorTool.Switch);
        if (toggleTrainUIButton != null) toggleTrainUIButton.onClick.AddListener(ToggleTrainWorldUI);
        PopulateTrainCatalog();
        UpdateToolButtonVisuals();
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
        HideAllMarkers();
        if (previewLine != null) previewLine.positionCount = 0;

        if (drawButton != null)       drawButton.onClick.RemoveAllListeners();
        if (deleteButton != null)     deleteButton.onClick.RemoveAllListeners();
        if (selectButton != null)     selectButton.onClick.RemoveAllListeners();
        if (switchButton != null)     switchButton.onClick.RemoveAllListeners();
        if (toggleTrainUIButton != null) toggleTrainUIButton.onClick.RemoveAllListeners();
        ClearTrainCatalogButtons();
    }

    private void UpdateToolButtonVisuals()
    {
        SetButtonHighlight(drawButton,   activeTool == ConductorTool.Draw);
        SetButtonHighlight(deleteButton, activeTool == ConductorTool.Delete);
        SetButtonHighlight(selectButton, activeTool == ConductorTool.Select);
        SetButtonHighlight(switchButton, activeTool == ConductorTool.Switch);
    }

    private static void SetButtonHighlight(Button btn, bool active)
    {
        if (btn == null) return;
        var colors = btn.colors;
        colors.normalColor = active ? new Color(0.3f, 0.8f, 0.3f) : Color.white;
        btn.colors = colors;
    }

    // ─── Update ──────────────────────────────────────────────────────────

    private void Update()
    {
        if (Mouse.current == null) return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            HideAllMarkers();
            if (previewLine != null) previewLine.positionCount = 0;
            return;
        }

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = sourceCamera.ScreenPointToRay(mousePos);

        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, terrainLayer))
        {
            HideAllMarkers();
            if (previewLine != null) previewLine.positionCount = 0;
            return;
        }

        UpdatePreview(hit.point);

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (activeTool == ConductorTool.Draw)
                PlaceKnot(hit.point);
            else if (activeTool == ConductorTool.Delete)
                TryDeleteSegment(hit.point);
            else if (activeTool == ConductorTool.Select)
                TrySelectEndpoint();
            else if (activeTool == ConductorTool.Switch)
                TryCycleSwitch(hit.point);
            else if (activeTool == ConductorTool.Place)
                TryPlaceTrain();
        }

        if (Mouse.current.rightButton.wasPressedThisFrame
            || (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame))
            FinishSpline();
    }

    // ─── Tile Snapping ───────────────────────────────────────────────────

    private Vector3 SnapPosToTileCenter(Vector3 pos)
    {
        if (terrainGrader == null) return pos;
        return terrainGrader.SnapToTileCenter(pos);
    }

    // ─── Delete (Phase 5) ────────────────────────────────────────────────

    private void TryDeleteSegment(Vector3 cursorPos)
    {
        if (selectedDeleteKnot < 0 || selectedDeleteKnot >= deleteKnotRefs.Count) return;
        var knotRef = deleteKnotRefs[selectedDeleteKnot];
        DeleteKnot(knotRef.splineIndex, knotRef.knotIndex);
    }

    private void UpdateDeletePreview(Vector3 cursorWorldPos)
    {
        deleteKnotRefs.Clear();

        if (network == null)
        {
            HideAllMarkers();
            return;
        }

        // Gather all knot world positions from finished splines.
        for (int s = 0; s < network.SplineCount; s++)
        {
            if (s == network.ActiveSplineIndex) continue;
            var spline = network.Container.Splines[s];
            for (int k = 0; k < spline.Count; k++)
            {
                Vector3 worldPos = network.transform.TransformPoint((Vector3)spline[k].Position);
                deleteKnotRefs.Add(new DeleteKnotRef
                {
                    splineIndex = s,
                    knotIndex = k,
                    worldPos = worldPos
                });
            }
        }

        // Find nearest knot to cursor (XZ distance).
        float bestDistSq = float.MaxValue;
        selectedDeleteKnot = -1;
        for (int i = 0; i < deleteKnotRefs.Count; i++)
        {
            Vector3 diff = deleteKnotRefs[i].worldPos - cursorWorldPos;
            diff.y = 0f;
            float dSq = diff.sqrMagnitude;
            if (dSq < bestDistSq)
            {
                bestDistSq = dSq;
                selectedDeleteKnot = i;
            }
        }

        // Ensure pool is large enough.
        EnsureMarkerPoolSize(deleteKnotRefs.Count);

        // Position and color markers.
        for (int i = 0; i < markerPool.Count; i++)
        {
            if (i < deleteKnotRefs.Count)
            {
                markerPool[i].SetActive(true);
                Vector3 pos = deleteKnotRefs[i].worldPos;
                pos.y += 0.5f;
                markerPool[i].transform.position = pos;

                bool isSel = (i == selectedDeleteKnot);
                markerPool[i].GetComponent<Renderer>().sharedMaterial =
                    isSel ? deleteSelectedMat : deleteMat;
                markerPool[i].transform.localScale = isSel
                    ? new Vector3(markerRadius * 1.3f, 0.2f, markerRadius * 1.3f)
                    : new Vector3(markerRadius, 0.15f, markerRadius);
            }
            else
            {
                markerPool[i].SetActive(false);
            }
        }

        if (previewLine != null) previewLine.positionCount = 0;
    }

    private void EnsureMarkerPoolSize(int count)
    {
        while (markerPool.Count < count)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = $"CandidateMarker_{markerPool.Count}";
            marker.transform.SetParent(transform);
            marker.transform.localScale = new Vector3(markerRadius, 0.15f, markerRadius);
            var col = marker.GetComponent<Collider>();
            if (col != null) Destroy(col);
            marker.GetComponent<Renderer>().sharedMaterial = candidateMat;
            marker.SetActive(false);
            markerPool.Add(marker);
        }
    }

    /// <summary>
    /// Computes cardinal group index (0=N, 1=E, 2=S, 3=W) from a direction vector.
    /// </summary>
    private static int ComputeCardinalGroup(Vector3 dir)
    {
        float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg; // 0=+Z
        angle = ((angle % 360f) + 360f) % 360f;
        return Mathf.FloorToInt(((angle + 45f) % 360f) / 90f);
    }

    /// <summary>
    /// Returns cardinal-group material by index: 0=North(Yellow), 1=East(Green), 2=South(Blue), 3=West(Orange).
    /// </summary>
    private Material GetCardinalGroupMaterial(int group)
    {
        return group switch
        {
            0 => northMat,
            1 => eastMat,
            2 => southMat,
            3 => westMat,
            _ => candidateMat
        };
    }

    private void DeleteKnot(int splineIndex, int knotIndex)
    {
        if (network == null || railGraph == null) return;
        if (splineIndex < 0 || splineIndex >= network.SplineCount) return;

        var spline = network.Container.Splines[splineIndex];
        int knotCount = spline.Count;
        if (knotCount < 2) return;

        // Copy all knots before the segment is removed.
        var allKnots = new List<BezierKnot>();
        for (int i = 0; i < spline.Count; i++)
            allKnots.Add(spline[i]);

        // Ungrade the terrain along the spline before removing it.
        if (terrainGrader != null)
        {
            int samples = Mathf.Max(2, (spline.Count - 1) * 10);
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / (samples - 1);
                Vector3 pos = network.EvaluatePositionWorld(splineIndex, t);
                terrainGrader.UngradeAroundPoint(pos);
            }
        }

        // Remove the old segment from the graph (also removes the spline and reindexes).
        RailSegment seg = railGraph.FindSegmentBySpline(splineIndex);
        if (seg != null)
            railGraph.RemoveSegment(seg);
        else
            network.RemoveSpline(splineIndex);

        // Remove the deleted knot from our copy.
        allKnots.RemoveAt(knotIndex);

        if (knotCount <= 2)
        {
            // Only 0-1 knots remain — nothing to recreate.
        }
        else if (knotIndex == 0 || knotIndex == knotCount - 1)
        {
            // Endpoint removal: one shorter segment.
            if (allKnots.Count >= 2)
                CreateSplineAndRegister(allKnots);
        }
        else
        {
            // Mid-spline removal: split into two segments.
            var leftKnots  = allKnots.GetRange(0, knotIndex);
            var rightKnots = allKnots.GetRange(knotIndex, allKnots.Count - knotIndex);

            if (leftKnots.Count >= 2)
                CreateSplineAndRegister(leftKnots);
            if (rightKnots.Count >= 2)
                CreateSplineAndRegister(rightKnots);
        }

        // Rebuild visuals.
        if (railLineRenderer != null)
            railLineRenderer.RebuildFromSplines(network);
    }

    private void CreateSplineAndRegister(List<BezierKnot> knots)
    {
        int newIdx = network.AddSplineFromKnots(knots);

        // Compute endpoint positions.
        Vector3 startPos = network.transform.TransformPoint((Vector3)knots[0].Position);
        Vector3 endPos   = network.transform.TransformPoint((Vector3)knots[knots.Count - 1].Position);

        // Exit direction at start: use the first knot's TangentOut which the drawing
        // system set when the segment was originally created. This preserves curve angles.
        Vector3 startExit = Vector3.zero;
        Vector3 tanOut = network.transform.TransformDirection((Vector3)knots[0].TangentOut);
        tanOut.y = 0f;
        if (tanOut.sqrMagnitude > 0.001f)
            startExit = tanOut.normalized;
        else if (knots.Count >= 2)
        {
            // Fallback: knot-to-knot direction if tangent is zero.
            startExit = (network.transform.TransformPoint((Vector3)knots[1].Position) - startPos);
            startExit.y = 0f;
            if (startExit.sqrMagnitude > 0.001f) startExit.Normalize();
        }

        // Exit direction at end: use the last knot's TangentIn which points back along
        // the track (the arriving tangent).
        Vector3 endExit = Vector3.zero;
        Vector3 tanIn = network.transform.TransformDirection((Vector3)knots[knots.Count - 1].TangentIn);
        tanIn.y = 0f;
        if (tanIn.sqrMagnitude > 0.001f)
            endExit = tanIn.normalized;
        else if (knots.Count >= 2)
        {
            Vector3 prev = network.transform.TransformPoint((Vector3)knots[knots.Count - 2].Position);
            endExit = (prev - endPos);
            endExit.y = 0f;
            if (endExit.sqrMagnitude > 0.001f) endExit.Normalize();
        }

        RailNode startNode = railGraph.GetOrCreateNode(startPos);
        RailNode endNode   = railGraph.GetOrCreateNode(endPos);
        railGraph.RegisterSegment(startNode, endNode, newIdx, startExit, endExit);

        // Re-grade terrain along the newly created spline so surviving segments stay raised.
        // Spline Y is already at bed height (base + raise), so subtract the raise
        // to get the original base Y that GradeAroundPoint expects.
        if (terrainGrader != null)
        {
            float bedOffset = terrainGrader.BedRaiseWorldHeight;
            int samples = Mathf.Max(2, (knots.Count - 1) * 10);
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / (samples - 1);
                Vector3 pos = network.EvaluatePositionWorld(newIdx, t);
                pos.y -= bedOffset;
                terrainGrader.GradeAroundPoint(pos);
            }
        }
    }

    private static Vector3 SnapToCardinal(Vector3 dir)
    {
        float absX = Mathf.Abs(dir.x);
        float absZ = Mathf.Abs(dir.z);
        if (absX >= absZ)
            return dir.x >= 0 ? Vector3.right : Vector3.left;
        else
            return dir.z >= 0 ? Vector3.forward : Vector3.back;
    }

    // ─── Select (continue drawing from endpoint) ─────────────────────────

    private void UpdateSelectPreview(Vector3 cursorWorldPos)
    {
        selectRefs.Clear();

        if (network == null || railGraph == null)
        {
            HideAllMarkers();
            return;
        }

        // Gather every knot on every finished spline.
        for (int s = 0; s < network.SplineCount; s++)
        {
            if (s == network.ActiveSplineIndex) continue;
            var spline = network.Container.Splines[s];
            if (spline.Count < 2) continue;

            for (int k = 0; k < spline.Count; k++)
            {
                Vector3 wp = network.transform.TransformPoint((Vector3)spline[k].Position);
                bool isMid = k > 0 && k < spline.Count - 1;

                Vector3 exitDir, exitDir2 = Vector3.zero;
                if (isMid)
                {
                    exitDir  = GetKnotTangentDir(network, spline, s, k, forward: true);
                    exitDir2 = GetKnotTangentDir(network, spline, s, k, forward: false);
                }
                else if (k == 0)
                {
                    exitDir = GetKnotTangentDir(network, spline, s, k, forward: true);
                }
                else
                {
                    exitDir = GetKnotTangentDir(network, spline, s, k, forward: false);
                }

                selectRefs.Add(new SelectEndpointRef
                {
                    splineIndex = s, knotIndex = k, knotCount = spline.Count,
                    worldPos = wp, exitDir = exitDir, exitDir2 = exitDir2,
                    isMidKnot = isMid
                });
            }

            // Also add mid-spline tile-grid samples so the user can fork
            // from any tile position along the spline, not just knots.
            Vector3 sp0 = network.EvaluatePositionWorld(s, 0f);
            Vector3 sp1 = network.EvaluatePositionWorld(s, 1f);
            float approxLen = Vector3.Distance(sp0, sp1);
            int sampleCount = Mathf.Max((int)(approxLen * 2f), 20);

            for (int si = 1; si < sampleCount; si++)
            {
                float t = (float)si / sampleCount;
                Vector3 rawPos = network.EvaluatePositionWorld(s, t);
                Vector3 snapped = SnapPosToTileCenter(rawPos);
                snapped.y = rawPos.y;

                // Skip if too close to an existing knot ref (they take priority).
                bool nearKnot = false;
                for (int r = 0; r < selectRefs.Count; r++)
                {
                    Vector3 d = selectRefs[r].worldPos - snapped;
                    d.y = 0f;
                    if (d.sqrMagnitude < 0.8f * 0.8f) { nearKnot = true; break; }
                }
                if (nearKnot) continue;

                // Dedup against previously added mid-spline samples on this spline.
                bool dup = false;
                for (int r = selectRefs.Count - 1; r >= 0; r--)
                {
                    if (!selectRefs[r].isMidSplineSample) break; // stop at knot refs
                    Vector3 d = selectRefs[r].worldPos - snapped;
                    d.y = 0f;
                    if (d.sqrMagnitude < 0.8f * 0.8f) { dup = true; break; }
                }
                if (dup) continue;

                Vector3 tangent = ((Vector3)network.Container.EvaluateTangent(s, t)).normalized;
                if (tangent.sqrMagnitude < 0.01f) tangent = Vector3.forward;

                selectRefs.Add(new SelectEndpointRef
                {
                    splineIndex = s, knotIndex = -1, knotCount = spline.Count,
                    worldPos = snapped, exitDir = tangent, exitDir2 = -tangent,
                    isMidKnot = true, isMidSplineSample = true
                });
            }
        }

        // Find nearest endpoint to cursor.
        float bestDistSq = float.MaxValue;
        selectedSelectEndpoint = -1;
        for (int i = 0; i < selectRefs.Count; i++)
        {
            Vector3 diff = selectRefs[i].worldPos - cursorWorldPos;
            diff.y = 0f;
            float dSq = diff.sqrMagnitude;
            if (dSq < bestDistSq)
            {
                bestDistSq = dSq;
                selectedSelectEndpoint = i;
            }
        }

        EnsureMarkerPoolSize(selectRefs.Count);

        for (int i = 0; i < markerPool.Count; i++)
        {
            if (i < selectRefs.Count)
            {
                markerPool[i].SetActive(true);
                Vector3 pos = selectRefs[i].worldPos;
                pos.y += 0.5f;
                markerPool[i].transform.position = pos;

                bool isSel = (i == selectedSelectEndpoint);
                markerPool[i].GetComponent<Renderer>().sharedMaterial =
                    isSel ? selectSelectedMat : selectMat;
                markerPool[i].transform.localScale = isSel
                    ? new Vector3(markerRadius * 1.3f, 0.2f, markerRadius * 1.3f)
                    : new Vector3(markerRadius, 0.15f, markerRadius);
            }
            else
            {
                markerPool[i].SetActive(false);
            }
        }

        if (previewLine != null) previewLine.positionCount = 0;
    }

    private void TrySelectEndpoint()
    {
        if (selectedSelectEndpoint < 0 || selectedSelectEndpoint >= selectRefs.Count) return;
        var ep = selectRefs[selectedSelectEndpoint];

        Vector3 worldPos = ep.worldPos;

        // Mid-spline sample (no knot here yet): split the segment to create a junction node.
        if (ep.isMidSplineSample)
        {
            RailNode splitNode = railGraph.SplitSegmentAtPosition(ep.splineIndex, worldPos);
            if (splitNode == null) return;
            if (railLineRenderer != null)
                railLineRenderer.RebuildFromSplines(network);
        }
        // If this is a mid-spline knot, split the spline at this point first
        // so there's a proper graph node here.
        else if (ep.knotIndex > 0 && ep.knotIndex < ep.knotCount - 1)
            SplitSplineAtKnot(ep.splineIndex, ep.knotIndex);

        // The exitDir points along the EXISTING track. To continue
        // building away from the existing rail we need the opposite direction.
        Vector3 continuationDir = -ep.exitDir;
        continuationDir.y = 0f;
        if (continuationDir.sqrMagnitude > 0.001f) continuationDir.Normalize();

        network.AddKnotExplicit(worldPos, Vector3.zero, Vector3.zero);
        isDrawing = true;
        drawingStartPos = worldPos;
        drawingStartExitDir = continuationDir;
        drawingEndExitDir = continuationDir;
        lastDirection = continuationDir;

        if (ep.isMidKnot)
        {
            // Mid-curve knot: generate candidates for both tangent
            // directions so the user can branch either way.
            Vector3 altDir = -ep.exitDir2;
            altDir.y = 0f;
            if (altDir.sqrMagnitude > 0.001f) altDir.Normalize();
            midKnotAltDirection = altDir;
        }
        else
        {
            midKnotAltDirection = Vector3.zero;
        }

        // Curved tangent → constrained candidates (knotCount=2).
        // Cardinal-aligned → all-direction candidates (knotCount=1).
        knotCount = IsCardinalAligned(continuationDir) && !ep.isMidKnot ? 1 : 2;

        // Switch to Draw tool so candidates show immediately.
        activeTool = ConductorTool.Draw;
        UpdateToolButtonVisuals();
    }

    /// <summary>
    /// Returns true if the direction is closely aligned with a cardinal (N/S/E/W).
    /// </summary>
    private static bool IsCardinalAligned(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return true;
        dir.Normalize();

        // Check dot product against each cardinal. Threshold ~15° (cos 15° ≈ 0.966).
        const float threshold = 0.95f;
        if (Mathf.Abs(Vector3.Dot(dir, Vector3.forward)) > threshold) return true;
        if (Mathf.Abs(Vector3.Dot(dir, Vector3.right))   > threshold) return true;
        return false;
    }

    /// <summary>
    /// Gets the tangent direction at a knot, projected to XZ and normalized.
    /// <paramref name="forward"/>: true = TangentOut / toward next knot,
    /// false = TangentIn / toward previous knot.
    /// </summary>
    private static Vector3 GetKnotTangentDir(RailNetworkAuthoring net, Spline spline,
                                              int splineIndex, int knotIndex, bool forward)
    {
        if (forward)
        {
            Vector3 tanOut = net.transform.TransformDirection((Vector3)spline[knotIndex].TangentOut);
            tanOut.y = 0f;
            if (tanOut.sqrMagnitude > 0.001f) return tanOut.normalized;
            if (knotIndex < spline.Count - 1)
            {
                Vector3 wp = net.transform.TransformPoint((Vector3)spline[knotIndex].Position);
                Vector3 next = net.transform.TransformPoint((Vector3)spline[knotIndex + 1].Position);
                Vector3 d = next - wp; d.y = 0f;
                if (d.sqrMagnitude > 0.001f) return d.normalized;
            }
            return Vector3.forward;
        }
        else
        {
            Vector3 tanIn = net.transform.TransformDirection((Vector3)spline[knotIndex].TangentIn);
            tanIn.y = 0f;
            if (tanIn.sqrMagnitude > 0.001f) return tanIn.normalized;
            if (knotIndex > 0)
            {
                Vector3 wp = net.transform.TransformPoint((Vector3)spline[knotIndex].Position);
                Vector3 prev = net.transform.TransformPoint((Vector3)spline[knotIndex - 1].Position);
                Vector3 d = prev - wp; d.y = 0f;
                if (d.sqrMagnitude > 0.001f) return d.normalized;
            }
            return Vector3.back;
        }
    }

    /// <summary>
    /// Splits a spline into two segments at the given knot index, creating
    /// a graph node at the split point so new track can branch from it.
    /// </summary>
    private void SplitSplineAtKnot(int splineIndex, int knotIndex)
    {
        if (network == null || railGraph == null) return;
        if (splineIndex < 0 || splineIndex >= network.SplineCount) return;

        var spline = network.Container.Splines[splineIndex];
        if (knotIndex <= 0 || knotIndex >= spline.Count - 1) return;

        // Copy all knots.
        var allKnots = new List<BezierKnot>();
        for (int i = 0; i < spline.Count; i++)
            allKnots.Add(spline[i]);

        // Remove the old segment from the graph.
        RailSegment seg = railGraph.FindSegmentBySpline(splineIndex);
        if (seg != null)
            railGraph.RemoveSegment(seg);
        else
            network.RemoveSpline(splineIndex);

        // Create two new segments: [0..knotIndex] and [knotIndex..end].
        var leftKnots  = allKnots.GetRange(0, knotIndex + 1);
        var rightKnots = allKnots.GetRange(knotIndex, allKnots.Count - knotIndex);

        if (leftKnots.Count >= 2)
            CreateSplineAndRegister(leftKnots);
        if (rightKnots.Count >= 2)
            CreateSplineAndRegister(rightKnots);

        if (railLineRenderer != null)
            railLineRenderer.RebuildFromSplines(network);
    }

    // ─── Switch (cycle junction direction) ───────────────────────────────

    private void UpdateSwitchPreview(Vector3 cursorWorldPos)
    {
        switchJunctions.Clear();

        if (railGraph == null)
        {
            HideAllMarkers();
            return;
        }

        // Gather all junction nodes (3+ connections).
        for (int i = 0; i < railGraph.Nodes.Count; i++)
        {
            if (railGraph.Nodes[i].IsJunction)
                switchJunctions.Add(railGraph.Nodes[i]);
        }

        // Find nearest junction to cursor.
        float bestDistSq = float.MaxValue;
        selectedSwitchJunction = -1;
        for (int i = 0; i < switchJunctions.Count; i++)
        {
            Vector3 diff = switchJunctions[i].worldPosition - cursorWorldPos;
            diff.y = 0f;
            float dSq = diff.sqrMagnitude;
            if (dSq < bestDistSq)
            {
                bestDistSq = dSq;
                selectedSwitchJunction = i;
            }
        }

        EnsureMarkerPoolSize(switchJunctions.Count);

        for (int i = 0; i < markerPool.Count; i++)
        {
            if (i < switchJunctions.Count)
            {
                markerPool[i].SetActive(true);
                Vector3 pos = switchJunctions[i].worldPosition;
                pos.y += 0.5f;
                markerPool[i].transform.position = pos;

                bool isSel = (i == selectedSwitchJunction);
                markerPool[i].GetComponent<Renderer>().sharedMaterial =
                    isSel ? switchActiveMat : switchMat;
                markerPool[i].transform.localScale = isSel
                    ? new Vector3(markerRadius * 1.5f, 0.25f, markerRadius * 1.5f)
                    : new Vector3(markerRadius, 0.15f, markerRadius);
            }
            else
            {
                markerPool[i].SetActive(false);
            }
        }

        if (previewLine != null) previewLine.positionCount = 0;

        // Update persistent arrow indicators.
        UpdateSwitchArrows();
    }

    private void TryCycleSwitch(Vector3 cursorPos)
    {
        if (selectedSwitchJunction < 0 || selectedSwitchJunction >= switchJunctions.Count) return;

        RailNode node = switchJunctions[selectedSwitchJunction];

        // Determine which direction group the cursor is closest to
        // by using the cursor-to-junction direction.
        Vector3 clickDir = cursorPos - node.worldPosition;
        clickDir.y = 0f;
        if (clickDir.sqrMagnitude < 0.01f)
            clickDir = Vector3.forward;

        int group = node.ClassifyToGroup(clickDir);
        int exitCount = node.GetGroupExitCount(group);

        if (exitCount < 2)
        {
            // No cycling possible for this group — try via switch box.
            Debug.Log($"[RailDrawing] Group {group} at {node.worldPosition} has {exitCount} exit(s), no cycling needed.");
        }
        else
        {
            int newGate = node.CycleGate(group);
            Debug.Log($"[RailDrawing] Junction at {node.worldPosition} group {group} gate → {newGate}");

            // Refresh switch box UI if it exists.
            if (railGraph != null)
            {
                var box = railGraph.GetSwitchBox(node);
                if (box != null) box.Refresh(node);
            }
        }

        UpdateSwitchArrows();
    }

    private void UpdateSwitchArrows()
    {
        // Hide all existing path lines.
        for (int i = 0; i < switchPathLines.Count; i++)
        {
            if (switchPathLines[i] != null)
                switchPathLines[i].positionCount = 0;
        }

        if (railGraph == null || network == null) return;

        int lineIdx = 0;

        for (int i = 0; i < railGraph.Nodes.Count; i++)
        {
            RailNode node = railGraph.Nodes[i];
            if (!node.IsJunction) continue;

            // Show through-paths: for each pair of opposite direction groups,
            // draw the active gate exits as a connected route through the junction.
            // Groups 0↔2 and 1↔3 are the two possible through-paths.
            for (int pair = 0; pair < 2; pair++)
            {
                int groupA = pair;          // 0 or 1
                int groupB = pair + 2;      // 2 or 3

                var exitsA = node.GetGroupExits(groupA);
                var exitsB = node.GetGroupExits(groupB);

                // Skip if neither group has exits.
                if (exitsA.Count == 0 && exitsB.Count == 0) continue;

                // Gather the segments to draw for this through-path.
                RailSegment segA = null, segB = null;
                if (exitsA.Count > 0)
                    segA = exitsA[node.gateIndices[groupA] % exitsA.Count];
                if (exitsB.Count > 0)
                    segB = exitsB[node.gateIndices[groupB] % exitsB.Count];

                // Draw each active segment as a path line from the junction outward.
                RailSegment[] segs = { segA, segB };
                for (int s = 0; s < segs.Length; s++)
                {
                    if (segs[s] == null) continue;

                    // Ensure we have enough LineRenderers.
                    while (lineIdx >= switchPathLines.Count)
                    {
                        var pathObj = new GameObject("SwitchPathLine");
                        pathObj.transform.SetParent(transform);
                        var lr = pathObj.AddComponent<LineRenderer>();
                        lr.useWorldSpace = true;
                        lr.startWidth = switchPathWidth;
                        lr.endWidth = switchPathWidth;
                        lr.material = switchPathMat;
                        lr.startColor = Color.green;
                        lr.endColor = Color.green;
                        lr.positionCount = 0;
                        lr.numCornerVertices = 4;
                        lr.numCapVertices = 4;
                        switchPathLines.Add(lr);
                    }

                    LineRenderer line = switchPathLines[lineIdx];
                    line.startWidth = switchPathWidth;
                    line.endWidth = switchPathWidth;
                    lineIdx++;

                    int splineIdx = segs[s].splineIndex;
                    if (splineIdx < 0 || splineIdx >= network.SplineCount)
                    {
                        line.positionCount = 0;
                        continue;
                    }

                    bool junctionAtStart = (segs[s].startNode == node);

                    float tStart, tEnd;
                    if (junctionAtStart)
                    {
                        tStart = 0f;
                        tEnd = FindTAtArcLength(splineIdx, 0f, 1f, switchPathLength);
                    }
                    else
                    {
                        tEnd = 1f;
                        tStart = FindTAtArcLength(splineIdx, 1f, 0f, switchPathLength);
                    }

                    line.positionCount = SwitchPathSamples;
                    for (int sp = 0; sp < SwitchPathSamples; sp++)
                    {
                        float t = Mathf.Lerp(tStart, tEnd, (float)sp / (SwitchPathSamples - 1));
                        Vector3 pt = network.EvaluatePositionWorld(splineIdx, t);
                        pt.y += SwitchPathYOffset;
                        line.SetPosition(sp, pt);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Walks along a spline from tFrom toward tTo, measuring arc length,
    /// and returns the t value where the accumulated distance reaches maxDist.
    /// If the spline is shorter than maxDist, returns tTo.
    /// </summary>
    private float FindTAtArcLength(int splineIdx, float tFrom, float tTo, float maxDist)
    {
        const int steps = 64;
        float accum = 0f;
        Vector3 prev = network.EvaluatePositionWorld(splineIdx, tFrom);

        for (int i = 1; i <= steps; i++)
        {
            float t = Mathf.Lerp(tFrom, tTo, (float)i / steps);
            Vector3 cur = network.EvaluatePositionWorld(splineIdx, t);
            accum += Vector3.Distance(prev, cur);
            prev = cur;

            if (accum >= maxDist)
                return t;
        }

        return tTo;
    }

    private void HideSwitchPathLines()
    {
        for (int i = 0; i < switchPathLines.Count; i++)
        {
            if (switchPathLines[i] != null)
                switchPathLines[i].positionCount = 0;
        }
    }

    private void ShowJoinArmLines()
    {
        int lineIdx = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (!candidates[i].isJoin) continue;
            if (candidates[i].arcPoints == null || candidates[i].arcPoints.Length < 2) continue;
            if (i == selectedCandidate) continue; // selected candidate uses the main preview line

            // Ensure pool is big enough.
            while (lineIdx >= joinArmLines.Count)
            {
                var obj = new GameObject("JoinArmLine");
                obj.transform.SetParent(transform);
                var lr = obj.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.startWidth = JoinArmLineWidth;
                lr.endWidth = JoinArmLineWidth;
                lr.material = joinArmMat;
                lr.startColor = joinArmMat.color;
                lr.endColor = joinArmMat.color;
                lr.positionCount = 0;
                lr.numCornerVertices = 4;
                lr.numCapVertices = 2;
                joinArmLines.Add(lr);
            }

            LineRenderer line = joinArmLines[lineIdx];
            line.positionCount = candidates[i].arcPoints.Length;
            for (int p = 0; p < candidates[i].arcPoints.Length; p++)
            {
                Vector3 pt = candidates[i].arcPoints[p];
                pt.y += JoinArmYOffset;
                line.SetPosition(p, pt);
            }
            lineIdx++;
        }

        // Hide unused lines.
        for (int i = lineIdx; i < joinArmLines.Count; i++)
        {
            if (joinArmLines[i] != null)
                joinArmLines[i].positionCount = 0;
        }
    }

    private void HideJoinArmLines()
    {
        for (int i = 0; i < joinArmLines.Count; i++)
        {
            if (joinArmLines[i] != null)
                joinArmLines[i].positionCount = 0;
        }
    }

    // ─── Train Catalog ─────────────────────────────────────────────────

    private void PopulateTrainCatalog()
    {
        ClearTrainCatalogButtons();
        if (trainCatalog == null || trainButtonPrefab == null || trainButtonContainer == null) return;

        foreach (TrainDefinition def in trainCatalog.Items)
        {
            if (def == null) continue;
            TrainCatalogButton btn = Instantiate(trainButtonPrefab, trainButtonContainer);
            btn.Initialise(def, HandleTrainCatalogSelection);
            trainCatalogButtons.Add(btn);
        }
    }

    private void ClearTrainCatalogButtons()
    {
        foreach (TrainCatalogButton btn in trainCatalogButtons)
            if (btn != null) Destroy(btn.gameObject);
        trainCatalogButtons.Clear();
    }

    private void HandleTrainCatalogSelection(TrainDefinition def)
    {
        selectedTrainDef = def;
        ActiveTool = ConductorTool.Place;
    }

    // ─── Place Mode ─────────────────────────────────────────────────────

    private void UpdatePlacePreview(Vector3 cursorWorldPos)
    {
        placeKnotRefs.Clear();

        if (network == null || selectedTrainDef == null)
        {
            HideAllMarkers();
            return;
        }

        // Gather every knot on every finished spline.
        for (int s = 0; s < network.SplineCount; s++)
        {
            if (s == network.ActiveSplineIndex) continue;
            var spline = network.Container.Splines[s];
            for (int k = 0; k < spline.Count; k++)
            {
                Vector3 worldPos = network.transform.TransformPoint((Vector3)spline[k].Position);
                placeKnotRefs.Add(new PlaceKnotRef
                {
                    splineIndex = s,
                    knotIndex = k,
                    worldPos = worldPos
                });
            }
        }

        // Find nearest knot to cursor.
        float bestDistSq = float.MaxValue;
        selectedPlaceKnot = -1;
        for (int i = 0; i < placeKnotRefs.Count; i++)
        {
            Vector3 diff = placeKnotRefs[i].worldPos - cursorWorldPos;
            diff.y = 0f;
            float dSq = diff.sqrMagnitude;
            if (dSq < bestDistSq)
            {
                bestDistSq = dSq;
                selectedPlaceKnot = i;
            }
        }

        EnsureMarkerPoolSize(placeKnotRefs.Count);

        for (int i = 0; i < markerPool.Count; i++)
        {
            if (i < placeKnotRefs.Count)
            {
                markerPool[i].SetActive(true);
                Vector3 pos = placeKnotRefs[i].worldPos;
                pos.y += 0.5f;
                markerPool[i].transform.position = pos;

                bool isSel = (i == selectedPlaceKnot);
                markerPool[i].GetComponent<Renderer>().sharedMaterial =
                    isSel ? placeSelectedMat : placeMat;
                markerPool[i].transform.localScale = isSel
                    ? new Vector3(markerRadius * 1.3f, 0.2f, markerRadius * 1.3f)
                    : new Vector3(markerRadius, 0.15f, markerRadius);
            }
            else
            {
                markerPool[i].SetActive(false);
            }
        }

        if (previewLine != null) previewLine.positionCount = 0;
    }

    private void TryPlaceTrain()
    {
        if (selectedPlaceKnot < 0 || selectedPlaceKnot >= placeKnotRefs.Count) return;
        if (selectedTrainDef == null || selectedTrainDef.LocomotivePrefab == null) return;
        if (network == null) return;

        var knotRef = placeKnotRefs[selectedPlaceKnot];

        // Compute arc-length distance to the clicked knot by sampling the spline.
        float knotT = (float)knotRef.knotIndex / Mathf.Max(1, network.Container.Splines[knotRef.splineIndex].Count - 1);
        float arcLength = 0f;
        int samples = 128;
        Vector3 prev = network.EvaluatePositionWorld(knotRef.splineIndex, 0f);
        for (int i = 1; i <= samples; i++)
        {
            float t = (float)i / samples;
            Vector3 p = network.EvaluatePositionWorld(knotRef.splineIndex, t);
            float segDist = Vector3.Distance(prev, p);
            if (t <= knotT + 0.001f)
                arcLength += segDist;
            prev = p;
        }

        // Estimate the new train's occupied range on the spline.
        // PlaceOnSpline clamps distance so the consist fits — replicate that logic.
        Locomotive prefabLoco = selectedTrainDef.LocomotivePrefab;
        float newBogieSpacing = prefabLoco.BogieSpacingPublic;
        float newConsistLen = prefabLoco.EstimateConsistLength();
        float minFront = newBogieSpacing + newConsistLen;
        float newFront = Mathf.Max(arcLength, minFront);
        float newTail = newFront - newBogieSpacing - newConsistLen;

        // Check for overlap with existing trains on this spline.
        const float spawnGap = 1f;
        for (int i = 0; i < Locomotive.AllTrains.Count; i++)
        {
            Locomotive other = Locomotive.AllTrains[i];
            if (other.CurrentSplineIndex != knotRef.splineIndex) continue;
            float otherFront = other.FrontDistance;
            float otherTail = other.TailDistance;
            // Two ranges overlap if one starts before the other ends.
            bool overlaps = (newFront + spawnGap > otherTail) && (otherFront + spawnGap > newTail);
            if (overlaps)
            {
                Debug.Log("[RailDrawing] Cannot spawn train — overlaps with existing train.");
                return;
            }
        }

        GameObject instance = Instantiate(selectedTrainDef.LocomotivePrefab.gameObject);
        Locomotive loco = instance.GetComponent<Locomotive>();
        if (loco != null)
        {
            loco.PlaceOnSpline(network, knotRef.splineIndex, railGraph, arcLength);
            spawnedTrains.Add(loco);

            TrainWorldUI worldUI = instance.GetComponentInChildren<TrainWorldUI>();
            if (worldUI != null)
                worldUI.SetVisible(trainWorldUIVisible);
        }
    }

    private void ToggleTrainWorldUI()
    {
        trainWorldUIVisible = !trainWorldUIVisible;
        for (int i = spawnedTrains.Count - 1; i >= 0; i--)
        {
            if (spawnedTrains[i] == null) { spawnedTrains.RemoveAt(i); continue; }
            TrainWorldUI worldUI = spawnedTrains[i].GetComponentInChildren<TrainWorldUI>();
            if (worldUI != null) worldUI.SetVisible(trainWorldUIVisible);
        }
    }

    // ─── Knot Placement ─────────────────────────────────────────────────

    private void PlaceKnot(Vector3 cursorPos)
    {
        if (network == null) return;

        knotCount++;

        if (knotCount == 1)
            PlaceFirstKnot(cursorPos);
        else if (knotCount == 2)
            PlaceSecondKnot(cursorPos);
        else
            PlaceConstrainedKnot(cursorPos);
    }

    private void PlaceFirstKnot(Vector3 terrainHit)
    {
        Vector3 worldPos = terrainHit;
        if (terrainGrader != null)
        {
            worldPos = terrainGrader.SnapToTileCenter(worldPos);
            worldPos.y = terrainGrader.GetBedWorldHeight(terrainHit);
        }

        // Snap to an existing node if close enough (start a branch from existing track).
        bool snappedToNode = false;
        if (railGraph != null)
        {
            RailNode node = railGraph.FindNodeAtPosition(worldPos, 0.6f);
            if (node != null && node.CanAddConnection)
            {
                // Block branching from any node that's within 7 tiles of an
                // existing junction — unless the node itself IS a junction
                // (you should always be able to add exits to a junction).
                if (!node.IsJunction && IsNearExistingJunction(node, 7f))
                {
                    knotCount--;
                    Debug.Log($"[RailDrawing] First knot rejected — node too close to an existing junction ({worldPos})");
                    return;
                }
                worldPos = node.worldPosition;
                snappedToNode = true;
                Debug.Log($"[RailDrawing] First knot snapped to existing node at {worldPos}");
            }
        }

        // Reject placement if too close to an existing rail (min 7 tiles) unless
        // we snapped to an existing node (branching is always allowed).
        if (!snappedToNode && FindSplineNearPosition(worldPos, 7f) >= 0)
        {
            knotCount--;
            Debug.Log($"[RailDrawing] First knot rejected — too close to existing rail ({worldPos})");
            return;
        }

        network.AddKnotExplicit(worldPos, Vector3.zero, Vector3.zero);
        isDrawing = true;
        drawingStartPos = worldPos;
        drawingStartExitDir = Vector3.zero;
        drawingStartGroupHint = -1;
        drawingEndGroupHint = -1;
        midKnotAltDirection = Vector3.zero;

        if (terrainGrader != null)
            terrainGrader.GradeAroundPoint(worldPos);
        if (railLineRenderer != null)
            railLineRenderer.RebuildFromSplines(network);
    }

    private void PlaceSecondKnot(Vector3 cursorPos)
    {
        // Use the same candidate selection system as knot 3+.
        if (selectedCandidate < 0 || selectedCandidate >= candidates.Count)
        { knotCount--; return; }

        SnapResult snap = candidates[selectedCandidate];
        if (!snap.isValid) { knotCount--; return; }

        network.SetLastKnotTangentOut(snap.tangentOut);
        network.AddKnotExplicit(snap.position, snap.tangentIn, Vector3.zero);
        previousKnotPos = drawingStartPos;
        lastDirection = snap.exitDirection;
        // Start exit = tangent leaving the first knot (= forward direction).
        // snap.exitDirection is the tangent at the arc ENDPOINT which differs
        // for curves and would store the wrong direction at the start node.
        drawingStartExitDir = snap.tangentOut.normalized;
        drawingEndExitDir = snap.exitDirection;
        drawingStartGroupHint = snap.cardinalGroup;
        drawingEndGroupHint = snap.cardinalGroup;

        // Direction is now committed; clear the dual-direction state.
        midKnotAltDirection = Vector3.zero;

        GradeArcPoints(snap);

        if (railLineRenderer != null)
            railLineRenderer.RebuildFromSplines(network);

        if (snap.isJoin)
        {
            if (snap.isMidSplineJoin && railGraph != null)
            {
                pendingJoinNode = railGraph.SplitSegmentAtPosition(
                    snap.joinSplineIndex, snap.position);
                Debug.Log($"[RailDrawing] Second-knot mid-spline join → split spline {snap.joinSplineIndex}");
            }
            else
            {
                Debug.Log($"[RailDrawing] Second-knot join detected → finishing spline at node {snap.joinNode?.worldPosition}");
            }
            FinishSpline();
        }
    }

    private void PlaceCardinalKnot(Vector3 cursorPos)
    {
        Vector3 prevKnot = network.GetLastKnotWorld().Value;
        SnapResult snap = SnapSecondKnot(prevKnot, cursorPos);
        if (!snap.isValid) { knotCount--; return; }

        // Check if the cardinal endpoint lands on an existing node or spline.
        if (railGraph != null)
        {
            RailNode node = railGraph.FindNodeAtPosition(snap.position, 0.6f);
            if (node != null && node.CanAddConnection)
            {
                snap.isJoin   = true;
                snap.joinNode = node;
                snap.position = node.worldPosition;
            }
            else
            {
                // Check mid-spline join.
                int hitSpline = FindSplineNearPosition(snap.position, 0.6f);
                if (hitSpline >= 0)
                {
                    snap.isJoin = true;
                    snap.isMidSplineJoin = true;
                    snap.joinSplineIndex = hitSpline;
                }
            }
        }

        network.SetLastKnotTangentOut(snap.tangentOut);
        network.AddKnotExplicit(snap.position, snap.tangentIn, Vector3.zero);
        previousKnotPos = prevKnot;
        lastDirection = snap.exitDirection;
        drawingStartExitDir = snap.exitDirection;
        drawingEndExitDir = snap.exitDirection;
        drawingStartGroupHint = snap.cardinalGroup;
        drawingEndGroupHint = snap.cardinalGroup;

        GradeArcPoints(snap);

        if (railLineRenderer != null)
            railLineRenderer.RebuildFromSplines(network);

        if (snap.isJoin)
        {
            if (snap.isMidSplineJoin)
            {
                pendingJoinNode = railGraph.SplitSegmentAtPosition(
                    snap.joinSplineIndex, snap.position);
                Debug.Log($"[RailDrawing] Cardinal mid-spline join → split spline {snap.joinSplineIndex}");
            }
            else
            {
                Debug.Log($"[RailDrawing] Cardinal join detected → finishing spline at node {snap.joinNode.worldPosition}");
            }
            FinishSpline();
        }
    }

    private void PlaceConstrainedKnot(Vector3 cursorPos)
    {
        if (selectedCandidate < 0 || selectedCandidate >= candidates.Count)
        { knotCount--; return; }

        SnapResult snap = candidates[selectedCandidate];
        if (!snap.isValid) { knotCount--; return; }

        // ── Multi-segment completion: auto-place all intermediate knots ──
        if (snap.completionKnots != null && snap.completionKnots.Length > 0)
        {
            // Place first segment (current knot → first completion knot).
            network.SetLastKnotTangentOut(snap.tangentOut);

            for (int k = 0; k < snap.completionKnots.Length; k++)
            {
                var ck = snap.completionKnots[k];
                network.AddKnotExplicit(ck.position, ck.tangentIn, ck.tangentOut);
                knotCount++;

                // Grade terrain along this segment's arc points.
                if (terrainGrader != null && ck.gradePoints != null)
                {
                    for (int i = 0; i < ck.gradePoints.Length - 1; i++)
                        terrainGrader.GradeAlongSegment(ck.gradePoints[i], ck.gradePoints[i + 1]);
                    if (ck.gradePoints.Length > 0)
                        terrainGrader.GradeAroundPoint(ck.gradePoints[^1]);
                }
            }

            // Place the final endpoint.
            network.AddKnotExplicit(snap.position, snap.tangentIn, Vector3.zero);
            knotCount++;
            previousKnotPos = (snap.completionKnots.Length > 0)
                ? snap.completionKnots[^1].position : network.GetSecondToLastKnotWorld() ?? snap.position;
            lastDirection = snap.exitDirection;
            drawingEndExitDir = snap.exitDirection;
            drawingEndGroupHint = snap.cardinalGroup;

            if (railLineRenderer != null)
                railLineRenderer.RebuildFromSplines(network);

            if (snap.isJoin)
            {
                if (snap.isMidSplineJoin && railGraph != null)
                {
                    pendingJoinNode = railGraph.SplitSegmentAtPosition(
                        snap.joinSplineIndex, snap.position);
                }
                FinishSpline();
            }
            return;
        }

        network.SetLastKnotTangentOut(snap.tangentOut);
        network.AddKnotExplicit(snap.position, snap.tangentIn, Vector3.zero);
        previousKnotPos = network.GetSecondToLastKnotWorld() ?? snap.position;
        lastDirection = snap.exitDirection;
        drawingEndExitDir = snap.exitDirection;
        drawingEndGroupHint = snap.cardinalGroup;

        GradeArcPoints(snap);

        if (railLineRenderer != null)
            railLineRenderer.RebuildFromSplines(network);

        // If the candidate connects to an existing node or spline, finish.
        if (snap.isJoin)
        {
            if (snap.isMidSplineJoin && railGraph != null)
            {
                pendingJoinNode = railGraph.SplitSegmentAtPosition(
                    snap.joinSplineIndex, snap.position);
                Debug.Log($"[RailDrawing] Mid-spline join → split spline {snap.joinSplineIndex} at {snap.position}");
            }
            else
            {
                Debug.Log($"[RailDrawing] Join detected → finishing spline at node {snap.joinNode?.worldPosition}");
            }
            FinishSpline();
        }
    }

    // ─── Parallel Return Detection ─────────────────────────────────────

    /// <summary>
    /// Finds the one candidate that mirrors the last placed segment, which
    /// would return the track to a direction parallel to the original.
    /// The mirror is computed by reflecting the last segment’s offset across
    /// the current forward direction (same forward, opposite lateral).
    /// </summary>
    private void TagParallelReturnCandidate(Vector3 lastKnot, Vector3 forward)
    {
        if (knotCount < 2) return; // need at least one placed segment to mirror
        if (forward.sqrMagnitude < 0.1f) return;

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        // Offset of the last placed segment.
        Vector3 delta = lastKnot - previousKnotPos;
        delta.y = 0f;

        // Decompose into the current forward frame.
        float fComp = Vector3.Dot(delta, forward);
        float lComp = Vector3.Dot(delta, right);

        // If the last segment was straight (no lateral), there's no mirror to show.
        if (Mathf.Abs(lComp) < 0.3f) return;

        // Mirror: same forward component, opposite lateral component → continues arc.
        Vector3 mirrorTarget = lastKnot + forward * fComp - right * lComp;
        mirrorTarget.y = lastKnot.y;
        mirrorTarget = SnapPosToTileCenter(mirrorTarget);

        // Straighten: same offset repeated → reverts curve back to straight.
        Vector3 straightenTarget = lastKnot + delta;
        straightenTarget.y = lastKnot.y;
        straightenTarget = SnapPosToTileCenter(straightenTarget);

        float bestMirrorDist = 0.6f;
        int bestMirrorIdx = -1;
        float bestStraightenDist = 0.6f;
        int bestStraightenIdx = -1;

        for (int i = 0; i < candidates.Count; i++)
        {
            Vector3 cFlat = new Vector3(candidates[i].position.x, 0, candidates[i].position.z);

            float dm = Vector3.Distance(cFlat, new Vector3(mirrorTarget.x, 0, mirrorTarget.z));
            if (dm < bestMirrorDist)
            {
                bestMirrorDist = dm;
                bestMirrorIdx = i;
            }

            float ds = Vector3.Distance(cFlat, new Vector3(straightenTarget.x, 0, straightenTarget.z));
            if (ds < bestStraightenDist)
            {
                bestStraightenDist = ds;
                bestStraightenIdx = i;
            }
        }

        if (bestMirrorIdx >= 0)
        {
            var c = candidates[bestMirrorIdx];
            c.isParallelReturn = true;
            candidates[bestMirrorIdx] = c;
        }

        // Don't flag the same candidate as both.
        if (bestStraightenIdx >= 0 && bestStraightenIdx != bestMirrorIdx)
        {
            var c = candidates[bestStraightenIdx];
            c.isStraightenReturn = true;
            candidates[bestStraightenIdx] = c;
        }
    }

    /// <summary>
    /// When drawing from a junction node, look at all existing exit curves and
    /// flag candidates that would mirror those curves across the junction's axes.
    /// This lets the player build symmetrical junction exits easily.
    /// </summary>
    private void TagJunctionMirrorCandidates()
    {
        if (railGraph == null || candidates.Count == 0) return;

        // Find the junction node at our drawing start.
        RailNode junctionNode = railGraph.FindNodeAtPosition(drawingStartPos, 0.6f);
        if (junctionNode == null || junctionNode.connections.Count < 1) return;

        Vector3 jPos = junctionNode.worldPosition;
        jPos.y = 0f;

        // Collect the offset of each existing exit's second knot relative to the junction.
        var exitDeltas = new List<Vector3>();
        for (int c = 0; c < junctionNode.connections.Count; c++)
        {
            RailSegment seg = junctionNode.connections[c];
            if (seg.splineIndex < 0 || seg.splineIndex >= network.Container.Splines.Count) continue;
            var spline = network.Container.Splines[seg.splineIndex];
            if (spline.Count < 2) continue;

            // Determine which end of the spline is at the junction.
            Vector3 firstKnotWorld = network.transform.TransformPoint((Vector3)spline[0].Position);
            Vector3 lastKnotWorld  = network.transform.TransformPoint((Vector3)spline[spline.Count - 1].Position);

            Vector3 secondKnotWorld;
            float distFirst = Vector3.Distance(new Vector3(firstKnotWorld.x, 0, firstKnotWorld.z),
                                                new Vector3(junctionNode.worldPosition.x, 0, junctionNode.worldPosition.z));
            float distLast  = Vector3.Distance(new Vector3(lastKnotWorld.x, 0, lastKnotWorld.z),
                                                new Vector3(junctionNode.worldPosition.x, 0, junctionNode.worldPosition.z));

            if (distFirst < distLast)
                secondKnotWorld = network.transform.TransformPoint((Vector3)spline[1].Position);
            else
                secondKnotWorld = network.transform.TransformPoint((Vector3)spline[spline.Count - 2].Position);

            Vector3 delta = secondKnotWorld - junctionNode.worldPosition;
            delta.y = 0f;

            // Only count non-straight exits (delta has both forward and lateral components).
            float ax = Mathf.Abs(delta.x);
            float az = Mathf.Abs(delta.z);
            if (ax < 0.3f || az < 0.3f) continue; // Straight exit — skip.

            exitDeltas.Add(delta);
        }

        if (exitDeltas.Count == 0) return;

        // For each exit delta, generate all 7 symmetry transformations
        // (reflections + 90°/270° rotations) to cover opposite AND adjacent directions.
        var mirrorTargets = new HashSet<Vector2Int>(); // snap to avoid duplicates
        foreach (var delta in exitDeltas)
        {
            Vector3[] symmetries = new Vector3[]
            {
                new Vector3(-delta.x,  0f,  delta.z), // reflect X
                new Vector3( delta.x,  0f, -delta.z), // reflect Z
                new Vector3(-delta.x,  0f, -delta.z), // rotate 180°
                new Vector3( delta.z,  0f,  delta.x), // reflect diagonal
                new Vector3(-delta.z,  0f,  delta.x), // rotate 90° CCW
                new Vector3( delta.z,  0f, -delta.x), // rotate 90° CW
                new Vector3(-delta.z,  0f, -delta.x), // reflect anti-diagonal
            };
            foreach (var r in symmetries)
            {
                Vector3 target = junctionNode.worldPosition + r;
                target.y = 0f;
                target = SnapPosToTileCenter(target);
                int tx = Mathf.RoundToInt(target.x * 10f);
                int tz = Mathf.RoundToInt(target.z * 10f);
                mirrorTargets.Add(new Vector2Int(tx, tz));
            }
        }

        // Also exclude positions that already have an existing exit (don't star what's already built).
        var existingPositions = new HashSet<Vector2Int>();
        foreach (var delta in exitDeltas)
        {
            Vector3 pos = junctionNode.worldPosition + delta;
            pos.y = 0f;
            pos = SnapPosToTileCenter(pos);
            existingPositions.Add(new Vector2Int(Mathf.RoundToInt(pos.x * 10f), Mathf.RoundToInt(pos.z * 10f)));
        }

        // Flag matching candidates.
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].isJoin) continue; // don't override join markers
            Vector3 cPos = candidates[i].position;
            cPos.y = 0f;
            int cx = Mathf.RoundToInt(cPos.x * 10f);
            int cz = Mathf.RoundToInt(cPos.z * 10f);
            var key = new Vector2Int(cx, cz);
            if (mirrorTargets.Contains(key) && !existingPositions.Contains(key))
            {
                var snap = candidates[i];
                snap.isJunctionMirror = true;
                candidates[i] = snap;
            }
        }
    }

    private void GradeArcPoints(SnapResult snap)
    {
        if (terrainGrader == null || snap.arcPoints == null) return;

        float baseY = snap.position.y - terrainGrader.BedRaiseWorldHeight;
        for (int i = 0; i < snap.arcPoints.Length; i++)
        {
            Vector3 gp = snap.arcPoints[i];
            gp.y = baseY;
            snap.arcPoints[i] = gp;
        }
        for (int i = 0; i < snap.arcPoints.Length - 1; i++)
            terrainGrader.GradeAlongSegment(snap.arcPoints[i], snap.arcPoints[i + 1]);
        if (snap.arcPoints.Length > 0)
            terrainGrader.GradeAroundPoint(snap.arcPoints[^1]);
    }

    // ─── Snap: Second Knot (cardinal) ────────────────────────────────────

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

        float dist = Mathf.Max(
            Mathf.Round(Vector3.Dot(offset, bestCardinal)),
            RailConstants.MinSegmentLength);

        Vector3 snappedPos = firstKnot + bestCardinal * dist;
        snappedPos.y = firstKnot.y;
        snappedPos = SnapPosToTileCenter(snappedPos);

        float tangentLen = dist / 3f;
        Vector3 tangent = bestCardinal * tangentLen;

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

    // ─── Occupied Position Tracking ─────────────────────────────────────

    private readonly List<Vector3> occupiedPositions = new();

    private void GatherOccupiedPositions()
    {
        occupiedPositions.Clear();
        if (network == null) return;

        for (int s = 0; s < network.SplineCount; s++)
        {
            if (s == network.ActiveSplineIndex) continue;
            var spline = network.Container.Splines[s];
            for (int k = 0; k < spline.Count; k++)
            {
                Vector3 wp = network.transform.TransformPoint((Vector3)spline[k].Position);
                occupiedPositions.Add(wp);
            }
        }
    }

    private bool IsPositionOccupied(Vector3 pos)
    {
        // Only block positions at graph nodes that have no room for more connections.
        // All other positions (mid-spline knots, empty space) are allowed through
        // for join detection in TagJoinCandidates.
        if (railGraph != null)
        {
            RailNode node = railGraph.FindNodeAtPosition(pos, 0.6f);
            if (node != null && !node.CanAddConnection) return true;
        }
        return false;
    }

    // ─── Spline Proximity Detection ──────────────────────────────────────

    /// <summary>
    /// Builds a list of sampled world positions for all finished splines.
    /// Used by TagJoinCandidates for efficient mid-spline join detection.
    /// </summary>
    private void BuildSplineSamples()
    {
        splineSamples.Clear();
        if (network == null) return;

        for (int s = 0; s < network.SplineCount; s++)
        {
            if (s == network.ActiveSplineIndex) continue;

            var spline = network.Container.Splines[s];

            // Add actual knot positions with approximate tangents.
            for (int k = 0; k < spline.Count; k++)
            {
                Vector3 wp = network.transform.TransformPoint((Vector3)spline[k].Position);
                float kt = (spline.Count > 1) ? (float)k / (spline.Count - 1) : 0f;
                Vector3 tan = ((Vector3)network.Container.EvaluateTangent(s, kt)).normalized;
                if (tan.sqrMagnitude < 0.01f) tan = Vector3.forward;
                splineSamples.Add(new SplineSample
                {
                    worldPos = wp, splineIndex = s, tangentDir = tan, t = kt
                });
            }

            // Estimate arc length and sample at ~2 samples per tile for good coverage.
            Vector3 startPos = network.EvaluatePositionWorld(s, 0f);
            Vector3 endPos = network.EvaluatePositionWorld(s, 1f);
            float approxLen = Vector3.Distance(startPos, endPos);
            int sampleCount = Mathf.Max((int)(approxLen * 2f), 20);

            for (int si = 1; si < sampleCount; si++)
            {
                float t = (float)si / sampleCount;
                Vector3 sp = network.EvaluatePositionWorld(s, t);
                Vector3 tan = ((Vector3)network.Container.EvaluateTangent(s, t)).normalized;
                if (tan.sqrMagnitude < 0.01f) tan = Vector3.forward;
                splineSamples.Add(new SplineSample
                {
                    worldPos = sp, splineIndex = s, tangentDir = tan, t = t
                });
            }
        }
    }

    /// <summary>
    /// Returns the spline index of an existing finished spline that passes
    /// near the given world position, or -1 if none found.
    /// </summary>
    private int FindSplineNearPosition(Vector3 worldPos, float tolerance)
    {
        if (network == null) return -1;

        float tolSq = tolerance * tolerance;

        for (int s = 0; s < network.SplineCount; s++)
        {
            if (s == network.ActiveSplineIndex) continue;

            var spline = network.Container.Splines[s];

            // Check knot positions.
            for (int k = 0; k < spline.Count; k++)
            {
                Vector3 kw = network.transform.TransformPoint((Vector3)spline[k].Position);
                Vector3 diff = kw - worldPos;
                diff.y = 0f;
                if (diff.sqrMagnitude < tolSq) return s;
            }

            // Sample spline curve between knots.
            int samples = Mathf.Max(spline.Count * 10, 20);
            for (int si = 1; si < samples; si++)
            {
                float t = (float)si / samples;
                Vector3 sp = network.EvaluatePositionWorld(s, t);
                Vector3 diff = sp - worldPos;
                diff.y = 0f;
                if (diff.sqrMagnitude < tolSq) return s;
            }
        }

        return -1;
    }

    // ─── Candidate Computation ─────────────────────────────────────────

    /// <summary>
    /// Builds candidates in all 4 cardinal directions (for the second knot,
    /// when no direction has been established yet).
    /// </summary>
    private void BuildAllDirectionCandidates(Vector3 lastKnot, Vector3 cursorWorldPos)
    {
        candidates.Clear();
        GatherOccupiedPositions();

        // Build straight + curve candidates for each cardinal direction.
        foreach (Vector3 cardinal in RailConstants.Cardinals)
        {
            BuildCandidatesForDirection(lastKnot, cardinal, cursorWorldPos, skipFlip: true);
        }
    }

    private void BuildCandidates(Vector3 lastKnot, Vector3 forward, Vector3 cursorWorldPos)
    {
        candidates.Clear();

        // Gather all occupied positions (existing knot world positions) for filtering.
        GatherOccupiedPositions();

        BuildCandidatesForDirection(lastKnot, forward, cursorWorldPos);

        // If branching from a mid-curve knot, also build candidates for the
        // opposite tangent direction so both sides are available.
        if (midKnotAltDirection.sqrMagnitude > 0.01f)
        {
            BuildCandidatesForDirection(lastKnot, midKnotAltDirection, cursorWorldPos);
        }
    }

    private void BuildCandidatesForDirection(Vector3 lastKnot, Vector3 forward, Vector3 cursorWorldPos, int maxStraight = -1, bool skipFlip = false)
    {
        // Check if the straight direction is completely blocked (first straight
        // candidate lands on an occupied position). If so, flip to opposite cardinal.
        if (!skipFlip)
        {
            Vector3 testPos = SnapPosToTileCenter(lastKnot + forward * 1f);
            testPos.y = lastKnot.y;
            if (IsPositionOccupied(testPos))
                forward = -forward;
        }

        // Determine cardinal group from the (possibly flipped) forward direction.
        int group = ComputeCardinalGroup(forward);

        // Skip this direction entirely if the start node already has 3 exits in this group.
        if (railGraph != null)
        {
            RailNode startNode = railGraph.FindNodeAtPosition(lastKnot, 0.6f);
            if (startNode != null && startNode.GetGroupExitCount(group) >= 3)
                return;
        }

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        int viewR = RailConstants.CandidateViewRadius;

        // 1. Straight candidates — only generate near the cursor.
        float cursorForwardProj = Vector3.Dot(cursorWorldPos - lastKnot, forward);
        int sMin = Mathf.Max(1, Mathf.FloorToInt(cursorForwardProj) - viewR);
        int sMax = maxStraight > 0
            ? Mathf.Min(maxStraight, Mathf.CeilToInt(cursorForwardProj) + viewR)
            : Mathf.CeilToInt(cursorForwardProj) + viewR;
        for (int tileCount = sMin; tileCount <= sMax; tileCount++)
        {
            float dist = tileCount;
            Vector3 pos = lastKnot + forward * dist;
            pos.y = lastKnot.y;
            pos = SnapPosToTileCenter(pos);

            float actualDist = Vector3.Distance(
                new Vector3(lastKnot.x, 0, lastKnot.z),
                new Vector3(pos.x, 0, pos.z));
            if (actualDist < RailConstants.MinSegmentLength) continue;
            if (IsPositionOccupied(pos)) continue;

            float tLen = actualDist / 3f;
            Vector3 tan = forward * tLen;

            int samples = Mathf.Max(2, Mathf.CeilToInt(actualDist) + 1);
            Vector3[] pts = new Vector3[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / (samples - 1);
                pts[i] = Vector3.Lerp(lastKnot, pos, t);
            }

            candidates.Add(new SnapResult
            {
                position = pos,
                exitDirection = forward,
                tangentOut = tan,
                tangentIn = -tan,
                arcPoints = pts,
                isValid = true,
                cardinalGroup = group
            });
        }

        // 2. Curve candidates: for each reachable tile center, compute the unique
        //    pure-arc radius R = (f² + l²) / (2|l|) that makes the turning circle
        //    pass exactly through both S and T.  No straight tail needed.
        float minR = minTurnRadius;
        float maxArcRad = RailConstants.MaxArcAngleDeg * Mathf.Deg2Rad;

        // Iterate only tiles within CandidateViewRadius of the cursor.
        Vector3 cursorOff = cursorWorldPos - lastKnot;
        int cx = Mathf.RoundToInt(cursorOff.x);
        int cz = Mathf.RoundToInt(cursorOff.z);
        for (int dx = cx - viewR; dx <= cx + viewR; dx++)
        for (int dz = cz - viewR; dz <= cz + viewR; dz++)
        {
            if (dx == 0 && dz == 0) continue;

            Vector3 tilePos = SnapPosToTileCenter(lastKnot + new Vector3(dx, 0, dz));
            tilePos.y = lastKnot.y;

            Vector3 toTile = tilePos - lastKnot;
            toTile.y = 0f;
            if (toTile.sqrMagnitude < RailConstants.MinSegmentLength * RailConstants.MinSegmentLength)
                continue;

            // Decompose offset into forward / lateral components.
            float f = Vector3.Dot(toTile, forward);
            float l = Vector3.Dot(toTile, right);

            if (f < 0.1f) continue;            // behind us
            float absL = Mathf.Abs(l);
            if (absL < 0.5f) continue;          // near-straight — covered above

            if (IsPositionOccupied(tilePos)) continue;

            // Unique radius for a circle through S heading forward that also hits T.
            float R = (f * f + l * l) / (2f * absL);
            if (R < minR) continue;             // tighter than minimum turn

            // Turn direction (+1 = right, –1 = left).
            float sign = l > 0f ? 1f : -1f;

            // Circle centre.
            Vector3 center = lastKnot + right * sign * R;

            // Vectors from centre to start / target.
            Vector3 CS = lastKnot - center;  CS.y = 0f;
            Vector3 CT = tilePos  - center;  CT.y = 0f;

            // Arc sweep (signed).
            float angleCS = Mathf.Atan2(CS.x, CS.z);
            float angleCT = Mathf.Atan2(CT.x, CT.z);
            float arcSweep = angleCT - angleCS;

            if (sign > 0f)
            {
                while (arcSweep < 0f)            arcSweep += Mathf.PI * 2f;
                while (arcSweep > Mathf.PI * 2f) arcSweep -= Mathf.PI * 2f;
            }
            else
            {
                while (arcSweep > 0f)             arcSweep -= Mathf.PI * 2f;
                while (arcSweep < -Mathf.PI * 2f) arcSweep += Mathf.PI * 2f;
            }

            float absArc = Mathf.Abs(arcSweep);
            if (absArc < 0.03f || absArc > maxArcRad) continue;

            // Duplicate check.
            bool duplicate = false;
            for (int ci = 0; ci < candidates.Count; ci++)
            {
                if (Vector3.Distance(candidates[ci].position, tilePos) < 0.1f)
                { duplicate = true; break; }
            }
            if (duplicate) continue;

            // Exit direction: forward rotated by the arc sweep.
            float arcDeg = absArc * Mathf.Rad2Deg;
            Quaternion exitRot = Quaternion.AngleAxis(sign * arcDeg, Vector3.up);
            Vector3 exitDir = (exitRot * forward).normalized;

            // Sample the arc for the preview line.
            int arcSamples = Mathf.Max(4,
                Mathf.CeilToInt(absArc / (Mathf.PI * 0.5f) * previewArcSamples));
            var points = new Vector3[arcSamples];
            for (int i = 0; i < arcSamples; i++)
            {
                float t = (float)i / Mathf.Max(1, arcSamples - 1);
                float a = angleCS + arcSweep * t;
                points[i] = center + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * R;
                points[i].y = lastKnot.y;
            }
            points[arcSamples - 1] = tilePos; // exact endpoint

            // Cubic-Bézier tangent handle length for a circular arc.
            float bezierD = (4f / 3f) * Mathf.Tan(absArc / 4f) * R;

            candidates.Add(new SnapResult
            {
                position      = tilePos,
                exitDirection  = exitDir,
                tangentOut     = forward * bezierD,
                tangentIn      = -exitDir * bezierD,
                arcPoints      = points,
                isValid        = true,
                cardinalGroup  = group,
                isParallelReturn = false,
                isStraightenReturn = false,
                isJunctionMirror = false
            });
        }
    }

    // ─── Join Detection ─────────────────────────────────────────────────

    /// <summary>
    /// After BuildCandidates(), tag any candidate whose endpoint coincides
    /// with an existing graph node or existing spline so we can visualise it
    /// as a join and auto-finish the spline when the player clicks it.
    /// </summary>
    private void TagJoinCandidates(Vector3 lastKnot = default, Vector3 forward = default)
    {
        bool hasDirection = forward.sqrMagnitude > 0.5f;

        for (int i = 0; i < candidates.Count; i++)
        {
            SnapResult c = candidates[i];

            // ── Check existing graph nodes ──
            if (railGraph != null && railGraph.Nodes.Count > 0)
            {
                RailNode node = railGraph.FindNodeAtPosition(c.position, 0.6f);

                // Don't join back to the start of the current drawing unless
                // the track is long enough to form a real loop (4+ knots).
                if (node != null
                    && Vector3.Distance(node.worldPosition, drawingStartPos) < 0.5f
                    && knotCount < 4)
                {
                    node = null;
                }

                if (node != null && node.CanAddConnection)
                {
                    // Before tagging as a join, check the candidate's path doesn't
                    // run on top of an existing spline (overlapping/collinear track).
                    if (IsPathOverlappingExistingSpline(c))
                    {
                        c.isValid = false;
                        candidates[i] = c;
                        continue;
                    }

                    // Block joins to any node that's within 7 tiles of an
                    // existing junction — unless the node itself IS a junction.
                    if (!node.IsJunction && IsNearExistingJunction(node, 7f))
                    {
                        continue; // skip — don't tag as join
                    }

                    c.isJoin    = true;
                    c.joinNode  = node;
                    c.position  = node.worldPosition; // snap to exact node position
                    candidates[i] = c;

                    // Add ±8 parallel completion candidates along the existing rail direction.
                    if (hasDirection)
                        AddParallelCompletionCandidates(lastKnot, forward, node);

                    continue;
                }
            }

            // ── Check mid-spline joins (knots and curve proximity) ──
            float tolSq = 0.8f * 0.8f;
            for (int j = 0; j < splineSamples.Count; j++)
            {
                Vector3 diff = c.position - splineSamples[j].worldPos;
                diff.y = 0f;
                if (diff.sqrMagnitude < tolSq)
                {
                    // Don't join back onto yourself: skip if the candidate is
                    // very close to the drawing start AND the track is short.
                    if (Vector3.Distance(c.position, drawingStartPos) < 1.1f && knotCount < 4)
                        break;

                    // Reject if the candidate's path runs on top of the existing spline.
                    if (IsPathOverlappingExistingSpline(c))
                    {
                        c.isValid = false;
                        candidates[i] = c;
                        break;
                    }

                    c.isJoin = true;
                    c.isMidSplineJoin = true;
                    c.joinSplineIndex = splineSamples[j].splineIndex;
                    candidates[i] = c;
                    break;
                }
            }
        }

        // Remove invalidated candidates.
        candidates.RemoveAll(c => !c.isValid);
    }

    // ─── Filter Candidates Near Existing Track ──────────────────────────

    /// <summary>
    /// Removes regular (non-join) candidates whose positions land on or near
    /// existing finished splines.  Join-arm candidates handle those connections
    /// instead, so regular dots should not overlap existing track.
    /// </summary>
    private void RemoveCandidatesOnExistingTrack()
    {
        float tolSq = 0.9f * 0.9f;
        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            if (candidates[i].isJoin) continue; // keep existing joins

            for (int j = 0; j < splineSamples.Count; j++)
            {
                Vector3 diff = candidates[i].position - splineSamples[j].worldPos;
                diff.y = 0f;
                if (diff.sqrMagnitude < tolSq)
                {
                    candidates.RemoveAt(i);
                    break;
                }
            }
        }
    }

    // ─── Crossover Prevention (exact Bézier math) ────────────────────────

    /// <summary>
    /// Extracts cubic Bézier segments (in world-space XZ) for a finished spline.
    /// Each pair of consecutive knots defines one segment.
    /// </summary>
    private List<BezierSegXZ> ExtractSplineBezierSegments(int splineIndex)
    {
        var spline = network.Container.Splines[splineIndex];
        var segs = new List<BezierSegXZ>(spline.Count - 1);
        var xform = network.transform;

        for (int k = 0; k < spline.Count - 1; k++)
        {
            var k0 = spline[k];
            var k1 = spline[k + 1];

            Vector3 p0w = xform.TransformPoint((Vector3)k0.Position);
            Vector3 p1w = xform.TransformPoint((Vector3)(k0.Position + k0.TangentOut));
            Vector3 p2w = xform.TransformPoint((Vector3)(k1.Position + k1.TangentIn));
            Vector3 p3w = xform.TransformPoint((Vector3)k1.Position);

            var seg = new BezierSegXZ
            {
                P0x = p0w.x, P0z = p0w.z,
                P1x = p1w.x, P1z = p1w.z,
                P2x = p2w.x, P2z = p2w.z,
                P3x = p3w.x, P3z = p3w.z
            };

            // AABB from convex hull of control points.
            seg.minX = Mathf.Min(Mathf.Min(seg.P0x, seg.P1x), Mathf.Min(seg.P2x, seg.P3x));
            seg.maxX = Mathf.Max(Mathf.Max(seg.P0x, seg.P1x), Mathf.Max(seg.P2x, seg.P3x));
            seg.minZ = Mathf.Min(Mathf.Min(seg.P0z, seg.P1z), Mathf.Min(seg.P2z, seg.P3z));
            seg.maxZ = Mathf.Max(Mathf.Max(seg.P0z, seg.P1z), Mathf.Max(seg.P2z, seg.P3z));
            segs.Add(seg);
        }
        return segs;
    }

    /// <summary>
    /// Finds real roots of a₃t³ + a₂t² + a₁t + a₀ = 0 in [0, 1].
    /// Uses sign-change detection + bisection for robustness.
    /// Returns count (0–3) and fills roots array.
    /// </summary>
    private static int FindCubicRootsIn01(
        float a3, float a2, float a1, float a0,
        out float r0, out float r1, out float r2)
    {
        r0 = r1 = r2 = 0f;

        // Degenerate: linear.
        if (Mathf.Abs(a3) < 1e-7f && Mathf.Abs(a2) < 1e-7f)
        {
            if (Mathf.Abs(a1) < 1e-7f) return 0;
            float root = -a0 / a1;
            if (root >= 0f && root <= 1f) { r0 = root; return 1; }
            return 0;
        }

        // Degenerate: quadratic.
        if (Mathf.Abs(a3) < 1e-7f)
        {
            float disc = a1 * a1 - 4f * a2 * a0;
            if (disc < 0f) return 0;
            float sqrtD = Mathf.Sqrt(disc);
            int cnt = 0;
            float ra = (-a1 + sqrtD) / (2f * a2);
            float rb = (-a1 - sqrtD) / (2f * a2);
            if (ra >= 0f && ra <= 1f) { r0 = ra; cnt++; }
            if (rb >= 0f && rb <= 1f && Mathf.Abs(rb - ra) > 1e-6f)
            {
                if (cnt == 0) r0 = rb; else r1 = rb;
                cnt++;
            }
            return cnt;
        }

        // Full cubic: sample at N points, find sign changes, bisect each.
        const int N = 20;
        int count = 0;
        float prev = ((a3 * 0f + a2) * 0f + a1) * 0f + a0; // f(0)

        for (int i = 1; i <= N; i++)
        {
            float t = (float)i / N;
            float val = ((a3 * t + a2) * t + a1) * t + a0;

            if (prev * val < 0f) // sign change
            {
                // Bisect to find the root.
                float lo = (float)(i - 1) / N;
                float hi = t;
                for (int j = 0; j < 24; j++) // 24 iterations ≈ 6e-8 precision
                {
                    float mid = (lo + hi) * 0.5f;
                    float fm = ((a3 * mid + a2) * mid + a1) * mid + a0;
                    if (fm * prev < 0f) hi = mid;
                    else { lo = mid; prev = fm; }
                }
                float root = (lo + hi) * 0.5f;
                if (count == 0) r0 = root;
                else if (count == 1) r1 = root;
                else r2 = root;
                count++;
                if (count >= 3) break;
            }
            prev = val;
        }

        // Also check exact endpoints for zero (within tolerance).
        float f0 = a0;
        float f1 = a3 + a2 + a1 + a0;
        if (Mathf.Abs(f0) < 1e-6f && (count == 0 || Mathf.Abs(r0) > 1e-4f))
        {
            if (count == 0) r0 = 0f; else if (count == 1) r1 = 0f; else r2 = 0f;
            count++;
        }
        if (count < 3 && Mathf.Abs(f1) < 1e-6f && (count == 0 || Mathf.Abs(r0 - 1f) > 1e-4f))
        {
            if (count == 0) r0 = 1f; else if (count == 1) r1 = 1f; else r2 = 1f;
            count++;
        }

        return count;
    }

    /// <summary>
    /// Tests whether the line segment A→B (in XZ) crosses the cubic Bézier
    /// segment described by <paramref name="seg"/>.
    /// If a crossing is found, outputs the crossing point in XZ.
    /// </summary>
    private static bool DoesLineCrossBezierXZ(
        float ax, float az, float bx, float bz,
        in BezierSegXZ seg,
        out float crossX, out float crossZ)
    {
        crossX = crossZ = 0f;

        // AABB early-out: expand both boxes slightly and check overlap.
        float lMinX = Mathf.Min(ax, bx), lMaxX = Mathf.Max(ax, bx);
        float lMinZ = Mathf.Min(az, bz), lMaxZ = Mathf.Max(az, bz);
        if (lMaxX < seg.minX - 0.1f || lMinX > seg.maxX + 0.1f) return false;
        if (lMaxZ < seg.minZ - 0.1f || lMinZ > seg.maxZ + 0.1f) return false;

        // Line normal in XZ: n = (-(bz-az), (bx-ax)).
        float nx = -(bz - az);
        float nz = bx - ax;

        // Cubic Bézier coefficients in the standard power basis:
        //   B(t) = (1-t)³P0 + 3(1-t)²tP1 + 3(1-t)t²P2 + t³P3
        //        = P0 + 3(P1-P0)t + 3(P0-2P1+P2)t² + (-P0+3P1-3P2+P3)t³
        // Signed distance from line through A: f(t) = n·(B(t) - A)
        float d0x = seg.P0x - ax, d0z = seg.P0z - az;
        float d1x = seg.P1x - seg.P0x, d1z = seg.P1z - seg.P0z;
        float d2x = seg.P0x - 2f * seg.P1x + seg.P2x, d2z = seg.P0z - 2f * seg.P1z + seg.P2z;
        float d3x = -seg.P0x + 3f * seg.P1x - 3f * seg.P2x + seg.P3x;
        float d3z = -seg.P0z + 3f * seg.P1z - 3f * seg.P2z + seg.P3z;

        float c0 = nx * d0x + nz * d0z;
        float c1 = 3f * (nx * d1x + nz * d1z);
        float c2 = 3f * (nx * d2x + nz * d2z);
        float c3 = nx * d3x + nz * d3z;

        int rootCount = FindCubicRootsIn01(c3, c2, c1, c0, out float t0, out float t1, out float t2);
        if (rootCount == 0) return false;

        // For each root, evaluate B(t) and check it falls on the line segment interior.
        float abx = bx - ax, abz = bz - az;
        float abLenSq = abx * abx + abz * abz;
        if (abLenSq < 1e-10f) return false;

        for (int ri = 0; ri < rootCount; ri++)
        {
            float t = ri == 0 ? t0 : (ri == 1 ? t1 : t2);
            if (t < 0f || t > 1f) continue;

            // Evaluate Bézier at t.
            float u = 1f - t;
            float u2 = u * u, t2v = t * t;
            float u3 = u2 * u, t3 = t2v * t;
            float bxt = u3 * seg.P0x + 3f * u2 * t * seg.P1x + 3f * u * t2v * seg.P2x + t3 * seg.P3x;
            float bzt = u3 * seg.P0z + 3f * u2 * t * seg.P1z + 3f * u * t2v * seg.P2z + t3 * seg.P3z;

            // Project B(t) onto line segment A→B: s = dot(B(t)-A, AB) / |AB|²
            // Inclusive bounds — adjacent arc segments share vertices, so a
            // crossing at a vertex must be caught by at least one segment.
            // The safe-radius check in RemoveCrossingCandidates handles
            // false positives near path start/end.
            float s = ((bxt - ax) * abx + (bzt - az) * abz) / abLenSq;
            if (s >= -0.01f && s <= 1.01f)
            {
                crossX = bxt;
                crossZ = bzt;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Removes candidates whose arc path crosses over any existing spline.
    /// Uses exact line-vs-Bézier intersection (no polyline sampling).
    /// </summary>
    private void RemoveCrossingCandidates()
    {
        if (network == null) return;

        // Extract Bézier segments for all finished splines.
        var allSegs = new List<List<BezierSegXZ>>();
        for (int s = 0; s < network.SplineCount; s++)
        {
            if (s == network.ActiveSplineIndex) continue;
            var spline = network.Container.Splines[s];
            if (spline.Count < 2) continue;
            allSegs.Add(ExtractSplineBezierSegments(s));
        }
        if (allSegs.Count == 0) return;

        float safeSq = 1.2f * 1.2f;

        for (int ci = candidates.Count - 1; ci >= 0; ci--)
        {
            var c = candidates[ci];
            if (c.arcPoints == null || c.arcPoints.Length < 2) continue;

            Vector3 pathStart = c.arcPoints[0];
            Vector3 pathEnd = c.arcPoints[c.arcPoints.Length - 1];
            bool crossed = false;

            for (int pi = 1; pi < c.arcPoints.Length && !crossed; pi++)
            {
                float ax = c.arcPoints[pi - 1].x, az = c.arcPoints[pi - 1].z;
                float bx = c.arcPoints[pi].x, bz = c.arcPoints[pi].z;

                foreach (var segList in allSegs)
                {
                    foreach (var seg in segList)
                    {
                        if (DoesLineCrossBezierXZ(ax, az, bx, bz, in seg,
                                out float cx, out float cz))
                        {
                            // Ignore crossings near path start (on existing track).
                            float dsX = cx - pathStart.x, dsZ = cz - pathStart.z;
                            if (dsX * dsX + dsZ * dsZ < safeSq) continue;

                            // Ignore crossings near path end (join target).
                            float deX = cx - pathEnd.x, deZ = cz - pathEnd.z;
                            if (deX * deX + deZ * deZ < safeSq) continue;

                            crossed = true;
                            break;
                        }
                    }
                    if (crossed) break;
                }
            }

            if (crossed)
                candidates.RemoveAt(ci);
        }
    }

    /// <summary>
    /// Removes non-join candidates whose position is within 8 tiles of any
    /// existing spline.  Ensures at least 7 tiles of clearance between
    /// independent rail lines.  Join candidates are exempt (they connect to
    /// existing track by design).
    /// </summary>
    private void RemoveProximityCandidates()
    {
        if (splineSamples.Count == 0) return;

        float minDistSq = 8f * 8f;

        for (int ci = candidates.Count - 1; ci >= 0; ci--)
        {
            var c = candidates[ci];
            if (c.isJoin) continue;

            for (int si = 0; si < splineSamples.Count; si++)
            {
                Vector3 diff = c.position - splineSamples[si].worldPos;
                diff.y = 0f;
                if (diff.sqrMagnitude < minDistSq)
                {
                    candidates.RemoveAt(ci);
                    break;
                }
            }
        }
    }

    // ─── Join Arm Candidates ────────────────────────────────────────────

    /// <summary>
    /// Generates join-arm candidates: smooth Bézier curves from the drawing's
    /// last knot to nearby existing track.  Each arm exits existing track
    /// <b>tangentially</b> (like a real junction switch) and curves to meet the
    /// drawing's endpoint heading in the drawing direction.
    /// </summary>
    private void BuildJoinArmCandidates(Vector3 lastKnot, Vector3 forward, Vector3 cursorWorldPos)
    {
        if (forward.sqrMagnitude < 0.5f) return;

        float viewR = RailConstants.CandidateViewRadius + 3; // slightly wider than normal candidates
        float viewRSq = viewR * viewR;
        float joinDedupSq = 0.9f * 0.9f; // one join arm per tile center

        // Track positions of accepted join arms for dedup (not against regular candidates).
        var joinPositions = new List<Vector3>();

        // Iterate spline samples (includes knot positions near nodes + mid-spline points).
        for (int i = 0; i < splineSamples.Count; i++)
        {
            var sample = splineSamples[i];

            // Show arms near the CURSOR, not near the drawing endpoint.
            Vector3 toCursor = sample.worldPos - cursorWorldPos;
            toCursor.y = 0f;
            if (toCursor.sqrMagnitude > viewRSq) continue;

            // Snap to tile grid so join dots align with regular candidates.
            Vector3 armPos = SnapPosToTileCenter(sample.worldPos);
            armPos.y = lastKnot.y;

            // Still need minimum distance from the drawing endpoint.
            Vector3 diff = armPos - lastKnot;
            diff.y = 0f;
            if (diff.sqrMagnitude < 1f) continue;

            // Don't loop back to drawing start on short tracks.
            if (Vector3.Distance(armPos, drawingStartPos) < 1.1f && knotCount < 4)
                continue;

            // Check for a graph node at this sample position.
            RailNode nodeAtSample = railGraph?.FindNodeAtPosition(armPos, 0.6f);
            if (nodeAtSample != null && !nodeAtSample.CanAddConnection) continue;
            if (nodeAtSample != null && !nodeAtSample.IsJunction && IsNearExistingJunction(nodeAtSample, 7f))
                continue;

            // Dedup against other join arms only (not regular candidates).
            bool tooClose = false;
            for (int jp = 0; jp < joinPositions.Count; jp++)
            {
                Vector3 d = armPos - joinPositions[jp];
                d.y = 0f;
                if (d.sqrMagnitude < joinDedupSq) { tooClose = true; break; }
            }
            if (tooClose) continue;

            // Try both ±tangent directions along the existing track; keep the best.
            SnapResult? bestArm = null;
            float bestAlignment = -2f;
            for (int dir = 0; dir < 2; dir++)
            {
                Vector3 armExitDir = (dir == 0) ? sample.tangentDir : -sample.tangentDir;
                SnapResult? arm = TryBuildJoinArmArc(lastKnot, forward, armPos, armExitDir, minTurnRadius);
                if (!arm.HasValue) continue;
                if (IsPathOverlappingExistingSpline(arm.Value)) continue;

                float al = Vector3.Dot(arm.Value.tangentOut.normalized, forward);
                if (al > bestAlignment) { bestAlignment = al; bestArm = arm; }
            }

            if (bestArm == null) continue;

            joinPositions.Add(armPos);

            SnapResult result = bestArm.Value;
            result.isJoin = true;
            if (nodeAtSample != null)
            {
                result.joinNode = nodeAtSample;
                result.position = nodeAtSample.worldPosition;
                candidates.Add(result);
                AddParallelCompletionCandidates(lastKnot, forward, nodeAtSample);
            }
            else
            {
                result.isMidSplineJoin = true;
                result.joinSplineIndex = sample.splineIndex;
                candidates.Add(result);
            }
        }
    }

    /// <summary>
    /// Builds a cubic-Bézier join arm from <paramref name="lastKnot"/> to
    /// <paramref name="armOrigin"/> (a point on existing track).
    /// <list type="bullet">
    ///   <item>At <c>lastKnot</c>: the curve departs in <paramref name="drawingDir"/> (continues the drawing).</item>
    ///   <item>At <c>armOrigin</c>: the curve arrives tangent to <paramref name="armExitDir"/> (smooth junction).</item>
    /// </list>
    /// Returns null when the geometry is invalid (behind the drawing, too tight, etc.).
    /// </summary>
    private SnapResult? TryBuildJoinArmArc(
        Vector3 lastKnot, Vector3 drawingDir,
        Vector3 armOrigin, Vector3 armExitDir, float minR)
    {
        Vector3 toArm = armOrigin - lastKnot;
        toArm.y = 0f;
        float dist = toArm.magnitude;
        if (dist < RailConstants.MinSegmentLength) return null;

        // Arm origin must be in the forward hemisphere of the drawing.
        if (Vector3.Dot(toArm.normalized, drawingDir) < 0f) return null;

        // armExitDir should generally face toward lastKnot so the arm
        // exits the track on the correct side.
        float armDot = Vector3.Dot((lastKnot - armOrigin).normalized, armExitDir);
        if (armDot < -0.2f) return null;

        int group = ComputeCardinalGroup(drawingDir);
        float D = dist / 3f;

        // Bézier tangent at lastKnot: continue the drawing direction.
        Vector3 tOut = drawingDir * D;

        // Bézier tangent at armOrigin: arrive tangent to existing track.
        Vector3 tIn = armExitDir * D;

        // exitDirection convention: in FinishSpline, endExit = -exitDirection.
        Vector3 exitDir = -armExitDir;

        // Sample the cubic Bézier for preview.
        Vector3 P0 = lastKnot;
        Vector3 P1 = P0 + tOut;
        Vector3 P2 = armOrigin + tIn;
        Vector3 P3 = armOrigin;

        int sampleCount = Mathf.Max(12, Mathf.CeilToInt(dist) * 3);
        var points = new Vector3[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / (sampleCount - 1);
            float u = 1f - t;
            points[i] = u * u * u * P0 + 3f * u * u * t * P1 + 3f * u * t * t * P2 + t * t * t * P3;
            points[i].y = lastKnot.y;
        }
        points[0] = lastKnot;
        points[sampleCount - 1] = armOrigin;

        // ── Turn-radius check: compute minimum radius of curvature ──
        // Walk the sampled polyline and measure the radius at each interior
        // point via the circumradius of three consecutive samples.
        for (int i = 1; i < sampleCount - 1; i++)
        {
            Vector3 a = points[i - 1]; a.y = 0f;
            Vector3 b = points[i];     b.y = 0f;
            Vector3 c = points[i + 1]; c.y = 0f;
            Vector3 ab = b - a;
            Vector3 bc = c - b;
            float cross = Mathf.Abs(ab.x * bc.z - ab.z * bc.x);
            if (cross < 1e-6f) continue; // nearly straight segment
            float abLen = ab.magnitude;
            float bcLen = bc.magnitude;
            float acLen = (c - a).magnitude;
            float radius = (abLen * bcLen * acLen) / (2f * cross);
            if (radius < minR) return null;
        }

        // Reject curves that fold back on themselves (excessive length).
        float curveLen = 0f;
        for (int i = 1; i < sampleCount; i++)
            curveLen += Vector3.Distance(points[i - 1], points[i]);
        if (curveLen > dist * 3f) return null;

        return new SnapResult
        {
            position = armOrigin,
            exitDirection = exitDir,
            tangentOut = tOut,
            tangentIn = tIn,
            arcPoints = points,
            isValid = true,
            cardinalGroup = group
        };
    }

    /// <summary>
    /// Returns true if any existing candidate is within <paramref name="tolerance"/>
    /// of the given position (XZ distance).
    /// </summary>
    private bool IsDuplicatePosition(Vector3 pos, float tolerance)
    {
        float tolSq = tolerance * tolerance;
        for (int i = 0; i < candidates.Count; i++)
        {
            Vector3 d = candidates[i].position - pos;
            d.y = 0f;
            if (d.sqrMagnitude < tolSq) return true;
        }
        return false;
    }

    /// <summary>
    /// Returns true if any existing junction node (3+ connections) is within
    /// the specified distance of the given node (excluding the node itself).
    /// Used to enforce minimum spacing between junctions by preventing
    /// forking/joining at nodes near existing junctions.
    /// </summary>
    private bool IsNearExistingJunction(RailNode node, float minDist)
    {
        if (railGraph == null) return false;
        float minDistSq = minDist * minDist;
        foreach (var other in railGraph.Nodes)
        {
            if (other == node) continue;
            if (!other.IsJunction) continue;
            Vector3 diff = other.worldPosition - node.worldPosition;
            diff.y = 0f;
            if (diff.sqrMagnitude < minDistSq) return true;
        }
        return false;
    }

    /// <summary>
    /// Returns true if the majority of the candidate's arc sample points lie on top
    /// of an existing spline. This detects collinear/overlapping track proposals.
    /// </summary>
    private bool IsPathOverlappingExistingSpline(SnapResult c)
    {
        if (c.arcPoints == null || c.arcPoints.Length < 2) return false;

        float tolSq = 0.9f * 0.9f;
        // Sample a few interior points (skip first/last which are at node positions).
        int overlapCount = 0;
        int testCount = 0;
        int step = Mathf.Max(1, c.arcPoints.Length / 5);
        for (int p = 1; p < c.arcPoints.Length - 1; p += step)
        {
            testCount++;
            for (int j = 0; j < splineSamples.Count; j++)
            {
                Vector3 diff = c.arcPoints[p] - splineSamples[j].worldPos;
                diff.y = 0f;
                if (diff.sqrMagnitude < tolSq)
                {
                    overlapCount++;
                    break;
                }
            }
        }

        // If most interior samples overlap existing splines, it's collinear.
        return testCount > 0 && overlapCount >= Mathf.CeilToInt(testCount * 0.5f);
    }

    // ─── Parallel Completion Suggestions ────────────────────────────────

    private const float CompletionParallelOffset = 8f;

    /// <summary>
    /// Adds two completion candidates at ±8 tiles perpendicular to the approach
    /// direction from a join node, enabling parallel track placement.
    /// </summary>
    private void AddParallelCompletionCandidates(Vector3 lastKnot, Vector3 forward, RailNode joinNode)
    {
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        float radius = PickCompletionRadius(joinNode, lastKnot, forward);

        Vector3 target1 = joinNode.worldPosition + right * CompletionParallelOffset;
        Vector3 target2 = joinNode.worldPosition - right * CompletionParallelOffset;

        TryBuildCompletionPath(lastKnot, forward, right, target1, radius, null);
        TryBuildCompletionPath(lastKnot, forward, right, target2, radius, null);
    }

    /// <summary>
    /// Determines the best turn radius for a completion path by checking what radii
    /// are used in the segments connected to the target node and the current drawing.
    /// Falls back to smaller radii if the most-used one doesn't fit.
    /// </summary>
    private float PickCompletionRadius(RailNode targetNode, Vector3 startPos, Vector3 forward)
    {
        Vector3 toTarget = targetNode.worldPosition - startPos;
        toTarget.y = 0f;
        float lateralDist = Mathf.Abs(Vector3.Dot(toTarget, Vector3.Cross(Vector3.up, forward).normalized));

        // Use minTurnRadius if it fits; otherwise clamp to lateral distance.
        return Mathf.Max(minTurnRadius, lateralDist * 0.5f);
    }

    /// <summary>
    /// Attempts to build a straight → 90° curve → straight completion path from
    /// startPos/forward to target. Adds to candidates list if geometry works out.
    /// </summary>
    private void TryBuildCompletionPath(Vector3 startPos, Vector3 forward, Vector3 right,
        Vector3 target, float radius, RailNode joinNode)
    {
        Vector3 toTarget = target - startPos;
        toTarget.y = 0f;

        // Determine which side the target is on.
        float lateralOffset = Vector3.Dot(toTarget, right);
        float forwardOffset = Vector3.Dot(toTarget, forward);
        if (forwardOffset < 1f) return; // target is behind us

        float sign = lateralOffset >= 0f ? 1f : -1f;
        float absLateral = Mathf.Abs(lateralOffset);

        // For a 90° turn: we need 'radius' of lateral space for the curve.
        if (absLateral < radius * 0.5f) return; // too close laterally

        // Path layout:
        //   straightLen forward → 90° curve (radius) → remainingLen perpendicular
        // The curve center is at startPos + forward*straightLen + right*sign*radius.
        // After 90° turn, we exit going in the 'right*sign' direction.
        // The curve endpoint is center + forward*(-1)*radius... let me compute properly.
        //
        // Curve center = turnStart + right*sign*radius
        // turnStart = startPos + forward * straightLen
        // Curve endpoint = center + forward * 0 * ... 
        // After 90° turn from forward around Y by sign*90°:
        //   exit direction = right * sign (perpendicular)
        //   exit position = center + (-right*sign)*radius rotated by sign*90° around Y
        //     = center - forward * radius (no, let me think again)
        //
        // Center of the arc: turnStart + right*sign*radius
        // Start of arc: turnStart (= center - right*sign*radius)
        // After 90° rotation:
        //   endpoint = center + Quaternion.AngleAxis(sign*90, up) * (turnStart - center)
        //            = center + Quaternion.AngleAxis(sign*90, up) * (-right*sign*radius)
        // Quaternion.AngleAxis(90, up) * (-right) = forward (since right = cross(up, forward))
        // Quaternion.AngleAxis(-90, up) * (right) = forward
        // So: sign=+1: rot(90, up) * (-right * radius) = forward * radius → endpoint = center + forward*radius
        //     sign=-1: rot(-90, up) * (right * radius) = forward * radius → endpoint = center + forward*radius
        // Wait that doesn't seem right. Let me use explicit computation.

        // The turn starts at some point along 'forward' from startPos.
        // After the turn, we continue perpendicular. The geometry:
        //   lateral distance consumed by curve = radius  
        //   forward distance consumed by curve = radius
        //   remaining lateral = absLateral - radius (covered by straight after curve)
        //   forward straight before curve = forwardOffset - radius

        float straightBefore = forwardOffset - radius;
        float straightAfter  = absLateral - radius;
        if (straightBefore < 0f || straightAfter < -0.5f) return;
        straightAfter = Mathf.Max(0f, straightAfter);

        Vector3 turnStart = startPos + forward * straightBefore;
        turnStart.y = startPos.y;

        Vector3 curveCenter = turnStart + right * sign * radius;
        Vector3 radiusVec = turnStart - curveCenter;
        float angleDeg = 90f;
        float theta = angleDeg * Mathf.Deg2Rad;
        Quaternion rotation = Quaternion.AngleAxis(sign * angleDeg, Vector3.up);
        Vector3 curveEnd = curveCenter + rotation * radiusVec;
        curveEnd.y = startPos.y;

        Vector3 exitDir = (rotation * forward).normalized;

        // Final endpoint after the straight section.
        Vector3 finalPos = curveEnd + exitDir * straightAfter;
        finalPos.y = startPos.y;
        finalPos = SnapPosToTileCenter(finalPos);

        // Check that the final position is close enough to the intended target.
        float finalError = Vector3.Distance(
            new Vector3(finalPos.x, 0, finalPos.z),
            new Vector3(target.x, 0, target.z));
        if (finalError > 2f) return; // geometry doesn't line up

        // Check for overlaps.
        if (IsPathOverlappingExistingSpline(new SnapResult { arcPoints = SampleCompletionPath(
            startPos, turnStart, curveCenter, radiusVec, sign, angleDeg, curveEnd, finalPos, startPos.y) }))
            return;

        // Snap final position to target for join candidates.
        if (joinNode != null)
            finalPos = joinNode.worldPosition;

        // Build arc points for preview line.
        Vector3[] previewPoints = SampleCompletionPath(
            startPos, turnStart, curveCenter, radiusVec, sign, angleDeg, curveEnd, finalPos, startPos.y);

        // Build Bezier tangent handles.
        float straightBeforeLen = Mathf.Max(straightBefore, 0.1f);
        float curveD = (4f / 3f) * Mathf.Tan(theta / 4f) * radius;
        float straightAfterLen = Mathf.Max(straightAfter, 0.1f);

        // Completion knots: turnStart (end of first straight / start of curve),
        //                   curveEnd (end of curve / start of second straight).
        var knots = new CompletionKnot[2];

        // Knot 0: turnStart — end of straight, start of curve
        knots[0] = new CompletionKnot
        {
            position = turnStart,
            tangentIn = -forward * (straightBeforeLen / 3f),
            tangentOut = forward * curveD,
            gradePoints = SampleStraight(startPos, turnStart, startPos.y)
        };

        // Knot 1: curveEnd — end of curve, start of final straight
        knots[1] = new CompletionKnot
        {
            position = curveEnd,
            tangentIn = -exitDir * curveD,
            tangentOut = exitDir * (straightAfterLen / 3f),
            gradePoints = SampleArc(curveCenter, radiusVec, sign, angleDeg, startPos.y)
        };

        // The SnapResult tangentOut is for the FIRST segment (start → turnStart).
        // tangentIn is for the LAST segment (curveEnd → finalPos).
        candidates.Add(new SnapResult
        {
            position = finalPos,
            exitDirection = exitDir,
            tangentOut = forward * (straightBeforeLen / 3f), // start knot tangent-out
            tangentIn = -exitDir * (straightAfterLen / 3f),  // final knot tangent-in
            arcPoints = previewPoints,
            isValid = true,
            isJoin = joinNode != null,
            joinNode = joinNode,
            cardinalGroup = ComputeCardinalGroup(forward),
            completionKnots = knots
        });
    }

    private Vector3[] SampleCompletionPath(Vector3 start, Vector3 turnStart,
        Vector3 curveCenter, Vector3 radiusVec, float sign, float angleDeg,
        Vector3 curveEnd, Vector3 finalPos, float y)
    {
        var points = new List<Vector3>();

        // Straight before curve.
        float straightDist = Vector3.Distance(
            new Vector3(start.x, 0, start.z),
            new Vector3(turnStart.x, 0, turnStart.z));
        int straightSamples = Mathf.Max(2, Mathf.CeilToInt(straightDist));
        for (int i = 0; i < straightSamples; i++)
        {
            float t = (float)i / Mathf.Max(1, straightSamples - 1);
            Vector3 p = Vector3.Lerp(start, turnStart, t);
            p.y = y;
            points.Add(p);
        }

        // Arc.
        int arcSamples = Mathf.Max(8, previewArcSamples);
        for (int i = 1; i <= arcSamples; i++)
        {
            float t = (float)i / arcSamples;
            Quaternion rot = Quaternion.AngleAxis(sign * angleDeg * t, Vector3.up);
            Vector3 p = curveCenter + rot * radiusVec;
            p.y = y;
            points.Add(p);
        }

        // Straight after curve.
        float afterDist = Vector3.Distance(
            new Vector3(curveEnd.x, 0, curveEnd.z),
            new Vector3(finalPos.x, 0, finalPos.z));
        int afterSamples = Mathf.Max(2, Mathf.CeilToInt(afterDist));
        for (int i = 1; i <= afterSamples; i++)
        {
            float t = (float)i / afterSamples;
            Vector3 p = Vector3.Lerp(curveEnd, finalPos, t);
            p.y = y;
            points.Add(p);
        }

        return points.ToArray();
    }

    private Vector3[] SampleStraight(Vector3 a, Vector3 b, float y)
    {
        float dist = Vector3.Distance(new Vector3(a.x, 0, a.z), new Vector3(b.x, 0, b.z));
        int count = Mathf.Max(2, Mathf.CeilToInt(dist) + 1);
        var pts = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / (count - 1);
            pts[i] = Vector3.Lerp(a, b, t);
            pts[i].y = y;
        }
        return pts;
    }

    private Vector3[] SampleArc(Vector3 center, Vector3 radiusVec, float sign,
        float angleDeg, float y)
    {
        int count = Mathf.Max(4, previewArcSamples);
        var pts = new Vector3[count + 1];
        for (int i = 0; i <= count; i++)
        {
            float t = (float)i / count;
            Quaternion rot = Quaternion.AngleAxis(sign * angleDeg * t, Vector3.up);
            pts[i] = center + rot * radiusVec;
            pts[i].y = y;
        }
        return pts;
    }

    // ─── Preview ─────────────────────────────────────────────────────────

    private void UpdatePreview(Vector3 cursorWorldPos)
    {
        if (activeTool == ConductorTool.Delete)
        {
            UpdateDeletePreview(cursorWorldPos);
            return;
        }

        if (activeTool == ConductorTool.Select)
        {
            UpdateSelectPreview(cursorWorldPos);
            return;
        }

        if (activeTool == ConductorTool.Switch)
        {
            UpdateSwitchPreview(cursorWorldPos);
            return;
        }

        if (activeTool == ConductorTool.Place)
        {
            UpdatePlacePreview(cursorWorldPos);
            return;
        }

        if (!isDrawing || network == null)
        {
            // Show joinable nodes as yellow markers when not yet drawing.
            if (activeTool == ConductorTool.Draw && railGraph != null)
            {
                UpdateJoinableNodePreview(cursorWorldPos);
            }
            else
            {
                HideAllMarkers();
            }
            if (previewLine != null) previewLine.positionCount = 0;
            return;
        }

        Vector3? lastKnotOpt = network.GetLastKnotWorld();
        if (!lastKnotOpt.HasValue)
        {
            HideAllMarkers();
            if (previewLine != null) previewLine.positionCount = 0;
            return;
        }

        // ── Knot 2+: candidate dot system ──
        if (knotCount == 1)
        {
            // Show candidates in all 4 cardinal directions.
            BuildAllDirectionCandidates(lastKnotOpt.Value, cursorWorldPos);
        }
        else
        {
            BuildCandidates(lastKnotOpt.Value, lastDirection, cursorWorldPos);
        }
        BuildSplineSamples();
        RemoveCandidatesOnExistingTrack();

        // Generate join-arm candidates (arcs from lastKnot to existing track).
        if (knotCount == 1)
        {
            foreach (Vector3 cardinal in RailConstants.Cardinals)
                BuildJoinArmCandidates(lastKnotOpt.Value, cardinal, cursorWorldPos);
        }
        else
        {
            BuildJoinArmCandidates(lastKnotOpt.Value, lastDirection, cursorWorldPos);
        }

        TagParallelReturnCandidate(lastKnotOpt.Value, lastDirection);
        TagJunctionMirrorCandidates();
        RemoveCrossingCandidates();
        RemoveProximityCandidates();

        // Find closest candidate to cursor (XZ distance).
        float bestDistSq = float.MaxValue;
        selectedCandidate = -1;
        for (int i = 0; i < candidates.Count; i++)
        {
            Vector3 diff = candidates[i].position - cursorWorldPos;
            diff.y = 0f;
            float dSq = diff.sqrMagnitude;
            if (dSq < bestDistSq)
            {
                bestDistSq = dSq;
                selectedCandidate = i;
            }
        }

        // Position markers.
        EnsureMarkerPoolSize(candidates.Count);
        for (int i = 0; i < markerPool.Count; i++)
        {
            if (i < candidates.Count)
            {
                markerPool[i].SetActive(true);
                Vector3 markerPos = candidates[i].position;
                markerPos.y += 0.5f; // float well above terrain
                markerPool[i].transform.position = markerPos;

                bool isSel  = (i == selectedCandidate);
                bool isJoin = candidates[i].isJoin;

                // Colour by cardinal direction group (N=Yellow, E=Green, S=Blue, W=Orange).
                Material mat;
                if (isSel)
                    mat = selectedMat;
                else if (isJoin)
                    mat = joinMat;
                else
                    mat = GetCardinalGroupMaterial(candidates[i].cardinalGroup);

                // Special markers: star for straighten-return / junction mirror, sphere for continue-arc.
                bool isStraighten = candidates[i].isStraightenReturn && !isSel && !isJoin;
                bool isContinueArc = candidates[i].isParallelReturn && !isSel && !isJoin;
                bool isJuncMirror = candidates[i].isJunctionMirror && !isSel && !isJoin;
                if (isStraighten) mat = parallelReturnMat;
                else if (isJuncMirror) mat = parallelReturnMat;
                else if (isContinueArc) mat = parallelReturnMat;

                markerPool[i].GetComponent<Renderer>().sharedMaterial = mat;
                var mf = markerPool[i].GetComponent<MeshFilter>();
                Mesh meshToUse = cylinderMesh;
                if (isStraighten) meshToUse = starMesh;
                else if (isJuncMirror) meshToUse = starMesh;
                else if (isContinueArc) meshToUse = sphereMesh;
                if (mf != null) mf.sharedMesh = meshToUse;

                Vector3 scale;
                if (isStraighten || isJuncMirror)
                    scale = new Vector3(1f, 1f, 1f);
                else if (isContinueArc)
                    scale = new Vector3(markerRadius * 1.8f, markerRadius * 1.8f, markerRadius * 1.8f);
                else if (isSel)
                    scale = new Vector3(markerRadius * 1.3f, 0.2f, markerRadius * 1.3f);
                else
                    scale = new Vector3(markerRadius, 0.15f, markerRadius);
                markerPool[i].transform.localScale = scale;
            }
            else
            {
                markerPool[i].SetActive(false);
            }
        }

        // Show arc lines for all join-arm candidates.
        ShowJoinArmLines();

        // Show preview line for selected candidate.
        if (selectedCandidate >= 0)
            ShowPreviewLine(candidates[selectedCandidate]);
        else if (previewLine != null)
            previewLine.positionCount = 0;
    }

    private void ShowPreviewLine(SnapResult snap)
    {
        if (previewLine == null) return;
        if (!snap.isValid || snap.arcPoints == null || snap.arcPoints.Length < 2)
        {
            previewLine.positionCount = 0;
            return;
        }

        previewLine.positionCount = snap.arcPoints.Length;
        for (int i = 0; i < snap.arcPoints.Length; i++)
        {
            Vector3 p = snap.arcPoints[i];
            p.y += 0.5f; // raise preview line above terrain
            previewLine.SetPosition(i, p);
        }
    }

    /// <summary>
    /// Before the player starts drawing, show yellow markers at all existing
    /// nodes that have room for another connection (joinable endpoints/junctions).
    /// </summary>
    private void UpdateJoinableNodePreview(Vector3 cursorWorldPos)
    {
        joinableNodeRefs.Clear();

        if (railGraph == null)
        {
            HideAllMarkers();
            return;
        }

        for (int i = 0; i < railGraph.Nodes.Count; i++)
        {
            RailNode node = railGraph.Nodes[i];
            if (node.CanAddConnection)
                joinableNodeRefs.Add(node.worldPosition);
        }

        if (joinableNodeRefs.Count == 0)
        {
            HideAllMarkers();
            return;
        }

        EnsureMarkerPoolSize(joinableNodeRefs.Count);

        float bestDistSq = float.MaxValue;
        int nearest = -1;
        for (int i = 0; i < joinableNodeRefs.Count; i++)
        {
            Vector3 diff = joinableNodeRefs[i] - cursorWorldPos;
            diff.y = 0f;
            float dSq = diff.sqrMagnitude;
            if (dSq < bestDistSq) { bestDistSq = dSq; nearest = i; }
        }

        for (int i = 0; i < markerPool.Count; i++)
        {
            if (i < joinableNodeRefs.Count)
            {
                markerPool[i].SetActive(true);
                Vector3 pos = joinableNodeRefs[i];
                pos.y += 0.5f;
                markerPool[i].transform.position = pos;

                bool isSel = (i == nearest);
                markerPool[i].GetComponent<Renderer>().sharedMaterial =
                    isSel ? selectedMat : joinMat;
                markerPool[i].transform.localScale = isSel
                    ? new Vector3(markerRadius * 1.3f, 0.2f, markerRadius * 1.3f)
                    : new Vector3(markerRadius, 0.15f, markerRadius);
            }
            else
            {
                markerPool[i].SetActive(false);
            }
        }
    }

    private void HideAllMarkers()
    {
        for (int i = 0; i < markerPool.Count; i++)
            markerPool[i].SetActive(false);
        selectedCandidate = -1;
        HideSwitchPathLines();
        HideJoinArmLines();
    }

    // ─── Finish ──────────────────────────────────────────────────────────

    private void FinishSpline()
    {
        if (network == null || !isDrawing) return;

        // Capture the spline index before finishing (it gets cleared).
        int splineIdx = network.ActiveSplineIndex;

        // Get endpoint positions from the spline before it's finalized.
        Vector3? endPos = network.GetLastKnotWorld();

        // Cache knot count before FinishCurrentSpline potentially removes the spline.
        int knotCountBeforeFinish = (splineIdx >= 0 && splineIdx < network.SplineCount)
            ? network.Container.Splines[splineIdx].Count : 0;

        network.FinishCurrentSpline();

        // Register in graph if the spline was valid (had 2+ knots).
        bool hasGraph = railGraph != null;
        bool validSpline = splineIdx >= 0 && knotCountBeforeFinish >= 2;
        bool hasEnd = endPos.HasValue;

        Debug.Log($"[RailDrawing] FinishSpline: splineIdx={splineIdx}, knotsBefore={knotCountBeforeFinish}, " +
                  $"hasGraph={hasGraph}, validSpline={validSpline}, hasEnd={hasEnd}, " +
                  $"startPos={drawingStartPos}, endPos={endPos}");

        if (hasGraph && validSpline && hasEnd)
        {
            RailNode startNode = railGraph.GetOrCreateNode(drawingStartPos);

            RailNode endNode;
            if (pendingJoinNode != null)
            {
                endNode = pendingJoinNode;
                pendingJoinNode = null;
            }
            else
            {
                endNode = railGraph.GetOrCreateNode(endPos.Value);
            }

            // Exit direction at start node points along the track away from the node.
            Vector3 startExit = drawingStartExitDir;
            // Exit direction at end node points backward (away from the node along the track).
            Vector3 endExit = -drawingEndExitDir;

            // Recompute both group hints from the actual exit directions to
            // ensure they are consistent.  The drawing-system's cardinalGroup
            // reflects the forward direction which matches startExit, but
            // endExit points the opposite way and needs recomputation.
            drawingStartGroupHint = ComputeCardinalGroup(startExit);
            drawingEndGroupHint   = ComputeCardinalGroup(endExit);

            Debug.Log($"[FinishSpline] startExit=({startExit.x:F2},{startExit.z:F2}) grp={drawingStartGroupHint} | " +
                      $"endExit=({endExit.x:F2},{endExit.z:F2}) grp={drawingEndGroupHint}");

            railGraph.RegisterSegment(startNode, endNode, splineIdx, startExit, endExit,
                                       drawingStartGroupHint, drawingEndGroupHint);
        }
        else
        {
            // Clear pending join if spline was invalid.
            pendingJoinNode = null;
        }

        isDrawing = false;
        knotCount = 0;
        lastDirection = Vector3.zero;

        HideAllMarkers();
        if (previewLine != null) previewLine.positionCount = 0;

        if (railLineRenderer != null)
            railLineRenderer.RebuildFromSplines(network);
    }

    /// <summary>
    /// Procedurally generates a flat star mesh (XZ plane) with the given number of
    /// points, outer/inner radii, and height (Y thickness for visibility).
    /// </summary>
    private static Mesh BuildStarMesh(int points, float outerR, float innerR, float height)
    {
        int verts = points * 2;
        var top    = new Vector3[verts];
        var bottom = new Vector3[verts];
        float halfH = height * 0.5f;

        for (int i = 0; i < verts; i++)
        {
            float angle = Mathf.PI * 2f * i / verts - Mathf.PI * 0.5f;
            float r = (i % 2 == 0) ? outerR : innerR;
            float x = Mathf.Cos(angle) * r;
            float z = Mathf.Sin(angle) * r;
            top[i]    = new Vector3(x, halfH, z);
            bottom[i] = new Vector3(x, -halfH, z);
        }

        // Build triangles: top cap fan + bottom cap fan + side quads.
        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        // Top cap.
        int centerIdx = vertices.Count;
        vertices.Add(new Vector3(0, halfH, 0));
        for (int i = 0; i < verts; i++) vertices.Add(top[i]);
        for (int i = 0; i < verts; i++)
        {
            triangles.Add(centerIdx);
            triangles.Add(centerIdx + 1 + i);
            triangles.Add(centerIdx + 1 + (i + 1) % verts);
        }

        // Bottom cap.
        int bCenterIdx = vertices.Count;
        vertices.Add(new Vector3(0, -halfH, 0));
        for (int i = 0; i < verts; i++) vertices.Add(bottom[i]);
        for (int i = 0; i < verts; i++)
        {
            triangles.Add(bCenterIdx);
            triangles.Add(bCenterIdx + 1 + (i + 1) % verts);
            triangles.Add(bCenterIdx + 1 + i);
        }

        // Side quads.
        for (int i = 0; i < verts; i++)
        {
            int next = (i + 1) % verts;
            int a = vertices.Count; vertices.Add(top[i]);
            int b = vertices.Count; vertices.Add(top[next]);
            int c = vertices.Count; vertices.Add(bottom[next]);
            int d = vertices.Count; vertices.Add(bottom[i]);
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(a); triangles.Add(c); triangles.Add(d);
        }

        var mesh = new Mesh();
        mesh.name = "StarMarker";
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Draws a circular ring on the terrain surface under the mouse cursor,
/// similar to The Sims terrain-edit brush indicator.
/// Attach to any GameObject — it adds its own LineRenderer.
///
/// Also owns the active tool state (Raise / Dig / Flatten) and optional
/// UI button references for switching tools.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class TerrainBrushOverlay : MonoBehaviour
{
    public enum Tool { Raise, Dig, Flatten, Smooth }

    [Header("Brush")]
    [SerializeField] private float brushRadius = 3f;
    [SerializeField] private float minBrushRadius = 0.5f;
    [SerializeField] private float maxBrushRadius = 20f;
    [SerializeField] private float brushRadiusStep = 0.5f;
    [SerializeField] private int segmentCount = 64;
    [SerializeField] private float heightOffset = 0.05f;
    [SerializeField] private Color brushColor = Color.cyan;
    [SerializeField] private float lineWidth = 0.08f;

    [Header("Raycast")]
    [SerializeField] private Camera sourceCamera;
    [SerializeField] private LayerMask terrainLayer = ~0;

    [Header("Terrain Editing")]
    [SerializeField] private TerrainEditController editController;
    [SerializeField] private float editRepeatInterval = 0.15f;

    [Header("Tool Buttons (optional)")]
    [SerializeField] private Button raiseButton;
    [SerializeField] private Button digButton;
    [SerializeField] private Button flattenButton;
    [SerializeField] private Button smoothButton;
    [SerializeField] private Toggle oneStepToggle;
    [SerializeField] private Slider strengthSlider;

    [Header("Conflict")]
    [SerializeField] private HumanFurniturePlacer furniturePlacer;

    private LineRenderer line;
    private Terrain[] terrains;
    private Vector3[] positions;
    private bool isOverTerrain;
    private Vector3 lastHitPoint;
    private bool isEditing;
    private float editTimer;
    private int flattenTargetHeight;
    private Tool activeTool = Tool.Raise;
    private bool oneStepMode;
    private int oneStepTarget;
    private int brushStrength = 1;

    /// <summary>Whether the brush is currently over valid terrain.</summary>
    public bool IsOverTerrain => isOverTerrain;
    /// <summary>The last world-space hit point on terrain.</summary>
    public Vector3 HitPoint => lastHitPoint;
    /// <summary>Current brush radius in world units.</summary>
    public float BrushRadius => brushRadius;
    /// <summary>Currently selected terrain tool.</summary>
    public Tool ActiveTool => activeTool;

    /// <summary>Whether one-step mode is active (raise/lower by exactly 1, then paint that level).</summary>
    public bool OneStepMode => oneStepMode;

    /// <summary>Change the active tool from code.</summary>
    public void SetTool(Tool t)
    {
        activeTool = t;
        UpdateToolButtonVisuals();
    }

    /// <summary>Set one-step mode on/off (called by Toggle.onValueChanged).</summary>
    public void SetOneStepMode(bool value)
    {
        oneStepMode = value;
    }

    /// <summary>Set brush strength (called by Slider.onValueChanged).</summary>
    public void SetBrushStrength(float value)
    {
        brushStrength = Mathf.Max(1, Mathf.RoundToInt(value));
    }

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.startColor = brushColor;
        line.endColor = brushColor;
        line.positionCount = segmentCount;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.material.color = brushColor;

        positions = new Vector3[segmentCount];

        // Gather all terrains in the scene for height sampling.
        terrains = Terrain.activeTerrains;
    }

    private void OnEnable()
    {
        if (line != null) line.enabled = false;
        if (raiseButton != null)   raiseButton.onClick.AddListener(() => SetTool(Tool.Raise));
        if (digButton != null)     digButton.onClick.AddListener(() => SetTool(Tool.Dig));
        if (flattenButton != null) flattenButton.onClick.AddListener(() => SetTool(Tool.Flatten));
        if (smoothButton != null)  smoothButton.onClick.AddListener(() => SetTool(Tool.Smooth));
        if (oneStepToggle != null) oneStepToggle.onValueChanged.AddListener(SetOneStepMode);
        if (strengthSlider != null)
        {
            strengthSlider.wholeNumbers = true;
            strengthSlider.minValue = 1;
            strengthSlider.maxValue = 10;
            strengthSlider.value = brushStrength;
            strengthSlider.onValueChanged.AddListener(SetBrushStrength);
        }
        UpdateToolButtonVisuals();
    }

    private void OnDisable()
    {
        if (raiseButton != null)   raiseButton.onClick.RemoveAllListeners();
        if (digButton != null)     digButton.onClick.RemoveAllListeners();
        if (flattenButton != null) flattenButton.onClick.RemoveAllListeners();
        if (smoothButton != null)  smoothButton.onClick.RemoveAllListeners();
        if (oneStepToggle != null) oneStepToggle.onValueChanged.RemoveAllListeners();
        if (strengthSlider != null) strengthSlider.onValueChanged.RemoveAllListeners();
    }

    private void Update()
    {
        if (sourceCamera == null || Mouse.current == null)
        {
            if (line != null) line.enabled = false;
            return;
        }

        // Ctrl + ScrollWheel to resize brush
        if (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                brushRadius += Mathf.Sign(scroll) * brushRadiusStep;
                brushRadius = Mathf.Clamp(brushRadius, minBrushRadius, maxBrushRadius);
            }
        }

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = sourceCamera.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, terrainLayer))
        {
            isOverTerrain = true;
            lastHitPoint = hit.point;
            line.enabled = true;
            BuildRing(hit.point);
            line.SetPositions(positions);
        }
        else
        {
            isOverTerrain = false;
            line.enabled = false;
        }

        HandleEditing();
    }

    private void BuildRing(Vector3 center)
    {
        float step = 360f / segmentCount;
        for (int i = 0; i < segmentCount; i++)
        {
            float angle = i * step * Mathf.Deg2Rad;
            float x = center.x + Mathf.Cos(angle) * brushRadius;
            float z = center.z + Mathf.Sin(angle) * brushRadius;
            float y = SampleTerrainHeight(x, z) + heightOffset;
            positions[i] = new Vector3(x, y, z);
        }
    }

    private float SampleTerrainHeight(float worldX, float worldZ)
    {
        if (terrains != null)
        {
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain t = terrains[i];
                if (t == null) continue;
                Vector3 tPos = t.transform.position;
                TerrainData td = t.terrainData;
                if (worldX >= tPos.x && worldX <= tPos.x + td.size.x &&
                    worldZ >= tPos.z && worldZ <= tPos.z + td.size.z)
                {
                    return t.SampleHeight(new Vector3(worldX, 0f, worldZ)) + tPos.y;
                }
            }
        }
        return 0f;
    }

    // ── Terrain Editing ───────────────────────────────────────────────────────────

    private void HandleEditing()
    {
        if (editController == null) return;
        if (Mouse.current == null) return;

        // Suppress terrain editing while clicking UI or dragging furniture
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (furniturePlacer != null && furniturePlacer.IsDragging) return;

        bool leftDown = Mouse.current.leftButton.isPressed;

        if (leftDown && isOverTerrain)
        {
            if (!isEditing)
            {
                isEditing = true;
                editTimer = 0f;
                flattenTargetHeight = editController.SampleTileLevel(lastHitPoint);

                // In one-step mode, compute the target height once on initial click
                if (oneStepMode && (activeTool == Tool.Raise || activeTool == Tool.Dig))
                {
                    int step = activeTool == Tool.Raise ? brushStrength : -brushStrength;
                    oneStepTarget = Mathf.Max(0, flattenTargetHeight + step);
                }

                ApplyBrushEdit();
            }
            else
            {
                editTimer += Time.deltaTime;
                if (editTimer >= editRepeatInterval)
                {
                    editTimer -= editRepeatInterval;
                    ApplyBrushEdit();
                }
            }
        }
        else
        {
            isEditing = false;
        }
    }

    private void ApplyBrushEdit()
    {
        switch (activeTool)
        {
            case Tool.Raise:
                if (oneStepMode)
                    editController.FlattenTilesInRadius(lastHitPoint, brushRadius, oneStepTarget);
                else
                    editController.RaiseTilesInRadius(lastHitPoint, brushRadius, brushStrength);
                break;
            case Tool.Dig:
                if (oneStepMode)
                    editController.FlattenTilesInRadius(lastHitPoint, brushRadius, oneStepTarget);
                else
                    editController.LowerTilesInRadius(lastHitPoint, brushRadius, brushStrength);
                break;
            case Tool.Flatten:
                editController.FlattenTilesInRadius(lastHitPoint, brushRadius, flattenTargetHeight);
                break;
            case Tool.Smooth:
                editController.SmoothTilesInRadius(lastHitPoint, brushRadius);
                break;
        }
    }

    // ── Tool Button Visuals ───────────────────────────────────────────────────────

    private void UpdateToolButtonVisuals()
    {
        SetButtonHighlight(raiseButton,   activeTool == Tool.Raise);
        SetButtonHighlight(digButton,     activeTool == Tool.Dig);
        SetButtonHighlight(flattenButton, activeTool == Tool.Flatten);
        SetButtonHighlight(smoothButton,  activeTool == Tool.Smooth);
    }

    private static void SetButtonHighlight(Button btn, bool active)
    {
        if (btn == null) return;
        ColorBlock cb = btn.colors;
        cb.normalColor = active ? new Color(1f, 0.78f, 0.2f) : Color.white;
        btn.colors = cb;
    }

}

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// Attach to the Player. Manages transitions between toy mode and three human sub-modes:
///   - Terraform: brush overlay for raising/lowering terrain
///   - Decorate: furniture placement via 3D raycasting + catalog panel
///   - Conductor: railroad spline drawing + terrain grading
///
/// Menu buttons call EnterHumanTerraformMode / EnterHumanDecorateMode / EnterHumanConductorMode.
/// Press H to return to toy mode from any human sub-mode.
/// </summary>
public class GameModeManager : MonoBehaviour
{
    [Header("Toy Mode")]
    [SerializeField] private Camera toyCamera;
    [SerializeField] private PlayerMotor toyPlayerMotor;

    [Header("Human Mode")]
    [SerializeField] private Camera humanCamera;
    [SerializeField] private HumanBodyController humanBody;
    [SerializeField] private TerrainBrushOverlay terrainBrushOverlay;
    [SerializeField] private TerrainEditController terrainEditController;
    [SerializeField] private HumanFurniturePlacer humanFurniturePlacer;
    [FormerlySerializedAs("buildModeController")]
    [SerializeField] private FurnitureSpawner furnitureSpawner;
    [SerializeField] private RailDrawingController railDrawingController;

    [Header("UI Panels")]
    [SerializeField] private GameObject terraformPanel;
    [SerializeField] private GameObject conductorPanel;

    [Header("Toggle")]
    [SerializeField] private Key exitKey = Key.H;

    public enum HumanSubMode { None, Terraform, Decorate, Conductor }

    private HumanSubMode currentSubMode = HumanSubMode.None;

    public bool IsInHumanMode => currentSubMode != HumanSubMode.None;
    public HumanSubMode CurrentSubMode => currentSubMode;

    private void Awake()
    {
        SetToyMode();
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (!Keyboard.current[exitKey].wasPressedThisFrame) return;

        if (IsInHumanMode)
            SetToyMode();
    }

    // ── Public entry points (call from menu buttons via UnityEvent) ───────────

    public void EnterHumanTerraformMode()
    {
        if (currentSubMode == HumanSubMode.Terraform) return;

        if (currentSubMode == HumanSubMode.Conductor)
            DeactivateConductor();
        else if (currentSubMode == HumanSubMode.Decorate)
            DeactivateDecorate();

        if (currentSubMode == HumanSubMode.None)
            ActivateHumanBase();

        currentSubMode = HumanSubMode.Terraform;

        // Enable terraform tools
        if (terrainBrushOverlay != null) terrainBrushOverlay.gameObject.SetActive(true);
        if (terraformPanel != null) terraformPanel.SetActive(true);

        // Disable furniture tools
        if (humanFurniturePlacer != null) humanFurniturePlacer.enabled = false;
    }

    public void EnterHumanDecorateMode()
    {
        if (currentSubMode == HumanSubMode.Decorate) return;

        if (currentSubMode == HumanSubMode.Conductor)
            DeactivateConductor();
        else if (currentSubMode == HumanSubMode.Terraform)
            DeactivateTerraform();

        if (currentSubMode == HumanSubMode.None)
            ActivateHumanBase();

        currentSubMode = HumanSubMode.Decorate;

        // Disable terraform tools
        if (terrainBrushOverlay != null) terrainBrushOverlay.gameObject.SetActive(false);

        // Enable furniture tools
        if (humanFurniturePlacer != null) humanFurniturePlacer.enabled = true;
        if (furnitureSpawner != null) furnitureSpawner.EnterDecorateMode(humanCamera);
    }

    public void SetToyMode()
    {
        if (currentSubMode == HumanSubMode.Terraform)
            DeactivateTerraform();
        else if (currentSubMode == HumanSubMode.Decorate)
            DeactivateDecorate();
        else if (currentSubMode == HumanSubMode.Conductor)
            DeactivateConductor();

        currentSubMode = HumanSubMode.None;

        DeactivateHumanBase();
    }

    public void EnterHumanConductorMode()
    {
        if (currentSubMode == HumanSubMode.Conductor) return;

        if (currentSubMode == HumanSubMode.Terraform)
            DeactivateTerraform();
        else if (currentSubMode == HumanSubMode.Decorate)
            DeactivateDecorate();

        if (currentSubMode == HumanSubMode.None)
            ActivateHumanBase();

        currentSubMode = HumanSubMode.Conductor;

        if (terrainBrushOverlay != null) terrainBrushOverlay.gameObject.SetActive(false);
        if (humanFurniturePlacer != null) humanFurniturePlacer.enabled = false;
        if (railDrawingController != null) railDrawingController.enabled = true;
        if (conductorPanel != null) conductorPanel.SetActive(true);
    }

    // ── Shared base activation / deactivation ─────────────────────────────────

    private void ActivateHumanBase()
    {
        if (humanCamera == null || humanBody == null)
        {
            Debug.LogError("[GameModeManager] humanCamera or humanBody is not assigned!", this);
            return;
        }

        Vector3 refPos = toyPlayerMotor != null ? toyPlayerMotor.transform.position : transform.position;
        humanBody.gameObject.SetActive(true);
        humanBody.Activate(refPos);

        if (toyCamera != null) toyCamera.enabled = false;
        humanCamera.enabled = true;

        if (toyPlayerMotor != null) toyPlayerMotor.enabled = false;
    }

    private void DeactivateHumanBase()
    {
        if (humanBody != null)
        {
            humanBody.Deactivate();
            humanBody.gameObject.SetActive(false);
        }

        if (humanCamera != null) humanCamera.enabled = false;
        if (toyCamera != null) toyCamera.enabled = true;

        if (toyPlayerMotor != null) toyPlayerMotor.enabled = true;
    }

    // ── Sub-mode teardown helpers ─────────────────────────────────────────────

    private void DeactivateTerraform()
    {
        if (terrainBrushOverlay != null) terrainBrushOverlay.gameObject.SetActive(false);
        if (terraformPanel != null) terraformPanel.SetActive(false);
        if (terrainEditController != null) terrainEditController.ExitTerraformMode();
    }

    private void DeactivateDecorate()
    {
        if (humanFurniturePlacer != null) humanFurniturePlacer.enabled = false;
        if (furnitureSpawner != null) furnitureSpawner.ExitDecorateMode();
    }

    private void DeactivateConductor()
    {
        if (railDrawingController != null) railDrawingController.enabled = false;
        if (conductorPanel != null) conductorPanel.SetActive(false);
    }
}

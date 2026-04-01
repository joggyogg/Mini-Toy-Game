using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space UI that floats above a train, providing Start/Stop,
/// Rotate, and Forward/Reverse buttons. Billboards toward the camera.
/// Attach as a child of the Locomotive prefab.
/// </summary>
public class TrainWorldUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button startStopButton;
    [SerializeField] private Button rotateButton;
    [SerializeField] private Button gearButton;

    [Header("Labels (optional)")]
    [SerializeField] private TMPro.TMP_Text startStopLabel;
    [SerializeField] private TMPro.TMP_Text gearLabel;

    [Header("Billboard")]
    [SerializeField] private float heightAbovePivot = 3f;

    private Locomotive loco;
    private Canvas canvas;

    private void Awake()
    {
        loco = GetComponentInParent<Locomotive>();
        canvas = GetComponentInChildren<Canvas>();

        if (startStopButton != null) startStopButton.onClick.AddListener(OnStartStop);
        if (rotateButton != null)    rotateButton.onClick.AddListener(OnRotate);
        if (gearButton != null)      gearButton.onClick.AddListener(OnGear);
    }

    private void OnDestroy()
    {
        if (startStopButton != null) startStopButton.onClick.RemoveListener(OnStartStop);
        if (rotateButton != null)    rotateButton.onClick.RemoveListener(OnRotate);
        if (gearButton != null)      gearButton.onClick.RemoveListener(OnGear);
    }

    private void LateUpdate()
    {
        if (loco == null) return;

        // Position above the locomotive.
        transform.position = loco.transform.position + Vector3.up * heightAbovePivot;

        // Billboard toward camera.
        Camera cam = Camera.main;
        if (cam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);

        UpdateLabels();
    }

    public void SetVisible(bool visible)
    {
        if (canvas != null) canvas.enabled = visible;
    }

    private void OnStartStop()
    {
        if (loco == null) return;
        loco.IsMoving = !loco.IsMoving;
    }

    private void OnRotate()
    {
        if (loco == null) return;
        loco.RotateTrain();
    }

    private void OnGear()
    {
        if (loco == null) return;
        loco.IsForwardGear = !loco.IsForwardGear;
    }

    private void UpdateLabels()
    {
        if (loco == null) return;
        if (startStopLabel != null)
            startStopLabel.text = loco.IsMoving ? "Stop" : "Start";
        if (gearLabel != null)
            gearLabel.text = loco.IsForwardGear ? "Fwd" : "Rev";
    }
}

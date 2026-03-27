using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Attach to a Camera that is a child of the Human Body game object (the "Human Camera").
/// This camera is perspective and follows the human body's eye position.
/// W/S control a vertical pitch (lean over the terrain vs. pull back to eye-level).
/// The GameModeManager enables/disables this camera's GameObject to switch modes.
/// </summary>
[RequireComponent(typeof(Camera))]
public class HumanCameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HumanBodyController humanBody;

    [Header("Zoom (Scroll Wheel)")]
    [SerializeField] private float defaultFOV = 60f;
    [SerializeField] private float minFOV = 20f;
    [SerializeField] private float maxFOV = 80f;
    [SerializeField] private float zoomSpeed = 10f;
    [SerializeField] private float zoomSmoothTime = 0.12f;

    [Header("Pitch Control (W/S)")]
    [Tooltip("Minimum pitch in degrees (eye-level). Lower = more horizontal.")]
    [SerializeField] private float minPitchDegrees = 30f;
    [Tooltip("Maximum pitch in degrees (overhead). Higher = more top-down.")]
    [SerializeField] private float maxPitchDegrees = 80f;
    [SerializeField] private float pitchSpeed = 60f;
    [SerializeField] private float pitchSmoothTime = 0.15f;

    [Header("Forward/Back Lean (W/S)")]
    [Tooltip("Maximum distance the camera moves forward (toward terrain) when W is fully held.")]
    [SerializeField] private float maxForwardOffset = 30f;
    [Tooltip("Maximum distance the camera moves backward (toward human) when S is fully held. Usually 0 or small.")]
    [SerializeField] private float maxBackwardOffset = 5f;
    [SerializeField] private float leanSmoothTime = 0.15f;

    [Header("Smoothing")]
    [SerializeField] private float positionSmoothTime = 0.08f;
    [SerializeField] private float rotationSharpness = 10f;

    [Header("Cursor Follow")]
    [Tooltip("Maximum degrees the camera can rotate toward the mouse cursor away from the base facing direction.")]
    [SerializeField] private float maxCursorRotationDegrees = 12f;
    [Tooltip("Height of the world plane used for cursor projection (should be near the model terrain surface).")]
    [SerializeField] private float cursorFocusPlaneHeight = 0f;

    [Header("Dead Zones (fractions of screen size)")]
    [Tooltip("Half-width of the centre column (suppresses yaw).")]
    [Range(0f, 0.5f)]
    [SerializeField] private float columnHalfWidth = 0.08f;
    [Tooltip("Half-height of the centre row (suppresses pitch).")]
    [Range(0f, 0.5f)]
    [SerializeField] private float rowHalfHeight = 0.08f;

    [Header("Debug")]
    [SerializeField] private bool showDeadZoneOverlay = false;

    private Camera attachedCamera;
    private float targetPitch;
    private float currentPitch;
    private float pitchVelocity;
    private Vector3 positionVelocity;
    private float currentLeanOffset;
    private float leanVelocity;
    private float targetFOV;
    private float currentFOV;
    private float fovVelocity;

    // Track which side of centre the cursor was on before entering a dead zone.
    // +1 = right/above, -1 = left/below.
    private float lastSideX;
    private float lastSideY;

    private void Awake()
    {
        attachedCamera = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        if (attachedCamera == null) attachedCamera = GetComponent<Camera>();
        attachedCamera.orthographic = false;
        targetFOV = defaultFOV;
        currentFOV = defaultFOV;
        fovVelocity = 0f;
        attachedCamera.fieldOfView = defaultFOV;

        // Start at a reasonable middle pitch
        targetPitch = (minPitchDegrees + maxPitchDegrees) * 0.5f;
        currentPitch = targetPitch;
        pitchVelocity = 0f;
        positionVelocity = Vector3.zero;
        currentLeanOffset = 0f;
        leanVelocity = 0f;

        // Snap to body position immediately if available
        if (humanBody != null && humanBody.IsActive)
        {
            transform.position = humanBody.EyeWorldPosition;
        }
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        float pitchInput = 0f;
        if (Keyboard.current.wKey.isPressed) pitchInput += 1f;
        if (Keyboard.current.sKey.isPressed) pitchInput -= 1f;

        targetPitch += pitchInput * pitchSpeed * Time.deltaTime;
        targetPitch = Mathf.Clamp(targetPitch, minPitchDegrees, maxPitchDegrees);

        // Scroll wheel zoom (narrower FOV = zoom in) — skip when Ctrl is held (brush resize)
        if (Mouse.current != null
            && !(Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed))
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                targetFOV -= (scroll / 120f) * zoomSpeed;
                targetFOV = Mathf.Clamp(targetFOV, minFOV, maxFOV);
            }
        }
    }

    private void LateUpdate()
    {
        if (humanBody == null) return;

        currentPitch = Mathf.SmoothDamp(currentPitch, targetPitch, ref pitchVelocity, pitchSmoothTime);

        // Compute lean offset: pitch normalized 0..1 across the range maps to backward..forward offset
        float pitchRange = maxPitchDegrees - minPitchDegrees;
        float pitchT = pitchRange > 0.001f ? (currentPitch - minPitchDegrees) / pitchRange : 0f;
        // At the diagonal corner substep, allow sqrt(2) extra forward reach
        float effectiveForwardOffset = humanBody.IsAtCornerDiagonal
            ? maxForwardOffset * Mathf.Sqrt(2f)
            : maxForwardOffset;
        // pitchT = 0 at minPitch (eye-level / pulled back), pitchT = 1 at maxPitch (overhead / leaned forward)
        float targetLean = Mathf.Lerp(-maxBackwardOffset, effectiveForwardOffset, pitchT);
        currentLeanOffset = Mathf.SmoothDamp(currentLeanOffset, targetLean, ref leanVelocity, leanSmoothTime);

        // Position: eye position + forward lean along the body's facing direction
        Vector3 eyePos = humanBody.EyeWorldPosition;
        Vector3 facingForward = humanBody.CurrentFacingRotation * Vector3.forward;
        facingForward.y = 0f;
        facingForward.Normalize();
        Vector3 targetPos = eyePos + facingForward * currentLeanOffset;

        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref positionVelocity, positionSmoothTime);

        // Base rotation from waypoint facing + current pitch
        float baseYaw = humanBody.CurrentFacingRotation.eulerAngles.y;
        Quaternion baseRotation = Quaternion.Euler(currentPitch, baseYaw, 0f);

        // Try to nudge the look direction toward the cursor (clamped, per-axis dead zones)
        Quaternion desired = baseRotation;
        if (TryGetCursorFocusPoint(out Vector3 focusPoint))
        {
            Vector3 lookDir = (focusPoint - transform.position).normalized;
            desired = ClampLookToBase(lookDir, baseYaw, currentPitch);
        }

        transform.rotation = Quaternion.Slerp(transform.rotation, desired, rotationSharpness * Time.deltaTime);

        // Apply smoothed FOV zoom
        currentFOV = Mathf.SmoothDamp(currentFOV, targetFOV, ref fovVelocity, zoomSmoothTime);
        attachedCamera.fieldOfView = currentFOV;
    }

    /// <summary>
    /// Projects the midpoint between the camera's forward hit and the cursor onto a world-space
    /// horizontal plane and returns the focus point, similar to OrthographicFollowCamera.
    /// </summary>
    private bool TryGetCursorFocusPoint(out Vector3 focusPoint)
    {
        focusPoint = Vector3.zero;
        if (attachedCamera == null || Mouse.current == null) return false;

        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;
        Vector2 cursorScreen = Mouse.current.position.ReadValue();

        // Dead zone clamping: pin the cursor to the dead zone edge on the side
        // it entered from, so there's a consistent bias and no centre-line jitter.
        float dx = Mathf.Abs(cursorScreen.x - cx);
        float dy = Mathf.Abs(cursorScreen.y - cy);
        float colEdge = columnHalfWidth * Screen.width;
        float rowEdge = rowHalfHeight * Screen.height;
        bool inColumn = dx < colEdge;
        bool inRow    = dy < rowEdge;

        // Update last-known side when cursor is outside the dead zone.
        if (!inColumn) lastSideX = Mathf.Sign(cursorScreen.x - cx);
        if (!inRow)    lastSideY = Mathf.Sign(cursorScreen.y - cy);

        // Clamp to the edge the cursor entered from (slight bias, no jitter).
        if (inColumn && !inRow)  cursorScreen.x = cx + lastSideX * colEdge;
        if (inRow && !inColumn)  cursorScreen.y = cy + lastSideY * rowEdge;

        // Midpoint between screen center and (clamped) cursor
        Vector2 midpoint = (new Vector2(cx, cy) + cursorScreen) * 0.5f;

        Ray ray = attachedCamera.ScreenPointToRay(midpoint);
        Plane plane = new Plane(Vector3.up, new Vector3(0f, cursorFocusPlaneHeight, 0f));
        if (!plane.Raycast(ray, out float enter)) return false;

        focusPoint = ray.GetPoint(enter);
        return true;
    }

    /// <summary>
    /// Clamps the desired look direction so it stays within maxCursorRotationDegrees of the
    /// base yaw and pitch, exactly like OrthographicFollowCamera.ClampLookDirectionToBase.
    /// </summary>
    private Quaternion ClampLookToBase(Vector3 lookDirection, float baseYaw, float basePitch)
    {
        Vector3 planar = new Vector3(lookDirection.x, 0f, lookDirection.z);
        float planarMag = planar.magnitude;
        if (planarMag < 0.0001f) return Quaternion.Euler(basePitch, baseYaw, 0f);

        float desiredYaw = Mathf.Atan2(lookDirection.x, lookDirection.z) * Mathf.Rad2Deg;
        float desiredPitch = Mathf.Atan2(-lookDirection.y, planarMag) * Mathf.Rad2Deg;

        float yawOffset = Mathf.DeltaAngle(baseYaw, desiredYaw);
        float finalYaw = baseYaw + Mathf.Clamp(yawOffset, -maxCursorRotationDegrees, maxCursorRotationDegrees);
        float finalPitch = basePitch + Mathf.Clamp(desiredPitch - basePitch, -maxCursorRotationDegrees, maxCursorRotationDegrees);

        return Quaternion.Euler(finalPitch, finalYaw, 0f);
    }

    private void OnGUI()
    {
        if (!showDeadZoneOverlay) return;

        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;
        float colHalfPx = columnHalfWidth * Screen.width;
        float rowHalfPx = rowHalfHeight * Screen.height;

        Texture2D pixel = Texture2D.whiteTexture;

        // Column (yaw dead zone) — blue, full height
        GUI.color = new Color(0f, 0.4f, 1f, 0.12f);
        GUI.DrawTexture(new Rect(cx - colHalfPx, 0, colHalfPx * 2, Screen.height), pixel);
        // Column borders
        GUI.color = new Color(0f, 0.4f, 1f, 0.7f);
        GUI.DrawTexture(new Rect(cx - colHalfPx, 0, 2, Screen.height), pixel);
        GUI.DrawTexture(new Rect(cx + colHalfPx - 2, 0, 2, Screen.height), pixel);

        // Row (pitch dead zone) — green, full width
        // GUI y is top-down, screen y is bottom-up
        float rowTop = cy - rowHalfPx;  // in GUI coords (top-down)
        GUI.color = new Color(0f, 1f, 0.3f, 0.12f);
        GUI.DrawTexture(new Rect(0, rowTop, Screen.width, rowHalfPx * 2), pixel);
        // Row borders
        GUI.color = new Color(0f, 1f, 0.3f, 0.7f);
        GUI.DrawTexture(new Rect(0, rowTop, Screen.width, 2), pixel);
        GUI.DrawTexture(new Rect(0, rowTop + rowHalfPx * 2 - 2, Screen.width, 2), pixel);

        // Status label
        bool inCol = false, inRow = false;
        if (Mouse.current != null)
        {
            Vector2 cur = Mouse.current.position.ReadValue();
            inCol = Mathf.Abs(cur.x - cx) < colHalfPx;
            inRow = Mathf.Abs(cur.y - cy) < rowHalfPx;
        }

        string status;
        if (inCol && inRow)      status = "CENTRE — yaw + pitch active";
        else if (inCol)          status = "COLUMN — no yaw, pitch active";
        else if (inRow)          status = "ROW — yaw active, no pitch";
        else                     status = "OUTSIDE — yaw + pitch active";

        Color statusColor = (inCol && inRow) ? Color.green
            : (inCol || inRow) ? Color.yellow
            : Color.green;

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperCenter
        };
        GUI.color = statusColor;
        GUI.Label(new Rect(0, 10, Screen.width, 30), status, style);
        GUI.color = Color.white;
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Plain-English purpose:
/// Attach this to an orthographic camera to make it follow the player from a fixed position above and behind them.
///
/// The camera keeps a stable follow offset, but it rotates slightly so it looks toward the midpoint between the
/// player and the mouse cursor projected onto the world. This makes the camera feel aware of where the player is
/// aiming or planning to place objects without drifting away from its follow position.
///
/// The base view direction is fixed in world space, so it is not tethered to the player's facing direction.
/// </summary>
[RequireComponent(typeof(Camera))]
public class OrthographicFollowCamera : MonoBehaviour
{
    private const float DefaultPitchDegrees = 30f;
    private const float DefaultRotationLimitDegrees = 8f;

    [Header("Follow")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private float followDistance = 25f;
    [SerializeField] private float followHeight = 25f;
    [SerializeField] private float positionSmoothTime = 0.12f;

    [Header("Rotation")]
    [SerializeField] private float baseYawDegrees = 0f;
    [SerializeField] private float basePitchDegrees = DefaultPitchDegrees;
    [SerializeField] private float maxRotationDegrees = DefaultRotationLimitDegrees;
    [SerializeField] private float rotationSharpness = 10f;

    [Header("Orbit")]
    [SerializeField] private float orbitStepDegrees = 90f;
    [SerializeField] private float orbitSmoothTime = 0.25f;

    [Header("Cursor Projection")]
    [SerializeField] private float focusPlaneHeight = 0f;

    private Camera attachedCamera;
    private Vector3 followVelocity;
    private float targetYaw;
    private float yawVelocity;

    private void Awake()
    {
        attachedCamera = GetComponent<Camera>();
        attachedCamera.orthographic = true;
        targetYaw = baseYawDegrees;
    }

    private void Update()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.eKey.wasPressedThisFrame)
                targetYaw += orbitStepDegrees;
            if (Keyboard.current.qKey.wasPressedThisFrame)
                targetYaw -= orbitStepDegrees;
        }
    }

    private void LateUpdate()
    {
        if (playerTarget == null)
        {
            return;
        }

        baseYawDegrees = Mathf.SmoothDampAngle(baseYawDegrees, targetYaw, ref yawVelocity, orbitSmoothTime);

        Vector3 desiredPosition = GetDesiredCameraPosition();
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref followVelocity, positionSmoothTime);

        Vector3 focusPoint = GetFocusPoint();
        Quaternion desiredRotation = GetDesiredRotation(focusPoint);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSharpness * Time.deltaTime);
    }

    private Vector3 GetDesiredCameraPosition()
    {
        // The camera stays behind a fixed world-facing direction instead of following the player's rotation.
        Vector3 viewForward = GetPlanarViewForward();
        Vector3 behindOffset = -viewForward * followDistance;
        Vector3 heightOffset = Vector3.up * followHeight;
        return playerTarget.position + behindOffset + heightOffset;
    }

    private Quaternion GetBaseRotation()
    {
        return GetRotationFromYawPitch(baseYawDegrees, basePitchDegrees);
    }

    /// <summary>The horizontal forward direction of the current orbit angle, suitable for use as a movement reference.</summary>
    public Vector3 PlanarForward => GetPlanarViewForward();

    private Vector3 GetPlanarViewForward()
    {
        Vector3 viewForward = Quaternion.Euler(0f, baseYawDegrees, 0f) * Vector3.forward;
        viewForward.y = 0f;
        return viewForward.normalized;
    }

    private Quaternion GetDesiredRotation(Vector3 focusPoint)
    {
        Vector3 lookDirection = focusPoint - transform.position;
        if (lookDirection.sqrMagnitude < 0.0001f)
        {
            return GetBaseRotation();
        }

        return ClampLookDirectionToBase(lookDirection.normalized);
    }

    private Vector3 GetFocusPoint()
    {
        if (!TryGetScreenSpaceMidpointWorldPoint(out Vector3 midpointWorldPoint))
        {
            Vector3 fallbackPoint = playerTarget.position;
            fallbackPoint.y = focusPlaneHeight;
            return fallbackPoint;
        }

        midpointWorldPoint.y = focusPlaneHeight;
        return midpointWorldPoint;
    }

    private bool TryGetScreenSpaceMidpointWorldPoint(out Vector3 midpointWorldPoint)
    {
        midpointWorldPoint = Vector3.zero;

        if (Mouse.current == null)
        {
            return false;
        }

        Vector3 playerScreenPoint = attachedCamera.WorldToScreenPoint(playerTarget.position);
        if (playerScreenPoint.z <= 0f)
        {
            return false;
        }

        Vector2 cursorScreenPoint = Mouse.current.position.ReadValue();
        Vector2 midpointScreenPoint = ((Vector2)playerScreenPoint + cursorScreenPoint) * 0.5f;

        Ray cursorRay = attachedCamera.ScreenPointToRay(midpointScreenPoint);
        Plane focusPlane = new Plane(Vector3.up, new Vector3(0f, focusPlaneHeight, 0f));
        if (!focusPlane.Raycast(cursorRay, out float enterDistance))
        {
            return false;
        }

        midpointWorldPoint = cursorRay.GetPoint(enterDistance);
        return true;
    }

    private Quaternion ClampLookDirectionToBase(Vector3 lookDirection)
    {
        Vector3 planarDirection = new Vector3(lookDirection.x, 0f, lookDirection.z);
        float planarMagnitude = planarDirection.magnitude;

        if (planarMagnitude < 0.0001f)
        {
            return GetBaseRotation();
        }

        float desiredYawDegrees = Mathf.Atan2(lookDirection.x, lookDirection.z) * Mathf.Rad2Deg;
        float desiredPitchDegrees = Mathf.Atan2(-lookDirection.y, planarMagnitude) * Mathf.Rad2Deg;

        float yawOffset = Mathf.DeltaAngle(baseYawDegrees, desiredYawDegrees);
        float clampedYawDegrees = baseYawDegrees + Mathf.Clamp(yawOffset, -maxRotationDegrees, maxRotationDegrees);
        float clampedPitchDegrees = basePitchDegrees + Mathf.Clamp(desiredPitchDegrees - basePitchDegrees, -maxRotationDegrees, maxRotationDegrees);
        return GetRotationFromYawPitch(clampedYawDegrees, clampedPitchDegrees);
    }

    private static Quaternion GetRotationFromYawPitch(float yawDegrees, float pitchDegrees)
    {
        return Quaternion.Euler(pitchDegrees, yawDegrees, 0f);
    }
}
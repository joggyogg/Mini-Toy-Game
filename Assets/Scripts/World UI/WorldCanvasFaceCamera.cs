using UnityEngine;

/// <summary>
/// Plain-English purpose:
/// Attach this to a world-space canvas or menu root so it always faces the camera.
///
/// This is useful for player-attached menus that should remain readable from the current game camera without manually
/// rotating the canvas every time the camera moves.
/// </summary>
public class WorldCanvasFaceCamera : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera targetCamera;

    [Header("Options")]
    [SerializeField] private bool lockXRotation;
    [SerializeField] private bool lockYRotation;
    [SerializeField] private bool lockZRotation;

    private Canvas cachedCanvas;

    private void Awake()
    {
        cachedCanvas = GetComponent<Canvas>();
    }

    private void LateUpdate()
    {
        Camera activeCamera = targetCamera != null ? targetCamera : Camera.main;

        // Camera.main requires the "MainCamera" tag. Fall back to any enabled camera.
        if (activeCamera == null)
        {
            foreach (Camera cam in Camera.allCameras)
            {
                if (cam.isActiveAndEnabled) { activeCamera = cam; break; }
            }
        }

        if (activeCamera == null)
        {
            return;
        }

        if (cachedCanvas != null && cachedCanvas.renderMode == RenderMode.WorldSpace && cachedCanvas.worldCamera != activeCamera)
        {
            cachedCanvas.worldCamera = activeCamera;
        }

        Vector3 directionToCamera = activeCamera.transform.position - transform.position;
        if (directionToCamera.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(-directionToCamera.normalized, activeCamera.transform.up);
        Vector3 eulerAngles = targetRotation.eulerAngles;
        Vector3 currentEulerAngles = transform.rotation.eulerAngles;

        if (lockXRotation)
        {
            eulerAngles.x = currentEulerAngles.x;
        }

        if (lockYRotation)
        {
            eulerAngles.y = currentEulerAngles.y;
        }

        if (lockZRotation)
        {
            eulerAngles.z = currentEulerAngles.z;
        }

        transform.rotation = Quaternion.Euler(eulerAngles);
    }
}
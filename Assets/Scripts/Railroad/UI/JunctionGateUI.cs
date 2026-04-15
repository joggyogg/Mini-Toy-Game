using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders a radial gate wheel for a junction via a GPU shader
/// displayed by a sibling RawImage on a world-space Canvas.
/// Each of 4 quadrants represents a direction group.
/// Active-gate pips are highlighted; the whole wheel rotates
/// by the junction's orientation so axes match the track layout.
/// Uses a vector shader — resolution-independent and crisp at any distance.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class JunctionGateUI : MonoBehaviour
{
    private RailNode _node;
    private bool _dirty = true;
    private float _paintedOrientRad = float.MaxValue;

    private RawImage _raw;
    private Material _mat;

    private static readonly int PropGroupAngles    = Shader.PropertyToID("_GroupAngles");
    private static readonly int PropBoundaryAngles = Shader.PropertyToID("_BoundaryAngles");
    private static readonly int PropExitCounts     = Shader.PropertyToID("_ExitCounts");
    private static readonly int PropActiveIndices  = Shader.PropertyToID("_ActiveIndices");
    private static readonly int PropJunctionId     = Shader.PropertyToID("_JunctionId");

    public void Initialize(RailNode railNode)
    {
        _node  = railNode;
        _dirty = true;
    }

    public void SetDirty() => _dirty = true;

    private void Awake()
    {
        _raw = GetComponent<RawImage>();
        var shader = Shader.Find("UI/JunctionGateWheel");
        _mat = new Material(shader);
        _raw.material = _mat;
        _raw.texture  = Texture2D.whiteTexture;
    }

    private void OnDestroy()
    {
        if (_mat != null) Destroy(_mat);
    }

    private Camera _cachedCam;

    private void LateUpdate()
    {
        if (_node == null) return;

        // Cache camera reference once per frame for projection helpers.
        _cachedCam = Camera.main;
        if (_cachedCam == null)
            foreach (Camera c in Camera.allCameras)
                if (c.isActiveAndEnabled) { _cachedCam = c; break; }

        // Recompute screen-space orientation every frame so the wheel stays
        // aligned with the tracks regardless of camera position or heading.
        float newOrient = ComputeScreenOrientRad();
        if (!_dirty && Mathf.Abs(Mathf.DeltaAngle(
                newOrient * Mathf.Rad2Deg, _paintedOrientRad * Mathf.Rad2Deg)) > 1f)
            _dirty = true;

        if (!_dirty) return;
        _dirty = false;
        _paintedOrientRad = newOrient;
        UpdateShaderParams();
    }

    /// <summary>
    /// Projects world-north (Vector3.forward / +Z) into screen space.
    /// Since junctions now use absolute world cardinals, group 0 is always north.
    /// </summary>
    private float ComputeScreenOrientRad()
    {
        return ComputeScreenAngleForDirection(Vector3.forward);
    }

    /// <summary>
    /// Projects an arbitrary XZ world direction into the texture's 2D
    /// coordinate system, accounting for the canvas billboard orientation
    /// and the vertical mirror applied after painting.
    /// Computes the canvas rotation directly from the camera position so
    /// the result is independent of script execution order.
    /// </summary>
    private float ComputeScreenAngleForDirection(Vector3 worldDir)
    {
        Camera cam = _cachedCam;
        if (cam == null) return 0f;

        // Compute the canvas orientation ourselves (same as WorldCanvasFaceCamera)
        // so we don't depend on LateUpdate execution order.
        Vector3 dirToCamera = cam.transform.position - _node.worldPosition;
        if (dirToCamera.sqrMagnitude < 0.001f) return 0f;

        Quaternion canvasRot = Quaternion.LookRotation(-dirToCamera.normalized, cam.transform.up);
        Vector3 canvasRight = canvasRot * Vector3.right;
        Vector3 canvasUp    = canvasRot * Vector3.up;

        float localX = Vector3.Dot(worldDir, canvasRight);
        float localY = Vector3.Dot(worldDir, canvasUp);

        if (localX * localX + localY * localY < 0.0001f) return 0f;

        return Mathf.Atan2(localY, localX);
    }

    // ─── Shader parameter upload ─────────────────────────────────────────

    private void UpdateShaderParams()
    {
        // Compute actual screen-space angle for each group center direction.
        Vector4 groupAngles = Vector4.zero;
        for (int g = 0; g < 4; g++)
        {
            float deg = _node.GroupCenterAngle(g);
            float rad = deg * Mathf.Deg2Rad;
            Vector3 worldDir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            groupAngles[g] = ComputeScreenAngleForDirection(worldDir);
        }

        // Compute boundary angles (midpoint between adjacent groups in screen space).
        Vector4 boundaryAngles = Vector4.zero;
        for (int g = 0; g < 4; g++)
        {
            int next = (g + 1) % 4;
            float a = groupAngles[g];
            float b = groupAngles[next];
            float delta = Mathf.DeltaAngle(a * Mathf.Rad2Deg, b * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            boundaryAngles[g] = a + delta * 0.5f;
        }

        // Pre-compute exit counts and active indices per group.
        Vector4 exitCounts = Vector4.zero;
        Vector4 activeIndices = Vector4.zero;
        for (int g = 0; g < 4; g++)
        {
            var exits = _node.GetGroupExits(g);
            int count = exits != null ? exits.Count : 0;
            exitCounts[g] = count;
            activeIndices[g] = count > 0 ? _node.gateIndices[g] % count : 0;
        }

        _mat.SetVector(PropGroupAngles,    groupAngles);
        _mat.SetVector(PropBoundaryAngles, boundaryAngles);
        _mat.SetVector(PropExitCounts,     exitCounts);
        _mat.SetVector(PropActiveIndices,  activeIndices);
        _mat.SetFloat(PropJunctionId,      _node.junctionId);
    }
}

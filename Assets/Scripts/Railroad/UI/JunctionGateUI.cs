using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders a radial gate wheel for a junction onto a Texture2D
/// displayed by a sibling RawImage on a world-space Canvas.
/// Each of 4 quadrants represents a direction group.
/// Active-gate pips are highlighted; the whole wheel rotates
/// by the junction's orientation so axes match the track layout.
/// Uses CPU pixel drawing — works on all render pipelines.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class JunctionGateUI : MonoBehaviour
{
    private RailNode _node;
    private bool _dirty = true;
    private float _paintedOrientRad = float.MaxValue;

    private Texture2D _tex;
    private RawImage _raw;
    private Color[] _pixels;

    private const int Size = 256;

    // All radii are in 0..0.5 space (0.5 = half-texture from centre).
    private const float WheelRadius  = 0.38f;
    private const float InnerRadius  = 0.14f;
    private const float CenterRadius = 0.06f;
    private const float PipRadius    = 0.040f;  // slightly smaller so 3 pips fit cleanly
    private const float GroupGap     = 0.07f;  // radians blanked at each group boundary

    private static readonly Color[] GroupColors =
    {
        new Color(1.00f, 0.85f, 0.10f, 1f),  // 0 Yellow
        new Color(0.10f, 0.70f, 0.15f, 1f),  // 1 Green
        new Color(0.10f, 0.50f, 0.85f, 1f),  // 2 Blue
        new Color(0.95f, 0.30f, 0.10f, 1f),  // 3 Orange/Red
    };

    private static readonly Color ColInactiveArc = new Color(0.82f, 0.82f, 0.80f, 1f);
    private static readonly Color ColEmptyGroup  = new Color(0.70f, 0.70f, 0.70f, 0.6f);
    private static readonly Color ColGap         = new Color(0.93f, 0.93f, 0.91f, 1f);
    private static readonly Color ColBackground  = new Color(0.96f, 0.96f, 0.94f, 1f);
    private static readonly Color ColCenter      = new Color(0.30f, 0.30f, 0.30f, 1f);
    private static readonly Color ColInactivePip = new Color(0.55f, 0.55f, 0.55f, 1f);

    public void Initialize(RailNode railNode)
    {
        _node  = railNode;
        _dirty = true;
    }

    public void SetDirty() => _dirty = true;

    private void Awake()
    {
        _raw    = GetComponent<RawImage>();
        _pixels = new Color[Size * Size];
        _tex    = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp
        };
        _raw.texture = _tex;
    }

    private void OnDestroy()
    {
        if (_tex != null) Destroy(_tex);
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
        Paint(newOrient);
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
    /// Projects an arbitrary XZ world direction from the junction's position
    /// into screen space and returns the angle (Atan2(screenY, screenX)).
    /// </summary>
    private float ComputeScreenAngleForDirection(Vector3 worldDir)
    {
        Camera cam = _cachedCam;
        if (cam == null) return 0f;

        Vector3 origin  = _node.worldPosition;
        Vector3 tip     = origin + worldDir;
        Vector3 oScreen = cam.WorldToScreenPoint(origin);
        Vector3 tScreen = cam.WorldToScreenPoint(tip);

        if (oScreen.z < 0f || tScreen.z < 0f) return 0f;

        Vector2 dir = new Vector2(tScreen.x - oScreen.x, tScreen.y - oScreen.y);
        if (dir.sqrMagnitude < 0.001f) return 0f;

        return Mathf.Atan2(dir.y, dir.x);
    }

    // ─── Rendering ───────────────────────────────────────────────────────────

    private void Paint(float orientRad)
    {

        // Pass 1 – per-pixel arc colouring
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float fx   = (x + 0.5f) / Size - 0.5f;  // [-0.5 .. 0.5]
            float fy   = (y + 0.5f) / Size - 0.5f;
            float dist = Mathf.Sqrt(fx * fx + fy * fy);

            Color c = Color.clear;

            if (dist <= WheelRadius)
            {
                c = ColBackground;

                if (dist >= InnerRadius)
                {
                    // angle rotated by orientation, offset +PI/4 so group 0 centre = east
                    float a       = Mathf.Atan2(fy, fx) - orientRad;
                    float norm    = Mathf.Repeat(a + Mathf.PI * 0.25f, Mathf.PI * 2f);
                    float inGroup = Mathf.Repeat(norm, Mathf.PI * 0.5f);
                    bool  inGap   = inGroup < GroupGap * 0.5f ||
                                    inGroup > Mathf.PI * 0.5f - GroupGap * 0.5f;

                    if (inGap)
                    {
                        c = ColGap;
                    }
                    else
                    {
                        int group = Mathf.FloorToInt(norm / (Mathf.PI * 0.5f)) % 4;
                        c = _node.GetGroupExitCount(group) == 0 ? ColEmptyGroup : ColInactiveArc;
                    }
                }
            }

            _pixels[y * Size + x] = c;
        }

        // Pass 2 – pip dots at 3 fixed positions per group (left / center / right).
        //          Positions are at fixed angular offsets from the group center in
        //          world space, so they never drift regardless of exit angle.
        //          Exit rank from GetGroupExits (sorted by angle) maps to position:
        //            1 exit  → center
        //            2 exits → left, right
        //            3 exits → left, center, right
        float midR = (InnerRadius + WheelRadius) * 0.5f;
        const float pipOffsetDeg = 30f; // degrees from group center for left/right pips

        for (int g = 0; g < RailNode.NumDirectionGroups; g++)
        {
            var exits = _node.GetGroupExits(g);
            int count = exits != null ? exits.Count : 0;
            if (count == 0) continue;

            int activeIdx = _node.gateIndices[g] % count;

            // Fixed world-space directions for the 3 pip positions.
            float centerDeg = _node.GroupCenterAngle(g);
            float leftRad  = (centerDeg - pipOffsetDeg) * Mathf.Deg2Rad;
            float centerRad = centerDeg * Mathf.Deg2Rad;
            float rightRad = (centerDeg + pipOffsetDeg) * Mathf.Deg2Rad;

            Vector3 leftDir  = new Vector3(Mathf.Sin(leftRad),  0f, Mathf.Cos(leftRad));
            Vector3 centerDir = new Vector3(Mathf.Sin(centerRad), 0f, Mathf.Cos(centerRad));
            Vector3 rightDir = new Vector3(Mathf.Sin(rightRad), 0f, Mathf.Cos(rightRad));

            // Map exits to positions by count.
            Vector3[] pipDirs;
            if (count == 1)
                pipDirs = new[] { centerDir };
            else if (count == 2)
                pipDirs = new[] { leftDir, rightDir };
            else
                pipDirs = new[] { leftDir, centerDir, rightDir };

            for (int p = 0; p < count; p++)
            {
                float screenAngle = ComputeScreenAngleForDirection(pipDirs[p]);
                float cx = 0.5f + Mathf.Cos(screenAngle) * midR;
                float cy = 0.5f - Mathf.Sin(screenAngle) * midR;
                Color col = (p == activeIdx) ? GroupColors[g] : ColInactivePip;
                FillCircle(cx, cy, PipRadius, col);
            }
        }

        // Pass 3 – centre dot
        FillCircle(0.5f, 0.5f, CenterRadius, ColCenter);

        // Mirror vertically — swap rows so the texture reads correctly
        // when the canvas faces the camera.
        for (int row = 0; row < Size / 2; row++)
        {
            int rowA = row * Size;
            int rowB = (Size - 1 - row) * Size;
            for (int x = 0; x < Size; x++)
            {
                Color tmp        = _pixels[rowA + x];
                _pixels[rowA + x] = _pixels[rowB + x];
                _pixels[rowB + x] = tmp;
            }
        }

        _tex.SetPixels(_pixels);
        _tex.Apply(false);
    }

    /// <summary>cx01, cy01, r01 are all in 0..1 normalised texture space.</summary>
    private void FillCircle(float cx01, float cy01, float r01, Color col)
    {
        int cx  = Mathf.RoundToInt(cx01 * Size);
        int cy  = Mathf.RoundToInt(cy01 * Size);
        int r   = Mathf.CeilToInt(r01  * Size);
        int rSq = r * r;

        for (int dy = -r; dy <= r; dy++)
        for (int dx = -r; dx <= r; dx++)
        {
            if (dx * dx + dy * dy > rSq) continue;
            int px = cx + dx, py = cy + dy;
            if ((uint)px >= (uint)Size || (uint)py >= (uint)Size) continue;
            _pixels[py * Size + px] = col;
        }
    }
}

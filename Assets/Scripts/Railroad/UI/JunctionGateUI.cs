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

    private void LateUpdate()
    {
        if (_node == null) return;

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
    /// Projects the junction's principal axis direction (group 0) into screen
    /// space and returns the resulting angle in standard math convention
    /// (Atan2(screenY, screenX), 0 = screen-right, CCW positive).
    /// This automatically accounts for any camera heading or pitch.
    /// </summary>
    private float ComputeScreenOrientRad()
    {
        Camera cam = Camera.main;
        if (cam == null)
            foreach (Camera c in Camera.allCameras)
                if (c.isActiveAndEnabled) { cam = c; break; }
        if (cam == null) return 0f;

        float worldRad = _node.junctionOrientation * Mathf.Deg2Rad;
        // Group 0 world direction (game convention: 0° = north = +Z)
        Vector3 worldDir = new Vector3(Mathf.Sin(worldRad), 0f, Mathf.Cos(worldRad));

        // Project two world points: junction centre and a point 1 unit along group-0.
        Vector3 origin  = _node.worldPosition;
        Vector3 tip     = origin + worldDir;
        Vector3 oScreen = cam.WorldToScreenPoint(origin);
        Vector3 tScreen = cam.WorldToScreenPoint(tip);

        // If either point is behind the camera, fall back to 0.
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

        // Pass 2 – pip dots
        for (int g = 0; g < RailNode.NumDirectionGroups; g++)
        {
            var exits = _node.GetGroupExits(g);
            if (exits == null || exits.Count == 0) continue;

            int   active     = _node.gateIndices[g] % exits.Count;
            float groupAngle = orientRad + g * Mathf.PI * 0.5f;
            float midR       = (InnerRadius + WheelRadius) * 0.5f;

            // Angular radius of one pip at midR, then centre-to-centre spacing
            // = 3 pip radii (one diameter + one pip gap).
            float pipAngR    = PipRadius / midR;
            float pipSpacing = pipAngR * 3f;

            // Half-spread grows with exit count but is capped so the outermost
            // pip stays at least one pip-diameter away from the adjacent group.
            float maxHalfSpread = Mathf.PI * 0.25f - pipAngR * 2f;
            float halfSpread    = exits.Count <= 1
                ? 0f
                : Mathf.Min((exits.Count - 1) * pipSpacing * 0.5f, maxHalfSpread);

            for (int e = 0; e < exits.Count; e++)
            {
                float t     = exits.Count == 1 ? 0f : Mathf.Lerp(-1f, 1f, (float)e / (exits.Count - 1));
                float angle = groupAngle + t * halfSpread;

                float cx = 0.5f + Mathf.Cos(angle) * midR;
                float cy = 0.5f + Mathf.Sin(angle) * midR;

                FillCircle(cx, cy, PipRadius, e == active ? GroupColors[g] : ColInactivePip);
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

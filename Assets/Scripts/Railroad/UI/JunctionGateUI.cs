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
    private const float WheelRadius    = 0.46f;
    private const float BorderWidth    = 0.025f;
    private const float ArcOuterR      = WheelRadius - BorderWidth;
    private const float ArcInnerR      = 0.22f;
    private const float GroupGapHalfDeg = 3f;    // half-gap at each group boundary (degrees)
    private const float PipGapDeg      = 2.5f;   // gap between pips within a group (degrees)
    private const float PaleBlend      = 0.68f;   // blend toward white for inactive pips

    private static readonly Color[] GroupColors =
    {
        new Color(1.00f, 0.85f, 0.10f, 1f),  // 0 Yellow
        new Color(0.10f, 0.70f, 0.15f, 1f),  // 1 Green
        new Color(0.10f, 0.50f, 0.85f, 1f),  // 2 Blue
        new Color(0.95f, 0.30f, 0.10f, 1f),  // 3 Orange/Red
    };

    private static readonly Color ColCenter = new Color(0.30f, 0.30f, 0.30f, 1f);

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

    // ─── Rendering ───────────────────────────────────────────────────────────

    private void Paint(float orientRad)
    {
        // Compute actual screen-space angle for each group center direction.
        float[] groupScreenAngles = new float[4];
        for (int g = 0; g < 4; g++)
        {
            float deg = _node.GroupCenterAngle(g);
            float rad = deg * Mathf.Deg2Rad;
            Vector3 worldDir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            groupScreenAngles[g] = ComputeScreenAngleForDirection(worldDir);
        }

        // Compute boundary angles (midpoint between adjacent groups in screen space).
        float[] boundaryAngles = new float[4];
        for (int g = 0; g < 4; g++)
        {
            int next = (g + 1) % 4;
            float a = groupScreenAngles[g];
            float b = groupScreenAngles[next];
            float delta = Mathf.DeltaAngle(a * Mathf.Rad2Deg, b * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            boundaryAngles[g] = a + delta * 0.5f;
        }

        // Pre-compute exit counts and active indices per group.
        int[] exitCounts = new int[4];
        int[] activeIndices = new int[4];
        for (int g = 0; g < 4; g++)
        {
            var exits = _node.GetGroupExits(g);
            exitCounts[g] = exits != null ? exits.Count : 0;
            activeIndices[g] = exitCounts[g] > 0 ? _node.gateIndices[g] % exitCounts[g] : 0;
        }

        // Single pass – per-pixel arc-segment colouring.
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float fx   = (x + 0.5f) / Size - 0.5f;
            float fy   = (y + 0.5f) / Size - 0.5f;
            float dist = Mathf.Sqrt(fx * fx + fy * fy);

            Color c = Color.clear;

            if (dist <= WheelRadius)
            {
                c = Color.white; // white for border ring, center, and gaps

                if (dist >= ArcInnerR && dist <= ArcOuterR)
                {
                    float pixelAngle = Mathf.Atan2(-fy, fx);
                    float pixelDeg = pixelAngle * Mathf.Rad2Deg;

                    // Find nearest group.
                    int bestGroup = 0;
                    float bestDelta = float.MaxValue;
                    for (int g = 0; g < 4; g++)
                    {
                        float d = Mathf.Abs(Mathf.DeltaAngle(
                            pixelDeg, groupScreenAngles[g] * Mathf.Rad2Deg));
                        if (d < bestDelta) { bestDelta = d; bestGroup = g; }
                    }

                    // Check boundary gaps.
                    bool inGap = false;
                    for (int g = 0; g < 4; g++)
                    {
                        float distToBound = Mathf.Abs(Mathf.DeltaAngle(
                            pixelDeg, boundaryAngles[g] * Mathf.Rad2Deg));
                        if (distToBound < GroupGapHalfDeg) { inGap = true; break; }
                    }

                    if (!inGap && exitCounts[bestGroup] > 0)
                    {
                        int N = exitCounts[bestGroup];
                        float centerDeg = groupScreenAngles[bestGroup] * Mathf.Rad2Deg;

                        // Compute available half-span from center to boundary.
                        float halfSpanDeg = Mathf.Abs(Mathf.DeltaAngle(
                            centerDeg, boundaryAngles[bestGroup] * Mathf.Rad2Deg));
                        float availHalfDeg = halfSpanDeg - GroupGapHalfDeg;

                        // Signed delta from group center.
                        float deltaDeg = Mathf.DeltaAngle(centerDeg, pixelDeg);

                        if (Mathf.Abs(deltaDeg) <= availHalfDeg)
                        {
                            // Distribute N pips symmetrically around center.
                            float totalSpanDeg = availHalfDeg * 2f;
                            float totalGapsDeg = (N - 1) * PipGapDeg;
                            float pipWidthDeg = (totalSpanDeg - totalGapsDeg) / N;
                            float localPos = deltaDeg + availHalfDeg; // 0..totalSpanDeg

                            float accum = 0f;
                            int pipIdx = -1;
                            for (int p = 0; p < N; p++)
                            {
                                if (localPos >= accum && localPos < accum + pipWidthDeg)
                                {
                                    pipIdx = p;
                                    break;
                                }
                                accum += pipWidthDeg + PipGapDeg;
                            }

                            if (pipIdx >= 0)
                            {
                                Color vivid = GroupColors[bestGroup];
                                c = ((N - 1 - pipIdx) == activeIndices[bestGroup])
                                    ? vivid
                                    : Color.Lerp(vivid, Color.white, PaleBlend);
                            }
                            // else in pip gap → stays white
                        }
                    }
                    // else no exits or in boundary gap → stays white
                }
            }

            _pixels[y * Size + x] = c;
        }

        // Junction ID number in the center.
        if (_node.junctionId >= 0)
            DrawNumber(_node.junctionId, Size / 2, Size / 2, 2, ColCenter);

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

    // ─── Tiny bitmap digit renderer ──────────────────────────────────────

    // 3×5 bitmap font for digits 0-9 (each row is 3 bits, MSB-left).
    private static readonly byte[][] DigitBitmaps = new byte[][]
    {
        new byte[] { 0b111, 0b101, 0b101, 0b101, 0b111 }, // 0
        new byte[] { 0b010, 0b110, 0b010, 0b010, 0b111 }, // 1
        new byte[] { 0b111, 0b001, 0b111, 0b100, 0b111 }, // 2
        new byte[] { 0b111, 0b001, 0b111, 0b001, 0b111 }, // 3
        new byte[] { 0b101, 0b101, 0b111, 0b001, 0b001 }, // 4
        new byte[] { 0b111, 0b100, 0b111, 0b001, 0b111 }, // 5
        new byte[] { 0b111, 0b100, 0b111, 0b101, 0b111 }, // 6
        new byte[] { 0b111, 0b001, 0b010, 0b010, 0b010 }, // 7
        new byte[] { 0b111, 0b101, 0b111, 0b101, 0b111 }, // 8
        new byte[] { 0b111, 0b101, 0b111, 0b001, 0b111 }, // 9
    };

    private void DrawNumber(int number, int cx, int cy, int scale, Color col)
    {
        string digits = number.ToString();
        int digitW = 3 * scale + scale; // 3 pixels wide + 1 pixel gap, scaled
        int totalW = digits.Length * digitW - scale; // no gap after last
        int startX = cx - totalW / 2;
        int startY = cy - (5 * scale) / 2;

        for (int d = 0; d < digits.Length; d++)
        {
            int digit = digits[d] - '0';
            if (digit < 0 || digit > 9) continue;
            DrawDigit(digit, startX + d * digitW, startY, scale, col);
        }
    }

    private void DrawDigit(int digit, int x0, int y0, int scale, Color col)
    {
        byte[] bmp = DigitBitmaps[digit];
        for (int row = 0; row < 5; row++)
        {
            for (int bit = 0; bit < 3; bit++)
            {
                if ((bmp[row] & (1 << (2 - bit))) == 0) continue;
                for (int sy = 0; sy < scale; sy++)
                for (int sx = 0; sx < scale; sx++)
                {
                    int px = x0 + bit * scale + sx;
                    int py = y0 + row * scale + sy;
                    if ((uint)px < (uint)Size && (uint)py < (uint)Size)
                        _pixels[py * Size + px] = col;
                }
            }
        }
    }
}

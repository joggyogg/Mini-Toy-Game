using System.Collections.Generic;
using UnityEngine;

public enum EaseMode
{
    Linear,
    EaseIn,
    EaseOut,
    EaseInOut,
    Custom
}

/// <summary>
/// A freehand-drawn gradient line on the terrain chunk map.
/// Each line carries its own noise parameters (amplitude, period, etc.) that
/// interpolate from start to end along its length. The base terrain is flat;
/// all shaping comes from gradient lines.
/// </summary>
[System.Serializable]
public class GradientLine
{
    [Tooltip("Polyline points in tile-space (full-tile float coordinates).")]
    public List<Vector2> points = new List<Vector2>();

    [Tooltip("Half-width of influence in tiles. Points farther than this are unaffected.")]
    public float influenceHalfWidth = 5f;

    [Tooltip("Falloff shape from centre to edge of influence.")]
    public EaseMode falloffEase = EaseMode.Linear;

    // ── Noise sampling frequency ────────────────────────────────────────────────
    [Tooltip("Perlin noise sampling frequency (lower = larger features).")]
    public float perlinScale = 0.05f;

    // ── Per-parameter start/end values ──────────────────────────────────────────

    public float amplitudeStart = 1f;
    public float amplitudeEnd = 1f;
    public EaseMode amplitudeEase = EaseMode.Linear;
    public AnimationCurve amplitudeCustomCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    public float periodStart = 1f;
    public float periodEnd = 1f;
    public EaseMode periodEase = EaseMode.Linear;
    public AnimationCurve periodCustomCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    public float heightContributionStart = 4f;
    public float heightContributionEnd = 4f;
    public EaseMode heightContributionEase = EaseMode.Linear;
    public AnimationCurve heightContributionCustomCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    public float octavesStart = 3f;
    public float octavesEnd = 3f;
    public EaseMode octavesEase = EaseMode.Linear;
    public AnimationCurve octavesCustomCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    public float persistenceStart = 0.45f;
    public float persistenceEnd = 0.45f;
    public EaseMode persistenceEase = EaseMode.Linear;
    public AnimationCurve persistenceCustomCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    public float lacunarityStart = 2f;
    public float lacunarityEnd = 2f;
    public EaseMode lacunarityEase = EaseMode.Linear;
    public AnimationCurve lacunarityCustomCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    public float baseHeightStart = 0f;
    public float baseHeightEnd = 0f;
    public EaseMode baseHeightEase = EaseMode.Linear;
    public AnimationCurve baseHeightCustomCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    /// <summary>Deep-copy this gradient line.</summary>
    public GradientLine Duplicate()
    {
        return JsonUtility.FromJson<GradientLine>(JsonUtility.ToJson(this));
    }

    /// <summary>Interpolate between start and end using the given ease mode at position t (0-1).</summary>
    public static float LerpParam(float start, float end, float t, EaseMode ease = EaseMode.Linear, AnimationCurve customCurve = null)
    {
        float easedT;
        switch (ease)
        {
            case EaseMode.EaseIn:
                easedT = t * t;
                break;
            case EaseMode.EaseOut:
                easedT = 1f - (1f - t) * (1f - t);
                break;
            case EaseMode.EaseInOut:
                easedT = t < 0.5f ? 2f * t * t : 1f - 2f * (1f - t) * (1f - t);
                break;
            case EaseMode.Custom:
                easedT = customCurve != null ? customCurve.Evaluate(t) : t;
                break;
            default: // Linear
                easedT = t;
                break;
        }
        return Mathf.Lerp(start, end, easedT);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    /// <summary>Total arc-length of the polyline in tile units.</summary>
    public float GetTotalLength()
    {
        float len = 0f;
        for (int i = 1; i < points.Count; i++)
            len += Vector2.Distance(points[i - 1], points[i]);
        return len;
    }

    /// <summary>
    /// Projects a tile-space point onto the polyline.
    /// Returns (t, dist) where t is 0–1 parametric position along the line
    /// and dist is the perpendicular distance from the point to the line.
    /// </summary>
    public void ProjectPoint(Vector2 point, out float t, out float dist)
    {
        t = 0f;
        dist = float.MaxValue;

        if (points.Count == 0) return;
        if (points.Count == 1)
        {
            dist = Vector2.Distance(point, points[0]);
            t = 0f;
            return;
        }

        float totalLength = GetTotalLength();
        if (totalLength < 0.0001f)
        {
            dist = Vector2.Distance(point, points[0]);
            t = 0f;
            return;
        }

        float accumulatedLength = 0f;
        float bestT = 0f;
        float bestDist = float.MaxValue;

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[i + 1];
            float segLen = Vector2.Distance(a, b);

            // Project point onto segment [a, b]
            Vector2 ab = b - a;
            float segParam = 0f;
            if (segLen > 0.0001f)
                segParam = Mathf.Clamp01(Vector2.Dot(point - a, ab) / (segLen * segLen));

            Vector2 closest = a + ab * segParam;
            float d = Vector2.Distance(point, closest);

            if (d < bestDist)
            {
                bestDist = d;
                bestT = (accumulatedLength + segParam * segLen) / totalLength;
            }

            accumulatedLength += segLen;
        }

        t = bestT;
        dist = bestDist;
    }

    /// <summary>
    /// Simplifies the polyline using the Ramer-Douglas-Peucker algorithm.
    /// </summary>
    public static List<Vector2> SimplifyPolyline(List<Vector2> pts, float tolerance)
    {
        if (pts == null || pts.Count < 3) return new List<Vector2>(pts ?? new List<Vector2>());

        var result = new List<Vector2>();
        RDPRecursive(pts, 0, pts.Count - 1, tolerance, result);
        result.Add(pts[pts.Count - 1]);
        return result;
    }

    private static void RDPRecursive(List<Vector2> pts, int startIdx, int endIdx, float tolerance, List<Vector2> output)
    {
        if (endIdx <= startIdx + 1)
        {
            output.Add(pts[startIdx]);
            return;
        }

        Vector2 start = pts[startIdx];
        Vector2 end = pts[endIdx];
        float maxDist = 0f;
        int farthestIdx = startIdx;

        Vector2 lineDir = end - start;
        float lineLen = lineDir.magnitude;

        for (int i = startIdx + 1; i < endIdx; i++)
        {
            float d;
            if (lineLen < 0.0001f)
            {
                d = Vector2.Distance(pts[i], start);
            }
            else
            {
                float param = Vector2.Dot(pts[i] - start, lineDir) / (lineLen * lineLen);
                param = Mathf.Clamp01(param);
                Vector2 proj = start + lineDir * param;
                d = Vector2.Distance(pts[i], proj);
            }

            if (d > maxDist)
            {
                maxDist = d;
                farthestIdx = i;
            }
        }

        if (maxDist > tolerance)
        {
            RDPRecursive(pts, startIdx, farthestIdx, tolerance, output);
            RDPRecursive(pts, farthestIdx, endIdx, tolerance, output);
        }
        else
        {
            output.Add(pts[startIdx]);
        }
    }
}

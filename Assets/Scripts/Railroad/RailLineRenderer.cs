using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Evaluates splines from a RailNetworkAuthoring and renders each as its own LineRenderer.
/// Attach to the same GameObject as RailNetworkAuthoring (or a child).
/// </summary>
public class RailLineRenderer : MonoBehaviour
{
    [SerializeField] private int samplesPerSegment = 20;
    [SerializeField] private float lineWidth = 0.15f;
    [SerializeField] private float heightOffset = 0.05f;
    [SerializeField] private Color lineColor = Color.black;

    private readonly List<LineRenderer> lineRenderers = new List<LineRenderer>();
    private Material lineMaterial;

    private void Awake()
    {
        lineMaterial = new Material(Shader.Find("Sprites/Default"));
    }

    /// <summary>
    /// Rebuilds all line renderers from splines in the network.
    /// One LineRenderer per spline so they don't visually connect.
    /// </summary>
    public void RebuildFromSplines(RailNetworkAuthoring network)
    {
        int neededCount = 0;
        if (network != null)
        {
            for (int s = 0; s < network.SplineCount; s++)
            {
                if (network.Container.Splines[s].Count >= 2)
                    neededCount++;
            }
        }

        // Create or destroy child LineRenderers to match spline count.
        while (lineRenderers.Count < neededCount)
        {
            var obj = new GameObject($"Rail Line {lineRenderers.Count}");
            obj.transform.SetParent(transform);
            var lr = obj.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.material = lineMaterial;
            lr.startColor = lineColor;
            lr.endColor = lineColor;
            lr.positionCount = 0;
            lineRenderers.Add(lr);
        }
        while (lineRenderers.Count > neededCount)
        {
            int last = lineRenderers.Count - 1;
            if (lineRenderers[last] != null)
                Destroy(lineRenderers[last].gameObject);
            lineRenderers.RemoveAt(last);
        }

        // Fill each LineRenderer from its corresponding spline.
        int lrIndex = 0;
        if (network != null)
        {
            for (int s = 0; s < network.SplineCount; s++)
            {
                int knotCount = network.Container.Splines[s].Count;
                if (knotCount < 2) continue;

                int totalSamples = (knotCount - 1) * samplesPerSegment + 1;
                var positions = new Vector3[totalSamples];

                for (int i = 0; i < totalSamples; i++)
                {
                    float t = (float)i / (totalSamples - 1);
                    Vector3 pos = network.EvaluatePositionWorld(s, t);
                    pos.y += heightOffset;
                    positions[i] = pos;
                }

                lineRenderers[lrIndex].positionCount = totalSamples;
                lineRenderers[lrIndex].SetPositions(positions);
                lrIndex++;
            }
        }
    }
}

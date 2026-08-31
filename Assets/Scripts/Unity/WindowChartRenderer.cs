using UnityEngine;
using Kitesim.Core;

namespace Kitesim.Unity
{
    /// <summary>
    /// Draws the wind window as a half-disc: the flat bottom is the ground, the arc
    /// is the zero-power edge, and the concentric rings are iso-power contours. The
    /// power at any point depends only on its radius, so the rings are exact rather
    /// than decorative. See docs/kite-anatomy.md section 3.
    /// </summary>
    public class WindowChartRenderer : MonoBehaviour
    {
        [SerializeField] private int ringCount = 5;
        [SerializeField] private int segmentsPerRing = 128;
        [SerializeField] private float lineWidth = 0.006f;

        private WindField wind;
        private ChartProjection projection;

        public void Configure(WindField windField, ChartProjection chartProjection)
        {
            wind = windField;
            projection = chartProjection;
            Rebuild();
        }

        private void Rebuild()
        {
            foreach (Transform child in transform)
                Destroy(child.gameObject);

            float edge = (float)WindowChart.EdgeRadius(projection);

            // Iso-power rings at power = i/ringCount. The loop stops before
            // i == ringCount, which would be a degenerate ring of radius zero at the
            // window centre.
            for (int i = 1; i < ringCount; i++)
            {
                double power = (double)i / ringCount;
                double theta = MathD.Acos(MathD.Clamp01(power));
                float radius = (float)WindowChart.RadiusFromTheta(theta, projection);
                float intensity = 0.20f + 0.45f * (float)power;
                CreateArc($"Ring_{i}", radius, new Color(intensity, intensity * 0.75f, 0.15f, 1f));
            }

            CreateArc("Edge", edge, new Color(0.85f, 0.35f, 0.25f, 1f));
            CreateGroundLine(edge);
        }

        /// <summary>Upper half of a circle: the sky part of an iso-power contour.</summary>
        private void CreateArc(string label, float radius, Color color)
        {
            var line = CreateLine(label, color);
            int points = segmentsPerRing / 2 + 1;
            line.positionCount = points;
            for (int i = 0; i < points; i++)
            {
                float angle = Mathf.PI * i / (points - 1);
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }
        }

        private void CreateGroundLine(float edge)
        {
            var line = CreateLine("Ground", new Color(0.45f, 0.35f, 0.2f, 1f));
            line.positionCount = 2;
            line.SetPosition(0, new Vector3(-edge, 0f, 0f));
            line.SetPosition(1, new Vector3(edge, 0f, 0f));
        }

        private LineRenderer CreateLine(string label, Color color)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = color;
            line.endColor = color;
            return line;
        }
    }
}

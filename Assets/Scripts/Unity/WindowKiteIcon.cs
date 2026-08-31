using UnityEngine;
using Kitesim.Core;

namespace Kitesim.Unity
{
    /// <summary>
    /// One kite marker on the chart: a triangle pointing along the kite's heading,
    /// tinted by line tension, trailing its recent path.
    /// </summary>
    public class WindowKiteIcon : MonoBehaviour
    {
        [SerializeField] private float size = 0.045f;

        private WindowKiteBehaviour kite;
        private WindField wind;
        private ChartProjection projection;
        private Color baseColor;
        private MeshRenderer meshRenderer;

        public void Bind(WindowKiteBehaviour target, WindField windField,
            ChartProjection chartProjection, Color color)
        {
            kite = target;
            wind = windField;
            projection = chartProjection;
            baseColor = color;
            BuildTriangle();
        }

        private void BuildTriangle()
        {
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(0f, size, 0f),
                    new Vector3(-size * 0.62f, -size * 0.7f, 0f),
                    new Vector3(size * 0.62f, -size * 0.7f, 0f)
                },
                triangles = new[] { 0, 2, 1 }
            };
            mesh.RecalculateNormals();

            var filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.material = new Material(Shader.Find("Sprites/Default"));
            meshRenderer.material.color = baseColor;

            var trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 3f;
            trail.startWidth = 0.008f;
            trail.endWidth = 0f;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = baseColor;
            trail.endColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
        }

        void LateUpdate()
        {
            if (kite == null || kite.Sim == null)
                return;

            KiteStateView view = kite.View;
            ChartPoint point = WindowChart.Project(view.Direction, wind, projection);
            transform.localPosition = new Vector3((float)point.X, (float)point.Y, 0f);

            // Heading measured in the chart plane: psi = 0 points to the top of the
            // chart, +90 degrees to the right, which is the same convention the
            // chart itself uses.
            float headingDegrees = (float)(view.HeadingRad * MathD.Rad2Deg);
            transform.localRotation = Quaternion.Euler(0f, 0f, -headingDegrees);

            // Tension shading: pale when the kite is loafing at the edge, saturated
            // in the power zone.
            float load = Mathf.Clamp01((float)(view.Tension / 120.0));
            meshRenderer.material.color = Color.Lerp(
                new Color(baseColor.r, baseColor.g, baseColor.b, 0.35f), baseColor, load);
        }
    }
}

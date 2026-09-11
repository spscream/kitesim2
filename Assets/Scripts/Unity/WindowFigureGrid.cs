using UnityEngine;
using Kitesim.Core;

namespace Kitesim.Unity
{
    /// <summary>
    /// Draws the 45-degree ballet grid on the same sphere the kites fly on.
    /// Hidden with N; default on, because the trainer exists to learn figures.
    /// </summary>
    public class WindowFigureGrid : MonoBehaviour
    {
        [SerializeField] private int segments = 48;
        [SerializeField] private float lineWidth = 0.08f;

        private GameObject root;

        public bool Visible
        {
            get => root != null && root.activeSelf;
            set
            {
                if (root != null)
                    root.SetActive(value);
            }
        }

        public void Build(WindField wind, double lineLength)
        {
            root = new GameObject("FigureGrid");
            root.transform.SetParent(transform, false);

            var color = new Color(0.92f, 0.92f, 0.95f, 0.35f);
            foreach (double azimuthDeg in FigureGrid.AzimuthStepsDeg)
            {
                Draw(FigureGrid.Meridian(wind, azimuthDeg * MathD.Deg2Rad, lineLength, segments),
                    $"Meridian_{azimuthDeg:0}", color);
            }

            foreach (double elevationDeg in FigureGrid.ElevationStepsDeg)
            {
                Draw(FigureGrid.Elevation(wind, elevationDeg * MathD.Deg2Rad, lineLength, segments),
                    $"Elevation_{elevationDeg:0}", color);
            }
        }

        private void Draw(Vec3[] points, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = color;
            line.endColor = color;
            line.positionCount = points.Length;
            for (int i = 0; i < points.Length; i++)
                line.SetPosition(i, points[i].ToVector3());
        }
    }
}

using UnityEngine;
using Kitesim.Core;

namespace Kitesim.Unity
{
    /// <summary>
    /// One pilot standing on the field: a coloured disc at their feet and a post at
    /// standing height, in the pilot's colour.
    ///
    /// This is what makes walking visible. The window is measured from each pilot's
    /// own hands, so a walking pilot does not move on the chart at all -- their kite
    /// simply gains or loses power. Without a mark on the ground the geometry of the
    /// field is invisible, and walking upwind in light wind is a real technique, not
    /// a detail.
    ///
    /// The disc turns yellow at the inner boundary, three metres inside the red one,
    /// which is the warning line of ISKRB section IV.D.1: it means the pilot has run
    /// out of field, not that they have been penalised yet.
    /// </summary>
    public class WindowPilotMarker : MonoBehaviour
    {
        /// <summary>Metres inside the outer boundary where the yellow line runs.</summary>
        private const double InnerMargin = 3.0;

        [SerializeField] private float postHeight = 1.7f;
        [SerializeField] private float postRadius = 0.12f;
        [SerializeField] private float discRadius = 0.65f;

        private static readonly Color WarningColor = new Color(0.95f, 0.85f, 0.15f);

        private WindowKiteBehaviour kite;

        /// <summary>
        /// The disc's renderer, not its material. Reading Renderer.material hands out
        /// an instance and can leave a held reference pointing at a material the
        /// renderer no longer draws, which shows up as a colour change that silently
        /// does nothing. Going through the renderer every time cannot drift.
        /// </summary>
        private Renderer discRenderer;
        private Color restColor;
        private double innerLimit = double.MaxValue;
        private bool warned;

        /// <summary>
        /// Build the marker for one pilot. Markers never appear on the chart, and the
        /// lead pilot's own marker also stays out of the first-person view: both of
        /// those cameras stand where that pilot stands. See ViewLayers.
        /// </summary>
        public void Bind(WindowKiteBehaviour target, Color color, bool isLeadPilot)
        {
            kite = target;
            restColor = color;

            if (kite != null && kite.Sim != null)
                innerLimit = kite.Sim.Config.FieldHalfSize - InnerMargin;

            discRenderer = CreatePart("Disc", discRadius, 0.01f, 0.0f, color);
            CreatePart("Post", postRadius, postHeight * 0.5f, postHeight * 0.5f, color);

            ViewLayers.Apply(gameObject,
                isLeadPilot ? ViewLayers.PilotSelf : ViewLayers.PilotMarker);
        }

        private Renderer CreatePart(string label, float radius, float halfHeight, float centreHeight, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = label;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, centreHeight, 0f);
            go.transform.localScale = new Vector3(radius * 2f, halfHeight, radius * 2f);

            // Nothing in this scene uses physics, and the primitives ship with one.
            Destroy(go.GetComponent<Collider>());

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var renderer = go.GetComponent<Renderer>();
            renderer.material = new Material(shader) { color = color };
            return renderer;
        }

        void LateUpdate()
        {
            if (kite == null || kite.Sim == null)
                return;

            Vec3 pilot = kite.Sim.PilotPosition;
            transform.position = pilot.ToVector3();

            bool atBoundary = MathD.Abs(pilot.X) >= innerLimit || MathD.Abs(pilot.Z) >= innerLimit;
            if (atBoundary == warned || discRenderer == null)
                return;

            warned = atBoundary;
            discRenderer.material.color = atBoundary ? WarningColor : restColor;
        }
    }
}

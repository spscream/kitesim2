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

        /// <summary>
        /// Fallback height and thickness, used only when no pilot model is assigned.
        /// A post is enough to see where somebody stands, which is the job; the
        /// figure is the same information with a person's outline on it.
        /// </summary>
        [SerializeField] private float postHeight = 1.7f;
        [SerializeField] private float postRadius = 0.12f;
        /// <summary>
        /// The disc under the figure's feet. Smaller than it was when it stood alone
        /// under a post: with a person on it, a metre across is enough to read the
        /// colour and the boundary warning without looking like a puddle.
        /// </summary>
        [SerializeField] private float discRadius = 0.5f;

        private static readonly Color WarningColor = new Color(0.95f, 0.85f, 0.15f);

        private WindowKiteBehaviour kite;

        /// <summary>
        /// The disc's renderer, not its material. Reading Renderer.material hands out
        /// an instance and can leave a held reference pointing at a material the
        /// renderer no longer draws, which shows up as a colour change that silently
        /// does nothing. Going through the renderer every time cannot drift.
        /// </summary>
        private Renderer discRenderer;
        private Transform figure;
        private Color restColor;
        private double innerLimit = double.MaxValue;
        private bool warned;

        /// <summary>
        /// Build the marker for one pilot. Markers never appear on the chart, and the
        /// lead pilot's own marker also stays out of the first-person view: both of
        /// those cameras stand where that pilot stands. See ViewLayers.
        /// </summary>
        public void Bind(WindowKiteBehaviour target, GameObject pilotModel, Color color, bool isLeadPilot)
        {
            kite = target;
            restColor = color;

            if (kite != null && kite.Sim != null)
                innerLimit = kite.Sim.Config.FieldHalfSize - InnerMargin;

            discRenderer = CreatePart("Disc", discRadius, 0.01f, 0.0f, color);

            if (pilotModel != null)
                BuildFigure(pilotModel, color);
            else
                CreatePart("Post", postRadius, postHeight * 0.5f, postHeight * 0.5f, color);

            ViewLayers.Apply(gameObject,
                isLeadPilot ? ViewLayers.PilotSelf : ViewLayers.PilotMarker);
        }

        /// <summary>
        /// The generated pilot figure (Tools/blender/make_pilot.py), standing on its
        /// feet at the pilot's position and tinted in their colour. It is modelled
        /// facing +Z with its hands where WindowKiteMesh anchors the lines, so the
        /// figure has to turn with the kite for the two to agree -- which is also
        /// what a flier does.
        /// </summary>
        private void BuildFigure(GameObject pilotModel, Color color)
        {
            figure = Instantiate(pilotModel, transform).transform;
            figure.localPosition = Vector3.zero;
            figure.localRotation = Quaternion.identity;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            foreach (Renderer renderer in figure.GetComponentsInChildren<Renderer>())
            {
                // Darker than the kite's own colour: a person on a pale field is a
                // silhouette, and the kite is what the eye should go to first.
                renderer.material = new Material(shader) { color = color * 0.55f };
            }
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
            FaceTheKite();

            bool atBoundary = MathD.Abs(pilot.X) >= innerLimit || MathD.Abs(pilot.Z) >= innerLimit;
            if (atBoundary == warned || discRenderer == null)
                return;

            warned = atBoundary;
            discRenderer.material.color = atBoundary ? WarningColor : restColor;
        }

        /// <summary>
        /// Turn to face the kite, about the vertical only: a flier turns on their
        /// feet, they do not lean back to follow a kite overhead. With a kite at the
        /// zenith the horizontal direction is undefined, so the figure keeps the way
        /// it was already facing.
        /// </summary>
        private void FaceTheKite()
        {
            if (figure == null)
                return;

            Vec3 direction = kite.View.Direction;
            Vector3 flat = new Vector3((float)direction.X, 0f, (float)direction.Z);
            if (flat.sqrMagnitude < 1e-4f)
                return;

            figure.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
        }
    }
}

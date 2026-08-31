using UnityEngine;
using Kitesim.Core;

namespace Kitesim.Unity
{
    /// <summary>
    /// One kite in the judge's view: the Fury mesh at its true size, flying at the
    /// end of two lines, trailing its recent path and tinted by line tension.
    ///
    /// Reads a KiteStateView and places the real mesh in the world, which is the
    /// point of the view: the kite's orientation is visible, where the flat chart
    /// this replaced could only draw a triangle.
    ///
    /// See docs/superpowers/specs/2026-08-31-judge-view-design.md.
    /// </summary>
    public class WindowKiteMesh : MonoBehaviour
    {
        /// <summary>
        /// Span of the mesh in its own units, measured from the asset rather than
        /// assumed: Assets/Models/Fury_v01.blend is 4.5999 wide, exactly twice life
        /// size. Dividing the real span by this is what brings it to scale.
        /// </summary>
        private const float MeshSpan = 4.5999f;

        /// <summary>
        /// Seconds of turn rate per radian of bank, and the bank ceiling. The
        /// reduced model has no roll, so it is synthesised -- see
        /// ViewGeometry.SynthesiseRoll. Negate the gain if the kite banks the wrong
        /// way; the sign depends on the handedness of the mesh's own axes, which is
        /// a property of the asset rather than something to derive.
        /// </summary>
        [SerializeField] private float rollGain = 0.22f;
        [SerializeField] private float maxRollDegrees = 55f;

        /// <summary>
        /// How much larger than life the kite is drawn on the chart. The chart is an
        /// instrument, and the flat chart this replaced always drew the kite as a
        /// fixed-size triangle rather than at its true 3.2 degrees -- so this keeps
        /// what the chart already did rather than introducing a distortion. At true size
        /// the kite is about 22 px on a full-width chart, which is too small to read;
        /// 3x was tried first and the owner found it too big. Only the chart sees
        /// this; the judge and pilot views get the real size.
        /// </summary>
        [SerializeField] private float chartExaggeration = 2f;

        [SerializeField] private float trailSeconds = 2.5f;
        [SerializeField] private float trailWidth = 0.6f;

        /// <summary>
        /// Line thickness at the hands and at the kite, metres. A real 45 m line is
        /// about 1.5 mm and is simply invisible at 45 m, so the far end is drawn
        /// thicker than life while the near end, the one a pilot has right in front
        /// of their eyes, is kept close to the truth. Drawing both ends at the old
        /// 5 cm was what made the lines read as hawsers running from the pilot's
        /// knees rather than as lines out of their hands.
        /// </summary>
        [SerializeField] private float lineWidthAtHands = 0.004f;
        [SerializeField] private float lineWidthAtKite = 0.05f;

        /// <summary>
        /// Points per line when the pair is clear, and how many more each wrap costs.
        /// A twisted pair is a helix, and a helix drawn with too few points reads as
        /// a zigzag, so the count grows with the twist rather than being fixed.
        /// </summary>
        [SerializeField] private int lineSegments = 14;
        [SerializeField] private int segmentsPerWrap = 22;
        [SerializeField] private int maxLineSegments = 260;

        /// <summary>
        /// Half the distance between the two towpoints on the bridle, metres. The
        /// lines are not tied to one point on the kite: each goes to its own pigtail,
        /// about over the centre of its outer sail panel (docs/kite-anatomy.md
        /// section 2), which on a 2.5 m span puts them roughly a metre apart.
        /// </summary>
        [SerializeField] private float towpointSeparation = 0.5f;

        /// <summary>
        /// Half the width of the braid where twisted lines lie against each other.
        /// Real lines touch, at a millimetre or two; this is drawn wider so that a
        /// wrap can be seen from where a judge stands, in the same spirit as the
        /// chart drawing the kite larger than life.
        /// </summary>
        [SerializeField] private float braidWidth = 0.06f;

        /// <summary>
        /// Weight of one line per metre, newtons. From docs/kite-anatomy.md section 6:
        /// 0.5-0.6 g/m of Dyneema, so about 0.05 N/m. This is what makes the sag a
        /// derived number rather than a look: a line of length L under tension T sags
        /// by about w*L*L/(8*T) in the middle, which at 45 m gives a quarter of a
        /// metre under a working 50 N and metres when the kite goes slack at the
        /// window edge. Slack lines sagging is exactly what the eye reads as line
        /// rather than as rod.
        /// </summary>
        [SerializeField] private float lineWeightPerMetre = 0.05f;

        /// <summary>Sag is capped at this share of the line length; a real line lands first.</summary>
        [SerializeField] private float maxSagFraction = 0.12f;

        /// <summary>
        /// Where the handles are, relative to the pilot's feet: half the separation
        /// between them, their height, and how far forward the arms reach. All three
        /// come off the owner's competition footage, and all three must match the
        /// pilot figure that holds them -- see Tools/blender/make_pilot.py, which
        /// carries the same numbers.
        ///
        /// Fliers hold the handles close together at chest height with the arms out,
        /// not spread wide at the waist: an earlier 0.7 m apart at 1.15 m was both
        /// wrong and the reason the lines looked like they left from the knees.
        /// </summary>
        [SerializeField] private float handSeparation = 0.12f;
        [SerializeField] private float handHeight = 1.34f;
        [SerializeField] private float handReach = 0.48f;

        private WindowKiteBehaviour kite;
        private Color baseColor;
        private Transform model;
        private readonly System.Collections.Generic.List<Renderer> renderers =
            new System.Collections.Generic.List<Renderer>();
        private LineRenderer leftLine;
        private LineRenderer rightLine;

        // Reused every frame: the shape is rebuilt continuously and none of this
        // should reach the garbage collector.
        private Vec3[] leftPoints = System.Array.Empty<Vec3>();
        private Vec3[] rightPoints = System.Array.Empty<Vec3>();
        private Vector3[] scratch = System.Array.Empty<Vector3>();

        private double previousHeading;
        private bool hasPreviousHeading;

        public void Bind(WindowKiteBehaviour target, GameObject modelPrefab, double wingSpan, Color color)
        {
            kite = target;
            baseColor = color;

            BuildModel(modelPrefab, wingSpan);
            BuildTrail();
            leftLine = BuildLine("LineLeft");
            rightLine = BuildLine("LineRight");
        }

        /// <summary>
        /// Two copies of the kite: one at life size for the judge and the pilot, one
        /// exaggerated for the chart. Both are children of this transform, so they
        /// fly together for free -- position, heading and roll are applied once, here.
        /// Layers keep each camera to exactly one of them; see ViewLayers.
        /// </summary>
        private void BuildModel(GameObject modelPrefab, double wingSpan)
        {
            model = CreateCopy(modelPrefab, wingSpan, 1f, ViewLayers.KiteTrueSize);
            CreateCopy(modelPrefab, wingSpan, chartExaggeration, ViewLayers.KiteChartSize);
        }

        private Transform CreateCopy(GameObject modelPrefab, double wingSpan, float exaggeration, int layer)
        {
            Transform copy;
            if (modelPrefab == null)
            {
                Debug.LogWarning("[WindowKiteMesh] No kite model assigned; drawing a placeholder.");
                copy = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                Destroy(copy.GetComponent<Collider>());
                copy.SetParent(transform, false);
                copy.localScale = new Vector3((float)wingSpan, 0.1f, (float)wingSpan * 0.45f) * exaggeration;
            }
            else
            {
                copy = Instantiate(modelPrefab, transform).transform;
                copy.localRotation = Quaternion.identity;

                float scale = (float)wingSpan / MeshSpan * exaggeration;
                copy.localScale = Vector3.one * scale;

                // The mesh's pivot is not its centre (bounds centre is at
                // (0, -0.25, -0.45) in mesh units), so rotating this object would
                // swing the kite around a point off its own body. Offset the model
                // inside this transform to cancel that.
                var filter = copy.GetComponentInChildren<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                    copy.localPosition = -filter.sharedMesh.bounds.center * scale;
            }

            ViewLayers.Apply(copy.gameObject, layer);

            var renderer = copy.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                renderer.material = new Material(shader) { color = baseColor };
                renderers.Add(renderer);
            }

            return copy;
        }

        private void BuildTrail()
        {
            var go = new GameObject("Trail");
            go.transform.SetParent(transform, false);

            var trail = go.AddComponent<TrailRenderer>();
            trail.time = trailSeconds;
            trail.startWidth = trailWidth;
            trail.endWidth = 0f;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = baseColor;
            trail.endColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
        }

        private LineRenderer BuildLine(string label)
        {
            var go = new GameObject(label);
            // World space: the lines run between two points that move independently,
            // so parenting them to the kite would drag one end around with it.
            go.transform.SetParent(transform.parent, false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = lineWidthAtHands;
            line.endWidth = lineWidthAtKite;
            line.numCapVertices = 0;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = new Color(0.9f, 0.9f, 0.9f, 0.55f);
            line.endColor = new Color(0.9f, 0.9f, 0.9f, 0.30f);
            return line;
        }

        void LateUpdate()
        {
            if (kite == null || kite.Sim == null)
                return;

            KiteStateView view = kite.View;

            Vector3 position = view.WorldPosition.ToVector3();
            transform.position = position;

            Vector3 heading = view.Heading.ToVector3();
            Vector3 direction = view.Direction.ToVector3();
            if (heading.sqrMagnitude > 1e-9f && direction.sqrMagnitude > 1e-9f)
            {
                // The mesh's nose is +Z and its sail faces +Y, so aiming +Z along the
                // heading and +Y along -Direction puts the nose where the kite is
                // going and turns the sail to face the pilot, which is upwind.
                Quaternion aim = Quaternion.LookRotation(heading, -direction);
                transform.rotation = aim * Quaternion.Euler(0f, 0f, Roll(view));
            }

            UpdateLines(view, position);
            UpdateTint(view);
        }

        private float Roll(KiteStateView view)
        {
            double heading = view.HeadingRad;

            if (!hasPreviousHeading)
            {
                previousHeading = heading;
                hasPreviousHeading = true;
                return 0f;
            }

            double roll = ViewGeometry.SynthesiseRoll(
                previousHeading, heading, Time.deltaTime,
                rollGain, maxRollDegrees * MathD.Deg2Rad);
            previousHeading = heading;

            return (float)(roll * MathD.Rad2Deg);
        }

        private void UpdateLines(KiteStateView view, Vector3 kitePosition)
        {
            Vector3 pilot = view.PilotPosition.ToVector3();

            // Hands sit either side of the pilot, across the line to the kite, and
            // out in front of them: a flier faces their kite with both arms
            // extended, which is also how the figure on the field is modelled.
            Vector3 toKite = kitePosition - pilot;
            Vector3 across = Vector3.Cross(Vector3.up, toKite);
            across = across.sqrMagnitude > 1e-6f ? across.normalized : Vector3.right;

            Vector3 forward = new Vector3(toKite.x, 0f, toKite.z);
            forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;

            Vector3 hands = pilot + Vector3.up * handHeight + forward * handReach;
            float sag = Sag(view, toKite.magnitude, hands.y, kitePosition.y);

            // Each line goes to its own towpoint, across the kite's span, so the pair
            // ends where the bridle is and turns with the kite. transform.right is
            // that span: the same rotation the mesh above is drawn with.
            Vector3 span = transform.right;
            Vector3 towLeft = kitePosition - span * towpointSeparation;
            Vector3 towRight = kitePosition + span * towpointSeparation;

            float wraps = Mathf.Abs((float)view.LineTurnsRad) / (2f * Mathf.PI);
            int points = Mathf.Clamp(
                lineSegments + Mathf.RoundToInt(segmentsPerWrap * wraps) + 1, 3, maxLineSegments);
            EnsureBuffers(points);

            LineGeometry.TwistedPair(
                (hands - across * handSeparation).ToVec3(),
                (hands + across * handSeparation).ToVec3(),
                towLeft.ToVec3(), towRight.ToVec3(),
                view.LineTurnsRad, sag, braidWidth,
                leftPoints, rightPoints);

            Apply(leftLine, leftPoints);
            Apply(rightLine, rightPoints);

            bool flying = !view.Landed;
            leftLine.enabled = flying;
            rightLine.enabled = flying;
        }

        private void EnsureBuffers(int points)
        {
            if (leftPoints.Length == points)
                return;

            leftPoints = new Vec3[points];
            rightPoints = new Vec3[points];
            scratch = new Vector3[points];
        }

        private void Apply(LineRenderer line, Vec3[] points)
        {
            for (int i = 0; i < points.Length; i++)
                scratch[i] = points[i].ToVector3();

            if (line.positionCount != points.Length)
                line.positionCount = points.Length;
            line.SetPositions(scratch);
        }

        /// <summary>
        /// Depth of the sag at the middle of a line, metres: w*L*L/(8*T), the standard
        /// shallow-catenary result, with the line's own weight from the reference and
        /// the tension from the model. Both lines share the pull, so each carries half.
        ///
        /// Two limits on top of the formula. A share of the length, because the
        /// shallow-catenary result stops being one for a rope hanging in a loop. And
        /// the ground: at the low tension of a kite parked on the window edge the
        /// formula asks for metres of sag, which is true and is exactly why a slack
        /// line lies on the field rather than passing through it. Without this the
        /// lines were drawn five metres underground.
        /// </summary>
        private float Sag(KiteStateView view, float lineLength, float handHeightWorld, float kiteHeight)
        {
            float tension = Mathf.Max((float)view.Tension * 0.5f, 0.01f);
            float sag = lineWeightPerMetre * lineLength * lineLength / (8f * tension);

            float aboveGround = Mathf.Max(0.5f * (handHeightWorld + kiteHeight) - 0.05f, 0f);
            return Mathf.Min(sag, Mathf.Min(lineLength * maxSagFraction, aboveGround));
        }

        private void UpdateTint(KiteStateView view)
        {

            // Tension reads as saturation, and the kite stays dark either way.
            //
            // Two earlier attempts were wrong for the same reason: they moved the
            // kite's brightness rather than its colour. Darkening it left it black
            // and lost; washing it toward white lost it against the sky. The owner's
            // competition footage settles it -- real kites are dark silhouettes on a
            // pale, hazy sky, and that contrast is the whole reason they are findable
            // at three degrees across. So an unloaded kite desaturates toward dark
            // slate instead of fading, and stays visible.
            float load = Mathf.Clamp01((float)(view.Tension / 120.0));
            Color tint = Color.Lerp(
                Color.Lerp(baseColor, new Color(0.20f, 0.20f, 0.24f), 0.6f), baseColor, load);
            foreach (Renderer renderer in renderers)
                renderer.material.color = tint;
        }
    }
}

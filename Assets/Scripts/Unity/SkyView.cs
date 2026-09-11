using UnityEngine;
using Kitesim.Core;

namespace Kitesim.Unity
{
    /// <summary>
    /// The static scenery of the judge's view: a camera standing behind the pilot
    /// line looking downwind, ground and horizon, the competition boundaries, a
    /// wind arrow, and the iso-power contours drawn as arcs across the sky.
    ///
    /// Everything here is fixed once. The kites are separate (WindowKiteMesh), so
    /// this component has no per-frame work at all.
    ///
    /// See docs/superpowers/specs/2026-08-31-judge-view-design.md.
    /// </summary>
    public class SkyView : MonoBehaviour
    {
        /// <summary>
        /// Contour i sits at power i/ringCount -- the ideal cos(theta) law, the same
        /// one WindowChart states, drawn here as arcs across the sky.
        /// </summary>
        [SerializeField] private int ringCount = 5;
        [SerializeField] private int segmentsPerArc = 96;
        [SerializeField] private float arcWidth = 0.25f;

        [SerializeField] private float groundSize = 600f;
        [SerializeField] private float competitionFieldSize = 90f;

        /// <summary>
        /// Where the judge stands: about 15 m behind the pilot line, on their feet.
        /// Both numbers are from the owner, who judges; an earlier version put the
        /// camera at 1.15 line lengths (52 m) back, which is three times too far.
        ///
        /// The honest consequence is that the window does not fit in the frame. From
        /// 15 m behind, the window edge 45 m out to the side lies 72 degrees off the
        /// view axis, past any sane field of view. A real judge turns their head. At
        /// the values below the camera holds roughly the middle 120 degrees of
        /// azimuth, which is where the flying happens, and the kite is correspondingly
        /// large -- at 52 m back it was a handful of pixels.
        /// </summary>
        [SerializeField] private float cameraBackMetres = 15f;
        [SerializeField] private float cameraEyeHeight = 1.7f;

        /// <summary>Elevation the judge is looking at, degrees above the horizon.</summary>
        [SerializeField] private float cameraAimElevation = 26f;

        /// <summary>
        /// Wide, and tuned against screenshots rather than guessed. At 34 degrees of
        /// aim the horizon left the frame entirely, taking the ground and the
        /// competition boundaries with it -- the very anchors the eye needs when the
        /// kite is this small. 26 degrees with this angle keeps the horizon just
        /// inside the bottom edge and reaches about 61 degrees of elevation.
        /// </summary>
        [SerializeField] private float fieldOfView = 70f;

        /// <summary>
        /// Pale and hazy, not postcard blue. Taken from the owner's competition
        /// footage, where the sky is almost white and the kites read as dark
        /// silhouettes against it. Contrast is what makes a kite findable at three
        /// degrees across; a saturated blue sky loses it.
        /// </summary>
        private static readonly Color SkyColor = new Color(0.80f, 0.84f, 0.87f);

        /// <summary>Height of a standing pilot's eyes, metres.</summary>
        [SerializeField] private float pilotEyeHeight = 1.65f;

        public Camera Camera { get; private set; }

        /// <summary>The same world seen from the pilot's own eyes.</summary>
        public Camera PilotCamera { get; private set; }

        private JudgeCameraRig rig;
        private JudgeCameraRig pilotRig;

        /// <summary>Kites the cameras should keep in frame. Call after Build.</summary>
        public void Track(WindowKiteBehaviour kite)
        {
            if (rig != null)
                rig.Track(kite);
            if (pilotRig != null)
                pilotRig.Track(kite);
        }

        /// <summary>
        /// Put the pilot's camera at a flier's eyes. Their kite is what it will
        /// follow, through the same neck the judge uses -- the only difference
        /// between the two views is where the head is standing.
        /// </summary>
        public void PlacePilot(Vec3 pilotPosition, WindField wind)
        {
            if (PilotCamera == null)
                return;

            Vector3 downwind = WindowChart.Center(wind).ToVector3();
            Vector3 eye = pilotPosition.ToVector3() + Vector3.up * pilotEyeHeight;
            pilotRig.Configure(eye, Quaternion.AngleAxis(
                -cameraAimElevation, Vector3.Cross(Vector3.up, downwind)) * downwind);
        }

        public void Build(WindField wind, double lineLength)
        {
            Vec3 downwind = WindowChart.Center(wind);

            CreateLight();
            CreateGround();
            CreateBoundaries();
            CreateWindArrow(downwind);
            CreateArcs(wind, lineLength);
            CreateFigureGrid(wind, lineLength);
            CreateCamera(downwind);
            CreatePilotCamera(downwind);
        }

        public WindowFigureGrid FigureGrid { get; private set; }

        private void CreateFigureGrid(WindField wind, double lineLength)
        {
            FigureGrid = gameObject.AddComponent<WindowFigureGrid>();
            FigureGrid.Build(wind, lineLength);
        }

        private void CreateLight()
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(transform, false);

            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            // Hazy day, not hard sun: the reference footage is flat and overcast.
            light.intensity = 0.85f;
            light.color = new Color(1f, 0.97f, 0.9f);
            // Behind and above the judge, so the sail is lit rather than silhouetted.
            go.transform.rotation = Quaternion.Euler(48f, 20f, 0f);
        }

        private void CreateGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(transform, false);
            // Slightly below zero so the competition boundary lines, which sit at
            // y = 0.02, are never swallowed by z-fighting with this plane.
            ground.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            ground.transform.localScale = new Vector3(groundSize / 10f, 1f, groundSize / 10f);

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            ground.GetComponent<Renderer>().material = new Material(shader)
            {
                // Pale sand, as in the reference footage, rather than lawn green.
                color = new Color(0.46f, 0.44f, 0.39f)
            };

            // The primitive ships with a collider; nothing in this scene uses physics.
            Destroy(ground.GetComponent<Collider>());
        }

        private void CreateBoundaries()
        {
            var go = new GameObject("FieldBoundary");
            go.transform.SetParent(transform, false);
            var boundary = go.AddComponent<FieldBoundary>();
            // This view draws its own, much larger ground for the horizon, so the
            // boundary must not lay a second plane on top of it.
            boundary.Configure(competitionFieldSize, 3f, createGround: false);
        }

        /// <summary>An arrow on the ground at the pilots' feet, pointing downwind.</summary>
        private void CreateWindArrow(Vec3 downwind)
        {
            Vector3 forward = downwind.ToVector3();
            Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 root = -forward * 6f + new Vector3(0f, 0.05f, 0f);
            Vector3 tip = root + forward * 10f;

            var line = CreateLine("WindArrow", new Color(0.95f, 0.95f, 0.98f, 0.9f), 0.12f);
            line.positionCount = 5;
            line.SetPosition(0, root);
            line.SetPosition(1, tip);
            line.SetPosition(2, tip - forward * 1.6f + side * 0.9f);
            line.SetPosition(3, tip);
            line.SetPosition(4, tip - forward * 1.6f - side * 0.9f);
        }

        /// <summary>
        /// Iso-power contours across the sky. The geometry is exact and comes from
        /// ViewGeometry.IsoPowerArc; this only turns it into lines. The arcs are
        /// anchored at the field origin rather than at any one pilot -- with pilots
        /// standing in a staircase there is no single correct centre. See the design
        /// note on that approximation.
        /// </summary>
        private void CreateArcs(WindField wind, double lineLength)
        {
            var root = new GameObject("PowerArcs");
            root.transform.SetParent(transform, false);

            for (int i = 1; i < ringCount; i++)
            {
                double power = (double)i / ringCount;
                double theta = MathD.Acos(MathD.Clamp01(power));
                float intensity = 0.20f + 0.45f * (float)power;
                CreateArc(root.transform, wind, theta, lineLength, $"Ring_{i}",
                    new Color(intensity, intensity * 0.75f, 0.15f, 0.55f));
            }

            CreateArc(root.transform, wind, 90.0 * MathD.Deg2Rad, lineLength, "Edge",
                new Color(0.85f, 0.35f, 0.25f, 0.7f));
        }

        private void CreateArc(Transform parent, WindField wind, double theta,
            double lineLength, string label, Color color)
        {
            Vec3[] points = ViewGeometry.IsoPowerArc(wind, theta, lineLength, segmentsPerArc);

            var line = CreateLine(label, color, arcWidth);
            line.transform.SetParent(parent, false);
            line.positionCount = points.Length;
            for (int i = 0; i < points.Length; i++)
                line.SetPosition(i, points[i].ToVector3());
        }

        private void CreateCamera(Vec3 downwind)
        {
            var go = new GameObject("JudgeCamera");
            go.transform.SetParent(transform, false);

            Camera = go.AddComponent<Camera>();
            Camera.fieldOfView = fieldOfView;
            Camera.backgroundColor = SkyColor;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.farClipPlane = Mathf.Max(1000f, groundSize * 2f);
            Camera.cullingMask = ViewLayers.WorldMask;

            Vector3 forward = downwind.ToVector3();
            Vector3 rest = -forward * cameraBackMetres + Vector3.up * cameraEyeHeight;

            // Resting aim: tilted up into the window. The rig takes over from here,
            // following the kites with the head and stepping back only if it must.
            Vector3 restAim = Quaternion.AngleAxis(
                -cameraAimElevation, Vector3.Cross(Vector3.up, forward)) * forward;

            rig = go.AddComponent<JudgeCameraRig>();
            rig.Configure(rest, restAim);
        }

        /// <summary>
        /// The pilot's own view of the same world. It reuses the judge's rig, which
        /// is the point: a pilot and a judge both watch a kite with a human neck, and
        /// two components pretending to be heads would drift apart. The base design
        /// calls for this camera at M5.1; the world already exists, so it costs a
        /// camera.
        /// </summary>
        private void CreatePilotCamera(Vec3 downwind)
        {
            var go = new GameObject("PilotCamera");
            go.transform.SetParent(transform, false);

            PilotCamera = go.AddComponent<Camera>();
            PilotCamera.fieldOfView = fieldOfView;
            PilotCamera.backgroundColor = SkyColor;
            PilotCamera.clearFlags = CameraClearFlags.SolidColor;
            PilotCamera.farClipPlane = Mathf.Max(1000f, groundSize * 2f);
            PilotCamera.cullingMask = ViewLayers.FirstPersonMask;
            PilotCamera.enabled = false;

            pilotRig = go.AddComponent<JudgeCameraRig>();

            // Standing still: a pilot walks a little but never retreats to frame a
            // kite -- they are holding it.
            pilotRig.NeverStepsBack();

            Vector3 forward = downwind.ToVector3();
            pilotRig.Configure(
                Vector3.up * pilotEyeHeight,
                Quaternion.AngleAxis(-cameraAimElevation, Vector3.Cross(Vector3.up, forward)) * forward);
        }

        private LineRenderer CreateLine(string label, Color color, float width)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.startWidth = width;
            line.endWidth = width;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = color;
            line.endColor = color;
            return line;
        }
    }
}

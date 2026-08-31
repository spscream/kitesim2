using UnityEngine;
using UnityEngine.InputSystem;
using Kitesim.Core;

namespace Kitesim.Unity
{
    /// <summary>
    /// Assembles the wind-window trainer and its views.
    ///
    /// The simulation is built once: one kite plus one PlayerInput per pilot,
    /// with PlayerInputManager pairing a gamepad to each pilot as it joins. There
    /// is a single 3D world on top of it (SkyView, WindowKiteMesh), and every view
    /// is a camera on that world: the judge, the pilot's own eyes, and the window
    /// chart, which is that same world seen through a fisheye at the pilot's hands.
    ///
    /// WindowViewSwitcher enables exactly one camera at a time. This component only
    /// orchestrates; each view knows how to build itself. See
    /// docs/superpowers/specs/2026-08-31-judge-view-design.md.
    /// </summary>
    public class WindowSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;

        /// <summary>The Fury model, used by the judge's view. See WindowKiteMesh.</summary>
        [SerializeField] private GameObject kiteModel;

        /// <summary>
        /// Default to one pilot. With several PlayerInput components active and only
        /// a keyboard attached, every pilot would read the same keys and every kite
        /// would fly at once, which is not what someone opening this scene for the
        /// first time wants to see. Settable up to 4 in the inspector for team play
        /// once real gamepads are attached.
        /// </summary>
        [SerializeField] private int pilotCount = 1;
        [SerializeField] private double windSpeed = 5.0;
        [SerializeField] private ChartProjection projection = ChartProjection.Equidistant;

        /// <summary>
        /// Team pilots stand in a staircase, not a line. Laterally they are very
        /// close, half a metre to a metre, and almost never spread wide — a wide
        /// spread is only used to stretch the window scene and is rare in practice.
        /// The real separation is in depth: the lead pilot stands about half a
        /// metre forward, the next half a metre behind them, and so on. The
        /// staircase exists so a kite does not sit in the wake of the one ahead.
        /// </summary>
        [SerializeField] private float pilotLateralSpacing = 0.6f;
        [SerializeField] private float pilotDepthStep = 0.5f;

        private static readonly Color[] PilotColors =
        {
            new Color(0.90f, 0.25f, 0.25f),
            new Color(0.25f, 0.55f, 0.95f),
            new Color(0.30f, 0.80f, 0.40f),
            new Color(0.95f, 0.75f, 0.20f)
        };

        void Awake()
        {
            var wind = new WindField { Speed = windSpeed, Direction = Vec3.Forward };

            var skyRoot = new GameObject("SkyView");

            double lineLength = new WindowConfiguration().LineLength;
            var sky = skyRoot.AddComponent<SkyView>();
            sky.Build(wind, lineLength);
            var meshRoot = new GameObject("Kites");
            meshRoot.transform.SetParent(skyRoot.transform, false);
            var markerRoot = new GameObject("Pilots");
            markerRoot.transform.SetParent(skyRoot.transform, false);

            // Global, like the view switcher: a routine belongs to the team, not to
            // one pad, and it is recorded and replayed for everyone at once.
            var recorder = gameObject.AddComponent<WindowTrackRecorder>();

            for (int i = 0; i < pilotCount; i++)
            {
                // Staircase: lead pilot furthest downwind, each following pilot a
                // step back. Downwind is +Z, so later pilots get a smaller Z.
                Vec3 start = new Vec3(
                    (i - (pilotCount - 1) * 0.5) * pilotLateralSpacing,
                    0.0,
                    -i * pilotDepthStep);
                var kite = CreateKite(i, start, wind);
                CreateMesh(i, kite, meshRoot.transform);
                CreateMarker(i, kite, markerRoot.transform);
                CreateInput(i, kite);
                sky.Track(kite);
                recorder.Register(kite);
            }

            // The pilot's eyes go where the lead flier stands.
            Vec3 leadPilot = new Vec3((0 - (pilotCount - 1) * 0.5) * pilotLateralSpacing, 0.0, 0.0);
            sky.PlacePilot(leadPilot, wind);

            // The chart, rendered from that same world through a fisheye at the lead
            // pilot's hands, laid over the corner of whichever view is showing.
            var fisheye = skyRoot.AddComponent<FisheyeChartView>();
            fisheye.Build(wind, projection, leadPilot, new Color(0.07f, 0.09f, 0.12f, 1f));

            // Built last: showing a view enables one camera and disables the rest,
            // so every view must exist before that happens.
            var switcher = gameObject.AddComponent<WindowViewSwitcher>();
            switcher.Add("Judge", sky.Camera);
            switcher.Add("Pilot", sky.PilotCamera);
            switcher.Add("Window chart", fisheye.BackdropCamera,
                shown => fisheye.SetFullscreen(shown));
            switcher.Show(0);

            var cornerToggle = gameObject.AddComponent<CornerChartToggle>();
            cornerToggle.Bind(fisheye);
        }

        private void CreateMesh(int index, WindowKiteBehaviour kite, Transform parent)
        {
            var go = new GameObject($"KiteMesh_{index}");
            go.transform.SetParent(parent, false);
            var mesh = go.AddComponent<WindowKiteMesh>();
            mesh.Bind(kite, kiteModel, new KiteConfiguration().WingSpan,
                PilotColors[index % PilotColors.Length]);
        }

        /// <summary>
        /// A mark on the ground where this pilot stands. Pilot 0 is the lead flier,
        /// whose eyes the first-person camera and the chart lens borrow, so their own
        /// marker is built on the layer those two cull. See ViewLayers.
        /// </summary>
        private void CreateMarker(int index, WindowKiteBehaviour kite, Transform parent)
        {
            var go = new GameObject($"Pilot_{index}_Marker");
            go.transform.SetParent(parent, false);
            var marker = go.AddComponent<WindowPilotMarker>();
            marker.Bind(kite, PilotColors[index % PilotColors.Length], isLeadPilot: index == 0);
        }

        private WindowKiteBehaviour CreateKite(int index, Vec3 start, WindField wind)
        {
            var go = new GameObject($"Kite_{index}");
            var kite = go.AddComponent<WindowKiteBehaviour>();
            kite.Configure(index, start, wind);
            return kite;
        }

        private void CreateInput(int index, WindowKiteBehaviour kite)
        {
            // Built inactive on purpose. new GameObject(...) returns an active object,
            // and AddComponent<PlayerInput>() runs Awake/OnEnable synchronously — if
            // that happened before "actions" and "defaultActionMap" were assigned,
            // PlayerInput would enable nothing and every action would silently read
            // as never-pressed forever after, with no exception to point at it. Keep
            // the object inactive until it is fully configured, then activate it once
            // so Awake/OnEnable run exactly once with everything already in place.
            var go = new GameObject($"Pilot_{index}");
            go.SetActive(false);

            var playerInput = go.AddComponent<PlayerInput>();
            playerInput.actions = inputActions;
            playerInput.defaultActionMap = "Player";

            var pilot = go.AddComponent<WindowPilotInput>();
            pilot.Bind(kite);

            go.SetActive(true);

            var playerMap = playerInput.actions != null ? playerInput.actions.FindActionMap("Player") : null;
            var deviceNames = string.Join(", ", System.Array.ConvertAll(playerInput.devices.ToArray(), d => d.displayName));
            Debug.Log($"[WindowSceneBootstrap] Pilot {index}: Player map enabled={playerMap != null && playerMap.enabled}, " +
                $"devices={playerInput.devices.Count} [{deviceNames}], controlScheme={playerInput.currentControlScheme}");
        }
    }
}

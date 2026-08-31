using UnityEngine;
using UnityEngine.InputSystem;
using Kitesim.Core;

namespace Kitesim.Unity
{
    /// <summary>
    /// Assembles the wind-window trainer and its two views.
    ///
    /// The simulation is built once: one kite plus one PlayerInput per pilot,
    /// with PlayerInputManager pairing a gamepad to each pilot as it joins. On top
    /// of that sit two independent presentations of the same KiteStateView, each
    /// under its own root with its own camera:
    ///
    ///   ChartView -- the flat wind-window chart, an orthographic camera on the
    ///                chart plane (WindowChartRenderer, WindowKiteIcon)
    ///   SkyView   -- the judge's view, a camera behind the pilot line looking
    ///                downwind (SkyView, WindowKiteMesh)
    ///
    /// WindowViewSwitcher enables exactly one of them. This component only
    /// orchestrates; each view knows how to build itself. See
    /// docs/superpowers/specs/2026-08-31-judge-view-design.md.
    /// </summary>
    public class WindowSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;

        /// <summary>The Fury model, used by the judge's view. See WindowKiteMesh.</summary>
        [SerializeField] private GameObject kiteModel;

        /// <summary>Which view the scene opens in.</summary>
        [SerializeField] private bool startInSkyView = true;

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

            var chartRoot = new GameObject("ChartView");
            var skyRoot = new GameObject("SkyView");

            Camera chartCamera = CreateChartCamera(chartRoot.transform, projection);
            CreateChart(chartRoot.transform, wind, projection);
            var iconRoot = new GameObject("Icons");
            iconRoot.transform.SetParent(chartRoot.transform, false);

            double lineLength = new WindowConfiguration().LineLength;
            var sky = skyRoot.AddComponent<SkyView>();
            sky.Build(wind, lineLength);
            var meshRoot = new GameObject("Kites");
            meshRoot.transform.SetParent(skyRoot.transform, false);

            for (int i = 0; i < pilotCount; i++)
            {
                // Staircase: lead pilot furthest downwind, each following pilot a
                // step back. Downwind is +Z, so later pilots get a smaller Z.
                Vec3 start = new Vec3(
                    (i - (pilotCount - 1) * 0.5) * pilotLateralSpacing,
                    0.0,
                    -i * pilotDepthStep);
                var kite = CreateKite(i, start, wind);
                CreateIcon(i, kite, wind, iconRoot.transform);
                CreateMesh(i, kite, meshRoot.transform);
                CreateInput(i, kite);
                sky.Track(kite);
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
            switcher.Add("Judge", skyRoot, sky.Camera);
            switcher.Add("Pilot", skyRoot, sky.PilotCamera);
            switcher.Add("Fisheye chart", skyRoot, fisheye.BackdropCamera,
                shown => fisheye.SetFullscreen(shown));
            switcher.Add("Flat chart", chartRoot, chartCamera);
            switcher.Show(startInSkyView ? 0 : 3);

            var cornerToggle = gameObject.AddComponent<CornerChartToggle>();
            cornerToggle.Bind(fisheye);
        }

        private Camera CreateChartCamera(Transform parent, ChartProjection chartProjection)
        {
            var go = new GameObject("ChartCamera");
            go.transform.SetParent(parent, false);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = (float)WindowChart.EdgeRadius(chartProjection) * 1.12f;
            cam.transform.position = new Vector3(0f, (float)WindowChart.EdgeRadius(chartProjection) * 0.5f, -10f);
            cam.backgroundColor = new Color(0.07f, 0.09f, 0.12f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            return cam;
        }

        private void CreateChart(Transform parent, WindField wind, ChartProjection chartProjection)
        {
            var go = new GameObject("WindowChart");
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<WindowChartRenderer>();
            renderer.Configure(wind, chartProjection);
        }

        private void CreateMesh(int index, WindowKiteBehaviour kite, Transform parent)
        {
            var go = new GameObject($"KiteMesh_{index}");
            go.transform.SetParent(parent, false);
            var mesh = go.AddComponent<WindowKiteMesh>();
            mesh.Bind(kite, kiteModel, new KiteConfiguration().WingSpan,
                PilotColors[index % PilotColors.Length]);
        }

        private WindowKiteBehaviour CreateKite(int index, Vec3 start, WindField wind)
        {
            var go = new GameObject($"Kite_{index}");
            var kite = go.AddComponent<WindowKiteBehaviour>();
            kite.Configure(index, start, wind);
            return kite;
        }

        private void CreateIcon(int index, WindowKiteBehaviour kite, WindField wind, Transform parent)
        {
            var go = new GameObject($"Icon_{index}");
            go.transform.SetParent(parent, false);
            var icon = go.AddComponent<WindowKiteIcon>();
            icon.Bind(kite, wind, projection, PilotColors[index % PilotColors.Length]);
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

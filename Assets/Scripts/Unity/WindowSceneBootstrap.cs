using UnityEngine;
using UnityEngine.InputSystem;
using Kitesim.Core;

namespace Kitesim.Unity
{
    /// <summary>
    /// Assembles the 2D wind-window trainer: an orthographic camera on the chart
    /// plane, the chart itself, and one kite plus one PlayerInput per pilot.
    /// PlayerInputManager pairs a gamepad to each pilot as it joins.
    /// </summary>
    public class WindowSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;

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

            CreateCamera(projection);
            CreateChart(wind, projection);

            var iconRoot = new GameObject("Kites").transform;

            for (int i = 0; i < pilotCount; i++)
            {
                // Staircase: lead pilot furthest downwind, each following pilot a
                // step back. Downwind is +Z, so later pilots get a smaller Z.
                Vec3 start = new Vec3(
                    (i - (pilotCount - 1) * 0.5) * pilotLateralSpacing,
                    0.0,
                    -i * pilotDepthStep);
                var kite = CreateKite(i, start, wind);
                CreateIcon(i, kite, wind, iconRoot);
                CreateInput(i, kite);
            }
        }

        private void CreateCamera(ChartProjection chartProjection)
        {
            var go = new GameObject("ChartCamera");
            var cam = go.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.orthographic = true;
            cam.orthographicSize = (float)WindowChart.EdgeRadius(chartProjection) * 1.12f;
            cam.transform.position = new Vector3(0f, (float)WindowChart.EdgeRadius(chartProjection) * 0.5f, -10f);
            cam.backgroundColor = new Color(0.07f, 0.09f, 0.12f);
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        private void CreateChart(WindField wind, ChartProjection chartProjection)
        {
            var go = new GameObject("WindowChart");
            var renderer = go.AddComponent<WindowChartRenderer>();
            renderer.Configure(wind, chartProjection);
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

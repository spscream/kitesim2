using UnityEngine;
using UnityEngine.UI;
using Kitesim.Core;

namespace Kitesim.Unity
{
    public class WindUI : MonoBehaviour
    {
        [SerializeField] private KiteBehaviour kite;
        [SerializeField] private float initialSpeedKmh = 20f;
        [SerializeField] private float initialDirectionDeg = 0f;

        private float speedKmh;
        private float directionDeg;

        public void Initialize(KiteBehaviour targetKite)
        {
            kite = targetKite;
        }

        void Start()
        {
            speedKmh = initialSpeedKmh;
            directionDeg = initialDirectionDeg;
            BuildUI();
            ApplyWind();
        }

        void Update()
        {
            const float speedStep = 1f;
            const float dirStep = 5f;

            if (Input.GetKey(KeyCode.UpArrow))
                speedKmh = Mathf.Clamp(speedKmh + speedStep * Time.deltaTime * 10f, 4f, 45f);
            if (Input.GetKey(KeyCode.DownArrow))
                speedKmh = Mathf.Clamp(speedKmh - speedStep * Time.deltaTime * 10f, 4f, 45f);
            if (Input.GetKey(KeyCode.LeftArrow))
                directionDeg = Mathf.Repeat(directionDeg - dirStep * Time.deltaTime * 10f, 360f);
            if (Input.GetKey(KeyCode.RightArrow))
                directionDeg = Mathf.Repeat(directionDeg + dirStep * Time.deltaTime * 10f, 360f);
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 220, 80), GUI.skin.box);
            GUILayout.Label($"Wind: {speedKmh:F1} km/h");
            GUILayout.Label($"Direction: {directionDeg:F0} deg");
            GUILayout.Label("Arrows = adjust | Sliders = UI");
            GUILayout.EndArea();
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("Wind UI");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            var panel = new GameObject("Panel");
            panel.transform.SetParent(canvasGO.transform, false);
            var panelRT = panel.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0, 1);
            panelRT.anchorMax = new Vector2(0, 1);
            panelRT.pivot = new Vector2(0, 1);
            panelRT.anchoredPosition = new Vector2(10, -110);
            panelRT.sizeDelta = new Vector2(260, 90);

            var image = panel.AddComponent<Image>();
            image.color = new Color(0, 0, 0, 0.5f);

            CreateSlider(panel.transform, "Speed", 4f, 45f, speedKmh,
                v => { speedKmh = v; ApplyWind(); }, new Vector2(20, -20));

            CreateSlider(panel.transform, "Direction", 0f, 360f, directionDeg,
                v => { directionDeg = v; ApplyWind(); }, new Vector2(20, -55));
        }

        private void CreateSlider(Transform parent, string label, float min, float max, float value,
            System.Action<float> onChanged, Vector2 anchoredPosition)
        {
            var sliderGO = DefaultControls.CreateSlider(new DefaultControls.Resources());
            sliderGO.name = label + " Slider";
            sliderGO.transform.SetParent(parent, false);

            var rt = sliderGO.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = new Vector2(220, 20);

            var slider = sliderGO.GetComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = label == "Direction";
            slider.value = value;
            slider.onValueChanged.AddListener(v => onChanged(v));
        }

        private void ApplyWind()
        {
            double speedMs = speedKmh / 3.6;
            double dirRad = directionDeg * Mathf.Deg2Rad;
            Vec3 dir = new Vec3(Mathf.Sin((float)dirRad), 0, Mathf.Cos((float)dirRad));
            kite.SetWind(speedMs, dir);
        }
    }
}

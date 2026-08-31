using UnityEngine;
using UnityEngine.InputSystem;

namespace Kitesim.Unity
{
    /// <summary>
    /// Shows or hides the corner chart. Global, like the view switcher and for the
    /// same reason: what is on screen belongs to whoever is watching, not to a pad.
    /// </summary>
    public class CornerChartToggle : MonoBehaviour
    {
        [SerializeField] private Key toggleKey = Key.C;

        private FisheyeChartView chart;

        public void Bind(FisheyeChartView view)
        {
            chart = view;
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || chart == null || !keyboard[toggleKey].wasPressedThisFrame)
                return;

            chart.CornerVisible = !chart.CornerVisible;
            Debug.Log($"[CornerChartToggle] corner chart {(chart.CornerVisible ? "on" : "off")}");
        }
    }
}

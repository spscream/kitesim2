using UnityEngine;
using UnityEngine.InputSystem;

namespace Kitesim.Unity
{
    /// <summary>
    /// Switches between the flat chart and the judge's 3D view.
    ///
    /// Read globally rather than through PlayerInput on purpose: PlayerInput binds
    /// to one gamepad, and the view is a property of the person watching, not of a
    /// pilot. In team play one person switches it, not each pad independently.
    ///
    /// Exactly one camera is enabled at a time, which is what makes the switch
    /// trivially correct -- there is no z-order or render-target arbitration to get
    /// wrong.
    /// </summary>
    public class WindowViewSwitcher : MonoBehaviour
    {
        [SerializeField] private Key toggleKey = Key.V;

        private GameObject chartRoot;
        private GameObject skyRoot;
        private bool showingSky;

        public void Bind(GameObject chart, GameObject sky, bool startWithSky)
        {
            chartRoot = chart;
            skyRoot = sky;
            showingSky = startWithSky;
            Apply();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[toggleKey].wasPressedThisFrame)
                return;

            showingSky = !showingSky;
            Apply();
        }

        private void Apply()
        {
            if (chartRoot != null)
                chartRoot.SetActive(!showingSky);
            if (skyRoot != null)
                skyRoot.SetActive(showingSky);
        }
    }
}

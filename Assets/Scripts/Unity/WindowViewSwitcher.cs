using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kitesim.Unity
{
    /// <summary>
    /// Cycles the main view: the judge behind the pilots, the pilot's own eyes, and
    /// the window chart.
    ///
    /// Read globally rather than through PlayerInput on purpose: PlayerInput binds
    /// to one gamepad, and the view is a property of the person watching, not of a
    /// pilot. In team play one person switches it, not each pad independently.
    ///
    /// Every view is a camera on the one 3D world, so switching is nothing but
    /// enabling one camera and disabling the rest -- there is no z-order or
    /// render-target arbitration to get wrong, and no scenery to switch on and off.
    /// </summary>
    public class WindowViewSwitcher : MonoBehaviour
    {
        [SerializeField] private Key nextViewKey = Key.V;

        private readonly List<View> views = new List<View>();
        private int current;

        private struct View
        {
            public string Name;
            public Camera Camera;

            /// <summary>
            /// Called with true when this view is chosen and false when it is left.
            /// Not every view is just a camera: the fisheye chart is a screen overlay
            /// that has to be told to fill the screen or go back to its corner.
            /// </summary>
            public System.Action<bool> OnShown;
        }

        /// <summary>Register a view. The first one registered is the one shown.</summary>
        public void Add(string name, Camera camera, System.Action<bool> onShown = null)
        {
            views.Add(new View { Name = name, Camera = camera, OnShown = onShown });
        }

        public void Show(int index)
        {
            if (views.Count == 0)
                return;

            current = ((index % views.Count) + views.Count) % views.Count;

            for (int i = 0; i < views.Count; i++)
            {
                if (views[i].Camera != null)
                    views[i].Camera.enabled = i == current;
                views[i].OnShown?.Invoke(i == current);
            }
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[nextViewKey].wasPressedThisFrame)
                return;

            Show(current + 1);
            Debug.Log($"[WindowViewSwitcher] {views[current].Name}");
        }
    }
}

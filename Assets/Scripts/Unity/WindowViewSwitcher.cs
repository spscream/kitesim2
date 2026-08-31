using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kitesim.Unity
{
    /// <summary>
    /// Cycles the main view: the judge behind the pilots, the pilot's own eyes, and
    /// the flat chart.
    ///
    /// Read globally rather than through PlayerInput on purpose: PlayerInput binds
    /// to one gamepad, and the view is a property of the person watching, not of a
    /// pilot. In team play one person switches it, not each pad independently.
    ///
    /// Exactly one camera is enabled at a time, which is what makes the switch
    /// trivially correct -- there is no z-order or render-target arbitration to get
    /// wrong. The 3D world stays alive whichever of its cameras is chosen, because
    /// the fisheye chart renders from it continuously; only the flat chart, which is
    /// a separate set of objects sitting near the origin, is switched off so its
    /// geometry cannot appear inside the 3D view.
    /// </summary>
    public class WindowViewSwitcher : MonoBehaviour
    {
        [SerializeField] private Key nextViewKey = Key.V;

        private readonly List<View> views = new List<View>();
        private int current;

        private struct View
        {
            public string Name;
            public GameObject Root;
            public Camera Camera;
        }

        /// <summary>Register a view. The first one registered is the one shown.</summary>
        public void Add(string name, GameObject root, Camera camera)
        {
            views.Add(new View { Name = name, Root = root, Camera = camera });
        }

        public void Show(int index)
        {
            if (views.Count == 0)
                return;

            current = ((index % views.Count) + views.Count) % views.Count;

            // Roots first, then cameras: a camera enabled inside a root that is about
            // to be deactivated would render one frame of the wrong world.
            for (int i = 0; i < views.Count; i++)
            {
                if (views[i].Root != null)
                    views[i].Root.SetActive(RootNeeded(i));
            }
            for (int i = 0; i < views.Count; i++)
            {
                if (views[i].Camera != null)
                    views[i].Camera.enabled = i == current;
            }
        }

        /// <summary>
        /// A root stays alive if any view sharing it is selected. The judge and the
        /// pilot are two cameras on the same world, so choosing one must not switch
        /// the other's scenery off.
        /// </summary>
        private bool RootNeeded(int index)
        {
            return views[index].Root == views[current].Root;
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

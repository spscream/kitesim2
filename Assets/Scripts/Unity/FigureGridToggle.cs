using UnityEngine;
using UnityEngine.InputSystem;

namespace Kitesim.Unity
{
    public class FigureGridToggle : MonoBehaviour
    {
        [SerializeField] private Key toggleKey = Key.N;

        private WindowFigureGrid grid;

        public void Bind(WindowFigureGrid target)
        {
            grid = target;
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || grid == null || !keyboard[toggleKey].wasPressedThisFrame)
                return;

            grid.Visible = !grid.Visible;
            Debug.Log($"[FigureGridToggle] figure grid {(grid.Visible ? "on" : "off")}");
        }
    }
}

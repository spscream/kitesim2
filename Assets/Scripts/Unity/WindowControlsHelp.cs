using UnityEngine;
using UnityEngine.InputSystem;

namespace Kitesim.Unity
{
    /// <summary>
    /// A corner "?" that shows how to fly: keyboard hands and the gamepad.
    /// Session keys stay off this card — they are already labelled on their own
    /// HUD. Hover or a click pins the same text; H does the click from the
    /// keyboard.
    /// </summary>
    public class WindowControlsHelp : MonoBehaviour
    {
        [SerializeField] private Key toggleKey = Key.H;

        public const string Copy =
            "Keyboard\n" +
            "A/D  left hand     O/P  right hand\n" +
            "W/S  walk          Space  launch\n" +
            "\n" +
            "Gamepad\n" +
            "L/R stick  hands   LT/RT  walk\n" +
            "A / Start  launch";

        public bool Pinned { get; private set; }

        public void TogglePinned()
        {
            Pinned = !Pinned;
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[toggleKey].wasPressedThisFrame)
                return;

            TogglePinned();
        }

        void OnGUI()
        {
            const float button = 26f;
            const float margin = 10f;
            const float cardWidth = 280f;
            const float cardHeight = 132f;

            var buttonRect = new Rect(
                Screen.width - margin - button,
                Screen.height - margin - button,
                button,
                button);
            var cardRect = new Rect(
                Screen.width - margin - cardWidth,
                buttonRect.yMin - 6f - cardHeight,
                cardWidth,
                cardHeight);

            Vector2 mouse = Event.current.mousePosition;
            bool hover = buttonRect.Contains(mouse) || cardRect.Contains(mouse);

            if (GUI.Button(buttonRect, "?"))
                TogglePinned();

            if (!Pinned && !hover)
                return;

            GUILayout.BeginArea(cardRect, GUI.skin.box);
            GUILayout.Label(Copy);
            GUILayout.EndArea();
        }
    }
}

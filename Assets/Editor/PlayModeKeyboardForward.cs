using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Kitesim.Unity.EditorTools
{
    /// <summary>
    /// Unity 6 FastKeyboard often does not see HID while Play Mode is running in
    /// the Editor, even with AllDeviceInputAlwaysGoesToGameView. The Editor's
    /// IMGUI event stream does see the keys; this copies A/D/O/P/W/S/Space onto
    /// Keyboard.current so the trainer can fly without a Game view focus dance.
    /// </summary>
    [InitializeOnLoad]
    static class PlayModeKeyboardForward
    {
        private static readonly HashSet<Key> Pressed = new HashSet<Key>();

        static PlayModeKeyboardForward()
        {
            FieldInfo field = typeof(EditorApplication).GetField(
                "globalEventHandler", BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null)
                return;

            var handler = (EditorApplication.CallbackFunction)field.GetValue(null);
            handler += OnGlobalEvent;
            field.SetValue(null, handler);
        }

        private static void OnGlobalEvent()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused)
                return;

            Event e = Event.current;
            if (e == null || !e.isKey || e.keyCode == KeyCode.None)
                return;
            if (!TryFlyKey(e.keyCode, out Key key))
                return;

            if (e.type == EventType.KeyDown)
                Pressed.Add(key);
            else if (e.type == EventType.KeyUp)
                Pressed.Remove(key);
            else
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.added)
                return;

            var keys = new Key[Pressed.Count];
            Pressed.CopyTo(keys);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        }

        private static bool TryFlyKey(KeyCode code, out Key key)
        {
            switch (code)
            {
                case KeyCode.A: key = Key.A; return true;
                case KeyCode.D: key = Key.D; return true;
                case KeyCode.O: key = Key.O; return true;
                case KeyCode.P: key = Key.P; return true;
                case KeyCode.W: key = Key.W; return true;
                case KeyCode.S: key = Key.S; return true;
                case KeyCode.Space: key = Key.Space; return true;
                default:
                    key = Key.None;
                    return false;
            }
        }
    }
}

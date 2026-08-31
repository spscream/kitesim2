using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kitesim.Unity
{
    /// <summary>
    /// Turns the steering help on and off for everybody, and says which it is.
    ///
    /// Global, like the view and the recorder, and for the same reason: it is a
    /// property of the session, not of one pad. A team rehearsing a routine either
    /// flies with the help or without it; half the team on each would make the
    /// timings disagree.
    ///
    /// It exists because the help changes what the kite does, so the flier has to be
    /// able to see whether it is on -- and because a routine flown with it and one
    /// flown without are not the same performance. See TurnAssist for what it does
    /// and why its authority is what it is.
    /// </summary>
    public class TurnAssistToggle : MonoBehaviour
    {
        [SerializeField] private Key toggleKey = Key.G;

        private readonly List<WindowKiteBehaviour> kites = new List<WindowKiteBehaviour>();
        private GUIStyle style;

        public void Register(WindowKiteBehaviour kite)
        {
            kites.Add(kite);
        }

        public bool Enabled
        {
            get
            {
                foreach (WindowKiteBehaviour kite in kites)
                {
                    if (kite != null && kite.Sim != null)
                        return kite.Sim.Config.TurnAssist;
                }
                return false;
            }
            set
            {
                foreach (WindowKiteBehaviour kite in kites)
                {
                    if (kite != null && kite.Sim != null)
                        kite.Sim.Config.TurnAssist = value;
                }
            }
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[toggleKey].wasPressedThisFrame)
                return;

            Enabled = !Enabled;
            Debug.Log($"[TurnAssistToggle] steering help {(Enabled ? "on" : "off")}");
        }

        void OnGUI()
        {
            if (kites.Count == 0)
                return;

            style ??= new GUIStyle(GUI.skin.label);
            style.normal.textColor = Enabled ? new Color(0.55f, 0.85f, 0.55f) : Color.white;

            GUILayout.BeginArea(new Rect(10f, 196f, 190f, 26f), GUI.skin.box);
            GUILayout.Label(Enabled ? "steering help (G): on" : "steering help (G): off", style);
            GUILayout.EndArea();
        }
    }
}

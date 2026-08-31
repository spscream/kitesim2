using System.Collections.Generic;
using UnityEngine;
using Kitesim.Core;

namespace Kitesim.Unity
{
    /// <summary>
    /// Shows how wound up each pair of kites is. The counting is PairWrapCounter's;
    /// this only puts it on screen.
    ///
    /// Two or three wraps between a pair is normal and four is a problem, so the
    /// number turns amber at three and red at four: a team plans the ballet so that
    /// no pair runs out, and the point of showing it live is to learn what a routine
    /// costs before the lines say so.
    ///
    /// Nothing is drawn with a single pilot -- there is no pair to wind.
    /// </summary>
    public class WindowWrapHud : MonoBehaviour
    {
        /// <summary>Wraps at which the count stops being routine. Owner's numbers.</summary>
        [SerializeField] private int cautionWraps = 3;
        [SerializeField] private int problemWraps = 4;

        private readonly List<WindowKiteBehaviour> kites = new List<WindowKiteBehaviour>();
        private TeamWrapCounter counter;
        private KiteStateView[] views;
        private GUIStyle style;

        public TeamWrapCounter Counter => counter;

        public void Build(WindField wind, int pilotCount)
        {
            counter = new TeamWrapCounter(pilotCount, WindowChart.Center(wind));
            views = new KiteStateView[pilotCount];
        }

        public void Register(WindowKiteBehaviour kite)
        {
            kites.Add(kite);
        }

        void Update()
        {
            if (counter == null || views == null)
                return;

            for (int i = 0; i < kites.Count; i++)
            {
                int index = kites[i].PilotIndex;
                if (index >= 0 && index < views.Length)
                    views[index] = kites[i].View;
            }

            // Fed with the view rather than the model, so a replayed routine is
            // counted exactly like a flown one.
            counter.Update(views);
        }

        void OnGUI()
        {
            if (counter == null || counter.PilotCount < 2)
                return;

            style ??= new GUIStyle(GUI.skin.label);

            float width = 150f;
            GUILayout.BeginArea(new Rect(Screen.width - width - 10f, 10f, width, 26f * PairCount() + 30f),
                GUI.skin.box);
            GUILayout.Label("line wraps");

            for (int i = 0; i < counter.PilotCount; i++)
            {
                for (int j = i + 1; j < counter.PilotCount; j++)
                {
                    int wraps = counter.Wraps(i, j);
                    int magnitude = Mathf.Abs(wraps);
                    style.normal.textColor = magnitude >= problemWraps
                        ? new Color(0.95f, 0.35f, 0.30f)
                        : magnitude >= cautionWraps
                            ? new Color(0.95f, 0.80f, 0.25f)
                            : Color.white;

                    GUILayout.Label($"{i + 1}–{j + 1}:  {wraps}", style);
                }
            }

            GUILayout.EndArea();
        }

        private int PairCount()
        {
            int n = counter.PilotCount;
            return n * (n - 1) / 2;
        }
    }
}

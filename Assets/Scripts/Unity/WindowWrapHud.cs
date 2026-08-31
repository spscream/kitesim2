using System.Collections.Generic;
using UnityEngine;
using Kitesim.Core;

namespace Kitesim.Unity
{
    /// <summary>
    /// Shows how wound up the lines are: each kite's own wraps, and each pair's
    /// wraps around each other. Both numbers are counted elsewhere -- the kite's own
    /// by the model, in WindowKiteState.AccumulatedTurn, the pair's by
    /// PairWrapCounter -- and this only puts them on screen.
    ///
    /// The two are different quantities and are shown apart on purpose. A kite's own
    /// wraps come from it rolling about its own line axis; a pair's come from one
    /// kite going around the other. Neither loses control on its own, both have a
    /// critical count, and only the second is what a team plans a ballet around.
    /// See docs/kite-anatomy.md section 6.
    ///
    /// Two or three wraps is normal and four is a problem, so the number turns amber
    /// at three and red at four.
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
            if (counter == null || counter.PilotCount < 1)
                return;

            style ??= new GUIStyle(GUI.skin.label);

            float width = 160f;
            // One row per kite, one per pair, plus the headings above each block.
            int lines = counter.PilotCount + PairCount() + (PairCount() > 0 ? 2 : 1);
            GUILayout.BeginArea(
                new Rect(Screen.width - width - 10f, 10f, width, 18f * lines + 34f), GUI.skin.box);

            GUILayout.Label("own wraps");
            for (int i = 0; i < counter.PilotCount && i < views.Length; i++)
                Row($"kite {i + 1}", views[i].LineWraps);

            if (PairCount() > 0)
            {
                GUILayout.Label("between kites");
                for (int i = 0; i < counter.PilotCount; i++)
                {
                    for (int j = i + 1; j < counter.PilotCount; j++)
                        Row($"{i + 1}–{j + 1}", counter.Wraps(i, j));
                }
            }

            GUILayout.EndArea();
        }

        private void Row(string label, int wraps)
        {
            int magnitude = Mathf.Abs(wraps);
            style.normal.textColor = magnitude >= problemWraps
                ? new Color(0.95f, 0.35f, 0.30f)
                : magnitude >= cautionWraps
                    ? new Color(0.95f, 0.80f, 0.25f)
                    : Color.white;

            GUILayout.Label($"{label}:  {wraps}", style);
        }

        private int PairCount()
        {
            int n = counter.PilotCount;
            return n * (n - 1) / 2;
        }
    }
}

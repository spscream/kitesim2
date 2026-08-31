using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Kitesim.Core;

namespace Kitesim.Unity
{
    /// <summary>
    /// Writes down what the pilot's hands did, alongside the track of what the kite
    /// did. Recording starts and stops with the track recorder, so one key covers
    /// both, and the file lands next to track.csv.
    ///
    /// The two are different questions and neither answers the other. A track says
    /// where the kite went; this says what it was asked for, what gesture the
    /// steering help read out of that, and whether it engaged. Without it, a turn
    /// that came out at 110 degrees cannot be told apart into "the pilot held on too
    /// long", "the help never saw a release" and "the help did the wrong thing".
    ///
    /// Both pairs are written: where the hands have got to, and where the sticks
    /// are. They differ by the rate limit on a hand, and every defect found in the
    /// steering help so far has lived in that gap.
    ///
    /// A diagnostic, not a feature: it is here to answer questions about the
    /// controls, and it can go when they are answered.
    /// </summary>
    public class WindowInputProbe : MonoBehaviour
    {
        /// <summary>
        /// No column for the instantaneous turn rate: asking the model for it would
        /// run the steering help a second time in the same frame and move the kite's
        /// assist state. The remembered turn next to it is the one the help itself
        /// works from, and it is enough to read the manoeuvre.
        /// </summary>
        public const string Header =
            "time,pilot,handLeft,handRight,stickLeft,stickRight,steering,headingDeg," +
            "recentTurnDeg,assistGridDeg,assistTargetDeg,speed,landed";

        [SerializeField] private string fileName = "inputs.csv";

        private readonly List<WindowKiteBehaviour> kites = new List<WindowKiteBehaviour>();
        private readonly StringBuilder rows = new StringBuilder();

        private WindowTrackRecorder recorder;
        private double time;
        private bool wasRecording;

        public string FilePath => Path.Combine(Application.persistentDataPath, fileName);

        public int Rows { get; private set; }

        public void Bind(WindowTrackRecorder trackRecorder)
        {
            recorder = trackRecorder;
        }

        public void Register(WindowKiteBehaviour kite)
        {
            kites.Add(kite);
        }

        void Update()
        {
            if (recorder == null)
                return;

            if (recorder.IsRecording && !wasRecording)
            {
                rows.Clear();
                rows.Append(Header).Append('\n');
                Rows = 0;
                time = 0.0;
            }

            if (!recorder.IsRecording)
            {
                if (wasRecording)
                    Write();
                wasRecording = false;
                return;
            }

            wasRecording = true;
            time += Time.deltaTime;

            var culture = CultureInfo.InvariantCulture;
            for (int i = 0; i < kites.Count; i++)
            {
                WindowKiteBehaviour kite = kites[i];
                if (kite == null || kite.Sim == null)
                    continue;

                KiteStateView view = kite.View;
                double heading = view.HeadingRad * MathD.Rad2Deg;
                double steering = MathD.Clamp(
                    (kite.HandRight - kite.HandLeft) / (2.0 * kite.Sim.Config.HandTravel), -1.0, 1.0);
                double target = double.IsNaN(kite.AssistTarget)
                    ? double.NaN
                    : kite.AssistTarget * MathD.Rad2Deg;

                rows.Append(time.ToString("F3", culture)).Append(',');
                rows.Append(kite.PilotIndex.ToString(culture)).Append(',');
                rows.Append(kite.HandLeft.ToString("F4", culture)).Append(',');
                rows.Append(kite.HandRight.ToString("F4", culture)).Append(',');
                rows.Append(kite.IntentLeft.ToString("F4", culture)).Append(',');
                rows.Append(kite.IntentRight.ToString("F4", culture)).Append(',');
                rows.Append(steering.ToString("F4", culture)).Append(',');
                rows.Append(heading.ToString("F2", culture)).Append(',');
                rows.Append((kite.RecentTurnRate * MathD.Rad2Deg).ToString("F2", culture)).Append(',');
                rows.Append((kite.AssistGrid * MathD.Rad2Deg).ToString("F1", culture)).Append(',');
                rows.Append(target.ToString("F2", culture)).Append(',');
                rows.Append(view.Speed.ToString("F2", culture)).Append(',');
                rows.Append(view.Landed ? "1" : "0").Append('\n');

                Rows++;
            }
        }

        private void Write()
        {
            if (Rows == 0)
                return;

            File.WriteAllText(FilePath, rows.ToString());
            Debug.Log($"[InputProbe] wrote {Rows} rows to {FilePath}");
        }
    }
}

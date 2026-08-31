using UnityEngine;
using Kitesim.Core;

namespace Kitesim.Unity
{
    /// <summary>
    /// Drives one kite of the reduced wind-window model. Holds no rendering logic;
    /// the views read View each frame.
    /// </summary>
    public class WindowKiteBehaviour : MonoBehaviour
    {
        [SerializeField] private int pilotIndex;
        [SerializeField] private float handLeft;
        [SerializeField] private float handRight;
        [SerializeField] private float walkAxis;
        [SerializeField] private float intentLeft;
        [SerializeField] private float intentRight;

        private WindowSim sim;
        private WindowKiteState state;

        /// <summary>
        /// Set while a recorded track is playing. Everything downstream -- the mesh,
        /// the trail, the judge's framing, the rumble -- reads View, so replacing the
        /// view here replays the routine everywhere at once, with no second path
        /// through the scene that could drift from the live one.
        /// </summary>
        private KiteStateView? playbackView;

        public int PilotIndex => pilotIndex;
        public WindowSim Sim => sim;

        /// <summary>
        /// What the hands are doing and what the steering help has made of it, for
        /// the flight recorder to write down. A track carries the kite's state, which
        /// is the right thing to replay; this is the other half, and the only way to
        /// tell a control problem from a model problem after the fact.
        /// </summary>
        public float HandLeft => handLeft;
        public float HandRight => handRight;
        public float IntentLeft => intentLeft;
        public float IntentRight => intentRight;
        public double RecentTurnRate => state != null ? state.RecentTurnRate : 0.0;
        public double AssistGrid => state != null ? state.AssistGrid : 0.0;
        public double AssistTarget => state != null ? state.AssistTarget : double.NaN;
        public bool Landed => playbackView.HasValue
            ? playbackView.Value.Landed
            : state != null && state.Landed;
        public KiteStateView View => playbackView ?? sim.View(state);

        /// <summary>Feed one replayed frame. The live model is frozen until Resume.</summary>
        public void ShowPlaybackFrame(KiteStateView view)
        {
            playbackView = view;
        }

        /// <summary>Hand the kite back to the live model, exactly where it was left.</summary>
        public void Resume()
        {
            playbackView = null;
        }

        public void Configure(int index, Vec3 startPilotPosition, WindField wind)
        {
            pilotIndex = index;
            sim = new WindowSim(new WindowConfiguration())
            {
                Wind = wind,
                PilotPosition = startPilotPosition,
                PilotVelocity = Vec3.Zero
            };
            state = WindowKiteState.OnGround(0.0);
        }

        /// <summary>
        /// Where the hands are. The raw pair is what the pilot is asking for before
        /// the hands have caught up: on a controller the input layer rate-limits the
        /// hands to human speed, so a release takes about 0.18 s to arrive, and the
        /// steering help needs the intent rather than the lag. Callers with no
        /// separate intent pass the hands twice.
        /// </summary>
        public void SetInputs(float left, float right, float walk)
        {
            SetInputs(left, right, walk, left, right);
        }

        public void SetInputs(float left, float right, float walk, float rawLeft, float rawRight)
        {
            handLeft = left;
            handRight = right;
            walkAxis = walk;
            intentLeft = rawLeft;
            intentRight = rawRight;
        }

        public void Launch()
        {
            sim.Launch(state);
        }

        public void SetWind(WindField wind)
        {
            sim.Wind = wind;
        }

        void FixedUpdate()
        {
            // Frozen during playback: stepping the model under a replayed view would
            // leave the kite somewhere else the moment playback stops.
            if (sim == null || playbackView.HasValue)
                return;

            // Hand axes are positions in metres; the axis is scaled by the
            // configured half-travel. Walk is a rate and passes through raw.
            double travel = sim.Config.HandTravel;
            sim.Step(state, Time.fixedDeltaTime,
                handLeft * travel, handRight * travel, walkAxis,
                intentLeft * travel, intentRight * travel);
        }
    }
}

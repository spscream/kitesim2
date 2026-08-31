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

        public void SetInputs(float left, float right, float walk)
        {
            handLeft = left;
            handRight = right;
            walkAxis = walk;
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
            sim.Step(state, Time.fixedDeltaTime, handLeft * travel, handRight * travel, walkAxis);
        }
    }
}

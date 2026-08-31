using UnityEngine;
using Kitesim.Core;

namespace Kitesim.Unity
{
    /// <summary>
    /// Drives one kite of the reduced wind-window model. Holds no rendering logic;
    /// the chart renderer reads View each frame.
    /// </summary>
    public class WindowKiteBehaviour : MonoBehaviour
    {
        [SerializeField] private int pilotIndex;
        [SerializeField] private float handLeft;
        [SerializeField] private float handRight;
        [SerializeField] private float walkAxis;

        private WindowSim sim;
        private WindowKiteState state;

        public int PilotIndex => pilotIndex;
        public WindowSim Sim => sim;
        public bool Landed => state != null && state.Landed;
        public KiteStateView View => sim.View(state);

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
            if (sim == null)
                return;

            // Hand axes are positions in metres; the axis is scaled by the
            // configured half-travel. Walk is a rate and passes through raw.
            double travel = sim.Config.HandTravel;
            sim.Step(state, Time.fixedDeltaTime, handLeft * travel, handRight * travel, walkAxis);
        }
    }
}

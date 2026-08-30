using NUnit.Framework;

namespace Kitesim.Core.Tests
{
    public class KiteSimTests
    {
        [Test]
        public void Step_KeepsKiteOnSphere()
        {
            var sim = new KiteSim(KiteConfiguration.KrdFury());
            var state = KiteState.FromSpherical(0, 30.0 * MathD.Deg2Rad, sim.Config.LineLength);
            state.Launched = true;

            for (int i = 0; i < 1000; i++)
            {
                sim.Step(state, 0.016, 0.0, 0.0);
            }

            double r = (state.Position - sim.PilotPosition).Length();
            Assert.AreEqual(sim.Config.LineLength, r, 0.01);
        }

        [Test]
        public void DifferentialPull_TurnsKiteRight()
        {
            var sim = new KiteSim(KiteConfiguration.KrdFury());
            var state = KiteState.FromSpherical(0, 45.0 * MathD.Deg2Rad, sim.Config.LineLength);
            state.Launched = true;

            var initialForward = state.Orientation;
            for (int i = 0; i < 100; i++)
            {
                // Right line pulled more => kite yaws to the right (positive X)
                sim.Step(state, 0.016, -1.0, 1.0);
            }

            Assert.Greater(state.Orientation.X, initialForward.X);
        }

        [Test]
        public void BothPulled_FullBrakes_LosesAltitude()
        {
            var sim = new KiteSim(KiteConfiguration.KrdFury());
            var state = KiteState.FromSpherical(0, 60.0 * MathD.Deg2Rad, sim.Config.LineLength);
            state.Launched = true;

            double initialY = state.Position.Y;
            for (int i = 0; i < 200; i++)
            {
                sim.Step(state, 0.016, 1.0, 1.0);
            }

            Assert.Less(state.Position.Y, initialY);
        }
    }
}

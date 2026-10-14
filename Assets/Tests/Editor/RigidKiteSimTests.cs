using NUnit.Framework;

namespace Kitesim.Core.Tests
{
    public class RigidKiteSimTests
    {
        [Test]
        public void GravityOnlySettlesOnTheGroundWithSlackLines()
        {
            RigidKiteSim sim = RigidKiteSim.HundredSixteen();
            RigidKiteState state = sim.SpawnDownwind(elevationY: 15.0);

            for (double t = 0.0; t < 10.0; t += 1.0 / 50.0)
                sim.Step(state, 1.0 / 50.0);

            Assert.GreaterOrEqual(state.Position.Y, -0.05, "must not fall through the ground");
            Assert.AreEqual(0.0, sim.Lines.TensionLeft);
            Assert.AreEqual(0.0, sim.Lines.TensionRight);
            Assert.Less(sim.LineLength(state, left: true), LineSystem.RestLength);
            Assert.Less(sim.LineLength(state, left: false), LineSystem.RestLength);
        }

        [Test]
        public void FlyingAttitudeAtSixMetresPerSecondLiftsMoreThanTheWeight()
        {
            RigidKiteSim sim = RigidKiteSim.HundredSixteen();
            RigidKiteState state = sim.SpawnDownwind(elevationY: 15.0);
            Vec3 wind = new Vec3(0.0, 0.0, 6.0);
            PanelAero.Accumulate(
                sim.Panels, state.Orientation, Vec3.Zero, Vec3.Zero, wind, sim.AirDensity,
                out Vec3 force, out Vec3 _);

            double weight = sim.Inertia.Mass * RigidKiteSim.Gravity;
            Assert.Greater(force.Y, weight, "upward aero must beat the weight at the flying attitude");
        }

        [Test]
        public void SixMetreWindHoldsTheKiteOffTheGroundForSixtySeconds()
        {
            RigidKiteSim sim = RigidKiteSim.HundredSixteen();
            sim.Wind.Speed = 6.0;
            RigidKiteState state = sim.SpawnDownwind(elevationY: 15.0);

            for (double t = 0.0; t < 60.0; t += 1.0 / 50.0)
                sim.Step(state, 1.0 / 50.0);

            Assert.Greater(state.Position.Y, 1.0, "still hanging, not on the grass");
            Vec3 nose = state.Orientation * new Vec3(0.0, 0.0, 1.0);
            Assert.Greater(nose.Y, 0.0, "nose still points generally up");
            AssertLineMatchesTension(sim, state, left: true);
            AssertLineMatchesTension(sim, state, left: false);
        }

        private static void AssertLineMatchesTension(RigidKiteSim sim, RigidKiteState state, bool left)
        {
            double dist = sim.LineLength(state, left);
            double tension = left ? sim.Lines.TensionLeft : sim.Lines.TensionRight;
            Assert.GreaterOrEqual(dist, LineSystem.RestLength - 0.002);
            Assert.AreEqual(LineSystem.RestLength + tension / LineSystem.Stiffness, dist, 0.03);
        }

        [Test]
        public void ViewPlacesTheKiteDownwindOfThePilotWithTheNoseTowardTheZenith()
        {
            RigidKiteSim sim = RigidKiteSim.HundredSixteen();
            RigidKiteState state = sim.SpawnDownwind(elevationY: 15.0);
            KiteStateView view = sim.View(state);

            Assert.IsFalse(view.Landed);
            Assert.Greater(view.WorldPosition.Y, 1.0);
            Assert.Greater(view.Direction.Z, 0.0, "downwind of the origin");
            Assert.Greater(view.Heading.Y, 0.0, "nose toward the sky so the mesh can aim");
            Assert.AreEqual(view.WorldPosition.X, view.PilotPosition.X + view.Direction.X * view.LineLength, 1e-9);
            Assert.AreEqual(view.WorldPosition.Y, view.PilotPosition.Y + view.Direction.Y * view.LineLength, 1e-9);
            Assert.AreEqual(view.WorldPosition.Z, view.PilotPosition.Z + view.Direction.Z * view.LineLength, 1e-9);
        }
    }
}

using NUnit.Framework;

namespace Kitesim.Core.Tests
{
    public class RigidKiteSimTests
    {
        [Test]
        public void GravityOnlyKeepsBothLinesAtFortyFiveMetresAndAboveTheGround()
        {
            RigidKiteSim sim = RigidKiteSim.HundredSixteen();
            RigidKiteState state = sim.SpawnDownwind(elevationY: 15.0);

            for (double t = 0.0; t < 10.0; t += 1.0 / 50.0)
                sim.Step(state, 1.0 / 50.0);

            Assert.GreaterOrEqual(state.Position.Y, -0.05, "must not fall through the ground");
            Assert.AreEqual(45.0, sim.LineLength(state, left: true), 0.01);
            Assert.AreEqual(45.0, sim.LineLength(state, left: false), 0.01);
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
            Assert.AreEqual(45.0, sim.LineLength(state, left: true), 0.05);
            Assert.AreEqual(45.0, sim.LineLength(state, left: false), 0.05);
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

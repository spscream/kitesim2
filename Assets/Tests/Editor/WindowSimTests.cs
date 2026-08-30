using NUnit.Framework;

namespace Kitesim.Core.Tests
{
    public class WindowSimTests
    {
        private const double Dt = 0.02;

        private static WindowSim MakeSim(double windSpeed = 5.0)
        {
            return new WindowSim(new WindowConfiguration())
            {
                Wind = new WindField { Speed = windSpeed, Direction = Vec3.Forward }
            };
        }

        private static WindowKiteState Flying(Vec3 direction, Vec3 heading, double speed)
        {
            Vec3 d = direction.Normalized();
            Vec3 h = heading - d * Vec3.Dot(heading, d);
            return new WindowKiteState
            {
                Direction = d,
                Heading = h.Normalized(),
                Speed = speed,
                Landed = false
            };
        }

        private static void Run(WindowSim sim, WindowKiteState state, double seconds,
            double handLeft = 0.0, double handRight = 0.0, double walk = 0.0)
        {
            int steps = (int)(seconds / Dt);
            for (int i = 0; i < steps; i++)
                sim.Step(state, Dt, handLeft, handRight, walk);
        }

        [Test]
        public void GlideRatio_ComesFromThePolarPlusParasiticDrag()
        {
            var config = new WindowConfiguration();
            var (cl, cd) = AeroPolar.Evaluate(config.TrimAngleOfAttack);
            Assert.AreEqual(cl / (cd + config.ParasiticDrag), config.GlideRatio, 1e-12);
            Assert.AreEqual(6.04, config.GlideRatio, 0.05);
        }

        [Test]
        public void AtWindowCentre_SpeedApproachesGlideRatioTimesWind()
        {
            var sim = MakeSim(5.0);
            var state = Flying(Vec3.Forward, Vec3.Up, 0.0);

            // Freeze the kite in place so only the speed relaxation is exercised.
            sim.Config.LineLength = 1e9;
            Run(sim, state, 5.0);

            Assert.AreEqual(5.0 * sim.Config.GlideRatio, state.Speed, 0.5);
            Assert.That(state.Speed, Is.InRange(25.0, 35.0));
        }

        [Test]
        public void AtWindowEdge_SpeedFallsToThePowerFloor()
        {
            var sim = MakeSim(5.0);
            sim.Config.LineLength = 1e9;
            // Directly crosswind at ground level: theta = 90 degrees.
            var state = Flying(new Vec3(1, 0, 0), Vec3.Up, 30.0);

            Run(sim, state, 5.0);

            double expected = sim.Config.GlideRatio * sim.Config.PowerFloor * 5.0;
            Assert.AreEqual(expected, state.Speed, 0.2);
            Assert.Less(state.Speed, 3.0);
        }

        [Test]
        public void HeadingTowardZenith_FliesUpTheMeridian()
        {
            var sim = MakeSim(6.0);
            var state = Flying(Vec3.Forward, Vec3.Up, 30.0);

            Run(sim, state, 3.0);

            // Gravity cannot turn a kite that is flying straight up, so it climbs
            // along the meridian and the azimuth stays at zero.
            Assert.Greater(sim.View(state).ElevationRad, 60.0 * MathD.Deg2Rad);
            Assert.AreEqual(0.0, sim.View(state).AzimuthRad, 1e-9);
        }

        [Test]
        public void DirectionAndHeadingStayUnitAndOrthogonal()
        {
            var sim = MakeSim(6.0);
            var state = Flying(Vec3.Forward, Vec3.Up, 25.0);

            Run(sim, state, 20.0, handLeft: -0.3, handRight: 0.5);

            Assert.AreEqual(1.0, state.Direction.Length(), 1e-9);
            Assert.AreEqual(1.0, state.Heading.Length(), 1e-9);
            Assert.AreEqual(0.0, Vec3.Dot(state.Direction, state.Heading), 1e-9);
        }
    }
}

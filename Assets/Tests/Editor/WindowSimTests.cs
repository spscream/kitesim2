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
        public void GreatCircleAdvance_TransportsTheHeadingForwardNotBackward()
        {
            // Pins the sign in `nextHeading = t * cos - p * sin`.
            //
            // At ordinary flight sigma (speed * dt / LineLength, well under 45
            // degrees per step at any realistic speed, dt and line length),
            // Orthonormalize's projection exactly recovers the correctly
            // transported tangent even if that sign is flipped: for any unit
            // p perpendicular to t and any sigma, projecting
            // (t*cos + p*sin) off (p*cos + t*sin) and renormalising reduces
            // algebraically to sign(cos(2*sigma)) * (t*cos - p*sin), and
            // cos(2*sigma) is positive for any sigma a kite racks up in one
            // step at 20-50 Hz. So no trajectory sampled at ordinary step
            // sizes, however long it runs, can tell the two signs apart --
            // confirmed both by this derivation and by direct experiment (5 s
            // and 20 s runs from the window centre heading at the zenith,
            // bit-identical between the correct and flipped sign). The only
            // place the bug is externally observable is where
            // cos(2*sigma) goes negative, i.e. sigma past 45 degrees in a
            // single step, which flips the corrected heading to point
            // backward. This test forces that with a short line length so a
            // single realistic-speed step crosses the threshold, then
            // compares against the closed-form great-circle transport.
            var config = new WindowConfiguration { LineLength = 0.5 };
            var sim = new WindowSim(config)
            {
                Wind = new WindField { Speed = 6.0, Direction = Vec3.Forward }
            };

            // Start exactly at the target speed so the speed-relaxation step
            // inside Step() leaves it unchanged, keeping sigma known and
            // deterministic.
            double speed = config.GlideRatio * sim.EffectiveWind(Vec3.Forward);
            var state = new WindowKiteState
            {
                Direction = Vec3.Forward,
                Heading = Vec3.Up,
                Speed = speed,
                Landed = false
            };

            double sigma = speed * Dt / config.LineLength;
            Assert.Greater(sigma, MathD.PI / 4.0,
                "the test needs sigma past the point where Orthonormalize stops masking the sign");

            Vec3 expectedHeading = (Vec3.Up * MathD.Cos(sigma) - Vec3.Forward * MathD.Sin(sigma)).Normalized();

            sim.Step(state, Dt, 0.0, 0.0, 0.0);

            Assert.Greater(Vec3.Dot(state.Heading, expectedHeading), 0.9,
                "the transported heading must point along the closed-form great-circle tangent, not backward");
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

        // Direction of turn is asserted on AccumulatedTurn, which is the unwrapped
        // integral of the turn rate, and over a short window. HeadingRad comes from
        // Atan2 and so folds at +/-180 degrees, and this kite turns far too fast for
        // that to be safe: at full input in 6 m/s wind the rate is around 7.5 rad/s,
        // which crosses the branch cut in under half a second. Asserting the
        // unwrapped quantity keeps these tests valid if SteeringGain is ever retuned.

        [Test]
        public void PullingTheRightHand_TurnsTheKiteToThePilotsRight()
        {
            var sim = MakeSim(6.0);
            var state = Flying(Vec3.Forward, Vec3.Up, 25.0);

            Run(sim, state, 0.1, handLeft: 0.0, handRight: 0.7);

            // Positive turn is from "toward zenith" toward "toward the pilot's right".
            Assert.Greater(state.AccumulatedTurn, 0.2);
            Assert.Greater(sim.View(state).HeadingRad, 0.2);
        }

        [Test]
        public void PullingTheLeftHand_TurnsTheKiteToThePilotsLeft()
        {
            var sim = MakeSim(6.0);
            var state = Flying(Vec3.Forward, Vec3.Up, 25.0);

            Run(sim, state, 0.1, handLeft: 0.7, handRight: 0.0);

            Assert.Less(state.AccumulatedTurn, -0.2);
            Assert.Less(sim.View(state).HeadingRad, -0.2);
        }

        [Test]
        public void TurnRate_IsLinearInSteeringInput()
        {
            var sim = MakeSim(6.0);
            var half = Flying(Vec3.Forward, Vec3.Up, 25.0);
            var full = Flying(Vec3.Forward, Vec3.Up, 25.0);

            double rateHalf = sim.TurnRate(half, 0.0, 0.35);
            double rateFull = sim.TurnRate(full, 0.0, 0.7);

            Assert.AreEqual(2.0, rateFull / rateHalf, 1e-9);
        }

        [Test]
        public void TurnRate_IsLinearInAirspeed()
        {
            var sim = MakeSim(6.0);
            var slow = Flying(Vec3.Forward, Vec3.Up, 10.0);
            var fast = Flying(Vec3.Forward, Vec3.Up, 20.0);

            // Straight up the meridian the gravity term vanishes, leaving only the
            // steering term, which is what the oracle law says is linear in v_app.
            double rateSlow = sim.TurnRate(slow, 0.0, 0.7);
            double rateFast = sim.TurnRate(fast, 0.0, 0.7);

            Assert.AreEqual(2.0, rateFast / rateSlow, 1e-9);
        }

        [Test]
        public void GravityDoesNotTurnAKiteFlyingStraightUp()
        {
            var sim = MakeSim(6.0);
            var state = Flying(Vec3.Forward, Vec3.Up, 25.0);

            Assert.AreEqual(0.0, sim.TurnRate(state, 0.0, 0.0), 1e-12);
        }

        [Test]
        public void GravityDroopsAKiteFlyingAcrossTheWindow()
        {
            var sim = MakeSim(6.0);
            // Flying toward the pilot's right at the window centre.
            var state = Flying(Vec3.Forward, new Vec3(1, 0, 0), 25.0);

            double rate = sim.TurnRate(state, 0.0, 0.0);

            // Positive rate turns the heading from "right" on toward "down".
            Assert.Greater(rate, 0.0);
            Assert.AreEqual(9.80665 / 25.0, rate, 1e-9);
        }

        [Test]
        public void GravityTermMatchesTheClosedFormOfTheOracleLaw()
        {
            var sim = MakeSim(6.0);
            double beta = 40.0 * MathD.Deg2Rad;
            double psi = 120.0 * MathD.Deg2Rad;
            double speed = 18.0;

            Vec3 direction = new Vec3(0.0, MathD.Sin(beta), MathD.Cos(beta)).Normalized();
            Vec3 heading = KiteStateView.TangentUp(direction) * MathD.Cos(psi)
                           + KiteStateView.TangentRight(direction) * MathD.Sin(psi);
            var state = Flying(direction, heading, speed);

            double expected = 9.80665 * MathD.Cos(beta) * MathD.Sin(psi) / speed;
            Assert.AreEqual(expected, sim.TurnRate(state, 0.0, 0.0), 1e-9);
        }

        [Test]
        public void LineWraps_CountFullTurns()
        {
            var sim = MakeSim(6.0);

            // Start well above the horizon. Under full steering the heading turns
            // about eleven times faster than the kite travels across the sphere, so
            // it spins almost in place on a circle of roughly five degrees. Started
            // at ground level it would dip below the horizon and land (Task 7) long
            // before completing a full turn.
            double beta = 45.0 * MathD.Deg2Rad;
            var state = Flying(new Vec3(0.0, MathD.Sin(beta), MathD.Cos(beta)), Vec3.Up, 25.0);
            Assert.AreEqual(0, state.LineWraps);

            Run(sim, state, 6.0, handLeft: 0.0, handRight: 0.7);

            Assert.IsFalse(state.Landed, "the kite must stay airborne to wrap its lines");
            Assert.Greater(state.LineWraps, 0);
        }

        [Test]
        public void WalkingBack_RaisesTheEffectiveWind()
        {
            var sim = MakeSim(5.0);
            double still = sim.EffectiveWind(Vec3.Forward);

            sim.PilotVelocity = new Vec3(0.0, 0.0, -1.7);
            double walking = sim.EffectiveWind(Vec3.Forward);

            Assert.AreEqual(5.0, still, 1e-12);
            Assert.AreEqual(6.7, walking, 1e-12);
        }

        [Test]
        public void WalkingForward_LowersTheEffectiveWind()
        {
            var sim = MakeSim(5.0);
            sim.PilotVelocity = new Vec3(0.0, 0.0, 1.2);

            Assert.AreEqual(3.8, sim.EffectiveWind(Vec3.Forward), 1e-12);
        }

        [Test]
        public void WalkAxis_MovesThePilotAtTheConfiguredSpeeds()
        {
            var sim = MakeSim(5.0);

            sim.UpdatePilot(1.0, -1.0);
            Assert.AreEqual(-sim.Config.WalkSpeedBack, sim.PilotPosition.Z, 1e-12);
            Assert.AreEqual(-sim.Config.WalkSpeedBack, sim.PilotVelocity.Z, 1e-12);

            sim.PilotPosition = Vec3.Zero;
            sim.UpdatePilot(1.0, 1.0);
            Assert.AreEqual(sim.Config.WalkSpeedForward, sim.PilotPosition.Z, 1e-12);
        }

        [Test]
        public void PilotCannotWalkOutOfTheField()
        {
            var sim = MakeSim(5.0);

            for (int i = 0; i < 5000; i++)
                sim.UpdatePilot(Dt, -1.0);

            Assert.AreEqual(-sim.Config.FieldHalfSize, sim.PilotPosition.Z, 1e-9);
            Assert.AreEqual(0.0, sim.PilotVelocity.Z, 1e-9,
                "velocity must be zeroed at the boundary or the kite keeps gaining airspeed");
        }

        [Test]
        public void TheGravityTurnDominatesWhenTheKiteIsSlow()
        {
            // This is the mechanism by which a kite drops out of the sky in light
            // wind: the gravity turn goes as 1/v, so a slow kite flying across the
            // window is whipped toward the ground within a second, while a fast one
            // barely droops. Asserted on the rate itself, which is exact, rather
            // than on an integrated trajectory.
            var sim = MakeSim(5.0);
            var slow = Flying(Vec3.Forward, new Vec3(1, 0, 0), 6.0);
            var fast = Flying(Vec3.Forward, new Vec3(1, 0, 0), 30.0);

            double slowRate = sim.TurnRate(slow, 0.0, 0.0);
            double fastRate = sim.TurnRate(fast, 0.0, 0.0);

            Assert.AreEqual(9.80665 / 6.0, slowRate, 1e-9);
            Assert.Greater(slowRate, fastRate * 4.0);
            Assert.Greater(slowRate, 1.5,
                "over a radian per second: the kite droops to the ground within a second");
        }

        [Test]
        public void InLightWind_WalkingBackKeepsTheKiteFaster()
        {
            var passive = MakeSim(1.1);
            var active = MakeSim(1.1);
            var passiveKite = Flying(Vec3.Forward, Vec3.Up, 6.0);
            var activeKite = Flying(Vec3.Forward, Vec3.Up, 6.0);

            passive.Config.LineLength = 1e9;
            active.Config.LineLength = 1e9;

            Run(passive, passiveKite, 5.0);
            Run(active, activeKite, 5.0, walk: -1.0);

            Assert.Greater(activeKite.Speed, passiveKite.Speed * 1.5,
                "walking back must be the dominant control in light wind");
        }

        [Test]
        public void FlyingIntoTheGround_Lands()
        {
            var sim = MakeSim(6.0);
            var state = Flying(
                new Vec3(0.0, MathD.Sin(0.2), MathD.Cos(0.2)),
                new Vec3(0.0, -1.0, 0.0),
                25.0);

            Run(sim, state, 3.0);

            Assert.IsTrue(state.Landed);
            Assert.AreEqual(0.0, state.Direction.Y, 1e-9);
            Assert.AreEqual(0.0, state.Speed, 1e-12);
        }

        [Test]
        public void ALandedKiteDoesNotMove()
        {
            var sim = MakeSim(6.0);
            var state = WindowKiteState.OnGround(0.0);
            Vec3 before = state.Direction;

            Run(sim, state, 3.0, handRight: 0.7);

            Assert.AreEqual(before.X, state.Direction.X, 1e-12);
            Assert.AreEqual(before.Y, state.Direction.Y, 1e-12);
            Assert.AreEqual(before.Z, state.Direction.Z, 1e-12);
        }

        [Test]
        public void LaunchingFromTheGround_Climbs()
        {
            var sim = MakeSim(6.0);
            var state = WindowKiteState.OnGround(0.0);

            sim.Launch(state);
            Run(sim, state, 4.0);

            Assert.IsFalse(state.Landed);
            Assert.Greater(sim.View(state).ElevationRad, 20.0 * MathD.Deg2Rad);
        }

        [Test]
        public void StaysStableOverTenMinutesOfSteering()
        {
            var sim = MakeSim(8.0);
            var state = Flying(Vec3.Forward, Vec3.Up, 25.0);

            int steps = (int)(600.0 / Dt);
            for (int i = 0; i < steps; i++)
            {
                double phase = MathD.Sin(i * Dt * 0.7) * 0.7;
                sim.Step(state, Dt, -phase, phase, 0.0);

                if (state.Landed)
                    sim.Launch(state);
            }

            Assert.AreEqual(1.0, state.Direction.Length(), 1e-6);
            Assert.IsFalse(double.IsNaN(state.Speed));
            Assert.That(state.Speed, Is.InRange(0.0, 200.0));
        }

        [Test]
        public void IsDeterministic()
        {
            var first = MakeSim(7.0);
            var second = MakeSim(7.0);
            var a = Flying(Vec3.Forward, Vec3.Up, 20.0);
            var b = Flying(Vec3.Forward, Vec3.Up, 20.0);

            for (int i = 0; i < 3000; i++)
            {
                double u = MathD.Sin(i * 0.013) * 0.7;
                double walk = MathD.Cos(i * 0.007);
                first.Step(a, Dt, -u, u, walk);
                second.Step(b, Dt, -u, u, walk);
            }

            Assert.AreEqual(a.Direction.X, b.Direction.X);
            Assert.AreEqual(a.Direction.Y, b.Direction.Y);
            Assert.AreEqual(a.Direction.Z, b.Direction.Z);
            Assert.AreEqual(a.Speed, b.Speed);
        }
    }
}

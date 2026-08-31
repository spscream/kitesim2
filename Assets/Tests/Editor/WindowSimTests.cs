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

        /// <summary>
        /// Stretch the line until the kite barely advances along the sphere (sigma =
        /// speed * dt / LineLength), isolating the speed relaxation, without
        /// disturbing the aerodynamics. The length has to be neutralised in the polar
        /// too: tether drag is proportional to LineLength, so a 1e9 m line would drive
        /// the glide ratio to zero and the kite would relax to a standstill. Fold the
        /// real tether drag into ResidualDrag, then switch the length-dependent term
        /// off, which leaves GlideRatio bit-for-bit unchanged.
        /// </summary>
        private static void FreezeInPlace(WindowSim sim)
        {
            WindowConfiguration config = sim.Config;
            double before = config.GlideRatio;

            config.ResidualDrag += 0.25 * config.LineDragCoefficient * 2.0
                                   * config.LineDiameter * config.LineLength / config.SailArea;
            config.LineDragCoefficient = 0.0;
            config.LineLength = 1e9;

            Assert.AreEqual(before, config.GlideRatio, 1e-12,
                "freezing the kite must not change how well it glides");
        }

        private static void Run(WindowSim sim, WindowKiteState state, double seconds,
            double handLeft = 0.0, double handRight = 0.0, double walk = 0.0)
        {
            int steps = (int)(seconds / Dt);
            for (int i = 0; i < steps; i++)
                sim.Step(state, Dt, handLeft, handRight, walk);
        }

        [Test]
        public void GlideRatio_CorrectsThePolarForAFiniteWingAndAddsTheMissingDrag()
        {
            var config = new WindowConfiguration();
            var (clInfinite, cdProfile) = AeroPolar.Evaluate(config.TrimAngleOfAttack);

            // AeroPolar is thin-airfoil theory for an infinite span. Both corrections
            // below are textbook lifting-line results; see WindowConfiguration.
            double denominator = MathD.PI * config.OswaldEfficiency * config.AspectRatio;
            double cl = clInfinite / (1.0 + (2.0 * MathD.PI) / denominator);
            double cdInduced = cl * cl / denominator;
            double cdTether = 0.25 * config.LineDragCoefficient * 2.0
                              * config.LineDiameter * config.LineLength / config.SailArea;
            double cd = cdProfile + cdInduced + cdTether + config.ResidualDrag;

            Assert.AreEqual(cl / cd, config.GlideRatio, 1e-12);

            // The finite wing loses lift and gains induced drag, so the glide ratio
            // has to come out below the infinite-span figure, not above it.
            Assert.Less(config.GlideRatio, clInfinite / cdProfile);
            Assert.AreEqual(4.39, config.GlideRatio, 0.05);
        }

        [Test]
        public void GlideRatio_FallsAsTheOneFreeDragTermRises()
        {
            // ResidualDrag is the only un-derived number left in the glide ratio, and
            // it is the knob for re-calibrating against a real kite. Pin its range so
            // a change to the derivation cannot silently move the whole envelope.
            var config = new WindowConfiguration();

            config.ResidualDrag = 0.0;
            Assert.AreEqual(6.05, config.GlideRatio, 0.02);

            config.ResidualDrag = 0.08;
            Assert.AreEqual(3.44, config.GlideRatio, 0.02);
        }

        [Test]
        public void GlideRatio_FallsAsTheLinesGetLonger()
        {
            // Tether drag is the one term that grows with line length, so a kite on
            // longer lines must glide worse. Nothing else in the config depends on it.
            var config = new WindowConfiguration { LineLength = 25.0 };
            double onShortLines = config.GlideRatio;

            config.LineLength = 45.0;
            Assert.Less(config.GlideRatio, onShortLines);
        }

        [Test]
        public void AtWindowCentre_SpeedApproachesGlideRatioTimesWind()
        {
            var sim = MakeSim(5.0);
            var state = Flying(Vec3.Forward, Vec3.Up, 0.0);

            FreezeInPlace(sim);
            // Isolate the aerodynamic relaxation: this test is about where the
            // quasi-steady crosswind speed lands, and the kite is heading up the
            // meridian, so the along-track gravity term would bleed it. That term has
            // its own tests.
            sim.Config.GravityAlongHeading = 0.0;
            Run(sim, state, 5.0);

            Assert.AreEqual(5.0 * sim.Config.GlideRatio, state.Speed, 0.5);

            // The old bound here was 25-35 m/s, justified in the config by "50-80
            // km/h commonly quoted for sport kites" -- but 30 m/s is 108 km/h, so the
            // bound contradicted its own stated source. 16.7-25.0 m/s is that source
            // converted honestly: 60-90 km/h. See docs/kite-anatomy.md section 4.
            Assert.That(state.Speed, Is.InRange(16.7, 25.0));
        }

        [Test]
        public void GravityAlongHeading_IsExactlyTheAlongTrackComponent()
        {
            // The coefficient is derived, not fitted: the tether is radial and does
            // no work along the heading, so the along-track force is exactly
            // g * Dot(heading, down). Pinned one step at a time, with the kite
            // started at its quasi-steady speed so the relaxation term contributes
            // nothing and the gravity term is the only thing moving Speed.
            //
            // Held at 45 degrees of elevation: at ground level a nose-down kite lands
            // inside the first step and the term never gets to act.
            // The value is not a tuning knob: it is g, and nothing about this kite
            // enters it. Contrast WeightTurnCoefficient, which is fitted per kite.
            Assert.AreEqual(9.80665, new WindowConfiguration().GravityAlongHeading, 1e-12,
                "along-track gravity is derived, so the default must be g itself");

            double beta = 45.0 * MathD.Deg2Rad;
            Vec3 direction = new Vec3(0.0, MathD.Sin(beta), MathD.Cos(beta)).Normalized();
            Vec3 up = KiteStateView.TangentUp(direction);

            foreach (var (name, heading) in new[]
            {
                ("nose down", up * -1.0),
                ("nose up", up),
                ("level", KiteStateView.TangentRight(direction)),
            })
            {
                var sim = MakeSim(6.0);
                var state = Flying(direction, heading, 0.0);
                state.Speed = sim.Config.GlideRatio * sim.EffectiveWind(direction);
                double before = state.Speed;

                sim.Step(state, Dt, 0.0, 0.0, 0.0);

                double expected = before
                                  - sim.Config.GravityAlongHeading * Vec3.Dot(heading, Vec3.Up) * Dt;
                Assert.AreEqual(expected, state.Speed, 1e-9,
                    $"heading {name}: the along-track gravity term must be exactly "
                    + "g * Dot(heading, down) * dt, with nothing else moving Speed");
            }

            // The level case above is the one that pins the sign convention: a kite
            // flying across the window neither gains nor loses anything to gravity.
            var levelSim = MakeSim(6.0);
            var level = Flying(direction, KiteStateView.TangentRight(direction), 0.0);
            level.Speed = levelSim.Config.GlideRatio * levelSim.EffectiveWind(direction);
            double unchanged = level.Speed;
            levelSim.Step(level, Dt, 0.0, 0.0, 0.0);
            Assert.AreEqual(unchanged, level.Speed, 1e-12);
        }

        [Test]
        public void DivingOutOfTheWindowEdge_TradesHeightForSpeed()
        {
            // The owner's report: a real kite leaves the window edge far faster than
            // this model let it. The cause was that Speed could only ever relax
            // toward E * W_eff, making it a pure function of position -- a kite
            // pointed straight down out of the edge held its edge speed, flat,
            // forever, because the only way to speed up was to fly into more wind.
            //
            // Measured at 6 m/s: the fixed model runs 1.32 -> 2.5 -> 3.0 -> 3.8 ->
            // 5.3 m/s over three seconds. The second half of this test pins the old
            // defect by switching the term off, so a regression cannot pass quietly.
            foreach (bool gravity in new[] { true, false })
            {
                var sim = MakeSim(6.0);
                if (!gravity)
                    sim.Config.GravityAlongHeading = 0.0;

                Vec3 center = WindowChart.Center(sim.Wind);
                Vec3 right = WindowChart.Right(sim.Wind);
                double beta = 55.0 * MathD.Deg2Rad;
                Vec3 direction = (right * MathD.Cos(beta) + Vec3.Up * MathD.Sin(beta)).Normalized();

                // Nose down and inward, the way a pilot dives off the edge.
                Vec3 down = KiteStateView.TangentUp(direction) * -1.0;
                Vec3 inward = (center - direction * Vec3.Dot(center, direction)).Normalized();
                var state = Flying(direction, down + inward, 0.0);

                double edgeSpeed = sim.Config.GlideRatio * sim.EffectiveWind(direction);
                state.Speed = edgeSpeed;

                Run(sim, state, 3.0);

                if (gravity)
                    Assert.Greater(state.Speed, edgeSpeed * 3.0,
                        "diving off the edge must trade height for speed, not hold the edge speed");
                else
                    Assert.AreEqual(edgeSpeed, state.Speed, 0.05,
                        "with the term off the kite must show the old defect exactly: "
                        + "speed pinned to what the local wind alone sustains");
            }
        }

        [Test]
        public void AStalledClimb_IsHeldAtTheWindDrivenFloorAndTheStateKeepsMoving()
        {
            // Guard on an absorbing state this fix introduced and then removed. A
            // kite released on the window edge heading straight up is bled to a stop
            // by gravity; at exactly that heading the weight term's lever
            // Dot(down, Cross(heading, direction)) is also exactly zero, so the turn
            // rate vanishes at the same moment. Speed zero plus turn rate zero means
            // the state can never change again. Measured before the floor was added:
            // the kite sat frozen for a full 3000 s probe.
            var sim = MakeSim(6.0);
            Vec3 center = WindowChart.Center(sim.Wind);
            Vec3 right = WindowChart.Right(sim.Wind);
            double beta = 50.0 * MathD.Deg2Rad;
            Vec3 direction = (right * MathD.Cos(beta) + Vec3.Up * MathD.Sin(beta)).Normalized();
            var state = Flying(direction, KiteStateView.TangentUp(direction), 0.0);
            state.Speed = sim.Config.GlideRatio * sim.EffectiveWind(direction);

            double floor = sim.Config.GlideRatio * sim.Config.PowerFloor * 6.0;
            Vec3 wasAt = state.Direction;

            Run(sim, state, 10.0);

            Assert.GreaterOrEqual(state.Speed, floor - 1e-9,
                "gravity may bleed a climbing kite almost to a stop, but never to a "
                + "dead one -- zero speed here is an absorbing state");
            Assert.Greater(1.0 - Vec3.Dot(state.Direction, wasAt), 1e-6,
                "the kite must still be moving somewhere, not frozen on the edge");
        }

        [Test]
        public void AtWindowEdge_SpeedFallsToThePowerFloor()
        {
            var sim = MakeSim(5.0);
            FreezeInPlace(sim);
            // Same isolation as AtWindowCentre: this is about the power law, not the
            // along-track gravity term.
            sim.Config.GravityAlongHeading = 0.0;
            // Directly crosswind at ground level: theta = 90 degrees.
            var state = Flying(new Vec3(1, 0, 0), Vec3.Up, 30.0);

            Run(sim, state, 5.0);

            double expected = sim.Config.GlideRatio * sim.Config.PowerFloor * 5.0;
            Assert.AreEqual(expected, state.Speed, 0.2);
            Assert.Less(state.Speed, 3.0);
        }

        [Test]
        public void DrivenPastTheWindowEdge_SpeedNeverGoesNegative()
        {
            // This model has no reverse flight: EffectiveWind is negative upwind of
            // the window edge (deliberately -- see EffectiveWind's comment), which
            // pulls targetSpeed negative and, before the Speed clamp in Step, pulled
            // Speed itself negative -- a negative Speed then advances the kite
            // backwards along its heading, which a real kite does not do.
            //
            // Measured on the pre-fix model (Speed not clamped): a sweep of 40
            // steering patterns (frequency 0.1-2.0 Hz, amplitude 0.2-1.0, wind
            // 6 m/s) driving a kite from just past the window edge reached Speed as
            // low as -27.0 m/s within a 35 s run; other wind speeds pushed that
            // past -33 m/s. The fix brief independently measured -19.5 m/s on its
            // own sweep. Both confirm the excursion is large and this is a real
            // defect, not a rounding artifact.
            //
            // The excursions are transient -- SpeedLag relaxation pulls Speed back
            // toward zero and then toward a positive target once the kite's
            // heading brings it back downwind of the edge -- so a final-state
            // assertion would miss them. Sample every step.
            (double freq, double amp)[] patterns =
            {
                (0.1, 0.4), (0.2, 0.8), (0.2, 1.0), (0.3, 0.6), (0.5, 1.0),
                (0.7, 0.7), (1.0, 0.5), (1.5, 1.0), (2.0, 0.3), (0.7, 0.0),
            };

            foreach (var (freq, amp) in patterns)
            {
                var sim = MakeSim(6.0);
                double beta = 20.0 * MathD.Deg2Rad;
                double phi = 100.6 * MathD.Deg2Rad;
                Vec3 direction = new Vec3(
                    MathD.Cos(beta) * MathD.Sin(phi),
                    MathD.Sin(beta),
                    MathD.Cos(beta) * MathD.Cos(phi)).Normalized();
                var state = new WindowKiteState
                {
                    Direction = direction,
                    Heading = KiteStateView.TangentUp(direction),
                    Speed = 25.0,
                    Landed = false
                };

                int steps = (int)(35.0 / Dt);
                for (int i = 0; i < steps; i++)
                {
                    double phase = MathD.Sin(i * Dt * freq) * amp;
                    sim.Step(state, Dt, -phase, phase, 0.0);

                    Assert.GreaterOrEqual(state.Speed, 0.0,
                        $"Speed went negative at step {i} under pattern freq={freq}, amp={amp}");

                    if (state.Landed)
                        break;
                }
            }
        }

        [Test]
        public void EffectiveWind_ThePowerFloorDoesNotApplyUpwind()
        {
            var sim = MakeSim(6.0);

            // Directly upwind of the pilot: the kite is being blown backwards, not
            // driven. The floor exists for the zenith and the edge of the window,
            // where the along-line component is zero, not for the upwind hemisphere,
            // where it is negative.
            double along = sim.EffectiveWind(-Vec3.Forward);

            Assert.LessOrEqual(along, 0.0,
                "the power floor must not hand a kite upwind of the pilot artificial driving wind");
            Assert.AreEqual(-6.0, along, 1e-12);
        }

        [Test]
        public void KiteDrivenPastTheWindowEdge_IsClampedBackAndRecovers()
        {
            // This test used to be KiteDrivenPastTheWindowEdge_ParksRatherThanReaching
            // TheUpwindHorizon, and documented (correctly, at the time) that from this
            // exact start the kite loses its driving wind, decelerates to exactly
            // zero, and parks motionless in the air at theta = 99.4 degrees forever --
            // state (ii) from the fix brief: alive in the sense of not climbing
            // further, but Speed == 0 makes it just as frozen as any other point past
            // the edge, since sigma = Speed*dt/LineLength is then also exactly zero.
            //
            // The hemisphere clamp in Step removes that state entirely, and this is
            // also the fix brief's required boundary-trap check: does clamping create
            // a *new* absorbing state pressed against theta = 90? Measured from this
            // same start (beta = 20 degrees, phi = 100.6 degrees, theta0 = 99.95
            // degrees, already past the edge, neutral hands): the very first Step
            // call snaps Direction back onto theta = 90.000000 exactly (the clamp
            // fires on the great-circle advance computed from the invalid starting
            // direction, same as it would on any in-sim excursion). Speed does not
            // drop to zero there -- the power floor applies exactly at the edge, so
            // Speed only relaxes from 25 m/s toward the floor-driven target (measured
            // 23.44 m/s after step 0) -- and the nonzero gravity turn (this start is
            // off the psi = 0 meridian, deliberately) rotates the heading enough that
            // theta is already measurably back inside the window (89.96 degrees) by
            // the very next step. The kite goes on to land normally at t = 13.56 s
            // (measured), never re-crossing theta = 90 again after step 0. No trap:
            // the boundary is not sticky.
            var sim = MakeSim(6.0);
            Vec3 center = WindowChart.Center(sim.Wind);

            double beta = 20.0 * MathD.Deg2Rad;
            double phi = 100.6 * MathD.Deg2Rad;
            Vec3 direction = new Vec3(
                MathD.Cos(beta) * MathD.Sin(phi),
                MathD.Sin(beta),
                MathD.Cos(beta) * MathD.Cos(phi)).Normalized();

            double theta0 = MathD.Acos(MathD.Clamp(Vec3.Dot(direction, center), -1.0, 1.0)) * MathD.Rad2Deg;
            Assert.Greater(theta0, 90.0, "the start must already be past the window edge");

            var state = new WindowKiteState
            {
                Direction = direction,
                Heading = KiteStateView.TangentUp(direction),
                Speed = 25.0,
                Landed = false
            };

            // theta0 itself is the deliberately-invalid past-the-edge start, before
            // Step has had a chance to run the clamp at all; maxTheta tracks the
            // clamp's effect from the first Step call onward, not that starting
            // excursion.
            double maxTheta = 0.0;
            // Generous relative to the measured 13.56 s landing time.
            int steps = (int)(60.0 / Dt);
            for (int i = 0; i < steps; i++)
            {
                sim.Step(state, Dt, 0.0, 0.0, 0.0);
                double theta = MathD.Acos(MathD.Clamp(Vec3.Dot(state.Direction, center), -1.0, 1.0)) * MathD.Rad2Deg;
                if (theta > maxTheta) maxTheta = theta;
                if (state.Landed)
                    break;
            }

            Assert.IsTrue(state.Landed,
                "released past the edge with neutral hands, the kite must recover into the window and eventually land, not park forever past the edge");
            Assert.AreEqual(0.0, state.Speed, 1e-12, "a landed kite has zero speed");
            Assert.Less(maxTheta, 90.0 + 1e-6,
                "after the first Step call, the clamp must keep the kite inside the downwind hemisphere for the rest of the run");
        }

        [Test]
        public void KiteLeftAlone_FromSeveralElevations_StaysInsideTheWindowAndDoesNotFreeze()
        {
            // Before the hemisphere clamp in Step, a kite left with neutral hands
            // crossed the window edge and froze permanently (Speed pinned at exactly
            // 0.0, so sigma = Speed*dt/LineLength is also exactly zero and nothing in
            // the state can change again): measured at 4.52 s from 15 degrees
            // elevation, 4.20 s from 30, 3.82 s from 45, 3.22 s from 60, 1.74 s from
            // 75 (6 m/s wind). This test locks in both halves of the fix: the kite
            // never leaves the downwind hemisphere, and it never ends up in that
            // frozen state -- it settles into the legitimate parked-at-the-edge
            // state instead (see KiteLeftAlone_DriftsToAndParksAtTheWindowEdge and
            // ParkedAtTheEdge_SteeringTheNoseDownLandsIt), which is alive and
            // steerable, not landed but also not stuck.
            //
            // Measured over 120 s (well past every one of the pre-fix freeze times
            // above) for all five elevations: theta caps at exactly 90.000000
            // degrees -- never exceeds it -- and every one of them is airborne with
            // Speed == 1.811800 m/s (the same floor-driven speed as the other
            // parking tests) at the end of the run, i.e. steerable, not frozen.
            double[] elevationsDeg = { 15.0, 30.0, 45.0, 60.0, 75.0 };
            foreach (var elevationDeg in elevationsDeg)
            {
                var sim = MakeSim(6.0);
                Vec3 center = WindowChart.Center(sim.Wind);
                double beta = elevationDeg * MathD.Deg2Rad;
                var state = Flying(new Vec3(0.0, MathD.Sin(beta), MathD.Cos(beta)), Vec3.Up, 25.0);

                int steps = (int)(120.0 / Dt);
                for (int i = 0; i < steps; i++)
                {
                    sim.Step(state, Dt, 0.0, 0.0, 0.0);
                    double theta = MathD.Acos(MathD.Clamp(Vec3.Dot(state.Direction, center), -1.0, 1.0)) * MathD.Rad2Deg;
                    Assert.Less(theta, 90.0 + 1e-6,
                        $"elevation {elevationDeg}: the kite must never leave the downwind hemisphere (step {i})");
                    if (state.Landed)
                        break;
                }

                Assert.IsTrue(state.Landed || state.Speed > 0.0,
                    $"elevation {elevationDeg}: after 120 s the kite must either have landed or still be moving (steerable), not sit frozen with Speed == 0 while airborne");
            }
        }

        [Test]
        public void ReleasedExactlyOnTheEdge_TheBoundaryIsNotATrap()
        {
            // The fix brief's explicit boundary-trap check: the clamp could in
            // principle create a *new* absorbing state by pinning a kite to
            // theta = 90 forever. Verified by measurement, not assumed: release a
            // kite sitting exactly on the edge circle (Dot(Direction, center) == 0,
            // constructed the same way ProjectOntoEdge does) at points away from the
            // fully symmetric phi = 90 meridian used by ParkedAtTheEdge_Steering
            // TheNoseDownLandsIt, with neutral hands, and confirm each one escapes
            // the boundary rather than sitting on it forever.
            //
            // Measured (6 m/s wind, Speed starting at the floor-driven equilibrium,
            // heading toward the local zenith): none of these get stuck. All land
            // within 57-90 s, and the escape is pure geometry, not the weight term.
            // The zenith lies on the edge circle (it is perpendicular to the
            // horizontal window centre), so the meridian a kite released nose-up
            // follows IS the edge circle: it slides along the boundary, over the top
            // and down the far side until Direction.Y crosses zero. The power floor
            // is what makes that possible -- it keeps Speed nonzero at the edge, so
            // sigma = Speed*dt/LineLength never reaches zero and the state keeps
            // changing.
            //
            // The weight term contributes nothing here: at the release orientation
            // its turn rate is Dot((0,-c,0), Cross(Heading, Direction)) / v, and a
            // nose-up heading makes that cross product horizontal, so the dot product
            // is exactly zero. Measured to confirm -- the escape times below are
            // identical to the digit at WeightTurnCoefficient 1.0, 2.0, 3.0 and
            // 9.80665. What does move them is the glide ratio, through Speed: these
            // budgets are set for the calibrated E of about 4.39 and were 40-75 s at
            // the old, roughly 40 percent faster E of 6.04.
            (double betaDeg, double phiDeg, double maxSeconds)[] cases =
            {
                (30.0, 100.0, 120.0),
                (50.0, 150.0, 120.0),
                (10.0, 179.0, 120.0),
            };

            foreach (var (betaDeg, phiDeg, maxSeconds) in cases)
            {
                var sim = MakeSim(6.0);
                Vec3 center = WindowChart.Center(sim.Wind);
                double beta = betaDeg * MathD.Deg2Rad;
                double phi = phiDeg * MathD.Deg2Rad;
                Vec3 raw = new Vec3(
                    MathD.Cos(beta) * MathD.Sin(phi),
                    MathD.Sin(beta),
                    MathD.Cos(beta) * MathD.Cos(phi)).Normalized();
                Vec3 direction = (raw - center * Vec3.Dot(raw, center)).Normalized();

                var state = new WindowKiteState
                {
                    Direction = direction,
                    Heading = KiteStateView.TangentUp(direction),
                    Speed = sim.Config.GlideRatio * sim.Config.PowerFloor * 6.0,
                    Landed = false
                };

                bool escaped = false;
                int steps = (int)(maxSeconds / Dt);
                for (int i = 0; i < steps; i++)
                {
                    sim.Step(state, Dt, 0.0, 0.0, 0.0);
                    double theta = MathD.Acos(MathD.Clamp(Vec3.Dot(state.Direction, center), -1.0, 1.0)) * MathD.Rad2Deg;
                    Assert.Less(theta, 90.0 + 1e-6,
                        $"beta={betaDeg} phi={phiDeg}: must stay inside the downwind hemisphere (step {i})");
                    if (state.Landed || theta < 90.0 - 1e-6)
                    {
                        escaped = true;
                        break;
                    }
                }

                Assert.IsTrue(escaped,
                    $"beta={betaDeg} phi={phiDeg}: a kite released exactly on the edge must eventually land or re-enter the window within {maxSeconds} s, not sit pinned to theta = 90 forever");
            }
        }

        [Test]
        public void KiteLeftAlone_DriftsToAndParksAtTheWindowEdge()
        {
            // A real kite parked at the window edge hangs there motionless, and
            // that is the basis of the standard strong-wind landing technique: fly
            // to the edge, park, put it down nose-first (see docs/kite-anatomy.md
            // section 3 and the design spec section 8). This test locks that in so
            // a future change -- e.g. an along-track gravity term to make the kite
            // "fall out" of the edge -- cannot silently remove it.
            //
            // Before the hemisphere clamp in Step, this test's own comment was
            // itself wrong about the settle time (claimed "around t = 50 s"): a
            // kite flying with zero steering input from 45 degrees elevation
            // actually drifted past the edge to theta = 90.725646 degrees and
            // froze there by t = 3.84 s, with Speed pinned at exactly zero -- state
            // (ii) from the fix brief, an absorbing bug, not a parked kite.
            //
            // With the clamp: measured over the same 600 s run (30000 steps), the
            // kite is pulled back to exactly theta = 90 degrees and, in this
            // particular symmetric case (heading stays in the x = 0 plane the
            // whole way up, so the gravity turn term is identically zero
            // throughout), settles precisely at the zenith, Direction ==
            // (0, 1, 0), by t = 12.18 s (step 609). From there Direction and Speed
            // are bit-identical to the end of the run -- a true fixed point, this
            // time at the floor-driven speed (see EffectiveWind's power floor) of
            // 1.8118002554 m/s, not the frozen Speed == 0 of the pre-clamp bug.
            // The zenith is itself part of the theta = 90 edge (Dot(zenith,
            // center) == 0 for any wind direction), so this is the same kind of
            // flyable parked state as ParkedAtTheEdge_SteeringTheNoseDownLandsIt's
            // start, not a new one.
            var sim = MakeSim(6.0);
            Vec3 center = WindowChart.Center(sim.Wind);
            double beta = 45.0 * MathD.Deg2Rad;
            var state = Flying(new Vec3(0.0, MathD.Sin(beta), MathD.Cos(beta)), Vec3.Up, 25.0);

            int steps = (int)(600.0 / Dt);
            for (int i = 0; i < steps; i++)
            {
                sim.Step(state, Dt, 0.0, 0.0, 0.0);
                Assert.IsFalse(state.Landed, $"the parked kite must stay airborne, not touch down (step {i})");
            }

            double theta = MathD.Acos(MathD.Clamp(Vec3.Dot(state.Direction, center), -1.0, 1.0)) * MathD.Rad2Deg;
            Assert.That(theta, Is.InRange(85.0, 95.0),
                "a parked kite's angular distance from the window centre must stay near the 90-degree edge");
            Assert.AreEqual(sim.Config.GlideRatio * sim.Config.PowerFloor * 6.0, state.Speed, 0.01,
                "a parked kite settles to the power floor's driving speed, not to zero -- it must stay steerable");
        }

        [Test]
        public void ParkedAtTheEdge_SteeringTheNoseDownLandsIt()
        {
            // The pilot technique the parking behaviour exists to support: in
            // strong wind, fly to the edge, park, then put the kite down under
            // control by pointing its nose toward the ground (see
            // docs/kite-anatomy.md section 3). The nose-down heading is reached
            // by driving the hand inputs through Step, not by hand-placing
            // Heading, so this exercises the actual control path.
            //
            // The parked starting state is constructed at phi = 90 degrees exactly
            // (on the meridian that forms the theta = 90 edge for any elevation --
            // see EffectiveWind), heading toward the zenith (psi = 0), which keeps
            // EffectiveWind pinned at exactly the power floor. Measured: left alone
            // there, Speed relaxes from 0 and settles at 1.8118 m/s within about
            // 5 s and theta stays exactly 90.000 degrees (this is the "effective
            // wind is at the floor, speed settles around 1.8 m/s" state referenced
            // in the fix brief) -- a genuinely flyable parked state, unlike the
            // upwind-of-the-edge parked state in
            // KiteLeftAlone_DriftsToAndParksAtTheWindowEdge, where Speed sits at
            // exactly zero and the kite cannot move at all.
            //
            // From there, holding handLeft = 0.2 (a moderate, sustained pull) turns
            // the nose from psi = 0 to psi around 177 degrees (pointing at the
            // ground, not exactly 180 because the constant steering input is
            // balanced against the gravity term) within about 2 s, and holding it
            // there measurably lands the kite: elevation descends from 40.7 degrees
            // to 0 over 19.80 s, at a Speed that stays close to the measured
            // 1.8118 m/s the entire way down.
            //
            // Working steering band, re-measured after the hemisphere clamp was
            // added to Step: an earlier measurement of this same scenario (on the
            // code before that clamp existed) found handLeft = 0.10-0.30 worked,
            // 0.05 and 0.32 were the true edges of that, and 0.35 failed to land
            // within 60 s. That failure mode no longer exists -- swept handLeft
            // from 0.01 to 1.0 (0.01, 0.02, 0.03, 0.05, 0.08, 0.10, 0.15, 0.20,
            // 0.25, 0.30, 0.32, 0.33, 0.35, 0.40, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0) and
            // every single value lands within 60 s (19.1-21.5 s, tightening
            // monotonically as handLeft grows). Confirmed this is the clamp's
            // doing, not a coincidence: re-running the same sweep with the clamp
            // disabled reproduces the old result exactly (0.01-0.32 land, 0.33
            // lands slower at 20.26 s, 0.35 and everything above it never lands
            // within 60 s). What changed: without the clamp, a strong enough pull
            // could transiently push the descending kite's Direction upwind of the
            // edge, where it hit the old freeze (Speed pinned at 0, sigma pinned at
            // 0) and never reached the ground. The clamp added for the hemisphere
            // fix (see WindowSim.Step) recovers that excursion instead of freezing
            // it, so the descent completes regardless of how hard the pilot pulls.
            // handLeft = 0.2 is comfortably inside either version of the band.
            var sim = MakeSim(6.0);
            double beta = 30.0 * MathD.Deg2Rad;
            double phi = 90.0 * MathD.Deg2Rad;
            Vec3 direction = new Vec3(
                MathD.Cos(beta) * MathD.Sin(phi),
                MathD.Sin(beta),
                MathD.Cos(beta) * MathD.Cos(phi)).Normalized();
            var state = new WindowKiteState
            {
                Direction = direction,
                Heading = KiteStateView.TangentUp(direction),
                Speed = 0.0,
                Landed = false
            };

            // Let the kite settle onto its parked, floor-driven speed before
            // steering the nose down, matching how a pilot would find the kite
            // already parked and then act.
            Run(sim, state, 5.0);
            Assert.AreEqual(sim.Config.GlideRatio * sim.Config.PowerFloor * 6.0, state.Speed, 0.01,
                "the parked, floor-driven speed at the edge, measured for this exact start");

            int steps = (int)(30.0 / Dt);
            for (int i = 0; i < steps; i++)
            {
                sim.Step(state, Dt, handLeft: 0.2, handRight: 0.0, walkAxis: 0.0);
                if (state.Landed)
                    break;
            }

            Assert.IsTrue(state.Landed,
                "steering the nose down from a parked edge state must land the kite within a reasonable time");
            Assert.AreEqual(0.0, state.Speed, 1e-9);
        }

        [Test]
        public void HeadingTowardZenith_FliesUpTheMeridian()
        {
            var sim = MakeSim(6.0);
            var state = Flying(Vec3.Forward, Vec3.Up, 30.0);

            Run(sim, state, 3.0);

            // The weight term cannot turn a kite that is flying straight up, so it
            // climbs along the meridian and the azimuth stays at zero.
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
            // Starting at Vec3.Forward (elevation zero) lands this kite at step
            // 17 of the run, long before this steering pattern ever gets
            // exercised, so start well above the horizon instead.
            double beta = 45.0 * MathD.Deg2Rad;
            var state = Flying(new Vec3(0.0, MathD.Sin(beta), MathD.Cos(beta)), Vec3.Up, 25.0);

            Run(sim, state, 20.0, handLeft: -0.3, handRight: 0.5);

            Assert.IsFalse(state.Landed,
                "the kite must still be flying at the end for this to test 20 s of integration hygiene");
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
        public void TheWeightTermDoesNotTurnAKiteFlyingStraightUp()
        {
            var sim = MakeSim(6.0);
            var state = Flying(Vec3.Forward, Vec3.Up, 25.0);

            Assert.AreEqual(0.0, sim.TurnRate(state, 0.0, 0.0), 1e-12);
        }

        [Test]
        public void TheWeightTermDroopsAKiteFlyingAcrossTheWindow()
        {
            var sim = MakeSim(6.0);
            // Flying toward the pilot's right at the window centre.
            var state = Flying(Vec3.Forward, new Vec3(1, 0, 0), 25.0);

            double rate = sim.TurnRate(state, 0.0, 0.0);

            // Positive rate turns the heading from "right" on toward "down".
            Assert.Greater(rate, 0.0);
            Assert.AreEqual(sim.Config.WeightTurnCoefficient / 25.0, rate, 1e-9);
        }

        [Test]
        public void TheWeightTermMatchesTheClosedFormOfTheOracleLaw()
        {
            var sim = MakeSim(6.0);
            double beta = 40.0 * MathD.Deg2Rad;
            double psi = 120.0 * MathD.Deg2Rad;
            double speed = 18.0;

            Vec3 direction = new Vec3(0.0, MathD.Sin(beta), MathD.Cos(beta)).Normalized();
            Vec3 heading = KiteStateView.TangentUp(direction) * MathD.Cos(psi)
                           + KiteStateView.TangentRight(direction) * MathD.Sin(psi);
            var state = Flying(direction, heading, speed);

            double expected = sim.Config.WeightTurnCoefficient
                              * MathD.Cos(beta) * MathD.Sin(psi) / speed;
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
        public void EffectiveWind_NormalisesANonUnitWindDirection()
        {
            var sim = MakeSim(5.0);
            // A non-unit Direction must not silently scale the wind speed: this
            // still means "5 m/s toward +Z", not 10 m/s.
            sim.Wind = new WindField { Speed = 5.0, Direction = new Vec3(0.0, 0.0, 2.0) };

            Assert.AreEqual(5.0, sim.EffectiveWind(Vec3.Forward), 1e-12);
            Assert.AreEqual(new Vec3(0.0, 0.0, 2.0).X, sim.Wind.Direction.X, 1e-12);
            Assert.AreEqual(2.0, sim.Wind.Direction.Z, 1e-12,
                "EffectiveWind must not mutate the caller's WindField");
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
        public void TheWeightTurnGrowsAsTheKiteSlowsDown()
        {
            // The weight term goes as 1/v, so a slow kite droops far harder than a
            // fast one. Asserted on the rate itself, which is exact, rather than on
            // an integrated trajectory.
            var sim = MakeSim(5.0);
            var slow = Flying(Vec3.Forward, new Vec3(1, 0, 0), 6.0);
            var fast = Flying(Vec3.Forward, new Vec3(1, 0, 0), 30.0);

            double slowRate = sim.TurnRate(slow, 0.0, 0.0);
            double fastRate = sim.TurnRate(fast, 0.0, 0.0);

            Assert.AreEqual(sim.Config.WeightTurnCoefficient / 6.0, slowRate, 1e-9);
            Assert.AreEqual(5.0, slowRate / fastRate, 1e-9, "the term goes exactly as 1/v");
        }

        [Test]
        public void TheWeightTurnNeverOutweighsTheSteeringEvenAtTheSlowestTheKiteGets()
        {
            // The defect this bound exists to prevent. Steering authority goes as v
            // and the weight term as 1/v, so their ratio goes as 1/v^2 and is worst
            // where the kite is slowest -- the window edge and light wind, which is
            // exactly where the owner reported the kite unflyable and the edge
            // inescapable. With the coefficient hard-coded to g the ratio at the
            // MinAirspeed floor was 4.90: full deflection could not undo what weight
            // was doing, so no input got the kite out. Anything at or above 1.0 is a
            // trap the pilot cannot steer out of.
            var config = new WindowConfiguration();
            double floor = config.MinAirspeed;

            double weight = config.WeightTurnCoefficient / floor;
            double steering = config.SteeringGain * floor;

            Assert.Less(weight / steering, 0.75,
                "at the slowest the model lets the kite get, full steering must still "
                + "clearly beat the weight term, or the window edge is a one-way trap");
        }

        [Test]
        public void InLightWind_WalkingBackKeepsTheKiteFaster()
        {
            var passive = MakeSim(1.1);
            var active = MakeSim(1.1);
            var passiveKite = Flying(Vec3.Forward, Vec3.Up, 6.0);
            var activeKite = Flying(Vec3.Forward, Vec3.Up, 6.0);

            FreezeInPlace(passive);
            FreezeInPlace(active);

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
        public void ALandedKiteReportsZeroTension()
        {
            // View() used to compute tension from an airspeed floored at
            // Config.MinAirspeed, which exists only for the gravity turn term's
            // denominator (see WindowConfiguration), so a landed kite (Speed == 0)
            // reported nonzero tension. Plan task 13 drives icon colour and gamepad
            // rumble from Tension, so a grounded kite would buzz.
            var sim = MakeSim(6.0);
            var state = WindowKiteState.OnGround(0.0);

            Assert.AreEqual(0.0, sim.View(state).Tension, 1e-12);
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
            // What this test is: a 30000-step (10-minute, 500 Hz-equivalent at this
            // dt) smoke test for NaN and blow-up, plus (see below) two invariants
            // that are independent of the steering controller. It is not, on its
            // own, a drift detector for the integrator: a review injected six
            // defects into WindowSim -- dropping the Direction renormalisation,
            // dropping the Heading orthonormalisation, replacing the exact
            // great-circle advance with first-order Euler, two turn-rate biases,
            // and a speed-relaxation scaling -- and the landing/flying-time
            // assertions below caught none of them, because the pursuit controller
            // steering toward a moving aim point absorbs systematic errors: it
            // corrects for a biased turn rate or a slightly-wrong integrator the
            // same way it corrects for wind gusts. A controller that compensates for
            // a defect is exactly the thing that must not be trusted to detect it,
            // which is why the two invariants below were added: they read
            // Direction and Heading directly, not through the controller's
            // output.
            //
            // Measured: the previous steering pattern here, phase = sin(0.7t)*0.7
            // pushed and pulled symmetrically, drifts to the window edge at
            // t = 15.4 s and sits there motionless for the remaining 585 s of the
            // 600 s run -- 97.4% of the 30000 steps below 1 m/s. Parking at the
            // edge is legitimate kite behaviour (see the edge-parking and
            // controlled-landing tests above), so that is not a bug in the model;
            // it just means this particular test could no longer fail on anything
            // but a NaN, which defeats its purpose of exercising the integrator
            // over a long run to catch drift or blow-up.
            //
            // Measured further, and corrected from an earlier draft of this
            // comment that overstated it: it is not true that *no* fixed
            // sin(f*t)*amplitude pattern avoids getting captured at the edge. A
            // wider sweep of 2760 combinations (amplitude, frequency, starting
            // elevation) found 207 that never froze at all over 600 s, surviving by
            // repeatedly landing and relaunching hundreds of times. What is true:
            // none of the 2760 passes both of this test's assertions (mostly
            // flyingSteps, since landing-and-relaunching spends real time on the
            // ground), and beyond 60 s of run time no fixed pattern passes at all.
            // A crude position-blind "steer back toward psi = 0 past a theta limit"
            // guard on top of the oscillation does not help either (same capture at
            // the edge), because psi = 0 means "toward the local zenith," not
            // "toward the window centre," and those only coincide by accident.
            //
            // What does work, measured over the full 600 s run below: steering
            // that re-aims at a point orbiting a hub 30 degrees above the window
            // centre (radius 15 degrees, 8 s period), using the true bearing from
            // the kite's current position to that point rather than a fixed local
            // angle. Over the full run it never lands, its angular distance from
            // the window centre never exceeds 52.1 degrees (well short of the
            // 90-degree edge), and Speed never drops below 25.8 m/s -- i.e. it is
            // airborne and moving for the entire run, which is what this test uses
            // to stay flying long enough to be a useful smoke test.
            var sim = MakeSim(8.0);
            const double hubElevationDeg = 30.0;
            const double aimRadiusDeg = 15.0;
            const double aimPeriod = 8.0;
            const double steeringGain = 3.0;

            double hubBeta = hubElevationDeg * MathD.Deg2Rad;
            Vec3 hub = new Vec3(0.0, MathD.Sin(hubBeta), MathD.Cos(hubBeta)).Normalized();
            Vec3 hubTangentUp = KiteStateView.TangentUp(hub);
            Vec3 hubTangentRight = KiteStateView.TangentRight(hub);
            double aimRadiusRad = aimRadiusDeg * MathD.Deg2Rad;

            var state = Flying(new Vec3(0.0, MathD.Sin(hubBeta), MathD.Cos(hubBeta)), Vec3.Up, 25.0);

            int steps = (int)(600.0 / Dt);
            int landings = 0;
            int flyingSteps = 0;
            const double flyingThreshold = 5.0; // m/s; measured minimum over the run is 25.8 m/s

            // Drift invariants, independent of the pursuit controller: it reads
            // Direction and Heading, never writes them, so unlike the
            // landing/flying assertions above it cannot be steered around a defect
            // in Step. Tracked as running maxima over all 30000 steps.
            double maxDirectionLengthError = 0.0;
            double maxDirectionHeadingDot = 0.0;

            for (int i = 0; i < steps; i++)
            {
                // Aim point: a small circle around the hub that the kite chases,
                // giving it a maintained figure rather than sitting at one point.
                double angle = 2.0 * MathD.PI * (i * Dt) / aimPeriod;
                Vec3 aimDirection = (hub * MathD.Cos(aimRadiusRad)
                    + (hubTangentRight * MathD.Cos(angle) + hubTangentUp * MathD.Sin(angle)) * MathD.Sin(aimRadiusRad))
                    .Normalized();

                // Bearing from the kite's current position to the aim point, in
                // the kite's own tangent-plane frame -- this is what makes the
                // steering self-correcting regardless of where the kite has
                // drifted to, unlike a fixed local heading target.
                Vec3 tangentUp = KiteStateView.TangentUp(state.Direction);
                Vec3 tangentRight = KiteStateView.TangentRight(state.Direction);
                Vec3 aimLocal = aimDirection - state.Direction * Vec3.Dot(aimDirection, state.Direction);
                double aimPsi = MathD.Atan2(Vec3.Dot(aimLocal, tangentRight), Vec3.Dot(aimLocal, tangentUp));

                double psiError = aimPsi - sim.View(state).HeadingRad;
                while (psiError > MathD.PI) psiError -= 2.0 * MathD.PI;
                while (psiError < -MathD.PI) psiError += 2.0 * MathD.PI;

                double steer = MathD.Clamp(steeringGain * psiError, -1.0, 1.0);
                double handRight = steer > 0.0 ? steer : 0.0;
                double handLeft = steer < 0.0 ? -steer : 0.0;

                sim.Step(state, Dt, handLeft, handRight, 0.0);

                if (state.Landed)
                {
                    landings++;
                    sim.Launch(state);
                }
                if (state.Speed > flyingThreshold) flyingSteps++;

                double lengthError = MathD.Abs(state.Direction.Length() - 1.0);
                double dot = MathD.Abs(Vec3.Dot(state.Direction, state.Heading));
                if (lengthError > maxDirectionLengthError) maxDirectionLengthError = lengthError;
                if (dot > maxDirectionHeadingDot) maxDirectionHeadingDot = dot;
            }

            Assert.Less(landings, 10,
                "the kite must spend most of the run flying, not cycling through the ground");
            Assert.AreEqual(1.0, state.Direction.Length(), 1e-6);
            Assert.IsFalse(double.IsNaN(state.Speed));

            // Measured over this exact 30000-step run: maxDirectionLengthError =
            // 2.22e-16, maxDirectionHeadingDot = 1.67e-16 -- both at machine epsilon
            // for a double (about 2.2e-16), because Step renormalises Direction and
            // re-orthonormalises Heading against it every single step, so error
            // never accumulates across steps. The bound below (1e-9) is the same
            // tolerance already used elsewhere in this file for the same invariant
            // (e.g. DirectionAndHeadingStayUnitAndOrthogonal): about seven orders of
            // magnitude above what a healthy integrator actually produces, and
            // enough margin to be robust to platform floating-point differences,
            // while still being far too tight for a dropped renormalisation or a
            // dropped orthonormalisation (either would let the corresponding error
            // grow roughly linearly step over step across 30000 steps) to pass
            // unnoticed.
            Assert.Less(maxDirectionLengthError, 1e-9,
                "Direction must stay unit length for the entire run -- a dropped renormalisation would show up here even though the pursuit controller absorbs its effect on the trajectory");
            Assert.Less(maxDirectionHeadingDot, 1e-9,
                "Heading must stay perpendicular to Direction for the entire run -- a dropped orthonormalisation would show up here even though the pursuit controller absorbs its effect on the trajectory");

            // This model has no reverse flight (see the Speed clamp in Step), so
            // Speed can never be negative.
            Assert.That(state.Speed, Is.InRange(0.0, 200.0));

            // Measured 100% of steps above the 5 m/s threshold over this run;
            // assert a lower bar so the test is not pinned to that exact figure.
            Assert.Greater(flyingSteps, (int)(steps * 0.9),
                "most of a ten-minute run under a maintained steering pattern must be spent actually flying, not parked");
        }

        [Test]
        public void IsDeterministic()
        {
            var first = MakeSim(7.0);
            var second = MakeSim(7.0);
            // Starting at Vec3.Forward (elevation zero) lands this kite at
            // 4.48 s of the original 60 s run, so most of the steps were
            // no-ops and the comparison was 0.0 against 0.0. Starting at 45
            // degrees keeps it airborne, but this exact steering pattern
            // still lands it at about 24.1 s, so the run stops at 18 s, with
            // about six seconds of margin.
            double beta = 45.0 * MathD.Deg2Rad;
            var a = Flying(new Vec3(0.0, MathD.Sin(beta), MathD.Cos(beta)), Vec3.Up, 20.0);
            var b = Flying(new Vec3(0.0, MathD.Sin(beta), MathD.Cos(beta)), Vec3.Up, 20.0);

            int steps = (int)(18.0 / Dt);
            for (int i = 0; i < steps; i++)
            {
                double u = MathD.Sin(i * 0.013) * 0.7;
                double walk = MathD.Cos(i * 0.007);
                first.Step(a, Dt, -u, u, walk);
                second.Step(b, Dt, -u, u, walk);
            }

            Assert.IsFalse(a.Landed,
                "the kite must still be flying, or a landed sim trivially agrees with itself at rest");

            // Assert.AreEqual(double, double) with no tolerance passes for
            // NaN == NaN in .NET, so a sim that diverged to NaN would be
            // reported deterministic. Guard each component explicitly.
            Assert.IsFalse(double.IsNaN(a.Direction.X), "a.Direction.X is NaN");
            Assert.IsFalse(double.IsNaN(a.Direction.Y), "a.Direction.Y is NaN");
            Assert.IsFalse(double.IsNaN(a.Direction.Z), "a.Direction.Z is NaN");
            Assert.IsFalse(double.IsNaN(a.Speed), "a.Speed is NaN");
            Assert.IsFalse(double.IsNaN(b.Direction.X), "b.Direction.X is NaN");
            Assert.IsFalse(double.IsNaN(b.Direction.Y), "b.Direction.Y is NaN");
            Assert.IsFalse(double.IsNaN(b.Direction.Z), "b.Direction.Z is NaN");
            Assert.IsFalse(double.IsNaN(b.Speed), "b.Speed is NaN");

            Assert.AreEqual(a.Direction.X, b.Direction.X);
            Assert.AreEqual(a.Direction.Y, b.Direction.Y);
            Assert.AreEqual(a.Direction.Z, b.Direction.Z);
            Assert.AreEqual(a.Speed, b.Speed);
        }
    }
}

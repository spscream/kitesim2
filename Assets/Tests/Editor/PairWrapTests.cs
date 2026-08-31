using NUnit.Framework;
using Kitesim.Core;

namespace Kitesim.Core.Tests
{
    public class PairWrapTests
    {
        private static readonly Vec3 Downwind = Vec3.Forward;

        /// <summary>A kite at the given direction, flown by a pilot at the origin.</summary>
        private static KiteStateView Kite(Vec3 direction, bool landed = false)
        {
            return new KiteStateView(
                Vec3.Zero, direction.Normalized(), KiteStateView.TangentUp(direction.Normalized()),
                45.0, 10.0, 50.0, 0, landed);
        }

        /// <summary>
        /// The second kite sitting <paramref name="offsetRad"/> away from the first,
        /// at bearing <paramref name="phi"/> around it: this is one kite orbiting the
        /// other, which is what winds their lines together.
        /// </summary>
        private static KiteStateView Orbiting(Vec3 centre, double offsetRad, double phi)
        {
            Vec3 c = centre.Normalized();
            Vec3 up = KiteStateView.TangentUp(c);
            Vec3 right = KiteStateView.TangentRight(c);
            Vec3 bearing = up * MathD.Cos(phi) + right * MathD.Sin(phi);
            return Kite(c * MathD.Cos(offsetRad) + bearing * MathD.Sin(offsetRad));
        }

        private static PairWrapCounter Sweep(Vec3 centre, double fromPhi, double toPhi, int steps = 360)
        {
            var counter = new PairWrapCounter(Downwind);
            KiteStateView a = Kite(centre);
            for (int i = 0; i <= steps; i++)
            {
                double phi = fromPhi + (toPhi - fromPhi) * i / steps;
                counter.Update(a, Orbiting(centre, 0.20, phi));
            }
            return counter;
        }

        private static readonly Vec3 MidWindow = new Vec3(0.3, 0.6, 0.75);

        [Test]
        public void KitesHoldingStationDoNotWrap()
        {
            var counter = new PairWrapCounter(Downwind);
            for (int i = 0; i < 100; i++)
                counter.Update(Kite(MidWindow), Orbiting(MidWindow, 0.20, 0.4));

            Assert.AreEqual(0, counter.Wraps);
            Assert.AreEqual(0.0, counter.AccumulatedAngleRad, 1e-9);
        }

        /// <summary>
        /// Kites flying the window side by side, both sweeping across it together,
        /// never orbit each other -- this is most of what a team does, and it must
        /// not register as winding the lines up.
        ///
        /// The angle does not stay at zero along the way: a pair holding formation
        /// from one edge of the window to the other does change its bearing about the
        /// window axis, by well under half a turn here. That is a property of where
        /// the pair is, not of how it got there, and it cannot reach a wrap on its
        /// own -- see TheAngleIsBoundedByWhereThePairIsUnlessItTrulyWinds.
        /// </summary>
        [Test]
        public void FlyingTheWindowSideBySideDoesNotWrap()
        {
            var counter = new PairWrapCounter(Downwind);
            for (int i = 0; i <= 200; i++)
            {
                double azimuth = -1.2 + 2.4 * i / 200.0;
                Vec3 centre = new Vec3(MathD.Sin(azimuth), 0.55, MathD.Cos(azimuth) * 0.83);
                counter.Update(Kite(centre), Orbiting(centre, 0.15, 0.0));
            }

            Assert.AreEqual(0, counter.Wraps);
            Assert.Less(MathD.Abs(counter.AccumulatedAngleRad), MathD.PI);
        }

        /// <summary>
        /// The guarantee that makes the count trustworthy over a whole routine: with
        /// the turning measured about a fixed axis, the total is the pair's current
        /// bearing plus whole turns, so a formation that flies away across the window
        /// and comes back reads exactly zero again. Nothing creeps.
        /// </summary>
        [Test]
        public void TheAngleIsBoundedByWhereThePairIsUnlessItTrulyWinds()
        {
            var counter = new PairWrapCounter(Downwind);
            for (int i = 0; i <= 200; i++)
            {
                double azimuth = 1.2 * MathD.Sin(2.0 * MathD.PI * i / 200.0);
                Vec3 centre = new Vec3(MathD.Sin(azimuth), 0.55, MathD.Cos(azimuth) * 0.83);
                counter.Update(Kite(centre), Orbiting(centre, 0.15, 0.0));
            }

            Assert.AreEqual(0.0, counter.AccumulatedAngleRad, 1e-6);
        }

        /// <summary>
        /// One orbit and a degree more. The extra degree is not padding of a shaky
        /// number: a whole turn lands exactly on the boundary where wraps truncate,
        /// and a kite that has come round once and kept going is the honest case.
        ///
        /// The angle comes out near one turn but not exactly at the swept phase,
        /// because bearing about the fixed window axis does not advance in step with
        /// the phase of the orbit -- only over a whole turn do the two agree. That
        /// difference is bounded and never reaches a wrap, which is the property
        /// TheAngleIsBoundedByWhereThePairIsUnlessItTrulyWinds pins down.
        /// </summary>
        [Test]
        public void OneKiteOrbitingTheOtherOnceIsOneWrap()
        {
            PairWrapCounter counter = Sweep(MidWindow, 0.0, 2.0 * MathD.PI + 0.02);

            Assert.AreEqual(2.0 * MathD.PI, MathD.Abs(counter.AccumulatedAngleRad), 0.05);
            Assert.AreEqual(1, System.Math.Abs(counter.Wraps));
        }

        [Test]
        public void ThreeOrbitsAreThreeWraps()
        {
            PairWrapCounter counter = Sweep(MidWindow, 0.0, 6.0 * MathD.PI + 0.02, 1080);

            Assert.AreEqual(3, System.Math.Abs(counter.Wraps));
        }

        [Test]
        public void OrbitingTheOtherWayUnwinds()
        {
            PairWrapCounter forward = Sweep(MidWindow, 0.0, 2.0 * MathD.PI + 0.02);
            PairWrapCounter backward = Sweep(MidWindow, 0.0, -2.0 * MathD.PI - 0.02);

            Assert.AreEqual(-forward.Wraps, backward.Wraps);
            Assert.AreEqual(-forward.AccumulatedAngleRad, backward.AccumulatedAngleRad, 0.01);
            Assert.AreEqual(1, System.Math.Abs(forward.Wraps));
        }

        /// <summary>
        /// The crossing that costs nothing: one kite passes the other and comes back
        /// the way it went. The lines are clear afterwards, and the count says so.
        /// </summary>
        [Test]
        public void PassingAndReturningTheSameWayLeavesTheLinesClear()
        {
            var counter = new PairWrapCounter(Downwind);
            KiteStateView a = Kite(MidWindow);

            for (int i = 0; i <= 180; i++)
                counter.Update(a, Orbiting(MidWindow, 0.20, MathD.PI * i / 180.0));
            for (int i = 180; i >= 0; i--)
                counter.Update(a, Orbiting(MidWindow, 0.20, MathD.PI * i / 180.0));

            Assert.AreEqual(0, counter.Wraps);
            Assert.AreEqual(0.0, counter.AccumulatedAngleRad, 1e-6);
        }

        /// <summary>
        /// The crossing that costs a wrap: over one way, back the other. Same start
        /// and finish as the test above, and a different state of the lines -- which
        /// is exactly why this is counted as winding rather than as crossings.
        /// </summary>
        [Test]
        public void PassingOverAndReturningTheOtherWayCostsAWrap()
        {
            PairWrapCounter counter = Sweep(MidWindow, 0.0, 2.0 * MathD.PI + 0.02);

            Assert.AreEqual(1, System.Math.Abs(counter.Wraps));
        }

        [Test]
        public void TheAnswerDoesNotDependOnWhichKiteIsNamedFirst()
        {
            var forward = new PairWrapCounter(Downwind);
            var swapped = new PairWrapCounter(Downwind);

            for (int i = 0; i <= 720; i++)
            {
                double phi = 4.0 * MathD.PI * i / 720.0;
                KiteStateView a = Kite(MidWindow);
                KiteStateView b = Orbiting(MidWindow, 0.20, phi);
                forward.Update(a, b);
                swapped.Update(b, a);
            }

            Assert.AreEqual(forward.Wraps, swapped.Wraps);
            Assert.AreEqual(forward.AccumulatedAngleRad, swapped.AccumulatedAngleRad, 1e-9);
        }

        /// <summary>
        /// Wraps truncate toward zero, as the kite's own line wraps already do: half
        /// a turn is not a wrap yet, in either direction.
        /// </summary>
        [Test]
        public void HalfATurnIsNotYetAWrap()
        {
            Assert.AreEqual(0, Sweep(MidWindow, 0.0, MathD.PI).Wraps);
            Assert.AreEqual(0, Sweep(MidWindow, 0.0, -MathD.PI).Wraps);
            Assert.AreEqual(1, System.Math.Abs(Sweep(MidWindow, 0.0, 3.0 * MathD.PI, 540).Wraps),
                "a turn and a half is one wrap, not two");
        }

        /// <summary>
        /// A kite on the ground is not orbiting anything; its lines lie on the field
        /// while its partner keeps flying. Counting that as winding would report a
        /// tangle that nobody has to unwind.
        /// </summary>
        [Test]
        public void AKiteOnTheGroundDoesNotWindAnything()
        {
            var counter = new PairWrapCounter(Downwind);
            for (int i = 0; i <= 360; i++)
            {
                double phi = 2.0 * MathD.PI * i / 360.0;
                counter.Update(Kite(MidWindow, landed: true), Orbiting(MidWindow, 0.20, phi));
            }

            Assert.AreEqual(0, counter.Wraps);
            Assert.AreEqual(0.0, counter.AccumulatedAngleRad, 1e-9);
        }

        /// <summary>
        /// Landing breaks the thread of the measurement, so the count resumes from
        /// where the pair actually is rather than jumping by whatever happened while
        /// one of them was down.
        /// </summary>
        [Test]
        public void FlyingResumesAfterALandingWithoutAJump()
        {
            var counter = new PairWrapCounter(Downwind);
            counter.Update(Kite(MidWindow), Orbiting(MidWindow, 0.20, 0.0));
            counter.Update(Kite(MidWindow, landed: true), Orbiting(MidWindow, 0.20, 0.0));
            counter.Update(Kite(MidWindow), Orbiting(MidWindow, 0.20, MathD.PI));

            Assert.AreEqual(0.0, counter.AccumulatedAngleRad, 1e-9);
        }

        [Test]
        public void ResetPutsThePairBackToClearLines()
        {
            PairWrapCounter counter = Sweep(MidWindow, 0.0, 2.0 * MathD.PI);
            counter.Reset();

            Assert.AreEqual(0, counter.Wraps);
            Assert.AreEqual(0.0, counter.AccumulatedAngleRad, 1e-9);
        }

        [Test]
        public void TwoKitesInTheSamePlaceAreNotAWrap()
        {
            var counter = new PairWrapCounter(Downwind);
            for (int i = 0; i < 10; i++)
                counter.Update(Kite(MidWindow), Kite(MidWindow));

            Assert.AreEqual(0, counter.Wraps);
        }
    }

    public class TeamWrapTests
    {
        private static KiteStateView Kite(Vec3 direction)
        {
            Vec3 d = direction.Normalized();
            return new KiteStateView(Vec3.Zero, d, KiteStateView.TangentUp(d), 45.0, 10.0, 50.0, 0, false);
        }

        private static KiteStateView Orbiting(Vec3 centre, double offsetRad, double phi)
        {
            Vec3 c = centre.Normalized();
            Vec3 bearing = KiteStateView.TangentUp(c) * MathD.Cos(phi)
                + KiteStateView.TangentRight(c) * MathD.Sin(phi);
            return Kite(c * MathD.Cos(offsetRad) + bearing * MathD.Sin(offsetRad));
        }

        private static readonly Vec3 Centre = new Vec3(0.0, 0.6, 0.8);

        /// <summary>
        /// Kite 1 flies a circle around kite 0, so those two wind up. Kite 2 holds
        /// station outside that circle, and winding needs one kite to go *around* the
        /// other: 1 passes 2 twice per lap and comes back the same way both times, so
        /// that pair ends the lap clear. This is the distinction the whole measure
        /// exists to make.
        /// </summary>
        [Test]
        public void EveryPairIsCountedSeparately()
        {
            var team = new TeamWrapCounter(3, Vec3.Forward);

            for (int i = 0; i <= 360; i++)
            {
                double phi = (2.0 * MathD.PI + 0.02) * i / 360.0;
                team.Update(new[]
                {
                    Kite(Centre),
                    Orbiting(Centre, 0.20, phi),   // circles kite 0
                    Orbiting(Centre, 0.35, 0.0)    // holds station outside that circle
                });
            }

            Assert.AreEqual(1, System.Math.Abs(team.Wraps(0, 1)), "0 and 1 wound once");
            Assert.AreEqual(0, team.Wraps(0, 2), "0 and 2 never moved apart");
            Assert.AreEqual(0, team.Wraps(1, 2), "1 passed 2 and came back, twice");
        }

        [Test]
        public void PairsCanBeAskedInEitherOrder()
        {
            var team = new TeamWrapCounter(4, Vec3.Forward);
            for (int i = 0; i <= 180; i++)
            {
                double phi = 2.0 * MathD.PI * i / 180.0;
                team.Update(new[]
                {
                    Kite(Centre), Orbiting(Centre, 0.2, phi),
                    Orbiting(Centre, 0.3, 0.0), Orbiting(Centre, 0.4, 1.0)
                });
            }

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    if (i != j)
                        Assert.AreEqual(team.Wraps(i, j), team.Wraps(j, i), $"pair {i},{j}");
                }
            }
        }

        [Test]
        public void APairOfItselfIsNeverWound()
        {
            var team = new TeamWrapCounter(2, Vec3.Forward);
            Assert.AreEqual(0, team.Wraps(1, 1));
        }

        [Test]
        public void TheWorstPairIsWhatTheTeamWatches()
        {
            var team = new TeamWrapCounter(3, Vec3.Forward);
            for (int i = 0; i <= 720; i++)
            {
                double phi = 4.0 * MathD.PI * i / 720.0;
                team.Update(new[] { Kite(Centre), Orbiting(Centre, 0.2, phi), Orbiting(Centre, 0.3, 0.0) });
            }

            Assert.AreEqual(2, team.WorstWraps);
        }

        [Test]
        public void FewerViewsThanPilotsIsIgnoredRatherThanThrown()
        {
            var team = new TeamWrapCounter(3, Vec3.Forward);
            team.Update(new[] { Kite(Centre) });

            Assert.AreEqual(0, team.WorstWraps);
        }
    }
}

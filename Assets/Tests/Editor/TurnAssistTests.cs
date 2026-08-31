using NUnit.Framework;
using Kitesim.Core;

namespace Kitesim.Core.Tests
{
    public class TurnAssistTests
    {
        private static WindowConfiguration On() => new WindowConfiguration();

        private static WindowConfiguration Off()
        {
            var config = new WindowConfiguration();
            config.TurnAssist = false;
            return config;
        }

        private const double Turning = 2.0;   // rad/s, a real turn
        private static double Deg(double degrees) => degrees * MathD.Deg2Rad;

        /// <summary>
        /// The promise the whole thing rests on: it cannot fly the kite. With no turn
        /// under way there is nothing to tidy, and nothing is added however far the
        /// heading is from a node.
        /// </summary>
        [Test]
        public void AKiteFlyingStraightGetsNoHelpAtAll()
        {
            foreach (double heading in new[] { 0.0, Deg(3), Deg(43), Deg(87), Deg(-87) })
                Assert.AreEqual(0.0, TurnAssist.Added(heading, 0.0, On()), 0.0, $"at {heading:F2}");
        }

        /// <summary>
        /// The other promise: it may never add more than the difference between a
        /// stick and a hand, which is what it exists to make up for. Swept across
        /// every heading and a range of turn rates, including absurd ones.
        /// </summary>
        [Test]
        public void ItNeverAddsMoreThanTheControllersOwnError()
        {
            WindowConfiguration config = On();

            for (int i = -360; i <= 360; i++)
            {
                foreach (double rate in new[] { 0.5, 2.0, 20.0, -0.5, -2.0, -20.0 })
                {
                    double added = TurnAssist.Added(Deg(i), rate, config);
                    Assert.LessOrEqual(MathD.Abs(added), config.TurnAssistMaxInput + 1e-12,
                        $"heading {i} deg at {rate} rad/s");
                }
            }
        }

        [Test]
        public void ShortOfARightAngleItPushesOnTowardIt()
        {
            double added = TurnAssist.Added(Deg(87), Turning, On());

            Assert.Greater(added, 0.0, "the node at 90 is to the right of 87");
        }

        [Test]
        public void PastARightAngleItEasesBack()
        {
            double added = TurnAssist.Added(Deg(93), Turning, On());

            Assert.Less(added, 0.0, "the node at 90 is to the left of 93");
        }

        /// <summary>
        /// A pilot flying to somewhere that is not on the grid is left alone. Twenty
        /// degrees off a node is a choice, not a slip of the thumb.
        /// </summary>
        [Test]
        public void AHeadingFarFromAnyNodeIsLeftAlone()
        {
            Assert.AreEqual(0.0, TurnAssist.Added(Deg(65), Turning, On()), 0.0);
            Assert.AreEqual(0.0, TurnAssist.Added(Deg(22), Turning, On()), 0.0);
        }

        [Test]
        public void TheDiagonalsAreNodesToo()
        {
            Assert.Greater(TurnAssist.Added(Deg(43), Turning, On()), 0.0, "45 is a node");
            Assert.Less(TurnAssist.Added(Deg(137), Turning, On()), 0.0, "135 is a node");
        }

        [Test]
        public void HelpIsMirroredLeftAndRight()
        {
            Assert.AreEqual(-TurnAssist.Added(Deg(87), Turning, On()),
                TurnAssist.Added(Deg(-87), Turning, On()), 1e-12);
        }

        /// <summary>
        /// Straight down is a node like any other, and it is where the heading angle
        /// wraps. A kite three degrees short of straight down must be helped the same
        /// as one three degrees short of straight up.
        /// </summary>
        [Test]
        public void TheGridSurvivesTheWrapAtHalfATurn()
        {
            Assert.Greater(TurnAssist.Added(Deg(177), Turning, On()), 0.0);
            Assert.Less(TurnAssist.Added(Deg(-177), Turning, On()), 0.0);
        }

        [Test]
        public void HelpFadesInWithTheTurnRatherThanSwitchingOn()
        {
            WindowConfiguration config = On();
            double slow = TurnAssist.Added(Deg(87), config.TurnAssistTurnRate * 0.25, config);
            double fast = TurnAssist.Added(Deg(87), config.TurnAssistTurnRate, config);

            Assert.Greater(fast, slow);
            Assert.AreEqual(0.25, slow / fast, 1e-9, "proportional to how much turn there is");
        }

        [Test]
        public void SwitchedOffItDoesNothing()
        {
            Assert.AreEqual(0.0, TurnAssist.Added(Deg(87), Turning, Off()), 0.0);
            Assert.AreEqual(0.0, TurnAssist.Added(Deg(87), Turning, null), 0.0);
        }

        [Test]
        public void TheNearestNodeIsTheNearestOne()
        {
            Assert.AreEqual(Deg(3), TurnAssist.ErrorToNearestNode(Deg(87), Deg(45)), 1e-12);
            Assert.AreEqual(Deg(-2), TurnAssist.ErrorToNearestNode(Deg(47), Deg(45)), 1e-12);
            Assert.AreEqual(Deg(0), TurnAssist.ErrorToNearestNode(Deg(90), Deg(45)), 1e-12);
            Assert.AreEqual(Deg(1), TurnAssist.ErrorToNearestNode(Deg(-46), Deg(45)), 1e-12);
        }
    }

    /// <summary>
    /// The assist through the model rather than on its own: what it does to a kite
    /// actually flying a turn.
    /// </summary>
    public class TurnAssistInFlightTests
    {
        private static WindowKiteState Flying(double headingDeg)
        {
            Vec3 direction = new Vec3(0.0, 0.6, 0.8).Normalized();
            Vec3 up = KiteStateView.TangentUp(direction);
            Vec3 right = KiteStateView.TangentRight(direction);
            double heading = headingDeg * MathD.Deg2Rad;

            return new WindowKiteState
            {
                Direction = direction,
                Heading = up * MathD.Cos(heading) + right * MathD.Sin(heading),
                Speed = 12.0,
                Landed = false
            };
        }

        private static WindowSim Sim(bool assist)
        {
            var config = new WindowConfiguration { TurnAssist = assist };
            return new WindowSim(config)
            {
                Wind = new WindField { Speed = 6.0, Direction = Vec3.Forward },
                PilotPosition = Vec3.Zero
            };
        }

        /// <summary>
        /// The case the pilot described: a hard turn released a few degrees short of
        /// the right angle. With the help the exit lands closer to ninety; without it
        /// the kite keeps whatever the thumb gave it.
        /// </summary>
        [Test]
        public void ATurnReleasedShortOfARightAngleFinishesCloserToIt()
        {
            double helped = ExitHeading(assist: true);
            double alone = ExitHeading(assist: false);

            Assert.Less(MathD.Abs(helped - 90.0), MathD.Abs(alone - 90.0),
                $"helped {helped:F2} deg, unhelped {alone:F2} deg");
        }

        /// <summary>
        /// And it stays a nudge: five degrees of authority means five degrees, so a
        /// pilot who lets go fifteen degrees out is still fifteen degrees out.
        /// </summary>
        [Test]
        public void ItCannotRescueATurnThatIsFarOut()
        {
            double helped = ExitHeading(assist: true, from: 70.0);
            double alone = ExitHeading(assist: false, from: 70.0);

            Assert.AreEqual(alone, helped, 0.001, "nothing to see this far from a node");
        }

        [Test]
        public void WithTheHelpOffTheModelFliesExactlyAsItDidBefore()
        {
            var sim = Sim(assist: false);
            WindowKiteState state = Flying(87.0);
            double rate = sim.TurnRate(state, 0.0, 0.0);

            // No hands, no help, only the weight term: the number the model gave
            // before any of this existed.
            Vec3 turnNormal = Vec3.Cross(state.Heading, state.Direction);
            double expected = Vec3.Dot(new Vec3(0.0, -sim.Config.WeightTurnCoefficient, 0.0), turnNormal)
                / MathD.Max(state.Speed, sim.Config.MinAirspeed);

            Assert.AreEqual(expected, rate, 1e-12);
        }

        /// <summary>
        /// Fly a hard right turn, let go at the given heading, and report where the
        /// kite has settled a moment later.
        /// </summary>
        private static double ExitHeading(bool assist, double from = 87.0)
        {
            WindowSim sim = Sim(assist);
            WindowKiteState state = Flying(from);

            const double dt = 0.02;
            for (int i = 0; i < 25; i++)
                sim.Step(state, dt, 0.0, 0.0, 0.0);

            Vec3 up = KiteStateView.TangentUp(state.Direction);
            Vec3 right = KiteStateView.TangentRight(state.Direction);
            return MathD.Atan2(Vec3.Dot(state.Heading, right), Vec3.Dot(state.Heading, up)) * MathD.Rad2Deg;
        }
    }
}

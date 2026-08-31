using NUnit.Framework;
using Kitesim.Core;

namespace Kitesim.Core.Tests
{
    public class TurnAssistTests
    {
        private static double Deg(double degrees) => degrees * MathD.Deg2Rad;

        private static WindowConfiguration On() => new WindowConfiguration();

        private static WindowConfiguration Off() => new WindowConfiguration { TurnAssist = false };

        /// <summary>A kite that has just been turning hard and is not being steered now.</summary>
        private static WindowKiteState JustReleased(double recentTurnRate = 4.0)
        {
            return new WindowKiteState { RecentTurnRate = recentTurnRate, Landed = false };
        }

        /// <summary>
        /// The promise that protects every figure passing through a node on its way
        /// somewhere else: while the pilot is still turning, their input is returned
        /// untouched.
        /// </summary>
        [Test]
        public void APilotStillTurningIsNotHelpedAtAll()
        {
            WindowKiteState state = JustReleased();

            Assert.AreEqual(1.0, TurnAssist.Steering(state, 1.0, Deg(87), On()), 0.0);
            Assert.AreEqual(-0.8, TurnAssist.Steering(state, -0.8, Deg(87), On()), 0.0);
            Assert.IsTrue(double.IsNaN(state.AssistTarget), "and nothing is being finished");
        }

        /// <summary>
        /// The second promise: it cannot begin a turn. A kite that was not turning
        /// gets no help however close it is drifting to a node.
        /// </summary>
        [Test]
        public void AKiteThatWasNotTurningIsLeftAlone()
        {
            var state = new WindowKiteState { RecentTurnRate = 0.2, Landed = false };

            Assert.AreEqual(0.0, TurnAssist.Steering(state, 0.0, Deg(87), On()), 0.0);
            Assert.IsTrue(double.IsNaN(state.AssistTarget));
        }

        [Test]
        public void LettingGoShortOfARightAngleStartsFinishingOnIt()
        {
            WindowKiteState state = JustReleased();

            double steering = TurnAssist.Steering(state, 0.0, Deg(87), On());

            Assert.Greater(steering, 0.0, "still turning right, toward 90");
            Assert.AreEqual(Deg(90), state.AssistTarget, 1e-9);
        }

        [Test]
        public void LettingGoPastARightAngleComesBackToIt()
        {
            WindowKiteState state = JustReleased();

            double steering = TurnAssist.Steering(state, 0.0, Deg(93), On());

            Assert.Less(steering, 0.0, "turning back left, toward 90");
            Assert.AreEqual(Deg(90), state.AssistTarget, 1e-9);
        }

        /// <summary>
        /// A pilot who lets go a long way from the grid meant to be there. Twenty
        /// degrees out is a choice, not a slip.
        /// </summary>
        [Test]
        public void LettingGoFarFromAnyNodeIsLeftAlone()
        {
            WindowKiteState state = JustReleased();

            Assert.AreEqual(0.0, TurnAssist.Steering(state, 0.0, Deg(65), On()), 0.0);
            Assert.IsTrue(double.IsNaN(state.AssistTarget));
        }

        /// <summary>
        /// The third promise: it finishes turns, it does not fly them. The help never
        /// steers harder than the input the pilot has just come below.
        /// </summary>
        [Test]
        public void ItNeverSteersHarderThanTheInputThePilotLetGoOf()
        {
            WindowConfiguration config = On();

            for (int i = -360; i <= 360; i++)
            {
                WindowKiteState state = JustReleased(20.0);
                double steering = TurnAssist.Steering(state, 0.0, Deg(i), config);

                Assert.LessOrEqual(MathD.Abs(steering), config.TurnAssistFinishInput + 1e-12,
                    $"at {i} degrees");
            }
        }

        [Test]
        public void TheDiagonalsAreNodesToo()
        {
            Assert.Greater(TurnAssist.Steering(JustReleased(), 0.0, Deg(43), On()), 0.0, "45 is a node");
            Assert.Less(TurnAssist.Steering(JustReleased(), 0.0, Deg(137), On()), 0.0, "135 is a node");
        }

        [Test]
        public void HelpIsMirroredLeftAndRight()
        {
            Assert.AreEqual(
                -TurnAssist.Steering(JustReleased(), 0.0, Deg(87), On()),
                TurnAssist.Steering(JustReleased(), 0.0, Deg(-87), On()), 1e-12);
        }

        /// <summary>
        /// Straight down is a node like any other, and it is where the heading angle
        /// wraps.
        /// </summary>
        [Test]
        public void TheGridSurvivesTheWrapAtHalfATurn()
        {
            Assert.Greater(TurnAssist.Steering(JustReleased(), 0.0, Deg(177), On()), 0.0);
            Assert.Less(TurnAssist.Steering(JustReleased(), 0.0, Deg(-177), On()), 0.0);
        }

        [Test]
        public void ArrivingAtTheNodeEndsTheHelp()
        {
            WindowKiteState state = JustReleased();
            TurnAssist.Steering(state, 0.0, Deg(87), On());
            Assert.IsFalse(double.IsNaN(state.AssistTarget), "finishing");

            double steering = TurnAssist.Steering(state, 0.0, Deg(90), On());

            Assert.AreEqual(0.0, steering, 0.0);
            Assert.IsTrue(double.IsNaN(state.AssistTarget), "let go on arrival");
        }

        [Test]
        public void TakingHoldAgainCancelsTheHelpImmediately()
        {
            WindowKiteState state = JustReleased();
            TurnAssist.Steering(state, 0.0, Deg(87), On());

            double steering = TurnAssist.Steering(state, -1.0, Deg(88), On());

            Assert.AreEqual(-1.0, steering, 0.0, "the pilot's input, untouched");
            Assert.IsTrue(double.IsNaN(state.AssistTarget));
        }

        [Test]
        public void SwitchedOffItDoesNothing()
        {
            WindowKiteState state = JustReleased();

            Assert.AreEqual(0.0, TurnAssist.Steering(state, 0.0, Deg(87), Off()), 0.0);
            Assert.IsTrue(double.IsNaN(state.AssistTarget));
            Assert.AreEqual(0.0, TurnAssist.Steering(state, 0.0, Deg(87), null), 0.0);
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
    /// The help through the model rather than on its own: what a kite actually does
    /// when a pilot flies a turn and lets go.
    /// </summary>
    public class TurnAssistInFlightTests
    {
        private const double Dt = 0.02;
        private const double Travel = 0.7;

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
            return new WindowSim(new WindowConfiguration { TurnAssist = assist })
            {
                Wind = new WindField { Speed = 6.0, Direction = Vec3.Forward },
                PilotPosition = Vec3.Zero
            };
        }

        private static double Heading(WindowKiteState state)
        {
            Vec3 up = KiteStateView.TangentUp(state.Direction);
            Vec3 right = KiteStateView.TangentRight(state.Direction);
            return MathD.Atan2(Vec3.Dot(state.Heading, right), Vec3.Dot(state.Heading, up)) * MathD.Rad2Deg;
        }

        /// <summary>
        /// The case the flier described. A hard turn, let go a few degrees short of
        /// the right angle: the kite arrives at ninety instead of wherever the thumb
        /// happened to stop it.
        /// </summary>
        /// <summary>
        /// The case the flier described. What is measured is where the turn *ends*:
        /// with the help it ends on the node, without it wherever the thumb happened
        /// to let go. Both kites then droop away under the weight term, which is why
        /// the end of the turn is the thing to measure and not the heading a moment
        /// later.
        /// </summary>
        [Test]
        public void ATurnLetGoOfNearARightAngleEndsOnIt()
        {
            double released = ReleaseHeading();
            Assert.Greater(MathD.Abs(released - 90.0), 1.0,
                $"the pilot let go at {released:F2} deg, so there is something to finish");

            double helped = HeadingWhereTheTurnEnds(assist: true);
            double alone = HeadingWhereTheTurnEnds(assist: false);

            Assert.AreEqual(90.0, helped, 1.0, $"helped turn ended at {helped:F2} deg");
            Assert.AreEqual(released, alone, 1e-9, "unhelped, the turn ends where the hands do");
        }

        /// <summary>
        /// The kite does not then sit at ninety, and should not: with the hands
        /// centred the weight term keeps bending the heading down, which is the model
        /// working. The help ends the turn on the node; what the kite does afterwards
        /// is flying.
        /// </summary>
        /// <summary>
        /// Having finished the turn, the help lets go: it does not hold the kite on
        /// the node. What the heading does next is the model's business -- the weight
        /// term bends it down, the turning of the tangent frame carries it as the
        /// kite crosses the sky -- and the point is only that nothing pins it.
        /// </summary>
        [Test]
        public void AfterArrivingTheHelpLetsGoRatherThanHolding()
        {
            WindowSim sim = Sim(assist: true);
            WindowKiteState state = Flying(20.0);

            for (int i = 0; i < 400 && Heading(state) < 87.0; i++)
                sim.Step(state, Dt, -Travel, Travel, 0.0);

            sim.Step(state, Dt, 0.0, 0.0, 0.0);
            for (int i = 0; i < 100 && !double.IsNaN(state.AssistTarget); i++)
                sim.Step(state, Dt, 0.0, 0.0, 0.0);

            double arrived = Heading(state);
            Assert.AreEqual(90.0, arrived, 1.0);

            for (int i = 0; i < 10; i++)
                sim.Step(state, Dt, 0.0, 0.0, 0.0);

            Assert.IsTrue(double.IsNaN(state.AssistTarget), "the help stays out of it");
            Assert.AreNotEqual(arrived, Heading(state), "and the kite goes on flying");
        }

        /// <summary>
        /// The figure this must not break: a half turn passes straight through the
        /// right angle, and a pilot holding the stick through it is not stopped
        /// there.
        /// </summary>
        [Test]
        public void ATurnHeldThroughARightAngleIsNotStoppedAtIt()
        {
            WindowSim sim = Sim(assist: true);
            WindowKiteState state = Flying(20.0);

            for (int i = 0; i < 400 && Heading(state) < 175.0; i++)
                sim.Step(state, Dt, -Travel, Travel, 0.0);

            Assert.Greater(Heading(state), 150.0, "the turn ran past ninety, as asked");
        }

        /// <summary>
        /// It closes the gap fast enough to feel like the end of the turn rather than
        /// a drift: at flying speed the last few degrees take a fraction of a second.
        /// </summary>
        [Test]
        public void TheFinishTakesLessThanASecond()
        {
            WindowSim sim = Sim(assist: true);
            WindowKiteState state = Flying(20.0);

            for (int i = 0; i < 400 && Heading(state) < 84.0; i++)
                sim.Step(state, Dt, -Travel, Travel, 0.0);

            int steps = 0;
            while (steps < 100 && MathD.Abs(Heading(state) - 90.0) > 1.0)
            {
                sim.Step(state, Dt, 0.0, 0.0, 0.0);
                steps++;
            }

            Assert.Less(steps * Dt, 0.5, $"took {steps * Dt:F2} s to reach the node");
        }

        [Test]
        public void ItCannotRescueATurnThatIsFarOut()
        {
            double helped = ExitHeading(assist: true, releaseAt: 70.0);
            double alone = ExitHeading(assist: false, releaseAt: 70.0);

            Assert.AreEqual(alone, helped, 0.001, "nothing to see this far from a node");
        }

        /// <summary>
        /// Why the help needs a memory of the turn: this model has no rotational
        /// inertia, so the turn rate is nothing the instant the hands centre, and
        /// there would be no way to tell a finished turn from a kite that was never
        /// turning.
        /// </summary>
        [Test]
        public void TheTurnIsRememberedForAMomentAfterTheHandsCentre()
        {
            WindowSim sim = Sim(assist: true);
            WindowKiteState state = Flying(87.0);

            sim.Step(state, Dt, -Travel, Travel, 0.0);
            double whileTurning = state.RecentTurnRate;
            Assert.Greater(whileTurning, 1.0, "a hard turn is a real turn rate");

            sim.Step(state, Dt, 0.0, 0.0, 0.0);
            Assert.Greater(state.RecentTurnRate, whileTurning * 0.9, "still there the next step");

            for (int i = 0; i < 100; i++)
                sim.Step(state, Dt, 0.0, 0.0, 0.0);

            // Not zero: with the hands centred the kite still droops under the weight
            // term, and that is a real turn. What has gone is the memory of the hard
            // one, which is what the help was riding on.
            Assert.Less(state.RecentTurnRate, whileTurning * 0.2, "the hard turn is forgotten");
        }

        [Test]
        public void WithTheHelpOffTheModelFliesExactlyAsItDidBefore()
        {
            var sim = Sim(assist: false);
            WindowKiteState state = Flying(87.0);
            state.RecentTurnRate = 4.0;

            double rate = sim.TurnRate(state, 0.0, 0.0);

            Vec3 turnNormal = Vec3.Cross(state.Heading, state.Direction);
            double expected = Vec3.Dot(new Vec3(0.0, -sim.Config.WeightTurnCoefficient, 0.0), turnNormal)
                / MathD.Max(state.Speed, sim.Config.MinAirspeed);

            Assert.AreEqual(expected, rate, 1e-12);
        }

        /// <summary>Where the hands come off, which is where an unhelped turn ends.</summary>
        private static double ReleaseHeading(double releaseAt = 87.0)
        {
            WindowSim sim = Sim(assist: false);
            WindowKiteState state = Flying(20.0);

            for (int i = 0; i < 400 && Heading(state) < releaseAt; i++)
                sim.Step(state, Dt, -Travel, Travel, 0.0);

            return Heading(state);
        }

        /// <summary>
        /// Fly the turn, let go, and report the heading at the moment the turning
        /// stops: for an unhelped kite that is the instant the hands centre, and for
        /// a helped one the instant it has finished on the node.
        /// </summary>
        private static double HeadingWhereTheTurnEnds(bool assist, double releaseAt = 87.0)
        {
            WindowSim sim = Sim(assist);
            WindowKiteState state = Flying(20.0);

            for (int i = 0; i < 400 && Heading(state) < releaseAt; i++)
                sim.Step(state, Dt, -Travel, Travel, 0.0);

            if (!assist)
                return Heading(state);

            // One step to take the target, then run until it is released again.
            sim.Step(state, Dt, 0.0, 0.0, 0.0);
            for (int i = 0; i < 100 && !double.IsNaN(state.AssistTarget); i++)
                sim.Step(state, Dt, 0.0, 0.0, 0.0);

            return Heading(state);
        }

        /// <summary>
        /// Turn hard to the right, let go the moment the kite passes the given
        /// heading, and report where it is pointing a fifth of a second later.
        /// </summary>
        private static double ExitHeading(bool assist, double releaseAt = 87.0)
        {
            WindowSim sim = Sim(assist);
            WindowKiteState state = Flying(20.0);

            for (int i = 0; i < 400 && Heading(state) < releaseAt; i++)
                sim.Step(state, Dt, -Travel, Travel, 0.0);

            for (int i = 0; i < 10; i++)
                sim.Step(state, Dt, 0.0, 0.0, 0.0);

            return Heading(state);
        }
    }
}

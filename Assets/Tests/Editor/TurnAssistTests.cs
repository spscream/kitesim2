using NUnit.Framework;
using Kitesim.Core;

namespace Kitesim.Core.Tests
{
    public class TurnAssistTests
    {
        private static double Deg(double degrees) => degrees * MathD.Deg2Rad;

        private static WindowConfiguration On() => new WindowConfiguration();

        private static WindowConfiguration Off() => new WindowConfiguration { TurnAssist = false };

        /// <summary>
        /// A kite that has just been turning hard, with the hands now centred. The
        /// grid is what the gesture asked for while the turn was on: a right angle
        /// unless a test says otherwise.
        /// </summary>
        private static WindowKiteState JustReleased(double recentTurnRate = 4.0, double gridDeg = 90.0)
        {
            return new WindowKiteState
            {
                RecentTurnRate = recentTurnRate,
                AssistGrid = Deg(gridDeg),
                Landed = false
            };
        }

        /// <summary>Hands centred: the pilot has let go.</summary>
        private static double LetGo(WindowKiteState state, double headingDeg, WindowConfiguration config)
        {
            return TurnAssist.Steering(state, 0.0, 0.0, 0.0, 0.0, Deg(headingDeg), config);
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

            Assert.AreEqual(1.0, TurnAssist.Steering(state, 1.0, 1.0, -1.0, 1.0, Deg(87), On()), 0.0);
            Assert.AreEqual(-0.8, TurnAssist.Steering(state, -0.8, -0.8, 1.0, -1.0, Deg(87), On()), 0.0);
            Assert.IsTrue(double.IsNaN(state.AssistTarget), "and nothing is being finished");
        }

        /// <summary>
        /// The second promise: it cannot begin a turn. A kite that was not turning
        /// gets no help however close it is drifting to a node.
        /// </summary>
        [Test]
        public void AKiteThatWasNotTurningIsLeftAlone()
        {
            WindowKiteState state = JustReleased(recentTurnRate: 0.2);

            Assert.AreEqual(0.0, LetGo(state, 87.0, On()), 0.0);
            Assert.IsTrue(double.IsNaN(state.AssistTarget));
        }

        /// <summary>
        /// The third promise, and the flier's own rule: a right angle is both hands
        /// hard in opposite directions, a diagonal is one hand, and everything else
        /// is flown smoothly and wants no help. A turn made with soft hands is left
        /// alone however close to a node it ends.
        /// </summary>
        [Test]
        public void ATurnFlownWithSoftHandsIsLeftAlone()
        {
            WindowKiteState state = JustReleased(gridDeg: 0.0);
            TurnAssist.Steering(state, 0.5, 0.5, -0.25, 0.25, Deg(60), On());
            Assert.AreEqual(0.0, state.AssistGrid, "no corner was asked for");

            Assert.AreEqual(0.0, LetGo(state, 87.0, On()), 0.0);
            Assert.IsTrue(double.IsNaN(state.AssistTarget));
        }

        /// <summary>
        /// The defect a flight found, and the reason nothing was ever helped: the
        /// gesture is read every frame while the pilot steers, and the last frames
        /// before a release have the hands already half way home, which is not a
        /// corner. Taking whatever the last frame said threw the answer away every
        /// time -- 1313 frames of corner read, and zero at all 41 releases.
        /// </summary>
        [Test]
        public void TheGestureIsRememberedFromItsStrongestMomentNotItsLast()
        {
            WindowKiteState state = JustReleased(gridDeg: 0.0);

            TurnAssist.Steering(state, 1.0, 1.0, -1.0, 1.0, Deg(50), On());
            Assert.AreEqual(Deg(90), state.AssistGrid, 1e-12, "both hands hard: a corner");

            // Hands on the way back, still above the finish threshold.
            TurnAssist.Steering(state, 0.5, 0.5, -0.25, 0.25, Deg(80), On());
            Assert.AreEqual(Deg(90), state.AssistGrid, 1e-12, "the corner is still what was asked for");

            LetGo(state, 87.0, On());
            Assert.AreEqual(Deg(90), state.AssistTarget, 1e-9, "and it finishes on the right angle");
        }

        /// <summary>
        /// The second thing a flight found: hands do not arrive or leave together.
        /// A two-handed corner begins as one hand, becomes two, and ends as one
        /// again, so the last reading of the gesture is a diagonal -- and a turn with
        /// both sticks to 0.83 came out as a 45. The strongest claim wins, and a
        /// right angle outranks a diagonal.
        /// </summary>
        [Test]
        public void ADiagonalAtTheEndOfACornerDoesNotDemoteIt()
        {
            WindowKiteState state = JustReleased(gridDeg: 0.0);

            TurnAssist.Steering(state, 0.7, 0.7, -0.1, 1.0, Deg(20), On());
            Assert.AreEqual(Deg(45), state.AssistGrid, 1e-12, "one hand first");

            TurnAssist.Steering(state, 1.0, 1.0, -1.0, 1.0, Deg(50), On());
            Assert.AreEqual(Deg(90), state.AssistGrid, 1e-12, "then both: a corner");

            TurnAssist.Steering(state, 0.7, 0.7, -0.1, 1.0, Deg(80), On());
            Assert.AreEqual(Deg(90), state.AssistGrid, 1e-12, "one hand leaves first, still a corner");
        }

        /// <summary>
        /// It does not carry over either: a corner flown a while ago must not decide
        /// the next figure. When the kite stops turning the gesture is forgotten.
        /// </summary>
        [Test]
        public void AGestureIsForgottenOnceTheTurnIsOver()
        {
            WindowKiteState state = JustReleased(recentTurnRate: 0.1, gridDeg: 90.0);

            LetGo(state, 87.0, On());

            Assert.AreEqual(0.0, state.AssistGrid);
        }

        [Test]
        public void BothHandsHardInOppositeDirectionsAskForARightAngle()
        {
            Assert.AreEqual(Deg(90), TurnAssist.GridFromGesture(-1.0, 1.0, On()), 1e-12);
            Assert.AreEqual(Deg(90), TurnAssist.GridFromGesture(0.8, -0.7, On()), 1e-12);
        }

        [Test]
        public void OneHandAsksForADiagonal()
        {
            Assert.AreEqual(Deg(45), TurnAssist.GridFromGesture(0.0, 1.0, On()), 1e-12);
            Assert.AreEqual(Deg(45), TurnAssist.GridFromGesture(-0.9, 0.1, On()), 1e-12);
        }

        /// <summary>Both hands the same way is the brakes, and the brakes are not a turn.</summary>
        [Test]
        public void BothHandsTheSameWayAskForNothing()
        {
            Assert.AreEqual(0.0, TurnAssist.GridFromGesture(0.9, 0.9, On()), 1e-12);
            Assert.AreEqual(0.0, TurnAssist.GridFromGesture(-0.8, -0.8, On()), 1e-12);
        }

        [Test]
        public void SmoothHandsAskForNothing()
        {
            Assert.AreEqual(0.0, TurnAssist.GridFromGesture(-0.4, 0.4, On()), 1e-12);
            Assert.AreEqual(0.0, TurnAssist.GridFromGesture(0.0, 0.5, On()), 1e-12);
        }

        [Test]
        public void LettingGoShortOfARightAngleStartsFinishingOnIt()
        {
            WindowKiteState state = JustReleased();

            double steering = LetGo(state, 87.0, On());

            Assert.Greater(steering, 0.0, "still turning right, toward 90");
            Assert.AreEqual(Deg(90), state.AssistTarget, 1e-9);
        }

        [Test]
        public void LettingGoPastARightAngleComesBackToIt()
        {
            WindowKiteState state = JustReleased();

            double steering = LetGo(state, 93.0, On());

            Assert.Less(steering, 0.0, "turning back left, toward 90");
            Assert.AreEqual(Deg(90), state.AssistTarget, 1e-9);
        }

        /// <summary>
        /// A one-handed turn is a diagonal, so forty-five is a node for it and the
        /// two-handed corner grid would have missed it entirely.
        /// </summary>
        [Test]
        public void AOneHandedTurnFinishesOnTheDiagonal()
        {
            WindowKiteState state = JustReleased(gridDeg: 45.0);

            double steering = LetGo(state, 43.0, On());

            Assert.Greater(steering, 0.0);
            Assert.AreEqual(Deg(45), state.AssistTarget, 1e-9);
        }

        /// <summary>
        /// And a two-handed corner does not stop on a diagonal: the pilot asked for a
        /// right angle, so forty-three degrees is on the way to ninety.
        /// </summary>
        [Test]
        public void ATwoHandedTurnIgnoresTheDiagonals()
        {
            WindowKiteState state = JustReleased(gridDeg: 90.0);

            Assert.AreEqual(0.0, LetGo(state, 43.0, On()), 0.0);
            Assert.IsTrue(double.IsNaN(state.AssistTarget));
        }

        /// <summary>
        /// A pilot who lets go a long way from the grid meant to be there. Half way
        /// between two right angles is a choice, not a slip.
        /// </summary>
        [Test]
        public void LettingGoFarFromAnyNodeIsLeftAlone()
        {
            WindowKiteState state = JustReleased();

            // Half way between two right angles: as far from the grid as it is
            // possible to be, and unmistakably a choice.
            Assert.AreEqual(0.0, LetGo(state, 45.0, On()), 0.0);
            Assert.IsTrue(double.IsNaN(state.AssistTarget));
        }

        /// <summary>
        /// It finishes turns, it does not fly them: the help never steers harder than
        /// the input the pilot has just come below.
        /// </summary>
        [Test]
        public void ItNeverSteersHarderThanTheInputThePilotLetGoOf()
        {
            WindowConfiguration config = On();

            for (int i = -360; i <= 360; i++)
            {
                WindowKiteState state = JustReleased(20.0);
                double steering = LetGo(state, i, config);

                Assert.LessOrEqual(MathD.Abs(steering), config.TurnAssistFinishInput + 1e-12,
                    $"at {i} degrees");
            }
        }

        [Test]
        public void HelpIsMirroredLeftAndRight()
        {
            Assert.AreEqual(
                -LetGo(JustReleased(), 87.0, On()),
                LetGo(JustReleased(), -87.0, On()), 1e-12);
        }

        /// <summary>
        /// Straight down is a node like any other, and it is where the heading angle
        /// wraps.
        /// </summary>
        [Test]
        public void TheGridSurvivesTheWrapAtHalfATurn()
        {
            Assert.Greater(LetGo(JustReleased(), 177.0, On()), 0.0);
            Assert.Less(LetGo(JustReleased(), -177.0, On()), 0.0);
        }

        [Test]
        public void ArrivingAtTheNodeEndsTheHelp()
        {
            WindowKiteState state = JustReleased();
            LetGo(state, 87.0, On());
            Assert.IsFalse(double.IsNaN(state.AssistTarget), "finishing");

            double steering = LetGo(state, 90.0, On());

            Assert.AreEqual(0.0, steering, 0.0);
            Assert.IsTrue(double.IsNaN(state.AssistTarget), "let go on arrival");
            Assert.AreEqual(0.0, state.AssistGrid, "and the gesture is spent");
        }

        [Test]
        public void TakingHoldAgainCancelsTheHelpImmediately()
        {
            WindowKiteState state = JustReleased();
            LetGo(state, 87.0, On());

            double steering = TurnAssist.Steering(state, -1.0, -1.0, 1.0, -1.0, Deg(88), On());

            Assert.AreEqual(-1.0, steering, 0.0, "the pilot's input, untouched");
            Assert.IsTrue(double.IsNaN(state.AssistTarget));
        }

        [Test]
        public void SwitchedOffItDoesNothing()
        {
            WindowKiteState state = JustReleased();

            Assert.AreEqual(0.0, LetGo(state, 87.0, Off()), 0.0);
            Assert.IsTrue(double.IsNaN(state.AssistTarget));
            Assert.AreEqual(0.0, state.AssistGrid);
            Assert.AreEqual(0.0, LetGo(state, 87.0, null), 0.0);
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
        /// The flier's second case, through the model: one hand does a diagonal. The
        /// same release that finishes on ninety with both hands finishes on
        /// forty-five with one.
        /// </summary>
        [Test]
        public void AOneHandedTurnFinishesOnTheDiagonal()
        {
            WindowSim sim = Sim(assist: true);
            WindowKiteState state = Flying(10.0);

            // One hand back, the other resting: the diagonal gesture.
            for (int i = 0; i < 400 && Heading(state) < 42.0; i++)
                sim.Step(state, Dt, 0.0, Travel, 0.0);

            sim.Step(state, Dt, 0.0, 0.0, 0.0);
            for (int i = 0; i < 100 && !double.IsNaN(state.AssistTarget); i++)
                sim.Step(state, Dt, 0.0, 0.0, 0.0);

            Assert.AreEqual(45.0, Heading(state), 1.0, $"ended at {Heading(state):F2} deg");
        }

        /// <summary>
        /// The flight this was all measured from. Flying squares, the pilot let go a
        /// median of 21 degrees from the right angle -- at the true release, before
        /// the hands had caught up -- because at 300 degrees a second a right angle
        /// cannot be timed by eye. So this is the case that has to work: let go 21
        /// degrees short, with the hands still coming home over their own 0.18 s, and
        /// the corner ends on the grid.
        /// </summary>
        [Test]
        public void TheCaseFromTheRecordedFlightEndsOnTheGrid()
        {
            WindowSim sim = Sim(assist: true);
            WindowKiteState state = Flying(20.0);

            for (int i = 0; i < 400 && Heading(state) < 69.0; i++)
                sim.Step(state, Dt, -Travel, Travel, 0.0, -Travel, Travel);

            // Let go: intent is zero at once, the hands take 0.18 s to follow.
            double hands = 1.0;
            for (int i = 0; i < 100 && !double.IsNaN(state.AssistTarget) || i < 5; i++)
            {
                hands = MathD.Max(hands - 3.57 * Dt, 0.0);
                sim.Step(state, Dt, -hands * Travel, hands * Travel, 0.0, 0.0, 0.0);
            }

            Assert.AreEqual(90.0, Heading(state), 2.0, $"ended at {Heading(state):F1} deg");
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
            double helped = ExitHeading(assist: true, releaseAt: 45.0);
            double alone = ExitHeading(assist: false, releaseAt: 45.0);

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

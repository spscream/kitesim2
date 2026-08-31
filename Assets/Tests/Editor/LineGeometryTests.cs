using NUnit.Framework;
using Kitesim.Core;

namespace Kitesim.Core.Tests
{
    public class LineGeometryTests
    {
        private const double HandHalf = 0.12;
        private const double TowHalf = 0.55;
        private const double Contact = 0.06;

        private static readonly Vec3 HandLeft = new Vec3(-HandHalf, 1.34, 0.0);
        private static readonly Vec3 HandRight = new Vec3(HandHalf, 1.34, 0.0);

        /// <summary>Towpoints across the kite's span, out at the end of the lines.</summary>
        private static void Towpoints(double rollRad, out Vec3 left, out Vec3 right)
        {
            Vec3 centre = new Vec3(0.0, 25.0, 37.0);
            Vec3 axis = (centre - new Vec3(0.0, 1.34, 0.0)).Normalized();
            Vec3 across = KiteStateView.TangentRight(axis);
            Vec3 up = KiteStateView.TangentUp(axis);
            Vec3 out_ = across * MathD.Cos(rollRad) + up * MathD.Sin(rollRad);
            left = centre - out_ * TowHalf;
            right = centre + out_ * TowHalf;
        }

        private static void Build(double turnsRad, double sag, out Vec3[] left, out Vec3[] right,
            int points = 97, double roll = 0.0)
        {
            left = new Vec3[points];
            right = new Vec3[points];
            Towpoints(roll, out Vec3 towLeft, out Vec3 towRight);
            LineGeometry.TwistedPair(HandLeft, HandRight, towLeft, towRight,
                turnsRad, sag, Contact, left, right);
        }

        private static double Distance(Vec3 a, Vec3 b) => (a - b).Length();

        [Test]
        public void TheLinesEndExactlyAtTheHandlesAndTheTowpoints()
        {
            Build(3.0 * MathD.PI, 0.4, out Vec3[] left, out Vec3[] right);
            Towpoints(0.0, out Vec3 towLeft, out Vec3 towRight);

            Assert.AreEqual(0.0, Distance(left[0], HandLeft), 1e-12, "left at the handle");
            Assert.AreEqual(0.0, Distance(right[0], HandRight), 1e-12, "right at the handle");
            Assert.AreEqual(0.0, Distance(left[left.Length - 1], towLeft), 1e-12, "left at the towpoint");
            Assert.AreEqual(0.0, Distance(right[right.Length - 1], towRight), 1e-12, "right at the towpoint");
        }

        /// <summary>
        /// With no twist and no sag the pair is two straight lines: this is the case
        /// the drawing has to keep looking like, because it is most of flying.
        /// </summary>
        [Test]
        public void UntwistedAndTautTheLinesAreStraight()
        {
            Build(0.0, 0.0, out Vec3[] left, out Vec3[] right);
            Towpoints(0.0, out Vec3 towLeft, out Vec3 towRight);

            for (int i = 0; i < left.Length; i++)
            {
                double t = (double)i / (left.Length - 1);
                Assert.AreEqual(0.0, Distance(left[i], HandLeft + (towLeft - HandLeft) * t), 1e-9, $"left at {t:F2}");
                Assert.AreEqual(0.0, Distance(right[i], HandRight + (towRight - HandRight) * t), 1e-9, $"right at {t:F2}");
            }
        }

        [Test]
        public void TheTwoLinesAlwaysStraddleTheSameCentreLine()
        {
            Build(4.0 * MathD.PI, 0.6, out Vec3[] left, out Vec3[] right);

            for (int i = 1; i < left.Length - 1; i++)
            {
                Vec3 middle = (left[i] + right[i]) * 0.5;
                Vec3 chord = HandRight - HandLeft;
                // The midpoint carries no sideways offset of its own: it sits on the
                // centre curve, which is what makes the pair read as one braid.
                Assert.AreEqual(0.0, Vec3.Dot(middle - MidCentre(i, left.Length, 0.6), chord.Normalized()), 1e-9);
            }
        }

        private static Vec3 MidCentre(int i, int count, double sag)
        {
            double t = (double)i / (count - 1);
            Vec3 handCentre = (HandLeft + HandRight) * 0.5;
            Towpoints(0.0, out Vec3 towLeft, out Vec3 towRight);
            Vec3 towCentre = (towLeft + towRight) * 0.5;
            return handCentre + (towCentre - handCentre) * t - Vec3.Up * (sag * 4.0 * t * (1.0 - t));
        }

        /// <summary>
        /// The pair winds once around for every full turn the kite has made. Counted
        /// the same way the wrap counter counts: the accumulated turning of the vector
        /// between the two lines.
        /// </summary>
        [Test]
        public void ThePairWindsOnceForEveryTurnTheKiteHasMade()
        {
            foreach (double turns in new[] { 1.0, 2.0, 3.0, -2.0 })
            {
                Build(turns * 2.0 * MathD.PI, 0.0, out Vec3[] left, out Vec3[] right, points: 361);

                Vec3 axis = (MidCentre(360, 361, 0.0) - MidCentre(0, 361, 0.0)).Normalized();
                double wound = 0.0;
                Vec3 previous = Across(left[0], right[0], axis);
                for (int i = 1; i < left.Length; i++)
                {
                    Vec3 now = Across(left[i], right[i], axis);
                    wound += SignedAngle(previous, now, axis);
                    previous = now;
                }

                Assert.AreEqual(turns, wound / (2.0 * MathD.PI), 0.02, $"{turns} turns of the kite");
            }
        }

        /// <summary>
        /// Twisted lines lie against each other, so the braid in the middle is
        /// narrower than the pair is at either end. Untwisted, nothing is pinched.
        /// </summary>
        [Test]
        public void TwistPinchesTheMiddleTogetherAndNoTwistDoesNot()
        {
            Build(0.0, 0.0, out Vec3[] clear, out Vec3[] clearRight);
            Build(2.0 * 2.0 * MathD.PI, 0.0, out Vec3[] wound, out Vec3[] woundRight);

            int middle = clear.Length / 2;
            double clearGap = Distance(clear[middle], clearRight[middle]);
            double woundGap = Distance(wound[middle], woundRight[middle]);

            Assert.AreEqual(2.0 * Contact, woundGap, 1e-6, "wound lines lie together");
            Assert.Greater(clearGap, 3.0 * woundGap, "clear lines keep their spread");
        }

        [Test]
        public void SagHangsTheMiddleBelowTheStraightLineByTheGivenDepth()
        {
            const double sag = 0.8;
            Build(0.0, sag, out Vec3[] left, out Vec3[] right);
            Towpoints(0.0, out Vec3 towLeft, out Vec3 towRight);

            int middle = left.Length / 2;
            Vec3 chordMiddle = (HandLeft + towLeft) * 0.5;

            Assert.AreEqual(sag, chordMiddle.Y - left[middle].Y, 0.02);
        }

        /// <summary>
        /// A kite hanging with its towpoints turned partway round does not make the
        /// pair pretend to be straight: the drawing takes the short way to wherever
        /// the bridle actually is, on top of the whole turns it was given.
        /// </summary>
        [Test]
        public void TheDrawingFollowsTheBridleWhereverItHasRolledTo()
        {
            Build(0.0, 0.0, out Vec3[] left, out Vec3[] right, roll: 1.0);
            Towpoints(1.0, out Vec3 towLeft, out Vec3 towRight);

            Assert.AreEqual(0.0, Distance(left[left.Length - 1], towLeft), 1e-12);
            Assert.AreEqual(0.0, Distance(right[right.Length - 1], towRight), 1e-12);

            // Half way along, the pair has turned about half of that roll.
            Vec3 axis = (MidCentre(96, 97, 0.0) - MidCentre(0, 97, 0.0)).Normalized();
            double half = SignedAngle(Across(left[0], right[0], axis),
                Across(left[48], right[48], axis), axis);
            Assert.AreEqual(0.5, half, 0.06);
        }

        [Test]
        public void ABadlySizedBufferIsIgnoredRatherThanThrown()
        {
            var one = new Vec3[1];
            var other = new Vec3[5];
            Assert.DoesNotThrow(() => LineGeometry.TwistedPair(
                HandLeft, HandRight, HandLeft, HandRight, 1.0, 0.0, Contact, one, one));
            Assert.DoesNotThrow(() => LineGeometry.TwistedPair(
                HandLeft, HandRight, HandLeft, HandRight, 1.0, 0.0, Contact, one, other));
            Assert.DoesNotThrow(() => LineGeometry.TwistedPair(
                HandLeft, HandRight, HandLeft, HandRight, 1.0, 0.0, Contact, null, null));
        }

        private static Vec3 Across(Vec3 left, Vec3 right, Vec3 axis)
        {
            Vec3 v = right - left;
            v = v - axis * Vec3.Dot(v, axis);
            return v.SqrLength < 1e-12 ? KiteStateView.TangentUp(axis) : v.Normalized();
        }

        private static double SignedAngle(Vec3 from, Vec3 to, Vec3 axis)
        {
            return MathD.Atan2(Vec3.Dot(Vec3.Cross(from, to), axis), Vec3.Dot(from, to));
        }
    }
}

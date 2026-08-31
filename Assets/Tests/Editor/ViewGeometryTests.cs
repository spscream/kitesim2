using NUnit.Framework;

namespace Kitesim.Core.Tests
{
    /// <summary>
    /// The two pieces of geometry the 3D judge view needs. They live in the core
    /// rather than in a MonoBehaviour so they can be tested at all; the Unity
    /// components that use them do nothing but call these and draw the result.
    /// See docs/superpowers/specs/2026-08-31-judge-view-design.md.
    /// </summary>
    public class ViewGeometryTests
    {
        private static WindField Wind(double speed = 6.0)
        {
            return new WindField { Speed = speed, Direction = Vec3.Forward };
        }

        // ------------------------------------------------------------ iso-power

        [Test]
        public void IsoPowerArc_PointsSitOnTheSphereOfTheGivenRadius()
        {
            Vec3[] points = ViewGeometry.IsoPowerArc(Wind(), 40.0 * MathD.Deg2Rad, 45.0, 32);

            Assert.AreEqual(33, points.Length, "segments + 1 points span the arc inclusively");
            foreach (Vec3 p in points)
                Assert.AreEqual(45.0, p.Length(), 1e-9,
                    "the arc is drawn on the sphere the kite actually flies on");
        }

        [Test]
        public void IsoPowerArc_EveryPointIsAtTheRequestedAngleFromTheWindowCentre()
        {
            // This is what makes the arcs exact rather than decorative: the driving
            // wind is W * cos(theta) and depends only on this angle, so a curve of
            // constant theta is a genuine iso-power contour. See kite-anatomy.md
            // section 3.
            WindField wind = Wind();
            Vec3 centre = WindowChart.Center(wind);

            foreach (double thetaDeg in new[] { 15.0, 45.0, 75.0, 90.0 })
            {
                double theta = thetaDeg * MathD.Deg2Rad;
                foreach (Vec3 p in ViewGeometry.IsoPowerArc(wind, theta, 45.0, 24))
                {
                    double actual = MathD.Acos(MathD.Clamp(Vec3.Dot(p.Normalized(), centre), -1.0, 1.0));
                    Assert.AreEqual(thetaDeg, actual * MathD.Rad2Deg, 1e-7);
                }
            }
        }

        [Test]
        public void IsoPowerArc_SpansTheSkyFromOneHorizonToTheOther()
        {
            // The flyable window is the upper half; drawing the full circle would put
            // half of every ring underground. The arc must start and end on the
            // ground and never dip below it.
            Vec3[] points = ViewGeometry.IsoPowerArc(Wind(), 50.0 * MathD.Deg2Rad, 45.0, 64);

            Assert.AreEqual(0.0, points[0].Y, 1e-9, "starts on the ground");
            Assert.AreEqual(0.0, points[points.Length - 1].Y, 1e-9, "ends on the ground");
            Assert.AreNotEqual(points[0].X, points[points.Length - 1].X,
                "the two ends are opposite sides of the window, not the same point");

            foreach (Vec3 p in points)
                Assert.GreaterOrEqual(p.Y, -1e-9, "no part of the arc goes underground");

            // The apex sits directly above the window centre at elevation theta, not
            // 90 - theta: the centre is the horizontal downwind direction, on the
            // horizon, so angular distance from it is measured up from zero. The
            // zenith is the theta = 90 degree contour.
            Vec3 apex = points[points.Length / 2];
            Assert.AreEqual(50.0, MathD.Asin(apex.Y / 45.0) * MathD.Rad2Deg, 1e-7);
        }

        [Test]
        public void IsoPowerArc_FollowsTheWindDirection()
        {
            // Turn the wind 90 degrees and the whole window turns with it.
            var turned = new WindField { Speed = 6.0, Direction = new Vec3(1.0, 0.0, 0.0) };
            Vec3 centre = WindowChart.Center(turned);

            foreach (Vec3 p in ViewGeometry.IsoPowerArc(turned, 30.0 * MathD.Deg2Rad, 45.0, 16))
            {
                double actual = MathD.Acos(MathD.Clamp(Vec3.Dot(p.Normalized(), centre), -1.0, 1.0));
                Assert.AreEqual(30.0, actual * MathD.Rad2Deg, 1e-7);
            }
        }

        // ------------------------------------------------------------------ roll

        [Test]
        public void SynthesisedRoll_IsZeroWhenTheKiteFliesStraight()
        {
            Assert.AreEqual(0.0, ViewGeometry.SynthesiseRoll(1.2, 1.2, 0.02, 0.25, 1.0), 1e-12);
        }

        [Test]
        public void SynthesisedRoll_IsProportionalToTheTurnRate()
        {
            // The 2D model carries no roll at all, so it is synthesised from how fast
            // the heading is turning -- otherwise the kite looks flat and dead through
            // every turn. See the base design, section 6.
            double slow = ViewGeometry.SynthesiseRoll(0.0, 0.1, 0.02, 0.25, 10.0);
            double fast = ViewGeometry.SynthesiseRoll(0.0, 0.2, 0.02, 0.25, 10.0);

            Assert.AreEqual(0.25 * (0.1 / 0.02), slow, 1e-12);
            Assert.AreEqual(2.0, fast / slow, 1e-12);
        }

        [Test]
        public void SynthesisedRoll_ReversesWithTheTurn()
        {
            double right = ViewGeometry.SynthesiseRoll(0.0, 0.1, 0.02, 0.25, 10.0);
            double left = ViewGeometry.SynthesiseRoll(0.0, -0.1, 0.02, 0.25, 10.0);

            Assert.Greater(right, 0.0, "a positive turn rate banks toward the pilot's right");
            Assert.AreEqual(-right, left, 1e-12);
        }

        [Test]
        public void SynthesisedRoll_IsClampedSoAFastFlickCannotRollTheKiteOver()
        {
            double maxRoll = 0.8;
            double roll = ViewGeometry.SynthesiseRoll(0.0, 1.5, 0.02, 0.25, maxRoll);

            Assert.AreEqual(maxRoll, roll, 1e-12);
            Assert.AreEqual(-maxRoll, ViewGeometry.SynthesiseRoll(0.0, -1.5, 0.02, 0.25, maxRoll), 1e-12);
        }

        [Test]
        public void SynthesisedRoll_HandlesTheHeadingWrappingThroughPi()
        {
            // HeadingRad comes from Atan2, so it jumps from +pi to -pi. Differencing
            // the raw values there would report a turn of nearly 2*pi in one frame and
            // slam the kite to full opposite bank for a frame -- a visible flick in
            // the one place a kite passes through often, flying straight down.
            double justUnder = MathD.PI - 0.05;
            double justOver = -MathD.PI + 0.05;

            double wrapped = ViewGeometry.SynthesiseRoll(justUnder, justOver, 0.02, 0.25, 10.0);
            double equivalent = ViewGeometry.SynthesiseRoll(0.0, 0.1, 0.02, 0.25, 10.0);

            Assert.AreEqual(equivalent, wrapped, 1e-12,
                "crossing pi must read as the small turn it is, not as a 2*pi flick");
        }

        [Test]
        public void SynthesisedRoll_IsZeroForANonPositiveTimeStep()
        {
            // Unity hands out a zero delta time on the first frame and when paused.
            Assert.AreEqual(0.0, ViewGeometry.SynthesiseRoll(0.0, 0.5, 0.0, 0.25, 1.0), 1e-12);
            Assert.AreEqual(0.0, ViewGeometry.SynthesiseRoll(0.0, 0.5, -0.02, 0.25, 1.0), 1e-12);
        }

        // ------------------------------------------------------------- wrapping

        [Test]
        public void WrapPi_MapsAnyAngleIntoTheHalfOpenTurn()
        {
            Assert.AreEqual(0.0, MathD.WrapPi(0.0), 1e-12);
            Assert.AreEqual(0.5, MathD.WrapPi(0.5), 1e-12);
            Assert.AreEqual(-0.5, MathD.WrapPi(-0.5), 1e-12);
            Assert.AreEqual(-0.1, MathD.WrapPi(2.0 * MathD.PI - 0.1), 1e-12);
            Assert.AreEqual(0.1, MathD.WrapPi(-2.0 * MathD.PI + 0.1), 1e-12);
            Assert.AreEqual(0.2, MathD.WrapPi(0.2 + 8.0 * MathD.PI), 1e-9,
                "many turns of accumulated angle still land in range");
        }
    }
}

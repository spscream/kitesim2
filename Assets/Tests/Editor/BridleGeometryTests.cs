using NUnit.Framework;
using Kitesim.Core;

namespace Kitesim.Core.Tests
{
    /// <summary>
    /// Kite coordinates throughout: +X across the span to the kite's right, +Y out
    /// of the sail toward the pilot, +Z along the spine toward the nose.
    /// </summary>
    public class BridleGeometryTests
    {
        // A plausible right-hand side of a delta frame: the two spreader connectors
        // sit on the leading edge, the T-piece on the spine behind them.
        private static readonly Vec3 UpperConnector = new Vec3(0.45, 0.0, 0.30);
        private static readonly Vec3 LowerConnector = new Vec3(0.62, 0.0, -0.10);
        private static readonly Vec3 SpineAnchor = new Vec3(0.0, 0.0, -0.05);

        private const double Upper = 0.55;
        private const double Lower = 0.55;
        private const double Inhaul = 0.80;

        private static Vec3 Towpoint(double upper = Upper, double lower = Lower, double inhaul = Inhaul)
        {
            Assert.IsTrue(BridleGeometry.TryTowpoint(
                UpperConnector, LowerConnector, SpineAnchor, upper, lower, inhaul, out Vec3 point),
                "the legs should reach");
            return point;
        }

        [Test]
        public void TheTowpointSitsAtTheGivenLegLengthsFromAllThreeAnchors()
        {
            Vec3 point = Towpoint();

            Assert.AreEqual(Upper, (point - UpperConnector).Length(), 1e-9, "upper outhaul");
            Assert.AreEqual(Lower, (point - LowerConnector).Length(), 1e-9, "lower outhaul");
            Assert.AreEqual(Inhaul, (point - SpineAnchor).Length(), 1e-9, "inhaul");
        }

        /// <summary>
        /// The towpoint hangs in front of the sail, on the pilot's side. The other
        /// trilateration solution is its mirror behind the sail, where a bridle
        /// cannot be: it would have to pass through the kite.
        /// </summary>
        [Test]
        public void TheTowpointIsInFrontOfTheSailAndNotBehindIt()
        {
            Assert.Greater(Towpoint().Y, 0.0);
        }

        /// <summary>
        /// Equal outhauls put the towpoint level between the two connectors. This is
        /// the symmetry the rule of thumb assumes when it makes them one piece of
        /// line.
        /// </summary>
        [Test]
        public void EqualOuthaulsLeaveTheTowpointEquidistantFromBothConnectors()
        {
            Vec3 point = Towpoint(upper: 0.55, lower: 0.55);

            Assert.AreEqual((point - UpperConnector).Length(), (point - LowerConnector).Length(), 1e-9);
        }

        /// <summary>
        /// Trimming nose-forward: shortening the upper outhaul against the lower one
        /// walks the towpoint up the frame toward the nose, which is the movement the
        /// reference describes as making the kite faster and less powerful.
        /// See docs/kite-anatomy.md section 2.
        /// </summary>
        [Test]
        public void ShorteningTheUpperOuthaulWalksTheTowpointTowardTheNose()
        {
            double alongSpine = Towpoint(upper: 0.55, lower: 0.55).Z;
            double noseward = Towpoint(upper: 0.50, lower: 0.60).Z;
            double tailward = Towpoint(upper: 0.60, lower: 0.50).Z;

            Assert.Greater(noseward, alongSpine, "shorter upper outhaul pulls toward the nose");
            Assert.Less(tailward, alongSpine, "longer upper outhaul lets it fall back");
        }

        /// <summary>
        /// The other trim: the inhaul sets how far out along the span the towpoint
        /// sits, which is what moves the point of influence between the spine and the
        /// wingtip.
        /// </summary>
        [Test]
        public void AShorterInhaulPullsTheTowpointInTowardTheSpine()
        {
            double wide = Towpoint(inhaul: 0.85).X;
            double narrow = Towpoint(inhaul: 0.75).X;

            Assert.Less(narrow, wide);
        }

        /// <summary>
        /// Legs that cannot reach have no answer, and saying so is the point: a
        /// bridle that does not close is a mistake in the numbers, not a towpoint at
        /// some approximate place.
        /// </summary>
        [Test]
        public void LegsTooShortToMeetHaveNoTowpoint()
        {
            Assert.IsFalse(BridleGeometry.TryTowpoint(
                UpperConnector, LowerConnector, SpineAnchor, 0.05, 0.05, 0.05, out _));
        }

        [Test]
        public void LegsTooLongToMeetHaveNoTowpointEither()
        {
            Assert.IsFalse(BridleGeometry.TryTowpoint(
                UpperConnector, LowerConnector, SpineAnchor, 5.0, 0.2, 0.2, out _));
        }

        /// <summary>
        /// Three anchors in a line leave the towpoint free to spin around that line,
        /// so there is no single answer and the geometry says so rather than picking
        /// one.
        /// </summary>
        [Test]
        public void AnchorsInALineHaveNoSingleTowpoint()
        {
            Assert.IsFalse(BridleGeometry.TryTowpoint(
                new Vec3(0.0, 0.0, 0.0), new Vec3(0.0, 0.0, 0.5), new Vec3(0.0, 0.0, 1.0),
                0.6, 0.6, 0.6, out _));
        }

        [Test]
        public void TwoAnchorsInTheSamePlaceHaveNoTowpoint()
        {
            Assert.IsFalse(BridleGeometry.TryTowpoint(
                UpperConnector, UpperConnector, SpineAnchor, 0.5, 0.5, 0.8, out _));
        }

        /// <summary>
        /// The point of influence: the line, continued through the towpoint, meets
        /// the plane of the sail. It is what a flier actually tunes -- the towpoint
        /// is only where the bridle happens to hang.
        /// </summary>
        [Test]
        public void ThePointOfInfluenceIsWhereTheLineCrossesTheSail()
        {
            Vec3 towpoint = new Vec3(0.55, 0.30, 0.10);
            Vec3 towardPilot = new Vec3(0.0, 1.0, 0.0);

            Assert.IsTrue(BridleGeometry.TryPointOfInfluence(towpoint, towardPilot, out Vec3 poi));

            Assert.AreEqual(0.0, poi.Y, 1e-12, "the sail is the plane Y = 0");
            Assert.AreEqual(0.55, poi.X, 1e-12, "straight back along the line");
            Assert.AreEqual(0.10, poi.Z, 1e-12);
        }

        [Test]
        public void ALineLyingInTheSailNeverCrossesIt()
        {
            Assert.IsFalse(BridleGeometry.TryPointOfInfluence(
                new Vec3(0.55, 0.30, 0.10), new Vec3(1.0, 0.0, 0.0), out _));
        }

        /// <summary>
        /// The rule of thumb for a first bridle: outhauls that make a right angle at
        /// the towpoint. With the two connectors a distance s apart, each leg is
        /// s/sqrt(2) -- the isoceles right triangle -- and the static bridle adds the
        /// yoke a turbo would have had. See docs/kite-anatomy.md section 2.
        /// </summary>
        [Test]
        public void TheRuleOfThumbMakesARightAngleAtTheTowpoint()
        {
            double spacing = (UpperConnector - LowerConnector).Length();
            double leg = BridleGeometry.RuleOfThumbOuthaul(spacing, staticYoke: 0.0);

            Assert.AreEqual(spacing / MathD.Sqrt(2.0), leg, 1e-12);

            // Short outhauls hold the towpoint close to the leading edge, so the
            // inhaul has to be shorter than the 0.80 the other cases use or it
            // cannot reach: with these legs the towpoint is between 0.34 and 0.77
            // from the spine anchor.
            Assert.IsTrue(BridleGeometry.TryTowpoint(
                UpperConnector, LowerConnector, SpineAnchor, leg, leg, 0.70, out Vec3 point));

            Vec3 toUpper = (UpperConnector - point).Normalized();
            Vec3 toLower = (LowerConnector - point).Normalized();
            Assert.AreEqual(0.0, Vec3.Dot(toUpper, toLower), 1e-9, "a right angle at the towpoint");
        }

        [Test]
        public void TheStaticBridleAddsTheYokeThatATurboWouldHaveHad()
        {
            double spacing = (UpperConnector - LowerConnector).Length();

            Assert.AreEqual(BridleGeometry.RuleOfThumbOuthaul(spacing, 0.0) + 0.09,
                BridleGeometry.RuleOfThumbOuthaul(spacing, 0.09), 1e-12);
        }
    }
}

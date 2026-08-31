namespace Kitesim.Core
{
    /// <summary>
    /// Where the flying line is tied to the kite, and what that does.
    ///
    /// The lines do not reach the sail or the frame. Each one ends on a pigtail --
    /// the towpoint -- and a bridle of three legs spreads the pull from there onto
    /// three points of the frame: the upper spreader connector, the lower spreader
    /// connector, and the T-piece on the spine. The bridle therefore sets both the
    /// angle of attack and the character of the turn, which is why it is the thing a
    /// flier tunes. See docs/kite-anatomy.md section 2.
    ///
    /// Given three anchors and three leg lengths the towpoint is not a matter of
    /// opinion: it is the intersection of three spheres. Two points satisfy it, one
    /// in front of the sail and its mirror behind, and only the front one is a place
    /// a bridle can hang -- the other would pass through the kite.
    ///
    /// Everything here is pure geometry with no dynamics in it, which is the point:
    /// the bridle can be got right, and tested, before anything flies.
    ///
    /// Kite coordinates: +X across the span to the kite's right, +Y out of the sail
    /// toward the pilot, +Z along the spine toward the nose.
    /// </summary>
    public static class BridleGeometry
    {
        private const double Tiny = 1e-12;

        /// <summary>
        /// The towpoint for one side: trilateration of the three legs. False when the
        /// legs cannot close -- too short to meet, too long to reach, or three anchors
        /// in a line, which leaves the towpoint free to spin about that line and so
        /// has no single answer.
        /// </summary>
        public static bool TryTowpoint(
            Vec3 upperConnector, Vec3 lowerConnector, Vec3 spineAnchor,
            double upperOuthaul, double lowerOuthaul, double inhaul,
            out Vec3 towpoint)
        {
            towpoint = Vec3.Zero;

            // A frame of the three anchors: ex along the first pair, ey across it
            // toward the third, ez out of their plane. The two solutions differ only
            // in the sign of their ez component.
            Vec3 toLower = lowerConnector - upperConnector;
            double spacing = toLower.Length();
            if (spacing < Tiny)
                return false;

            Vec3 ex = toLower / spacing;
            Vec3 toSpine = spineAnchor - upperConnector;
            double alongEx = Vec3.Dot(ex, toSpine);

            Vec3 acrossVector = toSpine - ex * alongEx;
            double across = acrossVector.Length();
            if (across < Tiny)
                return false;

            Vec3 ey = acrossVector / across;
            Vec3 ez = Vec3.Cross(ex, ey);

            double x = (upperOuthaul * upperOuthaul - lowerOuthaul * lowerOuthaul + spacing * spacing)
                / (2.0 * spacing);
            double y = (upperOuthaul * upperOuthaul - inhaul * inhaul
                    + alongEx * alongEx + across * across - 2.0 * alongEx * x)
                / (2.0 * across);

            double zSquared = upperOuthaul * upperOuthaul - x * x - y * y;
            if (zSquared < 0.0)
                return false;

            double z = MathD.Sqrt(zSquared);

            // Of the two mirrored solutions, the bridle hangs on the side the sail
            // faces. The sail lies in the plane of the three anchors, so "in front"
            // is simply the sign of the out-of-plane component along +Y.
            Vec3 outOfPlane = ez * z;
            if (Vec3.Dot(outOfPlane, Vec3.Up) < 0.0)
                outOfPlane = outOfPlane * -1.0;

            towpoint = upperConnector + ex * x + ey * y + outOfPlane;
            return true;
        }

        /// <summary>
        /// The point of influence: the flying line, continued through the towpoint,
        /// meets the plane of the sail. This is what a flier is really moving when
        /// they change a leg -- Newham's finger on the spinning record, near the rim
        /// for soft wide inputs, near the centre for snappy ones. The towpoint is
        /// only where the bridle happens to hang.
        ///
        /// False when the line runs along the sail and never crosses it.
        /// </summary>
        public static bool TryPointOfInfluence(Vec3 towpoint, Vec3 towardPilot, out Vec3 pointOfInfluence)
        {
            pointOfInfluence = Vec3.Zero;

            // The sail is the plane Y = 0 in kite coordinates.
            double alongNormal = towardPilot.Y;
            if (MathD.Abs(alongNormal) < Tiny)
                return false;

            double steps = -towpoint.Y / alongNormal;
            pointOfInfluence = towpoint + towardPilot * steps;
            return true;
        }

        /// <summary>
        /// The rule of thumb for a first bridle: outhauls long enough to make a right
        /// angle at the towpoint between the two spreader connectors. For connectors
        /// a distance s apart that is s/sqrt(2) each, and a static bridle adds the
        /// 75-100 mm that a turbo bridle would have left as yoke.
        /// See docs/kite-anatomy.md section 2.
        /// </summary>
        public static double RuleOfThumbOuthaul(double connectorSpacing, double staticYoke)
        {
            return connectorSpacing / MathD.Sqrt(2.0) + staticYoke;
        }
    }
}

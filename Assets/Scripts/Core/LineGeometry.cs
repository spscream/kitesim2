namespace Kitesim.Core
{
    /// <summary>
    /// The shape of one kite's pair of lines: from the pilot's two handles to the
    /// two towpoints on the bridle, sagging under their own weight and wound around
    /// each other by however far the kite has turned about its own line axis.
    ///
    /// The twist is not decoration. Every 360 degrees a kite turns about its lines
    /// is one wrap of the pair, control is not lost, and the wraps accumulate until
    /// they are flown back out -- see docs/kite-anatomy.md section 6. Drawing the
    /// lines as they actually lie is what lets a flier see the count they are
    /// carrying without reading a number.
    ///
    /// What a wrapped pair looks like, and what it does not. Twisted lines lie
    /// against each other and travel as **one** line: nobody counts the turns by
    /// looking, which is exactly why the count is an instrument on the screen. So
    /// the winding is drawn at the radius of the line itself -- the two strands
    /// merge, and the helix they make is far too fine to see, as in life. Drawing it
    /// as a visible braid was wrong twice over: it invited counting turns off the
    /// screen, and it crumpled the pair with every wrap.
    ///
    /// They part again only at the two ends, over the last few metres, because the
    /// handles hold them a hand apart and the bridle holds them a metre apart. That
    /// splay at the ends and the single line between is the whole visible signature
    /// of a wrapped pair.
    /// </summary>
    public static class LineGeometry
    {
        /// <summary>
        /// Fill two point arrays with the left and right line. Both arrays must be
        /// the same length and hold at least two points; the caller owns them, so a
        /// view can rebuild the shape every frame without allocating.
        /// </summary>
        /// <param name="handLeft">Left handle, world space.</param>
        /// <param name="handRight">Right handle, world space.</param>
        /// <param name="towLeft">Left towpoint on the kite, world space.</param>
        /// <param name="towRight">Right towpoint on the kite, world space.</param>
        /// <param name="turnsRad">
        /// How far the kite has turned about its lines, radians, signed. Only the
        /// part beyond the half-turn already implied by which towpoint is which
        /// becomes winding; see the residual below.
        /// </param>
        /// <param name="sag">Depth of the sag at mid-span, metres, along world down.</param>
        /// <param name="contactRadius">
        /// Half the gap between the lines where they are wound together: the radius of
        /// the line itself, a millimetre. Wound lines touch.
        /// </param>
        /// <param name="mergeOverSeparations">
        /// How far from each end the pair closes up, counted in that end's own
        /// separation. Twisted lines converge over a distance set by how far apart
        /// they are held, not by a fixed number of metres: the handles hold them
        /// 0.24 m apart and close within half a metre, while the bridle holds them a
        /// metre apart and takes a couple of metres. A fixed 3 m at both ends put a
        /// visible fork far out towards the kite, which is what gave the wraps away
        /// as living past the middle of the line.
        /// </param>
        public static void TwistedPair(
            Vec3 handLeft, Vec3 handRight,
            Vec3 towLeft, Vec3 towRight,
            double turnsRad, double sag, double contactRadius, double mergeOverSeparations,
            Vec3[] left, Vec3[] right)
        {
            if (left == null || right == null || left.Length < 2 || right.Length != left.Length)
                return;

            Vec3 handCentre = (handLeft + handRight) * 0.5;
            Vec3 towCentre = (towLeft + towRight) * 0.5;

            Vec3 span = towCentre - handCentre;
            Vec3 axis = span.SqrLength < 1e-12 ? Vec3.Forward : span.Normalized();

            Vec3 handOut = handRight - handLeft;
            Vec3 towOut = towRight - towLeft;

            double handRadius = handOut.Length() * 0.5;
            double towRadius = towOut.Length() * 0.5;

            Vec3 handAxis = Perpendicular(handOut, axis);
            Vec3 towAxis = Perpendicular(towOut, axis);

            // The angle the drawing has to cover: the whole turns the kite has made,
            // plus whatever is left over between where that leaves the pair and where
            // the towpoints actually are. Without the residual the far end of the
            // braid would sit beside the towpoints instead of on them.
            double residual = SignedAngle(handAxis, towAxis, axis) - WrapPi(turnsRad);
            double total = turnsRad + WrapPi(residual);

            // One whole turn is enough to bring the lines together; less than that
            // brings them part of the way, which is what a half-wrapped pair does.
            double wound = MathD.Clamp01(MathD.Abs(turnsRad) / (2.0 * MathD.PI));
            double length = span.Length();

            // Each end closes over its own separation, so the fork at the bridle is
            // as long as the bridle is wide and the one at the handles is a hand.
            double mergeAtHands = System.Math.Max(2.0 * handRadius * mergeOverSeparations, 1e-6);
            double mergeAtKite = System.Math.Max(2.0 * towRadius * mergeOverSeparations, 1e-6);

            for (int i = 0; i < left.Length; i++)
            {
                double t = (double)i / (left.Length - 1);

                Vec3 centre = handCentre + span * t - Vec3.Up * (sag * 4.0 * t * (1.0 - t));

                // Wound evenly along the length, and closed up to a single line
                // everywhere except near the two ends, where the handles and the
                // bridle hold the lines apart.
                double radius = MathD.Lerp(handRadius, towRadius, t);
                double alongFromHands = t * length / mergeAtHands;
                double alongFromKite = (1.0 - t) * length / mergeAtKite;
                double closed = wound * MathD.Clamp01(MathD.Min(alongFromHands, alongFromKite));
                radius = MathD.Lerp(radius, MathD.Min(radius, contactRadius), closed);

                Vec3 out_ = QuaternionD.AxisAngle(axis, total * t) * handAxis;
                left[i] = centre - out_ * radius;
                right[i] = centre + out_ * radius;
            }

            // Pin the ends exactly, rather than trusting the rotation to land there:
            // a line that starts a centimetre away from the handle is visible, and
            // nothing about the twist should move where the lines are tied.
            left[0] = handLeft;
            right[0] = handRight;
            left[left.Length - 1] = towLeft;
            right[right.Length - 1] = towRight;
        }

        /// <summary>The part of v across the axis, unit length; any perpendicular if v is along it.</summary>
        private static Vec3 Perpendicular(Vec3 v, Vec3 axis)
        {
            Vec3 across = v - axis * Vec3.Dot(v, axis);
            if (across.SqrLength < 1e-12)
                return KiteStateView.TangentUp(axis);
            return across.Normalized();
        }

        private static double SignedAngle(Vec3 from, Vec3 to, Vec3 axis)
        {
            double x = Vec3.Dot(from, to);
            double y = Vec3.Dot(Vec3.Cross(from, to), axis);
            return MathD.Atan2(y, x);
        }

        private static double WrapPi(double angle)
        {
            while (angle > MathD.PI)
                angle -= 2.0 * MathD.PI;
            while (angle < -MathD.PI)
                angle += 2.0 * MathD.PI;
            return angle;
        }
    }
}

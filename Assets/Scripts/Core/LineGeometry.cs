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
    /// Two ends and a middle. The ends are pinned exactly where the handles and the
    /// towpoints are, so the drawing never disagrees with the world it hangs in.
    /// The middle carries the winding, distributed evenly along the length, and is
    /// pinched together where the lines lie against each other: twisted lines touch,
    /// and it is that narrow braid, not the separation at the ends, that reads as a
    /// wrap.
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
        /// Half the width of the braid where the lines are wound together. Real lines
        /// touch, at a millimetre or two; this is drawn wider so a wrap can be seen at
        /// all, in the same spirit as the chart drawing the kite larger than life.
        /// </param>
        public static void TwistedPair(
            Vec3 handLeft, Vec3 handRight,
            Vec3 towLeft, Vec3 towRight,
            double turnsRad, double sag, double contactRadius,
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

            double pinch = MathD.Clamp01(MathD.Abs(turnsRad) / (2.0 * MathD.PI));

            for (int i = 0; i < left.Length; i++)
            {
                double t = (double)i / (left.Length - 1);

                Vec3 centre = handCentre + span * t - Vec3.Up * (sag * 4.0 * t * (1.0 - t));

                // Wound evenly along the length, and squeezed together in the middle,
                // where twisted lines lie against one another. Both ends keep their
                // true separation: the handles and the bridle hold them apart.
                double radius = MathD.Lerp(handRadius, towRadius, t);
                double braid = MathD.Sin(MathD.PI * t) * pinch;
                radius = MathD.Lerp(radius, MathD.Min(radius, contactRadius), braid);

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

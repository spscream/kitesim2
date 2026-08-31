namespace Kitesim.Core
{
    /// <summary>
    /// Geometry the 3D judge view needs, kept out of Unity so it can be tested.
    /// The components that use it do nothing but call these and draw the result.
    /// See docs/superpowers/specs/2026-08-31-judge-view-design.md.
    /// </summary>
    public static class ViewGeometry
    {
        /// <summary>
        /// Points along one iso-power contour, drawn on the sphere of the given
        /// radius centred on the origin, from one horizon across the sky to the
        /// other. Returns segments + 1 points.
        ///
        /// The driving wind is W * cos(theta), where theta is the angular distance
        /// from the window centre, so a curve of constant theta is a genuine
        /// iso-power contour rather than decoration -- the same fact the flat
        /// chart's concentric rings rest on. See docs/kite-anatomy.md section 3.
        ///
        /// Only the sky half is produced. The full circle of constant theta passes
        /// underground, and the flyable window is the upper half, so the arc runs
        /// from the right horizon (phi = 0) over the apex to the left horizon
        /// (phi = pi). Both ends land exactly on Y = 0.
        ///
        /// The caller places this in the world. It is centred on the origin, not on
        /// a pilot: with several pilots standing in a staircase there is no single
        /// correct centre, and the view anchors the arcs to the field origin. See
        /// the design note on that approximation.
        /// </summary>
        public static Vec3[] IsoPowerArc(WindField wind, double thetaRad, double radius, int segments)
        {
            if (segments < 1)
                segments = 1;

            Vec3 centre = WindowChart.Center(wind);
            Vec3 right = WindowChart.Right(wind);
            Vec3 up = Vec3.Cross(centre, right).Normalized();

            double cosTheta = MathD.Cos(thetaRad);
            double sinTheta = MathD.Sin(thetaRad);

            var points = new Vec3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                double phi = MathD.PI * i / segments;
                Vec3 direction = centre * cosTheta
                                 + (right * MathD.Cos(phi) + up * MathD.Sin(phi)) * sinTheta;
                points[i] = direction.Normalized() * radius;
            }

            return points;
        }

        /// <summary>
        /// Bank angle synthesised from how fast the heading is turning, in radians,
        /// positive meaning the kite banks toward the pilot's right -- the direction
        /// of a positive turn rate.
        ///
        /// The reduced model carries no roll at all: it tracks a point and a heading
        /// on a sphere, nothing about the kite's rotation about its own nose. Drawn
        /// literally, the kite would go through every turn flat and dead. The base
        /// design (section 6) prescribes synthesising it from the turn rate, and puts
        /// that on the view side deliberately: a recorded track has no turn rate in
        /// it either, so deriving roll from the heading the view already sees is what
        /// makes live flight and track replay look the same.
        ///
        /// The heading arrives from Atan2 and so jumps between +pi and -pi; the
        /// difference is wrapped, or a kite flying straight down would read as
        /// turning nearly a full circle in one frame and flick to full opposite bank.
        /// </summary>
        /// <param name="previousHeadingRad">Heading on the previous frame.</param>
        /// <param name="headingRad">Heading now.</param>
        /// <param name="dt">Seconds between the two. Zero or negative yields no roll.</param>
        /// <param name="gain">Seconds of turn rate per radian of bank.</param>
        /// <param name="maxRollRad">Bank is clamped to this either way.</param>
        public static double SynthesiseRoll(
            double previousHeadingRad, double headingRad, double dt, double gain, double maxRollRad)
        {
            if (dt <= 0.0)
                return 0.0;

            double turnRate = MathD.WrapPi(headingRad - previousHeadingRad) / dt;
            return MathD.Clamp(turnRate * gain, -maxRollRad, maxRollRad);
        }
    }
}

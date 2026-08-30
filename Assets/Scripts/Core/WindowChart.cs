namespace Kitesim.Core
{
    public enum ChartProjection
    {
        Equidistant,
        Stereographic
    }

    public readonly struct ChartPoint
    {
        public readonly double X;
        public readonly double Y;

        public ChartPoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double Radius => MathD.Sqrt(X * X + Y * Y);

        public override string ToString() => $"({X:F3}, {Y:F3})";
    }

    /// <summary>
    /// Azimuthal projection of the wind-window hemisphere onto a plane, centred on
    /// the horizontal downwind direction.
    ///
    /// For a unit direction p from pilot to kite and that centre C,
    /// Dot(p, C) = cos(beta) * cos(phi) = cos(theta), so the effective wind is
    /// W * cos(theta) and depends only on the angular distance from the centre.
    /// Consequences: iso-power contours are concentric circles, the ground is the
    /// horizontal diameter, the zero-power edge of the window is the circle at
    /// theta = 90 degrees, and the flyable window is the upper half-disc.
    /// See docs/kite-anatomy.md section 3.
    /// </summary>
    public static class WindowChart
    {
        /// <summary>Chart centre in world space: the horizontal part of the wind direction.</summary>
        public static Vec3 Center(WindField wind)
        {
            Vec3 horizontal = new Vec3(wind.Direction.X, 0.0, wind.Direction.Z);
            if (horizontal.SqrLength < 1e-12)
                return Vec3.Forward;
            return horizontal.Normalized();
        }

        /// <summary>Chart +x axis in world space: the pilot's right.</summary>
        public static Vec3 Right(WindField wind)
        {
            return Vec3.Cross(Vec3.Up, Center(wind)).Normalized();
        }

        public static double RadiusFromTheta(double thetaRad, ChartProjection projection)
        {
            if (projection == ChartProjection.Stereographic)
                return 2.0 * MathD.Tan(thetaRad * 0.5);
            return thetaRad;
        }

        public static double ThetaFromRadius(double radius, ChartProjection projection)
        {
            if (projection == ChartProjection.Stereographic)
                return 2.0 * MathD.Atan(radius * 0.5);
            return radius;
        }

        /// <summary>Chart radius at which the effective wind reaches zero.</summary>
        public static double EdgeRadius(ChartProjection projection)
        {
            return RadiusFromTheta(MathD.PI * 0.5, projection);
        }

        /// <summary>
        /// Normalised power, cos(theta), at a chart radius. Clamped to [0, 1]. This is
        /// the ideal law only: it returns exactly zero at the window edge, while
        /// WindowSim.EffectiveWind applies a power floor there, so the kite the sim
        /// actually flies has a little more power at the edge than the drawn iso-power
        /// rings show.
        /// </summary>
        public static double PowerAtRadius(double radius, ChartProjection projection)
        {
            return MathD.Clamp01(MathD.Cos(ThetaFromRadius(radius, projection)));
        }

        public static ChartPoint Project(Vec3 direction, WindField wind, ChartProjection projection)
        {
            Vec3 p = direction.Normalized();
            Vec3 center = Center(wind);
            Vec3 right = Vec3.Cross(Vec3.Up, center).Normalized();

            double cosTheta = MathD.Clamp(Vec3.Dot(p, center), -1.0, 1.0);
            double radius = RadiusFromTheta(MathD.Acos(cosTheta), projection);

            // Bearing inside the plane perpendicular to the centre. That plane is
            // spanned by world up and the chart right axis, because the centre is
            // horizontal.
            double bx = Vec3.Dot(p, right);
            double by = Vec3.Dot(p, Vec3.Up);
            double bearing = MathD.Sqrt(bx * bx + by * by);
            if (bearing < 1e-12)
                return new ChartPoint(0.0, 0.0);

            return new ChartPoint(radius * bx / bearing, radius * by / bearing);
        }

        public static Vec3 Unproject(ChartPoint point, WindField wind, ChartProjection projection)
        {
            Vec3 center = Center(wind);
            Vec3 right = Vec3.Cross(Vec3.Up, center).Normalized();

            double radius = point.Radius;
            if (radius < 1e-12)
                return center;

            double theta = ThetaFromRadius(radius, projection);
            Vec3 bearing = right * (point.X / radius) + Vec3.Up * (point.Y / radius);
            return (center * MathD.Cos(theta) + bearing * MathD.Sin(theta)).Normalized();
        }
    }
}

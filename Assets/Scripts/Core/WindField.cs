namespace Kitesim.Core
{
    public class WindField
    {
        public double Speed;
        public Vec3 Direction;

        public Vec3 GetWindVector() => Direction * Speed;
    }

    public static class WindWindow
    {
        /// <summary>
        /// Effective wind speed projected onto the line from the pilot to the kite.
        /// </summary>
        /// <param name="wind">Wind field. Direction is where the wind blows to.</param>
        /// <param name="position">Kite position relative to the pilot.</param>
        public static double EffectiveWindSpeed(WindField wind, Vec3 position)
        {
            Vec3 lineDir = position.Normalized();

            // Elevation beta is measured from the horizon; sin(beta) = lineDir.Y
            double cosElevation = MathD.Sqrt(MathD.Max(0.0, 1.0 - lineDir.Y * lineDir.Y));

            // Azimuth phi is the horizontal angle from the wind direction
            Vec3 horizontal = new Vec3(lineDir.X, 0.0, lineDir.Z).Normalized();
            double cosPhi = Vec3.Dot(horizontal, wind.Direction);
            cosPhi = MathD.Clamp(cosPhi, -1.0, 1.0);

            return wind.Speed * cosElevation * cosPhi;
        }
    }
}

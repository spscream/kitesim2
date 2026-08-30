namespace Kitesim.Core
{
    public class KiteState
    {
        public Vec3 Position;
        public Vec3 Velocity;
        public Vec3 Orientation;
        public Vec3 Up;
        public bool Launched;

        public static KiteState FromSpherical(double phiRad, double betaRad, double lineLength)
        {
            double horizontal = lineLength * MathD.Cos(betaRad);
            double x = horizontal * MathD.Sin(phiRad);
            double y = lineLength * MathD.Sin(betaRad);
            double z = horizontal * MathD.Cos(phiRad);

            Vec3 lineDir = new Vec3(x, y, z).Normalized();

            // Forward points from the pilot toward the kite (downwind).
            Vec3 forward = lineDir;

            // Up is perpendicular to forward, biased toward world up.
            Vec3 upCandidate = Vec3.Cross(Vec3.Cross(forward, Vec3.Up), forward);
            if (upCandidate.SqrLength < 1e-12)
                upCandidate = Vec3.Up;

            return new KiteState
            {
                Position = new Vec3(x, y, z),
                Velocity = Vec3.Zero,
                Orientation = forward.Normalized(),
                Up = upCandidate.Normalized(),
                Launched = false
            };
        }
    }
}

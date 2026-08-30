namespace Kitesim.Core
{
    /// <summary>
    /// Mutable state of one kite in the reduced model: a point on the sphere of
    /// radius LineLength around its pilot, plus the direction it is travelling.
    /// </summary>
    public class WindowKiteState
    {
        /// <summary>Unit vector from pilot to kite.</summary>
        public Vec3 Direction = Vec3.Forward;

        /// <summary>Unit direction of travel, perpendicular to Direction.</summary>
        public Vec3 Heading = Vec3.Up;

        public double Speed;

        /// <summary>
        /// Integral of the turn rate, radians. Every 2*pi of it is one twist of the
        /// two lines around each other.
        /// </summary>
        public double AccumulatedTurn;

        public bool Landed = true;

        public int LineWraps => (int)(AccumulatedTurn / (2.0 * MathD.PI));

        /// <summary>
        /// A kite lying on the ground at the given azimuth, nose up, ready to be
        /// launched.
        /// </summary>
        public static WindowKiteState OnGround(double azimuthRad)
        {
            return new WindowKiteState
            {
                Direction = new Vec3(MathD.Sin(azimuthRad), 0.0, MathD.Cos(azimuthRad)).Normalized(),
                Heading = Vec3.Up,
                Speed = 0.0,
                AccumulatedTurn = 0.0,
                Landed = true
            };
        }
    }
}

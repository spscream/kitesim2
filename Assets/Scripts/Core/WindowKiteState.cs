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

        /// <summary>
        /// The strongest turn of the last moment, rad/s, decaying with time. The
        /// model has no rotational inertia -- centre the hands and the turn stops in
        /// the same step -- so "is a turn going on" cannot be read off the current
        /// turn rate at the one moment it matters, which is the instant the pilot
        /// lets go. This remembers it for long enough to finish. See TurnAssist.
        /// </summary>
        public double RecentTurnRate;

        /// <summary>
        /// The heading the steering help is finishing on, radians, or NaN when it is
        /// not finishing anything. Set when the pilot lets go near a node of the
        /// figure grid and cleared on arrival. See TurnAssist.
        /// </summary>
        public double AssistTarget = double.NaN;

        /// <summary>
        /// The grid the pilot's own hands asked for while they were turning, radians
        /// between nodes, or zero for "they did not ask for a corner". Read off the
        /// gesture -- both hands hard in opposite directions is a right angle, one
        /// hand is a diagonal -- and remembered until the turn is finished, because
        /// by the time the hands come back to centre the gesture is over.
        /// </summary>
        public double AssistGrid;

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
                RecentTurnRate = 0.0,
                AssistTarget = double.NaN,
                AssistGrid = 0.0,
                Landed = true
            };
        }
    }
}

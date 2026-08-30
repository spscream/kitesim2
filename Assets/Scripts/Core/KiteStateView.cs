namespace Kitesim.Core
{
    /// <summary>
    /// Model-independent snapshot of one flying kite. Both WindowSim (the reduced
    /// 2D model) and the future RigidKiteSim produce this; rendering, track
    /// recording and cross-model comparison all consume it.
    ///
    /// Direction and Heading are unit vectors, not (phi, beta, psi) angles: the
    /// angle form is singular at the zenith. Angles are derived on demand.
    /// </summary>
    public readonly struct KiteStateView
    {
        /// <summary>Pilot's hands in world space. The pilot walks, so this varies.</summary>
        public readonly Vec3 PilotPosition;

        /// <summary>Unit vector from pilot to kite.</summary>
        public readonly Vec3 Direction;

        /// <summary>Unit direction of travel, perpendicular to Direction.</summary>
        public readonly Vec3 Heading;

        public readonly double LineLength;
        public readonly double Speed;
        public readonly double Tension;
        public readonly int LineWraps;
        public readonly bool Landed;

        public KiteStateView(
            Vec3 pilotPosition,
            Vec3 direction,
            Vec3 heading,
            double lineLength,
            double speed,
            double tension,
            int lineWraps,
            bool landed)
        {
            PilotPosition = pilotPosition;
            Direction = direction;
            Heading = heading;
            LineLength = lineLength;
            Speed = speed;
            Tension = tension;
            LineWraps = lineWraps;
            Landed = landed;
        }

        public Vec3 WorldPosition => PilotPosition + Direction * LineLength;

        /// <summary>Elevation above the horizon, radians.</summary>
        public double ElevationRad => MathD.Asin(MathD.Clamp(Direction.Y, -1.0, 1.0));

        /// <summary>Azimuth from world +Z toward +X, radians.</summary>
        public double AzimuthRad => MathD.Atan2(Direction.X, Direction.Z);

        /// <summary>
        /// Heading in the tangent plane: 0 toward the zenith, +90 degrees toward
        /// the pilot's right.
        /// </summary>
        public double HeadingRad
        {
            get
            {
                Vec3 up = TangentUp(Direction);
                Vec3 right = TangentRight(Direction);
                return MathD.Atan2(Vec3.Dot(Heading, right), Vec3.Dot(Heading, up));
            }
        }

        /// <summary>
        /// World up projected into the tangent plane at the given direction: the
        /// "toward zenith" axis of the local frame. At the zenith or the nadir
        /// (direction parallel to world up), that projection is zero and the frame
        /// is genuinely undefined; in that case this returns an arbitrary unit
        /// vector perpendicular to direction, Cross(direction, world forward).
        /// </summary>
        public static Vec3 TangentUp(Vec3 direction)
        {
            Vec3 candidate = Vec3.Up - direction * Vec3.Dot(Vec3.Up, direction);
            if (candidate.SqrLength < 1e-12)
                return Vec3.Cross(direction, Vec3.Forward).Normalized();
            return candidate.Normalized();
        }

        /// <summary>
        /// The "toward the pilot's right" axis of the local frame. Chosen so that a
        /// positive turn rate rotates the heading from TangentUp toward TangentRight.
        /// </summary>
        public static Vec3 TangentRight(Vec3 direction)
        {
            return Vec3.Cross(TangentUp(direction), direction).Normalized();
        }
    }
}

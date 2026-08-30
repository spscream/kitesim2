namespace Kitesim.Core
{
    /// <summary>
    /// Reduced wind-window model. The kite is a point constrained to the sphere of
    /// radius LineLength around the pilot's hands, carrying a heading in the local
    /// tangent plane. Speed relaxes toward the quasi-steady crosswind speed;
    /// heading turns at a rate set by the steering law plus gravity; position
    /// advances along a great circle, which parallel-transports the heading.
    ///
    /// This model assumes the kite always flies nose-first like a wing. It does not
    /// reproduce stalls, slack lines, hovering, launching or landing dynamics. See
    /// the spec, section 4.
    /// </summary>
    public class WindowSim
    {
        private const double Gravity = 9.80665;

        public WindowConfiguration Config;
        public WindField Wind;
        public Vec3 PilotPosition;
        public Vec3 PilotVelocity;

        public WindowSim(WindowConfiguration config)
        {
            Config = config;
            Wind = new WindField { Speed = 5.0, Direction = Vec3.Forward };
            PilotPosition = Vec3.Zero;
            PilotVelocity = Vec3.Zero;
        }

        public void Step(WindowKiteState state, double dt, double handLeft, double handRight, double walkAxis)
        {
            if (state.Landed)
                return;

            Vec3 p = state.Direction;
            Vec3 t = state.Heading;

            double effectiveWind = EffectiveWind(p);

            double targetSpeed = Config.GlideRatio * effectiveWind;
            state.Speed += (targetSpeed - state.Speed) * MathD.Clamp01(dt / Config.SpeedLag);

            // Advance along the great circle in the heading direction. This form is
            // exact and parallel-transports the heading, so it has no singularity at
            // the zenith the way integrating (phi, beta) would.
            double sigma = state.Speed * dt / Config.LineLength;
            double cos = MathD.Cos(sigma);
            double sin = MathD.Sin(sigma);
            Vec3 nextDirection = p * cos + t * sin;
            Vec3 nextHeading = t * cos - p * sin;

            state.Direction = nextDirection.Normalized();
            state.Heading = Orthonormalize(nextHeading, state.Direction);
        }

        /// <summary>
        /// Component of the wind relative to the pilot along the line. With a still
        /// pilot this is W * cos(theta); walking adds the pilot's own motion, which
        /// is how a flier keeps a kite up in light wind.
        /// </summary>
        public double EffectiveWind(Vec3 direction)
        {
            Vec3 relativeWind = Wind.GetWindVector() - PilotVelocity;
            double along = Vec3.Dot(relativeWind, direction);
            double floor = Config.PowerFloor * Wind.Speed;
            return MathD.Max(along, floor);
        }

        public void Launch(WindowKiteState state)
        {
            if (!state.Landed)
                return;

            state.Landed = false;
            state.Heading = KiteStateView.TangentUp(state.Direction);
            state.Speed = 0.0;
        }

        public KiteStateView View(WindowKiteState state)
        {
            double airspeed = MathD.Max(state.Speed, Config.MinAirspeed);
            double tension = 0.5 * Config.AirDensity * airspeed * airspeed
                             * Config.SailArea * Config.ForceCoefficient;

            return new KiteStateView(
                PilotPosition,
                state.Direction,
                state.Heading,
                Config.LineLength,
                state.Speed,
                tension,
                state.LineWraps,
                state.Landed);
        }

        private static Vec3 Orthonormalize(Vec3 heading, Vec3 direction)
        {
            Vec3 corrected = heading - direction * Vec3.Dot(heading, direction);
            if (corrected.SqrLength < 1e-12)
                return KiteStateView.TangentUp(direction);
            return corrected.Normalized();
        }
    }
}

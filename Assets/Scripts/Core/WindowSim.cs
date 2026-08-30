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
            UpdatePilot(dt, walkAxis);

            if (state.Landed)
                return;

            Vec3 p = state.Direction;
            Vec3 t = state.Heading;

            double effectiveWind = EffectiveWind(p);

            double targetSpeed = Config.GlideRatio * effectiveWind;
            state.Speed += (targetSpeed - state.Speed) * MathD.Clamp01(dt / Config.SpeedLag);

            // Rotate the heading inside the tangent plane.
            double turnRate = TurnRate(state, handLeft, handRight);
            double dPsi = turnRate * dt;
            Vec3 turnNormal = Vec3.Cross(t, p);
            t = t * MathD.Cos(dPsi) + turnNormal * MathD.Sin(dPsi);
            state.AccumulatedTurn += dPsi;

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

        /// <summary>
        /// Turn rate about the tether axis, radians per second.
        ///
        /// The steering term is the Erhard-Strauch law: linear in the steering input
        /// and in the apparent wind. The gravity term is derived rather than fitted.
        /// The component of gravity perpendicular to the flight direction inside the
        /// tangent plane, divided by speed, is the turn rate it produces; written in
        /// angles that is g * cos(beta) * sin(psi) / v, exactly the published form.
        /// </summary>
        public double TurnRate(WindowKiteState state, double handLeft, double handRight)
        {
            double airspeed = MathD.Max(state.Speed, Config.MinAirspeed);

            double steering = MathD.Clamp(
                (handRight - handLeft) / (2.0 * Config.HandTravel), -1.0, 1.0);

            // Unit tangent 90 degrees from the heading toward increasing psi.
            Vec3 turnNormal = Vec3.Cross(state.Heading, state.Direction);
            Vec3 gravity = new Vec3(0.0, -Gravity, 0.0);

            return Config.SteeringGain * airspeed * steering
                   + Vec3.Dot(gravity, turnNormal) / airspeed;
        }

        /// <summary>
        /// Move the pilot along the downwind axis. walkAxis is +1 for a full stride
        /// downwind (toward the kite) and -1 for a full stride upwind (away from
        /// it). Walking back is the flier's main tool for keeping a kite up in light
        /// wind: it raises the wind seen relative to the pilot, which EffectiveWind
        /// already accounts for.
        /// </summary>
        public void UpdatePilot(double dt, double walkAxis)
        {
            double axis = MathD.Clamp(walkAxis, -1.0, 1.0);
            double speed = axis >= 0.0
                ? axis * Config.WalkSpeedForward
                : axis * Config.WalkSpeedBack;

            Vec3 downwind = WindowChart.Center(Wind);
            Vec3 velocity = downwind * speed;
            Vec3 next = PilotPosition + velocity * dt;

            double half = Config.FieldHalfSize;
            double clampedX = MathD.Clamp(next.X, -half, half);
            double clampedZ = MathD.Clamp(next.Z, -half, half);
            Vec3 clamped = new Vec3(clampedX, next.Y, clampedZ);

            // Recover the velocity that was actually realised, so a pilot pressed
            // against the boundary stops feeding airspeed to the kite.
            PilotVelocity = dt > 1e-12 ? (clamped - PilotPosition) / dt : Vec3.Zero;
            PilotPosition = clamped;
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

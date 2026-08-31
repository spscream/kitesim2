namespace Kitesim.Core
{
    /// <summary>
    /// Reduced wind-window model. The kite is a point constrained to the sphere of
    /// radius LineLength around the pilot's hands, carrying a heading in the local
    /// tangent plane. Speed relaxes toward the quasi-steady crosswind speed;
    /// heading turns at a rate set by the steering law plus a fitted weight term;
    /// position advances along a great circle, which parallel-transports the
    /// heading. Step also clamps the kite to the downwind hemisphere (theta &lt;= 90 degrees) --
    /// see the comment in Step for why that boundary exists.
    ///
    /// This model assumes the kite always flies nose-first like a wing. It does not
    /// reproduce stalls, slack lines, hovering or trick moves. Launching and landing
    /// are present, but as state transitions rather than simulated dynamics:
    /// touching the ground moves the kite to "landed" (Land()), and a launch command
    /// returns it to flight (Launch()). See the spec, section 4.
    ///
    /// One WindowSim instance serves exactly one pilot: Step mutates PilotPosition
    /// and PilotVelocity, so sharing an instance across two kites would walk the
    /// pilot twice per frame.
    /// </summary>
    public class WindowSim
    {
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

            // This model has no reverse flight. Upwind of the window edge,
            // EffectiveWind goes negative (deliberately -- see EffectiveWind's
            // comment), which pulls targetSpeed negative and would relax Speed
            // below zero. A real kite that loses its driving wind slows to a stop
            // and hangs there; it does not back up along its heading. Clamp so a
            // negative Speed can never advance the kite backwards below.
            state.Speed = MathD.Max(state.Speed, 0.0);

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

            // Keep the kite inside the downwind hemisphere. The window edge (theta =
            // 90 degrees) is the great circle where Direction is perpendicular to the
            // wind centre, i.e. Dot(Direction, center) == 0; downwind of it that dot
            // is positive, upwind it is negative. The lines cannot drive a kite
            // upwind of that edge: past it EffectiveWind goes negative, the Speed
            // clamp above pins Speed at exactly zero, and because sigma =
            // Speed*dt/LineLength is then also exactly zero, nothing in the state can
            // ever change again -- measured, a kite left with neutral hands freezes
            // for good within 1.74-4.52 s from ordinary starting elevations (15-75
            // degrees, 6 m/s wind). Project any excursion back onto the edge instead
            // of letting it cross: at the edge itself the power floor still applies
            // (EffectiveWind's ">= 0" branch), so Speed stays nonzero and the kite
            // keeps moving -- either sliding along the edge or turning back into the
            // window under the gravity term -- rather than freezing. This does not
            // touch the legitimate parked state at theta == 90 exactly (see
            // KiteLeftAlone_DriftsToAndParksAtTheWindowEdge and
            // ParkedAtTheEdge_SteeringTheNoseDownLandsIt): those already sit on this
            // boundary and are unaffected by a clamp that only fires strictly past it.
            Vec3 center = WindowChart.Center(Wind);
            if (Vec3.Dot(state.Direction, center) < 0.0)
            {
                state.Direction = ProjectOntoEdge(state.Direction, center);
                state.Heading = Orthonormalize(state.Heading, state.Direction);
            }

            if (state.Direction.Y < 0.0)
                Land(state);
        }

        /// <summary>
        /// Component of the wind relative to the pilot along the line. With a still
        /// pilot this is W * cos(theta); walking adds the pilot's own motion, which
        /// is how a flier keeps a kite up in light wind.
        ///
        /// The power floor (Config.PowerFloor) only ever raises this value, and only
        /// downwind of the pilot (along positive): it exists so the zenith and the
        /// edge of the window, where the cos(theta) law gives exactly zero, still
        /// give a flying kite a little driving wind, matching a real kite still
        /// pulling a little there. Upwind of the pilot (along negative) the kite is
        /// being blown backwards; the floor does not apply there, so the value stays
        /// negative or zero and the kite loses speed rather than being handed
        /// artificial power to keep flying toward the upwind horizon.
        ///
        /// This is a different law from the pre-existing WindWindow.EffectiveWindSpeed:
        /// that one horizontalises the wind (cos(beta) * cos(phi), assuming Wind is
        /// horizontal), this one dots direction with the full 3D wind vector. They
        /// coincide for horizontal wind but diverge for tilted wind, since they then
        /// disagree about where "the window centre" (the point of maximum power) is:
        /// for a wind tilted 30 degrees up at 5 m/s, this method reaches the full
        /// 5.00 m/s exactly where direction is aligned with the tilted wind, while
        /// WindWindow.EffectiveWindSpeed only reaches 4.33 m/s at its own (horizontal)
        /// window centre. This method uses the full 3D form because the kite's
        /// position, not an assumed-horizontal wind, is what the sim actually tracks.
        /// </summary>
        public double EffectiveWind(Vec3 direction)
        {
            // Wind.Direction is not guaranteed to be unit length (WindField does not
            // enforce it, and WindowChart.Center normalises its own copy rather than
            // mutating Wind), so normalise a local copy here rather than trust
            // GetWindVector() -- otherwise a caller-supplied Direction with a
            // magnitude other than 1 would silently scale the wind speed.
            Vec3 windVector = Wind.Direction.Normalized() * Wind.Speed;
            Vec3 relativeWind = windVector - PilotVelocity;
            double along = Vec3.Dot(relativeWind, direction);
            if (along < 0.0)
                return along;
            double floor = Config.PowerFloor * Wind.Speed;
            return MathD.Max(along, floor);
        }

        /// <summary>
        /// Turn rate about the tether axis, radians per second.
        ///
        /// The steering term is the Erhard-Strauch law: linear in the steering input
        /// and in the apparent wind. The weight term keeps the closed form of that
        /// same published law -- the component of a downward vector perpendicular to
        /// the flight direction inside the tangent plane, divided by speed, written
        /// in angles as c * cos(beta) * sin(psi) / v -- but the constant c is
        /// Config.WeightTurnCoefficient, a fitted-per-kite parameter (see
        /// WindowConfiguration), not literally g. Deriving it from an unpowered
        /// point mass on a sphere over-constrains the model: a real kite is not
        /// unpowered, and the source literature (Erhard &amp; Strauch; Fechner et al.)
        /// treats this constant as fitted, not derived.
        /// </summary>
        public double TurnRate(WindowKiteState state, double handLeft, double handRight)
        {
            double airspeed = MathD.Max(state.Speed, Config.MinAirspeed);

            double steering = MathD.Clamp(
                (handRight - handLeft) / (2.0 * Config.HandTravel), -1.0, 1.0);

            // Unit tangent 90 degrees from the heading toward increasing psi.
            Vec3 turnNormal = Vec3.Cross(state.Heading, state.Direction);
            Vec3 weight = new Vec3(0.0, -Config.WeightTurnCoefficient, 0.0);

            return Config.SteeringGain * airspeed * steering
                   + Vec3.Dot(weight, turnNormal) / airspeed;
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
            // Config.MinAirspeed is documented as existing only for the gravity turn
            // term's denominator; using the floored value here would report a
            // grounded (or merely slow) kite pulling nonzero line tension. Use the
            // kite's actual speed, which is exactly zero for a landed kite.
            double tension = 0.5 * Config.AirDensity * state.Speed * state.Speed
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

        /// <summary>
        /// Removes the component of direction along center and renormalises, landing
        /// exactly on the theta = 90 degree circle (Dot(result, center) == 0). Used to
        /// pull a direction that has gone upwind of the window edge back onto it.
        /// </summary>
        private static Vec3 ProjectOntoEdge(Vec3 direction, Vec3 center)
        {
            Vec3 corrected = direction - center * Vec3.Dot(direction, center);
            if (corrected.SqrLength < 1e-12)
                return Vec3.Cross(Vec3.Up, center).Normalized();
            return corrected.Normalized();
        }

        private static void Land(WindowKiteState state)
        {
            Vec3 horizontal = new Vec3(state.Direction.X, 0.0, state.Direction.Z);
            state.Direction = horizontal.SqrLength < 1e-12
                ? Vec3.Forward
                : horizontal.Normalized();
            state.Heading = KiteStateView.TangentUp(state.Direction);
            state.Speed = 0.0;
            state.Landed = true;
        }
    }
}

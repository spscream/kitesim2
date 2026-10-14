namespace Kitesim.Core
{
    /// <summary>
    /// 6-DOF rigid kite on two compliant 45 m lines. Gravity and distance
    /// projection only: aero is a later slice. Does not replace WindowSim.
    /// </summary>
    public class RigidKiteSim
    {
        public const double Substep = 0.002;
        public const double Gravity = 9.80665;
        public const double RestLength = 45.0;

        public LineSystem Lines { get; private set; } = new LineSystem();

        public FrameInertia Inertia;
        public SailPanel[] Panels = System.Array.Empty<SailPanel>();
        public WindField Wind = new WindField { Speed = 0.0, Direction = Vec3.Forward };
        public double AirDensity = 1.225;
        public Vec3 TowLeftBody;
        public Vec3 TowRightBody;
        public Vec3 HandLeft = new Vec3(-0.12, 0.0, 0.0);
        public Vec3 HandRight = new Vec3(0.12, 0.0, 0.0);

        /// <summary>
        /// Nose up, sail facing the pilot: body +Z → world +Y, body +Y → world −Z.
        /// </summary>
        public static QuaternionD NoseUpFacingPilot =>
            QuaternionD.AxisAngle(new Vec3(1.0, 0.0, 0.0), -MathD.PI / 2.0);

        /// <summary>
        /// Sail facing the pilot along the lines, nose toward the zenith in the
        /// tangent plane. This is the flying attitude: a vertical sail has no lift.
        /// </summary>
        public static QuaternionD FacingPilot(Vec3 position)
        {
            Vec3 lineDir = position.Normalized();
            Vec3 bodyY = -lineDir;
            Vec3 bodyZ = Vec3.Cross(Vec3.Cross(lineDir, Vec3.Up), lineDir);
            if (bodyZ.SqrLength < 1e-12)
                bodyZ = Vec3.Forward;
            bodyZ = bodyZ.Normalized();
            QuaternionD q = QuaternionD.FromToRotation(Vec3.Up, bodyY);
            Vec3 zNow = q * Vec3.Forward;
            return (QuaternionD.FromToRotation(zNow, bodyZ) * q).Normalized();
        }

        public static RigidKiteSim HundredSixteen()
        {
            FrameInertia inertia = FrameInertia.FromHundredSixteen();
            AssertTowpoints(out Vec3 left, out Vec3 right);
            Vec3 com = inertia.CenterOfMass;
            return new RigidKiteSim
            {
                Inertia = inertia,
                Panels = PanelAero.HundredSixteen(com),
                TowLeftBody = left - com,
                TowRightBody = right - com,
                Lines = new LineSystem()
            };
        }

        public RigidKiteState SpawnDownwind(double elevationY)
        {
            Vec3 pilot = (HandLeft + HandRight) * 0.5;
            double z = MathD.Sqrt(RestLength * RestLength - elevationY * elevationY);
            Vec3 fromPilot = new Vec3(0.0, elevationY, z);
            RigidKiteState state = RigidKiteState.At(pilot + fromPilot);
            state.Orientation = FacingPilot(fromPilot);
            ProjectLines(state, Substep);
            return state;
        }

        /// <summary>
        /// Snapshot the mesh already consumes. Position is the centre of mass;
        /// heading is the nose projected off the line so LookRotation can aim it.
        /// </summary>
        public KiteStateView View(RigidKiteState state)
        {
            Vec3 pilot = (HandLeft + HandRight) * 0.5;
            Vec3 toKite = state.Position - pilot;
            double length = toKite.Length();
            Vec3 direction = length > 1e-9 ? toKite / length : Vec3.Forward;
            Vec3 nose = state.Orientation * Vec3.Forward;
            Vec3 heading = nose - direction * Vec3.Dot(nose, direction);
            if (heading.SqrLength < 1e-12)
                heading = KiteStateView.TangentUp(direction);
            else
                heading = heading.Normalized();

            bool landed = state.Position.Y < 0.15;
            double tension = landed ? 0.0 : 0.5 * (Lines.TensionLeft + Lines.TensionRight);
            return new KiteStateView(
                pilot,
                direction,
                heading,
                length,
                state.Velocity.Length(),
                tension,
                0.0,
                landed);
        }

        public double LineLength(RigidKiteState state, bool left)
        {
            Vec3 hand = left ? HandLeft : HandRight;
            Vec3 attach = Attach(state, left);
            return (attach - hand).Length();
        }

        public void Step(RigidKiteState state, double dt)
        {
            int n = (int)MathD.Max(1.0, MathD.Round(dt / Substep));
            double h = dt / n;
            for (int i = 0; i < n; i++)
                SubstepOnce(state, h);
        }

        private void SubstepOnce(RigidKiteState state, double dt)
        {
            Vec3 x0 = state.Position;
            QuaternionD q0 = state.Orientation;

            PanelAero.Accumulate(
                Panels,
                state.Orientation,
                state.Velocity,
                state.Omega,
                Wind.GetWindVector(),
                AirDensity,
                out Vec3 force,
                out Vec3 momentWorld);

            Vec3 mBody = state.Orientation.Conjugate() * momentWorld;
            state.Velocity += (force / Inertia.Mass + new Vec3(0.0, -Gravity, 0.0)) * dt;
            state.Omega += new Vec3(
                mBody.X / Inertia.Ixx,
                mBody.Y / Inertia.Iyy,
                mBody.Z / Inertia.Izz) * dt;
            state.Position += state.Velocity * dt;
            state.Orientation = IntegrateBodyOmega(state.Orientation, state.Omega, dt);

            ProjectLines(state, dt);

            if (state.Position.Y < 0.0)
                state.Position = new Vec3(state.Position.X, 0.0, state.Position.Z);

            state.Velocity = (state.Position - x0) / dt;
            state.Omega = QuaternionD.BodyOmega(q0, state.Orientation, dt);

            if (state.Position.Y <= 0.0 && state.Velocity.Y < 0.0)
                state.Velocity = new Vec3(state.Velocity.X, 0.0, state.Velocity.Z);
        }

        private void ProjectLines(RigidKiteState state, double dt)
        {
            Lines.Project(state, Inertia, HandLeft, TowLeftBody, HandRight, TowRightBody, dt);
        }

        private static QuaternionD IntegrateBodyOmega(QuaternionD q, Vec3 omega, double dt)
        {
            double angle = omega.Length() * dt;
            if (angle < 1e-15)
                return q;
            return (q * QuaternionD.AxisAngle(omega, angle)).Normalized();
        }

        private Vec3 Attach(RigidKiteState state, bool left)
        {
            Vec3 rBody = left ? TowLeftBody : TowRightBody;
            return state.Position + state.Orientation * rBody;
        }

        private static void AssertTowpoints(out Vec3 left, out Vec3 right)
        {
            bool ok = BridleGeometry.TryTowpoint(
                HundredSixteenLayout.UpperSpreaderRight,
                HundredSixteenLayout.LowerSpreaderRight,
                HundredSixteenLayout.InnerBridle,
                HundredSixteenLayout.UpperOuthaul,
                HundredSixteenLayout.OuterOuthaul,
                HundredSixteenLayout.Inhaul,
                out right);
            if (!ok)
                throw new System.InvalidOperationException("100Sixteen bridle does not close");
            left = new Vec3(-right.X, right.Y, right.Z);
        }
    }
}

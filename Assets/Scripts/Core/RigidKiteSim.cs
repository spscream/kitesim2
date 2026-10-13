namespace Kitesim.Core
{
    /// <summary>
    /// 6-DOF rigid kite on two inextensible 45 m lines. Gravity and distance
    /// projection only: aero is a later slice. Does not replace WindowSim.
    /// </summary>
    public class RigidKiteSim
    {
        public const double Substep = 0.002;
        public const double Gravity = 9.80665;
        public const double RestLength = 45.0;
        public const int ProjectionIterations = 8;

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
                TowRightBody = right - com
            };
        }

        public RigidKiteState SpawnDownwind(double elevationY)
        {
            double z = MathD.Sqrt(RestLength * RestLength - elevationY * elevationY);
            Vec3 position = new Vec3(0.0, elevationY, z);
            RigidKiteState state = RigidKiteState.At(position);
            state.Orientation = FacingPilot(position);
            ProjectLines(state);
            return state;
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

            ProjectLines(state);

            if (state.Position.Y < 0.0)
                state.Position = new Vec3(state.Position.X, 0.0, state.Position.Z);

            state.Velocity = (state.Position - x0) / dt;
            state.Omega = QuaternionD.BodyOmega(q0, state.Orientation, dt);

            if (state.Position.Y <= 0.0 && state.Velocity.Y < 0.0)
                state.Velocity = new Vec3(state.Velocity.X, 0.0, state.Velocity.Z);
        }

        private void ProjectLines(RigidKiteState state)
        {
            for (int i = 0; i < ProjectionIterations; i++)
            {
                ProjectOne(state, HandLeft, TowLeftBody);
                ProjectOne(state, HandRight, TowRightBody);
            }
        }

        private void ProjectOne(RigidKiteState state, Vec3 hand, Vec3 rBody)
        {
            Vec3 r = state.Orientation * rBody;
            Vec3 attach = state.Position + r;
            Vec3 delta = attach - hand;
            double dist = delta.Length();
            if (dist < 1e-12)
                return;

            Vec3 n = delta / dist;
            double c = dist - RestLength;
            Vec3 rxn = Vec3.Cross(r, n);
            Vec3 iinv = InverseInertiaWorld(state.Orientation, rxn);
            double w = 1.0 / Inertia.Mass + Vec3.Dot(rxn, iinv);
            if (w < 1e-15)
                return;

            double lambda = -c / w;
            state.Position += n * (lambda / Inertia.Mass);
            Vec3 dOmegaWorld = InverseInertiaWorld(state.Orientation, Vec3.Cross(r, n * lambda));
            state.Orientation = RotateWorld(state.Orientation, dOmegaWorld);
        }

        private Vec3 InverseInertiaWorld(QuaternionD q, Vec3 torqueWorld)
        {
            Vec3 tBody = q.Conjugate() * torqueWorld;
            Vec3 aBody = new Vec3(
                tBody.X / Inertia.Ixx,
                tBody.Y / Inertia.Iyy,
                tBody.Z / Inertia.Izz);
            return q * aBody;
        }

        private static QuaternionD IntegrateBodyOmega(QuaternionD q, Vec3 omega, double dt)
        {
            double angle = omega.Length() * dt;
            if (angle < 1e-15)
                return q;
            return (q * QuaternionD.AxisAngle(omega, angle)).Normalized();
        }

        private static QuaternionD RotateWorld(QuaternionD q, Vec3 rotVec)
        {
            double angle = rotVec.Length();
            if (angle < 1e-15)
                return q;
            return (QuaternionD.AxisAngle(rotVec, angle) * q).Normalized();
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

namespace Kitesim.Core
{
    public sealed class LineSystem
    {
        public const double RestLength = 45.0;
        public const double Stiffness = 750.0;
        public const int Iterations = 8;

        public double TensionLeft { get; private set; }
        public double TensionRight { get; private set; }

        public void Project(
            RigidKiteState state,
            FrameInertia inertia,
            Vec3 handLeft, Vec3 rLeftBody,
            Vec3 handRight, Vec3 rRightBody,
            double dt)
        {
            double lambdaL = 0.0;
            double lambdaR = 0.0;
            for (int i = 0; i < Iterations; i++)
            {
                ProjectOne(state, inertia, handLeft, rLeftBody, dt, ref lambdaL);
                ProjectOne(state, inertia, handRight, rRightBody, dt, ref lambdaR);
            }

            double scale = 1.0 / (dt * dt);
            TensionLeft = MathD.Max(0.0, -lambdaL * scale);
            TensionRight = MathD.Max(0.0, -lambdaR * scale);
        }

        private static void ProjectOne(
            RigidKiteState state,
            FrameInertia inertia,
            Vec3 hand,
            Vec3 rBody,
            double dt,
            ref double lambda)
        {
            Vec3 r = state.Orientation * rBody;
            Vec3 attach = state.Position + r;
            Vec3 delta = attach - hand;
            double dist = delta.Length();
            if (dist < 1e-12)
                return;

            Vec3 n = delta / dist;
            double c = dist - RestLength;
            if (c < 0.0)
            {
                lambda = 0.0;
                return;
            }

            Vec3 rxn = Vec3.Cross(r, n);
            Vec3 iinv = InverseInertiaWorld(inertia, state.Orientation, rxn);
            double w = 1.0 / inertia.Mass + Vec3.Dot(rxn, iinv);
            if (w < 1e-15)
                return;

            double atilde = (1.0 / Stiffness) / (dt * dt);
            double dLambda = (-c - atilde * lambda) / (w + atilde);
            lambda += dLambda;
            state.Position += n * (dLambda / inertia.Mass);
            Vec3 dOmegaWorld = InverseInertiaWorld(
                inertia, state.Orientation, Vec3.Cross(r, n * dLambda));
            state.Orientation = RotateWorld(state.Orientation, dOmegaWorld);
        }

        private static Vec3 InverseInertiaWorld(
            FrameInertia inertia, QuaternionD q, Vec3 torqueWorld)
        {
            Vec3 tBody = q.Conjugate() * torqueWorld;
            Vec3 aBody = new Vec3(
                tBody.X / inertia.Ixx,
                tBody.Y / inertia.Iyy,
                tBody.Z / inertia.Izz);
            return q * aBody;
        }

        private static QuaternionD RotateWorld(QuaternionD q, Vec3 rotVec)
        {
            double angle = rotVec.Length();
            if (angle < 1e-15)
                return q;
            return (QuaternionD.AxisAngle(rotVec, angle) * q).Normalized();
        }
    }
}

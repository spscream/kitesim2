namespace Kitesim.Core
{
    public class KiteSim
    {
        public KiteConfiguration Config;
        public Vec3 PilotPosition;
        public WindField Wind;

        public KiteSim(KiteConfiguration config)
        {
            Config = config;
            PilotPosition = Vec3.Zero;
            Wind = new WindField { Speed = 8.0, Direction = Vec3.Forward };
        }

        public void Step(KiteState state, double dt, double pullLeft, double pullRight)
        {
            // pull in [-1, 1]; +1 = pull toward the pilot (increase line tension / brake)
            double sumPull = MathD.Clamp01((pullLeft + pullRight) * 0.5);
            double diffPull = MathD.Clamp(pullRight - pullLeft, -1.0, 1.0);

            Vec3 r = state.Position - PilotPosition;
            double lineLen = r.Length();
            Vec3 lineDir = lineLen > 1e-12 ? r / lineLen : Vec3.Forward;

            // Wind window angles
            double cosElevation = MathD.Sqrt(MathD.Max(0.0, 1.0 - lineDir.Y * lineDir.Y));
            Vec3 horizontal = new Vec3(lineDir.X, 0.0, lineDir.Z);
            Vec3 horizDir = horizontal.SqrLength > 1e-12 ? horizontal.Normalized() : Vec3.Forward;
            double cosPhi = Vec3.Dot(horizDir, Wind.Direction);
            cosPhi = MathD.Clamp(cosPhi, -1.0, 1.0);

            // Effective wind component along the line
            double vWindEff = Wind.Speed * cosElevation * cosPhi;

            // Apparent wind
            Vec3 apparentWind = Wind.GetWindVector() - state.Velocity;
            double vApp = apparentWind.Length();
            Vec3 appDir = vApp > 1e-12 ? apparentWind / vApp : Wind.Direction;

            // Kite basis
            Vec3 span = Vec3.Cross(state.Up, state.Orientation).Normalized();
            Vec3 wingPlaneNormal = Vec3.Cross(state.Orientation, span).Normalized();

            // Angle of attack: pulling both increases AoA (pitch back / stall)
            double baseAoa = Config.BaseAngleOfAttack * (1.0 + Config.PitchEfficiency * sumPull);

            // Lift direction: perpendicular to apparent wind in the wing plane
            Vec3 liftDir = Vec3.Cross(appDir, span).Normalized();
            if (Vec3.Dot(liftDir, wingPlaneNormal) < 0.0)
                liftDir = -liftDir;

            double q = 0.5 * Config.AirDensity * vApp * vApp * Config.SailArea;
            var (cl, cdBase) = AeroPolar.Evaluate(baseAoa);
            double cd = cdBase * (1.0 + Config.DragSteeringFactor * MathD.Abs(diffPull))
                + Config.BrakeDragFactor * sumPull;

            Vec3 lift = liftDir * (q * cl);
            Vec3 drag = appDir * (q * cd);
            Vec3 gravity = new Vec3(0.0, -9.80665 * Config.Mass, 0.0);

            Vec3 totalForce = lift + drag + gravity;
            Vec3 acceleration = totalForce / Config.Mass;

            // Semi-implicit Euler
            state.Velocity += acceleration * dt;
            state.Position += state.Velocity * dt;

            // Spherical line-length constraint (lines pull, do not push)
            r = state.Position - PilotPosition;
            lineLen = r.Length();
            if (lineLen > Config.LineLength)
            {
                state.Position = PilotPosition + r.Normalized() * Config.LineLength;
                r = state.Position - PilotPosition;
                lineDir = r.Normalized();

                Vec3 radialVel = lineDir * Vec3.Dot(state.Velocity, lineDir);
                state.Velocity -= radialVel;
                state.Velocity *= 0.99;
            }

            // Yaw from differential line pull
            // diffPull > 0 (right pulled more) => yaw right (positive around world up)
            double yawRate = diffPull * 1.0;
            QuaternionD yaw = QuaternionD.AxisAngle(Vec3.Up, yawRate * dt);
            state.Orientation = yaw * state.Orientation;
            state.Up = yaw * state.Up;
            state.Orientation = state.Orientation.Normalized();
            state.Up = state.Up.Normalized();

            // Prevent the kite from pointing through the lines
            if (Vec3.Dot(state.Orientation, lineDir) < -0.2)
            {
                state.Orientation = (lineDir * 0.5 + state.Orientation * 0.5).Normalized();
                state.Up = (lineDir * 0.5 + state.Up * 0.5).Normalized();
            }
        }
    }
}

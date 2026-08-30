namespace Kitesim.Core
{
    public readonly struct QuaternionD
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Z;
        public readonly double W;

        public static readonly QuaternionD Identity = new QuaternionD(0.0, 0.0, 0.0, 1.0);

        public QuaternionD(double x, double y, double z, double w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public static QuaternionD AxisAngle(Vec3 axis, double angleRad)
        {
            Vec3 n = axis.Normalized();
            double half = angleRad * 0.5;
            double s = MathD.Sin(half);
            double c = MathD.Cos(half);
            return new QuaternionD(n.X * s, n.Y * s, n.Z * s, c);
        }

        public static QuaternionD FromToRotation(Vec3 fromDirection, Vec3 toDirection)
        {
            Vec3 a = fromDirection.Normalized();
            Vec3 b = toDirection.Normalized();
            double dot = Vec3.Dot(a, b);

            if (dot >= 1.0 - 1e-12)
                return Identity;

            if (dot <= -1.0 + 1e-12)
            {
                // 180-degree rotation around any perpendicular axis
                Vec3 axis = Vec3.Cross(a, Vec3.Up);
                if (axis.SqrLength < 1e-12)
                    axis = Vec3.Cross(a, Vec3.Forward);
                return AxisAngle(axis, MathD.PI);
            }

            Vec3 cross = Vec3.Cross(a, b);
            double s = MathD.Sqrt((1.0 + dot) * 2.0);
            double invS = 1.0 / s;

            return new QuaternionD(
                cross.X * invS,
                cross.Y * invS,
                cross.Z * invS,
                s * 0.5
            );
        }

        public static Vec3 operator *(QuaternionD q, Vec3 v)
        {
            // t = axis * sin(theta/2)
            Vec3 t = new Vec3(q.X, q.Y, q.Z);
            Vec3 txv = Vec3.Cross(t, v);
            Vec3 txtxv = Vec3.Cross(t, txv);
            return v + 2.0 * (q.W * txv + txtxv);
        }
    }
}

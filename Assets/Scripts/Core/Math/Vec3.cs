namespace Kitesim.Core
{
    public readonly struct Vec3
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Z;

        public static readonly Vec3 Zero = new Vec3(0.0, 0.0, 0.0);
        public static readonly Vec3 Up = new Vec3(0.0, 1.0, 0.0);
        public static readonly Vec3 Forward = new Vec3(0.0, 0.0, 1.0);

        public Vec3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double SqrLength => X * X + Y * Y + Z * Z;

        public double Length() => MathD.Sqrt(SqrLength);

        public Vec3 Normalized()
        {
            double len = Length();
            if (len < 1e-12)
                return Zero;
            return this / len;
        }

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator -(Vec3 v) => new Vec3(-v.X, -v.Y, -v.Z);
        public static Vec3 operator *(Vec3 v, double s) => new Vec3(v.X * s, v.Y * s, v.Z * s);
        public static Vec3 operator *(double s, Vec3 v) => v * s;
        public static Vec3 operator /(Vec3 v, double s) => new Vec3(v.X / s, v.Y / s, v.Z / s);

        public static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Vec3 Cross(Vec3 a, Vec3 b)
        {
            return new Vec3(
                a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X
            );
        }

        public override string ToString() => $"({X:F3}, {Y:F3}, {Z:F3})";
    }
}

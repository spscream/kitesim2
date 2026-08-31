namespace Kitesim.Core
{
    public static class MathD
    {
        public const double PI = System.Math.PI;
        public const double Deg2Rad = PI / 180.0;
        public const double Rad2Deg = 180.0 / PI;

        public static double Sqrt(double value) => System.Math.Sqrt(value);
        public static double Sin(double radians) => System.Math.Sin(radians);
        public static double Cos(double radians) => System.Math.Cos(radians);
        public static double Acos(double value) => System.Math.Acos(value);
        public static double Asin(double value) => System.Math.Asin(value);
        public static double Tan(double radians) => System.Math.Tan(radians);
        public static double Atan(double value) => System.Math.Atan(value);
        public static double Abs(double value) => System.Math.Abs(value);
        public static double Sign(double value) => System.Math.Sign(value);
        public static double Atan2(double y, double x) => System.Math.Atan2(y, x);
        public static double Max(double a, double b) => System.Math.Max(a, b);
        public static double Min(double a, double b) => System.Math.Min(a, b);

        public static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static double Clamp01(double value) => Clamp(value, 0.0, 1.0);

        /// <summary>
        /// Folds an angle into [-pi, pi). Used wherever two angles that came out of
        /// Atan2 are differenced: the raw difference jumps by 2*pi when either
        /// crosses the branch cut, which reads as an enormous rotation in one step.
        /// </summary>
        public static double WrapPi(double radians)
        {
            double turn = 2.0 * PI;
            double wrapped = System.Math.IEEERemainder(radians, turn);
            if (wrapped >= PI)
                wrapped -= turn;
            else if (wrapped < -PI)
                wrapped += turn;
            return wrapped;
        }

        public static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * Clamp01(t);
        }
    }
}

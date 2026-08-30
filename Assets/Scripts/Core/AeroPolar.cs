namespace Kitesim.Core
{
    public static class AeroPolar
    {
        public const double ALPHA_STALL_DEG = 14.0;
        public const double ALPHA_STALL = ALPHA_STALL_DEG * MathD.Deg2Rad;
        public const double CD0 = 0.03;

        /// <summary>
        /// Piecewise flat-plate polar.
        /// </summary>
        /// <param name="alphaRad">Angle of attack in radians, positive = nose up.</param>
        /// <returns>(cl, cd)</returns>
        public static (double cl, double cd) Evaluate(double alphaRad)
        {
            double cl;
            double cd;
            double absA = MathD.Abs(alphaRad);

            if (absA <= ALPHA_STALL)
            {
                // Thin-airfoil slope up to stall
                cl = 2.0 * MathD.PI * MathD.Sin(alphaRad);
                cd = 1.28 * MathD.Sin(absA) * MathD.Sin(absA) + CD0;
            }
            else
            {
                // Flat-plate post-stall
                double sign = MathD.Sign(alphaRad);
                cl = sign * 2.0 * MathD.Sin(absA) * MathD.Cos(absA);
                cd = 2.0 * MathD.Sin(absA) * MathD.Sin(absA) + CD0;
            }

            return (cl, cd);
        }
    }
}

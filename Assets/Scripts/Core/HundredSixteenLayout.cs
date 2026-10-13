namespace Kitesim.Core
{
    /// <summary>
    /// Published geometry of the 100Sixteen STD open plan, used as the temporary
    /// physical donor for M4 until a real Fury frame is measured.
    ///
    /// Source: https://www.kareloh.com/kite-plans/90three-family/100sixteen/
    /// Private non-commercial use only. The 5pt spine is 0.825 m; the p200 stub is
    /// cut to the sail so the assembled spine equals Height, with an internal
    /// ferrule and the tubes butted. The T-piece sits at Center Cross (0.597 m
    /// from the nose sewing). The inner bridle leg lands 40 mm further toward
    /// the tail. Camber/billow of the six panels is recovered from the frame,
    /// the A0 pattern and the standoff lengths, not from a flying measurement.
    ///
    /// Kite coordinates: +X to the kite's right, +Y out of the sail toward the
    /// pilot, +Z along the spine toward the nose. The sail plane is Y = 0.
    /// </summary>
    public static class HundredSixteenLayout
    {
        public const double Span = 2.60;
        public const double Height = 1.015;
        public const double Mass = 0.350;
        public const double TailWeight = 0.020;

        public const double UpperLeadingEdge = 0.810;
        public const double LowerLeadingEdge = 0.810;
        public const double UpperSpreader = 0.515;
        public const double LowerSpreader = 0.783;
        public const double Spine = 0.825;
        public const double P200Stub = Height - Spine;
        public const double InnerStandoff = 0.295;
        public const double OuterStandoff = 0.280;

        /// <summary>
        /// Skyshark Nitro Strong on the published ULE/LLE/LS: 23 g per 0.81 m tube.
        /// Used as the linear density for every carbon rod until pieces are weighed.
        /// </summary>
        public const double NitroStrongMass = 0.023;
        public const double NitroStrongLength = 0.810;
        public const double CarbonMassPerMetre = NitroStrongMass / NitroStrongLength;

        public const double UpperApaFromNose = 0.336;
        public const double LowerApaFromNose = 0.997;
        public const double CrossFromNose = 0.597;
        public const double InnerBridleFromNose = 0.637;

        public const double UpperOuthaul = 0.640;
        public const double OuterOuthaul = 0.575;
        public const double Inhaul = 0.725;

        public static Vec3 Nose => new Vec3(0.0, 0.0, Height);
        public static Vec3 Tail => Vec3.Zero;
        public static Vec3 LeftTip => new Vec3(-0.5 * Span, 0.0, Height - LowerApaFromNose);
        public static Vec3 RightTip => new Vec3(0.5 * Span, 0.0, Height - LowerApaFromNose);

        public static Vec3 UpperSpreaderLeft => Anchor(-0.5 * UpperSpreader, UpperApaFromNose);
        public static Vec3 UpperSpreaderRight => Anchor(0.5 * UpperSpreader, UpperApaFromNose);
        public static Vec3 LowerSpreaderLeft => Anchor(-0.5 * LowerSpreader, LowerApaFromNose);
        public static Vec3 LowerSpreaderRight => Anchor(0.5 * LowerSpreader, LowerApaFromNose);
        public static Vec3 CenterCross => Anchor(0.0, CrossFromNose);
        public static Vec3 TPiece => CenterCross;
        public static Vec3 InnerBridle => Anchor(0.0, InnerBridleFromNose);

        private static Vec3 Anchor(double x, double fromNose)
        {
            return new Vec3(x, 0.0, Height - fromNose);
        }
    }
}

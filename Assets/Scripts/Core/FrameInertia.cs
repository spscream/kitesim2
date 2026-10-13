namespace Kitesim.Core
{
    /// <summary>
    /// Mass, centre of mass and a diagonal inertia tensor in kite axes, built from
    /// the published carbon layout, the tail weight and the leftover sail lamina.
    /// Pitch is about +X, yaw about +Y, roll about the spine +Z.
    /// </summary>
    public readonly struct FrameInertia
    {
        public readonly double Mass;
        public readonly Vec3 CenterOfMass;
        public readonly double Ixx;
        public readonly double Iyy;
        public readonly double Izz;
        public readonly double RodMass;
        public readonly double SailMass;

        private FrameInertia(
            double mass,
            Vec3 centerOfMass,
            double ixx,
            double iyy,
            double izz,
            double rodMass,
            double sailMass)
        {
            Mass = mass;
            CenterOfMass = centerOfMass;
            Ixx = ixx;
            Iyy = iyy;
            Izz = izz;
            RodMass = rodMass;
            SailMass = sailMass;
        }

        public static FrameInertia FromHundredSixteen()
        {
            var acc = new Accumulator();
            double rho = HundredSixteenLayout.CarbonMassPerMetre;

            acc.AddRod(
                HundredSixteenLayout.Tail,
                HundredSixteenLayout.Nose,
                HundredSixteenLayout.Height * rho);
            acc.AddRod(
                HundredSixteenLayout.Nose,
                HundredSixteenLayout.LeftTip,
                (HundredSixteenLayout.UpperLeadingEdge + HundredSixteenLayout.LowerLeadingEdge) * rho);
            acc.AddRod(
                HundredSixteenLayout.Nose,
                HundredSixteenLayout.RightTip,
                (HundredSixteenLayout.UpperLeadingEdge + HundredSixteenLayout.LowerLeadingEdge) * rho);
            acc.AddRod(
                HundredSixteenLayout.UpperSpreaderLeft,
                HundredSixteenLayout.UpperSpreaderRight,
                HundredSixteenLayout.UpperSpreader * rho);
            acc.AddRod(
                HundredSixteenLayout.LowerSpreaderLeft,
                HundredSixteenLayout.LowerSpreaderRight,
                HundredSixteenLayout.LowerSpreader * rho);

            acc.AddPoint(HundredSixteenLayout.CenterCross, 2.0 * HundredSixteenLayout.InnerStandoff * rho);
            acc.AddPoint(HundredSixteenLayout.LowerSpreaderLeft, HundredSixteenLayout.OuterStandoff * rho);
            acc.AddPoint(HundredSixteenLayout.LowerSpreaderRight, HundredSixteenLayout.OuterStandoff * rho);

            double rodMass = acc.Mass;
            acc.AddPoint(HundredSixteenLayout.Tail, HundredSixteenLayout.TailWeight);

            double sailMass = HundredSixteenLayout.Mass - acc.Mass;
            double corner = sailMass * 0.25;
            acc.AddPoint(HundredSixteenLayout.Nose, corner);
            acc.AddPoint(HundredSixteenLayout.Tail, corner);
            acc.AddPoint(HundredSixteenLayout.LeftTip, corner);
            acc.AddPoint(HundredSixteenLayout.RightTip, corner);

            acc.ShiftToCenterOfMass();
            return new FrameInertia(
                acc.Mass,
                acc.CenterOfMass,
                acc.Ixx,
                acc.Iyy,
                acc.Izz,
                rodMass,
                sailMass);
        }

        private struct Accumulator
        {
            public double Mass;
            public Vec3 CenterOfMass;
            public double Ixx;
            public double Iyy;
            public double Izz;
            private double _mx;
            private double _my;
            private double _mz;
            private double _ixy;
            private double _ixz;
            private double _iyz;

            public void AddPoint(Vec3 r, double mass)
            {
                if (mass == 0.0)
                    return;

                Mass += mass;
                _mx += mass * r.X;
                _my += mass * r.Y;
                _mz += mass * r.Z;
                Ixx += mass * (r.Y * r.Y + r.Z * r.Z);
                Iyy += mass * (r.X * r.X + r.Z * r.Z);
                Izz += mass * (r.X * r.X + r.Y * r.Y);
                _ixy -= mass * r.X * r.Y;
                _ixz -= mass * r.X * r.Z;
                _iyz -= mass * r.Y * r.Z;
            }

            public void AddRod(Vec3 a, Vec3 b, double mass)
            {
                Vec3 delta = b - a;
                double length = delta.Length();
                Vec3 mid = (a + b) * 0.5;
                AddPoint(mid, mass);
                if (length < 1e-12 || mass == 0.0)
                    return;

                Vec3 u = delta / length;
                double along = mass * length * length / 12.0;
                Ixx += along * (1.0 - u.X * u.X);
                Iyy += along * (1.0 - u.Y * u.Y);
                Izz += along * (1.0 - u.Z * u.Z);
                _ixy -= along * u.X * u.Y;
                _ixz -= along * u.X * u.Z;
                _iyz -= along * u.Y * u.Z;
            }

            public void ShiftToCenterOfMass()
            {
                if (Mass == 0.0)
                {
                    CenterOfMass = Vec3.Zero;
                    return;
                }

                Vec3 c = new Vec3(_mx / Mass, _my / Mass, _mz / Mass);
                CenterOfMass = c;
                Ixx -= Mass * (c.Y * c.Y + c.Z * c.Z);
                Iyy -= Mass * (c.X * c.X + c.Z * c.Z);
                Izz -= Mass * (c.X * c.X + c.Y * c.Y);
                _ixy += Mass * c.X * c.Y;
                _ixz += Mass * c.X * c.Z;
                _iyz += Mass * c.Y * c.Z;
            }
        }
    }
}

namespace Kitesim.Core
{
    public readonly struct SailPanel
    {
        public readonly Vec3 Center;
        public readonly Vec3 Normal;
        public readonly Vec3 Chord;
        public readonly double Area;

        public SailPanel(Vec3 center, Vec3 normal, Vec3 chord, double area)
        {
            Center = center;
            Normal = normal;
            Chord = chord;
            Area = area;
        }
    }

    /// <summary>
    /// Six sail panels of the 100Sixteen: four wing facets split at the outer
    /// standoff station, two keel triangles standing off the spine. Forces use
    /// <see cref="AeroPolar"/>; angle of attack comes from the panel attitude.
    /// </summary>
    public static class PanelAero
    {
        public static SailPanel[] HundredSixteen(Vec3 com)
        {
            double splitX = 0.5 * HundredSixteenLayout.LowerSpreader;
            Vec3 teL = OnTrailingEdge(-splitX, -HundredSixteenLayout.InnerStandoff);
            Vec3 teR = OnTrailingEdge(splitX, -HundredSixteenLayout.InnerStandoff);
            Vec3 keelApex = HundredSixteenLayout.TPiece + new Vec3(0.0, -HundredSixteenLayout.InnerStandoff, 0.0);

            return new[]
            {
                FromTriangle(HundredSixteenLayout.Nose, HundredSixteenLayout.Tail, teL, com, Vec3.Up),
                FromTriangle(HundredSixteenLayout.Nose, HundredSixteenLayout.LeftTip, teL, com, Vec3.Up),
                FromTriangle(HundredSixteenLayout.Nose, HundredSixteenLayout.Tail, teR, com, Vec3.Up),
                FromTriangle(HundredSixteenLayout.Nose, HundredSixteenLayout.RightTip, teR, com, Vec3.Up),
                FromTriangle(HundredSixteenLayout.Nose, HundredSixteenLayout.TPiece, keelApex, com, new Vec3(1.0, 0.0, 0.0)),
                FromTriangle(HundredSixteenLayout.Tail, HundredSixteenLayout.TPiece, keelApex, com, new Vec3(1.0, 0.0, 0.0))
            };
        }

        public static void Accumulate(
            SailPanel[] panels,
            QuaternionD orientation,
            Vec3 velocity,
            Vec3 omegaBody,
            Vec3 wind,
            double airDensity,
            out Vec3 forceWorld,
            out Vec3 momentWorld)
        {
            forceWorld = Vec3.Zero;
            momentWorld = Vec3.Zero;
            Vec3 omegaWorld = orientation * omegaBody;

            for (int i = 0; i < panels.Length; i++)
            {
                SailPanel p = panels[i];
                if (p.Area <= 0.0)
                    continue;

                Vec3 r = orientation * p.Center;
                Vec3 vApp = wind - (velocity + Vec3.Cross(omegaWorld, r));
                double speed = vApp.Length();
                if (speed < 1e-6)
                    continue;

                Vec3 n = orientation * p.Normal;
                Vec3 chord = orientation * p.Chord;
                double flowN = -Vec3.Dot(vApp, n);
                double flowC = -Vec3.Dot(vApp, chord);
                double alpha = MathD.Atan2(flowN, flowC);
                var (cl, cd) = AeroPolar.Evaluate(alpha);

                Vec3 dragDir = vApp / speed;
                Vec3 liftDir = Vec3.Cross(Vec3.Cross(dragDir, n), dragDir);
                if (liftDir.SqrLength > 1e-12)
                {
                    liftDir = liftDir.Normalized();
                    // n is the pilot-facing (pressure) side; lift is toward the back.
                    if (Vec3.Dot(liftDir, n) > 0.0)
                        liftDir = -liftDir;
                }
                else
                {
                    liftDir = n;
                }

                double q = 0.5 * airDensity * speed * speed * p.Area;
                Vec3 force = liftDir * (q * cl) + dragDir * (q * cd);
                forceWorld += force;
                momentWorld += Vec3.Cross(r, force);
            }
        }

        private static SailPanel FromTriangle(Vec3 a, Vec3 b, Vec3 c, Vec3 com, Vec3 preferNormal)
        {
            Vec3 ab = b - a;
            Vec3 ac = c - a;
            Vec3 cross = Vec3.Cross(ab, ac);
            double twice = cross.Length();
            double area = 0.5 * twice;
            Vec3 normal = twice < 1e-12 ? preferNormal : cross / twice;
            if (Vec3.Dot(normal, preferNormal) < 0.0)
                normal = -normal;

            Vec3 centroid = (a + b + c) * (1.0 / 3.0);
            Vec3 le = a;
            if (b.Z > le.Z)
                le = b;
            if (c.Z > le.Z)
                le = c;
            Vec3 chord = le - centroid;
            if (chord.SqrLength < 1e-12)
                chord = new Vec3(0.0, 0.0, 1.0);

            return new SailPanel(centroid - com, normal, chord.Normalized(), area);
        }

        private static Vec3 OnTrailingEdge(double x, double y)
        {
            Vec3 tail = HundredSixteenLayout.Tail;
            Vec3 tip = x < 0.0 ? HundredSixteenLayout.LeftTip : HundredSixteenLayout.RightTip;
            double t = tip.X == 0.0 ? 0.0 : x / tip.X;
            Vec3 p = tail + (tip - tail) * t;
            return new Vec3(p.X, y, p.Z);
        }
    }
}

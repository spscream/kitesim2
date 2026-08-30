using NUnit.Framework;

namespace Kitesim.Core.Tests
{
    public class WindowChartTests
    {
        private static WindField DefaultWind()
        {
            return new WindField { Speed = 5.0, Direction = Vec3.Forward };
        }

        [Test]
        public void MathD_HasInverseTrigonometry()
        {
            Assert.AreEqual(1.0, MathD.Tan(MathD.PI * 0.25), 1e-12);
            Assert.AreEqual(MathD.PI * 0.25, MathD.Atan(1.0), 1e-12);
            Assert.AreEqual(MathD.PI * 0.5, MathD.Asin(1.0), 1e-12);
        }

        [Test]
        public void Center_IsHorizontalDownwind()
        {
            Vec3 c = WindowChart.Center(DefaultWind());
            Assert.AreEqual(0.0, c.X, 1e-12);
            Assert.AreEqual(0.0, c.Y, 1e-12);
            Assert.AreEqual(1.0, c.Z, 1e-12);
        }

        [Test]
        public void Right_IsPilotsRight()
        {
            Vec3 r = WindowChart.Right(DefaultWind());
            Assert.AreEqual(1.0, r.X, 1e-12);
            Assert.AreEqual(0.0, r.Y, 1e-12);
            Assert.AreEqual(0.0, r.Z, 1e-12);
        }

        [Test]
        public void WindowCentre_ProjectsToOrigin()
        {
            ChartPoint p = WindowChart.Project(Vec3.Forward, DefaultWind(), ChartProjection.Equidistant);
            Assert.AreEqual(0.0, p.X, 1e-12);
            Assert.AreEqual(0.0, p.Y, 1e-12);
        }

        [Test]
        public void Zenith_ProjectsToTopOfArc()
        {
            ChartPoint p = WindowChart.Project(Vec3.Up, DefaultWind(), ChartProjection.Equidistant);
            Assert.AreEqual(0.0, p.X, 1e-12);
            Assert.AreEqual(MathD.PI * 0.5, p.Y, 1e-12);
        }

        [Test]
        public void GroundIsTheHorizontalDiameter()
        {
            // Any direction with zero elevation must land on the chart x-axis.
            for (int degrees = -80; degrees <= 80; degrees += 20)
            {
                double phi = degrees * MathD.Deg2Rad;
                Vec3 direction = new Vec3(MathD.Sin(phi), 0.0, MathD.Cos(phi));
                ChartPoint p = WindowChart.Project(direction, DefaultWind(), ChartProjection.Equidistant);
                Assert.AreEqual(0.0, p.Y, 1e-12, $"phi={degrees} deg");
            }
        }

        [Test]
        public void PowerDependsOnlyOnRadius()
        {
            // Two directions at the same angular distance from the centre must have
            // the same effective wind, whatever their bearing.
            var wind = DefaultWind();
            double theta = 50.0 * MathD.Deg2Rad;

            Vec3 up = (Vec3.Forward * MathD.Cos(theta) + Vec3.Up * MathD.Sin(theta)).Normalized();
            Vec3 side = (Vec3.Forward * MathD.Cos(theta) + new Vec3(1, 0, 0) * MathD.Sin(theta)).Normalized();

            double windUp = WindWindow.EffectiveWindSpeed(wind, up);
            double windSide = WindWindow.EffectiveWindSpeed(wind, side);
            Assert.AreEqual(windUp, windSide, 1e-9);

            double radius = WindowChart.Project(up, wind, ChartProjection.Equidistant).Radius;
            double predicted = wind.Speed * WindowChart.PowerAtRadius(radius, ChartProjection.Equidistant);
            Assert.AreEqual(windUp, predicted, 1e-9);
        }

        [Test]
        public void EdgeRadius_HasZeroPower()
        {
            double edge = WindowChart.EdgeRadius(ChartProjection.Equidistant);
            Assert.AreEqual(MathD.PI * 0.5, edge, 1e-12);
            Assert.AreEqual(0.0, WindowChart.PowerAtRadius(edge, ChartProjection.Equidistant), 1e-12);
        }

        [Test]
        public void ProjectUnproject_RoundTrips_BothProjections()
        {
            var wind = DefaultWind();
            var projections = new[] { ChartProjection.Equidistant, ChartProjection.Stereographic };

            foreach (var projection in projections)
            {
                for (int beta = 0; beta <= 80; beta += 20)
                {
                    for (int phi = -80; phi <= 80; phi += 40)
                    {
                        double b = beta * MathD.Deg2Rad;
                        double f = phi * MathD.Deg2Rad;
                        Vec3 original = new Vec3(
                            MathD.Cos(b) * MathD.Sin(f),
                            MathD.Sin(b),
                            MathD.Cos(b) * MathD.Cos(f));

                        ChartPoint point = WindowChart.Project(original, wind, projection);
                        Vec3 restored = WindowChart.Unproject(point, wind, projection);

                        Assert.AreEqual(original.X, restored.X, 1e-9, $"{projection} beta={beta} phi={phi} X");
                        Assert.AreEqual(original.Y, restored.Y, 1e-9, $"{projection} beta={beta} phi={phi} Y");
                        Assert.AreEqual(original.Z, restored.Z, 1e-9, $"{projection} beta={beta} phi={phi} Z");
                    }
                }
            }
        }
    }
}

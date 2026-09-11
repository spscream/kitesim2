using NUnit.Framework;

namespace Kitesim.Core.Tests
{
    public class FigureGridTests
    {
        private static WindField Wind()
        {
            return new WindField { Speed = 6.0, Direction = Vec3.Forward };
        }

        [Test]
        public void MeridianSitsOnTheSphereAndKeepsItsAzimuth()
        {
            double azimuth = 45.0 * MathD.Deg2Rad;
            Vec3[] points = FigureGrid.Meridian(Wind(), azimuth, 45.0, 16);

            Assert.AreEqual(17, points.Length);
            Assert.AreEqual(0.0, points[0].Y, 1e-9, "starts on the horizon");
            Assert.Greater(points[points.Length - 1].Y, 44.0, "ends near the zenith");

            foreach (Vec3 p in points)
            {
                Assert.AreEqual(45.0, p.Length(), 1e-9);
                Assert.GreaterOrEqual(p.Y, -1e-9);
                if (FigureGrid.ElevationDeg(p) < 89.0)
                    Assert.AreEqual(45.0, FigureGrid.AzimuthDeg(Wind(), p), 1e-6);
            }
        }

        [Test]
        public void ElevationKeepsAConstantHeightAboveTheHorizon()
        {
            double elevation = 45.0 * MathD.Deg2Rad;
            Vec3[] points = FigureGrid.Elevation(Wind(), elevation, 45.0, 16);

            Assert.AreEqual(17, points.Length);
            foreach (Vec3 p in points)
            {
                Assert.AreEqual(45.0, p.Length(), 1e-9);
                Assert.AreEqual(45.0, FigureGrid.ElevationDeg(p), 1e-6);
            }
        }

        [Test]
        public void PublishedStepsAreTheFortyFiveDegreeBalletGrid()
        {
            CollectionAssert.AreEqual(
                new[] { -90.0, -45.0, 0.0, 45.0, 90.0 },
                FigureGrid.AzimuthStepsDeg);
            CollectionAssert.AreEqual(
                new[] { 45.0 },
                FigureGrid.ElevationStepsDeg);
        }
    }
}

using NUnit.Framework;

namespace Kitesim.Core.Tests
{
    public class KiteStateViewTests
    {
        private static KiteStateView Make(Vec3 direction, Vec3 heading)
        {
            return new KiteStateView(Vec3.Zero, direction, heading, 45.0, 20.0, 60.0, 0, false);
        }

        [Test]
        public void TangentUp_AtWindowCentre_IsWorldUp()
        {
            Vec3 up = KiteStateView.TangentUp(Vec3.Forward);
            Assert.AreEqual(0.0, up.X, 1e-12);
            Assert.AreEqual(1.0, up.Y, 1e-12);
            Assert.AreEqual(0.0, up.Z, 1e-12);
        }

        [Test]
        public void TangentRight_AtWindowCentre_IsPositiveX()
        {
            Vec3 right = KiteStateView.TangentRight(Vec3.Forward);
            Assert.AreEqual(1.0, right.X, 1e-12);
            Assert.AreEqual(0.0, right.Y, 1e-12);
            Assert.AreEqual(0.0, right.Z, 1e-12);
        }

        [Test]
        public void HeadingRad_TowardZenith_IsZero()
        {
            var view = Make(Vec3.Forward, Vec3.Up);
            Assert.AreEqual(0.0, view.HeadingRad, 1e-12);
        }

        [Test]
        public void HeadingRad_TowardPilotsRight_IsNinetyDegrees()
        {
            var view = Make(Vec3.Forward, new Vec3(1, 0, 0));
            Assert.AreEqual(MathD.PI * 0.5, view.HeadingRad, 1e-12);
        }

        [Test]
        public void HeadingRad_TowardGround_IsOneEightyDegrees()
        {
            var view = Make(Vec3.Forward, new Vec3(0, -1, 0));
            Assert.AreEqual(MathD.PI, MathD.Abs(view.HeadingRad), 1e-12);
        }

        [Test]
        public void ElevationAndAzimuth_MatchTheConstructedDirection()
        {
            double beta = 35.0 * MathD.Deg2Rad;
            double phi = -20.0 * MathD.Deg2Rad;
            Vec3 direction = new Vec3(
                MathD.Cos(beta) * MathD.Sin(phi),
                MathD.Sin(beta),
                MathD.Cos(beta) * MathD.Cos(phi));

            var view = Make(direction, KiteStateView.TangentUp(direction));

            Assert.AreEqual(beta, view.ElevationRad, 1e-12);
            Assert.AreEqual(phi, view.AzimuthRad, 1e-12);
        }

        [Test]
        public void WorldPosition_IsPilotPlusDirectionTimesLineLength()
        {
            var view = new KiteStateView(
                new Vec3(3.0, 0.0, 0.0), Vec3.Forward, Vec3.Up, 45.0, 20.0, 60.0, 0, false);

            Vec3 world = view.WorldPosition;
            Assert.AreEqual(3.0, world.X, 1e-12);
            Assert.AreEqual(0.0, world.Y, 1e-12);
            Assert.AreEqual(45.0, world.Z, 1e-12);
        }
    }
}

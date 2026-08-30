using NUnit.Framework;

namespace Kitesim.Core.Tests
{
    public class WindWindowTests
    {
        [Test]
        public void CenterOfWindow_HasMaximumEffectiveWind()
        {
            var wind = new WindField { Speed = 10.0, Direction = new Vec3(0, 0, 1) };
            var pos = new Vec3(0, 10, 43.87);
            double vEff = WindWindow.EffectiveWindSpeed(wind, pos);
            Assert.AreEqual(10.0, vEff, 0.5);
        }

        [Test]
        public void EdgeOfWindow_HasZeroEffectiveWind()
        {
            var wind = new WindField { Speed = 10.0, Direction = new Vec3(0, 0, 1) };
            var pos = new Vec3(45, 0, 0);
            double vEff = WindWindow.EffectiveWindSpeed(wind, pos);
            Assert.AreEqual(0.0, vEff, 0.1);
        }
    }
}

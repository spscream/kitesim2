using NUnit.Framework;

namespace Kitesim.Core.Tests
{
    public class PilotHandsTests
    {
        [Test]
        public void RestAxesSitAtTheDefaultStance()
        {
            RigidKiteSim sim = RigidKiteSim.HundredSixteen();
            PilotHands.Apply(sim, Vec3.Zero, 0.0, 0.0);
            Assert.AreEqual(-PilotHands.HalfWidth, sim.HandLeft.X, 1e-12);
            Assert.AreEqual(PilotHands.HalfWidth, sim.HandRight.X, 1e-12);
            Assert.AreEqual(0.0, sim.HandLeft.Y, 1e-12);
            Assert.AreEqual(0.0, sim.HandRight.Y, 1e-12);
        }

        [Test]
        public void FullRightPullDropsOnlyTheRightHandByTravel()
        {
            RigidKiteSim sim = RigidKiteSim.HundredSixteen();
            PilotHands.Apply(sim, Vec3.Zero, 0.0, 1.0);
            Assert.AreEqual(0.0, sim.HandLeft.Y, 1e-12);
            Assert.AreEqual(-PilotHands.Travel, sim.HandRight.Y, 1e-12);
        }
    }
}

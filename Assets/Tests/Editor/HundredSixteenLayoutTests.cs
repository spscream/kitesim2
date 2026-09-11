using NUnit.Framework;

namespace Kitesim.Core.Tests
{
    public class HundredSixteenLayoutTests
    {
        [Test]
        public void PublishedAnchorsSitOnTheSailAndTheThreePointBridleCloses()
        {
            Assert.AreEqual(2.60, HundredSixteenLayout.Span, 1e-12);
            Assert.AreEqual(0.350, HundredSixteenLayout.Mass, 1e-12);
            Assert.AreEqual(0.0, HundredSixteenLayout.UpperSpreaderRight.Y, 1e-12);
            Assert.AreEqual(
                HundredSixteenLayout.Height - HundredSixteenLayout.UpperApaFromNose,
                HundredSixteenLayout.UpperSpreaderRight.Z, 1e-12);

            Assert.IsTrue(BridleGeometry.TryTowpoint(
                HundredSixteenLayout.UpperSpreaderRight,
                HundredSixteenLayout.LowerSpreaderRight,
                HundredSixteenLayout.InnerBridle,
                HundredSixteenLayout.UpperOuthaul,
                HundredSixteenLayout.OuterOuthaul,
                HundredSixteenLayout.Inhaul,
                out Vec3 towpoint));

            Assert.Greater(towpoint.Y, 0.0, "the towpoint hangs in front of the sail");
        }
    }
}

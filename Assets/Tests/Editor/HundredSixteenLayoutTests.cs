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

        [Test]
        public void AssembledSpineEqualsSailHeightAndTheP200StubIsTheRemainder()
        {
            Assert.AreEqual(1.015, HundredSixteenLayout.Height, 1e-12);
            Assert.AreEqual(0.825, HundredSixteenLayout.Spine, 1e-12);
            Assert.AreEqual(0.190, HundredSixteenLayout.P200Stub, 1e-12);
            Assert.AreEqual(
                HundredSixteenLayout.Height,
                HundredSixteenLayout.Spine + HundredSixteenLayout.P200Stub, 1e-12);
        }

        [Test]
        public void TPieceSitsAtCenterCrossNotAtTheInnerBridle()
        {
            Assert.AreEqual(HundredSixteenLayout.CenterCross.X, HundredSixteenLayout.TPiece.X, 1e-12);
            Assert.AreEqual(HundredSixteenLayout.CenterCross.Y, HundredSixteenLayout.TPiece.Y, 1e-12);
            Assert.AreEqual(HundredSixteenLayout.CenterCross.Z, HundredSixteenLayout.TPiece.Z, 1e-12);
            Assert.AreEqual(0.597, HundredSixteenLayout.CrossFromNose, 1e-12);
            Assert.AreEqual(0.637, HundredSixteenLayout.InnerBridleFromNose, 1e-12);
            Assert.Greater(
                HundredSixteenLayout.TPiece.Z,
                HundredSixteenLayout.InnerBridle.Z,
                "the T-piece is 40 mm closer to the nose than the inhaul knot");
        }

        [Test]
        public void CarbonLinearDensityIsNitroStrongTwentyThreeGramsOnEightyOneCentimetres()
        {
            Assert.AreEqual(0.023, HundredSixteenLayout.NitroStrongMass, 1e-12);
            Assert.AreEqual(0.810, HundredSixteenLayout.NitroStrongLength, 1e-12);
            Assert.AreEqual(
                HundredSixteenLayout.NitroStrongMass / HundredSixteenLayout.NitroStrongLength,
                HundredSixteenLayout.CarbonMassPerMetre, 1e-12);
        }
    }
}

using NUnit.Framework;

namespace Kitesim.Core.Tests
{
    public class FrameInertiaTests
    {
        [Test]
        public void HundredSixteenMassIsThePublishedThreeHundredFiftyGrams()
        {
            FrameInertia inertia = FrameInertia.FromHundredSixteen();

            Assert.AreEqual(HundredSixteenLayout.Mass, inertia.Mass, 1e-12);
            Assert.Greater(inertia.SailMass, 0.0, "the rods must not eat the whole kite");
            Assert.AreEqual(
                inertia.Mass,
                inertia.RodMass + HundredSixteenLayout.TailWeight + inertia.SailMass,
                1e-12);
        }

        [Test]
        public void CenterOfMassSitsOnTheSailAndBetweenTailAndNose()
        {
            Vec3 com = FrameInertia.FromHundredSixteen().CenterOfMass;

            Assert.AreEqual(0.0, com.X, 1e-9, "left-right symmetry");
            Assert.AreEqual(0.0, com.Y, 1e-9, "everything is in the sail plane until billow is folded in");
            Assert.Greater(com.Z, HundredSixteenLayout.Tail.Z);
            Assert.Less(com.Z, HundredSixteenLayout.Nose.Z);
        }

        [Test]
        public void PitchInertiaIsMuchSmallerThanYawAndRollAndIsComputedNotTypedIn()
        {
            FrameInertia inertia = FrameInertia.FromHundredSixteen();

            Assert.Greater(inertia.Ixx, 0.0);
            Assert.Greater(inertia.Iyy, 0.0);
            Assert.Greater(inertia.Izz, 0.0);
            Assert.Less(inertia.Ixx, inertia.Iyy, "pitch about the span is the light axis");
            Assert.Less(inertia.Ixx, inertia.Izz, "roll about the spine sees the whole span");
            Assert.Greater(MathD.Abs(inertia.Ixx - 0.03), 1e-9, "must not be the Fury estimate typed in by hand");
            Assert.Greater(MathD.Abs(inertia.Izz - 0.21), 1e-9);
            Assert.Greater(MathD.Abs(inertia.Iyy - 0.24), 1e-9);
        }
    }
}

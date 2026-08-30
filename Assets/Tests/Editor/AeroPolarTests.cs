using NUnit.Framework;

namespace Kitesim.Core.Tests
{
    public class AeroPolarTests
    {
        [Test]
        public void ZeroAngleOfAttack_HasNearZeroLift()
        {
            var (cl, cd) = AeroPolar.Evaluate(0);
            Assert.AreEqual(0.0, cl, 0.01);
            Assert.AreEqual(AeroPolar.CD0, cd, 0.01);
        }

        [Test]
        public void StallBoundary_LiftPeaks()
        {
            var (cl, cd) = AeroPolar.Evaluate(AeroPolar.ALPHA_STALL);
            Assert.Greater(cl, 1.4);
            Assert.Greater(cd, 0.05);
        }

        [Test]
        public void PostStall_NormalToWind_DragHigh()
        {
            var (cl, cd) = AeroPolar.Evaluate(90.0 * MathD.Deg2Rad);
            Assert.AreEqual(0.0, cl, 0.01);
            Assert.Greater(cd, 1.8);
        }

        [Test]
        public void NegativeAngleOfAttack_ProducesNegativeLift()
        {
            var (cl, _) = AeroPolar.Evaluate(-15.0 * MathD.Deg2Rad);
            Assert.Less(cl, 0.0);
        }
    }
}

using NUnit.Framework;

namespace Kitesim.Core.Tests
{
    public class WindowChartTests
    {
        [Test]
        public void MathD_HasInverseTrigonometry()
        {
            Assert.AreEqual(1.0, MathD.Tan(MathD.PI * 0.25), 1e-12);
            Assert.AreEqual(MathD.PI * 0.25, MathD.Atan(1.0), 1e-12);
            Assert.AreEqual(MathD.PI * 0.5, MathD.Asin(1.0), 1e-12);
        }
    }
}

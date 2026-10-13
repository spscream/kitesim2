using NUnit.Framework;

namespace Kitesim.Core.Tests
{
    public class RigidKiteStateTests
    {
        [Test]
        public void StartsAtRestWithIdentityOrientation()
        {
            Vec3 position = new Vec3(0.0, 20.0, 40.0);
            RigidKiteState state = RigidKiteState.At(position);

            Assert.AreEqual(position.X, state.Position.X, 1e-12);
            Assert.AreEqual(position.Y, state.Position.Y, 1e-12);
            Assert.AreEqual(position.Z, state.Position.Z, 1e-12);
            Assert.AreEqual(0.0, state.Velocity.X, 1e-12);
            Assert.AreEqual(0.0, state.Velocity.Y, 1e-12);
            Assert.AreEqual(0.0, state.Velocity.Z, 1e-12);
            Assert.AreEqual(0.0, state.Omega.X, 1e-12);
            Assert.AreEqual(0.0, state.Omega.Y, 1e-12);
            Assert.AreEqual(0.0, state.Omega.Z, 1e-12);
            Assert.AreEqual(0.0, state.Orientation.X, 1e-12);
            Assert.AreEqual(0.0, state.Orientation.Y, 1e-12);
            Assert.AreEqual(0.0, state.Orientation.Z, 1e-12);
            Assert.AreEqual(1.0, state.Orientation.W, 1e-12);
        }
    }
}

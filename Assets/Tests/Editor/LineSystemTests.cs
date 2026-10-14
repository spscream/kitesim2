using NUnit.Framework;

namespace Kitesim.Core.Tests
{
    public class LineSystemTests
    {
        private static readonly double Dt = 0.002;

        [Test]
        public void SlackLineHasZeroTensionAndDoesNotPullTheBody()
        {
            FrameInertia inertia = FrameInertia.FromHundredSixteen();
            var state = RigidKiteState.At(new Vec3(0.0, 0.0, 40.0));
            Vec3 before = state.Position;
            var lines = new LineSystem();
            lines.Project(state, inertia,
                Vec3.Zero, Vec3.Zero, Vec3.Zero, Vec3.Zero, Dt);

            Assert.AreEqual(0.0, lines.TensionLeft);
            Assert.AreEqual(0.0, lines.TensionRight);
            Assert.AreEqual(before.X, state.Position.X, 1e-12);
            Assert.AreEqual(before.Y, state.Position.Y, 1e-12);
            Assert.AreEqual(before.Z, state.Position.Z, 1e-12);
        }

        [Test]
        public void SixtyNewtonsOfTensionStretchesAboutEightCentimetres()
        {
            FrameInertia inertia = FrameInertia.FromHundredSixteen();
            double stretch = 60.0 / LineSystem.Stiffness;
            var state = RigidKiteState.At(new Vec3(0.0, 0.0, LineSystem.RestLength + stretch));
            // Right hand sits on the body so that line is slack; only the left
            // constraint carries the load, otherwise two identical lines split it.
            Vec3 slackHand = state.Position;
            var lines = new LineSystem();
            lines.Project(state, inertia,
                Vec3.Zero, Vec3.Zero, slackHand, Vec3.Zero, Dt);

            double dist = (state.Position - Vec3.Zero).Length();
            Assert.AreEqual(0.0, lines.TensionRight, 1e-9);
            Assert.Greater(lines.TensionLeft, 50.0);
            Assert.Less(lines.TensionLeft, 70.0);
            Assert.AreEqual(LineSystem.RestLength + lines.TensionLeft / LineSystem.Stiffness,
                dist, 0.02);
        }
    }
}

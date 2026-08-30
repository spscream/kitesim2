using NUnit.Framework;

namespace Kitesim.Core.Tests
{
    public class TrackTests
    {
        private static KiteStateView View(Vec3 direction, Vec3 heading, double speed)
        {
            return new KiteStateView(Vec3.Zero, direction.Normalized(), heading.Normalized(),
                45.0, speed, 60.0, 0, false);
        }

        [Test]
        public void RecordsFramesPerPilot()
        {
            var track = new Track();
            track.Record(0.0, 0, View(Vec3.Forward, Vec3.Up, 20.0));
            track.Record(0.0, 1, View(Vec3.Forward, Vec3.Up, 25.0));
            track.Record(0.02, 0, View(Vec3.Forward, Vec3.Up, 21.0));

            Assert.AreEqual(3, track.Frames.Count);
            Assert.AreEqual(2, track.PilotCount);
            Assert.AreEqual(0.02, track.Duration, 1e-12);
        }

        [Test]
        public void SamplingAtARecordedTimeReturnsThatFrame()
        {
            var track = new Track();
            track.Record(0.0, 0, View(Vec3.Forward, Vec3.Up, 20.0));
            track.Record(0.02, 0, View(Vec3.Up, new Vec3(1, 0, 0), 30.0));

            var sample = track.Sample(0.02, 0, 45.0);

            Assert.AreEqual(0.0, sample.Direction.X, 1e-9);
            Assert.AreEqual(1.0, sample.Direction.Y, 1e-9);
            Assert.AreEqual(30.0, sample.Speed, 1e-9);
        }

        [Test]
        public void SamplingBetweenFramesInterpolates()
        {
            var track = new Track();
            track.Record(0.0, 0, View(Vec3.Forward, Vec3.Up, 10.0));
            track.Record(1.0, 0, View(Vec3.Forward, Vec3.Up, 30.0));

            var sample = track.Sample(0.5, 0, 45.0);

            Assert.AreEqual(20.0, sample.Speed, 1e-9);
            Assert.AreEqual(1.0, sample.Direction.Length(), 1e-9);
            Assert.AreEqual(1.0, sample.Heading.Length(), 1e-9);
        }

        [Test]
        public void SamplingOutsideTheRangeClampsToTheEnds()
        {
            var track = new Track();
            track.Record(1.0, 0, View(Vec3.Forward, Vec3.Up, 10.0));
            track.Record(2.0, 0, View(Vec3.Forward, Vec3.Up, 30.0));

            Assert.AreEqual(10.0, track.Sample(-5.0, 0, 45.0).Speed, 1e-9);
            Assert.AreEqual(30.0, track.Sample(99.0, 0, 45.0).Speed, 1e-9);
        }

        [Test]
        public void InterpolatedDirectionsStayUnitLength()
        {
            var track = new Track();
            track.Record(0.0, 0, View(Vec3.Forward, Vec3.Up, 20.0));
            track.Record(1.0, 0, View(Vec3.Up, new Vec3(1, 0, 0), 20.0));

            for (double t = 0.0; t <= 1.0; t += 0.1)
            {
                var sample = track.Sample(t, 0, 45.0);
                Assert.AreEqual(1.0, sample.Direction.Length(), 1e-9, $"t={t}");
                Assert.AreEqual(1.0, sample.Heading.Length(), 1e-9, $"t={t}");
            }
        }

        [Test]
        public void RecordAndReplayReproduceTheFlight()
        {
            var sim = new WindowSim(new WindowConfiguration())
            {
                Wind = new WindField { Speed = 6.0, Direction = Vec3.Forward }
            };
            // Start well above the horizon and keep the run short. From ground level
            // this steering lands the kite within about a third of a second, and the
            // round trip would then be matching a stationary point rather than a
            // flight. Even from 45 degrees the kite sags and lands at about 9.5 s —
            // correct behaviour, since gravity drags an unattended kite down — so the
            // run stops at 5 s, with roughly four and a half seconds of margin.
            double beta = 45.0 * MathD.Deg2Rad;
            Vec3 start = new Vec3(0.0, MathD.Sin(beta), MathD.Cos(beta));
            var state = new WindowKiteState
            {
                Direction = start,
                Heading = KiteStateView.TangentUp(start),
                Speed = 25.0,
                Landed = false
            };

            Vec3 firstDirection = state.Direction;
            var track = new Track();
            double time = 0.0;
            for (int i = 0; i < 250; i++)
            {
                double u = MathD.Sin(i * 0.02) * 0.7;
                sim.Step(state, 0.02, -u, u, 0.0);
                time += 0.02;
                track.Record(time, 0, sim.View(state));
            }

            Assert.IsFalse(state.Landed, "the round trip must be recorded from a kite that actually flew");
            Assert.Greater(
                MathD.Acos(MathD.Clamp(Vec3.Dot(firstDirection, state.Direction), -1.0, 1.0)),
                10.0 * MathD.Deg2Rad,
                "the kite must have crossed the window, not hovered in place");

            var last = track.Sample(time, 0, 45.0);
            Assert.AreEqual(state.Direction.X, last.Direction.X, 1e-9);
            Assert.AreEqual(state.Direction.Y, last.Direction.Y, 1e-9);
            Assert.AreEqual(state.Direction.Z, last.Direction.Z, 1e-9);
        }

        [Test]
        public void ClearEmptiesTheTrack()
        {
            var track = new Track();
            track.Record(0.0, 0, View(Vec3.Forward, Vec3.Up, 20.0));
            track.Clear();

            Assert.AreEqual(0, track.Frames.Count);
            Assert.AreEqual(0.0, track.Duration, 1e-12);
        }
    }
}

using System.Globalization;
using System.Threading;
using NUnit.Framework;
using Kitesim.Core;

namespace Kitesim.Core.Tests
{
    public class TrackCsvTests
    {
        private static Track TwoPilotsFlying()
        {
            var track = new Track();
            track.Record(0.0, 0, new KiteStateView(
                new Vec3(-0.3, 0.0, 0.0), new Vec3(0.0, 0.6, 0.8), Vec3.Up,
                45.0, 12.25, 84.5, 0.0, false));
            track.Record(0.0, 1, new KiteStateView(
                new Vec3(0.3, 0.0, -0.5), new Vec3(0.0, 0.0, 1.0), Vec3.Up,
                45.0, 0.0, 0.0, 0.0, true));
            track.Record(0.02, 0, new KiteStateView(
                new Vec3(-0.3, 0.0, 0.1), new Vec3(0.1, 0.6, 0.79), new Vec3(1.0, 0.0, 0.0),
                45.0, 13.0, 90.0, -2.5, false));
            return track;
        }

        [Test]
        public void EveryFieldOfEveryFrameSurvivesTheRoundTrip()
        {
            Track original = TwoPilotsFlying();

            Track restored = TrackCsv.Read(TrackCsv.Write(original));

            Assert.AreEqual(original.Frames.Count, restored.Frames.Count);
            for (int i = 0; i < original.Frames.Count; i++)
            {
                TrackFrame a = original.Frames[i];
                TrackFrame b = restored.Frames[i];
                Assert.AreEqual(a.Time, b.Time, 0.0, $"time of frame {i}");
                Assert.AreEqual(a.PilotIndex, b.PilotIndex, $"pilot of frame {i}");
                Assert.AreEqual(a.PilotPosition.X, b.PilotPosition.X, 0.0);
                Assert.AreEqual(a.PilotPosition.Y, b.PilotPosition.Y, 0.0);
                Assert.AreEqual(a.PilotPosition.Z, b.PilotPosition.Z, 0.0);
                Assert.AreEqual(a.Direction.X, b.Direction.X, 0.0);
                Assert.AreEqual(a.Direction.Y, b.Direction.Y, 0.0);
                Assert.AreEqual(a.Direction.Z, b.Direction.Z, 0.0);
                Assert.AreEqual(a.Heading.X, b.Heading.X, 0.0);
                Assert.AreEqual(a.Heading.Y, b.Heading.Y, 0.0);
                Assert.AreEqual(a.Heading.Z, b.Heading.Z, 0.0);
                Assert.AreEqual(a.Speed, b.Speed, 0.0);
                Assert.AreEqual(a.Tension, b.Tension, 0.0);
                Assert.AreEqual(a.LineTurnsRad, b.LineTurnsRad, 0.0, $"twist of frame {i}");
                Assert.AreEqual(a.Landed, b.Landed, $"landed of frame {i}");
            }
        }

        /// <summary>
        /// Whole line wraps are what a judge counts and the angle behind them is
        /// what the lines are drawn twisted by, so losing this column would silently
        /// ruin the very thing tracks are recorded for.
        /// </summary>
        [Test]
        public void ANegativeWrapCountSurvivesTheRoundTrip()
        {
            var track = new Track();
            track.Record(1.0, 0, new KiteStateView(
                Vec3.Zero, Vec3.Forward, Vec3.Up, 45.0, 5.0, 20.0, -3.0 * 2.0 * MathD.PI, false));

            Track restored = TrackCsv.Read(TrackCsv.Write(track));

            Assert.AreEqual(-3.0 * 2.0 * MathD.PI, restored.Frames[0].LineTurnsRad, 0.0);
        }

        /// <summary>
        /// The numbers are written round-trippable, not rounded: a track replayed
        /// from file must follow the same path as the one in memory.
        /// </summary>
        [Test]
        public void AwkwardDoublesComeBackBitForBit()
        {
            double awkward = 0.1 + 0.2;
            var track = new Track();
            track.Record(awkward, 0, new KiteStateView(
                new Vec3(1e-9, -1e12, awkward), new Vec3(0.0, 1.0, 0.0), Vec3.Forward,
                45.0, 1.0 / 3.0, double.Epsilon, 0.0, false));

            Track restored = TrackCsv.Read(TrackCsv.Write(track));

            Assert.AreEqual(awkward, restored.Frames[0].Time);
            Assert.AreEqual(1e-9, restored.Frames[0].PilotPosition.X);
            Assert.AreEqual(-1e12, restored.Frames[0].PilotPosition.Y);
            Assert.AreEqual(awkward, restored.Frames[0].PilotPosition.Z);
            Assert.AreEqual(1.0 / 3.0, restored.Frames[0].Speed);
            Assert.AreEqual(double.Epsilon, restored.Frames[0].Tension);
        }

        /// <summary>
        /// A locale that writes 0,5 for a half would turn every field into two
        /// fields. The owner's own machine is one such locale, so this is not a
        /// hypothetical.
        /// </summary>
        [Test]
        public void ACommaDecimalLocaleDoesNotSplitTheColumns()
        {
            CultureInfo saved = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

                Track restored = TrackCsv.Read(TrackCsv.Write(TwoPilotsFlying()));

                Assert.AreEqual(3, restored.Frames.Count);
                Assert.AreEqual(0.02, restored.Frames[2].Time, 0.0);
                Assert.AreEqual(0.6, restored.Frames[0].Direction.Y, 0.0);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = saved;
            }
        }

        [Test]
        public void WriteStartsWithAVersionLineThenANamedHeaderRow()
        {
            string csv = TrackCsv.Write(TwoPilotsFlying());
            string[] lines = csv.Split('\n');

            Assert.AreEqual("# kitesim-track 1", lines[0].TrimEnd('\r'));
            Assert.AreEqual("time,pilot,px,py,pz,dx,dy,dz,hx,hy,hz,speed,tension,turns,landed",
                lines[1].TrimEnd('\r'));
        }

        [Test]
        public void ALegacyFileWithoutAVersionLineStillLoads()
        {
            string legacy = "time,pilot,px,py,pz,dx,dy,dz,hx,hy,hz,speed,tension,turns,landed\n"
                + "0,0,0,0,0,0,0,1,0,1,0,10,20,0,0\n";

            TrackCsvReadResult result = TrackCsv.ReadInto(new Track(), legacy);

            Assert.AreEqual(0, result.Version);
            Assert.AreEqual(1, result.Loaded);
            Assert.AreEqual(0, result.Skipped);
        }

        [Test]
        public void ReadReportsHowManyBrokenLinesItSkipped()
        {
            string csv = TrackCsv.Write(TwoPilotsFlying()) + "0.04,0,1.0,2.0\nnot-a-frame\n";

            TrackCsvReadResult result = TrackCsv.ReadInto(new Track(), csv);

            Assert.AreEqual(1, result.Version);
            Assert.AreEqual(3, result.Loaded);
            Assert.AreEqual(2, result.Skipped);
        }

        [Test]
        public void ReadSkipsBlankLinesAndTolerantOfCarriageReturns()
        {
            string csv = TrackCsv.Write(TwoPilotsFlying()).Replace("\n", "\r\n") + "\r\n\r\n";

            Track restored = TrackCsv.Read(csv);

            Assert.AreEqual(3, restored.Frames.Count);
        }

        [Test]
        public void ReadOfAnEmptyStringGivesAnEmptyTrack()
        {
            Assert.AreEqual(0, TrackCsv.Read(string.Empty).Frames.Count);
            Assert.AreEqual(0, TrackCsv.Read(null).Frames.Count);
        }

        /// <summary>
        /// A truncated last line is what a crash mid-recording leaves behind. The
        /// frames before it are still a usable routine, so they are kept.
        /// </summary>
        [Test]
        public void ATruncatedTrailingLineIsDroppedAndTheRestIsKept()
        {
            string csv = TrackCsv.Write(TwoPilotsFlying()) + "0.04,0,1.0,2.0";

            Track restored = TrackCsv.Read(csv);

            Assert.AreEqual(3, restored.Frames.Count);
        }

        [Test]
        public void ReadIntoAnExistingTrackReplacesWhatWasThere()
        {
            var target = new Track();
            target.Record(9.0, 0, new KiteStateView(
                Vec3.Zero, Vec3.Forward, Vec3.Up, 45.0, 1.0, 1.0, 0.0, false));

            TrackCsv.ReadInto(target, TrackCsv.Write(TwoPilotsFlying()));

            Assert.AreEqual(3, target.Frames.Count);
            Assert.AreEqual(0.0, target.Frames[0].Time, 0.0);
        }
    }
}

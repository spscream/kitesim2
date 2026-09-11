using System.Collections.Generic;

namespace Kitesim.Core
{
    /// <summary>
    /// One recorded sample of one kite.
    ///
    /// Direction and Heading are stored as unit vectors, which is the
    /// singularity-free encoding of the (phi, beta, psi) triple. PilotPosition is
    /// recorded per frame because the pilot walks, and it is recorded in world
    /// space so that a track made in the 2D chart can be replayed in 3D with the
    /// pilots at their real spacing.
    /// </summary>
    public readonly struct TrackFrame
    {
        public readonly double Time;
        public readonly int PilotIndex;
        public readonly Vec3 PilotPosition;
        public readonly Vec3 Direction;
        public readonly Vec3 Heading;
        public readonly double Speed;
        public readonly double Tension;
        public readonly double LineTurnsRad;
        public readonly bool Landed;

        public TrackFrame(double time, int pilotIndex, Vec3 pilotPosition, Vec3 direction,
            Vec3 heading, double speed, double tension, double lineTurnsRad, bool landed)
        {
            Time = time;
            PilotIndex = pilotIndex;
            PilotPosition = pilotPosition;
            Direction = direction;
            Heading = heading;
            Speed = speed;
            Tension = tension;
            LineTurnsRad = lineTurnsRad;
            Landed = landed;
        }
    }

    /// <summary>
    /// In-memory recording of a flight, sampled per pilot. Playback interpolates
    /// between frames; unit vectors are lerped and renormalised, which at the 50 Hz
    /// sample rate is well inside the error of the model itself.
    ///
    /// File I/O deliberately lives outside Kitesim.Core.
    /// </summary>
    public class Track
    {
        private readonly List<TrackFrame> frames = new List<TrackFrame>();
        private readonly List<List<int>> framesByPilot = new List<List<int>>();

        public IReadOnlyList<TrackFrame> Frames => frames;

        public double Duration => frames.Count == 0 ? 0.0 : frames[frames.Count - 1].Time;

        public int PilotCount
        {
            get
            {
                int highest = -1;
                for (int i = 0; i < frames.Count; i++)
                {
                    if (frames[i].PilotIndex > highest)
                        highest = frames[i].PilotIndex;
                }
                return highest + 1;
            }
        }

        public void Record(double time, int pilotIndex, KiteStateView view)
        {
            frames.Add(new TrackFrame(
                time,
                pilotIndex,
                view.PilotPosition,
                view.Direction,
                view.Heading,
                view.Speed,
                view.Tension,
                view.LineTurnsRad,
                view.Landed));
            IndexOf(pilotIndex).Add(frames.Count - 1);
        }

        public void Clear()
        {
            frames.Clear();
            framesByPilot.Clear();
        }

        /// <summary>
        /// Interpolated view of one pilot's kite at the given time. Direction and
        /// Heading are lerped and re-orthonormalised; Speed, Tension and the twist of
        /// the lines are lerped straight. Landed is the one discrete quantity here and
        /// does not lerp sensibly, so it uses nearest-neighbour: whichever recorded
        /// frame, previous or next, is closer in time to the sample point.
        /// </summary>
        public KiteStateView Sample(double time, int pilotIndex, double lineLength)
        {
            TrackFrame? previous = null;
            TrackFrame? next = null;
            List<int> index = IndexOf(pilotIndex);

            if (index.Count == 0)
                return new KiteStateView(Vec3.Zero, Vec3.Forward, Vec3.Up, lineLength, 0.0, 0.0, 0, true);

            int lo = 0;
            int hi = index.Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                double midTime = frames[index[mid]].Time;
                if (midTime <= time)
                    lo = mid + 1;
                else
                    hi = mid - 1;
            }

            if (hi >= 0)
                previous = frames[index[hi]];
            if (lo < index.Count)
                next = frames[index[lo]];

            if (previous == null && next == null)
                return new KiteStateView(Vec3.Zero, Vec3.Forward, Vec3.Up, lineLength, 0.0, 0.0, 0, true);

            if (previous == null)
                return ToView(next!.Value, lineLength);

            if (next == null)
                return ToView(previous.Value, lineLength);

            TrackFrame a = previous.Value;
            TrackFrame b = next.Value;
            double span = b.Time - a.Time;
            double blend = span < 1e-12 ? 0.0 : MathD.Clamp01((time - a.Time) / span);

            Vec3 direction = Lerp(a.Direction, b.Direction, blend).Normalized();
            Vec3 heading = Lerp(a.Heading, b.Heading, blend);
            heading = heading - direction * Vec3.Dot(heading, direction);
            heading = heading.SqrLength < 1e-12
                ? KiteStateView.TangentUp(direction)
                : heading.Normalized();

            return new KiteStateView(
                Lerp(a.PilotPosition, b.PilotPosition, blend),
                direction,
                heading,
                lineLength,
                MathD.Lerp(a.Speed, b.Speed, blend),
                MathD.Lerp(a.Tension, b.Tension, blend),
                MathD.Lerp(a.LineTurnsRad, b.LineTurnsRad, blend),
                blend < 0.5 ? a.Landed : b.Landed);
        }

        private static KiteStateView ToView(TrackFrame frame, double lineLength)
        {
            return new KiteStateView(
                frame.PilotPosition,
                frame.Direction,
                frame.Heading,
                lineLength,
                frame.Speed,
                frame.Tension,
                frame.LineTurnsRad,
                frame.Landed);
        }

        private List<int> IndexOf(int pilotIndex)
        {
            while (framesByPilot.Count <= pilotIndex)
                framesByPilot.Add(new List<int>());
            return framesByPilot[pilotIndex];
        }

        private static Vec3 Lerp(Vec3 a, Vec3 b, double t)
        {
            return a + (b - a) * t;
        }
    }
}

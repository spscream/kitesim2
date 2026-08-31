using System.Globalization;
using System.Text;

namespace Kitesim.Core
{
    /// <summary>
    /// A track as CSV text, so a routine can be inspected, diffed and mailed as a
    /// plain file.
    ///
    /// This lives in the core rather than in the Unity layer, unlike the file
    /// handle around it: the format is logic, and logic here is under test. The
    /// core stays free of UnityEngine, not of the base class library.
    ///
    /// Two decisions that a hand-written format gets wrong. Numbers are written
    /// with "R" and the invariant culture: "R" round-trips a double bit for bit, so
    /// a replayed track follows the recorded path exactly rather than approximately,
    /// and the invariant culture keeps a comma-decimal locale from writing 0,5 and
    /// splitting one column into two. The twist of the lines is a column of its
    /// own: whole wraps are what a judge counts, the lines are drawn twisted by the
    /// angle behind them, and a format that dropped it would look correct in every
    /// other respect.
    /// </summary>
    public static class TrackCsv
    {
        public const string Header = "time,pilot,px,py,pz,dx,dy,dz,hx,hy,hz,speed,tension,turns,landed";

        private const int ColumnCount = 15;

        public static string Write(Track track)
        {
            var text = new StringBuilder();
            text.Append(Header).Append('\n');

            if (track == null)
                return text.ToString();

            var culture = CultureInfo.InvariantCulture;
            foreach (TrackFrame f in track.Frames)
            {
                text.Append(f.Time.ToString("R", culture)).Append(',');
                text.Append(f.PilotIndex.ToString(culture)).Append(',');
                Append(text, f.PilotPosition, culture);
                Append(text, f.Direction, culture);
                Append(text, f.Heading, culture);
                text.Append(f.Speed.ToString("R", culture)).Append(',');
                text.Append(f.Tension.ToString("R", culture)).Append(',');
                text.Append(f.LineTurnsRad.ToString("R", culture)).Append(',');
                text.Append(f.Landed ? '1' : '0').Append('\n');
            }

            return text.ToString();
        }

        public static Track Read(string csv)
        {
            var track = new Track();
            ReadInto(track, csv);
            return track;
        }

        /// <summary>
        /// Parse into an existing track, replacing its contents. Frames that do not
        /// parse are skipped rather than throwing: a recording cut short by a crash
        /// ends in a truncated line, and the routine before that line is still worth
        /// replaying.
        /// </summary>
        public static void ReadInto(Track track, string csv)
        {
            if (track == null)
                return;

            track.Clear();
            if (string.IsNullOrEmpty(csv))
                return;

            var culture = CultureInfo.InvariantCulture;
            string[] lines = csv.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("time,"))
                    continue;

                string[] c = line.Split(',');
                if (c.Length < ColumnCount)
                    continue;

                if (!double.TryParse(c[0], NumberStyles.Float, culture, out double time)) continue;
                if (!int.TryParse(c[1], NumberStyles.Integer, culture, out int pilot)) continue;
                if (!TryVec(c, 2, culture, out Vec3 pilotPosition)) continue;
                if (!TryVec(c, 5, culture, out Vec3 direction)) continue;
                if (!TryVec(c, 8, culture, out Vec3 heading)) continue;
                if (!double.TryParse(c[11], NumberStyles.Float, culture, out double speed)) continue;
                if (!double.TryParse(c[12], NumberStyles.Float, culture, out double tension)) continue;
                if (!double.TryParse(c[13], NumberStyles.Float, culture, out double turns)) continue;

                track.Record(time, pilot, new KiteStateView(
                    pilotPosition,
                    direction,
                    heading,
                    // The line length is a property of the kite being flown, not of
                    // the recording, and the player supplies it when sampling.
                    0.0,
                    speed,
                    tension,
                    turns,
                    c[14] == "1"));
            }
        }

        private static void Append(StringBuilder text, Vec3 v, CultureInfo culture)
        {
            text.Append(v.X.ToString("R", culture)).Append(',');
            text.Append(v.Y.ToString("R", culture)).Append(',');
            text.Append(v.Z.ToString("R", culture)).Append(',');
        }

        private static bool TryVec(string[] c, int first, CultureInfo culture, out Vec3 v)
        {
            v = Vec3.Zero;
            if (!double.TryParse(c[first], NumberStyles.Float, culture, out double x)) return false;
            if (!double.TryParse(c[first + 1], NumberStyles.Float, culture, out double y)) return false;
            if (!double.TryParse(c[first + 2], NumberStyles.Float, culture, out double z)) return false;
            v = new Vec3(x, y, z);
            return true;
        }
    }
}

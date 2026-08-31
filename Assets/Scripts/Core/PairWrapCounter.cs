using System.Collections.Generic;

namespace Kitesim.Core
{
    /// <summary>
    /// How many times two kites have wound their lines around each other.
    ///
    /// This is the quantity a team plans a ballet around: two or three wraps between
    /// a pair is normal, more is a problem, and control degrades as they pile up
    /// (see docs/kite-anatomy.md section 6). It is a different number from a kite's
    /// own line wraps, which come from the kite rolling about its own line axis.
    ///
    /// **What is measured.** Both pilots stand within a metre of each other while
    /// their kites fly on 45 m lines, so the two line pairs leave what is very nearly
    /// one point. Two threads from one anchor wind around each other exactly as often
    /// as the vector between their far ends turns about them: with unit directions
    /// dA and dB, that vector is s = dA - dB, and one full turn of s is one wrap. Not
    /// a proxy for the tangle -- the winding number is the tangle.
    ///
    /// **Why crossings are not counted instead.** A kite that crosses over its
    /// partner and returns the way it came leaves the lines clear; one that crosses
    /// over and returns underneath leaves a wrap. Both are two crossings, so counting
    /// crossings cannot tell them apart. Winding can, and does.
    ///
    /// **The axis.** Turns of s are measured about a fixed axis, the downwind window
    /// centre, rather than about the pair's own axis. That keeps the count free of
    /// the slow drift a moving frame accumulates, and it is exact rather than
    /// approximate: s is parallel to the window axis only when the two kites are in
    /// the same place, which is the one case this cannot count anyway. Anywhere else
    /// the winding number about the fixed axis is the same integer.
    /// </summary>
    public class PairWrapCounter
    {
        private const double Tiny = 1e-9;

        private readonly Vec3 axis;
        private readonly Vec3 reference;
        private readonly Vec3 crossReference;

        private double previousBearing;
        private bool tracking;

        /// <param name="windowAxis">
        /// The downwind window centre, from WindowChart.Center. Only its direction is
        /// used, and the count does not depend on the choice.
        /// </param>
        public PairWrapCounter(Vec3 windowAxis)
        {
            axis = windowAxis.SqrLength < Tiny ? Vec3.Forward : windowAxis.Normalized();
            reference = KiteStateView.TangentUp(axis);
            crossReference = Vec3.Cross(axis, reference).Normalized();
        }

        /// <summary>Total turning of the pair, radians. Signed: the sign says which way to unwind.</summary>
        public double AccumulatedAngleRad { get; private set; }

        /// <summary>
        /// Whole wraps, truncated toward zero, as a kite's own line wraps are: half a
        /// turn is not yet a wrap.
        /// </summary>
        public int Wraps => (int)(AccumulatedAngleRad / (2.0 * MathD.PI));

        /// <summary>Feed one frame of both kites. Call at the frame rate of the view.</summary>
        public void Update(KiteStateView a, KiteStateView b)
        {
            // A kite on the ground has its lines lying on the field, not wound around
            // anything, and its partner can fly circles above it without winding a
            // thing. Landing also breaks the thread of the measurement, so tracking
            // restarts from wherever the pair is when both are flying again.
            if (a.Landed || b.Landed)
            {
                tracking = false;
                return;
            }

            Vec3 separation = a.Direction - b.Direction;
            double x = Vec3.Dot(separation, reference);
            double y = Vec3.Dot(separation, crossReference);
            if (x * x + y * y < Tiny)
            {
                // The kites are in the same place: there is no direction between them
                // to have turned. Nothing is added, and nothing is reset either.
                return;
            }

            double bearing = MathD.Atan2(y, x);

            if (tracking)
                AccumulatedAngleRad += WrapPi(bearing - previousBearing);

            previousBearing = bearing;
            tracking = true;
        }

        public void Reset()
        {
            AccumulatedAngleRad = 0.0;
            tracking = false;
        }

        /// <summary>
        /// The shortest way round between two bearings. Steps larger than half a turn
        /// between frames are read as the short way, which at any sane frame rate is
        /// the true one: a pair would have to swap sides in a single frame to fool it.
        /// </summary>
        private static double WrapPi(double angle)
        {
            while (angle > MathD.PI)
                angle -= 2.0 * MathD.PI;
            while (angle < -MathD.PI)
                angle += 2.0 * MathD.PI;
            return angle;
        }
    }

    /// <summary>
    /// One PairWrapCounter for every pair of kites in the team, which is what the
    /// fliers actually watch: the ballet is planned so that no pair runs out of
    /// wraps, and the pair that is worst off is the one that ends the routine.
    /// </summary>
    public class TeamWrapCounter
    {
        private readonly int pilotCount;
        private readonly PairWrapCounter[] pairs;

        public TeamWrapCounter(int pilotCount, Vec3 windowAxis)
        {
            this.pilotCount = pilotCount < 0 ? 0 : pilotCount;
            pairs = new PairWrapCounter[this.pilotCount * this.pilotCount];

            for (int i = 0; i < this.pilotCount; i++)
            {
                for (int j = i + 1; j < this.pilotCount; j++)
                    pairs[i * this.pilotCount + j] = new PairWrapCounter(windowAxis);
            }
        }

        public int PilotCount => pilotCount;

        /// <summary>
        /// Feed one frame of every kite, indexed by pilot. A shorter list is taken as
        /// "those pilots are not flying yet" rather than as an error: the scene builds
        /// its pilots one at a time.
        /// </summary>
        public void Update(IReadOnlyList<KiteStateView> views)
        {
            if (views == null)
                return;

            int count = views.Count < pilotCount ? views.Count : pilotCount;
            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                    pairs[i * pilotCount + j].Update(views[i], views[j]);
            }
        }

        public int Wraps(int i, int j) => Pair(i, j)?.Wraps ?? 0;

        public double AngleRad(int i, int j) => Pair(i, j)?.AccumulatedAngleRad ?? 0.0;

        /// <summary>The most wound pair, by magnitude. Zero when there is no pair yet.</summary>
        public int WorstWraps
        {
            get
            {
                int worst = 0;
                for (int i = 0; i < pilotCount; i++)
                {
                    for (int j = i + 1; j < pilotCount; j++)
                    {
                        int wraps = System.Math.Abs(pairs[i * pilotCount + j].Wraps);
                        if (wraps > worst)
                            worst = wraps;
                    }
                }
                return worst;
            }
        }

        public void Reset()
        {
            for (int i = 0; i < pilotCount; i++)
            {
                for (int j = i + 1; j < pilotCount; j++)
                    pairs[i * pilotCount + j].Reset();
            }
        }

        private PairWrapCounter Pair(int i, int j)
        {
            if (i == j || i < 0 || j < 0 || i >= pilotCount || j >= pilotCount)
                return null;

            return i < j ? pairs[i * pilotCount + j] : pairs[j * pilotCount + i];
        }
    }
}

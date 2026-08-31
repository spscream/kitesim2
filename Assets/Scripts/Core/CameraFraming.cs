namespace Kitesim.Core
{
    /// <summary>Where the judge stands and what they are looking at.</summary>
    public readonly struct CameraStance
    {
        public readonly Vec3 Position;

        /// <summary>Unit view direction.</summary>
        public readonly Vec3 Forward;

        public CameraStance(Vec3 position, Vec3 forward)
        {
            Position = position;
            Forward = forward;
        }
    }

    /// <summary>
    /// Keeps every kite in the judge's frame, by turning the head first and only
    /// stepping back when no head angle is enough.
    ///
    /// A static camera at a real judging position cannot cover the window: standing
    /// 15 m behind the pilots, the window edge 45 m out to the side lies 72 degrees
    /// off the view axis. Measured on the built scene, a kite climbing the meridian
    /// leaves the top of a fixed frame within a couple of seconds. A judge follows
    /// it with their head, which is what Aim does. Stepping back is the fallback for
    /// the case the owner described -- kites running to opposite ends of the window
    /// at once -- and it is rare, because there is little wind at the edges and the
    /// flying happens nearer the middle.
    ///
    /// Pure geometry; the smoothing that makes this look like a neck rather than a
    /// servo lives in the Unity component. See
    /// docs/superpowers/specs/2026-08-31-judge-view-design.md.
    /// </summary>
    public static class CameraFraming
    {
        private const int BackOffIterations = 48;

        /// <summary>
        /// The stance that frames every target: aim at their centroid within the
        /// limits of a neck, stepping straight upwind by as little as possible.
        /// </summary>
        /// <param name="restPosition">Where the judge stands when nothing forces them back.</param>
        /// <param name="restForward">Unit horizontal direction they face at rest, downwind.</param>
        /// <param name="targets">Kite positions in world space. Empty leaves the rest stance.</param>
        /// <param name="maxYawRad">How far the head turns sideways.</param>
        /// <param name="maxPitchRad">How far the head tilts up or down.</param>
        /// <param name="verticalFovRad">Camera vertical field of view.</param>
        /// <param name="aspect">Viewport width divided by height.</param>
        /// <param name="margin">
        /// Fraction of each half-field kept clear. Without it the camera hunts back
        /// and forth while a kite balances on the frame edge: with it, the judge
        /// retreats before the kite truly reaches the border and returns only once it
        /// is well inside.
        /// </param>
        /// <param name="maxBackOff">Furthest the judge may retreat.</param>
        public static CameraStance Frame(
            Vec3 restPosition, Vec3 restForward, Vec3[] targets,
            double maxYawRad, double maxPitchRad,
            double verticalFovRad, double aspect, double margin, double maxBackOff)
        {
            // The retreat runs along the ground, so the step direction is the
            // horizontal bearing; the resting gaze itself may be tilted, and is
            // returned untouched when there is nothing to follow.
            Vec3 flat = new Vec3(restForward.X, 0.0, restForward.Z);
            Vec3 forward = flat.SqrLength < 1e-12 ? Vec3.Forward : flat.Normalized();
            if (targets == null || targets.Length == 0)
                return new CameraStance(restPosition, restForward.Normalized());

            Vec3 centroid = Centroid(targets);
            double halfVertical = verticalFovRad * 0.5 * (1.0 - margin);
            double halfHorizontal = MathD.Atan(MathD.Tan(verticalFovRad * 0.5) * aspect) * (1.0 - margin);

            if (Fits(0.0))
                return StanceAt(0.0);
            if (!Fits(maxBackOff))
                return StanceAt(maxBackOff);

            // The spread shrinks as the judge retreats, so the smallest distance that
            // works can be bisected for. Retreating never makes a framed target leave
            // the frame, which is what lets this be a bisection rather than a search.
            double lo = 0.0;
            double hi = maxBackOff;
            for (int i = 0; i < BackOffIterations; i++)
            {
                double mid = 0.5 * (lo + hi);
                if (Fits(mid)) hi = mid; else lo = mid;
            }
            return StanceAt(hi);

            CameraStance StanceAt(double back)
            {
                Vec3 eye = restPosition - forward * back;
                return new CameraStance(eye, Aim(eye, forward, centroid, maxYawRad, maxPitchRad));
            }

            bool Fits(double back)
            {
                CameraStance stance = StanceAt(back);
                foreach (Vec3 target in targets)
                {
                    if (!InFrame(stance, target, halfHorizontal, halfVertical))
                        return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Unit direction from the eye toward the point, turned back to whatever a
        /// neck allows. The limits are the ones the base design gives for the M5
        /// first-person camera, so the two views move the same way instead of each
        /// inventing its own idea of a head.
        ///
        /// Only the horizontal part of restForward is used, and that is deliberate.
        /// The judge's resting gaze is tilted up into the window, and an earlier
        /// version took that tilted direction as its reference and then added the
        /// computed pitch on top of it: a kite at the zenith came out at 74.25
        /// degrees against a 70 degree neck limit. Horizontalising here means no
        /// caller can make that mistake -- the reference is a compass bearing, not a
        /// gaze.
        /// </summary>
        public static Vec3 Aim(Vec3 eye, Vec3 restForward, Vec3 target, double maxYawRad, double maxPitchRad)
        {
            Vec3 flat = new Vec3(restForward.X, 0.0, restForward.Z);
            Vec3 forward = flat.SqrLength < 1e-12 ? Vec3.Forward : flat.Normalized();
            Vec3 right = Vec3.Cross(Vec3.Up, forward).Normalized();

            Vec3 toTarget = target - eye;
            if (toTarget.SqrLength < 1e-12)
                return forward;
            toTarget = toTarget.Normalized();

            Vec3 horizontal = new Vec3(toTarget.X, 0.0, toTarget.Z);
            double yaw = horizontal.SqrLength < 1e-12
                ? 0.0
                : MathD.Atan2(Vec3.Dot(horizontal, right), Vec3.Dot(horizontal, forward));
            double pitch = MathD.Asin(MathD.Clamp(toTarget.Y, -1.0, 1.0));

            yaw = MathD.Clamp(yaw, -maxYawRad, maxYawRad);
            pitch = MathD.Clamp(pitch, -maxPitchRad, maxPitchRad);

            Vec3 turned = forward * MathD.Cos(yaw) + right * MathD.Sin(yaw);
            return (turned * MathD.Cos(pitch) + Vec3.Up * MathD.Sin(pitch)).Normalized();
        }

        private static Vec3 Centroid(Vec3[] points)
        {
            Vec3 sum = Vec3.Zero;
            foreach (Vec3 p in points)
                sum = sum + p;
            return sum / points.Length;
        }

        private static bool InFrame(
            CameraStance stance, Vec3 target, double halfHorizontal, double halfVertical)
        {
            Vec3 right = Vec3.Cross(Vec3.Up, stance.Forward).Normalized();
            Vec3 up = Vec3.Cross(stance.Forward, right).Normalized();

            Vec3 v = target - stance.Position;
            double along = Vec3.Dot(v, stance.Forward);
            if (along <= 0.0)
                return false;

            return MathD.Abs(MathD.Atan2(Vec3.Dot(v, right), along)) <= halfHorizontal
                   && MathD.Abs(MathD.Atan2(Vec3.Dot(v, up), along)) <= halfVertical;
        }
    }
}

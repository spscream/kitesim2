using NUnit.Framework;

namespace Kitesim.Core.Tests
{
    /// <summary>
    /// Framing for the judge's camera: where to look, and how far to step back when
    /// looking is not enough. Pure geometry, so it is tested here rather than being
    /// stranded inside a MonoBehaviour.
    /// See docs/superpowers/specs/2026-08-31-judge-view-design.md.
    /// </summary>
    public class CameraFramingTests
    {
        private static readonly Vec3 Eye = new Vec3(0.0, 1.7, -15.0);
        private static readonly Vec3 Forward = Vec3.Forward;

        private const double Fov = 70.0 * MathD.Deg2Rad;
        private const double Aspect = 1280.0 / 720.0;
        private const double Margin = 0.1;
        private const double MaxYaw = 80.0 * MathD.Deg2Rad;
        private const double MaxPitch = 70.0 * MathD.Deg2Rad;
        private const double MaxBackOff = 120.0;

        private static CameraStance Frame(params Vec3[] targets)
        {
            return CameraFraming.Frame(
                Eye, Forward, targets, MaxYaw, MaxPitch, Fov, Aspect, Margin, MaxBackOff);
        }

        /// <summary>A point on the flying sphere, in the same convention as the sim.</summary>
        private static Vec3 Kite(double elevationDeg, double azimuthDeg, double radius = 45.0)
        {
            double beta = elevationDeg * MathD.Deg2Rad;
            double phi = azimuthDeg * MathD.Deg2Rad;
            return new Vec3(
                MathD.Cos(beta) * MathD.Sin(phi),
                MathD.Sin(beta),
                MathD.Cos(beta) * MathD.Cos(phi)) * radius;
        }

        // ------------------------------------------------------------------- aim

        [Test]
        public void WithNoKites_TheJudgeStandsStillAndLooksDownwind()
        {
            CameraStance stance = Frame(new Vec3[0]);

            Assert.AreEqual(0.0, (stance.Position - Eye).Length(), 1e-12);
            Assert.AreEqual(1.0, Vec3.Dot(stance.Forward, Forward), 1e-12);
        }

        [Test]
        public void TheJudgeLooksAtTheKite()
        {
            Vec3 target = Kite(30.0, 0.0);
            CameraStance stance = Frame(target);

            Vec3 toKite = (target - stance.Position).Normalized();
            Assert.AreEqual(1.0, Vec3.Dot(stance.Forward, toKite), 1e-12,
                "with one kite the head points straight at it");
        }

        [Test]
        public void AKiteHighOverhead_RaisesTheHeadInsteadOfLosingIt()
        {
            // The owner's case: the kite climbs the meridian and leaves the top of a
            // fixed frame. A judge raises their head.
            Vec3 target = Kite(65.0, 0.0);
            CameraStance stance = Frame(target);

            double pitch = MathD.Asin(MathD.Clamp(stance.Forward.Y, -1.0, 1.0)) * MathD.Rad2Deg;
            Assert.Greater(pitch, 40.0, "the head must actually tilt up to follow a high kite");
            Assert.That(Framed(stance, target), Is.True);
        }

        [Test]
        public void TheHeadStopsAtTheLimitsOfANeck()
        {
            // Straight up is past what a neck does; the limits come from the base
            // design section 7, so the judge's view and the M5 first-person view
            // move the same way rather than each inventing its own.
            CameraStance up = Frame(new Vec3(0.0, 45.0, 0.0));
            double pitch = MathD.Asin(MathD.Clamp(up.Forward.Y, -1.0, 1.0));
            Assert.LessOrEqual(pitch, MaxPitch + 1e-9);
            Assert.AreEqual(MaxPitch * MathD.Rad2Deg, pitch * MathD.Rad2Deg, 1e-6);

            CameraStance side = Frame(Kite(0.0, 175.0));
            Vec3 horizontal = new Vec3(side.Forward.X, 0.0, side.Forward.Z).Normalized();
            Vec3 right = Vec3.Cross(Vec3.Up, Forward).Normalized();
            double yaw = MathD.Abs(MathD.Atan2(Vec3.Dot(horizontal, right), Vec3.Dot(horizontal, Forward)));
            Assert.AreEqual(MaxYaw * MathD.Rad2Deg, yaw * MathD.Rad2Deg, 1e-6);
        }

        [Test]
        public void ARestingGazeThatIsAlreadyTiltedDoesNotTiltTheAimTwice()
        {
            // Caught by measurement on the live scene, not by reading the code: the
            // judge's resting gaze is tilted up into the window, and passing that
            // tilted direction in as the reference made the aim add the rest tilt on
            // top of the pitch it had just computed. A kite at the zenith came out at
            // 74.25 degrees against a 70 degree neck limit -- past the limit the
            // clamp exists to enforce, which is what gave it away.
            //
            // Only the horizontal part of the reference is a reference; the rest is
            // where the head happens to be pointing.
            Vec3 tilted = new Vec3(0.0, MathD.Sin(26.0 * MathD.Deg2Rad), MathD.Cos(26.0 * MathD.Deg2Rad));
            Vec3 zenith = new Vec3(0.0, 45.0, 0.0);

            Vec3 fromHorizontal = CameraFraming.Aim(Eye, Forward, zenith, MaxYaw, MaxPitch);
            Vec3 fromTilted = CameraFraming.Aim(Eye, tilted, zenith, MaxYaw, MaxPitch);

            Assert.AreEqual(1.0, Vec3.Dot(fromHorizontal, fromTilted), 1e-12,
                "the reference's own tilt must not reach the result");
            Assert.AreEqual(MaxPitch * MathD.Rad2Deg,
                MathD.Asin(MathD.Clamp(fromTilted.Y, -1.0, 1.0)) * MathD.Rad2Deg, 1e-9,
                "and the neck limit must still hold");
        }

        // -------------------------------------------------------------- back-off

        [Test]
        public void OneKiteNeverMakesTheJudgeStepBack()
        {
            // Turning the head is always enough for a single target, so the fallback
            // must stay out of the way in the ordinary case.
            foreach (double elevation in new[] { 5.0, 25.0, 45.0, 60.0 })
            {
                foreach (double azimuth in new[] { -60.0, -20.0, 0.0, 20.0, 60.0 })
                {
                    CameraStance stance = Frame(Kite(elevation, azimuth));
                    Assert.AreEqual(0.0, (stance.Position - Eye).Length(), 1e-9,
                        $"elevation {elevation}, azimuth {azimuth}: a lone kite is framed by looking");
                }
            }
        }

        [Test]
        public void TwoKitesAtOppositeExtremes_MakeTheJudgeStepBack()
        {
            // The rare case the owner described: kites running to opposite ends of
            // the window at once. No head angle frames both, so the judge backs away.
            Vec3 left = Kite(10.0, -75.0);
            Vec3 right = Kite(55.0, 75.0);

            CameraStance stance = Frame(left, right);

            double stepped = (stance.Position - Eye).Length();
            Assert.Greater(stepped, 1.0, "the judge must actually retreat to fit both");
            Assert.That(Framed(stance, left), Is.True, "left kite must be in frame");
            Assert.That(Framed(stance, right), Is.True, "right kite must be in frame");
        }

        [Test]
        public void TheJudgeStepsBackAlongTheWindAndNoFurtherThanAllowed()
        {
            Vec3 left = Kite(5.0, -89.0);
            Vec3 right = Kite(5.0, 89.0);

            CameraStance stance = Frame(left, right);
            Vec3 stepped = stance.Position - Eye;

            Assert.LessOrEqual(stepped.Length(), MaxBackOff + 1e-9);
            Assert.AreEqual(0.0, stepped.X, 1e-9, "the retreat is straight upwind");
            Assert.AreEqual(0.0, stepped.Y, 1e-9, "the judge does not levitate");
            Assert.Less(stepped.Z, 0.0, "upwind is the negative downwind direction");
        }

        [Test]
        public void TheJudgeStepsBackOnlyAsFarAsNeeded()
        {
            // A wider pair needs more room than a narrower one; the retreat is not a
            // fixed jump to the back of the field.
            double near = (Frame(Kite(20.0, -45.0), Kite(20.0, 45.0)).Position - Eye).Length();
            double far = (Frame(Kite(20.0, -80.0), Kite(20.0, 80.0)).Position - Eye).Length();

            Assert.Greater(far, near, "a wider spread costs more ground");
        }

        [Test]
        public void EverythingStaysFramedWithTheMarginKeptClear()
        {
            // The margin is what stops the camera hunting back and forth while a kite
            // sits on the frame edge: it is pushed back before the kite truly touches
            // the border, and comes back only once the kite is well inside.
            Vec3 left = Kite(15.0, -70.0);
            Vec3 right = Kite(50.0, 65.0);
            CameraStance stance = Frame(left, right);

            foreach (Vec3 target in new[] { left, right })
            {
                var (horizontal, vertical) = Offsets(stance, target);
                double halfVertical = Fov * 0.5 * (1.0 - Margin);
                double halfHorizontal = MathD.Atan(MathD.Tan(Fov * 0.5) * Aspect) * (1.0 - Margin);

                Assert.LessOrEqual(MathD.Abs(vertical), halfVertical + 1e-9);
                Assert.LessOrEqual(MathD.Abs(horizontal), halfHorizontal + 1e-9);
            }
        }

        [Test]
        public void AnUnframeableSpread_RetreatsAsFarAsAllowedRatherThanGivingUp()
        {
            // When the retreat is capped short of what the spread needs, the result
            // must still be a sane stance at the limit rather than a wild value or a
            // failure. Measured while writing this: kites at the two horizontal
            // extremes plus one high overhead ARE framable from 30 m back, which is
            // why this test caps the retreat at 5 m to reach the case it is about.
            const double shortRetreat = 5.0;
            CameraStance stance = CameraFraming.Frame(
                Eye, Forward, new[] { Kite(2.0, -90.0), Kite(2.0, 90.0), Kite(80.0, 0.0) },
                MaxYaw, MaxPitch, Fov, Aspect, Margin, shortRetreat);

            Assert.AreEqual(shortRetreat, (stance.Position - Eye).Length(), 1e-9);
            Assert.AreEqual(1.0, stance.Forward.Length(), 1e-9, "the aim stays a unit vector");
        }

        [Test]
        public void TheWholeWindowIsFramableByRetreating()
        {
            // Worth knowing, because it sets the cap: kites at both horizontal
            // extremes at once are framed from about 30 m back, not from the far side
            // of the field. The owner's instinct that this is a rare, cheap fallback
            // holds.
            CameraStance stance = Frame(Kite(2.0, -90.0), Kite(2.0, 90.0), Kite(80.0, 0.0));

            double stepped = (stance.Position - Eye).Length();
            Assert.That(stepped, Is.InRange(20.0, 45.0));
        }

        // ------------------------------------------------------------------ util

        private static (double horizontal, double vertical) Offsets(CameraStance stance, Vec3 target)
        {
            Vec3 right = Vec3.Cross(Vec3.Up, stance.Forward).Normalized();
            Vec3 up = Vec3.Cross(stance.Forward, right).Normalized();
            Vec3 v = target - stance.Position;

            double along = Vec3.Dot(v, stance.Forward);
            return (MathD.Atan2(Vec3.Dot(v, right), along), MathD.Atan2(Vec3.Dot(v, up), along));
        }

        private static bool Framed(CameraStance stance, Vec3 target)
        {
            var (horizontal, vertical) = Offsets(stance, target);
            double halfVertical = Fov * 0.5 * (1.0 - Margin);
            double halfHorizontal = MathD.Atan(MathD.Tan(Fov * 0.5) * Aspect) * (1.0 - Margin);
            return MathD.Abs(vertical) <= halfVertical + 1e-9
                   && MathD.Abs(horizontal) <= halfHorizontal + 1e-9;
        }
    }
}

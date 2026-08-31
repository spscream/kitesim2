namespace Kitesim.Core
{
    /// <summary>
    /// Tunables for the reduced wind-window model. Every value that can be derived
    /// is derived; only genuinely free parameters live here.
    /// </summary>
    public class WindowConfiguration
    {
        public double LineLength = 45.0;
        public double SailArea = 0.75;         // m^2
        public double AirDensity = 1.225;      // kg/m^3

        /// <summary>Trim angle of attack set by the bridle. 8 degrees.</summary>
        public double TrimAngleOfAttack = 8.0 * MathD.Deg2Rad;

        /// <summary>
        /// Kite aspect ratio (span^2 / area), used to correct AeroPolar's infinite-
        /// span thin-airfoil coefficients down to a finite wing (see GlideRatio).
        ///
        /// Measured, not guessed: Assets/Models/Fury_v01.blend was measured headless
        /// in Blender 4.2. The mesh is a delta whose planform, projected onto its own
        /// span-by-spine plane, has an area of 2.728 in mesh units at a span of
        /// 4.5999, giving span^2 / area = 7.76. Aspect ratio is a shape ratio, so the
        /// mesh's absolute scale does not enter -- which matters, because the mesh is
        /// exactly 2x life size (span 4.5999 for a kite of 2.30 m, spine 2.0996 for
        /// 1.05 m; a real Fury is about 2.36 by 1.07).
        ///
        /// This contradicts docs/kite-anatomy.md section 1, which called the config's
        /// implied AR of 8.4 "impossible for a delta, sport kites run roughly 3-5".
        /// That claim was marked [вывод] -- reasoned here, not sourced -- and it is
        /// the thing that was wrong. Three independent numbers agree on a high aspect
        /// ratio and none supports 3-5: this mesh (7.76), the config's own span and
        /// area (2.51 m, 0.75 m^2 -> 8.4), and the doc's own cited Cottrell figures
        /// (leading edge 1.37-1.68 m at a 90-120 degree nose angle gives a span of
        /// roughly 2.2-2.9 m over 0.84-1.4 m^2 -> 4.3-7.2). The reason is the deep
        /// cut-out trailing edge, which the mesh reproduces: the sail covers only
        /// about two thirds of the nose-to-tip triangle, so the mean chord, and with
        /// it the aspect ratio, is nothing like a full triangle's.
        ///
        /// The 7-vertex mesh is an artist's proxy, so treat this as good to about
        /// 10 percent, not to three digits. The doc has been corrected to match.
        /// </summary>
        public double AspectRatio = 7.76;

        /// <summary>
        /// Oswald efficiency factor for the induced-drag term (see GlideRatio).
        /// 0.7 is a standard textbook figure for a low-aspect-ratio planform of
        /// this kind; not specific to this kite.
        /// </summary>
        public double OswaldEfficiency = 0.7;

        /// <summary>Diameter of one control line, metres. ~0.9 mm for the training pair.</summary>
        public double LineDiameter = 0.0009;

        /// <summary>
        /// Cross-flow drag coefficient of a control line, treated as a cylinder.
        /// ~1.0 is standard for a smooth cylinder at the Reynolds numbers a flying
        /// line sees (order 10^3, subcritical regime).
        /// </summary>
        public double LineDragCoefficient = 1.0;

        /// <summary>
        /// Drag of bridle, frame, standoffs and sail billow, which nothing above
        /// derives. This is the one genuinely free number left in the glide-ratio
        /// computation, and it is the knob to turn if the kite feels wrong in the
        /// air: everything else is either measured or textbook. At 0.04 it is about
        /// 27 percent of total drag, which is unremarkable for a framed delta on 45 m
        /// of line; at 0.0 the glide ratio would be 6.05, at 0.08 it would be 3.44.
        ///
        /// 0.04 is where three anchors come closest to agreeing, none of them tight:
        ///  - docs/kite-anatomy.md section 4 estimates a kite-speed-to-wind-speed
        ///    ratio of 2.5-4 from "60-80 km/h in medium wind" -- and that ratio is
        ///    exactly this glide ratio. Marked [оценка] there. Puts E at 3.3-4.4 if
        ///    medium wind means 5 m/s.
        ///  - The owner's reported edge-to-edge pass of 5-7 s. Simulated rather than
        ///    divided out (the kite is slow near both edges), a held-elevation pass
        ///    obeys E * t = 43.5 at 5 m/s, 36.0 at 6 m/s and 27.1 at 8 m/s for a
        ///    +-80 degree sweep, and 31.2 at 5 m/s for a +-70 degree one. Two unknowns
        ///    ride on that report -- what wind, and how much of the window "edge to
        ///    edge" covers -- so it constrains E only to roughly 4-6.
        ///  - The speed ceiling. At E = 4.39 the kite tops out at 197 km/h at the
        ///    masters wind ceiling of 12.50 m/s, just over the 193 km/h Guinness kite
        ///    record. That is a soft check, not a violation: it is the theoretical
        ///    crosswind maximum at the window centre in a wind nobody flies a delta
        ///    in, while the record is a real speed on a purpose-built speed foil.
        ///    The old E = 6.04 gave 272 km/h there, which is not defensible.
        /// </summary>
        public double ResidualDrag = 0.04;

        /// <summary>
        /// Coefficient of the weight term in TurnRate, m/s^2. In the closed form
        /// c * cos(beta) * sin(psi) / v this was originally hard-coded to g, on the
        /// assumption of an unpowered point mass constrained to a sphere. A real
        /// kite is not unpowered: part of its lift lies in the tangent plane and
        /// works against the weight, and its bridle trim resists the nose dropping.
        /// The literature this law comes from (Erhard &amp; Strauch; Fechner et al.,
        /// see docs/kite-anatomy.md section 5) treats this constant as fitted per
        /// kite, and it can be negative -- a kite with a negative constant drifts
        /// toward the zenith rather than the ground.
        ///
        /// The quantity that decides this value is the ratio of the weight turn rate
        /// to the turn rate of full steering deflection. Steering goes as v and the
        /// weight term as 1/v, so their ratio goes as c / (SteeringGain * v^2): the
        /// pilot is strongest at the powered window centre and weakest wherever the
        /// kite is slow. Above 1.0 the pilot cannot win. Measured at E = 4.39:
        ///
        ///                centre           theta=60         window edge
        ///   c        1.1   5.0   8.0    1.1   5.0   8.0    any wind    m/s
        ///   9.81    0.83  0.04  0.02   3.32  0.16  0.06      4.90
        ///   3.00    0.25  0.01  0.00   1.02  0.05  0.02      1.50
        ///   1.00    0.08  0.00  0.00   0.34  0.02  0.01      0.50
        ///
        /// This is what produced both symptoms the owner reported, and it also
        /// corrects an earlier diagnosis of mine: the weight term is NOT eating the
        /// pilot's authority everywhere. In 5-8 m/s at the window centre, holding a
        /// level line costs 0.11 of full deflection on average even at c = g. The
        /// damage is concentrated exactly where the owner felt it -- light wind, and
        /// the window edge, where Speed collapses to the power floor and MinAirspeed
        /// takes over the denominator (which is why the edge column does not depend
        /// on wind speed at all). At c = g the edge is a 5:1 trap the pilot can never
        /// steer out of; c must be at most 2.0 for the pilot to break even there, and
        /// 1.0 leaves a 2:1 margin. Hands-off droop over 60 degrees of azimuth from
        /// 45 degrees elevation at 8 m/s corroborates it: 39.1 degrees lost at 9.81,
        /// 20.0 at 1.0, against 18.4 of irreducible great-circle descent at 0.0 (a
        /// straight line on a sphere descends away from its apex; that residual is
        /// geometry, not a defect, and is not the target).
        /// </summary>
        public double WeightTurnCoefficient = 1.0;

        /// <summary>
        /// Along-track gravity in the speed law, m/s^2. See the comment at its use in
        /// WindowSim.Step: this one is derived and equals g exactly, because the
        /// tether is radial and does no work along the heading, so there is no free
        /// constant here the way there is in WeightTurnCoefficient.
        ///
        /// It is a field rather than a constant only so tests that want to isolate
        /// the aerodynamic speed relaxation can switch it off. Setting it to zero
        /// restores the speed-is-a-function-of-position behaviour the model had
        /// before, in which height cannot be traded for speed at all.
        /// </summary>
        public double GravityAlongHeading = 9.80665;

        /// <summary>Turn rate per unit steering input per unit apparent wind, 1/m.</summary>
        public double SteeringGain = 0.5;

        /// <summary>Time constant of the speed relaxation, seconds.</summary>
        public double SpeedLag = 0.4;

        /// <summary>
        /// Fraction of the wind speed that remains available at the zenith and at
        /// the edge of the window. The cos(theta) law gives exactly zero there,
        /// while a real kite still flies a little; without this floor every visit
        /// to the zenith ends in a fall. See docs/kite-anatomy.md section 3.
        /// </summary>
        public double PowerFloor = 0.05;

        /// <summary>
        /// Airspeed floor for the gravity turn term, m/s. Below this the "kite
        /// flies along its heading" assumption has broken down anyway.
        /// </summary>
        public double MinAirspeed = 2.0;

        /// <summary>Half the travel of one hand, metres.</summary>
        public double HandTravel = 0.7;

        /// <summary>
        /// Steering help that makes up for the controller, not for the pilot. On by
        /// the owner's decision: the flier's own hands hold 0.7% of full travel and a
        /// thumb on a sprung stick holds about 5%, and that difference is worth about
        /// five degrees at the exit of a turn -- which is the "92 or 87 instead of
        /// 90" a pilot reports on a pad. See TurnAssist, where the numbers are
        /// derived; the stated risk is that the line between making up for the device
        /// and making up for the pilot cannot be checked from inside the simulator.
        /// </summary>
        public bool TurnAssist = true;

        /// <summary>
        /// The two grids, chosen by what the hands did rather than by where the kite
        /// happens to be. From the flier: a right angle is both hands hard in
        /// opposite directions, a diagonal is one hand, and everything else is flown
        /// smoothly and wants no help at all.
        /// </summary>
        public double TurnAssistCornerRad = 90.0 * MathD.Deg2Rad;
        public double TurnAssistDiagonalRad = 45.0 * MathD.Deg2Rad;

        /// <summary>A hand counts as working past this fraction of its travel.</summary>
        public double TurnAssistHardHand = 0.6;

        /// <summary>And as resting below this one.</summary>
        public double TurnAssistRestingHand = 0.25;

        /// <summary>
        /// How far from a node the help reaches, radians.
        ///
        /// Thirty degrees, and the number comes from a recorded flight rather than
        /// from taste. Over 41 turns flown as squares, the pilot let go a median of
        /// 21 degrees from the nearest right angle -- and that is at the true release,
        /// not after the hands had caught up. At 300 degrees a second nobody can time
        /// a right angle by eye; they let go when it looks about right. A ten degree
        /// window caught nine of those turns, which is why nothing seemed to work.
        ///
        /// This is wider than "making up for the controller" and is worth saying
        /// plainly: inside this window the help ends the corner for the pilot. The
        /// owner's frame is what makes that legitimate -- this is a tool for learning
        /// and remembering a routine, not a contest between joysticks -- and the help
        /// switches off with one key.
        /// </summary>
        public double TurnAssistCaptureRad = 30.0 * MathD.Deg2Rad;

        /// <summary>
        /// Two meanings, and they are the same number on purpose: the input below
        /// which the pilot counts as having let go, and the hardest the help may
        /// steer while it finishes. It can therefore never turn the kite harder than
        /// the pilot was turning it a moment ago, and at 12 m/s it closes the last
        /// few degrees in well under a tenth of a second.
        /// </summary>
        public double TurnAssistFinishInput = 0.35;

        /// <summary>
        /// The turn rate below which there was no turn worth finishing, rad/s. A kite
        /// drifting across a node is left alone.
        /// </summary>
        public double TurnAssistTurnRate = 1.0;

        /// <summary>
        /// How close to the node the help eases off, radians. Full authority outside
        /// this band, proportional inside it, so the turn stops on the node instead
        /// of ringing around it.
        ///
        /// Narrow on purpose. Spread the easing across the whole capture window and
        /// the help fades faster than the drift it is fighting -- the weight term and
        /// the turning of the tangent frame as the kite crosses the sky -- and the
        /// two balance a degree out. Measured: at the capture window it settled 1.1
        /// degrees short, at three degrees it arrives.
        /// </summary>
        public double TurnAssistFinishBandRad = 3.0 * MathD.Deg2Rad;

        /// <summary>
        /// Close enough to the node to call it arrived and let go. Not tighter than
        /// one step of the finish is worth -- at flying speed that is over a degree
        /// -- or the help rings around the node instead of landing on it, which is
        /// what a 0.3 degree band did.
        /// </summary>
        public double TurnAssistArrivedRad = 0.8 * MathD.Deg2Rad;

        /// <summary>
        /// How long the help keeps the turn in mind after it stops, seconds. This
        /// model has no rotational inertia: centre the hands and the turn ends in the
        /// same step, so without a memory the help loses all authority at the one
        /// moment the last few degrees are decided. Long enough to finish a turn,
        /// short enough that it is gone before the next figure.
        /// </summary>
        public double TurnAssistMemory = 0.6;

        public double WalkSpeedBack = 1.7;     // m/s
        public double WalkSpeedForward = 1.2;  // m/s

        /// <summary>Half the side of the outer competition boundary, metres.</summary>
        public double FieldHalfSize = 45.0;

        /// <summary>
        /// Finite-wing lift coefficient and total drag coefficient at the trim
        /// angle of attack, feeding both GlideRatio and ForceCoefficient.
        ///
        /// AeroPolar.Evaluate is thin-airfoil theory for a wing of infinite span
        /// (cl = 2*pi*sin(alpha)); a real kite has a finite one, and the previous
        /// glide ratio ignored that entirely. Two corrections, both textbook
        /// lifting-line results, at AspectRatio 7.76 and OswaldEfficiency 0.7:
        ///
        /// 1. Lift is reduced by the finite-wing lift-curve-slope ratio
        ///    a / a0 = 1 / (1 + a0 / (pi * e * AR)), a0 = 2*pi. That ratio is 0.731,
        ///    taking cl from 0.8744 to 0.6391 at the 8-degree trim -- lift was
        ///    overstated by about 37%.
        /// 2. Induced drag, cl^2 / (pi * e * AR), which AeroPolar does not include
        ///    at all: 0.0239, comparable to AeroPolar's entire profile drag of
        ///    0.0548 at this angle -- not a correction, a major term that was simply
        ///    missing.
        ///
        /// On top of that: tether drag from the two control lines, modelled as
        /// cylinders in cross-flow, referenced to the kite's dynamic pressure with
        /// the standard one-quarter factor for a line whose local apparent wind
        /// speed runs from zero at the pilot's hands to full at the kite
        /// (Cd_tether = 0.25 * LineDragCoefficient * 2 * LineDiameter * LineLength
        /// / SailArea) -- 0.0270 at the default 45 m lines, and the one term here
        /// that grows with line length. And ResidualDrag for bridle, frame,
        /// standoffs and billow (see its own comment).
        ///
        /// Totals at the defaults: cl = 0.6391, cd = 0.0548 (profile) + 0.0239
        /// (induced) + 0.0270 (tether) + 0.0400 (residual) = 0.1457, GlideRatio =
        /// 4.386. Kite speed across the competition wind envelope
        /// (docs/kite-anatomy.md section 7): 17.5 km/h at the masters minimum
        /// (1.11 m/s), 78.9 km/h at 5 m/s, 131.5 km/h at the novice ceiling
        /// (8.33 m/s), 197.4 km/h at the masters ceiling (12.50 m/s).
        /// </summary>
        private (double cl, double cd) FiniteWingAero()
        {
            var (clInfinite, cdProfile) = AeroPolar.Evaluate(TrimAngleOfAttack);

            double liftSlopeRatio = 1.0 / (1.0 + (2.0 * MathD.PI) / (MathD.PI * OswaldEfficiency * AspectRatio));
            double cl = clInfinite * liftSlopeRatio;

            double cdInduced = (cl * cl) / (MathD.PI * OswaldEfficiency * AspectRatio);
            double cdTether = 0.25 * LineDragCoefficient * 2.0 * LineDiameter * LineLength / SailArea;

            double cd = cdProfile + cdInduced + cdTether + ResidualDrag;
            return (cl, cd);
        }

        /// <summary>
        /// Lift-to-drag ratio of the finite-wing kite plus tether. This is what
        /// sets the kite's crosswind speed (targetSpeed = GlideRatio * effective
        /// wind in WindowSim.Step). See FiniteWingAero for the derivation.
        /// </summary>
        public double GlideRatio
        {
            get
            {
                var (cl, cd) = FiniteWingAero();
                return cl / cd;
            }
        }

        /// <summary>Resultant force coefficient, used for the line tension readout.</summary>
        public double ForceCoefficient
        {
            get
            {
                var (cl, cd) = FiniteWingAero();
                return MathD.Sqrt(cl * cl + cd * cd);
            }
        }
    }
}

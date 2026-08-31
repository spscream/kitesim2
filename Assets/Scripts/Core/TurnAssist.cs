namespace Kitesim.Core
{
    /// <summary>
    /// Finishes a turn on the grid, because on a controller the pilot cannot.
    ///
    /// The defect is not where I first put it. A hand holds its position far more
    /// finely than a thumb on a sprung stick, but that is not what loses the right
    /// angle: at 12 m/s a hard turn runs at about 340 degrees a second, so one frame
    /// of hesitation is seven degrees and the fifty to eighty milliseconds of a human
    /// release is twenty. The pilot's aim is fine. Their timing cannot be.
    ///
    /// So this does not steer. It decides **when to stop steering**: when the pilot
    /// lets go near a node of the figure grid, the turn is carried the last few
    /// degrees to that node and stopped there. Nothing is added to a turn the pilot
    /// is still flying -- hold the stick through ninety on the way to a hundred and
    /// eighty and nothing happens, which is what protects every figure that passes
    /// through a node on its way somewhere else.
    ///
    /// Three bounds, all tested:
    /// - it acts only after the pilot has let go, so it can never begin a turn;
    /// - it acts only within a capture window of a node, so a pilot aiming somewhere
    ///   off the grid is left alone;
    /// - it steers no harder than the input the pilot has just released below, so it
    ///   finishes a turn and never flies one.
    ///
    /// The grid is measured in the tangent plane of the window, where the heading
    /// already lives: zero toward the zenith, ninety to the pilot's right. That is
    /// the frame the figures are flown and judged in.
    ///
    /// This is a rehearsal tool, not a contest between joysticks (owner, 2026-08-31),
    /// and on the field the same movements are learned on sticks with toy kites. The
    /// help is on by default and switches off with one key.
    /// </summary>
    public static class TurnAssist
    {
        /// <summary>
        /// The steering to fly with, given what the pilot is asking for. Updates the
        /// kite's assist target as a side effect: the node it is currently finishing
        /// on, if any.
        /// </summary>
        /// <param name="pilotSteering">The pilot's own input, -1 to 1, right positive.</param>
        /// <param name="headingRad">Heading in the tangent plane, from the same frame as KiteStateView.</param>
        public static double Steering(
            WindowKiteState state, double pilotSteering, double headingRad, WindowConfiguration config)
        {
            if (state == null || config == null || !config.TurnAssist)
            {
                if (state != null)
                    state.AssistTarget = double.NaN;
                return pilotSteering;
            }

            // While the pilot is still turning, they are in charge and this does
            // nothing at all. That is what lets a turn run through a node.
            if (MathD.Abs(pilotSteering) > config.TurnAssistFinishInput)
            {
                state.AssistTarget = double.NaN;
                return pilotSteering;
            }

            if (double.IsNaN(state.AssistTarget))
            {
                // Only a turn that was actually being flown can be finished.
                if (state.RecentTurnRate < config.TurnAssistTurnRate)
                    return pilotSteering;

                double toNode = ErrorToNearestNode(headingRad, config.TurnAssistGridRad);
                if (MathD.Abs(toNode) > config.TurnAssistCaptureRad)
                    return pilotSteering;

                state.AssistTarget = headingRad + toNode;
            }

            double error = WrapPi(state.AssistTarget - headingRad);
            if (MathD.Abs(error) <= config.TurnAssistArrivedRad)
            {
                state.AssistTarget = double.NaN;

                // The turn is spent. Without this the help would latch onto the same
                // node again as the kite drifts off it, holding the heading for as
                // long as the memory lasted -- and holding a heading is not flying
                // straight: in the tangent frame of the window a straight leg does
                // not keep a constant heading angle. One turn, one finish.
                state.RecentTurnRate = 0.0;
                return pilotSteering;
            }

            // Full authority until the last few degrees, then proportional so the
            // turn stops on the node rather than ringing around it -- and capped at
            // the input the pilot has already let go below.
            double wanted = error / MathD.Max(config.TurnAssistFinishBandRad, 1e-6);
            double finish = MathD.Clamp(wanted, -1.0, 1.0) * config.TurnAssistFinishInput;

            return MathD.Clamp(pilotSteering + finish, -1.0, 1.0);
        }

        /// <summary>
        /// Signed angle from the heading to the nearest node of the grid, wrapped
        /// into half a turn either way. Positive means the node is to the pilot's
        /// right of where the kite is pointing.
        /// </summary>
        public static double ErrorToNearestNode(double headingRad, double gridRad)
        {
            if (gridRad <= 0.0)
                return 0.0;

            double nodes = headingRad / gridRad;
            double nearest = MathD.Round(nodes) * gridRad;
            return WrapPi(nearest - headingRad);
        }

        private static double WrapPi(double angle)
        {
            while (angle > MathD.PI)
                angle -= 2.0 * MathD.PI;
            while (angle < -MathD.PI)
                angle += 2.0 * MathD.PI;
            return angle;
        }
    }
}

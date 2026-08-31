namespace Kitesim.Core
{
    /// <summary>
    /// Makes up for the controller, not for the pilot.
    ///
    /// A flier holds the handles with their hands: 0.7 m of travel, held to about
    /// five millimetres, which is 0.7% of full input, with the pull in the lines
    /// telling them where they are. On a gamepad the same command is a thumb on a
    /// sprung stick: about 5% of full travel, with no way to hold a small offset at
    /// all, since the spring takes it back. The difference is 0.05 of input, and
    /// under the turn law that is 0.3 rad/s at 12 m/s -- about five degrees over the
    /// exit of a turn. That is exactly the "92 or 87 instead of 90" a flier reports
    /// on the pad, so the number is not a knob: it is the size of the defect.
    ///
    /// Hence the rule this class obeys: **it may never add more input than the
    /// difference between a stick and a hand.** Inside that budget it eases the
    /// heading onto the nearest node of the figure grid as a turn ends.
    ///
    /// Two properties keep it honest, and both are tested. It is gated by the turn
    /// the pilot is already flying, so with no input it does nothing at all -- it
    /// cannot turn the kite, only tidy a turn already being made. And it is capped,
    /// so a pilot who is ten degrees out stays ten degrees out; nobody is flattered
    /// into precision they do not have.
    ///
    /// The grid is measured in the tangent plane of the window, where the heading
    /// already lives: zero toward the zenith, ninety to the pilot's right. That is
    /// the frame a judge reads straight lines and right angles in.
    /// </summary>
    public static class TurnAssist
    {
        /// <summary>
        /// Extra steering to add, in the same units as the pilot's own: the hand
        /// difference over full travel, positive toward the pilot's right.
        /// </summary>
        /// <param name="headingRad">Heading in the tangent plane, from KiteStateView.</param>
        /// <param name="turnRateRad">
        /// The turn the pilot is already flying, before any help. This gates the
        /// whole thing: no turn, no assistance.
        /// </param>
        public static double Added(double headingRad, double turnRateRad, WindowConfiguration config)
        {
            if (config == null || !config.TurnAssist)
                return 0.0;

            double error = ErrorToNearestNode(headingRad, config.TurnAssistGridRad);
            if (MathD.Abs(error) > config.TurnAssistCaptureRad)
                return 0.0;

            // How much of a turn is going on, as a fraction. Nothing is added to a
            // kite flying straight, and the help fades in and out with the turn
            // rather than switching.
            double gate = MathD.Clamp01(MathD.Abs(turnRateRad) / config.TurnAssistTurnRate);
            if (gate <= 0.0)
                return 0.0;

            double wanted = error / config.TurnAssistCaptureRad;
            double added = MathD.Clamp(wanted, -1.0, 1.0) * config.TurnAssistMaxInput * gate;

            return added;
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

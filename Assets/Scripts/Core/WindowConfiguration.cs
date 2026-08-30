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
        /// Drag of lines, bridle and frame, which the sail polar does not include.
        /// Calibrated so the kite reaches 25-35 m/s at the window centre in 5 m/s
        /// wind, matching the 50-80 km/h commonly quoted for sport kites.
        /// </summary>
        public double ParasiticDrag = 0.09;

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

        public double WalkSpeedBack = 1.7;     // m/s
        public double WalkSpeedForward = 1.2;  // m/s

        /// <summary>Half the side of the outer competition boundary, metres.</summary>
        public double FieldHalfSize = 45.0;

        /// <summary>
        /// Lift-to-drag ratio, derived from the sail polar plus parasitic drag.
        /// This is what sets the kite's crosswind speed.
        /// </summary>
        public double GlideRatio
        {
            get
            {
                var (cl, cd) = AeroPolar.Evaluate(TrimAngleOfAttack);
                return cl / (cd + ParasiticDrag);
            }
        }

        /// <summary>Resultant force coefficient, used for the line tension readout.</summary>
        public double ForceCoefficient
        {
            get
            {
                var (cl, cd) = AeroPolar.Evaluate(TrimAngleOfAttack);
                double total = cd + ParasiticDrag;
                return MathD.Sqrt(cl * cl + total * total);
            }
        }
    }
}

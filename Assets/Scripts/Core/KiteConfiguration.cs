namespace Kitesim.Core
{
    public class KiteConfiguration
    {
        public double Mass = 0.397; // kg
        public double WingSpan = 2.51; // m
        public double SailArea = 0.75; // m^2
        public double LineLength = 45.0; // m
        public double BaseAngleOfAttack = 8.0 * MathD.Deg2Rad;
        public double SteeringEfficiency = 0.5;
        public double PitchEfficiency = 0.3;
        public double DragSteeringFactor = 0.6;
        public double BrakeDragFactor = 4.0;
        public double AirDensity = 1.225; // kg/m^3

        public static KiteConfiguration KrdFury()
        {
            return new KiteConfiguration();
        }
    }
}

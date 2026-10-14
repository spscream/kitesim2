namespace Kitesim.Core
{
    public static class PilotHands
    {
        public const double HalfWidth = 0.12;
        public const double Travel = 0.7;

        public static void Apply(RigidKiteSim sim, Vec3 stance, double left, double right)
        {
            double l = MathD.Clamp(left, -1.0, 1.0);
            double r = MathD.Clamp(right, -1.0, 1.0);
            sim.HandLeft = stance + new Vec3(-HalfWidth, -l * Travel, 0.0);
            sim.HandRight = stance + new Vec3(HalfWidth, -r * Travel, 0.0);
        }
    }
}

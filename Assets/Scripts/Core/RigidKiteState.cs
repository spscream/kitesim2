namespace Kitesim.Core
{
    /// <summary>
    /// Mutable 6-DOF state of the rigid kite: centre of mass, linear velocity,
    /// body-to-world orientation and body-frame angular velocity.
    /// </summary>
    public class RigidKiteState
    {
        public Vec3 Position;
        public Vec3 Velocity;
        public QuaternionD Orientation = QuaternionD.Identity;
        public Vec3 Omega;

        public static RigidKiteState At(Vec3 position)
        {
            return new RigidKiteState
            {
                Position = position,
                Velocity = Vec3.Zero,
                Orientation = QuaternionD.Identity,
                Omega = Vec3.Zero
            };
        }
    }
}

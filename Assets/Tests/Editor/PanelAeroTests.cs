using NUnit.Framework;

namespace Kitesim.Core.Tests
{
    public class PanelAeroTests
    {
        [Test]
        public void HundredSixteenHasSixPanelsWithSymmetricWingArea()
        {
            SailPanel[] panels = PanelAero.HundredSixteen(FrameInertia.FromHundredSixteen().CenterOfMass);

            Assert.AreEqual(6, panels.Length);
            double area = 0.0;
            for (int i = 0; i < panels.Length; i++)
                area += panels[i].Area;
            Assert.Greater(area, 0.8);
            Assert.Less(area, 2.0);
        }

        [Test]
        public void HeadwindOnAVerticalSailPushesDownwindAndNotThroughTheCanopy()
        {
            Vec3 com = FrameInertia.FromHundredSixteen().CenterOfMass;
            SailPanel[] panels = PanelAero.HundredSixteen(com);
            QuaternionD q = RigidKiteSim.NoseUpFacingPilot;
            Vec3 wind = new Vec3(0.0, 0.0, 6.0);

            PanelAero.Accumulate(
                panels, q, Vec3.Zero, Vec3.Zero, wind, 1.225,
                out Vec3 force, out Vec3 _);

            Assert.Greater(force.Z, 0.0, "drag carries the kite downwind");
            Assert.Greater(force.Y, -5.0, "must not suck the kite into the ground");
        }
    }
}

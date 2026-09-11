using Kitesim.Unity;
using Kitesim.Unity.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kitesim.Unity.EditorTests
{
    public class WindowProductConfigurationTests
    {
        [Test]
        public void ExclusiveBuildSettingsPutWindowFirstAndPreserveOtherScenesDisabled()
        {
            var current = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/TrainingScene.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/OtherScene.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/WindowScene.unity", false)
            };

            EditorBuildSettingsScene[] result = WindowSceneBuilder.ProductBuildScenes(current);

            Assert.AreEqual("Assets/Scenes/WindowScene.unity", result[0].path);
            Assert.IsTrue(result[0].enabled);
            Assert.AreEqual("Assets/Scenes/TrainingScene.unity", result[1].path);
            Assert.IsFalse(result[1].enabled);
            Assert.AreEqual("Assets/Scenes/OtherScene.unity", result[2].path);
            Assert.IsFalse(result[2].enabled);
        }

        [TestCase(0)]
        [TestCase(5)]
        public void BootstrapRejectsPilotCountsOutsideOneToFour(int pilotCount)
        {
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            try
            {
                Assert.IsFalse(WindowSceneBootstrap.TryValidateConfiguration(
                    pilotCount, actions, out string error));
                StringAssert.Contains("1..4", error);
            }
            finally
            {
                Object.DestroyImmediate(actions);
            }
        }

        [Test]
        public void BootstrapRejectsMissingInputActions()
        {
            Assert.IsFalse(WindowSceneBootstrap.TryValidateConfiguration(1, null, out string error));
            StringAssert.Contains("inputActions", error);
        }

        [Test]
        public void IndexedGamepadsAreAssignedInOrderAndKeyboardIsOnlyPilotZeroFallback()
        {
            var pads = new[] { MakePad(), MakePad() };

            Assert.AreSame(pads[0], WindowPilotDeviceAssignment.SelectDevice(0, pads, null));
            Assert.AreSame(pads[1], WindowPilotDeviceAssignment.SelectDevice(1, pads, null));
            Assert.IsNull(WindowPilotDeviceAssignment.SelectDevice(2, pads, null));

            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                Assert.AreSame(keyboard,
                    WindowPilotDeviceAssignment.SelectDevice(0, System.Array.Empty<Gamepad>(), keyboard));
                Assert.IsNull(
                    WindowPilotDeviceAssignment.SelectDevice(1, System.Array.Empty<Gamepad>(), keyboard));
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }

        private static Gamepad MakePad()
        {
            return InputSystem.AddDevice<Gamepad>();
        }

        [TearDown]
        public void TearDownDevices()
        {
            foreach (var device in InputSystem.devices)
            {
                if (device is Gamepad || device is Keyboard)
                    InputSystem.RemoveDevice(device);
            }
        }
    }
}

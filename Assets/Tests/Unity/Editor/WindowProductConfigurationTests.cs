using Kitesim.Core;
using Kitesim.Unity;
using Kitesim.Unity.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

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
        public void RigidHangPreviewStartsTheKiteInTheAir()
        {
            var go = new GameObject("Kite");
            try
            {
                var kite = go.AddComponent<WindowKiteBehaviour>();
                kite.Configure(0, Vec3.Zero, new WindField { Speed = 6.0, Direction = Vec3.Forward },
                    rigidHangPreview: true);

                Assert.IsFalse(kite.Landed);
                Assert.Greater(kite.View.WorldPosition.Y, 1.0);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
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

        [Test]
        public void KeyboardOverlayReadsHeldKeysWithoutPlayerInput()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A, Key.P, Key.W));
                InputSystem.Update();

                float left = 0f, right = 0f, walk = 0f;
                bool launch = false;
                WindowPilotInput.OverlayKeyboard(keyboard, ref left, ref right, ref walk, ref launch);

                Assert.AreEqual(-1f, left);
                Assert.AreEqual(1f, right);
                Assert.AreEqual(1f, walk);
                Assert.IsFalse(launch);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [Test]
        public void EmptyPilotZeroPicksUpAKeyboardThatAppearsLater()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/kitesim.inputactions");
            Assert.IsNotNull(actions, "the product input asset must be on disk");

            var kiteGo = new GameObject("Kite");
            var kite = kiteGo.AddComponent<WindowKiteBehaviour>();
            kite.Configure(0, Vec3.Zero, new WindField { Speed = 5.0, Direction = Vec3.Forward });

            PlayerInput player = null;
            Keyboard keyboard = null;
            try
            {
                player = WindowPilotDeviceAssignment.CreatePilotInput(
                    0, System.Array.Empty<Gamepad>(), null, actions, kite);
                Assert.AreEqual(0, player.devices.Count, "no device at spawn");

                keyboard = InputSystem.AddDevice<Keyboard>();
                Assert.IsTrue(WindowPilotDeviceAssignment.TryBindKeyboardIfEmpty(player, keyboard));
                Assert.AreEqual(1, player.devices.Count);
                Assert.AreSame(keyboard, player.devices[0]);
                Assert.AreEqual("Keyboard", player.currentControlScheme);
            }
            finally
            {
                if (player != null)
                    Object.DestroyImmediate(player.gameObject);
                Object.DestroyImmediate(kiteGo);
                if (keyboard != null && keyboard.added)
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

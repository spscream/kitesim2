using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Kitesim.Unity;
using Kitesim.Unity.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Kitesim.Unity.EditorTests
{
    public class WindowProductConfigurationTests
    {
        private readonly List<InputDevice> devices = new List<InputDevice>();
        private readonly List<GameObject> gameObjects = new List<GameObject>();
        private readonly List<InputActionAsset> actionAssets = new List<InputActionAsset>();

        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in gameObjects)
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);

            foreach (var actionAsset in actionAssets)
                if (actionAsset != null)
                    Object.DestroyImmediate(actionAsset);

            foreach (var device in devices)
                if (device.added)
                    InputSystem.RemoveDevice(device);
        }

        [Test]
        public void BuildSettingsPutWindowFirstAndDisableEveryOtherScene()
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

        [UnityTest]
        public IEnumerator InstantiateAndAssignUsesOnlyTheIndexedGamepad()
        {
            yield return new EnterPlayMode();

            AddDevice<Keyboard>();
            var first = AddDevice<Gamepad>();
            var second = AddDevice<Gamepad>();
            var template = CreatePlayerInputTemplate();
            var firstPlayer = WindowPilotDeviceAssignment.InstantiateAndAssign(
                template, 0, new[] { first, second }, Keyboard.current);
            var secondPlayer = WindowPilotDeviceAssignment.InstantiateAndAssign(
                template, 1, new[] { first, second }, Keyboard.current);
            gameObjects.Add(firstPlayer.gameObject);
            gameObjects.Add(secondPlayer.gameObject);

            AssertPlayerUsesOnly(firstPlayer, first, "Gamepad");
            AssertPlayerUsesOnly(secondPlayer, second, "Gamepad");
            Assert.AreNotSame(firstPlayer.actions, secondPlayer.actions,
                "Each PlayerInput must use its own filtered action asset copy.");

            AssertActionResolvesOnlyOn(
                firstPlayer.actions["Player/PullLeft"], first.leftStick.y);
            AssertActionResolvesOnlyOn(
                secondPlayer.actions["Player/PullLeft"], second.leftStick.y);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator InstantiateAndAssignUsesKeyboardOnlyAsPilotZeroFallback()
        {
            yield return new EnterPlayMode();

            var keyboard = AddDevice<Keyboard>();

            var playerInput = WindowPilotDeviceAssignment.InstantiateAndAssign(
                CreatePlayerInputTemplate(), 0, System.Array.Empty<Gamepad>(), keyboard);
            gameObjects.Add(playerInput.gameObject);

            AssertPlayerUsesOnly(playerInput, keyboard, "Keyboard");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator InstantiateAndAssignLeavesExtraPilotUnableToReadGlobalDevices()
        {
            yield return new EnterPlayMode();

            var keyboard = AddDevice<Keyboard>();
            LogAssert.Expect(LogType.Warning, new Regex(
                "Pilot 1 has no dedicated input device; input is disabled for this pilot"));

            var playerInput = WindowPilotDeviceAssignment.InstantiateAndAssign(
                CreatePlayerInputTemplate(), 1, System.Array.Empty<Gamepad>(), keyboard);
            gameObjects.Add(playerInput.gameObject);

            Assert.Zero(playerInput.devices.Count);
            Assert.IsTrue(playerInput.actions.devices.HasValue);
            Assert.Zero(playerInput.actions.devices.Value.Count);

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
            InputSystem.Update();

            Assert.Zero(playerInput.actions["Player/PullLeft"].ReadValue<float>());
            yield return new ExitPlayMode();
        }

        private TDevice AddDevice<TDevice>() where TDevice : InputDevice
        {
            var device = InputSystem.AddDevice<TDevice>();
            devices.Add(device);
            return device;
        }

        private GameObject CreatePlayerInputTemplate()
        {
            var source = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/kitesim.inputactions");
            Assert.IsNotNull(source, "The product input action asset must exist.");

            var actions = Object.Instantiate(source);
            actionAssets.Add(actions);

            var gameObject = new GameObject("PlayerInput test");
            gameObject.SetActive(false);
            gameObjects.Add(gameObject);

            var playerInput = gameObject.AddComponent<PlayerInput>();
            playerInput.actions = actions;
            playerInput.defaultActionMap = "Player";
            playerInput.neverAutoSwitchControlSchemes = true;
            return gameObject;
        }

        private static void AssertPlayerUsesOnly(
            PlayerInput playerInput, InputDevice expectedDevice, string expectedScheme)
        {
            Assert.AreEqual(1, playerInput.devices.Count);
            Assert.AreSame(expectedDevice, playerInput.devices[0]);
            Assert.AreEqual(expectedScheme, playerInput.currentControlScheme);
            Assert.IsTrue(playerInput.actions.devices.HasValue);
            Assert.AreEqual(1, playerInput.actions.devices.Value.Count);
            Assert.AreSame(expectedDevice, playerInput.actions.devices.Value[0]);
        }

        private static void AssertActionResolvesOnlyOn(
            InputAction action, InputControl expectedControl)
        {
            Assert.AreEqual(1, action.controls.Count);
            Assert.AreSame(expectedControl, action.controls[0]);
        }
    }
}

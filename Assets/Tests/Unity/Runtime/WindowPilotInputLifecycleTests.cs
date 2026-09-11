using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Kitesim.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Kitesim.Unity.PlayModeTests
{
    public class WindowPilotInputLifecycleTests : InputTestFixture
    {
        private readonly List<GameObject> players = new List<GameObject>();
        private InputActionAsset sourceActions;

        public override void Setup()
        {
            base.Setup();
            sourceActions = CreateProductActions();
        }

        public override void TearDown()
        {
            foreach (var player in players)
            {
                if (player != null)
                    Object.DestroyImmediate(player);
            }

            if (sourceActions != null)
                Object.DestroyImmediate(sourceActions);

            base.TearDown();
        }

        [Test]
        public void GamepadPilotAndEmptyPilotRemainStrictlyIsolated()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var gamepad = InputSystem.AddDevice<Gamepad>();
            LogAssert.Expect(LogType.Warning, new Regex(
                "Pilot 1 has no dedicated input device; input is disabled for this pilot"));

            var pilotZero = CreatePilot(0, new[] { gamepad }, keyboard);
            var pilotOne = CreatePilot(1, new[] { gamepad }, keyboard);

            AssertPlayerUsesOnly(pilotZero, 0, gamepad, "Gamepad");
            AssertPlayerUsesNoDevices(pilotOne, 1);
            Assert.AreNotSame(sourceActions, pilotZero.actions);
            Assert.AreNotSame(sourceActions, pilotOne.actions);
            Assert.AreNotSame(pilotZero.actions, pilotOne.actions);
            Assert.IsNull(sourceActions.devices,
                "Creating slots must not mutate the source action asset's device filter.");
            Assert.IsNotNull(pilotZero.GetComponent<WindowPilotInput>());
            Assert.IsNotNull(pilotOne.GetComponent<WindowPilotInput>());

            Set(gamepad.leftStick.y, 0.75f);
            float gamepadValue = pilotZero.actions["Player/PullLeft"].ReadValue<float>();
            Assert.Less(gamepadValue, -0.5f);
            Assert.Zero(pilotOne.actions["Player/PullLeft"].ReadValue<float>());

            Press(keyboard.aKey);
            Assert.AreEqual(gamepadValue,
                pilotZero.actions["Player/PullLeft"].ReadValue<float>(), 0.001f);
            Assert.Zero(pilotOne.actions["Player/PullLeft"].ReadValue<float>());
        }

        [Test]
        public void IndexedGamepadsReceiveIndependentActionAssets()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var first = InputSystem.AddDevice<Gamepad>();
            var second = InputSystem.AddDevice<Gamepad>();

            var pilotZero = CreatePilot(0, new[] { first, second }, keyboard);
            var pilotOne = CreatePilot(1, new[] { first, second }, keyboard);

            AssertPlayerUsesOnly(pilotZero, 0, first, "Gamepad");
            AssertPlayerUsesOnly(pilotOne, 1, second, "Gamepad");
            Assert.AreNotSame(pilotZero.actions, pilotOne.actions);
        }

        [Test]
        public void KeyboardFallbackBelongsOnlyToPilotZero()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            var pilotZero = CreatePilot(0, System.Array.Empty<Gamepad>(), keyboard);

            AssertPlayerUsesOnly(pilotZero, 0, keyboard, "Keyboard");
        }

        [UnityTest]
        public IEnumerator DestroyingPilotDestroysItsOwnedActionAsset()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            var pilot = CreatePilot(0, new[] { gamepad }, null);
            InputActionAsset ownedActions = pilot.actions;

            players.Remove(pilot.gameObject);
            Object.Destroy(pilot.gameObject);
            yield return null;

            Assert.IsTrue(ownedActions == null,
                "The per-pilot action asset must be destroyed with its PlayerInput.");
        }

        private PlayerInput CreatePilot(
            int pilotIndex, IReadOnlyList<Gamepad> gamepads, Keyboard keyboard)
        {
            var playerInput = WindowPilotDeviceAssignment.CreatePilotInput(
                pilotIndex, gamepads, keyboard, sourceActions, null);
            players.Add(playerInput.gameObject);
            return playerInput;
        }

        private static InputActionAsset CreateProductActions()
        {
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var player = new InputActionMap("Player");

            var pullLeft = player.AddAction("PullLeft", InputActionType.Value);
            pullLeft.AddBinding("<Gamepad>/leftStick/y", groups: "Gamepad")
                .WithProcessor("invert");
            pullLeft.AddBinding("<Keyboard>/a", groups: "Keyboard")
                .WithProcessor("invert");

            player.AddAction("PullRight", InputActionType.Value)
                .AddBinding("<Gamepad>/rightStick/y", groups: "Gamepad");
            player.AddAction("Walk", InputActionType.Value)
                .AddBinding("<Gamepad>/leftTrigger", groups: "Gamepad");
            player.AddAction("Launch", InputActionType.Button)
                .AddBinding("<Gamepad>/buttonSouth", groups: "Gamepad");

            actions.AddActionMap(player);
            actions.AddControlScheme("Gamepad").WithRequiredDevice<Gamepad>();
            actions.AddControlScheme("Keyboard").WithRequiredDevice<Keyboard>();
            return actions;
        }

        private static void AssertPlayerUsesOnly(
            PlayerInput playerInput, int expectedIndex,
            InputDevice expectedDevice, string expectedScheme)
        {
            Assert.AreEqual(expectedIndex, playerInput.playerIndex);
            Assert.AreEqual(1, playerInput.devices.Count);
            Assert.AreSame(expectedDevice, playerInput.devices[0]);
            Assert.AreEqual(expectedScheme, playerInput.currentControlScheme);
            Assert.IsTrue(playerInput.actions.devices.HasValue);
            Assert.AreEqual(1, playerInput.actions.devices.Value.Count);
            Assert.AreSame(expectedDevice, playerInput.actions.devices.Value[0]);
        }

        private static void AssertPlayerUsesNoDevices(PlayerInput playerInput, int expectedIndex)
        {
            Assert.AreEqual(expectedIndex, playerInput.playerIndex);
            Assert.Zero(playerInput.devices.Count);
            Assert.IsTrue(playerInput.actions.devices.HasValue);
            Assert.Zero(playerInput.actions.devices.Value.Count);
            Assert.Zero(playerInput.actions["Player/PullLeft"].controls.Count);
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kitesim.Unity
{
    /// <summary>
    /// Assigns one physical input device to each fixed pilot slot.
    /// </summary>
    public static class WindowPilotDeviceAssignment
    {
        private const string GamepadScheme = "Gamepad";
        private const string KeyboardScheme = "Keyboard";
        private const string NoDeviceScheme = "NoDevice";

        public static PlayerInput CreatePilotInput(
            int pilotIndex,
            IReadOnlyList<Gamepad> gamepads,
            Keyboard keyboard,
            InputActionAsset sourceActions,
            WindowKiteBehaviour kite)
        {
            if (gamepads == null)
                throw new ArgumentNullException(nameof(gamepads));
            if (sourceActions == null)
                throw new ArgumentNullException(nameof(sourceActions));
            if (pilotIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(pilotIndex));

            InputDevice device = SelectDevice(pilotIndex, gamepads, keyboard);
            InputActionAsset slotActions = UnityEngine.Object.Instantiate(sourceActions);
            if (device == null && slotActions.FindControlScheme(NoDeviceScheme) == null)
                slotActions.AddControlScheme(NoDeviceScheme);

            var template = new GameObject($"Pilot_{pilotIndex}");
            template.SetActive(false);
            var templateInput = template.AddComponent<PlayerInput>();
            templateInput.actions = slotActions;
            templateInput.defaultActionMap = "Player";
            templateInput.neverAutoSwitchControlSchemes = true;
            template.AddComponent<WindowPilotInput>().Bind(kite);

            PlayerInput playerInput = null;
            try
            {
                playerInput = device != null
                    ? PlayerInput.Instantiate(
                        template, pilotIndex, ControlSchemeFor(device), -1, device)
                    : PlayerInput.Instantiate(
                        template, pilotIndex, NoDeviceScheme, -1, Array.Empty<InputDevice>());

                if (playerInput == null)
                    throw new InvalidOperationException(
                        $"Could not create PlayerInput for pilot {pilotIndex}.");

                playerInput.gameObject.name = $"Pilot_{pilotIndex}";
                playerInput.gameObject.AddComponent<OwnedInputActionAsset>()
                    .Initialize(playerInput.actions);

                if (playerInput.actions != slotActions)
                    UnityEngine.Object.Destroy(slotActions);

                ValidateAssignment(playerInput, pilotIndex, device);
                if (device == null)
                {
                    Debug.LogWarning($"[WindowSceneBootstrap] Pilot {pilotIndex} has no dedicated input device; "
                        + "input is disabled for this pilot.");
                }

                return playerInput;
            }
            catch
            {
                if (playerInput != null)
                    UnityEngine.Object.Destroy(playerInput.gameObject);
                else
                    UnityEngine.Object.Destroy(slotActions);
                throw;
            }
            finally
            {
                UnityEngine.Object.Destroy(template);
            }
        }

        public static InputDevice SelectDevice(int pilotIndex,
            IReadOnlyList<Gamepad> gamepads, Keyboard keyboard)
        {
            if (gamepads == null)
                throw new ArgumentNullException(nameof(gamepads));
            if (pilotIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(pilotIndex));

            if (pilotIndex < gamepads.Count)
                return gamepads[pilotIndex];

            return pilotIndex == 0 ? keyboard : null;
        }

        private static string ControlSchemeFor(InputDevice device)
        {
            return device is Gamepad ? GamepadScheme : KeyboardScheme;
        }

        private static void ValidateAssignment(
            PlayerInput playerInput, int pilotIndex, InputDevice expectedDevice)
        {
            int expectedDeviceCount = expectedDevice == null ? 0 : 1;
            bool devicesMatch = playerInput.devices.Count == expectedDeviceCount
                && (expectedDevice == null || playerInput.devices[0] == expectedDevice);
            bool filterMatches = playerInput.actions.devices.HasValue
                && playerInput.actions.devices.Value.Count == expectedDeviceCount
                && (expectedDevice == null
                    || playerInput.actions.devices.Value[0] == expectedDevice);

            if (!playerInput.user.valid
                || playerInput.playerIndex != pilotIndex
                || !devicesMatch
                || !filterMatches)
            {
                throw new InvalidOperationException(
                    $"PlayerInput assignment for pilot {pilotIndex} was not exact.");
            }
        }
    }

    internal sealed class OwnedInputActionAsset : MonoBehaviour
    {
        private InputActionAsset actions;

        public void Initialize(InputActionAsset value)
        {
            actions = value;
        }

        void OnDestroy()
        {
            if (actions != null)
                Destroy(actions);
        }
    }
}

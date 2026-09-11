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

        public static PlayerInput InstantiateAndAssign(GameObject template, int pilotIndex,
            IReadOnlyList<Gamepad> gamepads, Keyboard keyboard)
        {
            if (template == null)
                throw new ArgumentNullException(nameof(template));

            InputDevice device = SelectDevice(pilotIndex, gamepads, keyboard);
            PlayerInput playerInput;

            if (device != null)
            {
                playerInput = PlayerInput.Instantiate(
                    template, pilotIndex, ControlSchemeFor(device), -1, device);
            }
            else
            {
                GameObject instance = UnityEngine.Object.Instantiate(template);
                instance.SetActive(true);
                playerInput = instance.GetComponent<PlayerInput>();
            }

            playerInput.gameObject.name = template.name;
            Assign(playerInput, pilotIndex, gamepads, keyboard);
            return playerInput;
        }

        public static bool Assign(PlayerInput playerInput, int pilotIndex,
            IReadOnlyList<Gamepad> gamepads, Keyboard keyboard)
        {
            if (playerInput == null)
                throw new ArgumentNullException(nameof(playerInput));
            if (pilotIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(pilotIndex));

            InputDevice device = SelectDevice(pilotIndex, gamepads, keyboard);

            if (device == null)
            {
                if (playerInput.user.valid)
                    playerInput.user.UnpairDevices();

                // An empty device filter is intentional. A null filter would let this
                // action copy resolve against every globally available device.
                playerInput.actions.devices = Array.Empty<InputDevice>();
                Debug.LogWarning($"[WindowSceneBootstrap] Pilot {pilotIndex} has no dedicated input device; "
                    + "input is disabled for this pilot.");
                return false;
            }

            playerInput.SwitchCurrentControlScheme(ControlSchemeFor(device), device);
            return true;
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
    }
}

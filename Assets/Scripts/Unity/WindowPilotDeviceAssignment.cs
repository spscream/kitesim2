using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

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

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void SendEditorKeysToTheGameView()
        {
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior =
                InputSettings.BackgroundBehavior.IgnoreFocus;
        }
#endif

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
                playerInput.gameObject.AddComponent<WindowPilotKeyboardCatch>()
                    .Bind(playerInput, pilotIndex);

                if (playerInput.actions != slotActions)
                    UnityEngine.Object.Destroy(slotActions);

                ValidateAssignment(playerInput, pilotIndex, device);
                TryBindKeyboardIfEmpty(playerInput, keyboard);
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

        /// <summary>
        /// Pair a keyboard onto an empty pilot-0 slot. Assignment happens in Awake,
        /// often before the Game view has focus, so Keyboard.current is still null
        /// and the slot is born as NoDevice. When the keyboard appears we catch it.
        /// </summary>
        public static bool TryBindKeyboardIfEmpty(PlayerInput playerInput, Keyboard keyboard)
        {
            if (playerInput == null || keyboard == null || !keyboard.added)
                return false;
            if (playerInput.devices.Count > 0)
                return false;
            if (!playerInput.user.valid)
                return false;

            playerInput.user.UnpairDevices();
            InputUser.PerformPairingWithDevice(keyboard, playerInput.user);
            playerInput.SwitchCurrentControlScheme(KeyboardScheme, keyboard);
            playerInput.actions.devices = new[] { keyboard };
            playerInput.ActivateInput();
            return playerInput.devices.Count > 0;
        }

        public static Keyboard FirstKeyboard()
        {
            if (Keyboard.current != null)
                return Keyboard.current;
            foreach (InputDevice device in InputSystem.devices)
            {
                if (device is Keyboard keyboard)
                    return keyboard;
            }
#if UNITY_EDITOR
            // Unity 6 Play Mode on this project exposes FastMouse but never a
            // FastKeyboard, even with Game view focused and
            // AllDeviceInputAlwaysGoesToGameView. Without a Keyboard device every
            // path — PlayerInput, OverlayKeyboard, the editor key forwarder — is a
            // no-op. Add one so the catcher and QueueStateEvent have a target.
            return InputSystem.AddDevice<Keyboard>();
#else
            return null;
#endif
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

    /// <summary>
    /// Pilot 0 keeps listening for a keyboard after spawn. Play Mode in the Editor
    /// often starts with the Game view unfocused, so the Input System has no
    /// keyboard yet and the slot would otherwise stay on NoDevice forever.
    /// </summary>
    internal sealed class WindowPilotKeyboardCatch : MonoBehaviour
    {
        private PlayerInput playerInput;
        private int pilotIndex;

        public void Bind(PlayerInput input, int index)
        {
            playerInput = input;
            pilotIndex = index;
            TryBind();
        }

        void OnEnable()
        {
            InputSystem.onDeviceChange += OnDeviceChange;
            TryBind();
        }

        void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
        }

        void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device is not Keyboard)
                return;
            if (change == InputDeviceChange.Added
                || change == InputDeviceChange.Reconnected
                || change == InputDeviceChange.Enabled)
                TryBind();
        }

        void TryBind()
        {
            if (pilotIndex != 0)
                return;
            WindowPilotDeviceAssignment.TryBindKeyboardIfEmpty(playerInput, WindowPilotDeviceAssignment.FirstKeyboard());
        }
    }
}

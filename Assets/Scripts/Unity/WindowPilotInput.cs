using UnityEngine;
using UnityEngine.InputSystem;

namespace Kitesim.Unity
{
    /// <summary>
    /// Reads one pilot's controls and feeds them to that pilot's kite. Sits on the
    /// same GameObject as a PlayerInput component whose device is assigned explicitly
    /// by WindowSceneBootstrap.
    ///
    /// Hands are position control: the stick axis is the hand's fore-aft position,
    /// rate-limited to a human hand speed so that a keyboard key does not teleport
    /// the hand across its whole travel in a single frame. Feet are rate control on
    /// the triggers.
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public class WindowPilotInput : MonoBehaviour
    {
        /// <summary>Maximum hand speed as a fraction of full travel per second.</summary>
        [SerializeField] private float handRatePerSecond = 2.5f / 0.7f;

        [SerializeField] private WindowKiteBehaviour kite;

        private PlayerInput playerInput;
        private InputAction pullLeft;
        private InputAction pullRight;
        private InputAction walk;
        private InputAction launch;

        private float smoothedLeft;
        private float smoothedRight;

        public void Bind(WindowKiteBehaviour target)
        {
            kite = target;
        }

        void Awake()
        {
            playerInput = GetComponent<PlayerInput>();
            pullLeft = playerInput.actions.FindAction("PullLeft", true);
            pullRight = playerInput.actions.FindAction("PullRight", true);
            walk = playerInput.actions.FindAction("Walk", true);
            launch = playerInput.actions.FindAction("Launch", true);
        }

        void Update()
        {
            if (kite == null)
                return;

            if (kite.PilotIndex == 0 && playerInput.devices.Count == 0)
                WindowPilotDeviceAssignment.TryBindKeyboardIfEmpty(
                    playerInput, WindowPilotDeviceAssignment.FirstKeyboard());

            float rawLeft = pullLeft.ReadValue<float>();
            float rawRight = pullRight.ReadValue<float>();
            float rawWalk = walk.ReadValue<float>();
            bool launchPressed = launch.WasPressedThisFrame();

            // Pilot 0 always reads the physical keyboard, even when PlayerInput
            // already has a device. Pairing FastKeyboard in the Editor does not
            // mean the action map is receiving HID; the device state does.
            if (kite.PilotIndex == 0)
                OverlayKeyboard(Keyboard.current, ref rawLeft, ref rawRight, ref rawWalk, ref launchPressed);

            float step = handRatePerSecond * Time.deltaTime;
            smoothedLeft = Mathf.MoveTowards(smoothedLeft, rawLeft, step);
            smoothedRight = Mathf.MoveTowards(smoothedRight, rawRight, step);

            // The smoothed pair is where the hands have got to and is what flies the
            // kite; the raw pair is what the pilot is asking for. They are the same
            // thing on real handles and 0.18 s apart on a stick, which matters to the
            // steering help and to nothing else.
            kite.SetInputs(smoothedLeft, smoothedRight, rawWalk, rawLeft, rawRight);

            if (launchPressed)
                kite.Launch();
        }

        /// <summary>
        /// Overlay A/D, O/P, W/S and Space from a keyboard onto already-read
        /// action values. Held keys win; unpressed keys leave the action value.
        /// </summary>
        public static void OverlayKeyboard(
            Keyboard keyboard, ref float left, ref float right, ref float walk, ref bool launch)
        {
            if (keyboard == null)
                return;

            if (keyboard.aKey.isPressed || keyboard.dKey.isPressed)
                left = Axis(keyboard.aKey.isPressed, keyboard.dKey.isPressed);
            if (keyboard.oKey.isPressed || keyboard.pKey.isPressed)
                right = Axis(keyboard.oKey.isPressed, keyboard.pKey.isPressed);
            if (keyboard.sKey.isPressed || keyboard.wKey.isPressed)
                walk = Axis(keyboard.sKey.isPressed, keyboard.wKey.isPressed);
            launch = launch || keyboard.spaceKey.wasPressedThisFrame;
        }

        private static float Axis(bool negative, bool positive)
        {
            float value = 0f;
            if (positive)
                value += 1f;
            if (negative)
                value -= 1f;
            return value;
        }
    }
}

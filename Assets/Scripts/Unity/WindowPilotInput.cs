using UnityEngine;
using UnityEngine.InputSystem;

namespace Kitesim.Unity
{
    /// <summary>
    /// Reads one pilot's controls and feeds them to that pilot's kite. Sits on the
    /// same GameObject as a PlayerInput component, so PlayerInputManager pairs one
    /// gamepad per pilot automatically.
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

        /// <summary>
        /// Line tension, in newtons, that corresponds to full rumble. A Fury on 45 m
        /// lines pulls tens of newtons in competition wind, so this is set above the
        /// usual working load: the rumble should read as loading up, not sit pinned.
        /// </summary>
        [SerializeField] private float rumbleFullScaleNewtons = 120f;

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

            float step = handRatePerSecond * Time.deltaTime;
            smoothedLeft = Mathf.MoveTowards(smoothedLeft, pullLeft.ReadValue<float>(), step);
            smoothedRight = Mathf.MoveTowards(smoothedRight, pullRight.ReadValue<float>(), step);

            kite.SetInputs(smoothedLeft, smoothedRight, walk.ReadValue<float>());

            if (launch.WasPressedThisFrame())
                kite.Launch();

            ApplyRumble();
        }

        /// <summary>
        /// The pull in the hands, as rumble. A kite loses its pull at the window edge
        /// and gains it in the middle, and feeling that is how a flier knows where
        /// the power is without looking at an instrument. The low motor gets the
        /// larger share: it reads as load rather than as a rattle.
        /// </summary>
        private void ApplyRumble()
        {
            var pad = playerInput.devices.Count > 0 ? playerInput.devices[0] as Gamepad : null;
            if (pad == null)
                return;

            float load = kite.Landed
                ? 0f
                : Mathf.Clamp01((float)kite.View.Tension / rumbleFullScaleNewtons);

            pad.SetMotorSpeeds(load * 0.25f, load * 0.12f);
        }

        /// <summary>
        /// Motors keep running after play stops unless they are told otherwise, which
        /// leaves a pad buzzing in the editor with nothing on screen.
        /// </summary>
        void OnDisable()
        {
            var pad = playerInput != null && playerInput.devices.Count > 0
                ? playerInput.devices[0] as Gamepad
                : null;
            pad?.ResetHaptics();
        }
    }
}

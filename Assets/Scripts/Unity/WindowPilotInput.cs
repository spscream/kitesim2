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
        }
    }
}

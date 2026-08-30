using UnityEngine;
using UnityEngine.InputSystem;

namespace Kitesim.Unity
{
    public class KiteInputController : MonoBehaviour
    {
        public static KiteInputController Instance { get; private set; }

        [SerializeField] private InputActionAsset actionsAsset;
        [SerializeField] private KiteBehaviour kite;

        private InputActionMap playerMap;

        public void Initialize(InputActionAsset asset, KiteBehaviour targetKite)
        {
            actionsAsset = asset;
            kite = targetKite;

            playerMap?.Disable();

            if (actionsAsset == null)
                CreateDefaultActions();

            playerMap = actionsAsset.FindActionMap("Player", true);
            playerMap.Enable();
        }

        void Awake()
        {
            Instance = this;
        }

        private void CreateDefaultActions()
        {
            actionsAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            actionsAsset.name = "kitesim";

            var map = new InputActionMap("Player");
            map.AddAction("PullLeft", binding: "<Gamepad>/leftStick/y", processors: "Invert");
            map.AddAction("PullRight", binding: "<Gamepad>/rightStick/y", processors: "Invert");
            var launch = map.AddAction("Launch", binding: "<Gamepad>/start");
            launch.AddBinding("<Gamepad>/buttonSouth");

            actionsAsset.AddActionMap(map);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            playerMap?.Disable();
        }

        void Update()
        {
            if (kite == null)
                return;

            float left = GetPull(0);
            float right = GetPull(1);
            kite.SetInputs(left, right);

            if (WasLaunchPressed())
                kite.Launch();
        }

        public float GetPull(int lineIndex)
        {
            var actionName = lineIndex == 0 ? "PullLeft" : "PullRight";
            var action = playerMap.FindAction(actionName, true);
            return action.ReadValue<float>();
        }

        public bool WasLaunchPressed()
        {
            var action = playerMap.FindAction("Launch", true);
            return action.WasPressedThisFrame();
        }
    }
}

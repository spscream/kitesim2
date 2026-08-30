using UnityEngine;
using Kitesim.Core;

namespace Kitesim.Unity
{
    [RequireComponent(typeof(LineRenderer))]
    public class KiteBehaviour : MonoBehaviour
    {
        [SerializeField] private KiteConfigAsset configAsset;
        [SerializeField] private Transform pilotTransform;
        [SerializeField] private Transform visualTransform;

        private KiteSim sim;
        private KiteState state;
        private LineRenderer lineRenderer;

        private float pullLeft;
        private float pullRight;

        public bool Launched => state?.Launched ?? false;

        public void Initialize(KiteConfigAsset config, Transform pilot, Transform visual)
        {
            configAsset = config;
            pilotTransform = pilot;
            this.visualTransform = visual;
        }

        void Start()
        {
            if (configAsset == null)
            {
                Debug.LogError("KiteBehaviour: configAsset is not assigned.", this);
                enabled = false;
                return;
            }

            sim = new KiteSim(configAsset.ToConfiguration());
            sim.PilotPosition = pilotTransform != null ? pilotTransform.position.ToVec3() : Vec3.Zero;
            sim.Wind = new WindField { Speed = 8.0, Direction = new Vec3(0, 0, 1) };

            state = KiteState.FromSpherical(0, 15f * MathD.Deg2Rad, sim.Config.LineLength);
            state.Position = sim.PilotPosition + new Vec3(0, 0.3, sim.Config.LineLength * 0.1);
            state.Launched = false;

            if (visualTransform == null)
                visualTransform = transform;

            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.positionCount = 4;
            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = 0.01f;
            lineRenderer.endWidth = 0.01f;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = Color.red;
            lineRenderer.endColor = Color.red;
        }

        void FixedUpdate()
        {
            if (!state.Launched)
                return;

            sim.Step(state, Time.fixedDeltaTime, pullLeft, pullRight);

            transform.position = state.Position.ToVector3();
            transform.rotation = Quaternion.LookRotation(
                state.Orientation.ToVector3(),
                state.Up.ToVector3()
            );

            SyncLines();
        }

        private void SyncLines()
        {
            Vector3 pilotBase = pilotTransform != null ? pilotTransform.position : Vector3.zero;
            Vector3 pilotHands = pilotBase + new Vector3(0, 1.5f, 0);
            Vector3 leftHand = pilotHands + new Vector3(-0.3f, 0, 0);
            Vector3 rightHand = pilotHands + new Vector3(0.3f, 0, 0);

            Vector3 kiteLeft = transform.position + transform.rotation * new Vector3(-0.5f, 0, 0);
            Vector3 kiteRight = transform.position + transform.rotation * new Vector3(0.5f, 0, 0);

            lineRenderer.SetPosition(0, leftHand);
            lineRenderer.SetPosition(1, kiteLeft);
            lineRenderer.SetPosition(2, kiteRight);
            lineRenderer.SetPosition(3, rightHand);
        }

        public void SetInputs(float left, float right)
        {
            pullLeft = left;
            pullRight = right;
        }

        public void SetPilot(Transform pilot)
        {
            pilotTransform = pilot;
            sim.PilotPosition = pilot.position.ToVec3();
        }

        public void SetWind(double speedMs, Vec3 direction)
        {
            sim.Wind = new WindField { Speed = speedMs, Direction = direction.Normalized() };
        }

        public void Launch()
        {
            if (state.Launched)
                return;

            state.Launched = true;
            state.Velocity = new Vec3(0, 2.0, 2.0);
        }
    }
}

using UnityEngine;

namespace Kitesim.Unity
{
    public class TrainingCamera : MonoBehaviour
    {
        [SerializeField] private Transform pilot;
        [SerializeField] private Transform kite;
        [SerializeField] private Vector3 offset = new Vector3(0, 8f, -25f);
        [SerializeField] private float smoothTime = 0.3f;

        private Vector3 velocity;

        public void Initialize(Transform pilotTransform, Transform kiteTransform)
        {
            pilot = pilotTransform;
            kite = kiteTransform;
        }

        void LateUpdate()
        {
            if (pilot == null)
                return;

            Vector3 desired = pilot.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);

            Vector3 lookTarget = pilot.position + Vector3.up * 5f;
            if (kite != null)
                lookTarget = kite.position;

            transform.LookAt(lookTarget);
        }
    }
}

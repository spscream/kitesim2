using UnityEngine;

namespace Kitesim.Unity
{
    public class TrainingCamera : MonoBehaviour
    {
        [SerializeField] private Transform pilot;
        [SerializeField] private Transform kite;
        [SerializeField] private float eyeHeight = 1.6f;

        public void Initialize(Transform pilotTransform, Transform kiteTransform)
        {
            pilot = pilotTransform;
            kite = kiteTransform;
        }

        void LateUpdate()
        {
            if (pilot == null)
                return;

            transform.position = pilot.position + Vector3.up * eyeHeight;

            Vector3 lookTarget = pilot.position + Vector3.up * 5f;
            if (kite != null)
                lookTarget = kite.position;

            transform.LookAt(lookTarget);
        }
    }
}

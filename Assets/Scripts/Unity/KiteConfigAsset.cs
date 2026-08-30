using UnityEngine;
using Kitesim.Core;

namespace Kitesim.Unity
{
    [CreateAssetMenu(fileName = "FuryConfig", menuName = "Kitesim/Kite Config")]
    public class KiteConfigAsset : ScriptableObject
    {
        [Header("Mass and Geometry")]
        public double mass = 0.397;
        public double wingSpan = 2.51;
        public double sailArea = 0.75;
        public double lineLength = 45.0;

        [Header("Aerodynamics")]
        [Range(0f, 30f)]
        public float baseAngleOfAttackDegrees = 8f;
        [Range(0f, 5f)]
        public float pitchEfficiency = 1f;
        [Range(0f, 2f)]
        public float steeringEfficiency = 0.5f;
        [Range(0f, 2f)]
        public float dragSteeringFactor = 0.6f;
        [Range(0f, 10f)]
        public float brakeDragFactor = 4f;
        public double airDensity = 1.225;

        public KiteConfiguration ToConfiguration()
        {
            return new KiteConfiguration
            {
                Mass = mass,
                WingSpan = wingSpan,
                SailArea = sailArea,
                LineLength = lineLength,
                BaseAngleOfAttack = baseAngleOfAttackDegrees * MathD.Deg2Rad,
                PitchEfficiency = pitchEfficiency,
                SteeringEfficiency = steeringEfficiency,
                DragSteeringFactor = dragSteeringFactor,
                BrakeDragFactor = brakeDragFactor,
                AirDensity = airDensity
            };
        }
    }
}

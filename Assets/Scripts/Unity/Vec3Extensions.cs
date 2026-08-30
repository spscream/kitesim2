using UnityEngine;
using Kitesim.Core;

namespace Kitesim.Unity
{
    public static class Vec3Extensions
    {
        public static Vector3 ToVector3(this Vec3 v)
        {
            return new Vector3((float)v.X, (float)v.Y, (float)v.Z);
        }

        public static Vec3 ToVec3(this Vector3 v)
        {
            return new Vec3(v.x, v.y, v.z);
        }
    }
}

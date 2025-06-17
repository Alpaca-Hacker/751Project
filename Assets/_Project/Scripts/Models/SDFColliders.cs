using UnityEngine;

namespace SoftBody.Scripts.Models
{
    // Enum to identify collider type on the GPU
    public enum ColliderType
    {
        Sphere = 0,
        Plane = 1
    }

    public struct SDFCollider
    {
        public Vector4 data1; // Sphere: center.xyz, radius.w | Plane: normal.xyz, distance.w
        public Vector4 data2; // Future use (e.g., box dimensions)
        public int type; // 0 for Sphere, 1 for Plane, etc.

        // Padding to ensure struct is 16-byte aligned
        private int _padding1;
        private int _padding2;
        private int _padding3;

        // --- Convenience Constructors ---
        public static SDFCollider CreateSphere(Vector3 center, float radius)
        {
            return new SDFCollider
            {
                data1 = new Vector4(center.x, center.y, center.z, radius),
                type = (int)ColliderType.Sphere
            };
        }

        public static SDFCollider CreatePlane(Vector3 normal, float distance)
        {
            return new SDFCollider
            {
                data1 = new Vector4(normal.x, normal.y, normal.z, distance),
                type = (int)ColliderType.Plane
            };
        }
    }
}

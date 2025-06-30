// Models/SDFColliders.cs - Complete updated version
using UnityEngine;

namespace SoftBody.Scripts.Models
{
    public enum ColliderType
    {
        Sphere = 0,
        Plane = 1,
        Box = 2,
        Cylinder = 3
    }

    public struct SDFCollider
    {
        public Vector4 data1; // Position (xyz) + radius/distance (w)
        public Vector4 data2; // Box: halfExtents (xyz) | Cylinder: radius(x), halfHeight(y)
        public Vector4 rotation; // Quaternion xyzw
        public int type;
        
        private int _padding1, _padding2, _padding3; // Padding to ensure alignment

        // --- Convenience Constructors ---
        public static SDFCollider CreateSphere(Vector3 center, float radius)
        {
            return new SDFCollider
            {
                data1 = new Vector4(center.x, center.y, center.z, radius),
                rotation = new Vector4(0, 0, 0, 1), // Identity quaternion
                type = (int)ColliderType.Sphere
            };
        }

        public static SDFCollider CreatePlane(Vector3 normal, float distance)
        {
            return new SDFCollider
            {
                data1 = new Vector4(normal.x, normal.y, normal.z, distance),
                rotation = new Vector4(0, 0, 0, 1),
                type = (int)ColliderType.Plane
            };
        }

        public static SDFCollider CreateBox(Vector3 center, Vector3 halfExtents)
        {
            return CreateBox(center, halfExtents, Quaternion.identity);
        }

        public static SDFCollider CreateBox(Vector3 center, Vector3 halfExtents, Quaternion rot)
        {
            return new SDFCollider
            {
                data1 = new Vector4(center.x, center.y, center.z, 0),
                data2 = new Vector4(halfExtents.x, halfExtents.y, halfExtents.z, 0),
                rotation = new Vector4(rot.x, rot.y, rot.z, rot.w),
                type = (int)ColliderType.Box
            };
        }

        public static SDFCollider CreateCylinder(Vector3 center, float radius, float height)
        {
            return CreateCylinder(center, radius, height, Quaternion.identity);
        }

        public static SDFCollider CreateCylinder(Vector3 center, float radius, float height, Quaternion rot)
        {
            return new SDFCollider
            {
                data1 = new Vector4(center.x, center.y, center.z, 0),
                data2 = new Vector4(radius, height * 0.5f, 0, 0),
                rotation = new Vector4(rot.x, rot.y, rot.z, rot.w),
                type = (int)ColliderType.Cylinder
            };
        }
    }
}
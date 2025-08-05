using UnityEngine;

namespace SoftBody.Scripts.Models
{
    public struct Particle
    {
        public Vector4 PositionAndInvMass; // x, y, z = position, w = invMass
        public Vector4 Velocity; // x, y, z = velocity, w = padding
        public Vector4 Force; // x, y, z = force, w = padding

        // Convenience properties to make the rest of the C# code easier to read
        // We use ref to modify the value directly in the struct.
        public Vector3 Position
        {
            get => new(PositionAndInvMass.x, PositionAndInvMass.y, PositionAndInvMass.z);
            set
            {
                PositionAndInvMass.x = value.x;
                PositionAndInvMass.y = value.y;
                PositionAndInvMass.z = value.z;
            }
        }

        public float InvMass
        {
            get => PositionAndInvMass.w;
            set => PositionAndInvMass.w = value;
        }
    }
}
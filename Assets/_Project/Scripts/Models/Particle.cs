using UnityEngine;

namespace SoftBody.Scripts.Models
{
    public struct Particle
    {
        public Vector4 _positionAndInvMass; // x, y, z = position, w = invMass
        public Vector4 Velocity; // x, y, z = velocity, w = padding
        public Vector4 Force; // x, y, z = force, w = padding

        // Convenience properties to make the rest of the C# code easier to read
        // We use ref to modify the value directly in the struct.
        public Vector3 Position
        {
            get => new(_positionAndInvMass.x, _positionAndInvMass.y, _positionAndInvMass.z);
            set
            {
                _positionAndInvMass.x = value.x;
                _positionAndInvMass.y = value.y;
                _positionAndInvMass.z = value.z;
            }
        }

        public float InvMass
        {
            get => _positionAndInvMass.w;
            set => _positionAndInvMass.w = value;
        }
    }
}
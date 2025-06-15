using UnityEngine;

namespace SoftBody.Scripts.Models
{
    public struct Particle
    {
      public Vector3 Position;
      public Vector3 Velocity;
      public Vector3 Force;
      public float InvMass;
    }
}
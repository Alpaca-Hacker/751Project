using System;
using UnityEngine;

namespace SoftBody.Scripts.Models
{
    [Serializable]
    public struct SerializableParticle
    {
        public Vector3 position;
        public float invMass;
        
        public Particle ToParticle()
        {
            return new Particle
            {
                Position = position,
                Velocity = Vector4.zero,
                Force = Vector4.zero,
                InvMass = invMass
            };
        }
        
        public static SerializableParticle FromParticle(Particle p)
        {
            return new SerializableParticle
            {
                position = p.Position,
                invMass = p.InvMass
            };
        }
    }
}
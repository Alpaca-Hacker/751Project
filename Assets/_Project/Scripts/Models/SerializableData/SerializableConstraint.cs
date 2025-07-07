using System;

namespace SoftBody.Scripts.Models
{
    [Serializable]
    public struct SerializableConstraint
    {
        public int particleA;
        public int particleB;
        public float restLength;
        public float compliance;
        public int colourGroup;
        
        public Constraint ToConstraint()
        {
            return new Constraint
            {
                ParticleA = particleA,
                ParticleB = particleB,
                RestLength = restLength,
                Compliance = compliance,
                Lambda = 0f,
                ColourGroup = colourGroup
            };
        }
        
        public static SerializableConstraint FromConstraint(Constraint c)
        {
            return new SerializableConstraint
            {
                particleA = c.ParticleA,
                particleB = c.ParticleB,
                restLength = c.RestLength,
                compliance = c.Compliance,
                colourGroup = c.ColourGroup
            };
        }
    }
}
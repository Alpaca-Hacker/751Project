using System;
using System.Collections.Generic;
using SoftBody.Scripts.Models;
using UnityEngine;

namespace SoftBody.Scripts.Models
{
    [Serializable]
    public class SerializableSoftBodyData
    {
        [Header("Generated Physics Data")]
        public SerializableParticle[] particles;
        public SerializableConstraint[] constraints;
        public SerializableVolumeConstraint[] volumeConstraints;
        public int[] indices;
        public Vector2[] uvs;
        
        [Header("Metadata")]
        public string meshName;
        public int particleCount;
        public int constraintCount;
        public int volumeConstraintCount;
        
        public bool IsValid => particles != null && particles.Length > 0 && 
                              constraints != null && constraints.Length > 0;
    }
}
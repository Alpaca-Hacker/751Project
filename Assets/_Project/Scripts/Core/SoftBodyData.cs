using System.Collections.Generic;
using SoftBody.Scripts.Models;
using UnityEngine;

namespace SoftBody.Scripts.Core
{
    public class SoftBodyData
    {
        public List<Particle> Particles { get; set; }
        public List<Constraint> Constraints { get; set; }
        public List<VolumeConstraint> VolumeConstraints { get; set; }
        public List<int> Indices { get; set; }
        public Vector2[] UVs { get; set; }

        public bool IsValid => Particles?.Count > 0 && Constraints?.Count > 0;
    }

}
using System.Collections.Generic;
using SoftBody.Scripts.Models;

namespace SoftBody.Scripts.Core
{
    public class SoftBodyData
    {
        public List<Particle> Particles { get; set; }
        public List<Constraint> Constraints { get; set; }
        public List<VolumeConstraint> VolumeConstraints { get; set; }
        public List<int> Indices { get; set; }
    }

}
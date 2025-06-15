using System.Collections.Generic;

namespace SoftBody.Scripts.Models
{
    public struct Cluster
    {
        public List<int> Constraints;
        public HashSet<int> Particles;
        public int ColourGroup;
    }
}
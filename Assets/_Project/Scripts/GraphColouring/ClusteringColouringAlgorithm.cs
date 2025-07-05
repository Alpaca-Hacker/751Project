using System.Collections.Generic;
using SoftBody.Scripts.Models;
using SoftBody.Scripts;

namespace SoftBody.Scripts.Algorithms.GraphColouring
{
    public class ClusteringColouringAlgorithm : IGraphColouringAlgorithm
    {
        public void ApplyColouring(List<Constraint> constraints, int particleCount)
        {
            var clusters = SoftBody.Scripts.GraphColouring.CreateClusters(constraints, particleCount, false);
            SoftBody.Scripts.GraphColouring.ColourClusters(clusters, constraints, false);
        }
        
        
    }
}
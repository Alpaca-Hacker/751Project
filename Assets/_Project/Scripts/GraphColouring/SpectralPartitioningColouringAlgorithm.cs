using System.Collections.Generic;
using SoftBody.Scripts.Models;

namespace SoftBody.Scripts.Algorithms.GraphColouring
{
    public class SpectralPartitioningColouringAlgorithm : IGraphColouringAlgorithm
    {
        public void ApplyColouring(List<Constraint> constraints, int particleCount)
        {
            var clusters = SoftBody.Scripts.GraphColouring.CreateClustersWithSpectralPartitioning(constraints, particleCount, false);
            SoftBody.Scripts.GraphColouring.ColourClusters(clusters, constraints, false);
        }
    }
}
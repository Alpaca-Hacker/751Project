using SoftBody.Scripts.Algorithms.GraphColouring;

namespace SoftBody.Scripts
{
    public static class GraphColouringFactory
    {
        public static IGraphColouringAlgorithm Create(GraphColouringMethod method)
        {
            return method switch
            {
                GraphColouringMethod.Greedy => new GreedyColouringAlgorithm(),
                GraphColouringMethod.Clustering => new ClusteringColouringAlgorithm(),
                GraphColouringMethod.SpectralPartitioning => new SpectralPartitioningColouringAlgorithm(),
                GraphColouringMethod.Naive => new NaiveColouringAlgorithm(),
                GraphColouringMethod.None => new NoColouringAlgorithm(),
                _ => new GreedyColouringAlgorithm() // Safe fallback
            };
        }
    }
}
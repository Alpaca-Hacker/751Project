using System.Collections.Generic;
using System.Linq;
using SoftBody.Scripts.Models;
using UnityEngine;

namespace SoftBody.Scripts
{
       public static class GraphClusterer
    {


        public static List<Cluster> CreateClusters(List<Constraint> constraints, int particleCount, int targetClustersPerParticle = 8)
        {
            // Build adjacency information
            var particleToConstraints = new List<int>[particleCount];
            for (var i = 0; i < particleCount; i++)
            {
                particleToConstraints[i] = new List<int>();
            }

            for (var i = 0; i < constraints.Count; i++)
            {
                particleToConstraints[constraints[i].ParticleA].Add(i);
                particleToConstraints[constraints[i].ParticleB].Add(i);
            }

            // Create initial clusters - one per constraint
            var clusters = new List<Cluster>();
            for (var i = 0; i < constraints.Count; i++)
            {
                var cluster = new Cluster
                {
                    Constraints = new List<int> { i },
                    Particles = new HashSet<int> { constraints[i].ParticleA, constraints[i].ParticleB },
                    ColourGroup = -1
                };
                clusters.Add(cluster);
            }

            // Merge clusters to reduce total count
            var targetClusterCount = Mathf.Max(1, constraints.Count / targetClustersPerParticle);
            
            while (clusters.Count > targetClusterCount)
            {
                // Find best pair to merge
                int bestI = -1, bestJ = -1;
                var bestSharedParticles = 0;

                for (var i = 0; i < clusters.Count; i++)
                {
                    for (var j = i + 1; j < clusters.Count; j++)
                    {
                        var sharedParticles = clusters[i].Particles.Intersect(clusters[j].Particles).Count();
                        if (sharedParticles > bestSharedParticles)
                        {
                            bestSharedParticles = sharedParticles;
                            bestI = i;
                            bestJ = j;
                        }
                    }
                }

                if (bestI == -1) break; // No more merges possible

                // Merge clusters
                clusters[bestI].Constraints.AddRange(clusters[bestJ].Constraints);
                clusters[bestI].Particles.UnionWith(clusters[bestJ].Particles);
                clusters.RemoveAt(bestJ);
            }

            Debug.Log($"Created {clusters.Count} clusters from {constraints.Count} constraints");
            return clusters;
        }

        public static void ColourClusters(List<Cluster> clusters, List<Constraint> constraints)
        {
            // Build cluster adjacency
            var adjacency = new HashSet<int>[clusters.Count];
            for (var i = 0; i < clusters.Count; i++)
            {
                adjacency[i] = new HashSet<int>();
            }

            // Two clusters are adjacent if they share particles
            for (var i = 0; i < clusters.Count; i++)
            {
                for (var j = i + 1; j < clusters.Count; j++)
                {
                    if (clusters[i].Particles.Intersect(clusters[j].Particles).Any())
                    {
                        adjacency[i].Add(j);
                        adjacency[j].Add(i);
                    }
                }
            }

            // Greedy colouring of clusters
            var clusterList = clusters.ToList(); // Make a copy to modify
            var maxColour = 0;
            
            for (var i = 0; i < clusterList.Count; i++)
            {
                var usedcolours = new HashSet<int>();
                foreach (var adj in adjacency[i])
                {
                    if (clusterList[adj].ColourGroup >= 0)
                    {
                        usedcolours.Add(clusterList[adj].ColourGroup);
                    }
                }

                // Find first available colour
                var colour = 0;
                while (usedcolours.Contains(colour)) colour++;
                
                var cluster = clusterList[i];
                cluster.ColourGroup = colour;
                clusterList[i] = cluster;
                
                maxColour = Mathf.Max(maxColour, colour);

                // Apply colour to all constraints in cluster
                foreach (var constraintIdx in cluster.Constraints)
                {
                    var constraint = constraints[constraintIdx];
                    constraint.ColourGroup = colour;
                    constraints[constraintIdx] = constraint;
                }
            }

            Debug.Log($"Graph clustering complete: {maxColour + 1} colour groups needed");
        }
    }
}
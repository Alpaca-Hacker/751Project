using System.Collections.Generic;
using System.Linq;
using SoftBody.Scripts.Models;
using UnityEngine;

namespace SoftBody.Scripts
{
    public enum GraphColouringMethod
    {
        None,
        Clustering,
        Greedy,
        Naive,
        SpectralPartitioning
    }
       public static class GraphColouring
    {

        public static List<Cluster> CreateClusters(List<Constraint> constraints, int particleCount,
          bool debugMessages,  int targetClustersPerParticle = 8)
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

            // Build a constraint adjacency graph
            var constraintAdjacency = new bool[constraints.Count, constraints.Count];
            for (var i = 0; i < constraints.Count; i++)
            {
                for (var j = i + 1; j < constraints.Count; j++)
                {
                    if (constraints[i].ParticleA == constraints[j].ParticleA ||
                        constraints[i].ParticleA == constraints[j].ParticleB ||
                        constraints[i].ParticleB == constraints[j].ParticleA ||
                        constraints[i].ParticleB == constraints[j].ParticleB)
                    {
                        constraintAdjacency[i, j] = true;
                        constraintAdjacency[j, i] = true;
                    }
                }
            }

            // Merge clusters using a different strategy: only merge if no constraints conflict
            var targetClusterCount = Mathf.Max(1, constraints.Count / targetClustersPerParticle);
            var clusterMap = new int[constraints.Count]; // Maps constraint index to cluster index
            for (var i = 0; i < constraints.Count; i++)
            {
                clusterMap[i] = i;
            }

            while (clusters.Count > targetClusterCount)
            {
                int bestI = -1, bestJ = -1;
                var bestScore = float.MaxValue;

                // Find best pair to merge that don't have conflicting constraints
                for (var i = 0; i < clusters.Count; i++)
                {
                    for (var j = i + 1; j < clusters.Count; j++)
                    {
                        // Check if these clusters can be merged (no constraints share particles)
                        var canMerge = true;
                        foreach (var c1 in clusters[i].Constraints)
                        {
                            foreach (var c2 in clusters[j].Constraints)
                            {
                                if (constraintAdjacency[c1, c2])
                                {
                                    canMerge = false;
                                    break;
                                }
                            }

                            if (!canMerge) break;
                        }

                        if (canMerge)
                        {
                            // Score based on spatial locality or shared vertices (prefer merging nearby constraints)
                            var sharedParticles = clusters[i].Particles.Intersect(clusters[j].Particles).Count();
                            var score = 1.0f / (sharedParticles + 1); // Lower score is better

                            if (score < bestScore)
                            {
                                bestScore = score;
                                bestI = i;
                                bestJ = j;
                            }
                        }
                    }
                }

                if (bestI == -1) break; // No more valid merges possible

                // Merge clusters
                clusters[bestI].Constraints.AddRange(clusters[bestJ].Constraints);
                clusters[bestI].Particles.UnionWith(clusters[bestJ].Particles);

                // Update cluster map
                foreach (var c in clusters[bestJ].Constraints)
                {
                    clusterMap[c] = bestI;
                }

                clusters.RemoveAt(bestJ);
            }

            if (debugMessages)
            {
                Debug.Log($"Created {clusters.Count} clusters from {constraints.Count} constraints");
            }

            return clusters;
        }

        public static void ColourClusters(List<Cluster> clusters, List<Constraint> constraints, bool debugMessages)
        {
            // Since clusters now contain non-conflicting constraints, 
            // all constraints within a cluster can have the same color
            for (var i = 0; i < clusters.Count; i++)
            {
                var cluster = clusters[i];
                cluster.ColourGroup = i; // Each cluster gets its own color
        
                // Apply color to all constraints in cluster
                foreach (var constraintIdx in cluster.Constraints)
                {
                    var constraint = constraints[constraintIdx];
                    constraint.ColourGroup = i;
                    constraints[constraintIdx] = constraint;
                }
            }

            if (debugMessages)
            {
                Debug.Log($"Graph clustering complete: {clusters.Count} colour groups used");
            }
        }
        
        public static int ColourConstraints(List<Constraint> constraints, int particleCount, bool debugMessages)
        {
            // Build adjacency list for constraints
            var constraintAdjacency = BuildConstraintAdjacencyList(constraints, particleCount);
            
            // Apply greedy graph coloring
            var maxColor = GreedyGraphColoring(constraints, constraintAdjacency);
            if (debugMessages)
            {
                Debug.Log(
                    $"Graph coloring complete: {maxColor + 1} color groups needed for {constraints.Count} constraints");
            }

            return maxColor + 1;
        }
        
        private static List<HashSet<int>> BuildConstraintAdjacencyList(List<Constraint> constraints, int particleCount)
        {
            // First, build a map from particles to constraints
            var particleToConstraints = new List<HashSet<int>>(particleCount);
            for (var i = 0; i < particleCount; i++)
            {
                particleToConstraints.Add(new HashSet<int>());
            }
            
            for (var i = 0; i < constraints.Count; i++)
            {
                particleToConstraints[constraints[i].ParticleA].Add(i);
                particleToConstraints[constraints[i].ParticleB].Add(i);
            }
            
            // Now build constraint adjacency list
            var adjacency = new List<HashSet<int>>(constraints.Count);
            for (var i = 0; i < constraints.Count; i++)
            {
                adjacency.Add(new HashSet<int>());
            }
            
            // Two constraints are adjacent if they share a particle
            for (var i = 0; i < constraints.Count; i++)
            {
                var c1 = constraints[i];
                
                // Find all constraints that share particles with constraint i
                var connectedConstraints = new HashSet<int>();
                connectedConstraints.UnionWith(particleToConstraints[c1.ParticleA]);
                connectedConstraints.UnionWith(particleToConstraints[c1.ParticleB]);
                connectedConstraints.Remove(i); // Don't include self
                
                adjacency[i] = connectedConstraints;
            }
            
            return adjacency;
        }
        
        private static int GreedyGraphColoring(List<Constraint> constraints, List<HashSet<int>> adjacency)
        {
            var colors = new int[constraints.Count];
            for (var i = 0; i < colors.Length; i++)
            {
                colors[i] = -1; // Uncolored
            }
            
            var maxColor = -1;
            
            // Color vertices one by one
            for (var i = 0; i < constraints.Count; i++)
            {
                // Find colors used by adjacent constraints
                var usedColors = new HashSet<int>();
                foreach (var neighbor in adjacency[i])
                {
                    if (colors[neighbor] != -1)
                    {
                        usedColors.Add(colors[neighbor]);
                    }
                }
                
                // Find first available color
                var color = 0;
                while (usedColors.Contains(color))
                {
                    color++;
                }
                
                colors[i] = color;
                maxColor = Mathf.Max(maxColor, color);
                
                // Update the constraint
                var constraint = constraints[i];
                constraint.ColourGroup = color;
                constraints[i] = constraint;
            }
            
            return maxColor;
        }

        public static List<Cluster> CreateClustersWithSpectralPartitioning(List<Constraint> constraints,
            int particleCount, bool debugMessages, int targetClusters = 32)
        {
            // Build constraint graph Laplacian
            var n = constraints.Count;
            var adjacencyMatrix = new float[n, n];
            var degreeMatrix = new float[n];

            // Build adjacency matrix
            for (var i = 0; i < n; i++)
            {
                for (var j = i + 1; j < n; j++)
                {
                    // Check if constraints share a particle
                    if (constraints[i].ParticleA == constraints[j].ParticleA ||
                        constraints[i].ParticleA == constraints[j].ParticleB ||
                        constraints[i].ParticleB == constraints[j].ParticleA ||
                        constraints[i].ParticleB == constraints[j].ParticleB)
                    {
                        adjacencyMatrix[i, j] = 1;
                        adjacencyMatrix[j, i] = 1;
                        degreeMatrix[i] += 1;
                        degreeMatrix[j] += 1;
                    }
                }
            }

            // Create independent sets of constraints
            var independentSets = new List<List<int>>();
            var assigned = new bool[n];

            while (independentSets.Count < targetClusters)
            {
                var currentSet = new List<int>();

                for (var i = 0; i < n; i++)
                {
                    if (assigned[i]) continue;

                    // Check if this constraint conflicts with any in current set
                    var conflicts = false;
                    foreach (var j in currentSet)
                    {
                        if (adjacencyMatrix[i, j] > 0)
                        {
                            conflicts = true;
                            break;
                        }
                    }

                    if (!conflicts)
                    {
                        currentSet.Add(i);
                        assigned[i] = true;
                    }
                }

                if (currentSet.Count == 0) break;
                independentSets.Add(currentSet);
            }

            // Convert independent sets to clusters
            var clusters = new List<Cluster>();
            for (var i = 0; i < independentSets.Count; i++)
            {
                var particles = new HashSet<int>();
                foreach (var cIdx in independentSets[i])
                {
                    particles.Add(constraints[cIdx].ParticleA);
                    particles.Add(constraints[cIdx].ParticleB);
                }

                clusters.Add(new Cluster
                {
                    Constraints = independentSets[i],
                    Particles = particles,
                    ColourGroup = i
                });
            }

            // Assign remaining constraints to compatible clusters
            for (var i = 0; i < n; i++)
            {
                if (!assigned[i])
                {
                    // Find a cluster this constraint can join
                    for (var c = 0; c < clusters.Count; c++)
                    {
                        var canJoin = true;
                        foreach (var j in clusters[c].Constraints)
                        {
                            if (adjacencyMatrix[i, j] > 0)
                            {
                                canJoin = false;
                                break;
                            }
                        }

                        if (canJoin)
                        {
                            clusters[c].Constraints.Add(i);
                            clusters[c].Particles.Add(constraints[i].ParticleA);
                            clusters[c].Particles.Add(constraints[i].ParticleB);
                            assigned[i] = true;
                            break;
                        }
                    }
                }
            }

            if (debugMessages)
            {
                Debug.Log($"Created {clusters.Count} independent clusters from {constraints.Count} constraints");
            }

            return clusters;
        }
        
        public static void ApplyNaiveGraphColouring(List<Constraint> constraints, bool debugMessages)
        {
            if (debugMessages)
            {
                Debug.Log($"Applying naive graph colouring to {constraints.Count} constraints...");
            }

            // Simple greedy graph colouring algorithm
            var colouredConstraints = new List<Constraint>();

            for (var i = 0; i < constraints.Count; i++)
            {
                var constraint = constraints[i];

                // Find which colours are already used by constraints sharing particles
                var usedcolours = new HashSet<int>();

                for (var j = 0; j < colouredConstraints.Count; j++)
                {
                    var other = colouredConstraints[j];

                    // Check if constraints share particles
                    if (constraint.ParticleA == other.ParticleA || constraint.ParticleA == other.ParticleB ||
                        constraint.ParticleB == other.ParticleA || constraint.ParticleB == other.ParticleB)
                    {
                        usedcolours.Add(other.ColourGroup);
                    }
                }

                // Assign the smallest available colour
                var colour = 0;
                while (usedcolours.Contains(colour))
                {
                    colour++;
                }

                constraint.ColourGroup = colour;
                colouredConstraints.Add(constraint);
            }

            // Update the constraints list
            constraints = colouredConstraints;

            // Count colours used
            var maxcolour = 0;
            foreach (var constraint in constraints)
            {
                maxcolour = Mathf.Max(maxcolour, constraint.ColourGroup);
            }

            Debug.Log($"Graph colouring complete: {maxcolour + 1} colour groups needed");
        }
    }
}
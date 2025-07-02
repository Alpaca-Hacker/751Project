using System.Collections.Generic;
using System.Linq;
using SoftBody.Scripts.Models;
using UnityEngine;

namespace SoftBody.Scripts
{
    
    /// <summary>
    /// Responsible for checking and fixing connectivity issues in soft body meshes.
    /// </summary>
    public static class Connectivity
    {
        public static void CheckNetworkConnectivity(List<Particle> particles, List<Constraint> constraints,
            SoftBodySettings settings)
        {
            // Build adjacency list
            var particleCount = particles.Count;
            var adjacency = new List<HashSet<int>>(particleCount);
            for (var i = 0; i < particleCount; i++)
            {
                adjacency.Add(new HashSet<int>());
            }

            foreach (var constraint in constraints)
            {
                adjacency[constraint.ParticleA].Add(constraint.ParticleB);
                adjacency[constraint.ParticleB].Add(constraint.ParticleA);
            }

            // Find connected components using BFS
            var visited = new bool[particleCount];
            var components = new List<List<int>>();

            for (var i = 0; i < particleCount; i++)
            {
                if (!visited[i])
                {
                    var component = new List<int>();
                    var queue = new Queue<int>();
                    queue.Enqueue(i);
                    visited[i] = true;

                    while (queue.Count > 0)
                    {
                        var current = queue.Dequeue();
                        component.Add(current);

                        foreach (var neighbor in adjacency[current])
                        {
                            if (!visited[neighbor])
                            {
                                visited[neighbor] = true;
                                queue.Enqueue(neighbor);
                            }
                        }
                    }

                    components.Add(component);
                }
            }

            if (settings.debugMessages)
            {
                Debug.Log($"CONNECTIVITY: Found {components.Count} separate components");
            }

            for (var i = 0; i < components.Count; i++)
            {
                if (settings.debugMessages)
                {
                    Debug.Log($"Component {i}: {components[i].Count} particles");
                    
                    if (components[i].Count < 10) // Log small components
                    {
                        Debug.Log($"Small component particles: [{string.Join(", ", components[i])}]");
                    }
                }
            }
            
            if (settings.autoFixConnectivity && components.Count > 1)
            {
                switch (settings.connectivityMethod)
                {
                    case SoftBodySettings.ConnectivityMethod.ProximityBased:
                        AddProximityConstraints(particles, constraints, settings);
                        break;
                    case SoftBodySettings.ConnectivityMethod.BridgeConstraints:
                        ForceConnectivity(particles, constraints, components, settings);
                        break;
                    case SoftBodySettings.ConnectivityMethod.Hybrid:
                        HybridConnectivity(particles, constraints, components, settings);
                        break;
                    default:
                        Debug.LogWarning("Unknown connectivity fix method! No action taken.");
                        break;
                }
            }
        }

        private static void ForceConnectivity(List<Particle> particles, List<Constraint> constraints,
            List<List<int>> components, SoftBodySettings settings)
        {
            if (components.Count <= 1) return; // Already connected
            if (settings.debugMessages)
            {
                Debug.Log($"Connecting {components.Count} disconnected components...");
            }

            // Connect each component to its nearest neighbor component
            for (var i = 0; i < components.Count; i++)
            {
                for (var j = i + 1; j < components.Count; j++)
                {
                    // Find closest points between components i and j
                    var minDistance = float.MaxValue;
                    var bestParticleI = -1;
                    var bestParticleJ = -1;

                    foreach (var particleI in components[i])
                    {
                        foreach (var particleJ in components[j])
                        {
                            var distance = Vector3.Distance(particles[particleI].Position,
                                particles[particleJ].Position);
                            if (distance < minDistance)
                            {
                                minDistance = distance;
                                bestParticleI = particleI;
                                bestParticleJ = particleJ;
                            }
                        }
                    }

                    // Add bridge constraint between the closest points
                    if (bestParticleI != -1 && bestParticleJ != -1)
                    {
                        SoftBodyGenerator.AddConstraintWithValidation(particles, constraints, bestParticleI, bestParticleJ,
                            settings.structuralCompliance * 0.5f); // Slightly more flexible bridge
                        if (settings.debugMessages)
                        {
                            Debug.Log(
                                $"Added bridge constraint between components {i} and {j}: particles {bestParticleI} <-> {bestParticleJ} (distance: {minDistance:F3})");
                        }
                    }
                }
            }
        }

        private static void AddProximityConstraints(List<Particle> particles, List<Constraint> constraints,
            SoftBodySettings settings)
        {
            if (settings.debugMessages)
            {
                Debug.Log("Adding proximity-based constraints...");
            }

            var maxConnectionDistance = CalculateOptimalConnectionDistance(constraints, settings.debugMessages);

            var proximityConstraintsAdded = 0;

            for (var i = 0; i < particles.Count; i++)
            {
                for (var j = i + 1; j < particles.Count; j++)
                {
                    var distance = Vector3.Distance(particles[i].Position, particles[j].Position);

                    if (distance <= maxConnectionDistance)
                    {
                        // Check if constraint already exists
                        var constraintExists = constraints.Any(c =>
                            (c.ParticleA == i && c.ParticleB == j) ||
                            (c.ParticleA == j && c.ParticleB == i));

                        if (!constraintExists)
                        {
                            SoftBodyGenerator.AddConstraintWithValidation(particles, constraints, i, j,
                                settings.structuralCompliance * 2f); // More flexible proximity constraints
                            proximityConstraintsAdded++;
                        }
                    }
                }
            }

            if (settings.debugMessages)
            {
                Debug.Log($"Added {proximityConstraintsAdded} proximity constraints");
            }
        }

        private static float CalculateOptimalConnectionDistance(List<Constraint> constraints, bool debugMessages)
        {
            if (constraints.Count == 0) return 0.1f;

            // Calculate average existing constraint length
            var totalLength = 0f;
            foreach (var constraint in constraints)
            {
                totalLength += constraint.RestLength;
            }

            var avgConstraintLength = totalLength / constraints.Count;

            // Connection distance should be slightly larger than average constraint length
            var connectionDistance = avgConstraintLength * 1.5f;
            if (debugMessages)
            {
                Debug.Log(
                    $"Calculated optimal connection distance: {connectionDistance:F4} (avg constraint: {avgConstraintLength:F4})");
            }

            return connectionDistance;
        }

        private static void HybridConnectivity(List<Particle> particles, List<Constraint> constraints,
            List<List<int>> components, SoftBodySettings settings)
        {
            ForceConnectivity(particles, constraints, components, settings);
            AddProximityConstraints(particles, constraints, settings);
        }
    }
}
using System.Collections.Generic;
using SoftBody.Scripts.Models;

namespace SoftBody.Scripts.Algorithms.GraphColouring
{
    public class GreedyColouringAlgorithm : IGraphColouringAlgorithm
    {
        public void ApplyColouring(List<Constraint> constraints, int particleCount)
        {
            // Build adjacency list from constraints
            var constraintAdjacency = BuildConstraintAdjacencyList(constraints, particleCount);
            
            // Apply greedy graph colouring
            ApplyGreedyColouring(constraints, constraintAdjacency);
        }
        
        private List<HashSet<int>> BuildConstraintAdjacencyList(List<Constraint> constraints, int particleCount)
        {
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
            
            var adjacency = new List<HashSet<int>>(constraints.Count);
            for (var i = 0; i < constraints.Count; i++)
            {
                adjacency.Add(new HashSet<int>());
            }
            
            for (var i = 0; i < constraints.Count; i++)
            {
                var c1 = constraints[i];
                
                var connectedConstraints = new HashSet<int>();
                connectedConstraints.UnionWith(particleToConstraints[c1.ParticleA]);
                connectedConstraints.UnionWith(particleToConstraints[c1.ParticleB]);
                connectedConstraints.Remove(i); // Don't include self
                
                adjacency[i] = connectedConstraints;
            }
            
            return adjacency;
        }
        
        private static void ApplyGreedyColouring(List<Constraint> constraints, List<HashSet<int>> adjacency)
        {
            var colours = new int[constraints.Count];
            for (var i = 0; i < colours.Length; i++)
            {
                colours[i] = -1; // Uncoloured
            }
            
            // Colour constraints one by one
            for (var i = 0; i < constraints.Count; i++)
            {
                // Find colours used by adjacent constraints
                var usedcolours = new HashSet<int>();
                foreach (var neighbor in adjacency[i])
                {
                    if (colours[neighbor] != -1)
                    {
                        usedcolours.Add(colours[neighbor]);
                    }
                }
                
                // Find first available colour
                var colour = 0;
                while (usedcolours.Contains(colour))
                {
                    colour++;
                }
                
                colours[i] = colour;
                
                // Update the constraint
                var constraint = constraints[i];
                constraint.ColourGroup = colour;
                constraints[i] = constraint;
            }
        }
    }
}
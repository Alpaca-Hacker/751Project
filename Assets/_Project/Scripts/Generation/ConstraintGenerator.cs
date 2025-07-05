using System.Collections.Generic;
using System.Linq;
using SoftBody.Scripts.Models;
using UnityEngine;

namespace SoftBody.Scripts.Generation
{
    public static class ConstraintGenerator
    {
        public static List<Constraint> GenerateFromMesh(List<Particle> particles, int[] triangles, SoftBodySettings settings)
        {
            var constraints = new List<Constraint>();

            if (triangles.Length % 3 != 0)
            {
                Debug.LogError($"Invalid triangle array length: {triangles.Length}");
                return constraints;
            }

         //   if (settings.debugMessages)
        //    {
                Debug.Log($"Generating constraints from mesh with {triangles.Length / 3} triangles");
        //    }

            // Extract edges and create structural constraints
            var edges = ExtractEdgesFromTriangles(triangles, particles.Count, settings.debugMessages);
            var triangleData = ExtractTriangleData(triangles);
            
            // Create structural constraints from edges
            CreateStructuralConstraints(particles, constraints, edges, settings.structuralCompliance);
            
            Debug.Log($"After structural: {constraints.Count} constraints");

            // Add bending constraints if requested
            if (settings.constraintDensityMultiplier > 1f)
            {
                var beforeBending = constraints.Count;
                GenerateBendingConstraints(particles, constraints, triangleData, settings);
                Debug.Log($"Added {constraints.Count - beforeBending} bending constraints");
            }

            // Filter constraints if enabled
            if (settings.enableConstraintFiltering)
            {
                var beforeFiltering = constraints.Count;
                FilterConstraints(particles, constraints, settings);
                Debug.Log($"After filtering: {constraints.Count} constraints (was {beforeFiltering})");
            }

            // Analyze the results
           // if (settings.debugMessages)
           // {
                AnalyzeConstraintGeneration(particles, constraints, triangles);
           // }

            return constraints;
        }

        private static HashSet<(int, int)> ExtractEdgesFromTriangles(int[] triangles, int particleCount, bool debugMessages)
        {
            var edges = new HashSet<(int, int)>();
            var invalidTriangles = 0;
            var edgeCount = 0;
            var duplicateEdges = 0;
            
            var maxTriangleIndex = triangles.Length > 0 ? triangles.Max() : -1;
            if (maxTriangleIndex >= particleCount)
            {
                Debug.LogError($"CRITICAL BUG: Triangle references vertex {maxTriangleIndex} but only {particleCount} particles exist!");
                Debug.LogError($"Triangle indices range: {triangles.Min()}-{triangles.Max()}, Particle count: {particleCount}");
        
                // Show some example bad triangles
                for (var i = 0; i < triangles.Length; i += 3)
                {
                    var a = triangles[i];
                    var b = triangles[i + 1];
                    var c = triangles[i + 2];
            
                    if (a >= particleCount || b >= particleCount || c >= particleCount)
                    {
                        Debug.LogError($"Bad triangle at index {i/3}: ({a}, {b}, {c}) - max allowed: {particleCount-1}");
                        if (invalidTriangles++ > 5) break; // Don't spam too much
                    }
                }
        
                return edges; // Return empty to prevent crash
            }

            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = triangles[i];
                var b = triangles[i + 1];
                var c = triangles[i + 2];

                // Validate triangle indices
                if (a < 0 || a >= particleCount || b < 0 || b >= particleCount || c < 0 || c >= particleCount)
                {
                    if (debugMessages)
                    {
                        Debug.LogError($"Invalid triangle indices: {a}, {b}, {c} (max: {particleCount - 1})");
                    }
                    invalidTriangles++;
                    continue;
                }

                // Skip degenerate triangles
                if (a == b || b == c || c == a)
                {
                    if (debugMessages)
                    {
                        Debug.LogWarning($"Degenerate triangle skipped: {a}, {b}, {c}");
                    }
                    invalidTriangles++;
                    continue;
                }

                // Count edges before adding
                var edgesBefore = edges.Count;
                AddEdge(edges, a, b);
                AddEdge(edges, b, c);
                AddEdge(edges, c, a);
                var edgesAfter = edges.Count;

                edgeCount += 3;
                duplicateEdges += 3 - (edgesAfter - edgesBefore);
            }

            if (debugMessages)
            {
                Debug.Log($"Edge extraction: {edgeCount} total edges, {edges.Count} unique edges, {duplicateEdges} duplicates");
                if (invalidTriangles > 0)
                {
                    Debug.LogWarning($"Skipped {invalidTriangles} invalid triangles during edge extraction");
                }
            }

            return edges;
        }

        private static List<(int, int, int)> ExtractTriangleData(int[] triangles)
        {
            var triangleData = new List<(int, int, int)>();
            
            for (var i = 0; i < triangles.Length; i += 3)
            {
                triangleData.Add((triangles[i], triangles[i + 1], triangles[i + 2]));
            }

            return triangleData;
        }

        private static void CreateStructuralConstraints(List<Particle> particles, List<Constraint> constraints,
            HashSet<(int, int)> edges, float compliance)
        {
            var validConstraints = 0;
            var invalidConstraints = 0;

            foreach (var edge in edges)
            {
                if (AddConstraintWithValidation(particles, constraints, edge.Item1, edge.Item2, compliance))
                {
                    validConstraints++;
                }
                else
                {
                    invalidConstraints++;
                }
            }

            Debug.Log($"Structural constraints: {validConstraints} created, {invalidConstraints} invalid");
        }

        private static void GenerateBendingConstraints(List<Particle> particles, List<Constraint> constraints, 
            List<(int, int, int)> triangles, SoftBodySettings settings)
        {
            if (settings.debugMessages)
            {
                Debug.Log("Generating bending constraints...");
            }

            var bendConstraintsBefore = constraints.Count;

            // Map each edge to the triangles that use it
            var edgeToTriangles = new Dictionary<(int, int), List<int>>();

            for (var i = 0; i < triangles.Count; i++)
            {
                var (a, b, c) = triangles[i];
                AddEdgeTriangleMapping(edgeToTriangles, a, b, i);
                AddEdgeTriangleMapping(edgeToTriangles, b, c, i);
                AddEdgeTriangleMapping(edgeToTriangles, c, a, i);
            }

            // Create bending constraints for shared edges
            foreach (var kvp in edgeToTriangles)
            {
                if (kvp.Value.Count == 2) // Edge shared by exactly 2 triangles
                {
                    var tri1 = triangles[kvp.Value[0]];
                    var tri2 = triangles[kvp.Value[1]];
                    var edge = kvp.Key;

                    // Find the two vertices not on the shared edge
                    var v1 = GetThirdVertex(tri1, edge);
                    var v2 = GetThirdVertex(tri2, edge);

                    if (v1 != -1 && v2 != -1)
                    {
                        // This creates a "bending" constraint across the shared edge
                        AddConstraintWithValidation(particles, constraints, v1, v2, settings.bendCompliance);
                    }
                }
            }

            var bendConstraintsAdded = constraints.Count - bendConstraintsBefore;
            if (settings.debugMessages)
            {
                Debug.Log($"Added {bendConstraintsAdded} bending constraints");
            }
        }

        private static void AddEdgeTriangleMapping(Dictionary<(int, int), List<int>> edgeToTriangles,
            int a, int b, int triangleIndex)
        {
            // Ensure consistent edge ordering
            if (a > b) (a, b) = (b, a);

            var edge = (a, b);
            if (!edgeToTriangles.ContainsKey(edge))
            {
                edgeToTriangles[edge] = new List<int>();
            }

            edgeToTriangles[edge].Add(triangleIndex);
        }

        private static int GetThirdVertex((int, int, int) triangle, (int, int) edge)
        {
            var (a, b, c) = triangle;
            var (edgeA, edgeB) = edge;

            // Find the vertex that's not part of the edge
            if (a != edgeA && a != edgeB) return a;
            if (b != edgeA && b != edgeB) return b;
            if (c != edgeA && c != edgeB) return c;

            return -1; // Should never happen if data is valid
        }

        private static void FilterConstraints(List<Particle> particles, List<Constraint> constraints, SoftBodySettings settings)
        {
            var originalCount = constraints.Count;
            if (settings.debugMessages)
            {
                Debug.Log($"Starting constraint filtering: {originalCount} constraints");
            }

            // 1. Categorize constraints by length
            var constraintData = new List<(Constraint constraint, float length, bool isStructural)>();

            foreach (var constraint in constraints)
            {
                var length = Vector3.Distance(particles[constraint.ParticleA].Position,
                    particles[constraint.ParticleB].Position);

                // Constraints under a threshold are likely structural (mesh edges)
                var isStructural = length <= 0.100f; // Adjust this threshold as needed

                constraintData.Add((constraint, length, isStructural));
            }

            // 2. Priority filtering: Keep structural constraints, filter others
            var filteredConstraints = new List<Constraint>();
            var constraintsPerParticle = new int[particles.Count];

            // Sort: structural first, then by length (shortest first)
            constraintData.Sort((a, b) =>
            {
                if (a.isStructural != b.isStructural)
                    return b.isStructural.CompareTo(a.isStructural); // Structural first
                return a.length.CompareTo(b.length); // Then by length
            });

            foreach (var (constraint, length, isStructural) in constraintData)
            {
                var particleA = constraint.ParticleA;
                var particleB = constraint.ParticleB;

                // Always keep structural constraints
                if (isStructural)
                {
                    filteredConstraints.Add(constraint);
                    constraintsPerParticle[particleA]++;
                    constraintsPerParticle[particleB]++;
                }
                // For non-structural, apply limits
                else if (constraintsPerParticle[particleA] < settings.maxConstraintsPerParticle &&
                         constraintsPerParticle[particleB] < settings.maxConstraintsPerParticle &&
                         length <= settings.maxConstraintLength)
                {
                    filteredConstraints.Add(constraint);
                    constraintsPerParticle[particleA]++;
                    constraintsPerParticle[particleB]++;
                }
            }

            // 3. Replace the constraint list
            constraints.Clear();
            constraints.AddRange(filteredConstraints);

            var finalCount = constraints.Count;
            var reductionPercent = (1f - (float)finalCount / originalCount) * 100f;

            if (settings.debugMessages)
            {
                Debug.Log($"Constraint filtering complete: {originalCount} → {finalCount} ({reductionPercent:F1}% reduction)");
            }

            // Analyze final distribution
            var finalConstraintsPerParticle = new int[particles.Count];
            foreach (var constraint in constraints)
            {
                finalConstraintsPerParticle[constraint.ParticleA]++;
                finalConstraintsPerParticle[constraint.ParticleB]++;
            }

            var avgFinal = finalConstraintsPerParticle.Average();
            var maxFinal = finalConstraintsPerParticle.Max();
            if (settings.debugMessages)
            {
                Debug.Log($"Final constraints per particle: Avg={avgFinal:F1}, Max={maxFinal}");
            }
        }

        private static void AnalyzeConstraintGeneration(List<Particle> particles, List<Constraint> constraints, int[] triangles)
        {
            Debug.Log("=== CONSTRAINT GENERATION ANALYSIS ===");

            // Count constraint types
            var edgeConstraints = new HashSet<(int, int)>();

            // Analyze triangle edges vs total constraints
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = triangles[i];
                var b = triangles[i + 1];
                var c = triangles[i + 2];

                AddEdgeToSet(edgeConstraints, a, b);
                AddEdgeToSet(edgeConstraints, b, c);
                AddEdgeToSet(edgeConstraints, c, a);
            }

            Debug.Log($"Surface mesh: {particles.Count} vertices, {triangles.Length / 3} triangles");
            Debug.Log($"Expected edge constraints from mesh: {edgeConstraints.Count}");
            Debug.Log($"Actual total constraints: {constraints.Count}");
            Debug.Log($"Constraint multiplier: {(float)constraints.Count / edgeConstraints.Count:F1}x");

            // Analyze constraint distances
            var distances = new List<float>();
            foreach (var constraint in constraints)
            {
                var dist = Vector3.Distance(particles[constraint.ParticleA].Position,
                    particles[constraint.ParticleB].Position);
                distances.Add(dist);
            }

            distances.Sort();
            var shortConstraints = distances.Count(d => d < distances[distances.Count / 2]); // Below median
            var longConstraints = distances.Count - shortConstraints;

            Debug.Log($"Constraint distances: Min={distances[0]:F3}, Max={distances.Last():F3}, Median={distances[distances.Count / 2]:F3}");
            Debug.Log($"Short constraints: {shortConstraints}, Long constraints: {longConstraints}");
        }

        private static void AddEdgeToSet(HashSet<(int, int)> edges, int a, int b)
        {
            if (a > b) (a, b) = (b, a);
            edges.Add((a, b));
        }

        // Helper methods
        private static void AddEdge(HashSet<(int, int)> edges, int a, int b)
        {
            // Ensure consistent ordering to avoid duplicates
            if (a > b) (a, b) = (b, a);
            edges.Add((a, b));
        }

        public static bool AddConstraintWithValidation(List<Particle> particles, List<Constraint> constraints, 
            int a, int b, float compliance)
        {
            if (a == b || a < 0 || a >= particles.Count || b < 0 || b >= particles.Count)
                return false;

            var restLength = Vector3.Distance(particles[a].Position, particles[b].Position);
            if (restLength < 0.001f)
            {
                Debug.LogWarning($"Constraint between particles {a} and {b} has very small rest length: {restLength}");
                return false;
            }

            constraints.Add(new Constraint
            {
                ParticleA = a,
                ParticleB = b,
                RestLength = restLength,
                Compliance = compliance,
                Lambda = 0f,
                ColourGroup = 0
            });

            return true;
        }

        public static void AddConstraint(List<Particle> particles, List<Constraint> constraints, int a, int b, float compliance)
        {
            var restLength = Vector3.Distance(particles[a].Position, particles[b].Position);

            if (restLength < 0.001f)
            {
                return;
            }

            constraints.Add(new Constraint
            {
                ParticleA = a,
                ParticleB = b,
                RestLength = restLength,
                Compliance = compliance,
                Lambda = 0f,
                ColourGroup = 0
            });
        }
    }
}
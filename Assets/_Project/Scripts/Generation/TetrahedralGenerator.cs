using System.Collections.Generic;
using System.Linq;
using SoftBody.Scripts.Models;
using SoftBody.Scripts.Utilities;
using UnityEngine;

namespace SoftBody.Scripts.Generation
{
    public class TetrahedralGenerator : ISoftBodyGenerator
    {
        public GenerationResult Generate(SoftBodySettings settings, Transform transform)
        {
            var mesh = settings.inputMesh;
            if (mesh == null)
            {
                Debug.LogError("Tetrahedral generator requires input mesh!");
                return new GenerationResult();
            }

            if (settings.debugMessages)
            {
                Debug.Log($"Generating tetrahedral soft body from mesh: {mesh.name}");
            }

            // Process surface mesh
            var (weldedVertices, weldedTriangles, vertexMapping) =
                MeshUtilities.WeldVertices(mesh.vertices, mesh.triangles, settings);

            // Step 1: Create ALL particles (surface + interior) FIRST
            var particles = new List<Particle>();
            var surfaceParticleIndices = new List<int>();

            // Add surface particles
            for (var i = 0; i < weldedVertices.Length; i++)
            {
                var worldPos = transform.TransformPoint(weldedVertices[i]);
                particles.Add(new Particle
                {
                    Position = worldPos,
                    Velocity = Vector4.zero,
                    Force = Vector4.zero,
                    InvMass = 1f / settings.mass
                });
                surfaceParticleIndices.Add(i);
            }

            // Generate and add interior particles
            var interiorPoints = InteriorPointGenerator.GenerateForMesh(
                weldedVertices, weldedTriangles, settings.interiorPointDensity, settings.debugMessages);

            var interiorParticleIndices = new List<int>();
            foreach (var point in interiorPoints)
            {
                var worldPos = transform.TransformPoint(point);
                particles.Add(new Particle
                {
                    Position = worldPos,
                    Velocity = Vector4.zero,
                    Force = Vector4.zero,
                    InvMass = 1f / (settings.mass * 2f) // Interior particles are heavier
                });
                interiorParticleIndices.Add(particles.Count - 1);
            }

            // Step 2: Create constraints (matching original implementation)
            var constraints = new List<Constraint>();

            // A) Surface constraints (flexible - for surface detail)
            CreateSurfaceConstraints(weldedTriangles, constraints,
                settings.structuralCompliance * 5f, settings.debugMessages);

            // B) Interior structural constraints (stiffer - for overall shape)
            CreateInteriorConstraints(interiorParticleIndices, particles, constraints,
                settings.structuralCompliance * 0.1f); // Much stiffer interior

            // C) Surface-to-interior connections (medium stiffness)
            ConnectSurfaceToInterior(surfaceParticleIndices, interiorParticleIndices, particles,
                constraints, settings.structuralCompliance);

            // Step 3: Create volume constraints
            var volumeConstraints = new List<VolumeConstraint>();
            CreateVolumeConstraintsFromTetrahedralization(surfaceParticleIndices, interiorParticleIndices,
                particles, volumeConstraints, settings);

            // E) Fallback volume constraints if needed
            if (volumeConstraints.Count == 0)
            {
                if (settings.debugMessages)
                {
                    Debug.Log("No volume constraints from tetrahedralization, using fallback");
                }

                CreateSimpleFallbackVolumeConstraints(surfaceParticleIndices, particles, volumeConstraints, settings);
            }

            // Step 4: Handle UVs
            Vector2[] expandedUVs = null;
            if (mesh.uv != null && mesh.uv.Length > 0)
            {
                var weldedUVs =
                    MeshUtilities.RemapUVsAfterWelding(mesh.uv, vertexMapping, weldedVertices.Length, settings);
                expandedUVs = new Vector2[particles.Count];

                // Copy surface UVs
                for (var i = 0; i < weldedVertices.Length; i++)
                {
                    expandedUVs[i] = weldedUVs[i];
                }

                // Interpolate UVs for interior particles
                for (var i = weldedVertices.Length; i < particles.Count; i++)
                {
                    var closestSurfaceIdx =
                        FindClosestSurfaceParticle(particles[i].Position, particles, weldedVertices.Length);
                    expandedUVs[i] = weldedUVs[closestSurfaceIdx];
                }
            }

            if (settings.debugMessages)
            {
                Debug.Log($"Tetrahedralized soft body: {particles.Count} particles, " +
                          $"{constraints.Count} constraints, {volumeConstraints.Count} volume constraints");
            }

            return new GenerationResult
            {
                Particles = particles,
                Constraints = constraints,
                VolumeConstraints = volumeConstraints,
                Indices = weldedTriangles.ToList(),
                UVs = expandedUVs
            };
        }

        // Helper methods
        private void CreateSurfaceConstraints(int[] triangles, List<Constraint> constraints,
            float compliance, bool debugMessages)
        {
            var edges = new HashSet<(int, int)>();

            // Extract edges from surface triangles
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = triangles[i];
                var b = triangles[i + 1];
                var c = triangles[i + 2];

                AddEdge(edges, a, b);
                AddEdge(edges, b, c);
                AddEdge(edges, c, a);
            }

            // Create constraints for each edge (WITHOUT calculating rest length yet!)
            foreach (var edge in edges)
            {
                constraints.Add(new Constraint
                {
                    ParticleA = edge.Item1,
                    ParticleB = edge.Item2,
                    RestLength = 0f, // Will be calculated later in SoftBodyGenerator
                    Compliance = compliance,
                    Lambda = 0f,
                    ColourGroup = 0
                });
            }

            if (debugMessages)
            {
                Debug.Log($"Created {edges.Count} surface constraints");
            }
        }

        private void CreateVolumeConstraintsFromTetrahedralization(List<int> surfaceIndices,
            List<int> interiorIndices, List<Particle> particles, List<VolumeConstraint> volumeConstraints,
            SoftBodySettings settings)
        {
            if (settings.debugMessages)
            {
                Debug.Log(
                    $"=== Starting volume constraint creation with {interiorIndices.Count} interior particles ===");
            }

            if (interiorIndices.Count == 0)
            {
                Debug.LogWarning("No interior particles for volume constraints");
                return;
            }

            var maxVolumeConstraints = settings.maxVolumeConstraints;
            var constraintsCreated = 0;

            foreach (var interiorIdx in interiorIndices)
            {
                if (constraintsCreated >= maxVolumeConstraints) break;

                // Find the closest surface points
                var closestSurface = surfaceIndices
                    .Select(idx => new
                    {
                        index = idx,
                        distance = Vector3.Distance(particles[interiorIdx].Position, particles[idx].Position)
                    })
                    .OrderBy(x => x.distance)
                    .Take(8)
                    .ToList();

                // Create multiple tetrahedra with different surface point combinations
                for (var i = 0; i < closestSurface.Count - 2 && constraintsCreated < maxVolumeConstraints; i++)
                {
                    for (var j = i + 1; j < closestSurface.Count - 1 && constraintsCreated < maxVolumeConstraints; j++)
                    {
                        for (var k = j + 1; k < closestSurface.Count && constraintsCreated < maxVolumeConstraints; k++)
                        {
                            var p1 = closestSurface[i].index;
                            var p2 = closestSurface[j].index;
                            var p3 = closestSurface[k].index;
                            var p4 = interiorIdx;

                            // Calculate tetrahedron volume
                            var pos1 = particles[p1].Position;
                            var pos2 = particles[p2].Position;
                            var pos3 = particles[p3].Position;
                            var pos4 = particles[p4].Position;

                            var v1 = pos1 - pos4;
                            var v2 = pos2 - pos4;
                            var v3 = pos3 - pos4;
                            var restVolume = Vector3.Dot(v1, Vector3.Cross(v2, v3)) / 6.0f;

                            // Only create if volume is reasonable
                            if (Mathf.Abs(restVolume) > 0.00001f)
                            {
                                volumeConstraints.Add(new VolumeConstraint
                                {
                                    P1 = p1, P2 = p2, P3 = p3, P4 = p4,
                                    RestVolume = Mathf.Abs(restVolume),
                                    Compliance = settings.volumeCompliance,
                                    Lambda = 0,
                                    PressureMultiplier = 1f
                                });
                                constraintsCreated++;
                            }
                        }
                    }
                }
            }

            if (settings.debugMessages)
            {
                Debug.Log($"Created {constraintsCreated} volume constraints");
            }
        }

        private static void CreateInteriorConstraints(List<int> interiorIndices, List<Particle> particles,
            List<Constraint> constraints, float compliance)
        {
            var positions = particles.Select(p => p.Position).ToList();
            var maxDistance = MeshUtilities.CalculateAverageEdgeLength(positions) * 1.5f;

            for (var i = 0; i < interiorIndices.Count; i++)
            {
                for (var j = i + 1; j < interiorIndices.Count; j++)
                {
                    var idx1 = interiorIndices[i];
                    var idx2 = interiorIndices[j];

                    var distance = Vector3.Distance(particles[idx1].Position, particles[idx2].Position);
                    if (distance < maxDistance)
                    {
                        constraints.Add(CreateConstraint(idx1, idx2, compliance));
                    }
                }
            }
        }

        private static void ConnectSurfaceToInterior(List<int> surfaceIndices, List<int> interiorIndices,
            List<Particle> particles, List<Constraint> constraints, float baseCompliance)
        {
            var positions = particles.Select(p => p.Position).ToList();
            var maxConnectionDistance = MeshUtilities.CalculateAverageEdgeLength(positions) * 2f;

            foreach (var surfaceIdx in surfaceIndices)
            {
                var surfacePos = particles[surfaceIdx].Position;
                var connectionsForThisVertex = 0;
                const int maxConnectionsPerSurfaceVertex = 3;

                var distances = new List<(int index, float distance)>();
                foreach (var interiorIdx in interiorIndices)
                {
                    var distance = Vector3.Distance(surfacePos, particles[interiorIdx].Position);
                    if (distance < maxConnectionDistance)
                    {
                        distances.Add((interiorIdx, distance));
                    }
                }

                distances.Sort((a, b) => a.distance.CompareTo(b.distance));

                foreach (var (interiorIdx, _) in distances)
                {
                    if (connectionsForThisVertex >= maxConnectionsPerSurfaceVertex) break;

                    constraints.Add(CreateConstraint(surfaceIdx, interiorIdx, baseCompliance));
                    connectionsForThisVertex++;
                }
            }
        }

        private static int FindClosestSurfaceParticle(Vector3 interiorPos, List<Particle> particles, int surfaceCount)
        {
            var minDistance = float.MaxValue;
            var closestIndex = 0;

            for (var i = 0; i < surfaceCount; i++)
            {
                var distance = Vector3.Distance(interiorPos, particles[i].Position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }

        // Helper methods
        private static void AddEdge(HashSet<(int, int)> edges, int a, int b)
        {
            if (a > b) (a, b) = (b, a);
            edges.Add((a, b));
        }

        private static Constraint CreateConstraint(int a, int b, float compliance)
        {
            return new Constraint
            {
                ParticleA = a,
                ParticleB = b,
                RestLength = 0f, // Will be calculated later
                Compliance = compliance,
                Lambda = 0f,
                ColourGroup = 0
            };
        }

        private void CreateSimpleFallbackVolumeConstraints(List<int> surfaceParticleIndices,
            List<Particle> particles, List<VolumeConstraint> volumeConstraints, SoftBodySettings settings)
        {
            if (settings.debugMessages)
            {
                Debug.Log("Creating simple fallback volume constraints");
            }

            // Find the centroid of all particles
            var centroid = Vector3.zero;
            foreach (var particle in particles)
            {
                centroid += particle.Position;
            }

            centroid /= particles.Count;

            // Add a pseudo-particle at the centroid (or use existing closest particle)
            var centroidParticleIdx = -1;
            var minDistToCentroid = float.MaxValue;

            for (var i = 0; i < particles.Count; i++)
            {
                var dist = Vector3.Distance(particles[i].Position, centroid);
                if (dist < minDistToCentroid)
                {
                    minDistToCentroid = dist;
                    centroidParticleIdx = i;
                }
            }

            if (centroidParticleIdx == -1)
            {
                Debug.LogError("Failed to find centroid particle for fallback volume constraints");
                return;
            }

            // Create tetrahedra using surface triangles and the centroid
            var constraintsCreated = 0;
            var maxConstraints = Mathf.Min(settings.maxVolumeConstraints, surfaceParticleIndices.Count / 3);

            // Sample surface particles to create tetrahedra
            for (var i = 0; i < surfaceParticleIndices.Count - 2; i += 3)
            {
                if (constraintsCreated >= maxConstraints) break;

                for (var j = i + 1; j < surfaceParticleIndices.Count - 1; j += 3)
                {
                    if (constraintsCreated >= maxConstraints) break;

                    for (var k = j + 1; k < surfaceParticleIndices.Count; k += 3)
                    {
                        if (constraintsCreated >= maxConstraints) break;

                        var p1 = surfaceParticleIndices[i];
                        var p2 = surfaceParticleIndices[j];
                        var p3 = surfaceParticleIndices[k];
                        var p4 = centroidParticleIdx;

                        // Skip if any indices are the same
                        if (p1 == p4 || p2 == p4 || p3 == p4) continue;

                        // Calculate tetrahedron volume
                        var pos1 = particles[p1].Position;
                        var pos2 = particles[p2].Position;
                        var pos3 = particles[p3].Position;
                        var pos4 = particles[p4].Position;

                        var v1 = pos1 - pos4;
                        var v2 = pos2 - pos4;
                        var v3 = pos3 - pos4;
                        var restVolume = Vector3.Dot(v1, Vector3.Cross(v2, v3)) / 6.0f;

                        if (Mathf.Abs(restVolume) > 0.001f)
                        {
                            volumeConstraints.Add(new VolumeConstraint
                            {
                                P1 = p1,
                                P2 = p2,
                                P3 = p3,
                                P4 = p4,
                                RestVolume = Mathf.Abs(restVolume),
                                Compliance = settings.volumeCompliance,
                                Lambda = 0,
                                PressureMultiplier = 1f
                            });
                            constraintsCreated++;
                        }
                    }
                }
            }

            if (settings.debugMessages)
            {
                Debug.Log($"Created {constraintsCreated} fallback volume constraints");
            }
        }
    }
}
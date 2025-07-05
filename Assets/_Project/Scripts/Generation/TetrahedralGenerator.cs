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

            // Create surface particles
            var particles = new List<Particle>();
            var surfaceParticleIndices = CreateSurfaceParticles(weldedVertices, particles, settings, transform);

            // Generate interior points
            var interiorPoints = InteriorPointGenerator.GenerateForMesh(
                weldedVertices, weldedTriangles, settings.interiorPointDensity, settings.debugMessages);

            var interiorParticleIndices = CreateInteriorParticles(interiorPoints, particles, settings, transform);

            // *** CRITICAL: Generate constraints AFTER all particles are created ***
            // For surface constraints, we need to pass only the surface triangle data
            var surfaceConstraints = ConstraintGenerator.GenerateFromMesh(
                particles.Take(surfaceParticleIndices.Count).ToList(), // Only surface particles
                weldedTriangles,
                settings);

            // Add interior and connection constraints
            var allConstraints = new List<Constraint>(surfaceConstraints);
            AddInteriorConstraints(interiorParticleIndices, particles, allConstraints, settings);
            AddSurfaceToInteriorConstraints(surfaceParticleIndices, interiorParticleIndices, particles, allConstraints,
                settings);

            // Generate volume constraints
            var volumeConstraints = CreateVolumeConstraints(surfaceParticleIndices, interiorParticleIndices,
                particles, settings);

            // Handle UVs
            Vector2[] expandedUVs = null;
            if (mesh.uv != null && mesh.uv.Length > 0)
            {
                var weldedUVs =
                    MeshUtilities.RemapUVsAfterWelding(mesh.uv, vertexMapping, weldedVertices.Length, settings);
                expandedUVs = ExpandUVsForInteriorParticles(weldedUVs, particles.Count, weldedVertices.Length,
                    particles, settings);
            }

            return new GenerationResult
            {
                Particles = particles,
                Constraints = allConstraints,
                VolumeConstraints = volumeConstraints,
                Indices = weldedTriangles.ToList(),
                UVs = expandedUVs
            };
        }

// Add these helper methods:
        private void AddInteriorConstraints(List<int> interiorIndices, List<Particle> particles,
            List<Constraint> constraints, SoftBodySettings settings)
        {
            // Move CreateInteriorConstraints logic here
            CreateInteriorConstraints(interiorIndices, particles, constraints, settings.structuralCompliance * 0.1f);
        }

        private void AddSurfaceToInteriorConstraints(List<int> surfaceIndices, List<int> interiorIndices,
            List<Particle> particles, List<Constraint> constraints, SoftBodySettings settings)
        {
            // Move ConnectSurfaceToInterior logic here
            ConnectSurfaceToInterior(surfaceIndices, interiorIndices, particles, constraints,
                settings.structuralCompliance);
        }

        private List<int> CreateSurfaceParticles(Vector3[] vertices, List<Particle> particles,
            SoftBodySettings settings, Transform transform)
        {
            var surfaceIndices = new List<int>();

            for (var i = 0; i < vertices.Length; i++)
            {
                var worldPos = transform.TransformPoint(vertices[i]);
                particles.Add(new Particle
                {
                    Position = worldPos,
                    Velocity = Vector4.zero,
                    Force = Vector4.zero,
                    InvMass = 1f / settings.mass
                });
                surfaceIndices.Add(i);
            }

            return surfaceIndices;
        }

        private List<int> CreateInteriorParticles(List<Vector3> interiorPoints, List<Particle> particles,
            SoftBodySettings settings, Transform transform)
        {
            var interiorIndices = new List<int>();

            foreach (var point in interiorPoints)
            {
                var worldPos = transform.TransformPoint(point);
                particles.Add(new Particle
                {
                    Position = worldPos,
                    Velocity = Vector4.zero,
                    Force = Vector4.zero,
                    InvMass = 1f / (settings.mass * 2f) // Interior points can be heavier
                });
                interiorIndices.Add(particles.Count - 1);
            }

            return interiorIndices;
        }

        private List<Constraint> CreateTetrahedralConstraints(List<int> surfaceIndices, List<int> interiorIndices,
            List<Particle> particles, int[] triangles, SoftBodySettings settings)
        {
            var constraints = ConstraintGenerator.GenerateFromMesh(particles, triangles, settings);

            CreateInteriorConstraints(interiorIndices, particles, constraints, settings.structuralCompliance * 0.1f);
            ConnectSurfaceToInterior(surfaceIndices, interiorIndices, particles, constraints,
                settings.structuralCompliance);

            return constraints;
        }


        private void CreateInteriorConstraints(List<int> interiorIndices, List<Particle> particles,
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

        private void ConnectSurfaceToInterior(List<int> surfaceIndices, List<int> interiorIndices,
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

        private List<VolumeConstraint> CreateVolumeConstraints(List<int> surfaceIndices, List<int> interiorIndices,
            List<Particle> particles, SoftBodySettings settings)
        {
            var volumeConstraints = new List<VolumeConstraint>();

            if (interiorIndices.Count == 0)
            {
                CreateFallbackVolumeConstraints(surfaceIndices, particles, volumeConstraints, settings);
                return volumeConstraints;
            }

            var constraintsCreated = 0;
            var maxVolumeConstraints = settings.maxVolumeConstraints;

            foreach (var interiorIdx in interiorIndices)
            {
                if (constraintsCreated >= maxVolumeConstraints) break;

                var interiorPos = particles[interiorIdx].Position;
                var closestSurface = FindClosestSurfacePoints(interiorPos, surfaceIndices, particles, 8);

                constraintsCreated += CreateTetrahedraFromInteriorPoint(interiorIdx, closestSurface, particles,
                    volumeConstraints, settings, maxVolumeConstraints - constraintsCreated);
            }

            if (settings.debugMessages)
            {
                Debug.Log($"Created {volumeConstraints.Count} volume constraints from tetrahedralization");
            }

            return volumeConstraints;
        }

        private List<int> FindClosestSurfacePoints(Vector3 interiorPos, List<int> surfaceIndices,
            List<Particle> particles, int count)
        {
            return surfaceIndices
                .Select(idx => new { index = idx, distance = Vector3.Distance(interiorPos, particles[idx].Position) })
                .OrderBy(x => x.distance)
                .Take(count)
                .Select(x => x.index)
                .ToList();
        }

        private int CreateTetrahedraFromInteriorPoint(int interiorIdx, List<int> closestSurface,
            List<Particle> particles, List<VolumeConstraint> volumeConstraints, SoftBodySettings settings,
            int remainingSlots)
        {
            var created = 0;

            for (var i = 0; i < closestSurface.Count - 2 && created < remainingSlots; i++)
            {
                for (var j = i + 1; j < closestSurface.Count - 1 && created < remainingSlots; j++)
                {
                    for (var k = j + 1; k < closestSurface.Count && created < remainingSlots; k++)
                    {
                        var volumeConstraint = CreateVolumeConstraint(
                            closestSurface[i], closestSurface[j], closestSurface[k], interiorIdx,
                            particles, settings.volumeCompliance);

                        if (volumeConstraint.HasValue)
                        {
                            volumeConstraints.Add(volumeConstraint.Value);
                            created++;
                        }
                    }
                }
            }

            return created;
        }

        private void CreateFallbackVolumeConstraints(List<int> surfaceIndices, List<Particle> particles,
            List<VolumeConstraint> volumeConstraints, SoftBodySettings settings)
        {
            var constraintsCreated = 0;

            for (var i = 0; i < surfaceIndices.Count - 3 && constraintsCreated < settings.maxVolumeConstraints; i += 4)
            {
                var volumeConstraint = CreateVolumeConstraint(
                    surfaceIndices[i], surfaceIndices[i + 1], surfaceIndices[i + 2], surfaceIndices[i + 3],
                    particles, settings.volumeCompliance);

                if (volumeConstraint.HasValue)
                {
                    volumeConstraints.Add(volumeConstraint.Value);
                    constraintsCreated++;
                }
            }

            if (settings.debugMessages)
            {
                Debug.Log($"Created {constraintsCreated} fallback volume constraints");
            }
        }

        private Vector2[] ExpandUVsForInteriorParticles(Vector2[] surfaceUVs, int totalParticleCount,
            int surfaceParticleCount, List<Particle> particles, SoftBodySettings settings)
        {
            var expandedUVs = new Vector2[totalParticleCount];

            // Copy surface UVs
            for (var i = 0; i < surfaceParticleCount; i++)
            {
                expandedUVs[i] = surfaceUVs[i];
            }

            // Interpolate UVs for interior particles from nearest surface particles
            for (var i = surfaceParticleCount; i < totalParticleCount; i++)
            {
                var closestSurfaceIdx =
                    FindClosestSurfaceParticle(particles[i].Position, particles, surfaceParticleCount);
                expandedUVs[i] = surfaceUVs[closestSurfaceIdx];
            }

            if (settings.debugMessages)
            {
                Debug.Log(
                    $"Expanded UV array from {surfaceParticleCount} to {totalParticleCount} for tetrahedralization");
            }

            return expandedUVs;
        }

        private int FindClosestSurfaceParticle(Vector3 interiorPos, List<Particle> particles, int surfaceCount)
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

        private static VolumeConstraint? CreateVolumeConstraint(int p1, int p2, int p3, int p4,
            List<Particle> particles, float compliance)
        {
            var pos1 = particles[p1].Position;
            var pos2 = particles[p2].Position;
            var pos3 = particles[p3].Position;
            var pos4 = particles[p4].Position;

            var v1 = pos1 - pos4;
            var v2 = pos2 - pos4;
            var v3 = pos3 - pos4;
            var restVolume = Vector3.Dot(v1, Vector3.Cross(v2, v3)) / 6.0f;

            if (Mathf.Abs(restVolume) > 0.00001f)
            {
                return new VolumeConstraint
                {
                    P1 = p1, P2 = p2, P3 = p3, P4 = p4,
                    RestVolume = Mathf.Abs(restVolume),
                    Compliance = compliance,
                    Lambda = 0
                };
            }

            return null;
        }
    }
}
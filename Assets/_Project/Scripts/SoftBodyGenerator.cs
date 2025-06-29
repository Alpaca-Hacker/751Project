using System;
using System.Collections.Generic;
using System.Linq;
using SoftBody.Scripts.Models;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SoftBody.Scripts
{
    public static class SoftBodyGenerator
    {
        public static void GenerateSoftBody(SoftBodySettings settings,
            Transform transform,
            out List<Particle> particles,
            out List<Constraint> constraints,
            out List<VolumeConstraint> volumeConstraints,
            out List<int> indices,
            out Vector2[] weldedUVs)
        {
            particles = new List<Particle>();
            constraints = new List<Constraint>();
            volumeConstraints = new List<VolumeConstraint>();
            indices = new List<int>();
            weldedUVs = null;

            if (settings.inputMesh != null && !settings.useProceduralCube)
            {
                GenerateMeshData(particles, constraints, volumeConstraints, indices, settings, transform,
                    out weldedUVs);
            }
            else
            {
                SoftBodyCubeGenerator.GenerateCubeData(particles, constraints, volumeConstraints, indices, settings,
                    transform);
            }

            if (settings.enableStuffingMode)
            {
                StuffingGenerator.CreateStuffedBodyStructure(particles, constraints, volumeConstraints, settings,
                    transform);
            }

            for (var i = 0; i < constraints.Count; i++)
            {
                var c = constraints[i];
                var restLength = Vector3.Distance(particles[c.ParticleA].Position, particles[c.ParticleB].Position);

                if (restLength < 0.001f)
                {
                    // Remove invalid constraint
                    constraints.RemoveAt(i);
                    i--; // Adjust index after removal
                    continue;
                }

                c.RestLength = restLength;
                constraints[i] = c;
            }

            Debug.Log($"Removed invalid constraints. Final count: {constraints.Count}");
        }

        private static void GenerateMeshData(List<Particle> particles,
            List<Constraint> constraints,
            List<VolumeConstraint> volumeConstraints,
            List<int> indices,
            SoftBodySettings settings,
            Transform transform,
            out Vector2[] weldedUVs)
        {
            weldedUVs = null;
            var mesh = settings.inputMesh;
            if (mesh == null)
            {
                Debug.LogError("Input mesh is null!");
                return;
            }

            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            var originalUVs = mesh.uv;

            Debug.Log($"Original mesh '{mesh.name}': {vertices.Length} vertices, {triangles.Length / 3} triangles");

            // Weld vertices
            WeldVertices(vertices, triangles, out var weldedVertices, out var weldedTriangles, out var vertexMapping);

            if (originalUVs != null && originalUVs.Length > 0)
            {
                weldedUVs = RemapUVsAfterWelding(originalUVs, vertexMapping, weldedVertices.Length);
            }

            Debug.Log($"After welding: {weldedVertices.Length} vertices");


            if (settings.useTetrahedralizationForHighPoly &&
                weldedVertices.Length > settings.maxSurfaceVerticesBeforeTetra)
            {
                GenerateTetrahedralizedSoftBody(weldedVertices, weldedTriangles, particles, constraints,
                    volumeConstraints, settings, transform, ref weldedUVs);
            }
            else
            {
                GenerateSurfaceBasedSoftBody(weldedVertices, particles, settings, transform);
            }

            // Store welded topology
            indices.AddRange(weldedTriangles);

            // Generate constraints from welded mesh
            GenerateConstraintsFromMesh(particles, constraints, weldedTriangles, settings);
            AnalyzeConstraintGeneration(particles, constraints, weldedTriangles);
            if (settings.enableConstraintFiltering)
            {
                FilterConstraints(particles, constraints, settings);
            }
        }

        private static void WeldVertices(Vector3[] originalVertices, int[] originalTriangles,
            out Vector3[] weldedVertices, out int[] weldedTriangles, out int[] vertexMapping,
            float weldDistance = 0.0001f)
        {
            var uniqueVertices = new List<Vector3>();
            vertexMapping = new int[originalVertices.Length];

            // Find unique vertices
            for (var i = 0; i < originalVertices.Length; i++)
            {
                var vertex = originalVertices[i];
                var existingIndex = -1;

                // Check if this vertex already exists
                for (var j = 0; j < uniqueVertices.Count; j++)
                {
                    if (Vector3.Distance(vertex, uniqueVertices[j]) < weldDistance)
                    {
                        existingIndex = j;
                        break;
                    }
                }

                if (existingIndex == -1)
                {
                    // New unique vertex
                    vertexMapping[i] = uniqueVertices.Count;
                    uniqueVertices.Add(vertex);
                }
                else
                {
                    // Use existing vertex
                    vertexMapping[i] = existingIndex;
                }
            }

            weldedVertices = uniqueVertices.ToArray();

            // Remap triangle indices
            weldedTriangles = new int[originalTriangles.Length];
            for (var i = 0; i < originalTriangles.Length; i++)
            {
                weldedTriangles[i] = vertexMapping[originalTriangles[i]];
            }

            Debug.Log($"Vertex welding: {originalVertices.Length} -> {weldedVertices.Length} vertices");
        }

        private static void GenerateSurfaceBasedSoftBody(Vector3[] vertices,
            List<Particle> particles, SoftBodySettings settings, Transform transform)
        {
            // Create particles from vertices (your existing logic)
            foreach (var vertex in vertices)
            {
                var worldPos = transform.TransformPoint(vertex);
                var particle = new Particle
                {
                    Position = worldPos,
                    Velocity = Vector4.zero,
                    Force = Vector4.zero,
                    InvMass = 1f / settings.mass
                };
                particles.Add(particle);
            }

        }

        private static Vector2[] RemapUVsAfterWelding(Vector2[] originalUVs, int[] vertexMapping, int weldedVertexCount)
        {
            if (originalUVs == null || originalUVs.Length == 0)
                return Array.Empty<Vector2>();

            var weldedUVs = new Vector2[weldedVertexCount];
            var uvCounts = new int[weldedVertexCount]; // To track how many UVs we've averaged per vertex

            // Average UVs for welded vertices
            for (var i = 0; i < originalUVs.Length; i++)
            {
                if (i >= vertexMapping.Length)
                {
                    continue;
                }

                var weldedIndex = vertexMapping[i];

                if (weldedIndex >= weldedVertexCount)
                {
                    continue;
                }

                weldedUVs[weldedIndex] += originalUVs[i];
                uvCounts[weldedIndex]++;
            }

            // Normalize averaged UVs
            for (var i = 0; i < weldedUVs.Length; i++)
            {
                if (uvCounts[i] > 0)
                {
                    weldedUVs[i] /= uvCounts[i];
                }
            }

            Debug.Log($"UV remapping: {originalUVs.Length} -> {weldedUVs.Length} UVs");
            return weldedUVs;
        }

        private static void GenerateConstraintsFromMesh(List<Particle> particles,
            List<Constraint> constraints,
            int[] triangles,
            SoftBodySettings settings)
        {
            if (triangles.Length % 3 != 0)
            {
                Debug.LogError($"Invalid triangle array length: {triangles.Length}");
                return;
            }

            var edges = new HashSet<(int, int)>();
            var triangleEdges = new List<(int, int, int)>();

            Debug.Log($"Processing {triangles.Length / 3} triangles...");

            // Track edge statistics
            var edgeCount = 0;
            var duplicateEdges = 0;
            var invalidTriangles = 0;

            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = triangles[i];
                var b = triangles[i + 1];
                var c = triangles[i + 2];

                // Validate indices
                if (ValidateParticles(particles, a, b, c, ref invalidTriangles))
                {
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

                triangleEdges.Add((a, b, c));
            }

            Debug.Log(
                $"Edge analysis: {edgeCount} total edges, {edges.Count} unique edges, {duplicateEdges} duplicates");
            Debug.Log($"Invalid triangles: {invalidTriangles}");

            if (edges.Count == 0)
            {
                Debug.LogError("NO EDGES FOUND! Check mesh topology.");
                return;
            }

            // Create constraints with detailed logging
            var validConstraints = 0;
            var invalidConstraints = 0;

            foreach (var edge in edges)
            {
                if (AddConstraintWithValidation(particles, constraints, edge.Item1, edge.Item2,
                        settings.structuralCompliance))
                {
                    validConstraints++;
                }
                else
                {
                    invalidConstraints++;
                }
            }

            Debug.Log($"Constraint creation: {validConstraints} valid, {invalidConstraints} invalid");

            // Add comprehensive debugging
            DebugConstraintConnectivity(particles, constraints, settings);

            // Generate bending constraints
            if (constraints.Count > 0 && settings.constraintDensityMultiplier > 1f)
            {
                var bendConstraintsBefore = constraints.Count;
                GenerateShearConstraintsFromTriangles(particles, constraints, triangleEdges, settings);
                Debug.Log($"Added {constraints.Count - bendConstraintsBefore} bending constraints");
            }
        }

        private static bool ValidateParticles(List<Particle> particles, int a, int b, int c, ref int invalidTriangles)
        {
            if (a < 0 || a >= particles.Count ||
                b < 0 || b >= particles.Count ||
                c < 0 || c >= particles.Count)
            {
                Debug.LogError($"Invalid triangle indices: {a}, {b}, {c} (max: {particles.Count - 1})");
                invalidTriangles++;
                return true;
            }

            // Check for degenerate triangles
            if (a == b || b == c || c == a)
            {
                Debug.LogWarning($"Degenerate triangle skipped: {a}, {b}, {c}");
                invalidTriangles++;
                return true;
            }

            return false;
        }

        private static void GenerateTetrahedralizedSoftBody(Vector3[] surfaceVertices, int[] surfaceTriangles,
            List<Particle> particles, List<Constraint> constraints, List<VolumeConstraint> volumeConstraints,
            SoftBodySettings settings, Transform transform, ref Vector2[] weldedUVs)
        {
            var originalSurfaceCount = surfaceVertices.Length;

            // Step 1: Add all surface vertices as particles
            var surfaceParticleIndices = new List<int>();
            for (var i = 0; i < surfaceVertices.Length; i++)
            {
                var worldPos = transform.TransformPoint(surfaceVertices[i]);
                particles.Add(new Particle
                {
                    Position = worldPos,
                    Velocity = Vector4.zero,
                    Force = Vector4.zero,
                    InvMass = 1f / settings.mass
                });
                surfaceParticleIndices.Add(i);
            }

            // Step 2: Generate interior points
            var interiorPoints =
                GenerateInteriorPointsForMesh(surfaceVertices, surfaceTriangles, settings.interiorPointDensity);
            var interiorParticleIndices = new List<int>();

            Debug.Log($"Generated {interiorPoints.Count} interior points");

            foreach (var interiorPoint in interiorPoints)
            {
                var worldPos = transform.TransformPoint(interiorPoint);
                particles.Add(new Particle
                {
                    Position = worldPos,
                    Velocity = Vector4.zero,
                    Force = Vector4.zero,
                    InvMass = 1f / (settings.mass * 2f) // Interior points can be heavier
                });
                interiorParticleIndices.Add(particles.Count - 1);
            }

            // Step 2.5: Update UV array for new particle count
            if (weldedUVs != null && weldedUVs.Length == originalSurfaceCount)
            {
                var expandedUVs = new Vector2[particles.Count];

                // Copy original surface UVs
                for (var i = 0; i < originalSurfaceCount; i++)
                {
                    expandedUVs[i] = weldedUVs[i];
                }

                // Interpolate UVs for interior particles from nearest surface particles
                for (var i = originalSurfaceCount; i < particles.Count; i++)
                {
                    var interiorPos = particles[i].Position;
                    var closestSurfaceIdx =
                        FindClosestSurfaceParticleIndex(interiorPos, particles, originalSurfaceCount);
                    expandedUVs[i] = weldedUVs[closestSurfaceIdx];
                }

                weldedUVs = expandedUVs;
                Debug.Log($"Expanded UV array from {originalSurfaceCount} to {particles.Count} for tetrahedralization");
            }

            // Step 3: Create constraint hierarchy
            // A) Surface constraints (flexible - for surface detail)
            CreateSurfaceConstraints(surfaceTriangles, constraints,
                settings.structuralCompliance * 5f); // More flexible surface

            // B) Interior structural constraints (stiffer - for overall shape)
            CreateInteriorConstraints(interiorParticleIndices, particles, constraints,
                settings.structuralCompliance * 0.1f); // Much stiffer interior

            // C) Surface-to-interior connections (medium stiffness)
            ConnectSurfaceToInterior(surfaceParticleIndices, interiorParticleIndices, particles,
                constraints, settings.structuralCompliance);

            // D) Volume constraints from tetrahedra
            CreateVolumeConstraintsFromTetrahedralization(surfaceParticleIndices, interiorParticleIndices,
                particles, volumeConstraints, settings);

            // E) Fallback volume constraints if needed
            if (volumeConstraints.Count == 0)
            {
                Debug.Log("No volume constraints from tetrahedralization, using fallback");
                CreateSimpleFallbackVolumeConstraints(surfaceParticleIndices, particles, volumeConstraints, settings);
            }
            else
            {
                Debug.Log($"Using {volumeConstraints.Count} volume constraints from tetrahedralization");
            }



            Debug.Log(
                $"Tetrahedralized soft body: {particles.Count} particles, {constraints.Count} constraints, {volumeConstraints.Count} volume constraints");
        }

// Helper method to find closest surface particle
        private static int FindClosestSurfaceParticleIndex(Vector3 interiorPos, List<Particle> particles,
            int surfaceCount)
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

        private static void CreateSurfaceConstraints(int[] triangles, List<Constraint> constraints, float compliance)
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

            // Create constraints for each edge
            foreach (var edge in edges)
            {
                AddConstraint(constraints, edge.Item1, edge.Item2, compliance);
            }

            Debug.Log($"Created {edges.Count} surface constraints");
        }

        private static void CreateInteriorConstraints(List<int> interiorIndices, List<Particle> particles,
            List<Constraint> constraints, float compliance)
        {
            // Connect each interior point to its nearest neighbors
            var maxDistance = CalculateAverageEdgeLength(particles) * 1.5f;

            for (var i = 0; i < interiorIndices.Count; i++)
            {
                for (var j = i + 1; j < interiorIndices.Count; j++)
                {
                    var idx1 = interiorIndices[i];
                    var idx2 = interiorIndices[j];

                    var distance = Vector3.Distance(particles[idx1].Position, particles[idx2].Position);
                    if (distance < maxDistance)
                    {
                        AddConstraint(constraints, idx1, idx2, compliance);
                    }
                }
            }
        }

        private static List<Vector3> GenerateInteriorPointsForMesh(Vector3[] surfaceVertices, int[] triangles,
            float density)
        {
            var bounds = CalculateMeshBounds(surfaceVertices);
            var interiorPoints = new List<Vector3>();

            // Calculate a reasonable target based on surface vertices
            var targetInteriorCount = Mathf.RoundToInt(surfaceVertices.Length * density * 0.1f);
            targetInteriorCount = Mathf.Clamp(targetInteriorCount, 20, 150); // Better range

            Debug.Log($"Target interior points: {targetInteriorCount} for {surfaceVertices.Length} surface vertices");

            // Shrink bounds to ensure points are well inside
            var shrinkFactor = 0.8f;
            var shrunkSize = bounds.size * shrinkFactor;
            var shrunkBounds = new Bounds(bounds.center, shrunkSize);

            var maxAttempts = targetInteriorCount * 50;
            var attempts = 0;

            while (interiorPoints.Count < targetInteriorCount && attempts < maxAttempts)
            {
                var candidate = new Vector3(
                    Random.Range(shrunkBounds.min.x, shrunkBounds.max.x),
                    Random.Range(shrunkBounds.min.y, shrunkBounds.max.y),
                    Random.Range(shrunkBounds.min.z, shrunkBounds.max.z)
                );

                if (IsPointInsideMesh(surfaceVertices, triangles, candidate))
                {
                    interiorPoints.Add(candidate);
                }

                attempts++;
            }

            Debug.Log($"Interior point generation: {attempts} attempts -> {interiorPoints.Count} points inside mesh");
            return interiorPoints;
        }

        private static void ConnectSurfaceToInterior(List<int> surfaceIndices, List<int> interiorIndices,
            List<Particle> particles, List<Constraint> constraints, float baseCompliance)
        {
            var maxConnectionDistance = CalculateAverageEdgeLength(particles) * 2f;

            foreach (var surfaceIdx in surfaceIndices)
            {
                var surfacePos = particles[surfaceIdx].Position;
                var connectionsForThisVertex = 0;
                const int maxConnectionsPerSurfaceVertex = 3;

                // Find closest interior points
                var distances = new List<(int index, float distance)>();
                foreach (var interiorIdx in interiorIndices)
                {
                    var distance = Vector3.Distance(surfacePos, particles[interiorIdx].Position);
                    if (distance < maxConnectionDistance)
                    {
                        distances.Add((interiorIdx, distance));
                    }
                }

                // Sort by distance and connect to closest
                distances.Sort((a, b) => a.distance.CompareTo(b.distance));

                foreach (var (interiorIdx, distance) in distances)
                {
                    if (connectionsForThisVertex >= maxConnectionsPerSurfaceVertex) break;

                    AddConstraint(particles, constraints, surfaceIdx, interiorIdx, baseCompliance);
                    connectionsForThisVertex++;
                }
            }
        }

        private static bool IsPointInsideMesh(Vector3[] vertices, int[] triangles, Vector3 point)
        {
            // Simple ray casting - count intersections with mesh
            var rayDirection = Vector3.right;
            var intersections = 0;

            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]];
                var b = vertices[triangles[i + 1]];
                var c = vertices[triangles[i + 2]];

                if (RayTriangleIntersect(point, rayDirection, a, b, c))
                {
                    intersections++;
                }
            }

            return intersections % 2 == 1; // Odd number = inside
        }

        private static bool RayTriangleIntersect(Vector3 rayOrigin, Vector3 rayDirection,
            Vector3 a, Vector3 b, Vector3 c)
        {
            const float epsilon = 1e-8f;

            var edge1 = b - a;
            var edge2 = c - a;
            var h = Vector3.Cross(rayDirection, edge2);
            var det = Vector3.Dot(edge1, h);

            if (det > -epsilon && det < epsilon) return false;

            var invDet = 1f / det;
            var s = rayOrigin - a;
            var u = invDet * Vector3.Dot(s, h);

            if (u < 0f || u > 1f) return false;

            var q = Vector3.Cross(s, edge1);
            var v = invDet * Vector3.Dot(rayDirection, q);

            if (v < 0f || u + v > 1f) return false;

            var t = invDet * Vector3.Dot(edge2, q);
            return t > epsilon; // Ray intersection
        }

        private static Bounds CalculateMeshBounds(Vector3[] vertices)
        {
            if (vertices.Length == 0) return new Bounds();

            var min = vertices[0];
            var max = vertices[0];

            foreach (var vertex in vertices)
            {
                min = Vector3.Min(min, vertex);
                max = Vector3.Max(max, vertex);
            }

            return new Bounds((min + max) * 0.5f, max - min);
        }

        private static float CalculateAverageEdgeLength(List<Particle> particles)
        {
            if (particles.Count < 2) return 1f;

            var totalDistance = 0f;
            var sampleCount = Mathf.Min(100, particles.Count * particles.Count / 4);

            for (var i = 0; i < sampleCount; i++)
            {
                var a = Random.Range(0, particles.Count);
                var b = Random.Range(0, particles.Count);
                if (a != b)
                {
                    totalDistance += Vector3.Distance(particles[a].Position, particles[b].Position);
                }
            }

            return totalDistance / sampleCount;
        }

        private static void AddConstraint(List<Constraint> constraints, int a, int b, float compliance)
        {
            constraints.Add(new Constraint
            {
                ParticleA = a,
                ParticleB = b,
                RestLength = 0f, // Will be calculated later
                Compliance = compliance,
                Lambda = 0f,
                ColourGroup = 0
            });
        }


        private static void CreateVolumeConstraintsFromTetrahedralization(List<int> surfaceIndices,
            List<int> interiorIndices, List<Particle> particles, List<VolumeConstraint> volumeConstraints,
            SoftBodySettings settings)
        {
            Debug.Log($"=== Starting volume constraint creation with {interiorIndices.Count} interior particles ===");

            if (interiorIndices.Count == 0)
            {
                Debug.LogWarning("No interior particles for volume constraints");
                return;
            }

            var maxVolumeConstraints = settings.maxVolumeConstraints;
            var constraintsCreated = 0;
            var volumesChecked = 0;
            var validVolumes = 0;

            foreach (var interiorIdx in interiorIndices)
            {
                if (constraintsCreated >= maxVolumeConstraints) break;

                Debug.Log($"Processing interior particle {interiorIdx}");
                var interiorPos = particles[interiorIdx].Position;

                // Find the closest surface points
                var closestSurface = surfaceIndices
                    .Select(idx => new
                    {
                        index = idx,
                        distance = Vector3.Distance(interiorPos, particles[idx].Position)
                    })
                    .OrderBy(x => x.distance)
                    .Take(8)
                    .ToList();

                Debug.Log($"Found {closestSurface.Count} closest surface particles for interior {interiorIdx}");

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

                            volumesChecked++;

                            //  Debug.Log($"Tetrahedron {p1}-{p2}-{p3}-{p4}: volume = {restVolume:F6}, abs = {Mathf.Abs(restVolume):F6}");

                            // Only create if volume is reasonable
                            if (Mathf.Abs(restVolume) > 0.00001f)
                            {
                                validVolumes++;
                                volumeConstraints.Add(new VolumeConstraint
                                {
                                    P1 = p1, P2 = p2, P3 = p3, P4 = p4,
                                    RestVolume = Mathf.Abs(restVolume),
                                    Compliance = settings.volumeCompliance,
                                    Lambda = 0,
                                    PressureMultiplier = settings.pressureResistance
                                });

                                constraintsCreated++;
                                //  Debug.Log($"Created volume constraint {constraintsCreated}: volume = {Mathf.Abs(restVolume):F6}");
                            }
                        }
                    }
                }

                // Only process first few interior particles for debugging
                if (interiorIdx - interiorIndices[0] > 2) break;
            }

            Debug.Log(
                $"Volume constraint summary: {volumesChecked} volumes checked, {validVolumes} valid, {constraintsCreated} constraints created");
            Debug.Log($"Created {volumeConstraints.Count} volume constraints from tetrahedralization");
        }

        private static void CreateSimpleFallbackVolumeConstraints(List<int> surfaceIndices,
            List<Particle> particles, List<VolumeConstraint> volumeConstraints, SoftBodySettings settings)
        {
            Debug.Log("Creating simple fallback volume constraints from surface particles");
            Debug.Log(
                $"Surface indices count: {surfaceIndices.Count}, Max constraints: {settings.maxVolumeConstraints}");

            var constraintsCreated = 0;
            var volumesChecked = 0;

            for (var i = 0; i < surfaceIndices.Count - 3 && constraintsCreated < settings.maxVolumeConstraints; i += 4)
            {
                var p1 = surfaceIndices[i];
                var p2 = surfaceIndices[i + 1];
                var p3 = surfaceIndices[i + 2];
                var p4 = surfaceIndices[i + 3];

                // Debug the volume calculation
                var pos1 = particles[p1].Position;
                var pos2 = particles[p2].Position;
                var pos3 = particles[p3].Position;
                var pos4 = particles[p4].Position;

                var v1 = pos1 - pos4;
                var v2 = pos2 - pos4;
                var v3 = pos3 - pos4;
                var restVolume = Vector3.Dot(v1, Vector3.Cross(v2, v3)) / 6.0f;

                volumesChecked++;

                // if (volumesChecked <= 5) // Log first few for debugging
                // {
                //     Debug.Log(
                //         $"Fallback tetrahedron {p1}-{p2}-{p3}-{p4}: volume = {restVolume:F6}, abs = {Mathf.Abs(restVolume):F6}");
                // }

                if (Mathf.Abs(restVolume) > 0.00001f) // Lower threshold
                {
                    volumeConstraints.Add(new VolumeConstraint
                    {
                        P1 = p1, P2 = p2, P3 = p3, P4 = p4,
                        RestVolume = Mathf.Abs(restVolume),
                        Compliance = settings.volumeCompliance,
                        Lambda = 0,
                        PressureMultiplier = settings.pressureResistance
                    });
                    constraintsCreated++;
                }
            }

            Debug.Log($"Fallback summary: {volumesChecked} volumes checked, {constraintsCreated} constraints created");
            Debug.Log($"Created {volumeConstraints.Count} simple fallback volume constraints");
        }

        private static void AddEdge(HashSet<(int, int)> edges, int a, int b)
        {
            // Ensure consistent ordering to avoid duplicates
            if (a > b)
            {
                (a, b) = (b, a);
            }

            edges.Add((a, b));
        }

        private static void GenerateShearConstraintsFromTriangles(List<Particle> particles,
            List<Constraint> constraints,
            List<(int, int, int)> triangles,
            SoftBodySettings settings)
        {
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
                        AddConstraint(particles, constraints, v1, v2, settings.bendCompliance);
                    }
                }
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

        public static bool AddConstraintWithValidation(List<Particle> particles,
            List<Constraint> constraints,
            int a, int b,
            float compliance)
        {
            if (a == b) return false; // Same particle
            if (a < 0 || a >= particles.Count || b < 0 || b >= particles.Count) return false; // Invalid indices

            var restLength = Vector3.Distance(particles[a].Position, particles[b].Position);

            if (restLength < 0.001f)
            {
                Debug.LogWarning($"Constraint between particles {a} and {b} has very small rest length: {restLength}");
                return false;
            }

            var constraint = new Constraint
            {
                ParticleA = a,
                ParticleB = b,
                RestLength = restLength,
                Compliance = compliance,
                Lambda = 0f,
                ColourGroup = 0
            };

            constraints.Add(constraint);
            return true;
        }

        public static void AddConstraint(List<Particle> particles, List<Constraint> constraints, int a, int b,
            float compliance)
        {
            var restLength = Vector3.Distance(particles[a].Position, particles[b].Position);

            if (restLength < 0.001f)
            {
                return;
            }

            var constraint = new Constraint
            {
                ParticleA = a,
                ParticleB = b,
                RestLength = restLength,
                Compliance = compliance, // Scale compliance
                Lambda = 0f,
                ColourGroup = 0
            };

            constraints.Add(constraint);
        }

        private static void DebugConstraintConnectivity(List<Particle> particles, List<Constraint> constraints,
            SoftBodySettings settings)
        {
            Debug.Log("=== CONSTRAINT CONNECTIVITY ANALYSIS ===");

            // Count connections per particle
            var connectionCount = new int[particles.Count];
            var maxDistance = 0f;
            var minDistance = float.MaxValue;
            var totalDistance = 0f;

            foreach (var constraint in constraints)
            {
                connectionCount[constraint.ParticleA]++;
                connectionCount[constraint.ParticleB]++;

                var distance = constraint.RestLength;
                maxDistance = Mathf.Max(maxDistance, distance);
                minDistance = Mathf.Min(minDistance, distance);
                totalDistance += distance;
            }

            // Find isolated particles
            var isolatedParticles = 0;
            var minConnections = int.MaxValue;
            var maxConnections = 0;

            for (var i = 0; i < connectionCount.Length; i++)
            {
                if (connectionCount[i] == 0)
                {
                    isolatedParticles++;
                    Debug.LogError($"ISOLATED PARTICLE FOUND: Particle {i} has no constraints!");
                }

                minConnections = Mathf.Min(minConnections, connectionCount[i]);
                maxConnections = Mathf.Max(maxConnections, connectionCount[i]);
            }

            Debug.Log($"Particles: {particles.Count}");
            Debug.Log($"Constraints: {constraints.Count}");
            Debug.Log($"Isolated particles: {isolatedParticles}");
            Debug.Log(
                $"Connections per particle - Min: {minConnections}, Max: {maxConnections}, Avg: {(float)constraints.Count * 2 / particles.Count:F1}");
            Debug.Log(
                $"Constraint distances - Min: {minDistance:F4}, Max: {maxDistance:F4}, Avg: {totalDistance / constraints.Count:F4}");

            // Check for network fragmentation
            Connectivity.CheckNetworkConnectivity(particles, constraints, settings);
        }

        private static void AnalyzeConstraintGeneration(List<Particle> particles, List<Constraint> constraints,
            int[] triangles)
        {
            Debug.Log("=== CONSTRAINT GENERATION ANALYSIS ===");

            // Count constraint types
            var edgeConstraints = new HashSet<(int, int)>();
            var bendingConstraints = new HashSet<(int, int)>();

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

            Debug.Log(
                $"Constraint distances: Min={distances[0]:F3}, Max={distances.Last():F3}, Median={distances[distances.Count / 2]:F3}");
            Debug.Log($"Short constraints: {shortConstraints}, Long constraints: {longConstraints}");
        }

        private static void AddEdgeToSet(HashSet<(int, int)> edges, int a, int b)
        {
            if (a > b) (a, b) = (b, a);
            edges.Add((a, b));
        }


        private static void FilterConstraints(List<Particle> particles, List<Constraint> constraints,
            SoftBodySettings settings)
        {
            var originalCount = constraints.Count;
            Debug.Log($"Starting constraint filtering: {originalCount} constraints");

            // 1. Categorize constraints by length
            var constraintData = new List<(Constraint constraint, float length, bool isStructural)>();

            foreach (var constraint in constraints)
            {
                var length = Vector3.Distance(particles[constraint.ParticleA].Position,
                    particles[constraint.ParticleB].Position);

                // Constraints under median length are likely structural (mesh edges)
                var isStructural = length <= 0.100f; // Use your median from analysis

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

            Debug.Log(
                $"Constraint filtering complete: {originalCount} → {finalCount} ({reductionPercent:F1}% reduction)");

            // Analyze final distribution
            var finalConstraintsPerParticle = new int[particles.Count];
            foreach (var constraint in constraints)
            {
                finalConstraintsPerParticle[constraint.ParticleA]++;
                finalConstraintsPerParticle[constraint.ParticleB]++;
            }

            var avgFinal = finalConstraintsPerParticle.Average();
            var maxFinal = finalConstraintsPerParticle.Max();
            Debug.Log($"Final constraints per particle: Avg={avgFinal:F1}, Max={maxFinal}");
        }
    }
}
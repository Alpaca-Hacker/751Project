using System.Collections.Generic;
using System.Linq;
using SoftBody.Scripts.Models;
using UnityEngine;

namespace SoftBody.Scripts
{
    public static class SoftBodyGenerator
    {
        public static bool GenerateSoftBody(SoftBodySettings settings,
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
            weldedUVs = null; // Initialize to null, will be set if UVs are present

            if (settings.inputMesh != null && !settings.useProceduralCube)
            {
                GenerateMeshData(particles, constraints, volumeConstraints, indices, settings, transform, out weldedUVs);
            }
            else
            {
                GenerateCubeData(particles, constraints, volumeConstraints, indices, settings, transform);
            }

            return true;
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
  
    
            // Create particles from welded vertices
            for (var i = 0; i < weldedVertices.Length; i++)
            {
                var worldPos = transform.TransformPoint(weldedVertices[i]);
                var particle = new Particle
                {
                    Position = worldPos,
                    Velocity = Vector4.zero,
                    Force = Vector4.zero,
                    InvMass = 1f / settings.mass
                };
                particles.Add(particle);
            }
    
            // Store welded topology
            indices.AddRange(weldedTriangles);
    
            // Generate constraints from welded mesh
            GenerateConstraintsFromMesh(particles, constraints, weldedTriangles, settings);
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

        private static void GenerateSurfaceBasedSoftBody(Vector3[] vertices, int[] triangles,
            List<Particle> particles, List<Constraint> constraints,
            SoftBodySettings settings, Transform transform)
        {
            // Create particles from vertices (your existing logic)
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
            }

            // Generate constraints (your existing logic)
            GenerateConstraintsFromMesh(particles, constraints, triangles, settings);
        }
        
        private static Vector2[] RemapUVsAfterWelding(Vector2[] originalUVs, int[] vertexMapping, int weldedVertexCount)
        {
            if (originalUVs == null || originalUVs.Length == 0)
                return new Vector2[0];
    
            var weldedUVs = new Vector2[weldedVertexCount];
            var uvCounts = new int[weldedVertexCount]; // To track how many UVs we've averaged per vertex
    
            // Average UVs for welded vertices
            for (var i = 0; i < originalUVs.Length; i++)
            {
                if (i < vertexMapping.Length)
                {
                    var weldedIndex = vertexMapping[i];
                    if (weldedIndex < weldedVertexCount)
                    {
                        weldedUVs[weldedIndex] += originalUVs[i];
                        uvCounts[weldedIndex]++;
                    }
                }
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
                if (a < 0 || a >= particles.Count ||
                    b < 0 || b >= particles.Count ||
                    c < 0 || c >= particles.Count)
                {
                    Debug.LogError($"Invalid triangle indices: {a}, {b}, {c} (max: {particles.Count - 1})");
                    invalidTriangles++;
                    continue;
                }

                // Check for degenerate triangles
                if (a == b || b == c || c == a)
                {
                    Debug.LogWarning($"Degenerate triangle skipped: {a}, {b}, {c}");
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

        private static void GenerateTetrahedralizedSoftBody(Vector3[] surfaceVertices, int[] surfaceTriangles,
            List<Particle> particles, List<Constraint> constraints, List<VolumeConstraint> volumeConstraints,
            SoftBodySettings settings, Transform transform)
        {
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

            // Step 3: Create constraint hierarchy

            // A) Surface constraints (flexible - for surface detail)
            CreateSurfaceConstraints(surfaceParticleIndices, surfaceTriangles, constraints,
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

            Debug.Log(
                $"Tetrahedralized soft body: {particles.Count} particles, {constraints.Count} constraints, {volumeConstraints.Count} volume constraints");
        }

        private static List<Vector3> GenerateInteriorPointsForMesh(Vector3[] surfaceVertices, int[] triangles,
            float density)
        {
            var bounds = CalculateMeshBounds(surfaceVertices);
            var interiorPoints = new List<Vector3>();

            // Calculate optimal spacing based on surface density
            var surfaceArea = CalculateSurfaceArea(surfaceVertices, triangles);
            var volume = bounds.size.x * bounds.size.y * bounds.size.z;
            var surfaceDensity = surfaceVertices.Length / surfaceArea;
            var targetInteriorDensity = surfaceDensity * density;

            var targetInteriorCount = Mathf.RoundToInt(volume * targetInteriorDensity * 0.1f);
            targetInteriorCount = Mathf.Clamp(targetInteriorCount, 10, 300); // Reasonable limits

            // Use Poisson disk sampling for good distribution
            var points = PoissonDiskSampling3D(bounds, targetInteriorCount);

            // Filter points to only those inside the mesh
            foreach (var point in points)
            {
                if (IsPointInsideMesh(surfaceVertices, triangles, point))
                {
                    interiorPoints.Add(point);
                }
            }

            Debug.Log($"Interior point generation: {points.Count} candidates -> {interiorPoints.Count} inside mesh");
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

            //  Debug.Log($"Generated {edgeToTriangles.Count(kvp => kvp.Value.Count == 2)} bending constraints");
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

        public static bool GenerateCube(SoftBodySettings settings,
            Transform transform,
            out List<Particle> particles,
            out List<Constraint> constraints,
            out List<VolumeConstraint> volumeConstraints,
            out List<int> indices)
        {
            particles = new List<Particle>();
            constraints = new List<Constraint>();
            volumeConstraints = new List<VolumeConstraint>();
            indices = new List<int>();

            GenerateCubeData(particles, constraints, volumeConstraints, indices, settings, transform);

            return true;
        }

        private static bool AddConstraintWithValidation(List<Particle> particles,
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

        private static void GenerateCubeData(List<Particle> particles,
            List<Constraint> constraints,
            List<VolumeConstraint> volumeConstraints,
            List<int> indices,
            SoftBodySettings settings, Transform transform)
        {
            var res = settings.resolution;
            var spacing = new Vector3(
                settings.size.x / (res - 1),
                settings.size.y / (res - 1),
                settings.size.z / (res - 1)
            );

            // Generate particles in a 3D grid
            for (var x = 0; x < res; x++)
            {
                for (var y = 0; y < res; y++)
                {
                    for (var z = 0; z < res; z++)
                    {
                        var pos = new Vector3(
                            x * spacing.x - settings.size.x * 0.5f,
                            y * spacing.y - settings.size.y * 0.5f,
                            z * spacing.z - settings.size.z * 0.5f
                        );

                        var particle = new Particle
                        {
                            Position = transform.TransformPoint(pos),
                            Velocity = Vector4.zero,
                            Force = Vector4.zero,
                            InvMass = 1f / settings.mass
                        };

                        particles.Add(particle);
                    }
                }
            }

            GenerateStructuralConstraints(settings, particles, constraints); // Main edges
            GenerateShearConstraints(settings, particles, constraints); // Face diagonals  
            GenerateBendConstraints(settings, particles, constraints); // Volume diagonals
            GenerateVolumeConstraints(settings, particles, volumeConstraints); // Volume preservation
            GenerateMeshTopology(settings, indices);
        }

        private static void GenerateVolumeConstraints(SoftBodySettings settings, List<Particle> particles,
            List<VolumeConstraint> volumeConstraints)
        {
            var res = settings.resolution;

            var compliance = settings.volumeCompliance;

            for (var x = 0; x < res - 1; x++)
            {
                for (var y = 0; y < res - 1; y++)
                {
                    for (var z = 0; z < res - 1; z++)
                    {
                        // Get the 8 corners of the cube cell
                        var p000 = x * res * res + y * res + z;
                        var p100 = (x + 1) * res * res + y * res + z;
                        var p110 = (x + 1) * res * res + (y + 1) * res + z;
                        var p010 = x * res * res + (y + 1) * res + z;
                        var p001 = x * res * res + y * res + z + 1;
                        var p101 = (x + 1) * res * res + y * res + z + 1;
                        var p111 = (x + 1) * res * res + (y + 1) * res + z + 1;
                        var p011 = x * res * res + (y + 1) * res + z + 1;

                        // Decompose the cube into 5 tetrahedra
                        AddTetrahedron(particles, volumeConstraints, p000, p100, p110, p001, compliance);
                        AddTetrahedron(particles, volumeConstraints, p010, p110, p000, p011, compliance);
                        AddTetrahedron(particles, volumeConstraints, p101, p001, p111, p100, compliance);
                        AddTetrahedron(particles, volumeConstraints, p011, p111, p001, p110, compliance);
                        AddTetrahedron(particles, volumeConstraints, p001, p110, p111, p011, compliance);
                    }
                }
            }

            Debug.Log($"Generated {volumeConstraints.Count} volume constraints from mesh");
        }

        private static void AddTetrahedron(List<Particle> particles, List<VolumeConstraint> volumeConstraints, int p1,
            int p2, int p3, int p4, float compliance)
        {
            var pos1 = particles[p1].Position;
            var pos2 = particles[p2].Position;
            var pos3 = particles[p3].Position;
            var pos4 = particles[p4].Position;

            // The signed volume of a tetrahedron is 1/6 * | (a-d) . ((b-d) x (c-d)) |
            var restVolume = Vector3.Dot(pos1 - pos4, Vector3.Cross(pos2 - pos4, pos3 - pos4)) / 6.0f;

            // We only want to resist compression, so only create constraints for positive volumes
            if (restVolume > 0.001f)
            {
                volumeConstraints.Add(new VolumeConstraint
                {
                    P1 = p1, P2 = p2, P3 = p3, P4 = p4,
                    RestVolume = restVolume,
                    Compliance = compliance,
                    Lambda = 0
                });
            }
        }

        private static void GenerateStructuralConstraints(SoftBodySettings settings, List<Particle> particles,
            List<Constraint> constraints)
        {
            var res = settings.resolution;

            for (var x = 0; x < res; x++)
            {
                for (var y = 0; y < res; y++)
                {
                    for (var z = 0; z < res; z++)
                    {
                        var index = x * res * res + y * res + z;

                        if (x < res - 1)
                            AddConstraint(particles, constraints, index, (x + 1) * res * res + y * res + z,
                                settings.structuralCompliance);
                        if (y < res - 1)
                            AddConstraint(particles, constraints, index, x * res * res + (y + 1) * res + z,
                                settings.structuralCompliance);
                        if (z < res - 1)
                            AddConstraint(particles, constraints, index, x * res * res + y * res + (z + 1),
                                settings.structuralCompliance);
                    }
                }
            }
        }

        private static void GenerateShearConstraints(SoftBodySettings settings, List<Particle> particles,
            List<Constraint> constraints)
        {
            var res = settings.resolution;

            for (var x = 0; x < res - 1; x++)
            {
                for (var y = 0; y < res - 1; y++)
                {
                    for (var z = 0; z < res - 1; z++)
                    {
                        // XY face diagonals
                        AddConstraint(particles, constraints,
                            x * res * res + y * res + z,
                            (x + 1) * res * res + (y + 1) * res + z,
                            settings.shearCompliance
                        );

                        // XZ face diagonals  
                        AddConstraint(particles, constraints,
                            x * res * res + y * res + z,
                            (x + 1) * res * res + y * res + (z + 1),
                            settings.shearCompliance
                        );

                        // YZ face diagonals
                        AddConstraint(particles, constraints,
                            x * res * res + y * res + z,
                            x * res * res + (y + 1) * res + (z + 1),
                            settings.shearCompliance
                        );
                    }
                }
            }
        }

        private static void GenerateBendConstraints(SoftBodySettings settings, List<Particle> particles,
            List<Constraint> constraints)
        {
            var res = settings.resolution;

            // Long-range constraints for volume preservation
            for (var x = 0; x < res - 1; x++)
            {
                for (var y = 0; y < res - 1; y++)
                {
                    for (var z = 0; z < res - 1; z++)
                    {
                        // Cube diagonals
                        AddConstraint(particles, constraints,
                            x * res * res + y * res + z,
                            (x + 1) * res * res + (y + 1) * res + (z + 1),
                            settings.bendCompliance
                        );
                    }
                }
            }
        }

        private static void AddConstraint(List<Particle> particles, List<Constraint> constraints, int a, int b,
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

        private static void GenerateMeshTopology(SoftBodySettings settings, List<int> indices)
        {
            indices.Clear();
            var res = settings.resolution;

            // Generate surface triangles (simplified cube faces)
            for (var x = 0; x < res - 1; x++)
            {
                for (var y = 0; y < res - 1; y++)
                {
                    for (var z = 0; z < res - 1; z++)
                    {
                        // Only render surface faces
                        if (x == 0 || x == res - 2 || y == 0 || y == res - 2 || z == 0 || z == res - 2)
                        {
                            AddCubeFace(indices, x, y, z, res);
                        }
                    }
                }
            }
        }

        private static void AddCubeFace(List<int> indices, int x, int y, int z, int res)
        {
            var i000 = x * res * res + y * res + z;
            var i001 = x * res * res + y * res + (z + 1);
            var i010 = x * res * res + (y + 1) * res + z;
            var i011 = x * res * res + (y + 1) * res + (z + 1);
            var i100 = (x + 1) * res * res + y * res + z;
            var i101 = (x + 1) * res * res + y * res + (z + 1);
            var i110 = (x + 1) * res * res + (y + 1) * res + z;
            var i111 = (x + 1) * res * res + (y + 1) * res + (z + 1);

            // Add triangles for visible faces
            if (x == 0) AddQuad(indices, i000, i010, i011, i001); // Left face
            if (x == res - 2) AddQuad(indices, i100, i101, i111, i110); // Right face
            if (y == 0) AddQuad(indices, i000, i001, i101, i100); // Bottom face
            if (y == res - 2) AddQuad(indices, i010, i110, i111, i011); // Top face
            if (z == 0) AddQuad(indices, i000, i100, i110, i010); // Front face
            if (z == res - 2) AddQuad(indices, i001, i011, i111, i101); // Back face
        }

        private static void AddQuad(List<int> indices, int a, int b, int c, int d)
        {
            // Triangle 1 (counter-clockwise for correct normals)
            indices.Add(a);
            indices.Add(c);
            indices.Add(b);

            // Triangle 2 (counter-clockwise for correct normals)
            indices.Add(a);
            indices.Add(d);
            indices.Add(c);
        }

        private static void DebugConstraintConnectivity(List<Particle> particles, List<Constraint> constraints, SoftBodySettings settings)
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
            CheckNetworkConnectivity(particles, constraints, settings);
        }

        private static void CheckNetworkConnectivity(List<Particle> particles, List<Constraint> constraints, SoftBodySettings settings)
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

            Debug.Log($"CONNECTIVITY: Found {components.Count} separate components");
            for (var i = 0; i < components.Count; i++)
            {
                Debug.Log($"Component {i}: {components[i].Count} particles");
                if (components[i].Count < 10) // Log small components
                {
                    Debug.Log($"Small component particles: [{string.Join(", ", components[i])}]");
                }
            }

            // Add switch here for more advanced connectivity handling
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
    
            Debug.Log($"Connecting {components.Count} disconnected components...");
    
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
                            var distance = Vector3.Distance(particles[particleI].Position, particles[particleJ].Position);
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
                        AddConstraintWithValidation(particles, constraints, bestParticleI, bestParticleJ, 
                            settings.structuralCompliance * 0.5f); // Slightly more flexible bridge
                    
                        Debug.Log($"Added bridge constraint between components {i} and {j}: particles {bestParticleI} <-> {bestParticleJ} (distance: {minDistance:F3})");
                    }
                }
            }
        }
        
        private static void AddProximityConstraints(List<Particle> particles, List<Constraint> constraints, 
            SoftBodySettings settings)
        {
            Debug.Log("Adding proximity-based constraints...");
            
            var maxConnectionDistance = CalculateOptimalConnectionDistance(constraints);
    
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
                            AddConstraintWithValidation(particles, constraints, i, j, 
                                settings.structuralCompliance * 2f); // More flexible proximity constraints
                            proximityConstraintsAdded++;
                        }
                    }
                }
            }
    
            Debug.Log($"Added {proximityConstraintsAdded} proximity constraints");
        }
        
        private static float CalculateOptimalConnectionDistance(List<Constraint> constraints)
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
    
            Debug.Log($"Calculated optimal connection distance: {connectionDistance:F4} (avg constraint: {avgConstraintLength:F4})");
            return connectionDistance;
        }
        
        private static void HybridConnectivity(List<Particle> particles, List<Constraint> constraints, List<List<int>> components, SoftBodySettings settings)
        {
            ForceConnectivity(particles, constraints, components, settings);
            AddProximityConstraints(particles, constraints, settings);
        }

        private static List<Vector3> PoissonDiskSampling3D(Bounds bounds, int targetCount)
        {
            var points = new List<Vector3>();
            var minDistance = Mathf.Pow(bounds.size.x * bounds.size.y * bounds.size.z / targetCount, 1f / 3f) * 0.8f;

            var maxAttempts = targetCount * 30;
            var attempts = 0;

            while (points.Count < targetCount && attempts < maxAttempts)
            {
                var candidate = new Vector3(
                    UnityEngine.Random.Range(bounds.min.x, bounds.max.x),
                    UnityEngine.Random.Range(bounds.min.y, bounds.max.y),
                    UnityEngine.Random.Range(bounds.min.z, bounds.max.z)
                );

                var tooClose = false;
                foreach (var existing in points)
                {
                    if (Vector3.Distance(candidate, existing) < minDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    points.Add(candidate);
                }

                attempts++;
            }

            Debug.Log($"Poisson sampling: {points.Count} points in {attempts} attempts");
            return points;
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

            return (intersections % 2) == 1; // Odd number = inside
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

        private static float CalculateSurfaceArea(Vector3[] vertices, int[] triangles)
        {
            var totalArea = 0f;

            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]];
                var b = vertices[triangles[i + 1]];
                var c = vertices[triangles[i + 2]];

                // Triangle area = 0.5 * |cross product|
                var cross = Vector3.Cross(b - a, c - a);
                totalArea += cross.magnitude * 0.5f;
            }

            return totalArea;
        }

        private static float CalculateAverageEdgeLength(List<Particle> particles)
        {
            if (particles.Count < 2) return 1f;

            var totalDistance = 0f;
            var sampleCount = Mathf.Min(100, particles.Count * particles.Count / 4);

            for (var i = 0; i < sampleCount; i++)
            {
                var a = UnityEngine.Random.Range(0, particles.Count);
                var b = UnityEngine.Random.Range(0, particles.Count);
                if (a != b)
                {
                    totalDistance += Vector3.Distance(particles[a].Position, particles[b].Position);
                }
            }

            return totalDistance / sampleCount;
        }

        private static void CreateSurfaceConstraints(List<int> surfaceIndices, int[] triangles,
            List<Constraint> constraints, float compliance)
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
            // Simple approach: create tetrahedra connecting interior points to nearby surface points
            foreach (var interiorIdx in interiorIndices)
            {
                var interiorPos = particles[interiorIdx].Position;

                // Find 3 closest surface points to form tetrahedra
                var closestSurface = surfaceIndices
                    .Select(idx => new
                        { index = idx, distance = Vector3.Distance(interiorPos, particles[idx].Position) })
                    .OrderBy(x => x.distance)
                    .Take(6) // Take more than 3 to create multiple tetrahedra
                    .ToList();

                // Create tetrahedra from combinations of 3 surface points + 1 interior point
                for (var i = 0; i < closestSurface.Count - 2; i++)
                {
                    for (var j = i + 1; j < closestSurface.Count - 1; j++)
                    {
                        for (var k = j + 1; k < closestSurface.Count; k++)
                        {
                            var p1 = closestSurface[i].index;
                            var p2 = closestSurface[j].index;
                            var p3 = closestSurface[k].index;
                            var p4 = interiorIdx;

                            AddVolumeConstraint(particles, volumeConstraints, p1, p2, p3, p4,
                                settings.volumeCompliance);
                        }
                    }
                }
            }
        }

        private static void AddVolumeConstraint(List<Particle> particles, List<VolumeConstraint> volumeConstraints,
            int p1, int p2, int p3, int p4, float compliance)
        {
            var pos1 = particles[p1].Position;
            var pos2 = particles[p2].Position;
            var pos3 = particles[p3].Position;
            var pos4 = particles[p4].Position;

            // Calculate tetrahedron volume
            var restVolume = Vector3.Dot(pos1 - pos4, Vector3.Cross(pos2 - pos4, pos3 - pos4)) / 6.0f;

            if (Mathf.Abs(restVolume) > 0.001f)
            {
                volumeConstraints.Add(new VolumeConstraint
                {
                    P1 = p1, P2 = p2, P3 = p3, P4 = p4,
                    RestVolume = Mathf.Abs(restVolume),
                    Compliance = compliance,
                    Lambda = 0
                });
            }
        }
    }
}
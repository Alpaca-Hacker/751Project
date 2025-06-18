using System.Collections.Generic;
using SoftBody.Scripts.Models;
using UnityEngine;

namespace SoftBody.Scripts
{
    public static class SoftBodyGenerator
    {
        public static bool GenerateCube( SoftBodySettings settings, 
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
            
            GenerateStructuralConstraints(settings, particles, constraints);  // Main edges
            GenerateShearConstraints(settings, particles, constraints);       // Face diagonals  
            GenerateBendConstraints(settings, particles, constraints);        // Volume diagonals
            GenerateVolumeConstraints(settings, particles, volumeConstraints); // Volume preservation
            GenerateMeshTopology(settings, indices);
        }

        private static void GenerateVolumeConstraints( SoftBodySettings settings, List<Particle> particles, List<VolumeConstraint> volumeConstraints)
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

        private static void AddTetrahedron(List<Particle> particles, List<VolumeConstraint> volumeConstraints, int p1, int p2, int p3, int p4, float compliance)
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

        private static void GenerateStructuralConstraints(SoftBodySettings settings, List<Particle> particles, List<Constraint> constraints)
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
                            AddConstraint(particles, constraints, index, (x + 1) * res * res + y * res + z, settings.structuralCompliance);
                        if (y < res - 1)
                            AddConstraint(particles, constraints,index, x * res * res + (y + 1) * res + z, settings.structuralCompliance);
                        if (z < res - 1)
                            AddConstraint(particles, constraints,index, x * res * res + y * res + (z + 1), settings.structuralCompliance);
                    }
                }
            }
        }

        private static void GenerateShearConstraints(SoftBodySettings settings, List<Particle> particles, List<Constraint> constraints)
        {
            var res = settings.resolution;
            
            for (var x = 0; x < res - 1; x++)
            {
                for (var y = 0; y < res - 1; y++)
                {
                    for (var z = 0; z < res - 1; z++)
                    {
                        // XY face diagonals
                        AddConstraint( particles, constraints,
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
        private static void GenerateBendConstraints(SoftBodySettings settings, List<Particle> particles, List<Constraint> constraints)
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
        
        private static void AddConstraint(List<Particle> particles, List<Constraint> constraints, int a, int b, float compliance)
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
            if (x == 0) AddQuad(indices,i000, i010, i011, i001); // Left face
            if (x == res - 2) AddQuad(indices, i100, i101, i111, i110); // Right face
            if (y == 0) AddQuad(indices,i000, i001, i101, i100); // Bottom face
            if (y == res - 2) AddQuad(indices,i010, i110, i111, i011); // Top face
            if (z == 0) AddQuad(indices,i000, i100, i110, i010); // Front face
            if (z == res - 2) AddQuad(indices,i001, i011, i111, i101); // Back face
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
    }
}
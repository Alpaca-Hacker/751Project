using System.Collections.Generic;
using System.Linq;
using SoftBody.Scripts.Models;
using SoftBody.Scripts.Utilities;
using UnityEngine;

namespace SoftBody.Scripts.Generation
{
    public class MeshBasedGenerator : ISoftBodyGenerator
    {
        public GenerationResult Generate(SoftBodySettings settings, Transform transform)
        {
            var mesh = settings.inputMesh;
            if (mesh == null)
            {
                Debug.LogError("Input mesh is null!");
                return new GenerationResult();
            }

            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            var originalUVs = mesh.uv;

            if (settings.debugMessages)
            {
                Debug.Log($"Processing mesh '{mesh.name}': {vertices.Length} vertices, {triangles.Length / 3} triangles");
            }

            // Weld vertices to remove duplicates
            var (weldedVertices, weldedTriangles, vertexMapping) = MeshUtilities.WeldVertices(vertices, triangles, settings);

            // Remap UVs after welding
            Vector2[] weldedUVs = null;
            if (originalUVs != null && originalUVs.Length > 0)
            {
                weldedUVs = MeshUtilities.RemapUVsAfterWelding(originalUVs, vertexMapping, weldedVertices.Length, settings);
            }

            // Create particles from welded vertices
            var particles = CreateParticlesFromVertices(weldedVertices, settings, transform);

            // Generate constraints from mesh topology
            var constraints = ConstraintGenerator.GenerateFromMesh(particles, weldedTriangles, settings);

            // Generate volume constraints if enabled
            var volumeConstraints = new List<VolumeConstraint>();
            if (settings.enableStuffingMode)
            {
                // This would call into a VolumeConstraintGenerator
                // volumeConstraints = VolumeConstraintGenerator.Generate(particles, settings);
            }

            return new GenerationResult
            {
                Particles = particles,
                Constraints = constraints,
                VolumeConstraints = volumeConstraints,
                Indices = weldedTriangles.ToList(),
                UVs = weldedUVs
            };
        }

        private List<Particle> CreateParticlesFromVertices(Vector3[] vertices, SoftBodySettings settings, Transform transform)
        {
            var particles = new List<Particle>();

            foreach (var vertex in vertices)
            {
                var worldPos = transform.TransformPoint(vertex);
                particles.Add(new Particle
                {
                    Position = worldPos,
                    Velocity = Vector4.zero,
                    Force = Vector4.zero,
                    InvMass = 1f / settings.mass
                });
            }

            return particles;
        }
    }
}
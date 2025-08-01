using System.Collections.Generic;
using SoftBody.Scripts.Models;
using UnityEngine;

namespace SoftBody.Scripts.Generation
{
    public class ProceduralCubeGenerator : ISoftBodyGenerator
    {
        public GenerationResult Generate(SoftBodySettings settings, Transform transform)
        {
            var particles = new List<Particle>();
            var constraints = new List<Constraint>();
            var volumeConstraints = new List<VolumeConstraint>();
            var indices = new List<int>();

            // Generate cube data using the existing SoftBodyCubeGenerator logic
            SoftBodyCubeGenerator.GenerateCubeData(particles, constraints, volumeConstraints, indices, settings, transform);
            
            var uvs = new Vector2[particles.Count];
            for (var i = 0; i < particles.Count; i++)
            {
                // Very simple UV mapping that will at least show the material
                uvs[i] = new Vector2(1f, 1f); // Use a consistent part of the texture
            }

            return new GenerationResult
            {
                Particles = particles,
                Constraints = constraints,
                VolumeConstraints = volumeConstraints,
                Indices = indices,
                UVs = uvs
            };
        }
    }
}
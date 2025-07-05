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

            return new GenerationResult
            {
                Particles = particles,
                Constraints = constraints,
                VolumeConstraints = volumeConstraints,
                Indices = indices,
                UVs = null // Procedural cubes don't need custom UVs
            };
        }
    }
}
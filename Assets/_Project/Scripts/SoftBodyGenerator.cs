using System.Collections.Generic;
using SoftBody.Scripts.Generation;
using SoftBody.Scripts.Models;
using UnityEngine;

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
            // Validate settings
            var validationResult = SoftBodySettingsValidator.Validate(settings);
            if (!validationResult.IsValid)
            {
                Debug.LogError($"Invalid soft body settings: {string.Join(", ", validationResult.Errors)}");
                CreateEmptyResult(out particles, out constraints, out volumeConstraints, out indices, out weldedUVs);
                return;
            }

            // Log warnings if any
            foreach (var warning in validationResult.Warnings)
            {
                Debug.LogWarning($"Soft body settings warning: {warning}");
            }

            // Generate using appropriate generator
            var generator = SoftBodyGeneratorFactory.CreateGenerator(settings);
            var result = generator.Generate(settings, transform);

            if (!result.IsValid)
            {
                Debug.LogError("Failed to generate valid soft body data");
                CreateEmptyResult(out particles, out constraints, out volumeConstraints, out indices, out weldedUVs);
                return;
            }

            // Apply stuffing mode if enabled
            if (settings.enableStuffingMode)
            {
                Debug.Log("Applying stuffing mode...");
                StuffingGenerator.CreateStuffedBodyStructure(result.Particles, result.Constraints, 
                    result.VolumeConstraints, settings, transform);
                Debug.Log($"After stuffing: {result.Particles.Count} particles, {result.Constraints.Count} constraints, {result.VolumeConstraints.Count} volume constraints");
            }
            else
            {
                Debug.Log("Stuffing mode not enabled, skipping...");
            }

            // Final validation and cleanup
            ValidateAndCleanConstraints(result.Particles, result.Constraints, settings);

            // Output results
            particles = result.Particles;
            constraints = result.Constraints;
            volumeConstraints = result.VolumeConstraints;
            indices = result.Indices;
            weldedUVs = result.UVs;

            if (settings.debugMessages)
            {
                Debug.Log($"Soft body generation complete: {particles.Count} particles, " +
                         $"{constraints.Count} constraints, {volumeConstraints.Count} volume constraints");
            }
        }

        private static void CreateEmptyResult(out List<Particle> particles, out List<Constraint> constraints,
            out List<VolumeConstraint> volumeConstraints, out List<int> indices, out Vector2[] weldedUVs)
        {
            particles = new List<Particle>();
            constraints = new List<Constraint>();
            volumeConstraints = new List<VolumeConstraint>();
            indices = new List<int>();
            weldedUVs = null;
        }

        private static void ValidateAndCleanConstraints(List<Particle> particles, List<Constraint> constraints, 
            SoftBodySettings settings)
        {
            var removedCount = 0;

            for (var i = constraints.Count - 1; i >= 0; i--)
            {
                var c = constraints[i];
                var restLength = Vector3.Distance(particles[c.ParticleA].Position, particles[c.ParticleB].Position);

                if (restLength < 0.001f)
                {
                    constraints.RemoveAt(i);
                    removedCount++;
                    continue;
                }

                c.RestLength = restLength;
                constraints[i] = c;
            }

            if (settings.debugMessages && removedCount > 0)
            {
                Debug.Log($"Removed {removedCount} invalid constraints during validation");
            }
        }

        // Keeping these methods for backwards compatibility
        public static bool AddConstraintWithValidation(List<Particle> particles, List<Constraint> constraints, 
            int a, int b, float compliance)
        {
            return ConstraintGenerator.AddConstraintWithValidation(particles, constraints, a, b, compliance);
        }

        public static void AddConstraint(List<Particle> particles, List<Constraint> constraints, int a, int b, float compliance)
        {
            ConstraintGenerator.AddConstraintWithValidation(particles, constraints, a, b, compliance);
        }
    }
}
using System.Collections.Generic;

namespace SoftBody.Scripts
{
    public static class SoftBodySettingsValidator
    {
        public static ValidationResult Validate(SoftBodySettings settings)
        {
            var result = new ValidationResult();

            // Physics validation
            if (settings.mass <= 0)
                result.AddError("Mass must be positive");

            if (settings.damping < 0 || settings.damping > 1)
                result.AddWarning("Damping should be between 0 and 1");

            if (settings.solverIterations < 1)
                result.AddError("Solver iterations must be at least 1");

            // Mesh validation
            if (settings.useRandomMesh && settings.randomMeshes.Length == 0)
                result.AddWarning("Random mesh enabled but no meshes provided");

            if (!settings.useProceduralCube && settings.inputMesh == null && (!settings.useRandomMesh || settings.randomMeshes.Length == 0))
                result.AddError("No valid mesh source configured");

            // Cube validation
            if (settings.useProceduralCube)
            {
                if (settings.resolution < 2)
                    result.AddError("Cube resolution must be at least 2");

                if (settings.size.x <= 0 || settings.size.y <= 0 || settings.size.z <= 0)
                    result.AddError("Cube size must be positive in all dimensions");
            }

            return result;
        }
    }

    public class ValidationResult
    {
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();
        
        public bool IsValid => Errors.Count == 0;

        public void AddError(string error) => Errors.Add(error);
        public void AddWarning(string warning) => Warnings.Add(warning);
    }
}
using UnityEngine;

namespace SoftBody.Scripts.Generation
{
    public static class SoftBodyGeneratorFactory
    {
        public static ISoftBodyGenerator CreateGenerator(SoftBodySettings settings)
        {
            // Handle random mesh selection first
            if (settings.useRandomMesh && settings.randomMeshes.Length > 0)
            {
                SelectRandomMesh(settings);
            }

            // Choose appropriate generator based on settings
            if (settings.inputMesh != null && !settings.useProceduralCube)
            {
                if (settings.useTetrahedralizationForHighPoly && 
                    settings.inputMesh.vertices.Length > settings.maxSurfaceVerticesBeforeTetra)
                {
                    if (settings.debugMessages)
                    {
                        Debug.Log($"Using TetrahedralGenerator for {settings.inputMesh.name} ({settings.inputMesh.vertices.Length} vertices)");
                    }
                    return new TetrahedralGenerator();
                }

                if (settings.debugMessages)
                {
                    Debug.Log($"Using MeshBasedGenerator for {settings.inputMesh.name} ({settings.inputMesh.vertices.Length} vertices)");
                }

                return new MeshBasedGenerator();
            }

            return new ProceduralCubeGenerator();
        }

        private static void SelectRandomMesh(SoftBodySettings settings)
        {
            var randomIndex = Random.Range(0, settings.randomMeshes.Length);
            var selectedMesh = settings.randomMeshes[randomIndex];

            if (selectedMesh != null)
            {
                settings.inputMesh = selectedMesh;
                settings.useProceduralCube = false;

                if (settings.debugMessages)
                {
                    Debug.Log($"Selected random mesh: {selectedMesh.name}");
                }
            }
        }
    }
}
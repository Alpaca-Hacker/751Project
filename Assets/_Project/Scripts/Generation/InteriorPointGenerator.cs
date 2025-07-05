using System.Collections.Generic;
using SoftBody.Scripts.Utilities;
using UnityEngine;

namespace SoftBody.Scripts.Generation
{
    public static class InteriorPointGenerator
    {
        public static List<Vector3> GenerateForMesh(Vector3[] surfaceVertices, int[] triangles, 
            float density, bool debugMessages)
        {
            var bounds = MeshUtilities.CalculateMeshBounds(surfaceVertices);
            var interiorPoints = new List<Vector3>();

            var targetCount = CalculateTargetInteriorCount(surfaceVertices.Length, density);
            
            if (debugMessages)
            {
                Debug.Log($"Generating interior points: target={targetCount} for {surfaceVertices.Length} surface vertices");
            }

            var shrinkFactor = 0.8f;
            var shrunkBounds = new Bounds(bounds.center, bounds.size * shrinkFactor);

            var maxAttempts = targetCount * 50;
            var attempts = 0;

            while (interiorPoints.Count < targetCount && attempts < maxAttempts)
            {
                var candidate = GenerateRandomPointInBounds(shrunkBounds);

                if (MeshUtilities.IsPointInsideMesh(surfaceVertices, triangles, candidate))
                {
                    interiorPoints.Add(candidate);
                }

                attempts++;
            }

            if (debugMessages)
            {
                Debug.Log($"Interior point generation: {attempts} attempts -> {interiorPoints.Count} points inside mesh");
            }

            return interiorPoints;
        }

        private static int CalculateTargetInteriorCount(int surfaceVertexCount, float density)
        {
            var targetCount = Mathf.RoundToInt(surfaceVertexCount * density * 0.1f);
            return Mathf.Clamp(targetCount, 20, 150);
        }

        private static Vector3 GenerateRandomPointInBounds(Bounds bounds)
        {
            return new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y),
                Random.Range(bounds.min.z, bounds.max.z)
            );
        }
    }
}
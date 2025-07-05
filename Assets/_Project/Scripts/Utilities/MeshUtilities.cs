using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SoftBody.Scripts.Utilities
{
    public static class MeshUtilities
    {
        public static (Vector3[] vertices, int[] triangles, int[] vertexMapping) WeldVertices(
            Vector3[] originalVertices, int[] originalTriangles, SoftBodySettings settings, float weldDistance = 0.0001f)
        {
            
            var uniqueVertices = new List<Vector3>();
            var vertexMapping = new int[originalVertices.Length];

            // Find unique vertices
            for (var i = 0; i < originalVertices.Length; i++)
            {
                var vertex = originalVertices[i];
                var existingIndex = FindExistingVertex(uniqueVertices, vertex, weldDistance);

                if (existingIndex == -1)
                {
                    vertexMapping[i] = uniqueVertices.Count;
                    uniqueVertices.Add(vertex);
                }
                else
                {
                    vertexMapping[i] = existingIndex;
                }
            }

            // Remap triangle indices
            var weldedTriangles = new int[originalTriangles.Length];
            var maxOriginalIndex = -1;
            var maxWeldedIndex = -1;
    
            for (var i = 0; i < originalTriangles.Length; i++)
            {
                var originalIndex = originalTriangles[i];
                maxOriginalIndex = Mathf.Max(maxOriginalIndex, originalIndex);
        
                if (originalIndex >= vertexMapping.Length)
                {
                    Debug.LogError($"Triangle references vertex {originalIndex} but vertex mapping only has {vertexMapping.Length} entries!");
                    weldedTriangles[i] = 0; // Safe fallback
                }
                else
                {
                    weldedTriangles[i] = vertexMapping[originalIndex];
                    maxWeldedIndex = Mathf.Max(maxWeldedIndex, weldedTriangles[i]);
                }
            }

            if (settings.debugMessages)
            {
                Debug.Log($"Vertex welding: {originalVertices.Length} -> {uniqueVertices.Count} vertices");
            }

            return (uniqueVertices.ToArray(), weldedTriangles, vertexMapping);
        }

        public static Vector2[] RemapUVsAfterWelding(Vector2[] originalUVs, int[] vertexMapping, 
            int weldedVertexCount, SoftBodySettings settings)
        {
            if (originalUVs == null || originalUVs.Length == 0)
                return System.Array.Empty<Vector2>();

            var weldedUVs = new Vector2[weldedVertexCount];
            var uvCounts = new int[weldedVertexCount];

            // Average UVs for welded vertices
            for (var i = 0; i < originalUVs.Length; i++)
            {
                if (i >= vertexMapping.Length) continue;

                var weldedIndex = vertexMapping[i];
                if (weldedIndex >= weldedVertexCount) continue;

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

            if (settings.debugMessages)
            {
                Debug.Log($"UV remapping: {originalUVs.Length} -> {weldedUVs.Length} UVs");
            }

            return weldedUVs;
        }

        public static Bounds CalculateMeshBounds(Vector3[] vertices)
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

        public static float CalculateAverageEdgeLength(List<Vector3> positions, int sampleCount = 100)
        {
            if (positions.Count < 2) return 1f;

            var totalDistance = 0f;
            var actualSamples = Mathf.Min(sampleCount, positions.Count * positions.Count / 4);

            for (var i = 0; i < actualSamples; i++)
            {
                var a = Random.Range(0, positions.Count);
                var b = Random.Range(0, positions.Count);
                if (a != b)
                {
                    totalDistance += Vector3.Distance(positions[a], positions[b]);
                }
            }

            return totalDistance / actualSamples;
        }

        public static bool IsPointInsideMesh(Vector3[] vertices, int[] triangles, Vector3 point)
        {
            // Ray casting algorithm - count intersections with mesh
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

        public static bool RayTriangleIntersect(Vector3 rayOrigin, Vector3 rayDirection,
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
            return t > epsilon;
        }

        private static int FindExistingVertex(List<Vector3> uniqueVertices, Vector3 vertex, float weldDistance)
        {
            for (var j = 0; j < uniqueVertices.Count; j++)
            {
                if (Vector3.Distance(vertex, uniqueVertices[j]) < weldDistance)
                {
                    return j;
                }
            }
            return -1;
        }
    }
}
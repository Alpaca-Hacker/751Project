using UnityEngine;
using System.Collections.Generic;
using SoftBody.Scripts.Pooling;

namespace SoftBody.Scripts.Utilities
{
    public class MeshArrayHelper : MonoBehaviour
    {
        [Header("Auto-populate from FBX Files")]
        public string fbxFolderPath = "Plush_Toys–Low-Poly_BigPack/Models";
        
        [Header("Filter Options")]
        [Tooltip("Only include meshes with names containing these terms (leave empty for all)")]
        public string[] includeNameFilters = new string[0];
        [Tooltip("Exclude meshes with names containing these terms")]
        public string[] excludeNameFilters = { "Collider", "LOD", "_low", "_high" };
        
        [Header("Preview")]
        [SerializeField] private List<string> foundMeshNames = new();
        
        [ContextMenu("Find All Meshes in FBX Files")]
        private void FindMeshesInFBX()
        {
            foundMeshNames.Clear();
            
            #if UNITY_EDITOR
            var fbxFiles = UnityEditor.AssetDatabase.FindAssets("t:Model", new[] { $"Assets/{fbxFolderPath}" });
            var meshList = new List<Mesh>();
            
            foreach (var guid in fbxFiles)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
                
                foreach (var asset in assets)
                {
                    if (asset is Mesh mesh)
                    {
                        // Apply filters
                        if (ShouldIncludeMesh(mesh.name))
                        {
                            meshList.Add(mesh);
                            foundMeshNames.Add($"{System.IO.Path.GetFileName(path)} -> {mesh.name}");
                        }
                    }
                }
            }
            
            // Apply to soft body
            var poolGenerator = GetComponent<SoftBodyPoolGenerator>();
            if (poolGenerator != null)
            {
                poolGenerator.meshVariants= meshList.ToArray();
                UnityEditor.EditorUtility.SetDirty(poolGenerator);
            }
            
            Debug.Log($"Found {meshList.Count} meshes in {fbxFiles.Length} FBX files from {fbxFolderPath}");
            
            // Log the found meshes
            if (foundMeshNames.Count > 0)
            {
                Debug.Log("Found meshes:\n" + string.Join("\n", foundMeshNames));
            }
            #endif
        }
        
        private bool ShouldIncludeMesh(string meshName)
        {
            // Check exclude filters first
            foreach (var exclude in excludeNameFilters)
            {
                if (!string.IsNullOrEmpty(exclude) && meshName.ToLower().Contains(exclude.ToLower()))
                {
                    return false;
                }
            }
            
            // Check include filters (if any are specified)
            if (includeNameFilters.Length > 0)
            {
                foreach (var include in includeNameFilters)
                {
                    if (!string.IsNullOrEmpty(include) && meshName.ToLower().Contains(include.ToLower()))
                    {
                        return true;
                    }
                }
                return false; // If include filters exist but none matched
            }
            
            return true; // No include filters, so include by default
        }
        
        [ContextMenu("List All Meshes (Preview Only)")]
        private void PreviewMeshesInFBX()
        {
            foundMeshNames.Clear();
            
            #if UNITY_EDITOR
            var fbxFiles = UnityEditor.AssetDatabase.FindAssets("t:Model", new[] { $"Assets/{fbxFolderPath}" });
            
            foreach (var guid in fbxFiles)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
                
                Debug.Log($"=== FBX File: {System.IO.Path.GetFileName(path)} ===");
                
                foreach (var asset in assets)
                {
                    if (asset is Mesh mesh)
                    {
                        var included = ShouldIncludeMesh(mesh.name) ? "[INCLUDED]" : "[EXCLUDED]";
                        var info = $"{included} Mesh: {mesh.name} (Verts: {mesh.vertexCount}, Tris: {mesh.triangles.Length/3})";
                        Debug.Log(info);
                        foundMeshNames.Add(info);
                    }
                }
            }
            
            Debug.Log($"Preview complete. Found {foundMeshNames.Count} total meshes in {fbxFiles.Length} FBX files.");
            #endif
        }
        
        [ContextMenu("Clear Random Meshes")]
        private void ClearRandomMeshes()
        {
            var softBody = GetComponent<SoftBodyPhysics>();
            if (softBody != null)
            {
                softBody.settings.randomMeshes = new Mesh[0];
                softBody.settings.useRandomMesh = false;
                
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(softBody);
                #endif
                
                Debug.Log("Cleared random meshes array");
            }
        }
    }
}
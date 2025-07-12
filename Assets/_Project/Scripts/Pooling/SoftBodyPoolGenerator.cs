#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace SoftBody.Scripts.Pooling
{
    public class SoftBodyPoolGenerator : MonoBehaviour
    {
        [Header("Generation Settings")]
        public GameObject softBodyPrefab;
        public Mesh[] meshVariants;
        
        [Header("Generated Pool")]
        public List<GameObject> generatedToys = new ();
        
        [ContextMenu("Generate Pool in Editor")]
        public void GeneratePoolInEditor()
        {
            if (softBodyPrefab == null)
            {
                Debug.LogError("No softBodyPrefab assigned!");
                return;
            }
            
            if (meshVariants == null || meshVariants.Length == 0)
            {
                Debug.LogError("No mesh variants assigned!");
                return;
            }
            
            // Clear existing pool
            ClearExistingPool();
            
            Debug.Log($"Generating {meshVariants.Length} toy variants in editor...");
            
            for (var i = 0; i < meshVariants.Length; i++)
            {
                var mesh = meshVariants[i];
                if (mesh == null) continue;
                
                // Create the toy
                var toy = PrefabUtility.InstantiatePrefab(softBodyPrefab, transform) as GameObject;
                toy.name = $"Toy_{mesh.name}";
                
                // Configure the soft body
                var softBody = toy.GetComponent<SoftBodyPhysics>();
                if (softBody != null)
                {
                    softBody.settings.inputMesh = mesh;
                    softBody.settings.useRandomMesh = false;
                    softBody.settings.changeOnActivation = false;
                }
                
                // Add to our list
                generatedToys.Add(toy);
                
                // Start inactive
                toy.SetActive(false);
                
                Debug.Log($"Generated toy {i+1}/{meshVariants.Length}: {mesh.name}");
            }
            
            Debug.Log($"Pool generation complete! Created {generatedToys.Count} unique toys.");
            
            // Mark scene as dirty so changes are saved
            EditorUtility.SetDirty(gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
        
        [ContextMenu("Clear Pool")]
        public void ClearExistingPool()
        {
            for (var i = generatedToys.Count - 1; i >= 0; i--)
            {
                if (generatedToys[i] != null)
                {
                    DestroyImmediate(generatedToys[i]);
                }
            }
            generatedToys.Clear();
            
            // Also clear any children that might be leftover
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }
        }
        
        [ContextMenu("Validate Pool")]
        public void ValidatePool()
        {
            Debug.Log($"Pool contains {generatedToys.Count} toys:");
            for (var i = 0; i < generatedToys.Count; i++)
            {
                var toy = generatedToys[i];
                if (toy == null)
                {
                    Debug.LogWarning($"  {i}: NULL TOY");
                    continue;
                }
                
                var softBody = toy.GetComponent<SoftBodyPhysics>();
                var meshName = softBody?.settings.inputMesh?.name ?? "NO_MESH";
                Debug.Log($"  {i}: {toy.name} - Mesh: {meshName}");
            }
        }
        [ContextMenu("Generate Pool and Physics Data")]
        public void GeneratePoolAndPhysicsData()
        {
            GeneratePoolInEditor();
            
            foreach (var toy in generatedToys)
            {
                var softBody = toy.GetComponent<SoftBodyPhysics>();
                if (softBody != null)
                {
                    softBody.GeneratePhysicsDataInEditor();
                }
            }
    
            Debug.Log($"Generated physics data for all {generatedToys.Count} toys in pool");
        }
    }
}
#endif
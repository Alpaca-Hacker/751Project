using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoftBody.Scripts.Core
{
    /// <summary>
    /// Manages cache cleanup when scenes change
    /// </summary>
    public class SoftBodySceneManager : MonoBehaviour
    {
        private void Awake()
        {
            // Ensure this persists across scene loads
            DontDestroyOnLoad(gameObject);
            
            // Subscribe to scene change events
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Clear caches when new scene loads
            SoftBodyCacheManager.ClearAllCaches();
    
            // Force immediate cache population to avoid timing issues
            StartCoroutine(InitializeCacheAfterFrame());
        }

        private System.Collections.IEnumerator InitializeCacheAfterFrame()
        {
            // Wait one frame for all objects to be properly initialized
            yield return null;
    
            // Force cache update
            var colliders = SoftBodyCacheManager.GetCachedColliders();
            Debug.Log($"Scene cache initialized with {colliders.Count} colliders");
        }

        private void OnSceneUnloaded(Scene scene)
        {
            // Clear caches when scene unloads
            SoftBodyCacheManager.ClearAllCaches();
        }
    }
}
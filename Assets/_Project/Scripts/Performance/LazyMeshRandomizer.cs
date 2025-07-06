using UnityEngine;
using System.Collections;

namespace SoftBody.Scripts.Performance
{
    public class LazyMeshRandomizer : MonoBehaviour
    {
        private SoftBodyPhysics _softBody;
        private Mesh[] _availableMeshes;
        private bool _isRandomizing = false;
        private bool _hasBeenRandomized = false;
        
        [Header("Performance Settings")]
        public float maxRandomizationDelay = 0.2f;
        public bool randomizeOnFirstActivation = true;
        
        private void Start()
        {
            _softBody = GetComponent<SoftBodyPhysics>();
            if (_softBody != null && _softBody.settings.useRandomMesh)
            {
                _availableMeshes = _softBody.settings.randomMeshes;
                // Disable automatic randomization to prevent performance hit
                _softBody.settings.changeOnActivation = false;
                
                if (_availableMeshes == null || _availableMeshes.Length == 0)
                {
                    Debug.LogWarning($"LazyMeshRandomizer on {gameObject.name}: No random meshes available!");
                    enabled = false;
                }
            }
            else
            {
                enabled = false; // Disable if no random mesh setup
            }
        }
        
        private void OnEnable()
        {
            // Only randomize if we haven't done it yet, or if it's the first activation
            if (_availableMeshes != null && _availableMeshes.Length > 0 && 
                !_isRandomizing && (!_hasBeenRandomized || randomizeOnFirstActivation))
            {
                StartCoroutine(RandomizeMeshDelayed());
            }
        }
        
        private IEnumerator RandomizeMeshDelayed()
        {
            _isRandomizing = true;
            
            // Spread out randomization over time to avoid frame spikes
            var delay = Random.Range(0f, maxRandomizationDelay);
            yield return new WaitForSeconds(delay);
            
            // Double-check we're still active and valid
            if (this == null || !gameObject.activeInHierarchy || _softBody == null)
            {
                _isRandomizing = false;
                yield break;
            }
            
            // Pick a random mesh
            var randomMesh = _availableMeshes[Random.Range(0, _availableMeshes.Length)];
            
            // Only regenerate if the mesh is actually different
            if (_softBody.settings.inputMesh != randomMesh)
            {
                _softBody.settings.inputMesh = randomMesh;
                
                try
                {
                    _softBody.RegenerateWithRandomMesh();
                    Debug.Log($"Regenerated {gameObject.name} with mesh: {randomMesh.name}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to regenerate mesh for {gameObject.name}: {e.Message}");
                }
            }
            
            _hasBeenRandomized = true;
            _isRandomizing = false;
        }
        
        // Call this if you want to force a new random mesh (e.g., on recycling)
        public void ForceRandomize()
        {
            if (_isRandomizing) return;
            
            _hasBeenRandomized = false;
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(RandomizeMeshDelayed());
            }
        }
        
        private void OnDisable()
        {
            if (_isRandomizing)
            {
                StopAllCoroutines();
                _isRandomizing = false;
            }
        }
    }
}
using UnityEngine;

namespace SoftBody.Scripts.Pooling
{
    [RequireComponent(typeof(SoftBodyPhysics))]
    public class SoftBodyPoolable : MonoBehaviour
    {
        private PreGeneratedToyPool _pool;
        private SoftBodyPhysics _softBody;
        private float _activeTime = 0f;
        private bool _hasBeenReturned = false; // Prevent double returns
    
        [Header("Auto Return Settings")]
        public float autoReturnTime = 30f;
        public float fallThreshold = -20f;
    
        private void Awake()
        {
            _softBody = GetComponent<SoftBodyPhysics>();
        }
    
        public void Initialize(PreGeneratedToyPool pool)
        {
            _pool = pool;
        }
    
        public PreGeneratedToyPool GetPool()
        {
            return _pool;
        }
    
        public void OnGetFromPool()
        {
            _activeTime = 0f;
            _hasBeenReturned = false; // Reset flag
        
            if (_softBody != null)
            {
                _softBody.WakeUp();
                _softBody.settings.SkipUpdate = false;
            }
        }
    
        public void OnReturnToPool()
        {
            _hasBeenReturned = true; // Mark as returned
        
            if (_softBody != null)
            {
                _softBody.settings.SkipUpdate = true;
            }
        }
    
        private void Update()
        {
            if (!gameObject.activeInHierarchy || _hasBeenReturned) return;
        
            _activeTime += Time.deltaTime;
        
            // Auto return conditions - but let ToyManager handle fall detection
            if (_activeTime > autoReturnTime)
            {
                ReturnToPool();
            }
        
            // Remove fall detection from here since ToyManager handles it
            // if (transform.position.y < fallThreshold)
            // {
            //     ReturnToPool();
            // }
        }
    
        public void ReturnToPool()
        {
            if (_hasBeenReturned || _pool == null) return;
        
            _hasBeenReturned = true;
            _pool.ReturnToy(gameObject);
        }
    }
}
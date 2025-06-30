using UnityEngine;

namespace SoftBody.Scripts.Pooling
{
    public class DefaultPoolable : MonoBehaviour, IPoolable
    {
        [Header("Auto Return Settings")]
        public bool enableAutoReturn = true;
        public float autoReturnTime = 30f;
        public bool returnOnFallBelow = true;
        public float fallThreshold = -20f;
    
        protected GenericObjectPool _pool;
        protected float _activeTime = 0f;
    
        public virtual void Initialize(GenericObjectPool pool)
        {
            _pool = pool;
        }
    
        public virtual void OnGetFromPool()
        {
            _activeTime = 0f;
        }
    
        public virtual void OnReturnToPool()
        {
            // Override in derived classes for custom cleanup
        }
    
        public virtual void ReturnToPool()
        {
            if (_pool != null)
            {
                _pool.ReturnObject(gameObject);
            }
        }
    
        protected virtual void Update()
        {
            if (!gameObject.activeInHierarchy) return;
        
            _activeTime += Time.deltaTime;
        
            // Auto return conditions
            if (enableAutoReturn && _activeTime > autoReturnTime)
            {
                ReturnToPool();
                return;
            }
        
            if (returnOnFallBelow && transform.position.y < fallThreshold)
            {
                ReturnToPool();
                return;
            }
        }
    }
}
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
    
        protected GenericObjectPool Pool;
        protected float ActiveTime;
    
        public virtual void Initialize(GenericObjectPool pool)
        {
            Pool = pool;
        }
    
        public virtual void OnGetFromPool()
        {
            ActiveTime = 0f;
        }
    
        public virtual void OnReturnToPool()
        {
            // Override in derived classes for custom cleanup
        }
    
        public virtual void ReturnToPool()
        {
            if (Pool != null)
            {
                Pool.ReturnObject(gameObject);
            }
        }
    
        protected virtual void Update()
        {
            if (!gameObject.activeInHierarchy) return;
        
            ActiveTime += Time.deltaTime;
        
            // Auto return conditions
            if (enableAutoReturn && ActiveTime > autoReturnTime)
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
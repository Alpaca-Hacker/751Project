// Create new file: SoftBodyCollisionDebugger.cs
using UnityEngine;
using SoftBody.Scripts.Core;

namespace SoftBody.Scripts
{
    public class SoftBodyCollisionDebugger : MonoBehaviour
    {
        [Header("Debug Settings")]
        public bool showInteractionRadius = true;
        public bool showDetectedBodies = true;
        public bool logCollisionInfo = false;
        
        private SoftBodyPhysics _softBody;
        private CollisionSystem _collisionSystem;
        
        private void Start()
        {
            _softBody = GetComponent<SoftBodyPhysics>();
        }
        
        private void OnDrawGizmos()
        {
            if (_softBody == null) return;
    
            if (showInteractionRadius)
            {
                // Environment collision range (larger)
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(transform.position, _softBody.settings.maxEnvironmentCollisionDistance);
        
                // Soft body interaction range (smaller)
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(transform.position, _softBody.settings.maxSoftBodyInteractionDistance);
        
                // Inner interaction strength area
                Gizmos.color = Color.yellow;
                var innerRadius = _softBody.settings.maxSoftBodyInteractionDistance * _softBody.settings.interactionStrength;
                Gizmos.DrawWireSphere(transform.position, innerRadius);
            }
    
            if (showDetectedBodies)
            {
                var nearbyBodies = SoftBodyCacheManager.GetSoftBodiesNear(
                    transform.position, _softBody.settings.maxSoftBodyInteractionDistance);
        
                Gizmos.color = Color.red;
                foreach (var body in nearbyBodies)
                {
                    if (body != null && body.transform != transform)
                    {
                        Gizmos.DrawLine(transform.position, body.transform.position);
                        Gizmos.DrawWireSphere(body.transform.position, 0.2f);
                    }
                }
            }
        }
        
        private void Update()
        {
            if (logCollisionInfo && Time.frameCount % 120 == 0) // Every 2 seconds
            {
                var nearbyBodies = SoftBodyCacheManager.GetSoftBodiesNear(
                    transform.position, _softBody.settings.maxSoftBodyInteractionDistance);
                
                Debug.Log($"[{gameObject.name}] Collision Debug:");
                Debug.Log($"  EnableSoftBodyCollisions: {_softBody.settings.enableSoftBodyCollisions}");
                Debug.Log($"  InteractionStrength: {_softBody.settings.interactionStrength}");
                Debug.Log($"  MaxDistance: {_softBody.settings.maxSoftBodyInteractionDistance}");
                Debug.Log($"  Nearby bodies: {nearbyBodies.Count - 1}"); // -1 to exclude self
            }
        }
    }
}
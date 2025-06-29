using UnityEngine;

namespace SoftBody.Scripts
{
    public class SoftBodyCleanupTracker : MonoBehaviour
    {
        private SoftBodySpawner _spawner;
    
        public void Initialize(SoftBodySpawner spawner)
        {
            _spawner = spawner;
        }
    
        private void OnDestroy()
        {
            if (_spawner != null)
            {
                _spawner.RemoveObject(gameObject);
            }
        }
    }
}
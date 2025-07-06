using UnityEngine;

namespace SoftBody.Scripts.Performance
{
    public class ManagedSoftBody : MonoBehaviour
    {
        private SoftBodyPerformanceManager _manager;
        private SoftBodyPhysics _softBody;
        
        private void Start()
        {
            _softBody = GetComponent<SoftBodyPhysics>();
            
            // Find or create manager
            _manager = FindFirstObjectByType<SoftBodyPerformanceManager>();
            if (_manager == null)
            {
                var managerObj = new GameObject("SoftBodyPerformanceManager");
                _manager = managerObj.AddComponent<SoftBodyPerformanceManager>();
            }
            
            _manager.RegisterSoftBody(_softBody);
        }
        
        private void OnDestroy()
        {
            if (_manager != null)
            {
                _manager.UnregisterSoftBody(_softBody);
            }
        }
    }
}
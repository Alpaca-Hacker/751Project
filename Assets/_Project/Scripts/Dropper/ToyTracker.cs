using SoftBody.Scripts.Pooling;
using UnityEngine;

namespace SoftBody.Scripts.Dropper
{
    public class ToyTracker : MonoBehaviour
    {
        private static ToyManager _manager;
        private bool _isRegistered;
        
        public void Initialize(BasicCoinPusherTest pusher, PreGeneratedToyPool softBodyPool)
        {
            // Find or create the manager
            if (_manager == null)
            {
                _manager = FindFirstObjectByType<ToyManager>();
                if (_manager == null)
                {
                    var managerObj = new GameObject("ToyManager");
                    _manager = managerObj.AddComponent<ToyManager>();
                }
            }
            
            _manager.RegisterToy(gameObject, pusher, softBodyPool);
            _isRegistered = true;
        }
        
        private void OnDisable()
        {
            if (_isRegistered && _manager != null)
            {
                _manager.UnregisterToy(gameObject);
                _isRegistered = false;
            }
        }
        
        private void OnDestroy()
        {
            if (_isRegistered && _manager != null)
            {
                _manager.UnregisterToy(gameObject);
                _isRegistered = false;
            }
        }
    }
}
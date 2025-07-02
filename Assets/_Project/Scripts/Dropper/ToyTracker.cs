using UnityEngine;

namespace SoftBody.Scripts.Dropper
{
    public class ToyTracker : MonoBehaviour
    {
        private BasicCoinPusherTest coinPusher;
        private SoftBodyPool pool;
        private bool isTracking = false;
        
        public void Initialize(BasicCoinPusherTest pusher, SoftBodyPool softBodyPool)
        {
            coinPusher = pusher;
            pool = softBodyPool;
            isTracking = true;
        }
        
        private void Update()
        {
            if (!isTracking) return;
            
            if (transform.position.y < -5f)
            {
                isTracking = false;
                
                if (coinPusher != null)
                {
                    coinPusher.OnToyFellOff();
                }
                
                if (pool != null)
                {
                    pool.ReturnObject(gameObject);
                }
            }
        }
        
        private void OnDisable()
        {
            // Clean up when returned to pool
            isTracking = false;
        }
    }
}
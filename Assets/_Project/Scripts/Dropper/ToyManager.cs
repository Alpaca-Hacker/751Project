using System.Collections.Generic;
using UnityEngine;
using System.Collections;

namespace SoftBody.Scripts.Dropper
{
    public class ToyManager : MonoBehaviour
    {
        [Header("Tracking Settings")]
        public float fallThreshold = -5f;
        public float checkInterval = 0.2f; // Reduced frequency
        public int maxToysPerFrame = 3; // Reduced batch size
        
        public int TrackedToyCount => _trackedToys.Count;
        
        private readonly List<TrackedToy> _trackedToys = new();
        private readonly List<int> _indicesToRemove = new(); // Reuse list
        private Coroutine _trackingCoroutine;
        
        private struct TrackedToy
        {
            public GameObject gameObject;
            public Transform transform;
            public BasicCoinPusherTest coinPusher;
            public SoftBodyPool pool;
            
            public TrackedToy(GameObject obj, BasicCoinPusherTest pusher, SoftBodyPool softBodyPool)
            {
                gameObject = obj;
                transform = obj.transform;
                coinPusher = pusher;
                pool = softBodyPool;
            }
        }
        
        private void Start()
        {
            _trackingCoroutine = StartCoroutine(TrackToys());
        }
        
        public void RegisterToy(GameObject toy, BasicCoinPusherTest coinPusher, SoftBodyPool pool)
        {
            if (toy == null) return;
            _trackedToys.Add(new TrackedToy(toy, coinPusher, pool));
        }
        
        public void UnregisterToy(GameObject toy)
        {
            for (var i = _trackedToys.Count - 1; i >= 0; i--)
            {
                if (_trackedToys[i].gameObject == toy)
                {
                    _trackedToys.RemoveAt(i);
                    return;
                }
            }
        }
        
        private IEnumerator TrackToys()
        {
            var currentIndex = 0;
            
            while (true)
            {
                if (_trackedToys.Count == 0)
                {
                    yield return new WaitForSeconds(checkInterval * 2f); // Wait longer when no toys
                    continue;
                }
                
                // Check toys in smaller batches
                var toysChecked = 0;
                _indicesToRemove.Clear();
                
                for (var i = 0; i < maxToysPerFrame && currentIndex < _trackedToys.Count; i++)
                {
                    var toy = _trackedToys[currentIndex];
                    
                    // Quick null check first
                    if (toy.gameObject == null || !toy.gameObject.activeInHierarchy)
                    {
                        _indicesToRemove.Add(currentIndex);
                    }
                    else
                    {
                        // Only check position if object is valid
                        var pos = toy.transform.position;
                        if (pos.y < fallThreshold)
                        {
                            toy.coinPusher?.OnToyFellOff();
                            toy.pool?.ReturnObject(toy.gameObject);
                            _indicesToRemove.Add(currentIndex);
                        }
                    }
                    
                    currentIndex++;
                    toysChecked++;
                }
                
                // Remove from back to front to maintain indices
                for (var i = _indicesToRemove.Count - 1; i >= 0; i--)
                {
                    var indexToRemove = _indicesToRemove[i];
                    _trackedToys.RemoveAt(indexToRemove);
                    if (currentIndex > indexToRemove) currentIndex--;
                }
                
                // Reset index if we've gone through all toys
                if (currentIndex >= _trackedToys.Count)
                    currentIndex = 0;
                
                yield return new WaitForSeconds(checkInterval);
            }
        }
        
        
        private void OnDestroy()
        {
            if (_trackingCoroutine != null)
                StopCoroutine(_trackingCoroutine);
        }
    }
}
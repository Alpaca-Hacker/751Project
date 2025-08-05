using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using SoftBody.Scripts.Pooling;

namespace SoftBody.Scripts.Dropper
{
    public class ToyManager : MonoBehaviour
    {
        [Header("Tracking Settings")]
        public float fallThreshold = -5f;
        public float checkInterval = 0.1f;
        public int maxToysPerFrame = 5;
        
        public int TrackedToyCount => _trackedToys.Count;
        
        private readonly List<TrackedToy> _trackedToys = new();
        private readonly List<TrackedToy> _pendingAdditions = new(); 
        private readonly List<GameObject> _pendingRemovals = new(); 
        private readonly object _lockObject = new object(); // Thread safety
        
        private Coroutine _trackingCoroutine;

        private struct TrackedToy
        {
            public readonly GameObject GameObject;
            public readonly Transform Transform;
            public readonly BasicCoinPusherTest CoinPusher;
            public readonly PreGeneratedToyPool Pool;

            public TrackedToy(GameObject obj, BasicCoinPusherTest pusher, PreGeneratedToyPool softBodyPool)
            {
                GameObject = obj;
                Transform = obj.transform;
                CoinPusher = pusher;
                Pool = softBodyPool;
            }
        }

        private void Start()
        {
            _trackingCoroutine = StartCoroutine(TrackToys());
        }
        
        public void RegisterToy(GameObject toy, BasicCoinPusherTest coinPusher, object unusedPool)
        {
            if (toy == null) return;
    
            // Get the pool from the toy's component instead
            var poolable = toy.GetComponent<SoftBodyPoolable>();
            var pool = poolable?.GetPool(); // You'll need to add this method
    
            lock (_lockObject)
            {
                _pendingAdditions.Add(new TrackedToy(toy, coinPusher, pool));
            }
        }
        
        public void UnregisterToy(GameObject toy)
        {
            if (toy == null) return;
            
            // Thread-safe removal
            lock (_lockObject)
            {
                _pendingRemovals.Add(toy);
            }
        }
        
        private IEnumerator TrackToys()
        {
            while (true)
            {
                // Process pending operations first (thread-safe)
                ProcessPendingOperations();
                
                if (_trackedToys.Count == 0)
                {
                    yield return new WaitForSeconds(checkInterval);
                    continue;
                }
                
                // Create a snapshot to iterate safely
                var toysSnapshot = new TrackedToy[_trackedToys.Count];
                _trackedToys.CopyTo(toysSnapshot);
                
                var toysToRemove = new List<TrackedToy>();
                var checkedThisFrame = 0;
                
                // Iterate over the snapshot, not the live list
                for (var i = 0; i < toysSnapshot.Length && checkedThisFrame < maxToysPerFrame; i++)
                {
                    var toy = toysSnapshot[i];
                    checkedThisFrame++;
                    
                    // Check if toy is still valid
                    if (toy.GameObject == null || !toy.GameObject.activeInHierarchy)
                    {
                        toysToRemove.Add(toy);
                        continue;
                    }
                    
                    if (toy.Transform.position.y < fallThreshold)
                    {
                        // Toy fell off - handle cleanup
                        toy.CoinPusher?.OnToyFellOff();
                        toy.Pool?.ReturnToy(toy.GameObject);
                        toysToRemove.Add(toy);
                        continue;
                    }
                }
                
                // Remove invalid toys from the main list
                foreach (var toyToRemove in toysToRemove)
                {
                    RemoveToyFromList(toyToRemove);
                }
                
                yield return new WaitForSeconds(checkInterval);
            }
        }
        
        private void ProcessPendingOperations()
        {
            lock (_lockObject)
            {
                // Add pending toys
                foreach (var pendingToy in _pendingAdditions)
                {
                    _trackedToys.Add(pendingToy);
                }
                _pendingAdditions.Clear();
                
                // Remove pending toys
                foreach (var toyToRemove in _pendingRemovals)
                {
                    for (var i = _trackedToys.Count - 1; i >= 0; i--)
                    {
                        if (_trackedToys[i].GameObject == toyToRemove)
                        {
                            _trackedToys.RemoveAt(i);
                            break;
                        }
                    }
                }
                _pendingRemovals.Clear();
            }
        }
        
        private void RemoveToyFromList(TrackedToy toyToRemove)
        {
            for (var i = _trackedToys.Count - 1; i >= 0; i--)
            {
                if (_trackedToys[i].GameObject == toyToRemove.GameObject)
                {
                    _trackedToys.RemoveAt(i);
                    break;
                }
            }
        }
        
        public void RegisterToy(GameObject toy, BasicCoinPusherTest coinPusher, PreGeneratedToyPool toyPool)
        {
            if (toy == null) return;
    
            // Get the pool from the toy's poolable component
            var poolable = toy.GetComponent<SoftBodyPoolable>();
            var actualPool = poolable?.GetPool() ?? toyPool;
    
            lock (_lockObject)
            {
                _pendingAdditions.Add(new TrackedToy(toy, coinPusher, actualPool));
            }
        }
        
        private void OnDestroy()
        {
            if (_trackingCoroutine != null)
                StopCoroutine(_trackingCoroutine);
        }
    }
}
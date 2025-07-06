using UnityEngine;
using System.Collections.Generic;

namespace SoftBody.Scripts.Pooling
{
    public class PreGeneratedToyPool : MonoBehaviour
    {
        [Header("Pre-Generated Toys")]
        public List<GameObject> availableToys = new List<GameObject>();
        
        private Queue<GameObject> _availablePool = new Queue<GameObject>();
        private HashSet<GameObject> _activePool = new HashSet<GameObject>();
        
        public int AvailableCount => _availablePool.Count;
        public int ActiveCount => _activePool.Count;
        public int TotalCount => availableToys.Count;
        
        private void Start()
        {
            InitializePool();
        }
        
        private void InitializePool()
        {
            // Add all pre-generated toys to available pool
            foreach (var toy in availableToys)
            {
                if (toy != null)
                {
                    toy.SetActive(false);
                    _availablePool.Enqueue(toy);
                    
                    // Add poolable component if missing
                    var poolable = toy.GetComponent<SoftBodyPoolablePreGenerated>();
                    if (poolable == null)
                    {
                        poolable = toy.AddComponent<SoftBodyPoolablePreGenerated>();
                    }
                    poolable.Initialize(this);
                }
            }
            
            Debug.Log($"Initialized pool with {_availablePool.Count} pre-generated toys");
        }
        
        public GameObject GetRandomToy()
        {
            if (_availablePool.Count == 0)
            {
                Debug.LogWarning("No toys available in pool!");
                return null;
            }
            
            var toy = _availablePool.Dequeue();
            _activePool.Add(toy);
            
            // Simple activation
            toy.SetActive(true);
            
            var poolable = toy.GetComponent<SoftBodyPoolable>();
            poolable?.OnGetFromPool();
            
            return toy;
        }
        
        public void ReturnToy(GameObject toy)
        {
            if (!_activePool.Contains(toy))
            {
                Debug.LogWarning($"Toy {toy.name} not in active pool");
                return;
            }
            
            _activePool.Remove(toy);
            _availablePool.Enqueue(toy);
            
            var poolable = toy.GetComponent<SoftBodyPoolable>();
            poolable?.OnReturnToPool();
            
            toy.SetActive(false);
        }
        
        public void ReturnAllToys()
        {
            var activeToys = new List<GameObject>(_activePool);
            foreach (var toy in activeToys)
            {
                ReturnToy(toy);
            }
        }
    }
   
}
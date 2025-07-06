using System.Collections;
using System.Collections.Generic;
using SoftBody.Scripts.Pooling;
using UnityEngine;

namespace SoftBody.Scripts
{
    public class SoftBodyPool : MonoBehaviour
    {
        [Header("Prefab Configuration")] public GameObject softBodyPrefab;

        [Header("Pool Settings")] public int initialSize = 10;
        public int maxSize = 50;
        public bool allowGrowth = true;

        [Header("Auto Return Settings")] 
        public bool enableAutoReturn = true;
        public float autoReturnTime = 30f;
        public float fallThreshold = -20f;

        [Header("Reset Behavior")] public bool resetPhysicsOnGet = true;
        public bool resetTransformOnGet = true;
        public bool wakeUpOnGet = true;

        [Header("Events")] public UnityEngine.Events.UnityEvent<GameObject> OnObjectSpawned;
        public UnityEngine.Events.UnityEvent<GameObject> OnObjectReturned;
        public UnityEngine.Events.UnityEvent OnPoolExhausted;

        private Queue<GameObject> _availableObjects = new Queue<GameObject>();
        private HashSet<GameObject> _activeObjects = new HashSet<GameObject>();
        private List<GameObject> _allPooledObjects = new List<GameObject>();
        private bool _isInitialized = false;

        // Pool statistics
        public int AvailableCount => _availableObjects.Count;
        public int ActiveCount => _activeObjects.Count;
        public int TotalCount => _allPooledObjects.Count;

        private void Start()
        {
            if (softBodyPrefab == null)
            {
                Debug.LogError($"SoftBodyPool '{gameObject.name}': No prefab assigned!");
                return;
            }

            // Validate prefab has SoftBodyPhysics
            if (softBodyPrefab.GetComponent<SoftBodyPhysics>() == null)
            {
                Debug.LogError($"SoftBodyPool '{gameObject.name}': Prefab must have SoftBodyPhysics component!");
                return;
            }

            InitializePool();
        }

        private void InitializePool()
        {
            if (_isInitialized)
            {
                Debug.LogWarning($"SoftBodyPool '{gameObject.name}' is already initialized!");
                return;
            }
            
            for (var i = 0; i < initialSize; i++)
            {
                CreatePooledObject();
            }

            Debug.Log($"SoftBodyPool '{gameObject.name}' initialized with {initialSize} objects");
            _isInitialized = true;
        }
        
        private GameObject CreatePooledObject()
        {
            var obj = Instantiate(softBodyPrefab, transform);
            obj.name = $"{softBodyPrefab.name}_Pooled_{_allPooledObjects.Count}";
    
            // Ensure object starts INACTIVE
            obj.SetActive(false);
    
            var softBody = obj.GetComponent<SoftBodyPhysics>();
            if (softBody == null)
            {
                Debug.LogError($"Pooled object {obj.name} doesn't have SoftBodyPhysics component!");
                Destroy(obj);
                return null;
            }
    
            // Add poolable component while inactive
            var poolable = obj.GetComponent<SoftBodyPoolable>();
            if (poolable == null)
            {
                poolable = obj.AddComponent<SoftBodyPoolable>();
            }
    
            poolable.Initialize(this);
            poolable.enableAutoReturn = enableAutoReturn;
            poolable.autoReturnTime = autoReturnTime;
            poolable.fallThreshold = fallThreshold;
            poolable.resetPhysicsStateOnGet = resetPhysicsOnGet;
            poolable.wakeUpOnGet = wakeUpOnGet;
    
            // Add to available queue (object should be inactive)
            _availableObjects.Enqueue(obj);
            _allPooledObjects.Add(obj);
    
            Debug.Log($"Created pooled object {obj.name} - Active: {obj.activeInHierarchy}");
    
            return obj;
        }

        public GameObject GetObject()
        {
            if (_availableObjects.Count == 0)
            {
                if (allowGrowth && _allPooledObjects.Count < maxSize)
                {
                    CreatePooledObject();
                    Debug.Log($"Pool '{gameObject.name}' grew to {_allPooledObjects.Count} objects");
                }
                else
                {
                    Debug.LogWarning($"Pool '{gameObject.name}' exhausted! Active: {ActiveCount}, Total: {TotalCount}");
                    OnPoolExhausted?.Invoke();
                    return null;
                }
            }

            var obj = _availableObjects.Dequeue();
            _activeObjects.Add(obj);

            // Reset and activate
            ResetObject(obj);
            obj.SetActive(true);

            // Notify poolable
            var poolable = obj.GetComponent<SoftBodyPoolable>();
            poolable?.OnGetFromPool();

            OnObjectSpawned?.Invoke(obj);
    
            Debug.Log($"Got object {obj.name} from pool - Available: {_availableObjects.Count}, Active: {_activeObjects.Count}");
    
            return obj;
        }

        public void ReturnObject(GameObject obj)
        {
            if (obj == null) return;
    
            if (!_allPooledObjects.Contains(obj))
            {
                Debug.LogWarning($"Object '{obj.name}' doesn't belong to pool '{gameObject.name}' - ignoring return");
                return;
            }
    
            if (!_activeObjects.Contains(obj))
            {
                Debug.LogWarning($"Object '{obj.name}' is not currently active in pool '{gameObject.name}' - ignoring return");
                return;
            }

            _activeObjects.Remove(obj);
            _availableObjects.Enqueue(obj);

            var poolable = obj.GetComponent<SoftBodyPoolable>();
            poolable?.OnReturnToPool();

            obj.SetActive(false);
            OnObjectReturned?.Invoke(obj);
        }

        private void ResetObject(GameObject obj)
        {
            // Reset transform
            if (resetTransformOnGet)
            {
                obj.transform.localPosition = Vector3.zero;
                obj.transform.localRotation = Quaternion.identity;
                obj.transform.localScale = Vector3.one;
            }

            // Reset physics
            if (resetPhysicsOnGet)
            {
                var rb = obj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }

            // Reset soft body physics
            var softBody = obj.GetComponent<SoftBodyPhysics>();
            if (softBody != null)
            {
                if (resetPhysicsOnGet)
                {
                    softBody.ResetToInitialState();
                }

                if (wakeUpOnGet)
                {
                    softBody.WakeUp();
                }
            }
        }

        public void ReturnAllActiveObjects()
        {
            var activeList = new List<GameObject>(_activeObjects);
            foreach (var obj in activeList)
            {
                ReturnObject(obj);
            }

            Debug.Log($"Returned {activeList.Count} active objects to pool '{gameObject.name}'");
        }

        public void PrewarmPool(int count)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning($"Cannot prewarm pool '{gameObject.name}' - not initialized yet");
                return;
            }
    
            // Don't prewarm beyond maxSize
            var availableSlots = maxSize - _allPooledObjects.Count;
            var actualCount = Mathf.Min(count, availableSlots);
    
            Debug.Log($"PrewarmPool called with {count}, but only creating {actualCount} (available slots: {availableSlots})");

            for (var i = 0; i < actualCount; i++)
            {
                CreatePooledObject();
            }

            Debug.Log($"Prewarmed pool '{gameObject.name}' with {actualCount} objects. Total: {_allPooledObjects.Count}");
        }
        
        [ContextMenu("Debug Pool State")]
        public void DebugPoolState()
        {
            Debug.Log($"=== Pool '{gameObject.name}' State ===");
            Debug.Log($"Total Objects: {_allPooledObjects.Count}");
            Debug.Log($"Available: {_availableObjects.Count}");
            Debug.Log($"Active: {_activeObjects.Count}");
    
            Debug.Log("=== All Pooled Objects ===");
            for (var i = 0; i < _allPooledObjects.Count; i++)
            {
                var obj = _allPooledObjects[i];
                var isInActive = _activeObjects.Contains(obj);
                var isInAvailable = _availableObjects.Contains(obj);
                Debug.Log($"  {i}: {obj.name} - GameObject.active: {obj.activeInHierarchy} - InActiveSet: {isInActive} - InAvailableQueue: {isInAvailable}");
            }
        }
    }
}
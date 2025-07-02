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
            for (var i = 0; i < initialSize; i++)
            {
                CreatePooledObject();
            }

            Debug.Log($"SoftBodyPool '{gameObject.name}' initialized with {initialSize} objects");
        }

        // In SoftBodyPool.cs - CreatePooledObject method
        private GameObject CreatePooledObject()
        {
            var obj = Instantiate(softBodyPrefab, transform);
            obj.name = $"{softBodyPrefab.name}_Pooled_{_allPooledObjects.Count}";
            
            var softBody = obj.GetComponent<SoftBodyPhysics>();
            if (softBody == null)
            {
                Debug.LogError($"Pooled object {obj.name} doesn't have SoftBodyPhysics component!");
                Destroy(obj);
                return null;
            }
            
            obj.SetActive(true);
            
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
            
            StartCoroutine(DeactivateAfterInit(obj, softBody));

            _availableObjects.Enqueue(obj);
            _allPooledObjects.Add(obj);

            return obj;
        }

        private IEnumerator DeactivateAfterInit(GameObject obj, SoftBodyPhysics softBody)
        {
            var waitFrames = 0;
            while (softBody.ParticleCount == 0 && waitFrames < 10)
            {
                yield return null;
                waitFrames++;
            }
    
            if (softBody.ParticleCount == 0)
            {
                Debug.LogWarning($"Soft body {obj.name} failed to initialize particles after {waitFrames} frames");
            }
            else
            {
                Debug.Log($"Soft body {obj.name} initialized with {softBody.ParticleCount} particles");
            }
    
            obj.SetActive(false);
        }

        public GameObject GetObject()
        {
            // Try to grow pool if needed
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

            // Reset the object
            ResetObject(obj);

           // obj.SetActive(true);

            // Notify the poolable component
            // var poolable = obj.GetComponent<SoftBodyPoolable>();
            // poolable?.OnGetFromPool();

            OnObjectSpawned?.Invoke(obj);

            return obj;
        }

        public void ReturnObject(GameObject obj)
        {
            if (!_allPooledObjects.Contains(obj) || !_activeObjects.Contains(obj))
            {
                Debug.LogWarning(
                    $"Trying to return object '{obj.name}' that doesn't belong to pool '{gameObject.name}'");
                return;
            }

            _activeObjects.Remove(obj);
            _availableObjects.Enqueue(obj);

            // Notify the poolable component
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
            count = Mathf.Min(count, maxSize - _allPooledObjects.Count);

            for (var i = 0; i < count; i++)
            {
                CreatePooledObject();
            }

            Debug.Log($"Prewarmed pool '{gameObject.name}' with {count} additional objects");
        }
    }
}
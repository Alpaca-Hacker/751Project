using System.Collections;
using System.Collections.Generic;
using SoftBody.Scripts.Pooling;
using UnityEngine;

namespace SoftBody.Scripts.Spawning
{
    public class GenericSpawner : MonoBehaviour
    {
        [Header("Pool Reference")] 
        [SerializeField] private GenericObjectPool objectPool;
        [SerializeField] private string poolName; 

        [Header("Spawn Configuration")] 
        public SpawnSettings spawnSettings = new SpawnSettings();

        [Header("Events")] 
        public UnityEngine.Events.UnityEvent<GameObject> OnObjectSpawned;
        public UnityEngine.Events.UnityEvent OnSpawnLimitReached;

        private Coroutine _autoSpawnCoroutine;
        private int _totalSpawned = 0;
        private List<GameObject> _activeSpawnedObjects = new List<GameObject>();

        public int ActiveObjectCount => _activeSpawnedObjects.Count;
        public int TotalSpawned => _totalSpawned;
        
        public GenericObjectPool ObjectPool 
        { 
            get => objectPool;
            set => objectPool = value;
        }

        private void Start()
        {
            // Try multiple ways to find the pool
            if (objectPool == null && !string.IsNullOrEmpty(poolName))
            {
                var poolObj = GameObject.Find(poolName);
                if (poolObj != null)
                {
                    objectPool = poolObj.GetComponent<GenericObjectPool>();
                    Debug.Log($"Found pool by name: {poolName}");
                }
            }
    
            if (objectPool == null)
            {
                objectPool = GetComponent<GenericObjectPool>();
                Debug.Log($"Found pool component on same GameObject: {objectPool != null}");
            }
    
            if (objectPool == null)
            {
                objectPool = FindFirstObjectByType<GenericObjectPool>();
                Debug.Log($"Found pool in scene: {objectPool != null}");
            }
    
            if (objectPool == null)
            {
                Debug.LogError($"GenericSpawner '{gameObject.name}' could not find any GenericObjectPool!");
                return;
            }
    
            Debug.Log($"Using pool: {objectPool.gameObject.name}");

            Debug.Log($"Pool setup complete. Auto spawn enabled: {spawnSettings.autoSpawn}");

            // Subscribe to pool events for tracking
            objectPool.OnObjectReturned.AddListener(OnObjectReturnedToPool);

            if (spawnSettings.autoSpawn)
            {
                Debug.Log($"Starting auto spawn with interval: {spawnSettings.spawnInterval}s");
                StartAutoSpawn();
            }
        }

        public void StartAutoSpawn()
        {
            if (_autoSpawnCoroutine != null)
            {
                StopCoroutine(_autoSpawnCoroutine);
            }

            _autoSpawnCoroutine = StartCoroutine(AutoSpawnCoroutine());
        }

        public void StopAutoSpawn()
        {
            if (_autoSpawnCoroutine != null)
            {
                StopCoroutine(_autoSpawnCoroutine);
                _autoSpawnCoroutine = null;
            }
        }

        private IEnumerator AutoSpawnCoroutine()
        {
            Debug.Log($"Auto spawn coroutine started. Delay: {spawnSettings.spawnDelay}s");
    
            if (spawnSettings.spawnDelay > 0)
            {
                yield return new WaitForSeconds(spawnSettings.spawnDelay);
            }
    
            Debug.Log("Auto spawn delay complete, starting spawn loop");
    
            while (spawnSettings.autoSpawn)
            {
                Debug.Log($"Spawn loop iteration. Can spawn: {CanSpawn()}, Active: {_activeSpawnedObjects.Count}, Max: {spawnSettings.maxActiveObjects}");
        
                if (CanSpawn())
                {
                    var spawned = SpawnObject();
                    Debug.Log($"Spawned object: {spawned != null}");
                }
                else
                {
                    Debug.Log("Cannot spawn - limit reached or other condition failed");
                }
        
                yield return new WaitForSeconds(spawnSettings.spawnInterval);
            }
    
            Debug.Log("Auto spawn coroutine ended");
        }

        public GameObject SpawnObject()
        {
            if (!CanSpawn())
            {
                return null;
            }

            var obj = objectPool.GetObject();
            if (obj == null)
            {
                return null;
            }

            ConfigureSpawnedObject(obj);

            _activeSpawnedObjects.Add(obj);
            _totalSpawned++;

            OnObjectSpawned?.Invoke(obj);

            if (spawnSettings.totalSpawnLimit > 0 && _totalSpawned >= spawnSettings.totalSpawnLimit)
            {
                OnSpawnLimitReached?.Invoke();
                StopAutoSpawn();
            }

            return obj;
        }

        private bool CanSpawn()
        {
            if (_activeSpawnedObjects.Count >= spawnSettings.maxActiveObjects)
                return false;

            if (spawnSettings.totalSpawnLimit > 0 && _totalSpawned >= spawnSettings.totalSpawnLimit)
                return false;

            return true;
        }

        private void ConfigureSpawnedObject(GameObject obj)
        {
            // Set position
            var spawnPosition = GetSpawnPosition();
            obj.transform.position = spawnPosition;

            // Set rotation
            if (spawnSettings.randomizeRotation)
            {
                obj.transform.rotation = Random.rotation;
            }

            StartCoroutine(ApplyPhysicsAfterSpawn(obj));

        }

        private IEnumerator ApplyPhysicsAfterSpawn(GameObject obj)
        {
            yield return new WaitForFixedUpdate(); // Wait for physics frame

            if (!obj || !obj.activeInHierarchy) yield break;

            var rb = obj.GetComponent<Rigidbody>();
            if (rb)
            {
                // Reset physics first
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                // Then apply initial velocity
                var velocity = new Vector3(
                    Random.Range(spawnSettings.initialVelocityMin.x, spawnSettings.initialVelocityMax.x),
                    Random.Range(spawnSettings.initialVelocityMin.y, spawnSettings.initialVelocityMax.y),
                    Random.Range(spawnSettings.initialVelocityMin.z, spawnSettings.initialVelocityMax.z)
                );

                rb.linearVelocity = velocity;

                if (spawnSettings.addRandomTorque)
                {
                    rb.angularVelocity = Random.insideUnitSphere * spawnSettings.maxTorque;
                }
            }
        }

        private Vector3 GetSpawnPosition()
        {
            var basePosition = transform.position;

            // Use spawn points if available
            if (spawnSettings.spawnPoints != null && spawnSettings.spawnPoints.Length > 0)
            {
                if (spawnSettings.randomizeSpawnPoint)
                {
                    var spawnPoint = spawnSettings.spawnPoints[Random.Range(0, spawnSettings.spawnPoints.Length)];
                    basePosition = spawnPoint.position;
                }
                else
                {
                    basePosition = spawnSettings.spawnPoints[0].position;
                }
            }

            // Add area variation if enabled
            if (spawnSettings.useSpawnArea)
            {
                var areaOffset = new Vector3(
                    Random.Range(-spawnSettings.spawnAreaSize.x * 0.5f, spawnSettings.spawnAreaSize.x * 0.5f),
                    Random.Range(-spawnSettings.spawnAreaSize.y * 0.5f, spawnSettings.spawnAreaSize.y * 0.5f),
                    Random.Range(-spawnSettings.spawnAreaSize.z * 0.5f, spawnSettings.spawnAreaSize.z * 0.5f)
                );

                basePosition += areaOffset;
            }

            return basePosition;
        }

        private void OnObjectReturnedToPool(GameObject obj)
        {
            _activeSpawnedObjects.Remove(obj);
        }

        private void OnDrawGizmosSelected()
        {
            if (spawnSettings.useSpawnArea)
            {
                Gizmos.color = Color.green;
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(Vector3.zero, spawnSettings.spawnAreaSize);
            }

            if (spawnSettings.spawnPoints != null)
            {
                Gizmos.color = Color.yellow;
                foreach (var point in spawnSettings.spawnPoints)
                {
                    if (point != null)
                    {
                        Gizmos.DrawWireSphere(point.position, 0.2f);
                    }
                }
            }
        }
    }
}
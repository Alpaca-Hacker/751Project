using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoftBody.Scripts
{
    public class SoftBodySpawner_old : MonoBehaviour
    {
        [Header("Spawning Settings")] public GameObject softBodyPrefab;
        public int maxObjects = 50;
        public float spawnRate = 1f; // Objects per second
        public bool autoSpawn = true;
        public bool spawnOnStart = false;

        [Header("Spawn Area")] public Vector3 spawnAreaSize = new Vector3(10f, 5f, 10f);
        public Vector3 spawnAreaOffset = Vector3.zero;
        public bool showSpawnArea = true;

        [Header("Physics Variation")] public Vector2 massRange = new Vector2(0.5f, 2f);
        public Vector2 scaleRange = new Vector2(0.8f, 1.2f);
        public bool randomizeCompliance = true;
        public Vector2 complianceMultiplier = new Vector2(0.5f, 2f);

        [Header("Initial Velocity")] public bool addRandomVelocity = true;
        public Vector3 velocityRange = new Vector3(2f, 0f, 2f);

        [Header("Cleanup")] public bool enableCleanup = true;
        public float cleanupHeight = -20f;
        public float cleanupCheckInterval = 2f;

        // Internal state
        private List<GameObject> _spawnedObjects = new List<GameObject>();
        private float _lastSpawnTime;
        private Coroutine _spawnCoroutine;
        private Coroutine _cleanupCoroutine;

        // Events for monitoring
        public System.Action<int> OnObjectCountChanged;

        public int CurrentObjectCount => _spawnedObjects.Count;
        public int TotalObjectsSpawned { get; private set; }

        private void Start()
        {
            if (softBodyPrefab == null)
            {
                Debug.LogError("SoftBodySpawner: No prefab assigned!");
                return;
            }

            // Validate prefab has SoftBodyPhysics component
            if (softBodyPrefab.GetComponent<SoftBodyPhysics>() == null)
            {
                Debug.LogError("SoftBodySpawner: Prefab must have SoftBodyPhysics component!");
                return;
            }

            if (spawnOnStart)
            {
                SpawnObject();
            }

            if (autoSpawn)
            {
                StartAutoSpawn();
            }

            if (enableCleanup)
            {
                StartCleanup();
            }
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }

        public void StartAutoSpawn()
        {
            if (_spawnCoroutine != null)
            {
                StopCoroutine(_spawnCoroutine);
            }

            _spawnCoroutine = StartCoroutine(AutoSpawnCoroutine());
        }

        public void StopAutoSpawn()
        {
            if (_spawnCoroutine != null)
            {
                StopCoroutine(_spawnCoroutine);
                _spawnCoroutine = null;
            }
        }

        private IEnumerator AutoSpawnCoroutine()
        {
            while (autoSpawn)
            {
                if (_spawnedObjects.Count < maxObjects)
                {
                    SpawnObject();
                    yield return new WaitForSeconds(1f / spawnRate);
                }
                else
                {
                    yield return new WaitForSeconds(0.1f); // Check less frequently when at max
                }
            }
        }

        public GameObject SpawnObject()
        {
            if (_spawnedObjects.Count >= maxObjects)
            {
                Debug.LogWarning($"SoftBodySpawner: Maximum objects ({maxObjects}) reached!");
                return null;
            }

            // Calculate spawn position
            var spawnPos = transform.position + spawnAreaOffset + new Vector3(
                Random.Range(-spawnAreaSize.x * 0.5f, spawnAreaSize.x * 0.5f),
                Random.Range(0f, spawnAreaSize.y),
                Random.Range(-spawnAreaSize.z * 0.5f, spawnAreaSize.z * 0.5f)
            );

            // Instantiate object
            var newObject = Instantiate(softBodyPrefab, spawnPos, Random.rotation);

            // Apply variations
            ApplyVariations(newObject);

            // Track object
            _spawnedObjects.Add(newObject);
            TotalObjectsSpawned++;

            // Add cleanup component
            var cleanup = newObject.AddComponent<SoftBodyCleanupTracker>();
            //cleanup.Initialize(this);

            OnObjectCountChanged?.Invoke(_spawnedObjects.Count);

            Debug.Log($"Spawned soft body #{TotalObjectsSpawned}. Current count: {_spawnedObjects.Count}");

            return newObject;
        }

        private void ApplyVariations(GameObject obj)
        {
            // Scale variation
            if (scaleRange.x != scaleRange.y)
            {
                var scale = Random.Range(scaleRange.x, scaleRange.y);
                obj.transform.localScale = Vector3.one * scale;
            }

            // Get soft body component
            var softBody = obj.GetComponent<SoftBodyPhysics>();
            if (softBody == null) return;

            // Mass variation
            if (massRange.x != massRange.y)
            {
                var mass = Random.Range(massRange.x, massRange.y);
                softBody.settings.mass = mass;
            }

            // Compliance variation
            if (randomizeCompliance && complianceMultiplier.x != complianceMultiplier.y)
            {
                var multiplier = Random.Range(complianceMultiplier.x, complianceMultiplier.y);
                softBody.settings.structuralCompliance *= multiplier;
                softBody.settings.shearCompliance *= multiplier;
                softBody.settings.bendCompliance *= multiplier;
            }

            // Initial velocity
            if (addRandomVelocity)
            {
                var rb = obj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    var velocity = new Vector3(
                        Random.Range(-velocityRange.x, velocityRange.x),
                        Random.Range(-velocityRange.y, velocityRange.y),
                        Random.Range(-velocityRange.z, velocityRange.z)
                    );
                    rb.linearVelocity = velocity;
                }
            }
        }

        public void RemoveObject(GameObject obj)
        {
            if (_spawnedObjects.Contains(obj))
            {
                _spawnedObjects.Remove(obj);
                OnObjectCountChanged?.Invoke(_spawnedObjects.Count);
            }
        }

        public void ClearAllObjects()
        {
            foreach (var obj in _spawnedObjects)
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
            }

            _spawnedObjects.Clear();
            OnObjectCountChanged?.Invoke(0);
        }

        private void StartCleanup()
        {
            if (_cleanupCoroutine != null)
            {
                StopCoroutine(_cleanupCoroutine);
            }

            _cleanupCoroutine = StartCoroutine(CleanupCoroutine());
        }

        private IEnumerator CleanupCoroutine()
        {
            while (enableCleanup)
            {
                yield return new WaitForSeconds(cleanupCheckInterval);

                for (int i = _spawnedObjects.Count - 1; i >= 0; i--)
                {
                    var obj = _spawnedObjects[i];
                    if (obj == null || obj.transform.position.y < cleanupHeight)
                    {
                        if (obj != null)
                        {
                            Destroy(obj);
                        }

                        _spawnedObjects.RemoveAt(i);
                    }
                }

                OnObjectCountChanged?.Invoke(_spawnedObjects.Count);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!showSpawnArea) return;

            Gizmos.color = Color.green;
            Gizmos.matrix = transform.localToWorldMatrix;

            var center = spawnAreaOffset + Vector3.up * spawnAreaSize.y * 0.5f;
            Gizmos.DrawWireCube(center, spawnAreaSize);

            // Draw spawn points
            Gizmos.color = Color.yellow;
            for (int i = 0; i < 10; i++)
            {
                var pos = spawnAreaOffset + new Vector3(
                    Random.Range(-spawnAreaSize.x * 0.5f, spawnAreaSize.x * 0.5f),
                    Random.Range(0f, spawnAreaSize.y),
                    Random.Range(-spawnAreaSize.z * 0.5f, spawnAreaSize.z * 0.5f)
                );
                Gizmos.DrawWireSphere(pos, 0.1f);
            }
        }
    }
}
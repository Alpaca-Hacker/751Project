using System.Collections;
using UnityEngine;

namespace SoftBody.Scripts
{
    public class SoftBodySpawner : MonoBehaviour
    {
        [Header("Pool")] public SoftBodyPool pool;

        [Header("Spawning")] public bool autoSpawn = true;
        public float spawnInterval = 2f;
        public int maxActiveObjects = 20;

        [Header("Spawn Location")] public Vector3 spawnAreaSize = new Vector3(5f, 2f, 5f);
        public bool randomizeRotation = true;

        [Header("Initial Physics")] public Vector3 initialVelocityMin = Vector3.zero;
        public Vector3 initialVelocityMax = new Vector3(2f, 0f, 2f);

        [Header("Events")] public UnityEngine.Events.UnityEvent<GameObject> OnObjectSpawned;

        public int ActiveObjectCount => pool != null ? pool.ActiveCount : 0;
        public int TotalObjectCount => pool != null ? pool.TotalCount : 0;

        private void Start()
        {
            if (pool == null)
            {
                pool = GetComponent<SoftBodyPool>();
            }

            if (pool == null)
            {
                Debug.LogError("SoftBodySpawnerSimple needs a SoftBodyPool!");
                return;
            }

            if (autoSpawn)
            {
                StartCoroutine(SpawnLoop());
            }
        }

        private IEnumerator SpawnLoop()
        {
            while (autoSpawn)
            {
                if (pool.ActiveCount < maxActiveObjects)
                {
                    SpawnObject();
                }

                yield return new WaitForSeconds(spawnInterval);
            }
        }

        public GameObject SpawnObject()
        {
            if (pool == null || pool.ActiveCount >= maxActiveObjects)
                return null;

            var obj = pool.GetObject();
            if (obj == null) return null;

            // BETTER spawn position calculation
            var spawnPos = CalculateSafeSpawnPosition();
            obj.transform.position = spawnPos;

            if (randomizeRotation)
            {
                obj.transform.rotation = Random.rotation;
            }

            // Reset physics immediately to prevent issues
            var rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Apply initial velocity after ensuring clean state
            StartCoroutine(ApplyInitialVelocityDelayed(obj));

            OnObjectSpawned?.Invoke(obj);

            return obj;
        }

        private Vector3 CalculateSafeSpawnPosition()
        {
            var basePosition = transform.position;

            // Add random offset within spawn area
            var offset = new Vector3(
                Random.Range(-spawnAreaSize.x * 0.5f, spawnAreaSize.x * 0.5f),
                Random.Range(0f, spawnAreaSize.y), // Only spawn above the base position
                Random.Range(-spawnAreaSize.z * 0.5f, spawnAreaSize.z * 0.5f)
            );

            var spawnPos = basePosition + offset;

            // Ensure spawn position is above ground (simple raycast check)
            if (Physics.Raycast(spawnPos + Vector3.up * 2f, Vector3.down, out var hit, 10f))
            {
                spawnPos.y = hit.point.y + 1f; // 1 meter above ground
            }

            return spawnPos;
        }

        private IEnumerator ApplyInitialVelocityDelayed(GameObject obj)
        {
            // Wait longer to ensure object is properly positioned
            yield return new WaitForSeconds(0.1f);

            if (obj == null || !obj.activeInHierarchy) yield break;

            var rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                var velocity = new Vector3(
                    Random.Range(initialVelocityMin.x, initialVelocityMax.x),
                    Random.Range(initialVelocityMin.y, initialVelocityMax.y),
                    Random.Range(initialVelocityMin.z, initialVelocityMax.z)
                );
                rb.linearVelocity = velocity;
            }
        }

        private IEnumerator ApplyInitialVelocity(GameObject obj)
        {
            yield return new WaitForFixedUpdate();

            if (obj == null || !obj.activeInHierarchy) yield break;

            var rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                var velocity = new Vector3(
                    Random.Range(initialVelocityMin.x, initialVelocityMax.x),
                    Random.Range(initialVelocityMin.y, initialVelocityMax.y),
                    Random.Range(initialVelocityMin.z, initialVelocityMax.z)
                );
                rb.linearVelocity = velocity;
            }
        }

        public void StartSpawning()
        {
            autoSpawn = true;
            StartCoroutine(SpawnLoop());
        }

        public void StopSpawning()
        {
            autoSpawn = false;
        }

        public void ReturnAllObjects()
        {
            if (pool != null)
            {
                pool.ReturnAllActiveObjects();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, spawnAreaSize);
        }
    }

}
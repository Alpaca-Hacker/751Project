using System.Collections;
using SoftBody.Scripts.Pooling;
using UnityEngine;

namespace SoftBody.Scripts.Dropper
{
    public class BasicCoinPusherTest : MonoBehaviour
    {
        [Header("References")] 
        public SoftBodyPool softBodyPool;
        public GameObject pusher;
        public Transform dropZone;

        [Header("Pusher Settings")] public float pushDistance = 5f;
        public float pushSpeed = 0.5f;
        public float returnSpeed = 2f;

        [Header("Spawn Settings")] public float spawnInterval = 3f;
        public int maxToys = 30;
        
        private int _currentToyCount = 0;
        private ToyManager _toyManager;

        private void Start()
        {
            // Create or find toy manager
            _toyManager = FindFirstObjectByType<ToyManager>();
            if (_toyManager == null)
            {
                var managerObj = new GameObject("ToyManager");
                _toyManager = managerObj.AddComponent<ToyManager>();
            }
    
            // Pre-warm pool
            softBodyPool.PrewarmPool(maxToys);
            
            StartCoroutine(PusherCycle());
            StartCoroutine(SpawnCycle());
        }

        private IEnumerator PusherCycle()
        {
            var startPos = pusher.transform.position;
            var endPos = startPos - Vector3.forward * pushDistance;

            while (true)
            {
                // Push forward
                var pushTime = pushDistance / pushSpeed;
                float elapsed = 0;

                while (elapsed < pushTime)
                {
                    elapsed += Time.deltaTime;
                    var t = elapsed / pushTime;
                    pusher.transform.position = Vector3.Lerp(
                        startPos,
                        endPos,
                        t
                    );
                    yield return null;
                }
                
                yield return new WaitForSeconds(0.5f);

                // Return quickly
                var returnTime = pushDistance / returnSpeed;
                elapsed = 0;

                while (elapsed < returnTime)
                {
                    elapsed += Time.deltaTime;
                    var t = elapsed / returnTime;
                    pusher.transform.position = Vector3.Lerp(
                        endPos,
                        startPos,
                        t
                    );
                    yield return null;
                }

                yield return new WaitForSeconds(0.5f);
            }
        }

        private IEnumerator SpawnCycle()
        {
            while (true)
            {
                yield return new WaitForSeconds(spawnInterval);
        
                // Only spawn if under limit AND pool has available objects
                if (_currentToyCount < maxToys && softBodyPool.AvailableCount > 0)
                {
                    SpawnToy();
                }
                else if (_currentToyCount >= maxToys)
                {
                    Debug.Log($"Max toys reached ({maxToys}), waiting for cleanup");
                }
                else if (softBodyPool.AvailableCount <= 0)
                {
                    Debug.Log("Pool exhausted, waiting for returns");
                }
            }
        }

        private void SpawnToy()
        {
            if (softBodyPool == null)
            {
                Debug.LogError("softBodyPool is null!");
                return;
            }
    
            // Check dropZone exists
            if (dropZone == null)
            {
                Debug.LogError("dropZone is null!");
                return;
            }
            
            var toy = softBodyPool.GetObject();
            if (toy == null)
            {
                Debug.LogWarning("Pool returned null!");
                return;
            }
            
            Debug.Log("Spawning toy: " + toy.name);
            
            var targetPos = dropZone.position + new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(0f, 1f),
                Random.Range(-0.5f, 0.5f)
            );
            
            toy.transform.rotation = Random.rotation;
            
            toy.SetActive(true);
            
            var softBody = toy.GetComponent<SoftBodyPhysics>();
            if (softBody)
            {
                softBody.SetWorldPosition(targetPos);
            }
            else
            {
                toy.transform.position = targetPos;
            }
            
            var poolable = toy.GetComponent<SoftBodyPoolable>();
            poolable?.OnGetFromPool();
            
            _currentToyCount++;
            
            var tracker = toy.GetComponent<ToyTracker>();
            if (tracker == null)
            {
                tracker = toy.AddComponent<ToyTracker>();
            }
            tracker.Initialize(this, softBodyPool);
        }

// Add this method to be called by the tracker
        public void OnToyFellOff()
        {
            _currentToyCount--;
            _currentToyCount = Mathf.Max(0, _currentToyCount);
        }

        private IEnumerator TrackToy(GameObject toy)
        {
            while (toy.activeSelf)
            {
                if (toy.transform.position.y < -5f)
                {
                    // Toy fell off the edge
                    _currentToyCount--;
                    softBodyPool.ReturnObject(toy);
                    yield break;
                }

                yield return new WaitForSeconds(0.5f);
            }
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(10, 10, 200, 20), $"Active Toys: {_currentToyCount}/{maxToys}");
            GUI.Label(new Rect(10, 30, 200, 20), "Basic Coin Pusher Test");
        }
    }
}
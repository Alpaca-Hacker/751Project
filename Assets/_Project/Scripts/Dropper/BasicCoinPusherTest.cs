using System.Collections;
using SoftBody.Scripts.Pooling;
using UnityEngine;

namespace SoftBody.Scripts.Dropper
{
    public class BasicCoinPusherTest : MonoBehaviour
    {
        [Header("References")] 
        public PreGeneratedToyPool toyPool; 
        public GameObject pusher;
        public Transform dropZone;

        [Header("Pusher Settings")] 
        public float pushDistance = 5f;
        public float pushSpeed = 0.5f;
        public float returnSpeed = 2f;

        [Header("Spawn Settings")] 
        public float spawnInterval = 3f;
        public int maxToys = 8;
        
        private int _currentToyCount;
        private ToyManager _toyManager;

        private void Start()
        {
            // Create toy manager
            _toyManager = FindFirstObjectByType<ToyManager>();
            if (_toyManager == null)
            {
                var managerObj = new GameObject("ToyManager");
                _toyManager = managerObj.AddComponent<ToyManager>();
            }
            
            StartCoroutine(PusherCycle());
            StartCoroutine(SpawnCycle());
        }

        private IEnumerator SpawnCycle()
        {
            while (true)
            {
                yield return new WaitForSeconds(spawnInterval);
                
                if (_currentToyCount < maxToys && toyPool.AvailableCount > 0)
                {
                    SpawnToy();
                }
                else if (_currentToyCount >= maxToys)
                {
                    Debug.Log($"Max toys reached ({maxToys}), waiting for cleanup");
                }
                else if (toyPool.AvailableCount <= 0)
                {
                    Debug.Log("Pool exhausted, waiting for returns");
                }
            }
        }

        private void SpawnToy()
        {
            var toy = toyPool.GetRandomToy();
            if (toy == null)
            {
                Debug.LogWarning("Pool returned null!");
                return;
            }
    
            Debug.Log($"Spawning toy: {toy.name}");
    
            var targetPos = dropZone.position + new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(0f, 1f),
                Random.Range(-0.5f, 0.5f)
            );
    
            toy.transform.position = targetPos;
            toy.transform.rotation = Random.rotation;
    
            // Register with toy manager - pass the actual pool
            _toyManager.RegisterToy(toy, this, toyPool);
    
            _currentToyCount++;
        }
        // Called by ToyManager when toys fall off
        public void OnToyFellOff()
        {
            _currentToyCount--;
            _currentToyCount = Mathf.Max(0, _currentToyCount);
        }

        // Remove the old TrackToy coroutine - ToyManager handles this now

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
                    pusher.transform.position = Vector3.Lerp(startPos, endPos, t);
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
                    pusher.transform.position = Vector3.Lerp(endPos, startPos, t);
                    yield return null;
                }
                
                yield return new WaitForSeconds(1f);
            }
        }
    }
}
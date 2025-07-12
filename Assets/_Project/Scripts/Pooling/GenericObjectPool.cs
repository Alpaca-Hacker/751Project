using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace SoftBody.Scripts.Pooling
{
    public class GenericObjectPool : MonoBehaviour
{
    [Header("Pool Settings")]
    public PoolSettings poolSettings = new PoolSettings();
    
    [Header("Events")]
    public UnityEngine.Events.UnityEvent<GameObject> OnObjectSpawned;
    public UnityEngine.Events.UnityEvent<GameObject> OnObjectReturned;
    public UnityEngine.Events.UnityEvent OnPoolExhausted;
    
    private Queue<GameObject> _availableObjects = new Queue<GameObject>();
    private HashSet<GameObject> _activeObjects = new HashSet<GameObject>();
    private List<GameObject> _allPooledObjects = new List<GameObject>();
    
    public int AvailableCount => _availableObjects.Count;
    public int ActiveCount => _activeObjects.Count;
    public int TotalCount => _allPooledObjects.Count;
    
    private void Start()
    {
        InitializePool();
    }
    
    private void InitializePool()
    {
        if (poolSettings.prefab == null)
        {
            Debug.LogError($"GenericObjectPool: No prefab assigned to {gameObject.name}");
            return;
        }
        
        for (var i = 0; i < poolSettings.initialSize; i++)
        {
            CreatePooledObject();
        }
        
        Debug.Log($"GenericObjectPool '{gameObject.name}' initialized with {poolSettings.initialSize} objects");
    }
    
    private GameObject CreatePooledObject()
    {
        var obj = Instantiate(poolSettings.prefab, transform);
        obj.SetActive(false);
        
        // Add poolable component if it doesn't exist
        var poolable = obj.GetComponent<IPoolable>();
        if (poolable == null)
        {
            obj.AddComponent<DefaultPoolable>();
            poolable = obj.GetComponent<IPoolable>();
        }
        
        poolable.Initialize(this);
        
        _availableObjects.Enqueue(obj);
        _allPooledObjects.Add(obj);
        
        return obj;
    }
    
    public GameObject GetObject()
    {
        // Try to grow pool if needed
        if (_availableObjects.Count == 0)
        {
            if (poolSettings.allowGrowth && _allPooledObjects.Count < poolSettings.maxSize)
            {
                CreatePooledObject();
            }
            else
            {
                Debug.LogWarning($"Pool '{gameObject.name}' exhausted!");
                OnPoolExhausted?.Invoke();
                return null;
            }
        }
        
        var obj = _availableObjects.Dequeue();
        _activeObjects.Add(obj);
        
        // Reset object state
        if (poolSettings.resetTransformOnGet)
        {
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = poolSettings.randomizeRotationOnGet ? 
                UnityEngine.Random.rotation : Quaternion.identity;
        }
        
        if (poolSettings.resetPhysicsOnGet)
        {
            var rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
        
        obj.SetActive(true);
        
        // Notify poolable component
        var poolable = obj.GetComponent<IPoolable>();
        poolable?.OnGetFromPool();
        
        OnObjectSpawned?.Invoke(obj);
        
        return obj;
    }
    
    public void ReturnObject(GameObject obj)
    {
        if (!_allPooledObjects.Contains(obj) || !_activeObjects.Contains(obj))
        {
            Debug.LogWarning($"Trying to return object that doesn't belong to pool '{gameObject.name}'");
            return;
        }
        
        _activeObjects.Remove(obj);
        _availableObjects.Enqueue(obj);
        
        // Notify poolable component
        var poolable = obj.GetComponent<IPoolable>();
        poolable?.OnReturnToPool();
        
        obj.SetActive(false);
        
        OnObjectReturned?.Invoke(obj);
    }
    
    public void ReturnAllActiveObjects()
    {
        var activeList = new List<GameObject>(_activeObjects);
        foreach (var obj in activeList)
        {
            ReturnObject(obj);
        }
    }
    
    public void PrewarmPool(int count)
    {
        count = Mathf.Min(count, poolSettings.maxSize - _allPooledObjects.Count);
        
        for (var i = 0; i < count; i++)
        {
            CreatePooledObject();
        }
    }
}
}
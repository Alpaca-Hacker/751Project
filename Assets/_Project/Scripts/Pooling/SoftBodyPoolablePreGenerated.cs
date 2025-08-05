using SoftBody.Scripts;
using SoftBody.Scripts.Pooling;
using UnityEngine;

public class SoftBodyPoolablePreGenerated : MonoBehaviour
{
    private PreGeneratedToyPool _pool;
    private SoftBodyPhysics _softBody;
    private float _activeTime;
        
    [Header("Auto Return Settings")]
    public float autoReturnTime = 30f;
    public float fallThreshold = -20f;
        
    private void Awake()
    {
        _softBody = GetComponent<SoftBodyPhysics>();
    }
        
    public void Initialize(PreGeneratedToyPool pool)
    {
        _pool = pool;
    }
        
    public void OnGetFromPool()
    {
        _activeTime = 0f;
            
        if (_softBody != null)
        {
            _softBody.WakeUp();
            _softBody.settings.skipUpdate = false;
        }
    }
        
    public void OnReturnToPool()
    {
        if (_softBody != null)
        {
            _softBody.settings.skipUpdate = true;
        }
    }
        
    private void Update()
    {
        if (!gameObject.activeInHierarchy) return;
            
        _activeTime += Time.deltaTime;
            
        if (_activeTime > autoReturnTime || transform.position.y < fallThreshold)
        {
            _pool?.ReturnToy(gameObject);
        }
    }
}

using UnityEngine;

namespace SoftBody.Scripts.Pooling
{
    [RequireComponent(typeof(SoftBodyPhysics))]
    public class SoftBodyPoolable : MonoBehaviour
    {
        [Header("Auto Return Settings")] 
        public bool enableAutoReturn = true;
        public float autoReturnTime = 30f;
        public float fallThreshold = -20f;

        [Header("Soft Body Reset")] public bool resetPhysicsStateOnGet = true;
        public bool wakeUpOnGet = true;

        private SoftBodyPool _pool;
        private SoftBodyPhysics _softBody;
        private Vector3 _initialScale;
        private float _activeTime = 0f;
        private bool _hasBeenReturned = false;

        private void Awake()
        {
            _softBody = GetComponent<SoftBodyPhysics>();
            _initialScale = transform.localScale;
        }

        public void Initialize(SoftBodyPool pool)
        {
            _pool = pool;
        }

        public void OnGetFromPool()
        {
            _activeTime = 0f;
            _hasBeenReturned = false;
            
            if (_softBody != null)
            {
                if (_softBody.settings.useRandomMesh && _softBody.settings.changeOnActivation)
                {
                    _softBody.RegenerateWithRandomMesh();
                }
                
                if (resetPhysicsStateOnGet && _softBody.ParticleCount > 0)
                {
                    var pos = _softBody.transform.position;
                    _softBody.ResetToInitialState();
                    _softBody.SetWorldPosition(pos);
                }

                if (wakeUpOnGet)
                {
                    _softBody.WakeUp();
                }
        
                _softBody.settings.SkipUpdate = false;
            }

            // Reset scale in case it was deformed
            transform.localScale = _initialScale;
        }

        public void OnReturnToPool()
        {
            _hasBeenReturned = true;
            if (_softBody != null)
            {
                if (_softBody.settings.enableSleepSystem)
                {
                    // Reset sleep timer
                    _softBody.WakeUp();
                }

                _softBody.settings.SkipUpdate = true;
            }
        }

        public void ReturnToPool()
        {
            if (_pool != null || _hasBeenReturned)
            {
                _pool.ReturnObject(gameObject);
            }
        }

        private void Update()
        {
            if (!gameObject.activeInHierarchy || _hasBeenReturned)
            {
                return;
            }

            _activeTime += Time.deltaTime;

            // Auto return conditions
            if (enableAutoReturn && _activeTime > autoReturnTime)
            {
                ReturnToPool();
                return;
            }

            if (transform.position.y < fallThreshold)
            {
                ReturnToPool();
                return;
            }
        }
    }
}
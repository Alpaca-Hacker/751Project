using System.Collections.Generic;
using System.Linq;
using SoftBody.Scripts.Core;
using SoftBody.Scripts.Models;
using SoftBody.Scripts.Performance;
using UnityEngine;

namespace SoftBody.Scripts
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SoftBodyPhysics : MonoBehaviour
    {
        [SerializeField] public SoftBodySettings settings = new();
        [SerializeField] private ComputeShader computeShader;
        [SerializeField] private Material renderMaterial;

        [Header("Pre-Generated Physics Data")] [SerializeField]
        private SerializableSoftBodyData _preGeneratedPhysicsData;

        [SerializeField] private bool _hasPreGeneratedData = false;

        [Header("Editor Tools")] [SerializeField]
        private bool _showPhysicsDataInfo = false;

        // Core systems
        private SoftBodySimulation _simulation;
        private SoftBodyRenderer _renderer;
        private SoftBodyInteraction _interaction;
        private SoftBodyInitializer _initializer;
        private CollisionSystem _collisionSystem;
        private SleepSystem _sleepSystem;
        private BufferManager _bufferManager;
        private ComputeShaderManager _computeManager;

        private Vector3 _lastPosition;
        private Vector3 _lastCenterOfMass;
        private float _currentMovementSpeed = 0f;
        private bool _wasPreviouslyMoving = true;
        private int _sleepCheckCounter = 0;


        // State
        private SoftBodyData _softBodyData;
        private List<Particle> _initialParticles;
        private bool _isInitialized = false;

        // Public properties
        public int ParticleCount => _simulation?.ParticleCount ?? 0;
        public int ConstraintCount => _simulation?.ConstraintCount ?? 0;
        public bool IsAsleep => _sleepSystem?.IsAsleep ?? false;
        public float MovementSpeed => _sleepSystem?.CurrentSpeed ?? 0f;
        public float MemoryUsageMB => _simulation?.MemoryUsageMB ?? 0f;
        public bool HasPreGeneratedData => _hasPreGeneratedData && _preGeneratedPhysicsData.IsValid;
        public SerializableSoftBodyData GetPreGeneratedData() => _preGeneratedPhysicsData;

        private void Start()
        {
            SoftBodyCacheManager.RegisterSoftBody(this);

            if (SoftBodyPerformanceManager.Instance != null)
            {
                SoftBodyPerformanceManager.Instance.RegisterSoftBody(this);
            }

            if (_isInitialized)
            {
                return;
            }

            if (settings.SkipUpdate)
            {
                return;
            }

            InitializeSoftBody();
        }

        private void InitializeSoftBody()
        {
            // Try fast initialization first if we have pre-generated data
            if (HasPreGeneratedData && TryFastInitialization())
            {
                if (settings.debugMessages)
                {
                    Debug.Log($"Fast initialization completed for {gameObject.name} " +
                              $"({_preGeneratedPhysicsData.particleCount} particles)");
                }

                return;
            }

            // Fallback to normal initialization
            if (settings.debugMessages)
            {
                Debug.Log($"Using normal initialization for {gameObject.name} (no pre-generated data)");
            }

            _initializer = new SoftBodyInitializer(settings, transform);
            var result = _initializer.Initialize(computeShader);

            if (!result.Success)
            {
                Debug.LogError($"Soft body initialization failed: {result.ErrorMessage}");
                settings.SkipUpdate = true;
                return;
            }

            // Store systems and data
            _simulation = result.Simulation;
            _renderer = result.Renderer;
            _collisionSystem = result.CollisionSystem;
            _sleepSystem = result.SleepSystem;
            _bufferManager = result.BufferManager;
            _computeManager = result.ComputeManager;
            _softBodyData = result.SoftBodyData;
            _initialParticles = result.InitialParticles;

            // Create interaction system
            _interaction = new SoftBodyInteraction(_simulation, _sleepSystem, settings, transform);

            // Setup rendering
            _renderer.CreateMesh(_softBodyData, _softBodyData.UVs);
            _renderer.SetupMaterial(renderMaterial);

            settings.LogSettings();
            _isInitialized = true;
        }

        private bool TryFastInitialization()
        {
            try
            {
                // Convert serialized data back to runtime format
                var particles = _preGeneratedPhysicsData.particles.Select(p => p.ToParticle()).ToList();
                var constraints = _preGeneratedPhysicsData.constraints.Select(c => c.ToConstraint()).ToList();
                var volumeConstraints = _preGeneratedPhysicsData.volumeConstraints.Select(vc => vc.ToVolumeConstraint())
                    .ToList();

                // Transform particles to current world position
                for (var i = 0; i < particles.Count; i++)
                {
                    var p = particles[i];
                    p.Position = transform.TransformPoint(p.Position);
                    particles[i] = p;
                }

                // Create soft body data
                _softBodyData = new SoftBodyData
                {
                    Particles = particles,
                    Constraints = constraints,
                    VolumeConstraints = volumeConstraints,
                    Indices = _preGeneratedPhysicsData.indices.ToList(),
                    UVs = _preGeneratedPhysicsData.uvs
                };

                // Store initial state
                _initialParticles = new List<Particle>(particles);

                // Initialize systems directly
                _bufferManager = new BufferManager();
                _computeManager = new ComputeShaderManager(computeShader);
                _computeManager.SetBufferManager(_bufferManager);

                _simulation = new SoftBodySimulation(settings, _computeManager, _bufferManager);
                _simulation.Initialize(_softBodyData, transform.position);

                _computeManager.BindAllBuffersOnce();

                _renderer = new SoftBodyRenderer(transform, settings);
                _collisionSystem = new CollisionSystem(settings, transform, _computeManager, _bufferManager);
                _sleepSystem = new SleepSystem(settings, transform);
                _interaction = new SoftBodyInteraction(_simulation, _sleepSystem, settings, transform);

                // Setup rendering
                _renderer.CreateMesh(_softBodyData, _softBodyData.UVs);
                _renderer.SetupMaterial(renderMaterial);

                _isInitialized = true;
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Fast initialization failed for {gameObject.name}: {e.Message}");
                return false;
            }
        }



        private void FixedUpdate()
        {
            if (settings.SkipUpdate || !enabled || !gameObject.activeInHierarchy)
            {
                return;
            }

            if (!_isInitialized || _simulation == null || _computeManager == null)
            {
                return;
            }

            // Update sleep state
            if (settings.enableSleepSystem)
            {
                _sleepSystem.Update(Time.fixedDeltaTime); // Use fixedDeltaTime
                if (_sleepSystem.IsAsleep)
                {
                    return;
                }
            }

            // Run physics simulation - THIS is what needs fixed timing
            RunPhysicsSimulation();
            UpdateMovementTracking();

            if (settings.debugMode && Time.frameCount % 60 == 0)
            {
                DebugCollisionInfo();
            }
        }

        private void Update() // MODIFY existing method
        {
            if (settings.SkipUpdate || !enabled || !gameObject.activeInHierarchy)
            {
                return;
            }

            if (renderMaterial && _simulation != null)
            {
                var vertexBuffer = _simulation.GetVertexBuffer();
                if (vertexBuffer != null)
                {
                    renderMaterial.SetBuffer(Constants.Vertices, vertexBuffer);
                }
            }

            // Process any pending mesh updates
            _renderer?.ProcessMeshUpdate();

            // Update mesh rendering - this can happen at variable frame rate
            // _renderer?.RequestMeshUpdate(_simulation.GetVertexBuffer());
        }

        private void RunPhysicsSimulation()
        {
            var shouldUpdateMesh = SoftBodyPerformanceManager.Instance?.ShouldUpdateMesh(this) ?? true;
            // Target internal physics rate of 120Hz for stability
            const float targetInternalRate = 120f;
            var substeps = Mathf.CeilToInt(Time.fixedDeltaTime * targetInternalRate);
            substeps = Mathf.Clamp(substeps, 1, 30);

            var substepDeltaTime = Time.fixedDeltaTime / substeps;

            for (var step = 0; step < substeps; step++)
            {
                var isLastSubstep = (step == substeps - 1);
                SimulateSubstep(substepDeltaTime, isLastSubstep);
            }

            if (shouldUpdateMesh)
            {
                _renderer?.RequestMeshUpdate(_simulation.GetVertexBuffer());
            }
        }

        private void SimulateSubstep(float deltaTime, bool isLastSubstep)
        {
            // Update collisions
            if (settings.enableCollision || settings.enableSoftBodyCollisions)
            {
                _collisionSystem.UpdateColliders();
            }

            // Update simulation world position
            _simulation.UpdateWorldPosition(transform.position);

            // Run simulation step with isLastSubstep parameter
            var maxColourGroup = GetMaxColourGroup();
            _simulation.SimulateStep(deltaTime, maxColourGroup, isLastSubstep);
        }

        private int GetMaxColourGroup()
        {
            return _softBodyData?.Constraints?.Count > 0
                ? _softBodyData.Constraints.Max(c => c.ColourGroup)
                : 0;
        }

        private void OnCollisionEnter(Collision collision)
        {
            _sleepSystem?.OnCollisionImpact(collision.relativeVelocity.magnitude);
        }

        private void OnEnable()
        {
            SoftBodyCacheManager.RegisterSoftBody(this);

            if (settings.useRandomMesh && settings.changeOnActivation &&
                settings.randomMeshes.Length > 0 && !_isInitialized)
            {
                InitializeSoftBody();
            }
        }

        private void OnDestroy()
        {
            SoftBodyCacheManager.UnregisterSoftBody(this);
            _simulation?.Dispose();
            _renderer?.Dispose();
            _collisionSystem?.Unregister();
            _sleepSystem?.Unregister();
            _bufferManager?.Dispose();
        }

        private void OnDisable()
        {
            SoftBodyCacheManager.UnregisterSoftBody(this);
        }

        // Public API methods
        public void ResetToInitialState() => _interaction?.ResetToInitialState(_initialParticles);
        public void ResetVelocities() => _interaction?.ResetVelocities();

        public void PokeAtPosition(Vector3 worldPosition, Vector3 impulse, float radius = 1f) =>
            _interaction?.PokeAtPosition(worldPosition, impulse, radius);

        public void ApplyContinuousForce(Vector3 worldPosition, Vector3 force, float radius = 1f) =>
            _interaction?.ApplyContinuousForce(worldPosition, force, radius);

        public void SetWorldPosition(Vector3 position)
        {
            transform.position = position;
            _interaction?.SetWorldPosition(position);
        }

        public void WakeUp() => _sleepSystem?.WakeUp();
        public void GetParticleData(Particle[] outputArray) => _interaction?.GetParticleData(outputArray);
        public void SetParticleData(Particle[] inputArray) => _interaction?.SetParticleData(inputArray);

        public void RegenerateWithRandomMesh()
        {
            if (!settings.useRandomMesh || settings.randomMeshes.Length == 0)
            {
                return;
            }

            settings.SkipUpdate = true; // Prevent updates during regeneration
            _isInitialized = false;

            // Clean up existing systems
            _simulation?.Dispose();
            _renderer?.Dispose();
            _bufferManager?.Dispose();

            // Reinitialize with new mesh
            InitializeSoftBody();

            if (settings.debugMessages)
            {
                Debug.Log($"Regenerated with new mesh: {settings.inputMesh?.name}");
            }
        }

        private void UpdateMovementTracking()
        {
            // Only check every few frames for performance
            _sleepCheckCounter++;
            if (_sleepCheckCounter < 10) return; // Check every 10 frames
            _sleepCheckCounter = 0;

            var currentPosition = transform.position;
            var currentCenterOfMass = CalculateCenterOfMass();

            // Calculate movement speeds
            var positionMovement = Vector3.Distance(currentPosition, _lastPosition);
            var centerMovement = Vector3.Distance(currentCenterOfMass, _lastCenterOfMass);
            _currentMovementSpeed =
                Mathf.Max(positionMovement, centerMovement) / (Time.deltaTime * 10f); // Account for 10-frame skip

            var wasMoving = _currentMovementSpeed > settings.stillnessThreshold;

            if (wasMoving != _wasPreviouslyMoving)
            {
                if (settings.showSleepState)
                {
                    if (settings.debugMessages)
                    {
                        Debug.Log(
                            $"{gameObject.name} movement state changed: {(wasMoving ? "Moving" : "Slowing down")} (speed: {_currentMovementSpeed:F4})");
                    }
                }

                // Wake up nearby objects when this one starts moving significantly
                if (wasMoving && _currentMovementSpeed > settings.sleepVelocityThreshold * 4f)
                {
                    WakeUpNearbyObjects();
                }
            }

            _wasPreviouslyMoving = wasMoving;
            _lastPosition = currentPosition;
            _lastCenterOfMass = currentCenterOfMass;
        }

        private Vector3 CalculateCenterOfMass()
        {
            // Simple approximation using transform position
            // For more accuracy, you could average particle positions
            return transform.position;
        }

        private void WakeUpNearbyObjects(float radius = 0f)
        {
            if (!settings.enableProximityWake)
            {
                return;
            }

            if (Time.frameCount % settings.proximityCheckInterval != 0)
            {
                return;
            }

            if (radius <= 0f)
            {
                radius = settings.proximityWakeRadius;
            }

            var position = transform.position;
            var radiusSq = radius * radius;

            // Find all soft bodies in scene
            var nearbySoftBodies = SoftBodyCacheManager.GetSoftBodiesNear(position, radius);

            foreach (var body in nearbySoftBodies)
            {
                if (body == null || body == this || !body.IsAsleep) continue;

                body.WakeUp();
                if (settings.showSleepState)
                {
                    Debug.Log($"{body.gameObject.name} woken by nearby movement from {gameObject.name}");
                }
            }
        }

        private void DebugCollisionInfo()
        {
            if (_collisionSystem == null) return;

            Debug.Log($"SoftBody {gameObject.name}: Collision system active");

            // Add debug about current collision state
            Debug.Log($"  EnableCollision: {settings.enableCollision}");
            Debug.Log($"  EnableSoftBodyCollisions: {settings.enableSoftBodyCollisions}");
            Debug.Log($"  InteractionStrength: {settings.interactionStrength}");
            Debug.Log($"  MaxInteractionDistance: {settings.maxEnvironmentCollisionDistance}");
        }
        
        private SerializableSoftBodyData GeneratePhysicsDataForCurrentSettings()
        {
            List<Particle> particles;
            List<Constraint> constraints;
            List<VolumeConstraint> volumeConstraints;
            List<int> indices;
            Vector2[] uvs;

            // Use local position for generation, then store relative positions
            var originalPos = transform.position;
            transform.position = Vector3.zero;

            try
            {
                SoftBodyGenerator.GenerateSoftBody(settings, transform,
                    out particles, out constraints, out volumeConstraints, out indices, out uvs);

                // Apply graph colouring
                var algorithm = GraphColouringFactory.Create(settings.graphColouringMethod);
                algorithm.ApplyColouring(constraints, particles.Count);

                // Convert to local space for storage
                for (int i = 0; i < particles.Count; i++)
                {
                    var p = particles[i];
                    p.Position = transform.InverseTransformPoint(p.Position);
                    particles[i] = p;
                }

                return new SerializableSoftBodyData
                {
                    particles = particles.Select(SerializableParticle.FromParticle).ToArray(),
                    constraints = constraints.Select(SerializableConstraint.FromConstraint).ToArray(),
                    volumeConstraints = volumeConstraints.Select(SerializableVolumeConstraint.FromVolumeConstraint)
                        .ToArray(),
                    indices = indices.ToArray(),
                    uvs = uvs,
                    meshName = settings.inputMesh?.name ?? "Procedural",
                    particleCount = particles.Count,
                    constraintCount = constraints.Count,
                    volumeConstraintCount = volumeConstraints.Count
                };
            }
            finally
            {
                transform.position = originalPos;
            }
        }

        public void SetPreGeneratedPhysicsData(SerializableSoftBodyData physicsData)
        {
            _preGeneratedPhysicsData = physicsData;
            _hasPreGeneratedData = physicsData.IsValid;
    
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
        
        [ContextMenu("Generate Physics Data Now")]
        public void GeneratePhysicsDataInEditor()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                Debug.LogWarning("Cannot generate physics data during play mode");
                return;
            }
    
            try
            {
                var physicsData = GeneratePhysicsDataForCurrentSettings();
                if (physicsData.IsValid)
                {
                    SetPreGeneratedPhysicsData(physicsData);
                    Debug.Log($"Generated physics data for {gameObject.name}: " +
                              $"{physicsData.particleCount} particles, {physicsData.constraintCount} constraints");
                }
                else
                {
                    Debug.LogError($"Failed to generate physics data for {gameObject.name}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Physics data generation failed: {e.Message}");
            }
#endif
        }

        [ContextMenu("Clear Physics Data")]
        public void ClearPhysicsData()
        {
            _preGeneratedPhysicsData = new SerializableSoftBodyData();
            _hasPreGeneratedData = false;
    
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"Cleared physics data for {gameObject.name}");
#endif
        }

    }
}
using System.Collections.Generic;
using System.Linq;
using SoftBody.Scripts.Core;
using SoftBody.Scripts.Models;
using UnityEngine;

namespace SoftBody.Scripts
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SoftBodyPhysics : MonoBehaviour
    {
        [SerializeField] public SoftBodySettings settings = new();
        [SerializeField] private ComputeShader computeShader;
        [SerializeField] private Material renderMaterial;

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

        // Public properties
        public int ParticleCount => _simulation?.ParticleCount ?? 0;
        public int ConstraintCount => _simulation?.ConstraintCount ?? 0;
        public bool IsAsleep => _sleepSystem?.IsAsleep ?? false;
        public float MovementSpeed => _sleepSystem?.CurrentSpeed ?? 0f;
        public float MemoryUsageMB => _simulation?.MemoryUsageMB ?? 0f;

        private void Start()
        {
            if (settings.SkipUpdate) return;

            InitializeSoftBody();
        }

        private void InitializeSoftBody()
        {
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
        }

        private void Update()
        {
            if (settings.SkipUpdate || !enabled || !gameObject.activeInHierarchy)
                return;

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

            // Update sleep state
            if (settings.enableSleepSystem)
            {
                _sleepSystem.Update(Time.deltaTime);
                if (_sleepSystem.IsAsleep) return;
            }

            // Run physics simulation
            RunPhysicsSimulation();
            UpdateMovementTracking();
            if (settings.debugMode && Time.frameCount % 60 == 0)
            {
                DebugCollisionInfo();
            }

            // Update mesh rendering
            _renderer?.RequestMeshUpdate(_simulation.GetVertexBuffer());
        }

        private void RunPhysicsSimulation()
        {
            //  Debug.Log($"Running physics for {gameObject.name}");

            var targetDeltaTime = 1f / 60f;
            var substeps = Mathf.CeilToInt(Time.deltaTime / targetDeltaTime);
            substeps = Mathf.Clamp(substeps, 1, 100);

            var substepDeltaTime = Time.deltaTime / substeps;

            for (var step = 0; step < substeps; step++)
            {
                var isLastSubstep = (step == substeps - 1);
                SimulateSubstep(substepDeltaTime, isLastSubstep);
            }

            _renderer?.RequestMeshUpdate(_simulation.GetVertexBuffer());
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
            if (settings.useRandomMesh && settings.changeOnActivation && settings.randomMeshes.Length > 0)
            {
                RegenerateWithRandomMesh();
            }
        }

        private void OnDestroy()
        {
            _simulation?.Dispose();
            _renderer?.Dispose();
            _collisionSystem?.Unregister();
            _sleepSystem?.Unregister();
            _bufferManager?.Dispose();
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
            if (!settings.useRandomMesh || settings.randomMeshes.Length == 0) return;

            settings.SkipUpdate = true; // Prevent updates during regeneration

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
            if (!settings.enableProximityWake) return;

            if (Time.frameCount % settings.proximityCheckInterval != 0) return;

            if (radius <= 0f) radius = settings.proximityWakeRadius;

            var position = transform.position;
            var radiusSq = radius * radius;

            // Find all soft bodies in scene
            var allSoftBodies = FindObjectsByType<SoftBodyPhysics>(FindObjectsSortMode.None);

            foreach (var body in allSoftBodies)
            {
                if (body == null || body == this || !body.IsAsleep) continue;

                var distanceSq = Vector3.SqrMagnitude(position - body.transform.position);
                if (distanceSq < radiusSq)
                {
                    body.WakeUp();
                    if (settings.showSleepState)
                    {
                        Debug.Log($"{body.gameObject.name} woken by nearby movement from {gameObject.name}");
                    }
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
            Debug.Log($"  MaxInteractionDistance: {settings.maxInteractionDistance}");
        }
    }
}
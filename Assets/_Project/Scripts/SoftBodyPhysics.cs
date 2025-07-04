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
        [SerializeField] 
        public SoftBodySettings settings = new();
        [SerializeField] 
        private ComputeShader computeShader;
        [SerializeField] 
        private Material renderMaterial;
        
        public float MemoryUsageMB => _simulation?.MemoryUsageMB ?? 0f;
        
        // Core systems
        private SoftBodySimulation _simulation;
        private MeshManager _meshManager;
        private BufferManager _bufferManager;
        private ComputeShaderManager _computeManager;
        private CollisionSystem _collisionSystem;
        private SleepSystem _sleepSystem;
        
        // Data
        private SoftBodyData _softBodyData;
        private List<Particle> _initialParticles;
        
        // Public properties
        public int ParticleCount => _simulation?.ParticleCount ?? 0;
        public int ConstraintCount => _simulation?.ConstraintCount ?? 0;
        public bool IsAsleep => _sleepSystem?.IsAsleep ?? false;
        public float MovementSpeed => _sleepSystem?.CurrentSpeed ?? 0f;
        
        private void Start()
        {
            if (!ValidateComponents()) return;
            
            InitializeSystems();
            GenerateSoftBody();
            SetupRendering();
        }
        
        private bool ValidateComponents()
        {
            if (computeShader == null)
            {
                Debug.LogError("Compute shader not assigned!");
                return false;
            }
            return true;
        }
        
        private void InitializeSystems()
        {
            _bufferManager = new BufferManager();
            _computeManager = new ComputeShaderManager(computeShader);
            _computeManager.SetBufferManager(_bufferManager);
            
            _simulation = new SoftBodySimulation(settings, _computeManager, _bufferManager);
            _meshManager = new MeshManager(transform);
            _collisionSystem = new CollisionSystem(settings, transform, _computeManager, _bufferManager);
            _sleepSystem = new SleepSystem(settings, transform);
        }
        
        private void GenerateSoftBody()
        {
            // Generate soft body data
            List<Particle> particles;
            List<Constraint> constraints;
            List<VolumeConstraint> volumeConstraints;
            List<int> indices;
            Vector2[] uvs;
            
            SoftBodyGenerator.GenerateSoftBody(settings, transform, 
                out particles, out constraints, out volumeConstraints, out indices, out uvs);
            
            // Apply graph colouring
            ApplyGraphColouring(constraints, particles.Count);
            
            // Store data
            _softBodyData = new SoftBodyData
            {
                Particles = particles,
                Constraints = constraints,
                VolumeConstraints = volumeConstraints,
                Indices = indices
            };
            
            _initialParticles = new List<Particle>(particles);
            
            // Initialize simulation
            _simulation.Initialize(_softBodyData, transform.position);
            
            // Create mesh
            _meshManager.CreateMesh(_softBodyData, uvs);
        }
        
        private void SetupRendering()
        {
            _meshManager.SetMaterial(renderMaterial);
        }
        
        private void Update()
        {
            if (settings.SkipUpdate || !enabled || !gameObject.activeInHierarchy)
            {
                return;
            }
            
            _meshManager.ProcessReadbackData(); 
            
            // Update sleep state
            if (settings.enableSleepSystem)
            {
                _sleepSystem.Update(Time.deltaTime);
                if (_sleepSystem.IsAsleep)
                {
                    return;
                }
            }
            
            // Run simulation
            var targetDeltaTime = 1f / 60f;
            var substeps = Mathf.CeilToInt(Time.deltaTime / targetDeltaTime);
            substeps = Mathf.Clamp(substeps, 1, 10);
            
            var substepDeltaTime = Time.deltaTime / substeps;
            
            for (int step = 0; step < substeps; step++)
            {
                SimulateSubstep(substepDeltaTime);
            }
            
            // Update mesh
            _meshManager.RequestMeshUpdate(_simulation.GetVertexBuffer());
        }
        
        private void SimulateSubstep(float deltaTime)
        {
            // Update collisions
            if (settings.enableCollision || settings.enableSoftBodyCollisions)
            {
                _collisionSystem.UpdateColliders();
            }
            
            // Run simulation
            _simulation.UpdateWorldPosition(transform.position);
            
            var maxColourGroup = GetMaxColourGroup();
            _simulation.SimulateStep(deltaTime, maxColourGroup);
            
            // Apply collision response
            if (settings.enableCollision|| settings.enableSoftBodyCollisions)
            {
                _collisionSystem.ApplyCollisions(_simulation.ParticleCount);
            }
        }
        
        private int GetMaxColourGroup()
        {
            return _softBodyData.Constraints.Max(c => c.ColourGroup);
        }
        
        private void ApplyGraphColouring(List<Constraint> constraints, int particleCount)
        {
            // Initialize all constraints with colour group 0 as fallback
            for (var i = 0; i < constraints.Count; i++)
            {
                var c = constraints[i];
                c.ColourGroup = 0;
                constraints[i] = c;
            }

            try
            {

                List<Cluster> clusters;
                switch (settings.graphColouringMethod)
                {
                    case GraphColouringMethod.Naive:
                    {
                        GraphColouring.ApplyNaiveGraphColouring(constraints, settings.debugMessages);
                        break;
                    }
                    case GraphColouringMethod.Clustering:
                    {
                        clusters = GraphColouring.CreateClusters(constraints, particleCount,
                            settings.debugMessages);
                        GraphColouring.ColourClusters(clusters, constraints, settings.debugMessages);
                        break;
                    }
                    case GraphColouringMethod.None:
                    {
                        for (var i = 0; i < constraints.Count; i++)
                        {
                            var c = constraints[i];
                            c.ColourGroup = i;
                            constraints[i] = c;
                        }

                        break;
                    }
                    case GraphColouringMethod.Greedy:
                    {
                        var numColors =
                            GraphColouring.ColourConstraints(constraints, particleCount, settings.debugMessages);
                        if (settings.debugMessages)
                        {
                            Debug.Log($"Successfully applied graph coloring with {numColors} colours");
                        }

                        break;
                    }
                    case GraphColouringMethod.SpectralPartitioning:
                    {
                        clusters = GraphColouring.CreateClustersWithSpectralPartitioning(constraints,
                            particleCount, settings.debugMessages);
                        GraphColouring.ColourClusters(clusters, constraints, settings.debugMessages);
                        break;
                    }
                    default:
                        Debug.LogError($"Unknown graph colouring method: {settings.graphColouringMethod}");
                        break;
                }

            }
            catch (System.Exception e)
            {
                Debug.LogError($"Graph clustering failed: {e.Message}");
            }
        }
        
        private void OnCollisionEnter(Collision collision)
        {
            if (settings.enableSleepSystem && _sleepSystem != null)
            {
                _sleepSystem.OnCollisionImpact(collision.relativeVelocity.magnitude);
            }
        }
        
        // Public API methods
        public void ResetToInitialState()
        {
            if (_initialParticles == null) return;
            
            var resetParticles = _initialParticles.Select(p => 
            {
                var particle = p;
                particle.Velocity = Vector4.zero;
                particle.Force = Vector4.zero;
                return particle;
            }).ToList();
            
            _simulation.SetParticleData(resetParticles);
            _sleepSystem?.WakeUp();
        }
        
        public void PokeAtPosition(Vector3 worldPosition, Vector3 impulse, float radius = 1f)
        {
            _sleepSystem?.WakeUp();
            _simulation.ApplyImpulse(worldPosition, impulse, radius);
        }
        
        public void SetWorldPosition(Vector3 position)
        {
            _simulation.SetWorldPosition(position);
            transform.position = position;
        }
        
        private void OnDestroy()
        {
            _simulation?.Dispose();
            _collisionSystem?.Unregister();
            _sleepSystem?.Unregister();
            _bufferManager?.Dispose();
        }
        public void RegenerateWithRandomMesh()
        {
            if (!settings.useRandomMesh || settings.randomMeshes.Length == 0) return;

            // Select a new random mesh
            var randomIndex = Random.Range(0, settings.randomMeshes.Length);
            settings.inputMesh = settings.randomMeshes[randomIndex];
        
            // Dispose of old simulation data
            _simulation?.Dispose();
        
            // Regenerate everything
            GenerateSoftBody();
            SetupRendering();
        
            Debug.Log($"Regenerated with new mesh: {settings.inputMesh.name}");
        }

        public void WakeUp()
        {
            _sleepSystem?.WakeUp();
        }
    
        public void ResetVelocities()
        {
            _simulation?.ResetVelocities();
        }

        public void ApplyContinuousForce(Vector3 worldPosition, Vector3 force, float radius)
        {
            _sleepSystem?.WakeUp();
            _simulation?.ApplyContinuousForce(worldPosition, force, radius);
        }

        public void GetParticleData(Particle[] outputArray)
        {
            _simulation?.GetParticleData(outputArray);
        }
    
        public void SetParticleData(Particle[] inputArray)
        {
            _simulation?.SetParticleData(new List<Particle>(inputArray));
        }
    }
}
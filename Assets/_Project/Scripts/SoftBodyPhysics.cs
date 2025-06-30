
using System.Collections.Generic;
using System.Linq;
using SoftBody.Scripts.Models;
using UnityEngine;
using static System.Runtime.InteropServices.Marshal;

namespace SoftBody.Scripts
{
    public class SoftBodyPhysics : MonoBehaviour
    {
        [SerializeField] public SoftBodySettings settings = new();
        [SerializeField] private ComputeShader computeShader;
        [SerializeField] private Material renderMaterial;

        public int ParticleCount => _particles?.Count ?? 0;
        public int ConstraintCount => _constraints?.Count ?? 0;

        private ComputeBuffer _particleBuffer;
        private ComputeBuffer _constraintBuffer;
        private ComputeBuffer _vertexBuffer;
        private ComputeBuffer _indexBuffer;
        private ComputeBuffer _debugBuffer;
        private ComputeBuffer _volumeConstraintBuffer;
        private ComputeBuffer _previousPositionsBuffer;
        private ComputeBuffer _colliderBuffer;
        private ComputeBuffer _collisionCorrectionsBuffer;

        private Mesh _mesh;
        private List<Particle> _particles;
        private List<Constraint> _constraints;
        private List<VolumeConstraint> _volumeConstraints;
        private List<int> _indices;
        private List<SDFCollider> _colliders = new();
        private Vector2[] _weldedUVs;

        private int _kernelIntegrateAndStore;
        private int _kernelSolveConstraints;
        private int _kernelUpdateMesh;
        private int _kernelDecayLambdas;
        private int _kernelVolumeConstraints;
        private int _kernelUpdateVelocities;
        private int _kernelDebugAndValidate;
        private int _kernelSolveGeneralCollisions;
        private int _kernelApplyCollisionCorrections;

        private UnityEngine.Rendering.AsyncGPUReadbackRequest _readbackRequest;
        private bool _isReadbackPending = false;
        private List<Particle> _initialParticles;

        private SoftBodyProfiler _profiler;

        private bool _isAsleep = false;
        private float _sleepTimer = 0f;
        private Vector3 _lastPosition;
        private Vector3 _lastCenterOfMass;
        private int _sleepCheckCounter = 0;
        private float _currentMovementSpeed = 0f;
        private bool _wasPreviouslyMoving = true;

        private float _lastActiveTime;
        private float _totalSleepTime = 0f;
        
        private static List<SoftBodyPhysics> _allSoftBodies = new ();
        private static float _lastCacheUpdate = 0f;
        private static readonly float CacheUpdateInterval = 2f;

        public bool IsAsleep => _isAsleep;
        public float MovementSpeed => _currentMovementSpeed;
        public float SleepEfficiency => Time.time > 0 ? _totalSleepTime / Time.time : 0f;

        private void Start()
        {
            try
            {
                _profiler = gameObject.AddComponent<SoftBodyProfiler>();

                Debug.Log("SoftBodySimulator: Starting initialization...");
                InitializeComputeShader();

                SoftBodyGenerator.GenerateSoftBody(settings, transform, out _particles, out _constraints,
                    out _volumeConstraints, out _indices, out _weldedUVs);

                ApplyGraphColouring();
                SetupBuffers();
                SetupRenderMaterial();

                Debug.Log(
                    $"Initialization complete. Particles: {_particles?.Count}, Constraints: {_constraints?.Count}");

                settings.LogSettings();

                if (_particles != null && _particles.Count > 0)
                {
                    var testParticle = _particles[0];
                    Debug.Log($"First particle position: {testParticle.Position}, invMass: {testParticle.InvMass}");
                }

                _lastPosition = transform.position;
                _lastCenterOfMass = CalculateCenterOfMass();
                _lastActiveTime = Time.time;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Initialization failed: {e.Message}\n{e.StackTrace}");
                settings.SkipUpdate = true;
            }
        }

        private void InitializeComputeShader()
        {
            if (computeShader == null)
            {
                Debug.LogError("Compute Shader not assigned! Please assign the XPBDSoftBody compute shader.");
                return;

            }

            _kernelIntegrateAndStore = computeShader.FindKernel("IntegrateAndStorePositions");
            _kernelSolveConstraints = computeShader.FindKernel("SolveConstraints");
            _kernelUpdateMesh = computeShader.FindKernel("UpdateMesh");
            _kernelDecayLambdas = computeShader.FindKernel("DecayLambdas");
            _kernelVolumeConstraints = computeShader.FindKernel("SolveVolumeConstraints");
            _kernelUpdateVelocities = computeShader.FindKernel("UpdateVelocities");
            _kernelDebugAndValidate = computeShader.FindKernel("DebugAndValidateParticles");
            _kernelSolveGeneralCollisions = computeShader.FindKernel("SolveGeneralCollisions");
            _kernelApplyCollisionCorrections = computeShader.FindKernel("ApplyCollisionCorrections");

            // Verify all kernels were found
            if (_kernelIntegrateAndStore == -1 || _kernelSolveConstraints == -1 || _kernelUpdateMesh == -1 ||
                _kernelDecayLambdas == -1)
            {
                Debug.LogError(
                    "Could not find required compute shader kernels! Make sure the compute shader has IntegrateParticles, SolveConstraints, and UpdateMesh kernels.");
            }
            else
            {
                Debug.Log("Compute shader kernels found successfully.");
            }
        }

        private void ApplyGraphColouring()
        {
            // Initialize all constraints with colour group 0 as fallback
            for (var i = 0; i < _constraints.Count; i++)
            {
                var c = _constraints[i];
                c.ColourGroup = 0;
                _constraints[i] = c;
            }

            try
            {

                List<Cluster> clusters;
                switch (settings.graphColouringMethod)
                {
                    case GraphColouringMethod.Naive:
                    {
                        GraphColouring.ApplyNaiveGraphColouring(_constraints);
                        break;
                    }
                    case GraphColouringMethod.Clustering:
                    {
                        clusters = GraphColouring.CreateClusters(_constraints, _particles.Count);
                        GraphColouring.ColourClusters(clusters, _constraints);
                        break;
                    }
                    case GraphColouringMethod.None:
                    {
                        for (var i = 0; i < _constraints.Count; i++)
                        {
                            var c = _constraints[i];
                            c.ColourGroup = i;
                            _constraints[i] = c;
                        }

                        break;
                    }
                    case GraphColouringMethod.Greedy:
                    {
                        var numColors = GraphColouring.ColourConstraints(_constraints, _particles.Count);
                        Debug.Log($"Successfully applied graph coloring with {numColors} colors");
                        break;
                    }
                    case GraphColouringMethod.SpectralPartitioning:
                    {
                        clusters = GraphColouring.CreateClustersWithSpectralPartitioning(_constraints,
                            _particles.Count);
                        GraphColouring.ColourClusters(clusters, _constraints);
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

        private void SetupBuffers()
        {

            if (_particles == null || _particles.Count == 0)
            {
                Debug.LogError("No particles generated! Check mesh input.");
                settings.SkipUpdate = true;
                return;
            }

            if (_constraints == null || _constraints.Count == 0)
            {
                Debug.LogError("No constraints generated! Check mesh topology.");
                settings.SkipUpdate = true;
                return;
            }

            // Create compute buffers
            _particleBuffer = new ComputeBuffer(_particles.Count, SizeOf<Particle>());
            _constraintBuffer = new ComputeBuffer(_constraints.Count, SizeOf<Constraint>());
            _vertexBuffer = new ComputeBuffer(_particles.Count, sizeof(float) * 3);
            _indexBuffer = new ComputeBuffer(_indices.Count, sizeof(int));
            _debugBuffer = new ComputeBuffer(1, sizeof(float) * 4);
            _volumeConstraintBuffer =
                new ComputeBuffer(Mathf.Max(1, _volumeConstraints.Count), SizeOf<VolumeConstraint>());
            _previousPositionsBuffer = new ComputeBuffer(_particles.Count, sizeof(float) * 3);
            _colliderBuffer = new ComputeBuffer(64, SizeOf<SDFCollider>());
            _collisionCorrectionsBuffer = new ComputeBuffer(_particles.Count, sizeof(float) * 3);

            ValidateAllData();

            // Upload initial data
            _particleBuffer.SetData(_particles);
            _constraintBuffer.SetData(_constraints);
            _indexBuffer.SetData(_indices);
            _volumeConstraintBuffer.SetData(_volumeConstraints);

            // Store initial particle positions for reset functionality
            _initialParticles = new List<Particle>(_particles);

            // Create mesh
            _mesh = new Mesh();
            _mesh.name = settings.inputMesh != null ? $"SoftBody_{settings.inputMesh.name}" : "SoftBody_Procedural";

            var vertices = new Vector3[_particles.Count];
            for (var i = 0; i < _particles.Count; i++)
            {
                vertices[i] = transform.InverseTransformPoint(_particles[i].Position);
            }

            _mesh.vertices = vertices;
            _mesh.triangles = _indices.ToArray();

            // Apply welded UVs if available
            if (_weldedUVs != null && _weldedUVs.Length == _particles.Count)
            {
                _mesh.uv = _weldedUVs;
                Debug.Log("Applied remapped UVs after welding");
            }
            else if (_weldedUVs != null)
            {
                Debug.LogWarning($"UV count mismatch: {_weldedUVs.Length} UVs vs {_particles.Count} particles");

                // Create expanded UV array if needed
                var expandedUVs = new Vector2[_particles.Count];
                var copyCount = Mathf.Min(_weldedUVs.Length, _particles.Count);
                System.Array.Copy(_weldedUVs, expandedUVs, copyCount);
                _mesh.uv = expandedUVs;
            }

            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();

            // Ensure MeshFilter exists and assign mesh
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            meshFilter.mesh = _mesh;

            Debug.Log($"Soft body initialized with {_particles.Count} particles and {_constraints.Count} constraints");
            Debug.Log(
                $"Constraint buffer size: {SizeOf<Constraint>()} bytes per constraint");
        }

        private void SetupRenderMaterial()
        {
            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }

            // Use the assigned material, or fallback to default
            if (renderMaterial != null)
            {
                meshRenderer.material = renderMaterial;
                Debug.Log($"Applied custom material: {renderMaterial.name}");
            }
            else
            {
                // Fallback material for URP
                var fallbackMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                fallbackMaterial.color = Color.cyan;
                meshRenderer.material = fallbackMaterial;
                Debug.LogWarning(
                    "No material assigned! Using fallback URP/Lit material. Please assign a material in the SoftBodySettings.");
            }

            // Ensure proper lighting setup
            SetupLighting();
        }

        private void SetupLighting()
        {

            if (_mesh)
            {
                _mesh.RecalculateNormals();
                _mesh.RecalculateTangents();
            }
        }

        private void Update()
        {
            if (settings.SkipUpdate || !enabled || !gameObject.activeInHierarchy)
            {
                return;
            }

            if (!computeShader)
            {
                Debug.LogError("Compute Shader not assigned to SoftBodySimulator!");
                return;
            }

            if (_particleBuffer == null)
            {
                Debug.LogError("Particle buffer not initialized!");
                return;
            }
            
            if (!ValidateBuffers())
            {
                Debug.LogWarning($"SoftBody '{gameObject.name}' has invalid buffers, skipping frame");
                return;
            }

            if (renderMaterial && _vertexBuffer != null)
            {
                renderMaterial.SetBuffer(Constants.Vertices, _vertexBuffer);
            }

            if (settings.enableSleepSystem || settings.enableMovementDampening)
            {
                UpdateMovementAndSleep();
            }

            if (settings.enableSleepSystem && _isAsleep)
            {
                _totalSleepTime += Time.deltaTime;
                return; // Don't run any physics
            }

            if (settings.enableMovementDampening && ShouldApplyDampening())
            {
                ApplyMovementDampening();
            }

            _lastActiveTime = Time.time;


            var targetDeltaTime = 1f / 60f; // 60 Hz physics
            var frameTime = Time.deltaTime;

            // Subdivide large frames into small steps
            var substeps = Mathf.CeilToInt(frameTime / targetDeltaTime);
            substeps = Mathf.Clamp(substeps, 1, 100); // Max 10 substeps per frame

            var substepDeltaTime = frameTime / substeps;

            for (var step = 0; step < substeps; step++)
            {
                var isLastSubstep = (step == substeps - 1);
                SimulateSubstep(substepDeltaTime, isLastSubstep);
            }

            // Update mesh (async, won't block)
            UpdateMeshFromGPU();
            UpdateMovementTracking();
        }

        private bool ValidateBuffers()
        {
            return _particleBuffer != null && 
                   _constraintBuffer != null && 
                   _vertexBuffer != null && 
                   _particles != null && 
                   _particles.Count > 0;
        }

        private void SimulateSubstep(float deltaTime, bool isLastSubstep)
        {
            if (!ValidateBuffers())
            {
                Debug.LogWarning("Skipping substep due to invalid buffers");
                return;
            }
            // Start overall frame timing
            var frameTimer = System.Diagnostics.Stopwatch.StartNew();
            var stepTimer = System.Diagnostics.Stopwatch.StartNew();

            // Initialize metrics
            var metrics = new PerformanceMetrics();

            // Set compute shader parameters (unchanged)
            SetComputeShaderParameters(deltaTime);
            if (settings.enableCollision)
            {
                UpdateColliders();
            }

            BindBuffers();

            var constraintThreadGroups = Mathf.CeilToInt(_constraints.Count / 64f);
            var particleThreadGroups = Mathf.CeilToInt(_particles.Count / 64f);
            var volumeConstraintThreadGroups = Mathf.CeilToInt(_volumeConstraints.Count / 64f);

            // // Calculate actual thread utilization
            // var actualConstraintThreads = constraintThreadGroups * 64;
            // var actualParticleThreads = particleThreadGroups * 64;
            // var actualVolumeThreads = volumeConstraintThreadGroups * 64;
            //
            // var constraintUtilization = _constraints.Count / (float)actualConstraintThreads * 100f;
            // var particleUtilization = _particles.Count / (float)actualParticleThreads * 100f;
            // var volumeUtilization = _volumeConstraints.Count > 0 ? _volumeConstraints.Count / (float)actualVolumeThreads * 100f : 0f;

            // Log every 60 frames
            // if (Time.frameCount % 60 == 0)
            // {
            //     Debug.Log($"THREAD EFFICIENCY REPORT:");
            //     Debug.Log($"  Particles: {_particles.Count} actual, {actualParticleThreads} dispatched ({particleUtilization:F1}% efficient)");
            //     Debug.Log($"  Constraints: {_constraints.Count} actual, {actualConstraintThreads} dispatched ({constraintUtilization:F1}% efficient)");
            //     Debug.Log($"  Volume: {_volumeConstraints.Count} actual, {actualVolumeThreads} dispatched ({volumeUtilization:F1}% efficient)");
            //     Debug.Log($"  Thread Groups: P={particleThreadGroups}, C={constraintThreadGroups}, V={volumeConstraintThreadGroups}");
            // }

            // Integrate particles
            // var constraintThreadGroups = Mathf.CeilToInt(_constraints.Count / 64f);
            // var particleThreadGroups = Mathf.CeilToInt(_particles.Count / 64f);
            // var volumeConstraintThreadGroups = Mathf.CeilToInt(_volumeConstraints.Count / 64f);

            // === LAMBDA DECAY ===
            SoftBodyProfiler.BeginSample("LambdaDecay");
            stepTimer.Restart();

            if (constraintThreadGroups > 0)
            {
                computeShader.Dispatch(_kernelDecayLambdas, constraintThreadGroups, 1, 1);
            }

            var lambdaDecayTime = (float)stepTimer.Elapsed.TotalMilliseconds;
            SoftBodyProfiler.EndSample("LambdaDecay");

            // === INTEGRATION ===
            SoftBodyProfiler.BeginSample("Integration");
            stepTimer.Restart();

            if (particleThreadGroups > 0)
            {
                computeShader.Dispatch(_kernelIntegrateAndStore, particleThreadGroups, 1, 1);
            }

            metrics.integrationTime = (float)stepTimer.Elapsed.TotalMilliseconds;
            SoftBodyProfiler.EndSample("Integration");

            // === CONSTRAINT SOLVING LOOP ===
            SoftBodyProfiler.BeginSample("ConstraintSolving");

            // Timing accumulators for detailed metrics
            var totalConstraintTime = 0f;
            var totalVolumeTime = 0f;
            var totalCollisionTime = 0f;


            for (var iter = 0; iter < settings.solverIterations; iter++)
            {
                var maxColourGroup = GetMaxColourGroup();

                for (var colourGroup = 0; colourGroup <= maxColourGroup; colourGroup++)
                {
                    // Time constraint solving per colour group
                    stepTimer.Restart();

                    computeShader.SetInt(Constants.CurrentColourGroup, colourGroup);

                    if (constraintThreadGroups > 0)
                    {
                        computeShader.Dispatch(_kernelSolveConstraints, constraintThreadGroups, 1, 1);
                    }

                    totalConstraintTime += (float)stepTimer.Elapsed.TotalMilliseconds;

                    // Volume constraints
                    if (_volumeConstraints.Count > 0 && colourGroup == 0)
                    {
                        stepTimer.Restart();
                        computeShader.Dispatch(_kernelVolumeConstraints, volumeConstraintThreadGroups, 1, 1);
                        totalVolumeTime += (float)stepTimer.Elapsed.TotalMilliseconds;
                    }
                }

                // Collision solving
                if (_colliders.Count > 0)
                {
                    stepTimer.Restart();
                    computeShader.Dispatch(_kernelSolveGeneralCollisions, particleThreadGroups, 1, 1);
                    computeShader.Dispatch(_kernelApplyCollisionCorrections, particleThreadGroups, 1, 1);
                    totalCollisionTime += (float)stepTimer.Elapsed.TotalMilliseconds;
                }
            }

            metrics.constraintSolvingTime = totalConstraintTime;
            metrics.volumeConstraintTime = totalVolumeTime;
            metrics.collisionTime = totalCollisionTime;
            SoftBodyProfiler.EndSample("ConstraintSolving");

            // === VELOCITY UPDATE (unchanged) ===
            SoftBodyProfiler.BeginSample("VelocityUpdate");
            stepTimer.Restart();

            if (particleThreadGroups > 0)
            {
                computeShader.Dispatch(_kernelUpdateVelocities, particleThreadGroups, 1, 1);
            }

            var velocityUpdateTime = (float)stepTimer.Elapsed.TotalMilliseconds;
            SoftBodyProfiler.EndSample("VelocityUpdate");

            // === MESH UPDATE ===
            if (isLastSubstep)
            {
                SoftBodyProfiler.BeginSample("MeshUpdate");
                stepTimer.Restart();

                if (particleThreadGroups > 0)
                {
                    computeShader.Dispatch(_kernelUpdateMesh, particleThreadGroups, 1, 1);
                }

                metrics.meshUpdateTime = (float)stepTimer.Elapsed.TotalMilliseconds;
                SoftBodyProfiler.EndSample("MeshUpdate");
            }

            // === DEBUG VALIDATION ===
            computeShader.Dispatch(_kernelDebugAndValidate, 1, 1, 1);

            // Debug output (unchanged frequency and logic)
            if (Time.frameCount % 10 == 0 && settings.debugMode)
            {
                var debugData = new float[4];
                _debugBuffer.GetData(debugData);
                if (debugData[0] > 0 || debugData[1] > 0)
                {
                    Debug.LogError(
                        $"INSTABILITY DETECTED! NaN Count: {debugData[0]}, Inf Count: {debugData[1]}, Max Speed: {debugData[2]:F2}, First Bad Particle Index: {debugData[3]}");
                }
                else if (settings.debugMode) // Only log stability in debug mode
                {
                    Debug.Log($"System stable. Max Speed: {debugData[2]:F2}");
                }
            }

            // === RECORD FINAL METRICS ===
            metrics.totalFrameTime = (float)frameTimer.Elapsed.TotalMilliseconds;
            metrics.activeParticles = _particles.Count;
            metrics.activeConstraints = _constraints.Count;
            metrics.solverIterations = settings.solverIterations;
            metrics.memoryUsageMB = CalculateMemoryUsage();

            // Additional detailed metrics
            metrics.lambdaDecayTime = lambdaDecayTime;
            metrics.velocityUpdateTime = velocityUpdateTime;

            // Record metrics if profiler exists
            if (_profiler != null)
            {
                _profiler.RecordMetrics(metrics);
            }
        }

        private void BindBuffers()
        {
            computeShader.SetBuffer(_kernelIntegrateAndStore, Constants.Particles, _particleBuffer);
            computeShader.SetBuffer(_kernelIntegrateAndStore, Constants.PreviousPositions, _previousPositionsBuffer);

            computeShader.SetBuffer(_kernelSolveConstraints, Constants.Particles, _particleBuffer);
            computeShader.SetBuffer(_kernelSolveConstraints, Constants.Constraints, _constraintBuffer);

            computeShader.SetBuffer(_kernelUpdateMesh, Constants.Particles, _particleBuffer);
            computeShader.SetBuffer(_kernelUpdateMesh, Constants.Vertices, _vertexBuffer);
            computeShader.SetBuffer(_kernelDecayLambdas, Constants.Constraints, _constraintBuffer);

            computeShader.SetBuffer(_kernelVolumeConstraints, Constants.Particles, _particleBuffer);
            computeShader.SetBuffer(_kernelVolumeConstraints, Constants.VolumeConstraints, _volumeConstraintBuffer);

            computeShader.SetBuffer(_kernelUpdateVelocities, Constants.Particles, _particleBuffer);
            computeShader.SetBuffer(_kernelUpdateVelocities, Constants.PreviousPositions, _previousPositionsBuffer);

            computeShader.SetBuffer(_kernelDebugAndValidate, Constants.Particles, _particleBuffer);
            computeShader.SetBuffer(_kernelDebugAndValidate, Constants.DebugBuffer, _debugBuffer);

            computeShader.SetBuffer(_kernelSolveGeneralCollisions, Constants.Particles, _particleBuffer);
            computeShader.SetBuffer(_kernelSolveGeneralCollisions, Constants.Colliders, _colliderBuffer);
            computeShader.SetBuffer(_kernelSolveGeneralCollisions, Constants.CollisionCorrections,
                _collisionCorrectionsBuffer);

            computeShader.SetBuffer(_kernelApplyCollisionCorrections, Constants.Particles, _particleBuffer);
            computeShader.SetBuffer(_kernelApplyCollisionCorrections, Constants.CollisionCorrections,
                _collisionCorrectionsBuffer);
            computeShader.SetBuffer(_kernelApplyCollisionCorrections, Constants.PreviousPositions,
                _previousPositionsBuffer);
        }


        private void SetComputeShaderParameters(float deltaTime)
        {
            computeShader.SetFloat(Constants.DeltaTime, deltaTime);
            computeShader.SetFloat(Constants.Gravity, settings.gravity);
            computeShader.SetFloat(Constants.Damping, settings.damping);
            computeShader.SetVector(Constants.WorldPosition, transform.position);
            computeShader.SetInt(Constants.ParticleCount, _particles.Count);
            computeShader.SetInt(Constants.ConstraintCount, _constraints.Count);
            computeShader.SetFloat(Constants.LambdaDecay, settings.lambdaDecay);
            computeShader.SetInt(Constants.VolumeConstraintCount, _volumeConstraints.Count);
        }

        private void UpdateColliders()
        {
            _colliders.Clear();

            // Collect all Unity colliders in the scene
            var allColliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);

            foreach (var col in allColliders)
            {
                if (_colliders.Count >= 64) break;

                // Skip self and other soft bodies
                if (col.GetComponent<SoftBodyPhysics>() != null) continue;

                // Skip triggers
                if (col.isTrigger) continue;

                // Convert Unity collider to SDF representation
                SDFCollider? sdfCollider = ConvertToSDFCollider(col);
                if (sdfCollider.HasValue)
                {
                    _colliders.Add(sdfCollider.Value);
                }
            }

            // Upload to GPU
            if (_colliders.Count > 0)
            {
                _colliderBuffer.SetData(_colliders, 0, 0, _colliders.Count);
            }

            computeShader.SetInt(Constants.ColliderCount, _colliders.Count);
        }

        private SDFCollider? ConvertToSDFCollider(Collider col)
        {
            if (col.CompareTag("Floor") || col.name.ToLower().Contains("floor"))
            {
                // Always treat floor as a plane
                var planeNormal = col.transform.up;
                var planePos = col.transform.position;
    
                // Offset slightly up to account for collider thickness
                planePos += planeNormal * 0.01f;
    
                var planeDistance = Vector3.Dot(planePos, planeNormal);
                return SDFCollider.CreatePlane(planeNormal, planeDistance);
            }
            
            switch (col)
            {
                case BoxCollider box:
                    var boxTransform = box.transform;
                    var center = boxTransform.TransformPoint(box.center);
                    var size = Vector3.Scale(box.size, boxTransform.lossyScale);
                    return SDFCollider.CreateBox(center, size * 0.5f, boxTransform.rotation);

                case SphereCollider sphere:
                    var sphereTransform = sphere.transform;
                    var sphereCenter = sphereTransform.TransformPoint(sphere.center);
                    var radius = sphere.radius * Mathf.Max(
                        sphereTransform.lossyScale.x,
                        sphereTransform.lossyScale.y,
                        sphereTransform.lossyScale.z);
                    return SDFCollider.CreateSphere(sphereCenter, radius);

                case CapsuleCollider capsule:
                    // Convert capsule to cylinder (approximation)
                    var capsuleTransform = capsule.transform;
                    var capsuleCenter = capsuleTransform.TransformPoint(capsule.center);
                    var capsuleRadius = capsule.radius * Mathf.Max(
                        capsuleTransform.lossyScale.x,
                        capsuleTransform.lossyScale.z);
                    var capsuleHeight = capsule.height * capsuleTransform.lossyScale.y;

                    // Capsule direction affects rotation
                    Quaternion capsuleRotation = capsuleTransform.rotation;
                    if (capsule.direction == 0) // X-axis
                        capsuleRotation *= Quaternion.Euler(0, 0, 90);
                    else if (capsule.direction == 2) // Z-axis
                        capsuleRotation *= Quaternion.Euler(90, 0, 0);

                    return SDFCollider.CreateCylinder(capsuleCenter, capsuleRadius, capsuleHeight, capsuleRotation);

                case MeshCollider mesh when mesh.convex:
                    // For now, approximate convex mesh as box
                    var bounds = mesh.bounds;
                    var meshCenter = mesh.transform.TransformPoint(bounds.center);
                    var meshSize = Vector3.Scale(bounds.size, mesh.transform.lossyScale);
                    return SDFCollider.CreateBox(meshCenter, meshSize * 0.5f, mesh.transform.rotation);

                default:
                    return null;
            }
        }


        private int GetMaxColourGroup()
        {
            return _constraints.Max(c => c.ColourGroup);
        }

        private void UpdateMeshFromGPU()
        {
            if (_mesh == null) return;

            // Don't start new readback if one is pending
            if (_isReadbackPending)
            {
                // Check if readback is complete
                if (_readbackRequest.done)
                {
                    _isReadbackPending = false;

                    if (_readbackRequest.hasError)
                    {
                        Debug.LogWarning("AsyncGPUReadback failed! Skipping frame.");
                        
                        return;
                    }

                    // Process the readback data
                    var data = _readbackRequest.GetData<float>();
                    ProcessVertexData(data);
                }

                return; // Wait for current readback to complete
            }

            // Start new async readback
            _readbackRequest = UnityEngine.Rendering.AsyncGPUReadback.Request(_vertexBuffer);
            _isReadbackPending = true;
        }

        private void ProcessVertexData(Unity.Collections.NativeArray<float> vertexData)
        {
            var vertices = new Vector3[_particles.Count];
            var centreOffset = Vector3.zero;
            var worldPositions = new Vector3[_particles.Count];

            // First pass: read positions and check validity
            for (var i = 0; i < _particles.Count; i++)
            {
                var worldPos = new Vector3(
                    vertexData[i * 3],
                    vertexData[i * 3 + 1],
                    vertexData[i * 3 + 2]
                );

                // Check for invalid data
                if (float.IsNaN(worldPos.x) || float.IsNaN(worldPos.y) || float.IsNaN(worldPos.z) ||
                    float.IsInfinity(worldPos.x) || float.IsInfinity(worldPos.y) || float.IsInfinity(worldPos.z))
                {
                    Debug.LogWarning($"Invalid GPU data at particle {i}: {worldPos}");
                    return;
                }

                worldPositions[i] = worldPos;
                centreOffset += worldPos;
            }

            // Calculate center of mass
            centreOffset /= _particles.Count;

            // Update transform to follow center of mass
            transform.position = centreOffset;

            // Convert to local coordinates
            for (var i = 0; i < _particles.Count; i++)
            {
                vertices[i] = worldPositions[i] - centreOffset;
            }

            try
            {
                _mesh.vertices = vertices;
                _mesh.RecalculateNormals();
                _mesh.RecalculateTangents();
                _mesh.RecalculateBounds();

                // Force mesh filter update
                GetComponent<MeshFilter>().mesh = _mesh;

                // Update collider if present
                var meshCollider = GetComponent<MeshCollider>();
                if (meshCollider)
                {
                    meshCollider.sharedMesh = _mesh;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to update mesh: {e.Message}");
                _isReadbackPending = false;
            }
        }

        private void UpdateMovementAndSleep()
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

            // Sleep detection
            if (settings.enableSleepSystem)
            {
                UpdateSleepState();
            }

            _lastPosition = currentPosition;
            _lastCenterOfMass = currentCenterOfMass;
        }

        private void UpdateSleepState()
        {
            if (_currentMovementSpeed < settings.sleepVelocityThreshold)
            {
                _sleepTimer += Time.deltaTime * 10f; // Account for frame skipping

                if (_sleepTimer > settings.sleepTimeThreshold && !_isAsleep)
                {
                    GoToSleep();
                }
            }
            else
            {
                _sleepTimer = 0f;
                if (_isAsleep)
                {
                    WakeUp();
                }
            }
        }

        private bool ShouldApplyDampening()
        {
            return _currentMovementSpeed > settings.minMovementSpeed &&
                   _currentMovementSpeed < settings.stillnessThreshold &&
                   !_isAsleep;
        }

        private void ApplyMovementDampening()
        {
            if (_particleBuffer == null) return;

            // Get current particle data
            var currentParticles = new Particle[_particles.Count];
            _particleBuffer.GetData(currentParticles);

            // Apply dampening to velocities
            for (var i = 0; i < currentParticles.Length; i++)
            {
                var p = currentParticles[i];
                if (p.InvMass > 0) // Only dampen moveable particles
                {
                    p.Velocity.x *= settings.dampeningStrength;
                    p.Velocity.y *= settings.dampeningStrength;
                    p.Velocity.z *= settings.dampeningStrength;

                    // Zero out very small velocities
                    if (Mathf.Abs(p.Velocity.x) < settings.minMovementSpeed) p.Velocity.x = 0;
                    if (Mathf.Abs(p.Velocity.y) < settings.minMovementSpeed) p.Velocity.y = 0;
                    if (Mathf.Abs(p.Velocity.z) < settings.minMovementSpeed) p.Velocity.z = 0;

                    currentParticles[i] = p;
                }
            }

            // Upload modified particles back to GPU
            _particleBuffer.SetData(currentParticles);
        }

        private void UpdateMovementTracking()
        {
            var wasMoving = _currentMovementSpeed > settings.stillnessThreshold;
    
            if (wasMoving != _wasPreviouslyMoving)
            {
                if (settings.showSleepState)
                {
                    Debug.Log($"{gameObject.name} movement state changed: {(wasMoving ? "Moving" : "Slowing down")} (speed: {_currentMovementSpeed:F4})");
                }
        
                // Wake up nearby objects when this one starts moving significantly
                if (wasMoving && _currentMovementSpeed > settings.sleepVelocityThreshold * 4f) // Only for significant movement
                {
                    WakeUpNearbyObjects();
                }
            }
    
            _wasPreviouslyMoving = wasMoving;
        }

        private void GoToSleep()
        {
            _isAsleep = true;
            if (settings.showSleepState)
            {
                Debug.Log(
                    $"{gameObject.name} went to sleep (speed: {_currentMovementSpeed:F4}, inactive for: {_sleepTimer:F1}s)");
            }
        }

        public void WakeUp()
        {
            if (_isAsleep && settings.showSleepState)
            {
                Debug.Log($"{gameObject.name} woke up after {Time.time - _lastActiveTime:F1}s of sleep");
            }

            _isAsleep = false;
            _sleepTimer = 0f;
        }

        private Vector3 CalculateCenterOfMass()
        {
            // Simple approximation using transform position
            // For more accuracy, you could average particle positions
            return transform.position;
        }
        
        private void OnCollisionEnter(Collision collision)
        {
            if (settings.enableSleepSystem && _isAsleep)
            {
                var impactForce = collision.relativeVelocity.magnitude;
                if (impactForce > 0.5f) // Significant impact
                {
                    WakeUp();
                    if (settings.showSleepState) Debug.Log($"{gameObject.name} woken by collision (force: {impactForce:F2})");
                }
            }
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
    
            if (radius <= 0f) radius = settings.proximityWakeRadius;
    
            // Update static cache periodically
            if (Time.time - _lastCacheUpdate > CacheUpdateInterval)
            {
                _allSoftBodies.Clear();
                _allSoftBodies.AddRange(FindObjectsByType<SoftBodyPhysics>(FindObjectsSortMode.None));
                _lastCacheUpdate = Time.time;
            }
    
            var position = transform.position;
            var radiusSq = radius * radius;
    
            // Check cached soft bodies
            foreach (var body in _allSoftBodies)
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
        

        private void OnDestroy()
        {
            ReleaseBuffers();
            if (_mesh != null)
            {
                Destroy(_mesh);
            }

        }

        private void ReleaseBuffers()
        {
            _particleBuffer?.Release();
            _constraintBuffer?.Release();
            _vertexBuffer?.Release();
            _indexBuffer?.Release();
            _debugBuffer?.Release();
            _volumeConstraintBuffer?.Release();
            _previousPositionsBuffer?.Release();
            _colliderBuffer?.Release();
            _collisionCorrectionsBuffer?.Release();

            // Set them to null so we know they are released
            _particleBuffer = null;
            _constraintBuffer = null;
            _vertexBuffer = null;
            _indexBuffer = null;
            _debugBuffer = null;
            _volumeConstraintBuffer = null;
            _previousPositionsBuffer = null;
            _colliderBuffer = null;
            _collisionCorrectionsBuffer = null;

        }

        private void OnValidate()
        {
            // Regenerate mesh when settings change in editor
            if (Application.isPlaying && _particles != null)
            {
            
                ReleaseBuffers();
                SetupBuffers();
                ResetToInitialState();
            }
        }

        private void ValidateAllData()
        {
            var particleCount = _particles.Count;
            if (particleCount == 0)
            {
                Debug.Log("Validation skipped: No particles.");
                return;
            }

            // --- Validate Constraints ---
            for (var i = 0; i < _constraints.Count; i++)
            {
                var c = _constraints[i];
                if (c.ParticleA < 0 || c.ParticleA >= particleCount ||
                    c.ParticleB < 0 || c.ParticleB >= particleCount)
                {
                    Debug.LogError(
                        $"CRITICAL ERROR IN CONSTRAINT DATA! Constraint at index {i} has invalid particle indices: A={c.ParticleA}, B={c.ParticleB}. Particle count is {particleCount}. THIS WILL CAUSE A GPU CRASH.");
                    settings.SkipUpdate = true; // Halt simulation
                    return;
                }

                if (c.RestLength <= 0)
                {
                    Debug.LogWarning($"Constraint at index {i} has zero or negative rest length: {c.RestLength}");
                }
            }

            // --- Validate Volume Constraints ---
            for (var i = 0; i < _volumeConstraints.Count; i++)
            {
                var vc = _volumeConstraints[i];
                if (vc.P1 < 0 || vc.P1 >= particleCount ||
                    vc.P2 < 0 || vc.P2 >= particleCount ||
                    vc.P3 < 0 || vc.P3 >= particleCount ||
                    vc.P4 < 0 || vc.P4 >= particleCount)
                {
                    Debug.LogError(
                        $"CRITICAL ERROR IN VOLUME CONSTRAINT DATA! VolumeConstraint at index {i} has invalid particle indices. P1={vc.P1}, P2={vc.P2}, P3={vc.P3}, P4={vc.P4}. THIS WILL CAUSE A GPU CRASH.");
                    settings.SkipUpdate = true; // Halt simulation
                    return;
                }
            }

            Debug.Log("CPU Data Validation PASSED. All constraint indices are within particle bounds.");
        }

        public void SetParticleData(Particle[] inputArray)
        {
            if (_particleBuffer != null && inputArray.Length == _particles.Count)
            {
                _particleBuffer.SetData(inputArray);
            }
        }

        public void GetParticleData(Particle[] outputArray)
        {
            if (_particleBuffer != null && outputArray.Length >= _particles.Count)
            {
                _particleBuffer.GetData(outputArray, 0, 0, _particles.Count);
            }
        }

        private float CalculateMemoryUsage()
        {
            var totalBytes = 0f;

            if (_particleBuffer != null) totalBytes += _particleBuffer.count * _particleBuffer.stride;
            if (_constraintBuffer != null) totalBytes += _constraintBuffer.count * _constraintBuffer.stride;
            if (_vertexBuffer != null) totalBytes += _vertexBuffer.count * _vertexBuffer.stride;
            if (_indexBuffer != null) totalBytes += _indexBuffer.count * _indexBuffer.stride;
            if (_debugBuffer != null) totalBytes += _debugBuffer.count * _debugBuffer.stride;
            if (_volumeConstraintBuffer != null)
                totalBytes += _volumeConstraintBuffer.count * _volumeConstraintBuffer.stride;
            if (_previousPositionsBuffer != null)
                totalBytes += _previousPositionsBuffer.count * _previousPositionsBuffer.stride;
            if (_colliderBuffer != null) totalBytes += _colliderBuffer.count * _colliderBuffer.stride;
            if (_collisionCorrectionsBuffer != null)
                totalBytes += _collisionCorrectionsBuffer.count * _collisionCorrectionsBuffer.stride;

            return totalBytes / (1024f * 1024f); // Convert to MB
        }


        #region Designer Methods

        // Public methods for designer interaction
        public void ResetToInitialState()
        {
            if (_initialParticles == null || _particleBuffer == null)
            {
                Debug.LogWarning("Cannot reset - initial state not stored or buffers not initialized");
                return;
            }

            // Reset particles to initial positions and clear velocities
            var resetParticles = new List<Particle>();
            foreach (var particle in _initialParticles)
            {
                var p = particle;
                p.Velocity = Vector4.zero; // Clear velocity
                p.Force = Vector4.zero; // Clear forces
                resetParticles.Add(p);
            }

            // Update the lists and buffers
            _particles = resetParticles;
            _particleBuffer.SetData(_particles);

            // Reset constraint lambdas
            for (var i = 0; i < _constraints.Count; i++)
            {
                var c = _constraints[i];
                c.Lambda = 0f;
                _constraints[i] = c;
            }

            _constraintBuffer.SetData(_constraints);

            // Reset volume constraint lambdas
            for (var i = 0; i < _volumeConstraints.Count; i++)
            {
                var vc = _volumeConstraints[i];
                vc.Lambda = 0f;
                _volumeConstraints[i] = vc;
            }

            _volumeConstraintBuffer.SetData(_volumeConstraints);

            Debug.Log("Soft body reset to initial state");
        }

        public void ResetVelocities()
        {
            if (_particleBuffer == null) return;

            var currentParticles = new Particle[_particles.Count];
            _particleBuffer.GetData(currentParticles);

            for (var i = 0; i < currentParticles.Length; i++)
            {
                var p = currentParticles[i];
                p.Velocity = Vector4.zero;
                p.Force = Vector4.zero;
                currentParticles[i] = p;
            }

            _particleBuffer.SetData(currentParticles);
            Debug.Log("Velocities reset");
        }

        public void PokeAtPosition(Vector3 worldPosition, Vector3 impulse, float radius = 1f)
        {
            if (_particles == null || _particles.Count == 0 || _particleBuffer == null)
            {
                Debug.LogWarning("Cannot poke, physics system not initialized.");
                return;
            }
            
            WakeUp();

            // Get current particle data
            var currentParticles = new Particle[_particles.Count];
            _particleBuffer.GetData(currentParticles);

            var affectedParticles = new List<int>();
            var radiusSq = radius * radius;

            // Find all particles within radius
            for (var i = 0; i < currentParticles.Length; i++)
            {
                var distSq = Vector3.SqrMagnitude(currentParticles[i].Position - worldPosition);
                if (distSq <= radiusSq && currentParticles[i].InvMass > 0)
                {
                    affectedParticles.Add(i);
                }
            }

            if (affectedParticles.Count == 0) return;

            Debug.Log($"Poking {affectedParticles.Count} particles at {worldPosition}");

            // Apply impulse with falloff
            foreach (var idx in affectedParticles)
            {
                var p = currentParticles[idx];
                var distance = Vector3.Distance(p.Position, worldPosition);
                var falloff = 1f - (distance / radius);

                var deltaVelocity = impulse * (falloff * p.InvMass);
                p.Velocity.x += deltaVelocity.x;
                p.Velocity.y += deltaVelocity.y;
                p.Velocity.z += deltaVelocity.z;

                currentParticles[idx] = p;
            }

            // Upload modified particles back to GPU
            _particleBuffer.SetData(currentParticles);
        }

// Add continuous force application
        public void ApplyContinuousForce(Vector3 worldPosition, Vector3 force, float radius = 1f)
        {
            if (_particles == null || _particleBuffer == null) return;

            WakeUp();
            // Use a more efficient approach - only read/write affected particles
            var currentParticles = new Particle[_particles.Count];
            _particleBuffer.GetData(currentParticles);

            var radiusSq = radius * radius;
            var modifiedIndices = new List<int>();

            for (var i = 0; i < currentParticles.Length; i++)
            {
                var distSq = Vector3.SqrMagnitude(currentParticles[i].Position - worldPosition);
                if (distSq <= radiusSq && currentParticles[i].InvMass > 0)
                {
                    var p = currentParticles[i];
                    var distance = Mathf.Sqrt(distSq);
                    var falloff = 1f - (distance / radius);

                    // Apply force as velocity change (more responsive)
                    var deltaVelocity = force * (falloff * p.InvMass * Time.deltaTime);
                    p.Velocity.x += deltaVelocity.x;
                    p.Velocity.y += deltaVelocity.y;
                    p.Velocity.z += deltaVelocity.z;

                    currentParticles[i] = p;
                    modifiedIndices.Add(i);
                }
            }

            if (modifiedIndices.Count > 0)
            {
                _particleBuffer.SetData(currentParticles);
            }
        }

        #endregion
    }
}

using System.Collections.Generic;
using System.Linq;
using SoftBody.Scripts.Models;
using UnityEngine;
using static System.Runtime.InteropServices.Marshal;

namespace SoftBody.Scripts
{
    public class SoftBodyPhysics : MonoBehaviour
    {
        [SerializeField] private SoftBodySettings settings = new ();
        [SerializeField] private ComputeShader computeShader;
        [SerializeField] private Material renderMaterial;
        
        
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
        private List<SDFCollider> _colliders = new ();

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

        private void Start()
        {
            try
            {
                Debug.Log("SoftBodySimulator: Starting initialization...");
                InitializeComputeShader();
                SoftBodyGenerator.GenerateCube(settings, transform, out _particles, out _constraints, out _volumeConstraints, out _indices);
                ApplyGraphColouring();
                SetupBuffers();
                SetupRenderMaterial();
                
                Debug.Log($"Initialization complete. Particles: {_particles?.Count}, Constraints: {_constraints?.Count}");
                settings.LogSettings();
                
                if (_particles != null && _particles.Count > 0)
                {
                    var testParticle = _particles[0];
                    Debug.Log($"First particle position: {testParticle.Position}, invMass: {testParticle.InvMass}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Initialization failed: {e.Message}\n{e.StackTrace}");
                settings.useCPUFallback = true;
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
            if (_kernelIntegrateAndStore == -1 || _kernelSolveConstraints == -1 || _kernelUpdateMesh == -1 || _kernelDecayLambdas == -1)
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
                var clusters = new List<Cluster>();
                switch (settings.GraphColouringMethod)
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
                        clusters = GraphColouring.CreateClustersWithSpectralPartitioning(_constraints, _particles.Count);
                        GraphColouring.ColourClusters(clusters, _constraints);
                        break;
                    }
                    default:
                        Debug.LogError($"Unknown graph colouring method: {settings.GraphColouringMethod}");
                        break;
                }
          
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Graph clustering failed: {e.Message}, using naive colouring");
              //  ApplyNaiveGraphColouring();
            }
        }

        private void SetupBuffers()
        {
            // Create compute buffers
            _particleBuffer = new ComputeBuffer(_particles.Count, SizeOf<Particle>());
            _constraintBuffer = new ComputeBuffer(_constraints.Count, SizeOf<Constraint>());
            _vertexBuffer = new ComputeBuffer(_particles.Count, sizeof(float) * 3);
            _indexBuffer = new ComputeBuffer(_indices.Count, sizeof(int));
            _debugBuffer = new ComputeBuffer(1, sizeof(float) * 4);
            _volumeConstraintBuffer = new ComputeBuffer(_volumeConstraints.Count, SizeOf<VolumeConstraint>());
            _previousPositionsBuffer = new ComputeBuffer(_particles.Count, sizeof(float) * 3);
            _colliderBuffer = new ComputeBuffer(64, SizeOf<SDFCollider>());
            _collisionCorrectionsBuffer = new ComputeBuffer(_particles.Count, sizeof(float) * 3);

            ValidateAllData();
            if (settings.useCPUFallback)
            {
                Debug.LogWarning("Using CPU fallback mode! Performance may be significantly reduced.");
                return;
            }
            // Upload initial data
            _particleBuffer.SetData(_particles);
            _constraintBuffer.SetData(_constraints);
            _indexBuffer.SetData(_indices);
            _volumeConstraintBuffer.SetData(_volumeConstraints);

            // Create mesh
            _mesh = new Mesh();
            _mesh.name = "SoftBody";

            var vertices = new Vector3[_particles.Count];
            for (var i = 0; i < _particles.Count; i++)
            {
                vertices[i] = transform.InverseTransformPoint(_particles[i].Position);
            }

            _mesh.vertices = vertices;
            _mesh.triangles = _indices.ToArray();
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
                Debug.LogWarning("No material assigned! Using fallback URP/Lit material. Please assign a material in the SoftBodySettings.");
            }

            // Ensure proper lighting setup
            SetupLighting();
        }

        private void SetupLighting()
        {
            // Ensure the mesh has proper normals for lighting
            if (_mesh != null)
            {
                _mesh.RecalculateNormals();
                _mesh.RecalculateTangents(); // Important for normal mapping in URP
            }

            // Optional: Add a MeshCollider for more accurate lighting interactions
            var meshCollider = GetComponent<MeshCollider>();
            if (meshCollider == null && settings.enableCollision)
            {
                meshCollider = gameObject.AddComponent<MeshCollider>();
                meshCollider.convex = true; // Required for soft body physics
                meshCollider.sharedMesh = _mesh;
            }
        }

        private void Update()
        {
            if (settings.useCPUFallback)
            {
              //  UpdateCPU();
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
            
            if(renderMaterial && _vertexBuffer != null)
            {
                renderMaterial.SetBuffer(Constants.Vertices, _vertexBuffer);
            }
            
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
        }

        private void SimulateSubstep(float deltaTime, bool isLastSubstep)
        {
            
            SetComputeShaderParameters(deltaTime);
            if (settings.enableCollision)
            {
                 UpdateColliders();
            }
            BindBuffers();
            
            // Integrate particles
            var constraintThreadGroups = Mathf.CeilToInt(_constraints.Count / 64f);
            var particleThreadGroups = Mathf.CeilToInt(_particles.Count / 64f);
            var volumeConstraintThreadGroups = Mathf.CeilToInt(_volumeConstraints.Count / 64f);
            
            if (constraintThreadGroups > 0)
            {
                computeShader.Dispatch(_kernelDecayLambdas, constraintThreadGroups, 1, 1);
            }
            
            if (particleThreadGroups > 0)
            {
                computeShader.Dispatch(_kernelIntegrateAndStore, particleThreadGroups, 1, 1);
            }
            
            for (var iter = 0; iter < settings.solverIterations; iter++)
            {
                var maxColourGroup = GetMaxColourGroup();

                for (var colourGroup = 0; colourGroup <= maxColourGroup; colourGroup++)
                {
                    
                    computeShader.SetInt(Constants.CurrentColourGroup, colourGroup);
                    
                    if (constraintThreadGroups > 0)
                    {
                        computeShader.Dispatch(_kernelSolveConstraints, constraintThreadGroups, 1, 1);
                    }
                    
                    if (_volumeConstraints.Count > 0 && colourGroup == 0)
                    {
                        computeShader.Dispatch(_kernelVolumeConstraints, volumeConstraintThreadGroups, 1, 1);
                    }
                }

                if (_colliders.Count > 0)
                {
                   computeShader.Dispatch(_kernelSolveGeneralCollisions, particleThreadGroups, 1, 1);
                   computeShader.Dispatch(_kernelApplyCollisionCorrections, particleThreadGroups, 1, 1);
                }
            }
            
            if (particleThreadGroups > 0)
            {
                computeShader.Dispatch(_kernelUpdateVelocities, particleThreadGroups, 1, 1);
            }
            
            // Update mesh vertices (only on last substep to save bandwidth)
            if (isLastSubstep)
            {
                if (particleThreadGroups > 0)
                {
                    computeShader.Dispatch(_kernelUpdateMesh, particleThreadGroups, 1, 1);
                }
            }
            
            computeShader.Dispatch(_kernelDebugAndValidate, 1, 1, 1);
            
            if (Time.frameCount % 10 == 0 && settings.debugMode)
            {
                var debugData = new float[4];
                _debugBuffer.GetData(debugData);
                if (debugData[0] > 0 || debugData[1] > 0)
                {
                    Debug.LogError($"INSTABILITY DETECTED! NaN Count: {debugData[0]}, Inf Count: {debugData[1]}, Max Speed: {debugData[2]:F2}, First Bad Particle Index: {debugData[3]}");
                }
                else
                {
                    Debug.Log($"System stable. Max Speed: {debugData[2]:F2}");
                }
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
            computeShader.SetBuffer(_kernelSolveGeneralCollisions, Constants.CollisionCorrections, _collisionCorrectionsBuffer);

            computeShader.SetBuffer(_kernelApplyCollisionCorrections, Constants.Particles, _particleBuffer);
            computeShader.SetBuffer(_kernelApplyCollisionCorrections, Constants.CollisionCorrections, _collisionCorrectionsBuffer);
            computeShader.SetBuffer(_kernelApplyCollisionCorrections, Constants.PreviousPositions, _previousPositionsBuffer);
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

            if (settings.floorTransform)
            {
                var planeNormal = settings.floorTransform.up;
        
                // The distance of the plane from the world origin (0,0,0) is calculated
                // by projecting the plane's position onto its own normal.
                var planeDistance = Vector3.Dot(settings.floorTransform.position, planeNormal);

                // Create the SDFCollider struct for the plane.
                var floorPlane = SDFCollider.CreatePlane(planeNormal, planeDistance);
                _colliders.Add(floorPlane);
            }
            else
            {
                // Fallback if no floor is assigned (uses the old raycast method)
                var floorPlane = SDFCollider.CreatePlane(Vector3.up, 0);
                _colliders.Add(floorPlane);
            }
            
            
            // Example: Find all GameObjects with a specific tag and add them
             foreach (var sphereCollider in FindObjectsByType<SphereCollider>(FindObjectsSortMode.None))
            {
                if (_colliders.Count >= 64)
                {
                    break;
                } // Don't exceed buffer capacity
                var sphere = SDFCollider.CreateSphere(sphereCollider.transform.position, sphereCollider.radius * sphereCollider.transform.lossyScale.x);
                _colliders.Add(sphere);
            }

            // Upload the data to the GPU
            if (_colliders.Count > 0)
            {
                _colliderBuffer.SetData(_colliders, 0, 0, _colliders.Count);
            }
            computeShader.SetInt(Constants.ColliderCount, _colliders.Count);
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
                        Debug.LogError("AsyncGPUReadback failed! Switching to CPU mode.");
                        settings.useCPUFallback = true;
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
                    settings.useCPUFallback = true;
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
            }
        }

        private void ResetToInitialPositions()
        {
            // Reset particles to initial positions
            for (var i = 0; i < _particles.Count; i++)
            {
                var p = _particles[i];
                p.Velocity = Vector4.zero;
                p.Force = Vector4.zero;
                // Keep original position but reset physics state
                _particles[i] = p;
            }

            if (_particleBuffer != null)
            {
                _particleBuffer.SetData(_particles);
            }

            Debug.Log("Reset particles to initial state due to invalid data");
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
                
                SoftBodyGenerator.GenerateCube(settings, transform, out _particles, out _constraints, out _volumeConstraints, out _indices);
                SetupBuffers();
                ResetToInitialPositions();
            }
        }
        
        private void ValidateAllData()
        {
            int particleCount = _particles.Count;
            if (particleCount == 0)
            {
                Debug.Log("Validation skipped: No particles.");
                return;
            }
        
            // --- Validate Constraints ---
            for (int i = 0; i < _constraints.Count; i++)
            {
                Constraint c = _constraints[i];
                if (c.ParticleA < 0 || c.ParticleA >= particleCount ||
                    c.ParticleB < 0 || c.ParticleB >= particleCount)
                {
                    Debug.LogError($"CRITICAL ERROR IN CONSTRAINT DATA! Constraint at index {i} has invalid particle indices: A={c.ParticleA}, B={c.ParticleB}. Particle count is {particleCount}. THIS WILL CAUSE A GPU CRASH.");
                    settings.useCPUFallback = true; // Halt simulation
                    return;
                }
                if (c.RestLength <= 0)
                {
                    Debug.LogWarning($"Constraint at index {i} has zero or negative rest length: {c.RestLength}");
                }
            }
        
            // --- Validate Volume Constraints ---
            for (int i = 0; i < _volumeConstraints.Count; i++)
            {
                VolumeConstraint vc = _volumeConstraints[i];
                if (vc.P1 < 0 || vc.P1 >= particleCount ||
                    vc.P2 < 0 || vc.P2 >= particleCount ||
                    vc.P3 < 0 || vc.P3 >= particleCount ||
                    vc.P4 < 0 || vc.P4 >= particleCount)
                {
                    Debug.LogError($"CRITICAL ERROR IN VOLUME CONSTRAINT DATA! VolumeConstraint at index {i} has invalid particle indices. P1={vc.P1}, P2={vc.P2}, P3={vc.P3}, P4={vc.P4}. THIS WILL CAUSE A GPU CRASH.");
                    settings.useCPUFallback = true; // Halt simulation
                    return;
                }
            }
            
            Debug.Log("CPU Data Validation PASSED. All constraint indices are within particle bounds.");
        }

        #region Designer Methods

        // Public methods for designer interaction
        public void AddForce(Vector3 force, Vector3 position, float radius = 1f)
        {
            // Add external force to particles within radius
            for (var i = 0; i < _particles.Count; i++)
            {
                var distance = Vector3.Distance(_particles[i].Position, position);
                if (distance < radius)
                {
                    var falloff = 1f - (distance / radius);
                    var p = _particles[i];
                    var forceToApply = force * falloff;
                    p.Force.x = forceToApply.x;
                    p.Force.y = forceToApply.y;
                    p.Force.z = forceToApply.z;
                    
                    _particles[i] = p;
                }
            }

            _particleBuffer.SetData(_particles);
            Debug.Log($"Applied force {force} to {_particles.Count} particles");
        }

        public void PokeParticle(Vector3 worldPosition, Vector3 impulse)
        {
            if (_particles == null || _particles.Count == 0 || _particleBuffer == null)
            {
                Debug.LogWarning("Cannot poke, physics system not initialized.");
                return;
            }
            
            var currentParticles = new Particle[_particles.Count];
            _particleBuffer.GetData(currentParticles);

            var closestParticleIndex = -1;
            var minDistanceSq = float.MaxValue;

            for (var i = 0; i < currentParticles.Length; i++)
            {
                var distSq = Vector3.SqrMagnitude(currentParticles[i].Position - worldPosition);
                if (distSq < minDistanceSq)
                {
                    minDistanceSq = distSq;
                    closestParticleIndex = i;
                }
            }

            if (closestParticleIndex != -1)
            {
                Debug.Log($"Poking particle {closestParticleIndex} with impulse {impulse}.");
                
                var p = currentParticles[closestParticleIndex];
                
                if (p.InvMass > 0)
                {
                    var deltaVelocity = impulse * p.InvMass;
                    p.Velocity.x += deltaVelocity.x;
                    p.Velocity.y += deltaVelocity.y;
                    p.Velocity.z += deltaVelocity.z;

                    currentParticles[closestParticleIndex] = p;
                    
                    _particleBuffer.SetData(currentParticles, closestParticleIndex, closestParticleIndex, 1);
                }
                else
                {
                    Debug.Log($"Attempted to poke particle {closestParticleIndex}, but it is pinned (invMass = 0).");
                }
            }
        }

        public void SetPinned(Vector3 position, float radius = 0.5f, bool pinned = true)
        {
            // Pin/unpin particles within radius
            for (var i = 0; i < _particles.Count; i++)
            {
                var distance = Vector3.Distance(_particles[i].Position, position);
                if (distance < radius)
                {
                    var p = _particles[i];
                    p.InvMass = pinned ? 0f : 1f / settings.mass;
                    _particles[i] = p;
                }
            }

            _particleBuffer.SetData(_particles);
        }

        #endregion
        
    }
}

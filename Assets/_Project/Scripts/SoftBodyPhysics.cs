
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
        
        private UnityEngine.Rendering.AsyncGPUReadbackRequest _readbackRequest;
        private bool _isReadbackPending = false;

        private void Start()
        {
            try
            {
                Debug.Log("SoftBodySimulator: Starting initialization...");
                InitializeComputeShader();
                GenerateMesh();
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
            _kernelIntegrateAndStore = computeShader.FindKernel("IntegrateAndStorePositions");
            _kernelUpdateVelocities = computeShader.FindKernel("UpdateVelocities");
            _kernelDebugAndValidate = computeShader.FindKernel("DebugAndValidateParticles");
            _kernelSolveGeneralCollisions = computeShader.FindKernel("SolveGeneralCollisions");

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

        private void GenerateMesh()
        {
            _particles = new List<Particle>();
            _constraints = new List<Constraint>();
            _indices = new List<int>();

            var res = settings.resolution;
            var spacing = new Vector3(
                settings.size.x / (res - 1),
                settings.size.y / (res - 1),
                settings.size.z / (res - 1)
            );

            // Generate particles in a 3D grid
            for (var x = 0; x < res; x++)
            {
                for (var y = 0; y < res; y++)
                {
                    for (var z = 0; z < res; z++)
                    {
                        var pos = new Vector3(
                            x * spacing.x - settings.size.x * 0.5f,
                            y * spacing.y - settings.size.y * 0.5f,
                            z * spacing.z - settings.size.z * 0.5f
                        );

                        var particle = new Particle
                        {
                            Position = transform.TransformPoint(pos),
                            Velocity = Vector4.zero,
                            Force = Vector4.zero,
                            InvMass = 1f / settings.mass
                        };

                        _particles.Add(particle);
                    }
                }
            }
            
            GenerateStructuralConstraints();  // Main edges
            GenerateShearConstraints();       // Face diagonals  
            GenerateBendConstraints();        // Volume diagonals
            GenerateVolumeConstraints();
            ApplyGraphColouring();
            GenerateMeshTopology();
        }
        
        private void GenerateStructuralConstraints()
        {
            var res = settings.resolution;
            
            for (var x = 0; x < res; x++)
            {
                for (var y = 0; y < res; y++)
                {
                    for (var z = 0; z < res; z++)
                    {
                        var index = x * res * res + y * res + z;
                
                        if (x < res - 1) AddConstraint(index, (x + 1) * res * res + y * res + z, settings.structuralCompliance);
                        if (y < res - 1) AddConstraint(index, x * res * res + (y + 1) * res + z, settings.structuralCompliance);
                        if (z < res - 1) AddConstraint(index, x * res * res + y * res + (z + 1), settings.structuralCompliance);
                    }
                }
            }
        }

        private void GenerateShearConstraints()
        {
            var res = settings.resolution;
            
            for (var x = 0; x < res - 1; x++)
            {
                for (var y = 0; y < res - 1; y++)
                {
                    for (var z = 0; z < res - 1; z++)
                    {
                        // XY face diagonals
                        AddConstraint(
                            x * res * res + y * res + z,
                            (x + 1) * res * res + (y + 1) * res + z,
                            settings.shearCompliance
                        );

                        // XZ face diagonals  
                        AddConstraint(
                            x * res * res + y * res + z,
                            (x + 1) * res * res + y * res + (z + 1),
                            settings.shearCompliance
                        );

                        // YZ face diagonals
                        AddConstraint(
                            x * res * res + y * res + z,
                            x * res * res + (y + 1) * res + (z + 1),
                            settings.shearCompliance
                        );
                    }
                }
            }
        }
        private void GenerateBendConstraints()
        {
            var res = settings.resolution;
    
            // Long-range constraints for volume preservation
            for (var x = 0; x < res - 1; x++)
            {
                for (var y = 0; y < res - 1; y++)
                {
                    for (var z = 0; z < res - 1; z++)
                    {
                        // Cube diagonals
                        AddConstraint(
                            x * res * res + y * res + z,
                            (x + 1) * res * res + (y + 1) * res + (z + 1),
                            settings.bendCompliance
                        );
                    }
                }
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
                // Try graph clustering
                var clusters = GraphClusterer.CreateClusters(_constraints, _particles.Count);
                GraphClusterer.ColourClusters(clusters, _constraints);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Graph clustering failed: {e.Message}, using naive colouring");
                ApplyNaiveGraphColouring();
            }
        }
        
        private void ApplyNaiveGraphColouring()
        {
            Debug.Log($"Applying naive graph colouring to {_constraints.Count} constraints...");

            // Simple greedy graph colouring algorithm
            var colouredConstraints = new List<Constraint>();

            for (var i = 0; i < _constraints.Count; i++)
            {
                var constraint = _constraints[i];

                // Find which colours are already used by constraints sharing particles
                var usedcolours = new HashSet<int>();

                for (var j = 0; j < colouredConstraints.Count; j++)
                {
                    var other = colouredConstraints[j];

                    // Check if constraints share particles
                    if (constraint.ParticleA == other.ParticleA || constraint.ParticleA == other.ParticleB ||
                        constraint.ParticleB == other.ParticleA || constraint.ParticleB == other.ParticleB)
                    {
                        usedcolours.Add(other.ColourGroup);
                    }
                }

                // Assign the smallest available colour
                var colour = 0;
                while (usedcolours.Contains(colour))
                {
                    colour++;
                }

                constraint.ColourGroup = colour;
                colouredConstraints.Add(constraint);
            }

            // Update the constraints list
            _constraints = colouredConstraints;

            // Count colours used
            var maxcolour = 0;
            foreach (var constraint in _constraints)
            {
                maxcolour = Mathf.Max(maxcolour, constraint.ColourGroup);
            }

            Debug.Log($"Graph colouring complete: {maxcolour + 1} colour groups needed");
        }

        private void AddConstraint(int a, int b, float compliance)
        {
            var restLength = Vector3.Distance(_particles[a].Position, _particles[b].Position);
    
            if (restLength < 0.001f)
            {
                return;
            }

            var constraint = new Constraint
            {
                ParticleA = a,
                ParticleB = b,
                RestLength = restLength,
                Compliance = compliance, // Scale compliance
                Lambda = 0f,
                ColourGroup = 0
            };

            _constraints.Add(constraint);
        }

        private void GenerateMeshTopology()
        {
            _indices.Clear();
            var res = settings.resolution;

            // Generate surface triangles (simplified cube faces)
            for (var x = 0; x < res - 1; x++)
            {
                for (var y = 0; y < res - 1; y++)
                {
                    for (var z = 0; z < res - 1; z++)
                    {
                        // Only render surface faces
                        if (x == 0 || x == res - 2 || y == 0 || y == res - 2 || z == 0 || z == res - 2)
                        {
                            AddCubeFace(x, y, z, res);
                        }
                    }
                }
            }
        }

        private void AddCubeFace(int x, int y, int z, int res)
        {
            var i000 = x * res * res + y * res + z;
            var i001 = x * res * res + y * res + (z + 1);
            var i010 = x * res * res + (y + 1) * res + z;
            var i011 = x * res * res + (y + 1) * res + (z + 1);
            var i100 = (x + 1) * res * res + y * res + z;
            var i101 = (x + 1) * res * res + y * res + (z + 1);
            var i110 = (x + 1) * res * res + (y + 1) * res + z;
            var i111 = (x + 1) * res * res + (y + 1) * res + (z + 1);

            // Add triangles for visible faces
            if (x == 0) AddQuad(i000, i010, i011, i001); // Left face
            if (x == res - 2) AddQuad(i100, i101, i111, i110); // Right face
            if (y == 0) AddQuad(i000, i001, i101, i100); // Bottom face
            if (y == res - 2) AddQuad(i010, i110, i111, i011); // Top face
            if (z == 0) AddQuad(i000, i100, i110, i010); // Front face
            if (z == res - 2) AddQuad(i001, i011, i111, i101); // Back face
        }

        private void AddQuad(int a, int b, int c, int d)
        {
            // Triangle 1 (counter-clockwise for correct normals)
            _indices.Add(a);
            _indices.Add(c);
            _indices.Add(b);

            // Triangle 2 (counter-clockwise for correct normals)
            _indices.Add(a);
            _indices.Add(d);
            _indices.Add(c);
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
            
            var targetDeltaTime = 1f / 120f; // 120 Hz physics
            var frameTime = Time.deltaTime;

            // Subdivide large frames into small steps
            var substeps = Mathf.CeilToInt(frameTime / targetDeltaTime);
            substeps = Mathf.Clamp(substeps, 1, 10); // Max 10 substeps per frame

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
            UpdateColliders();
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
        }
        

        private void SetComputeShaderParameters(float deltaTime)
        {
            var floorY = FindFloorLevel();
            computeShader.SetFloat(Constants.DeltaTime, deltaTime);
            computeShader.SetFloat(Constants.Gravity, settings.gravity);
            computeShader.SetFloat(Constants.Damping, settings.damping);
            computeShader.SetVector(Constants.WorldPosition, transform.position);
            computeShader.SetInt(Constants.ParticleCount, _particles.Count);
            computeShader.SetInt(Constants.ConstraintCount, _constraints.Count);
            computeShader.SetFloat(Constants.LambdaDecay, settings.lambdaDecay);
            computeShader.SetInt(Constants.VolumeConstraintCount, _volumeConstraints.Count);
            
            computeShader.SetFloat(Constants.CollisionCompliance, settings.collisionCompliance);
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
            // foreach (var sphereCollider in FindObjectsOfType<SphereCollider>())
            // {
            //     if (_colliders.Count >= 64) break; // Don't exceed buffer capacity
            //     var sphere = SDFCollider.CreateSphere(sphereCollider.transform.position, sphereCollider.radius * sphereCollider.transform.lossyScale.x);
            //     _colliders.Add(sphere);
            // }

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

        private void GenerateVolumeConstraints()
        {
            _volumeConstraints = new List<VolumeConstraint>();
            var res = settings.resolution;
            
            var particleVolumeCount = new int[_particles.Count];
            var compliance = settings.volumeCompliance;

            for (var x = 0; x < res - 1; x++)
            {
                for (var y = 0; y < res - 1; y++)
                {
                    for (var z = 0; z < res - 1; z++)
                    {
                        // Get the 8 corners of the cube cell
                        var p000 = (x * res * res) + (y * res) + z;
                        var p100 = ((x + 1) * res * res) + (y * res) + z;
                        var p110 = ((x + 1) * res * res) + ((y + 1) * res) + z;
                        var p010 = (x * res * res) + ((y + 1) * res) + z;
                        var p001 = (x * res * res) + (y * res) + (z + 1);
                        var p101 = ((x + 1) * res * res) + (y * res) + (z + 1);
                        var p111 = ((x + 1) * res * res) + ((y + 1) * res) + (z + 1);
                        var p011 = (x * res * res) + ((y + 1) * res) + (z + 1);

                        // Decompose the cube into 5 tetrahedra
                        AddTetrahedron(p000, p100, p110, p001, compliance);
                        AddTetrahedron(p010, p110, p000, p011, compliance);
                        AddTetrahedron(p101, p001, p111, p100, compliance);
                        AddTetrahedron(p011, p111, p001, p110, compliance);
                        AddTetrahedron(p001, p110, p111, p011, compliance);
                    }
                }
            }
            
            Debug.Log($"Generated {_volumeConstraints.Count} volume constraints from mesh");
        }

        private void AddTetrahedron(int p1, int p2, int p3, int p4, float compliance)
        {
            var pos1 = _particles[p1].Position;
            var pos2 = _particles[p2].Position;
            var pos3 = _particles[p3].Position;
            var pos4 = _particles[p4].Position;

            // The signed volume of a tetrahedron is 1/6 * | (a-d) . ((b-d) x (c-d)) |
            var restVolume = Vector3.Dot(pos1 - pos4, Vector3.Cross(pos2 - pos4, pos3 - pos4)) / 6.0f;

            // We only want to resist compression, so only create constraints for positive volumes
            if (restVolume > 0.001f)
            {
                _volumeConstraints.Add(new VolumeConstraint
                {
                    P1 = p1, P2 = p2, P3 = p3, P4 = p4,
                    RestVolume = restVolume,
                    Compliance = compliance,
                    Lambda = 0
                });
            }
        }

        // CPU fallback for testing and debugging
       
        /*
        private void UpdateCPU()
        {
            var deltaTime = Mathf.Min(Time.deltaTime, 0.02f);
            var floorY = FindFloorLevel();

            // Debug first particle before physics
            if (Time.frameCount % 30 == 0)
            {
                Debug.Log(
                    $"Before physics - First particle: pos={_particles[0].Position}, vel={_particles[0].Velocity}, invMass={_particles[0].InvMass}");
                Debug.Log($"Transform position: {transform.position}, Floor level: {floorY}");
            }

            // Integrate particles on CPU
            for (var i = 0; i < _particles.Count; i++)
            {
                var p = _particles[i];

                if (p.InvMass <= 0) continue; // Skip pinned particles

                // Apply gravity force
                var gravityForce = Vector3.down * settings.gravity;
                p.Force += gravityForce;

                // Simple physics integration
                var acceleration = p.Force * p.InvMass;
                p.Velocity += acceleration * deltaTime;

                // Apply damping
                p.Velocity *= (1f - settings.damping);

                // Update position
                p.Position += p.Velocity * deltaTime;

                // Floor collision
                if (p.Position.y < floorY)
                {
                    p.Position.y = floorY;
                    if (p.Velocity.y < 0)
                    {
                        p.Velocity.y = -p.Velocity.y * 0.3f; // Bounce
                    }

                    // Friction
                    p.Velocity.x *= 0.9f;
                    p.Velocity.z *= 0.9f;
                }

                // Reset forces for next frame
                p.Force = Vector3.zero;
                _particles[i] = p;
            }

            // Simple constraint solving (distance constraints)
            for (var iter = 0; iter < settings.solverIterations; iter++)
            {
                for (var i = 0; i < _constraints.Count; i++)
                {
                    var constraint = _constraints[i];
                    var pA = _particles[constraint.ParticleA];
                    var pB = _particles[constraint.ParticleB];

                    var delta = pB.Position - pA.Position;
                    var currentLength = delta.magnitude;

                    if (currentLength > 0.001f) // Avoid division by zero
                    {
                        var direction = delta / currentLength;
                        var violation = currentLength - constraint.RestLength;

                        var totalInvMass = pA.InvMass + pB.InvMass;
                        if (totalInvMass > 0)
                        {
                            var constraintMass = 1f / totalInvMass;
                            var lambda = -violation * constraintMass * settings.stiffness * 0.01f;

                            var correction = lambda * direction;

                            if (pA.InvMass > 0)
                            {
                                pA.Position -= correction * pA.InvMass;
                                _particles[constraint.ParticleA] = pA;
                            }

                            if (pB.InvMass > 0)
                            {
                                pB.Position += correction * pB.InvMass;
                                _particles[constraint.ParticleB] = pB;
                            }
                        }
                    }
                }
            }

            // Update mesh directly
            var vertices = new Vector3[_particles.Count];
            var centerOffset = Vector3.zero;

            // Calculate center of mass for proper positioning
            for (var i = 0; i < _particles.Count; i++)
            {
                centerOffset += _particles[i].Position;
            }

            centerOffset /= _particles.Count;

            // Update transform position to follow center of mass
            transform.position = centerOffset;

            // Convert particle world positions to local mesh coordinates
            for (var i = 0; i < _particles.Count; i++)
            {
                vertices[i] = _particles[i].Position - centerOffset;
            }

            try
            {
                _mesh.vertices = vertices;
                _mesh.RecalculateNormals();
                _mesh.RecalculateBounds();

                // Force mesh filter to update
                GetComponent<MeshFilter>().mesh = _mesh;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"CPU mesh update failed: {e.Message}");
            }

            // Debug after physics
            if (Time.frameCount % 30 == 0)
            {
                Debug.Log($"After physics - First particle: pos={_particles[0].Position}, vel={_particles[0].Velocity}");
                Debug.Log($"Mesh vertex[0]: {vertices[0]}, Center: {centerOffset}");
            }
        }

        */
        private float FindFloorLevel()
        {
            // Raycast downward to find the floor
            if (Physics.Raycast(transform.position, Vector3.down, out var hit, 100f, settings.collisionLayers))
            {
                return hit.point.y;
            }

            // Default floor level if no collider found
            return -5f;
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
            _particleBuffer?.Release();
            _constraintBuffer?.Release();
            _vertexBuffer?.Release();
            _indexBuffer?.Release();
            _debugBuffer?.Release();
            _volumeConstraintBuffer?.Release();
            _previousPositionsBuffer?.Release();
            _colliderBuffer?.Release();
            if (_mesh != null)
            {
                Destroy(_mesh);
            }
            
        }

        private void OnValidate()
        {
            // Regenerate mesh when settings change in editor
            if (Application.isPlaying && _particles != null)
            {
                GenerateMesh();
                SetupBuffers();
                ResetToInitialPositions();
            }
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

        // Test method - call this to verify the system is working
        [ContextMenu("Test Physics System")]
        public void TestPhysicsSystem()
        {
            Debug.Log("=== Physics System Test ===");
            Debug.Log($"Compute Shader: {(computeShader != null ? "Assigned" : "NULL")}");
            Debug.Log($"Particle Buffer: {(_particleBuffer != null ? "Valid" : "NULL")}");
            Debug.Log($"Particles Count: {_particles?.Count ?? 0}");
            Debug.Log($"Constraints Count: {_constraints?.Count ?? 0}");
            Debug.Log($"CPU Fallback Mode: {settings.useCPUFallback}");

            if (_particles != null && _particles.Count > 0)
            {
                // Manually modify first particle to test - make it very obvious
                var testParticle = _particles[0];
                var originalPos = testParticle.Position;
                testParticle.Position += Vector3.up * 2.0f; // Move up 2 units
                testParticle.Velocity = Vector3.zero; // Reset velocity
                _particles[0] = testParticle;

                // Update buffer if using GPU
                if (!settings.useCPUFallback && _particleBuffer != null)
                {
                    _particleBuffer.SetData(_particles);
                }

                Debug.Log($"Moved particle 0 from {originalPos} to {testParticle.Position}");
                Debug.Log("Watch for mesh movement - it should fall from the new position!");
            }

            // Force a big downward force on all particles
            AddForce(Vector3.up * 50f, transform.position, 10f);

            // Test mesh update manually in CPU mode
            if (settings.useCPUFallback)
            {
                Debug.Log("CPU mode - manually updating mesh...");
                // UpdateCPU();
            }
        }
        
        [ContextMenu("Test Single Thread Solving")]
        public void TestSingleThreadSolving()
        {
            // Temporarily disable graph colouring
            for (var i = 0; i < _constraints.Count; i++)
            {
                var c = _constraints[i];
                c.ColourGroup = i; // Each constraint gets unique colour
                _constraints[i] = c;
            }
            _constraintBuffer.SetData(_constraints);
    
            Debug.Log($"Testing with {_constraints.Count} sequential colour groups");
           
        }
        
        [ContextMenu("Validate Constraint Data")]
        public void ValidateConstraintData()
        {
            var constraintData = new Constraint[_constraints.Count];
            _constraintBuffer.GetData(constraintData);
    
            var validConstraints = 0;
            for (var i = 0; i < constraintData.Length; i++)
            {
                var c = constraintData[i];
                if (c.ParticleA >= 0 && c.ParticleA < _particles.Count &&
                    c.ParticleB >= 0 && c.ParticleB < _particles.Count &&
                    c.RestLength > 0)
                {
                    validConstraints++;
                }
        
                if (i < 5) // Log first 5 constraints
                {
                    Debug.Log($"Constraint {i}: A={c.ParticleA}, B={c.ParticleB}, " +
                              $"RestLength={c.RestLength}, Compliance={c.Compliance}, " +
                              $"Lambda={c.Lambda}, colourGroup={c.ColourGroup}");
                }
            }
    
            Debug.Log($"Valid constraints: {validConstraints}/{_constraints.Count}");
        }

        [ContextMenu("Reset Lambdas")]
        private void ResetLambdas()
        {
            var constraintData = new Constraint[_constraints.Count];
            _constraintBuffer.GetData(constraintData);

            for (var i = 0; i < constraintData.Length; i++)
            {
                constraintData[i].Lambda = 0f;
            }

            _constraintBuffer.SetData(constraintData);
        }

        [ContextMenu("Simple Two Particle Test")]
        public void SimpleTwoParticleTest()
        {
            // Create just 2 particles
            _particles = new List<Particle>();
            _constraints = new List<Constraint>();

            // Particle 0 at origin (fixed)
            _particles.Add(new Particle
            {
                Position = Vector3.zero,
                Velocity = Vector3.zero,
                Force = Vector3.zero,
                InvMass = 0f // Fixed
            });

            // Particle 1 stretched away
            _particles.Add(new Particle
            {
                Position = new Vector3(2f, 0, 0), // Stretched
                Velocity = Vector3.zero,
                Force = Vector3.zero,
                InvMass = 1f
            });

            // One constraint with rest length 1
            _constraints.Add(new Constraint
            {
                ParticleA = 0,
                ParticleB = 1,
                RestLength = 1f,
                Compliance = 0.0f, // Perfectly stiff
                Lambda = 0f,
                ColourGroup = 0
            });

            SetupBuffers();
            Debug.Log("Simple test setup: 2 particles, 1 constraint");
        }

        #endregion
        
    }
}

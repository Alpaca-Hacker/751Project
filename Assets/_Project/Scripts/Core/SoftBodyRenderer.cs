using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace SoftBody.Scripts.Core
{
    public class SoftBodyRenderer
    {
        private readonly Transform _transform;
        private readonly SoftBodySettings _settings;
        
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        
        private AsyncGPUReadbackRequest _readbackRequest;
        private bool _isReadbackPending;
        private float _lastMeshUpdateTime = 0f;
        
        private const float MIN_MESH_UPDATE_INTERVAL = 0.016f;

        public SoftBodyRenderer(Transform transform, SoftBodySettings settings)
        {
            _transform = transform;
            _settings = settings;
            SetupComponents();
        }

        private void SetupComponents()
        {
            _meshFilter = _transform.GetComponent<MeshFilter>();
            if (_meshFilter == null)
            {
                _meshFilter = _transform.gameObject.AddComponent<MeshFilter>();
            }

            _meshRenderer = _transform.GetComponent<MeshRenderer>();
            if (_meshRenderer == null)
            {
                _meshRenderer = _transform.gameObject.AddComponent<MeshRenderer>();
            }
        }

        public void CreateMesh(SoftBodyData data, Vector2[] uvs, string meshName = null)
        {
            _mesh = new Mesh 
            { 
                name = meshName ?? (_settings.inputMesh?.name != null 
                    ? $"SoftBody_{_settings.inputMesh.name}" 
                    : "SoftBody_Procedural")
            };

            var vertices = new Vector3[data.Particles.Count];
            for (int i = 0; i < data.Particles.Count; i++)
            {
                vertices[i] = _transform.InverseTransformPoint(data.Particles[i].Position);
            }

            _mesh.vertices = vertices;
            _mesh.triangles = data.Indices.ToArray();

            if (uvs != null && uvs.Length == data.Particles.Count)
            {
                _mesh.uv = uvs;
                
                if (_settings.debugMessages)
                {
                    Debug.Log("Applied UVs to soft body mesh");
                }
            }
            else if (uvs != null && _settings.debugMessages)
            {
                Debug.LogWarning($"UV count mismatch: {uvs.Length} UVs vs {data.Particles.Count} particles");
            }

            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            _meshFilter.mesh = _mesh;
        }

        public void SetupMaterial(Material renderMaterial)
        {
            if (renderMaterial != null)
            {
                _meshRenderer.material = renderMaterial;
                
                if (_settings.debugMessages)
                {
                    Debug.Log($"Applied custom material: {renderMaterial.name}");
                }
            }
            else
            {
                var fallbackMaterial = CreateFallbackMaterial();
                _meshRenderer.material = fallbackMaterial;
                
                Debug.LogWarning("No material assigned! Using fallback URP/Lit material. " +
                               "Please assign a material in the SoftBodySettings.");
            }

            SetupLighting();
        }

        public void RequestMeshUpdate(ComputeBuffer vertexBuffer)
        {
            if (_isReadbackPending || vertexBuffer == null)
            {
                return;
            }
    
            // Throttle mesh updates to prevent GPU overload
            if (Time.time - _lastMeshUpdateTime < MIN_MESH_UPDATE_INTERVAL)
            {
                return;
            }
    
            _readbackRequest = AsyncGPUReadback.Request(vertexBuffer);
            _isReadbackPending = true;
            _lastMeshUpdateTime = Time.time;
        }

        public void ProcessMeshUpdate()
        {
            if (!_isReadbackPending || !_readbackRequest.done)
            {
                return;
            }
            
            if (!_meshRenderer.isVisible)
            {
                return;
            }

            _isReadbackPending = false;

            if (_readbackRequest.hasError)
            {
                Debug.LogWarning($"AsyncGPUReadback failed! GPU overload detected. Skipping mesh update.");
        
                // Reset readback state and wait longer before next attempt
                _lastMeshUpdateTime = Time.time + 0.1f; // Wait 100ms before trying again
                return;
            }

            var data = _readbackRequest.GetData<float>();
            UpdateMeshFromGPUData(data);
        }

        public void SetVertexBufferOnMaterial(ComputeBuffer vertexBuffer, Material material)
        {
            if (material != null && vertexBuffer != null)
            {
                material.SetBuffer(Constants.Vertices, vertexBuffer);
            }
        }

        private void UpdateMeshFromGPUData(NativeArray<float> vertexData)
        {
            if (_mesh == null) return;

            var particleCount = vertexData.Length / 3;
            var vertices = new Vector3[particleCount];
            var centerOfMass = Vector3.zero;
            var worldPositions = new Vector3[particleCount];

            // Read and validate world positions
            for (var i = 0; i < particleCount; i++)
            {
                var worldPos = new Vector3(
                    vertexData[i * 3],
                    vertexData[i * 3 + 1],
                    vertexData[i * 3 + 2]
                );

                if (!IsValidPosition(worldPos))
                {
                    Debug.LogWarning($"Invalid GPU data at particle {i}: {worldPos}");
                    return;
                }

                worldPositions[i] = worldPos;
                centerOfMass += worldPos;
            }

            // Calculate center of mass and update transform
            centerOfMass /= particleCount;
            _transform.position = centerOfMass;

            // Convert to local coordinates
            for (var i = 0; i < particleCount; i++)
            {
                vertices[i] = worldPositions[i] - centerOfMass;
            }

            try
            {
                _mesh.vertices = vertices;
                _mesh.RecalculateNormals();
                _mesh.RecalculateTangents();
                _mesh.RecalculateBounds();

                // Force mesh filter update
                _meshFilter.mesh = _mesh;

                // Update mesh collider if present
                var meshCollider = _transform.GetComponent<MeshCollider>();
                if (meshCollider != null)
                {
                    meshCollider.sharedMesh = _mesh;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to update mesh: {e.Message}");
            }
        }

        private Material CreateFallbackMaterial()
        {
            var fallbackMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            fallbackMaterial.color = Color.cyan;
            return fallbackMaterial;
        }

        private void SetupLighting()
        {
            if (_mesh != null)
            {
                _mesh.RecalculateNormals();
                _mesh.RecalculateTangents();
            }
        }

        private bool IsValidPosition(Vector3 pos)
        {
            return !float.IsNaN(pos.x) && !float.IsNaN(pos.y) && !float.IsNaN(pos.z) &&
                   !float.IsInfinity(pos.x) && !float.IsInfinity(pos.y) && !float.IsInfinity(pos.z);
        }

        public void Dispose()
        {
            if (_mesh != null)
            {
                Object.Destroy(_mesh);
                _mesh = null;
            }
        }
    }
}
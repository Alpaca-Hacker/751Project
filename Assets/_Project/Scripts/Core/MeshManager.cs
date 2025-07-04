using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace SoftBody.Scripts.Core
{
    public class MeshManager
    {
        private Mesh _mesh;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private readonly Transform _transform;
        
        private AsyncGPUReadbackRequest _readbackRequest;
        private bool _isReadbackPending;
        
        public MeshManager(Transform transform)
        {
            _transform = transform;
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
        
        public void CreateMesh(SoftBodyData data, Vector2[] uvs)
        {
            _mesh = new Mesh { name = "SoftBody_Dynamic" };
            
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
            }
            
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            
            _meshFilter.mesh = _mesh;
        }
        
        public void RequestMeshUpdate(ComputeBuffer vertexBuffer)
        {
            if (_isReadbackPending) return;
        
            _readbackRequest = AsyncGPUReadback.Request(vertexBuffer);
            _isReadbackPending = true;
        }
        
        public void ProcessReadbackData()
        {
            if (!_isReadbackPending || !_readbackRequest.done) return;
        
            _isReadbackPending = false;
        
            if (_readbackRequest.hasError)
            {
                Debug.LogWarning("GPU readback failed");
                return;
            }
        
            var data = _readbackRequest.GetData<float>();
            UpdateMeshVertices(data);
        }
        
        private void UpdateMeshVertices(NativeArray<float> vertexData)
        {
            int vertexCount = vertexData.Length / 3;
            var vertices = new Vector3[vertexCount];
            var centerOfMass = Vector3.zero;
            
            // Read world positions
            for (int i = 0; i < vertexCount; i++)
            {
                var worldPos = new Vector3(
                    vertexData[i * 3],
                    vertexData[i * 3 + 1],
                    vertexData[i * 3 + 2]
                );
                
                if (!ValidatePosition(worldPos)) return;
                
                centerOfMass += worldPos;
            }
            
            centerOfMass /= vertexCount;
            _transform.position = centerOfMass;
            
            // Convert to local space
            for (int i = 0; i < vertexCount; i++)
            {
                var worldPos = new Vector3(
                    vertexData[i * 3],
                    vertexData[i * 3 + 1],
                    vertexData[i * 3 + 2]
                );
                vertices[i] = worldPos - centerOfMass;
            }
            
            _mesh.vertices = vertices;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
        }
        
        private bool ValidatePosition(Vector3 pos)
        {
            return !float.IsNaN(pos.x) && !float.IsNaN(pos.y) && !float.IsNaN(pos.z) &&
                   !float.IsInfinity(pos.x) && !float.IsInfinity(pos.y) && !float.IsInfinity(pos.z);
        }
        
        public void SetMaterial(Material material)
        {
            if (material != null)
            {
                _meshRenderer.material = material;
            }
            else
            {
                // Fallback material
                var fallback = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                fallback.color = Color.cyan;
                _meshRenderer.material = fallback;
            }
        }
    }
}
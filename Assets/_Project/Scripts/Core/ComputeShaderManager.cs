using System.Collections.Generic;
using UnityEngine;

namespace SoftBody.Scripts.Core
{
    public class ComputeShaderManager
    {
        private readonly ComputeShader _computeShader;
        private readonly Dictionary<string, int> _kernels = new();
        private BufferManager _bufferManager;
        
        // Cache thread group sizes
        private const int THREAD_GROUP_SIZE = 64;
        
        public ComputeShaderManager(ComputeShader computeShader)
        {
            _computeShader = computeShader;
            if (_computeShader == null)
            {
                Debug.LogError("ComputeShader is null in ComputeShaderManager!");
                return;
            }
            
            CacheKernels();
        }
        
        private void CacheKernels()
        {
            // Cache all kernel indices
            _kernels["IntegrateAndStore"] = _computeShader.FindKernel("IntegrateAndStorePositions");
            _kernels["SolveConstraints"] = _computeShader.FindKernel("SolveConstraints");
            _kernels["UpdateMesh"] = _computeShader.FindKernel("UpdateMesh");
            _kernels["DecayLambdas"] = _computeShader.FindKernel("DecayLambdas");
            _kernels["VolumeConstraints"] = _computeShader.FindKernel("SolveVolumeConstraints");
            _kernels["UpdateVelocities"] = _computeShader.FindKernel("UpdateVelocities");
            _kernels["DebugAndValidate"] = _computeShader.FindKernel("DebugAndValidateParticles");
            _kernels["SolveCollisions"] = _computeShader.FindKernel("SolveGeneralCollisions");
            _kernels["ApplyCollisions"] = _computeShader.FindKernel("ApplyCollisionCorrections");
            _kernels["ApplyDamping"] = _computeShader.FindKernel("ApplyGlobalDamping");
            
            // Validate all kernels were found
            foreach (var kvp in _kernels)
            {
                if (kvp.Value == -1)
                {
                    Debug.LogError($"Kernel '{kvp.Key}' not found in compute shader!");
                }
            }
        }
        
        public void SetBufferManager(BufferManager bufferManager)
        {
            _bufferManager = bufferManager;
        }
        
        public void SetGlobalParameters(float deltaTime, SoftBodySettings settings, int particleCount, int constraintCount, int volumeConstraintCount, Vector3 worldPosition)
        {
            _computeShader.SetFloat(Constants.DeltaTime, deltaTime);
            _computeShader.SetFloat(Constants.Gravity, settings.gravity);
            _computeShader.SetFloat(Constants.Damping, settings.damping);
            _computeShader.SetInt(Constants.ParticleCount, particleCount);
            _computeShader.SetInt(Constants.ConstraintCount, constraintCount);
            _computeShader.SetInt(Constants.VolumeConstraintCount, volumeConstraintCount);
            _computeShader.SetFloat(Constants.LambdaDecay, settings.lambdaDecay);
            _computeShader.SetVector(Constants.WorldPosition, worldPosition);
            _computeShader.SetFloat(Constants.CollisionCompliance, 0.0001f);
        }
        
        public void SetColourGroup(int colourGroup)
        {
            _computeShader.SetInt(Constants.CurrentColourGroup, colourGroup);
        }
        
        public void SetColliderCount(int count)
        {
            _computeShader.SetInt(Constants.ColliderCount, count);
        }
        
        public void BindBuffersForKernel(string kernelName)
        {
            if (!_kernels.TryGetValue(kernelName, out int kernel))
            {
                Debug.LogError($"Kernel {kernelName} not found!");
                return;
            }
            
            // Get buffers from manager
            var particleBuffer = _bufferManager.GetBuffer("particles");
            var constraintBuffer = _bufferManager.GetBuffer("constraints");
            var vertexBuffer = _bufferManager.GetBuffer("vertices");
            var previousPositionsBuffer = _bufferManager.GetBuffer("previousPositions");
            var volumeConstraintBuffer = _bufferManager.GetBuffer("volumeConstraints");
            var debugBuffer = _bufferManager.GetBuffer("debug");
            var colliderBuffer = _bufferManager.GetBuffer("colliders");
            var collisionCorrectionsBuffer = _bufferManager.GetBuffer("collisionCorrections");
            
            // Bind buffers based on kernel requirements
            switch (kernelName)
            {
                case "IntegrateAndStore":
                    _computeShader.SetBuffer(kernel, Constants.Particles, particleBuffer);
                    _computeShader.SetBuffer(kernel, Constants.PreviousPositions, previousPositionsBuffer);
                    break;
                    
                case "SolveConstraints":
                    _computeShader.SetBuffer(kernel, Constants.Particles, particleBuffer);
                    _computeShader.SetBuffer(kernel, Constants.Constraints, constraintBuffer);
                    break;
                    
                case "UpdateMesh":
                    _computeShader.SetBuffer(kernel, Constants.Particles, particleBuffer);
                    _computeShader.SetBuffer(kernel, Constants.Vertices, vertexBuffer);
                    break;
                    
                case "DecayLambdas":
                    _computeShader.SetBuffer(kernel, Constants.Constraints, constraintBuffer);
                    break;
                    
                case "VolumeConstraints":
                    _computeShader.SetBuffer(kernel, Constants.Particles, particleBuffer);
                    _computeShader.SetBuffer(kernel, Constants.VolumeConstraints, volumeConstraintBuffer);
                    break;
                    
                case "UpdateVelocities":
                    _computeShader.SetBuffer(kernel, Constants.Particles, particleBuffer);
                    _computeShader.SetBuffer(kernel, Constants.PreviousPositions, previousPositionsBuffer);
                    break;
                    
                case "DebugAndValidate":
                    _computeShader.SetBuffer(kernel, Constants.Particles, particleBuffer);
                    _computeShader.SetBuffer(kernel, Constants.DebugBuffer, debugBuffer);
                    break;
                    
                case "SolveCollisions":
                    _computeShader.SetBuffer(kernel, Constants.Particles, particleBuffer);
                    _computeShader.SetBuffer(kernel, Constants.Colliders, colliderBuffer);
                    _computeShader.SetBuffer(kernel, Constants.CollisionCorrections, collisionCorrectionsBuffer);
                    break;
                    
                case "ApplyCollisions":
                    _computeShader.SetBuffer(kernel, Constants.Particles, particleBuffer);
                    _computeShader.SetBuffer(kernel, Constants.CollisionCorrections, collisionCorrectionsBuffer);
                    _computeShader.SetBuffer(kernel, Constants.PreviousPositions, previousPositionsBuffer);
                    break;
                    
                case "ApplyDamping":
                    _computeShader.SetBuffer(kernel, Constants.Particles, particleBuffer);
                    _computeShader.SetBuffer(kernel, Constants.Colliders, colliderBuffer);
                    break;
            }
        }
        
        // Dispatch methods
        public void DispatchLambdaDecay(int constraintCount)
        {
            if (constraintCount <= 0) return;
            
            BindBuffersForKernel("DecayLambdas");
            int threadGroups = CalculateThreadGroups(constraintCount);
            _computeShader.Dispatch(_kernels["DecayLambdas"], threadGroups, 1, 1);
        }
        
        public void DispatchIntegration(int particleCount)
        {
            if (particleCount <= 0) return;
            
            BindBuffersForKernel("IntegrateAndStore");
            int threadGroups = CalculateThreadGroups(particleCount);
            _computeShader.Dispatch(_kernels["IntegrateAndStore"], threadGroups, 1, 1);
        }
        
        public void DispatchConstraints(int constraintCount)
        {
            if (constraintCount <= 0) return;
            
            BindBuffersForKernel("SolveConstraints");
            int threadGroups = CalculateThreadGroups(constraintCount);
            _computeShader.Dispatch(_kernels["SolveConstraints"], threadGroups, 1, 1);
        }
        
        public void DispatchVolumeConstraints(int volumeConstraintCount)
        {
            if (volumeConstraintCount <= 0) return;
            
            BindBuffersForKernel("VolumeConstraints");
            int threadGroups = CalculateThreadGroups(volumeConstraintCount);
            _computeShader.Dispatch(_kernels["VolumeConstraints"], threadGroups, 1, 1);
        }
        
        public void DispatchCollisionDetection(int particleCount)
        {
            if (particleCount <= 0) return;
            
            BindBuffersForKernel("SolveCollisions");
            int threadGroups = CalculateThreadGroups(particleCount);
            _computeShader.Dispatch(_kernels["SolveCollisions"], threadGroups, 1, 1);
        }
        
        public void DispatchCollisionResponse(int particleCount)
        {
            if (particleCount <= 0) return;
            
            BindBuffersForKernel("ApplyCollisions");
            int threadGroups = CalculateThreadGroups(particleCount);
            _computeShader.Dispatch(_kernels["ApplyCollisions"], threadGroups, 1, 1);
        }
        
        public void DispatchVelocityUpdate(int particleCount)
        {
            if (particleCount <= 0) return;
            
            BindBuffersForKernel("UpdateVelocities");
            int threadGroups = CalculateThreadGroups(particleCount);
            _computeShader.Dispatch(_kernels["UpdateVelocities"], threadGroups, 1, 1);
        }
        
        public void DispatchGlobalDamping(int particleCount)
        {
            if (particleCount <= 0) return;
            
            BindBuffersForKernel("ApplyDamping");
            int threadGroups = CalculateThreadGroups(particleCount);
            _computeShader.Dispatch(_kernels["ApplyDamping"], threadGroups, 1, 1);
        }
        
        public void DispatchMeshUpdate(int particleCount)
        {
            if (particleCount <= 0) return;
            
            BindBuffersForKernel("UpdateMesh");
            int threadGroups = CalculateThreadGroups(particleCount);
            _computeShader.Dispatch(_kernels["UpdateMesh"], threadGroups, 1, 1);
        }
        
        public void DispatchDebugValidation()
        {
            BindBuffersForKernel("DebugAndValidate");
            _computeShader.Dispatch(_kernels["DebugAndValidate"], 1, 1, 1);
        }
        
        // Debug data retrieval
        public float[] GetDebugData()
        {
            var debugBuffer = _bufferManager.GetBuffer("debug");
            if (debugBuffer == null) return new float[4];
            
            var debugData = new float[4];
            debugBuffer.GetData(debugData);
            return debugData;
        }
        
        public void ValidateDebugData(bool debugMode)
        {
            if (!debugMode) return;
            
            DispatchDebugValidation();
            
            var debugData = GetDebugData();
            if (debugData[0] > 0 || debugData[1] > 0) // NaN or Inf detected
            {
                Debug.LogError($"INSTABILITY DETECTED! NaN Count: {debugData[0]}, " +
                             $"Inf Count: {debugData[1]}, Max Speed: {debugData[2]:F2}, " +
                             $"First Bad Particle Index: {debugData[3]}");
            }
        }
        
        // Helper methods
        private int CalculateThreadGroups(int elementCount)
        {
            return Mathf.CeilToInt((float)elementCount / THREAD_GROUP_SIZE);
        }
        
        public void LogThreadEfficiency(string systemName, int actualCount, int threadGroups)
        {
            int dispatchedThreads = threadGroups * THREAD_GROUP_SIZE;
            float efficiency = (float)actualCount / dispatchedThreads * 100f;
            
            if (Time.frameCount % 300 == 0) // Log every 5 seconds at 60fps
            {
                Debug.Log($"{systemName} Thread Efficiency: {actualCount} elements, " +
                         $"{dispatchedThreads} threads ({efficiency:F1}% efficient)");
            }
        }
    }
}
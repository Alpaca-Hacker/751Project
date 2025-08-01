using System.Collections.Generic;
using UnityEngine;
using SoftBody.Scripts.Models;

namespace SoftBody.Scripts.Core
{
    public class SoftBodySimulation
    {
        private ComputeShaderManager _computeManager;
        private BufferManager _bufferManager;
        private readonly SoftBodySettings _settings;
        
        private List<Particle> _particles;
        private List<Constraint> _constraints;
        private List<VolumeConstraint> _volumeConstraints;
        private Vector3 _worldPosition;
        
        public int ParticleCount => _particles?.Count ?? 0;
        public int ConstraintCount => _constraints?.Count ?? 0;
        public int VolumeConstraintCount => _volumeConstraints?.Count ?? 0;
        public float MemoryUsageMB => _bufferManager.GetTotalMemoryUsageMB();
        
        public SoftBodySimulation(SoftBodySettings settings, ComputeShaderManager computeManager, BufferManager bufferManager)
        {
            _settings = settings;
            _computeManager = computeManager;
            _bufferManager = bufferManager;
        }
        
        public void Initialize(SoftBodyData data, Vector3 initialWorldPosition)
        {
            _particles = data.Particles;
            _constraints = data.Constraints;
            _volumeConstraints = data.VolumeConstraints;
            _worldPosition = initialWorldPosition;
            
            CreateBuffers();
            UploadInitialData();
        }
        

        public void SimulateStep(float deltaTime, int maxColourGroup, bool isLastSubstep)
        {
            if (!ValidateSimulation()) return;

            _computeManager.SetGlobalParameters(
                deltaTime, _settings, ParticleCount, ConstraintCount, VolumeConstraintCount, _worldPosition
            );

            _computeManager.DispatchLambdaDecay(ConstraintCount);
            _computeManager.DispatchIntegration(ParticleCount);
            _computeManager.BindBuffersForConstraintSolving();

            for (var iter = 0; iter < _settings.solverIterations; iter++)
            {
                for (var colourGroup = 0; colourGroup <= maxColourGroup; colourGroup++)
                {
                    _computeManager.SetColourGroup(colourGroup);
                    _computeManager.DispatchConstraintsWithoutBinding(ConstraintCount);

                    if (VolumeConstraintCount > 0 && colourGroup == 0)
                    {
                        _computeManager.DispatchVolumeConstraints(VolumeConstraintCount);
                    }
                }
            }
            
            if (_settings.enableCollision || _settings.enableSoftBodyCollisions)
            {
                _computeManager.DispatchCollisionDetection(ParticleCount);
                _computeManager.DispatchCollisionResponse(ParticleCount);
            }

            _computeManager.DispatchVelocityUpdate(ParticleCount);
            _computeManager.DispatchGlobalDamping(ParticleCount);

            if (isLastSubstep)
            {
                _computeManager.DispatchMeshUpdate(ParticleCount);
            }

            // Always run debug validation
            _computeManager.DispatchDebugValidation();
    
            if (_settings.debugMode && Time.frameCount % 10 == 0)
            {
                _computeManager.ValidateDebugData(_settings.debugMode);
            }
        }

        public void UpdateWorldPosition(Vector3 newPosition)
        {
            _worldPosition = newPosition;
        }
        
        public ComputeBuffer GetVertexBuffer()
        {
            return _bufferManager.GetBuffer("vertices");
        }
        
        public void SetParticleData(List<Particle> particles)
        {
            _particles = particles;
            _bufferManager.SetData("particles", particles);
        }
        
        public void GetParticleData(Particle[] outputArray)
        {
            _bufferManager.GetData("particles", outputArray);
        }
        
        public void ApplyImpulse(Vector3 worldPosition, Vector3 impulse, float radius)
        {
            // Get current particle data
            var currentParticles = new Particle[_particles.Count];
            GetParticleData(currentParticles);
            
            var affectedParticles = new List<int>();
            var radiusSq = radius * radius;
            
            // Find particles within radius
            for (var i = 0; i < currentParticles.Length; i++)
            {
                var distSq = Vector3.SqrMagnitude(currentParticles[i].Position - worldPosition);
                if (distSq <= radiusSq && currentParticles[i].InvMass > 0)
                {
                    affectedParticles.Add(i);
                }
            }
            
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
            
            // Upload modified particles back
            _bufferManager.SetData("particles", currentParticles);
        }
        
        public void SetWorldPosition(Vector3 newPosition)
        {
            var currentParticles = new Particle[_particles.Count];
            GetParticleData(currentParticles);
            
            // Calculate current center of mass
            var currentCenter = Vector3.zero;
            foreach (var particle in currentParticles)
            {
                currentCenter += particle.Position;
            }
            currentCenter /= currentParticles.Length;
            
            // Calculate offset
            var offset = newPosition - currentCenter;
            
            // Apply offset to all particles
            for (var i = 0; i < currentParticles.Length; i++)
            {
                var p = currentParticles[i];
                p.Position += offset;
                currentParticles[i] = p;
            }
            
            // Update data
            _particles = new List<Particle>(currentParticles);
            _bufferManager.SetData("particles", currentParticles);
            _worldPosition = newPosition;
        }
        
        public void ResetVelocities()
        {
            var currentParticles = new Particle[ParticleCount];
            GetParticleData(currentParticles);

            for (var i = 0; i < currentParticles.Length; i++)
            {
                currentParticles[i].Velocity = Vector4.zero;
            }

            SetParticleData(new List<Particle>(currentParticles));
        }

        public void ApplyContinuousForce(Vector3 worldPosition, Vector3 force, float radius)
        {
            var currentParticles = new Particle[ParticleCount];
            GetParticleData(currentParticles);

            var radiusSq = radius * radius;

            for (var i = 0; i < currentParticles.Length; i++)
            {
                if (currentParticles[i].InvMass <= 0) continue;

                var distSq = Vector3.SqrMagnitude(currentParticles[i].Position - worldPosition);
                if (distSq <= radiusSq)
                {
                    var p = currentParticles[i];
                    var distance = Mathf.Sqrt(distSq);
                    var falloff = 1f - (distance / radius);

                    var deltaVelocity = force * (falloff * p.InvMass * Time.deltaTime);
                    p.Velocity.x += deltaVelocity.x;
                    p.Velocity.y += deltaVelocity.y;
                    p.Velocity.z += deltaVelocity.z;
                
                    currentParticles[i] = p;
                }
            }
        
            _bufferManager.SetData("particles", currentParticles);
        }
        
        private void CreateBuffers()
        {
            _bufferManager.CreateBuffer<Particle>("particles", _particles.Count);
            _bufferManager.CreateBuffer<Constraint>("constraints", _constraints.Count);
            _bufferManager.CreateBuffer<VolumeConstraint>("volumeConstraints", 
                Mathf.Max(1, _volumeConstraints.Count));
            _bufferManager.CreateBuffer<Vector3>("vertices", _particles.Count);
            _bufferManager.CreateBuffer<Vector3>("previousPositions", _particles.Count);
            _bufferManager.CreateBuffer<float>("debug", 4); // 4 floats for debug data
            _bufferManager.CreateBuffer<SDFCollider>("colliders", 64); // Max 64 colliders
            _bufferManager.CreateBuffer<Vector3>("collisionCorrections", _particles.Count);
        }
        
        private void UploadInitialData()
        {
            _bufferManager.SetData("particles", _particles);
            _bufferManager.SetData("constraints", _constraints);
            if (_volumeConstraints.Count > 0)
            {
                _bufferManager.SetData("volumeConstraints", _volumeConstraints);
            }
            
            // Initialize debug buffer
            _bufferManager.SetData("debug", new float[] { 0, 0, 0, -1 });
        }
        
        private bool ValidateSimulation()
        {
            return _particles != null && _particles.Count > 0 && 
                   _constraints != null && _constraints.Count > 0 &&
                   _bufferManager != null && _computeManager != null;
        }
        
        public void Dispose()
        {
            _bufferManager?.Dispose();
        }
    }
}

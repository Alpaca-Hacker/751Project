using UnityEngine;

namespace SoftBody.Scripts.Performance
{
    public class ManagedSoftBody
    {
        public SoftBodyPhysics SoftBody { get; private set; }
        public float DistanceToCamera { get; private set; }
        public bool IsVisible { get; private set; }
        public PerformanceQuality CurrentQuality { get; private set; } = PerformanceQuality.High;
        
        private readonly SoftBodyPerformanceManager _manager;
        private MeshRenderer _renderer;
        private int _lastMeshUpdateFrame = 0;
        private int _lastCollisionUpdateFrame = 0;
        
        public ManagedSoftBody(SoftBodyPhysics softBody, SoftBodyPerformanceManager manager)
        {
            SoftBody = softBody;
            _manager = manager;
            _renderer = softBody.GetComponent<MeshRenderer>();
        }
        
        public void UpdateCameraDistance(Camera camera)
        {
            if (camera == null || SoftBody == null) return;
            
            DistanceToCamera = Vector3.Distance(SoftBody.transform.position, camera.transform.position);
            IsVisible = _renderer != null && _renderer.isVisible;
        }

        public void ApplyQualityLevel(PerformanceQuality quality)
        {
            if (CurrentQuality == quality) return;

            CurrentQuality = quality;

            switch (quality)
            {
                case PerformanceQuality.Disabled:
                    SoftBody.settings.SkipUpdate = true;
                    SoftBody.gameObject.SetActive(false);
                    break;

                case PerformanceQuality.Low:
                    SoftBody.gameObject.SetActive(true);
                    SoftBody.settings.SkipUpdate = false;
                    SoftBody.settings.solverIterations = 1;
                    SoftBody.settings.maxStuffingParticles = 5;
                    SoftBody.settings.damping = 0.3f;
                    // DISABLE soft body collisions for low quality
                    SoftBody.settings.enableSoftBodyCollisions = false;
                    SoftBody.settings.enableCollision = true; // Keep environment collisions
                    break;

                case PerformanceQuality.Medium:
                    SoftBody.gameObject.SetActive(true);
                    SoftBody.settings.SkipUpdate = false;
                    SoftBody.settings.solverIterations = _manager.reducedQualitySolverIterations;
                    SoftBody.settings.maxStuffingParticles = _manager.reducedQualityMaxParticles;
                    SoftBody.settings.damping = 0.25f;
                    // LIMITED soft body collisions for medium quality
                    SoftBody.settings.enableSoftBodyCollisions = false;
                    SoftBody.settings.maxInteractionDistance = 3f; // Reduced range
                    break;

                case PerformanceQuality.High:
                    SoftBody.gameObject.SetActive(true);
                    SoftBody.settings.SkipUpdate = false;
                    SoftBody.settings.solverIterations = _manager.fullQualitySolverIterations;
                    SoftBody.settings.maxStuffingParticles = _manager.fullQualityMaxParticles;
                    SoftBody.settings.damping = 0.02f;
                    // FULL soft body collisions for high quality
                    SoftBody.settings.enableSoftBodyCollisions = false;
                    SoftBody.settings.maxInteractionDistance = 8f;
                    break;
            }
        }

        public bool ShouldUpdateMesh(int currentFrame, float baseInterval)
        {
            if (!IsVisible || CurrentQuality == PerformanceQuality.Disabled) return false;
            
            // Calculate frame interval based on quality and distance
            var intervalMultiplier = CurrentQuality switch
            {
                PerformanceQuality.High => 1f,
                PerformanceQuality.Medium => 2f,
                PerformanceQuality.Low => 4f,
                _ => float.MaxValue
            };
            
            var frameInterval = Mathf.RoundToInt(baseInterval * 60f * intervalMultiplier);
            
            if (currentFrame - _lastMeshUpdateFrame >= frameInterval)
            {
                _lastMeshUpdateFrame = currentFrame;
                return true;
            }
            
            return false;
        }
        
        public bool ShouldUpdateCollisions(int currentFrame, float baseInterval)
        {
            if (CurrentQuality == PerformanceQuality.Disabled) return false;
            
            var intervalMultiplier = CurrentQuality switch
            {
                PerformanceQuality.High => 1f,
                PerformanceQuality.Medium => 1.5f,
                PerformanceQuality.Low => 3f,
                _ => float.MaxValue
            };
            
            var frameInterval = Mathf.RoundToInt(baseInterval * 60f * intervalMultiplier);
            
            if (currentFrame - _lastCollisionUpdateFrame >= frameInterval)
            {
                _lastCollisionUpdateFrame = currentFrame;
                return true;
            }
            
            return false;
        }
    }
}
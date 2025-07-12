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
                   //SoftBody.gameObject.SetActive(false);
                    break;
            
                case PerformanceQuality.Low:
                    SoftBody.gameObject.SetActive(true);
                    SoftBody.settings.SkipUpdate = false;
                    SoftBody.settings.solverIterations = 1;
                    SoftBody.settings.maxStuffingParticles = 3;
                    SoftBody.settings.damping = 0.3f; // Increased damping to reduce bouncing
                    SoftBody.settings.enableSoftBodyCollisions = false;
                    SoftBody.settings.enableCollision = true;
                    break;
            
                case PerformanceQuality.Medium:
                    SoftBody.gameObject.SetActive(true);
                    SoftBody.settings.SkipUpdate = false;
                    SoftBody.settings.solverIterations = 1;
                    SoftBody.settings.maxStuffingParticles = 5;
                    SoftBody.settings.damping = 0.25f; // Increased damping
                    SoftBody.settings.enableSoftBodyCollisions = true;
                    SoftBody.settings.enableCollision = true;
                    break;
            
                case PerformanceQuality.High:
                    SoftBody.gameObject.SetActive(true);
                    SoftBody.settings.SkipUpdate = false;
                    SoftBody.settings.solverIterations = 2;
                    SoftBody.settings.maxStuffingParticles = 8;
                    SoftBody.settings.damping = 0.2f; // Increased damping
                    SoftBody.settings.enableSoftBodyCollisions = true;
                    SoftBody.settings.enableCollision = true;
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
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SoftBody.Scripts.Performance
{
    public class SoftBodyPerformanceManager : MonoBehaviour
    {
        [Header("Performance Thresholds")]
        public int maxFullQualityToys = 3;
        public int maxReducedQualityToys = 6;
        public int maxActiveToys = 8;
        
        [Header("Distance-Based Quality")]
        public float highQualityDistance = 8f;
        public float mediumQualityDistance = 15f;
        public float cullingDistance = 25f;
        
        [Header("Quality Settings")]
        public int fullQualitySolverIterations = 2;
        public int reducedQualitySolverIterations = 1;
        public int fullQualityMaxParticles = 15;
        public int reducedQualityMaxParticles = 8;
        
        [Header("Performance Intervals")]
        public float performanceCheckInterval = 0.2f;
        public float meshUpdateBaseInterval = 0.033f; // 30fps
        public float collisionUpdateInterval = 0.1f;
        
        private readonly List<ManagedSoftBody> _managedSoftBodies = new();
        private Camera _mainCamera;
        private int _lastActiveCount = -1;
        private float _updateTimer;
        private int _frameCounter;
        
        public static SoftBodyPerformanceManager Instance { get; private set; }
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }
        
        private void Start()
        {
            _mainCamera = Camera.main;
        }
        
        private void Update()
        {
            _frameCounter++;
            _updateTimer += Time.deltaTime;
            
            // Only check every interval to avoid performance hit
            if (_updateTimer < performanceCheckInterval)
            {
                return;
            }
            _updateTimer = 0f;
            
            // Clean up null references
            _managedSoftBodies.RemoveAll(msb => msb?.SoftBody == null);
            
            // Update distances and visibility for all managed bodies
            foreach (var managed in _managedSoftBodies)
            {
                managed.UpdateCameraDistance(_mainCamera);
            }
            
            // Sort by distance for priority-based management
            _managedSoftBodies.Sort((a, b) => a.DistanceToCamera.CompareTo(b.DistanceToCamera));
            
            // Count active toys and update quality
            var activeCount = UpdateQualityBasedOnDistanceAndLimits();
            
            // Only log if count changed significantly
            if (Mathf.Abs(activeCount - _lastActiveCount) > 1)
            {
                Debug.Log($"Performance Manager: {activeCount} active toys, camera distance sorting applied");
                _lastActiveCount = activeCount;
            }
        }
        
        public void RegisterSoftBody(SoftBodyPhysics softBody)
        {
            if (_managedSoftBodies.Any(msb => msb.SoftBody == softBody))
                return;
                
            var managed = new ManagedSoftBody(softBody, this);
            _managedSoftBodies.Add(managed);
        }
        
        public void UnregisterSoftBody(SoftBodyPhysics softBody)
        {
            _managedSoftBodies.RemoveAll(msb => msb.SoftBody == softBody);
        }
        
        private int UpdateQualityBasedOnDistanceAndLimits()
        {
            var activeCount = 0;
            var highQualityCount = 0;
            var mediumQualityCount = 0;
            
            foreach (var managed in _managedSoftBodies)
            {
                if (managed.SoftBody == null || !managed.SoftBody.gameObject) continue;
                
                var quality = DetermineOptimalQuality(managed, activeCount, highQualityCount, mediumQualityCount);
                managed.ApplyQualityLevel(quality);
                
                // Count active objects
                if (quality != PerformanceQuality.Disabled)
                {
                    activeCount++;
                    if (quality == PerformanceQuality.High) highQualityCount++;
                    else if (quality == PerformanceQuality.Medium) mediumQualityCount++;
                }
            }
            
            return activeCount;
        }
        
        private PerformanceQuality DetermineOptimalQuality(ManagedSoftBody managed, int activeCount, int highCount, int mediumCount)
        {
            // Hard distance culling
            if (managed.DistanceToCamera > cullingDistance || !managed.IsVisible)
                return PerformanceQuality.Disabled;
            
            // Global active limit
            if (activeCount >= maxActiveToys)
                return PerformanceQuality.Disabled;
            
            // High quality assignment
            if (managed.DistanceToCamera <= highQualityDistance && highCount < maxFullQualityToys)
                return PerformanceQuality.High;
            
            // Medium quality assignment  
            if (managed.DistanceToCamera <= mediumQualityDistance && mediumCount < maxReducedQualityToys)
                return PerformanceQuality.Medium;
            
            // Low quality for everything else within culling distance
            return PerformanceQuality.Low;
        }
        
        // Public API for other systems to check update permissions
        public bool ShouldUpdateMesh(SoftBodyPhysics softBody)
        {
            var managed = _managedSoftBodies.FirstOrDefault(m => m.SoftBody == softBody);
            if (managed == null) return true; // Default to true if not managed
            
            return managed.ShouldUpdateMesh(_frameCounter, meshUpdateBaseInterval);
        }
        
        public bool ShouldUpdateCollisions(SoftBodyPhysics softBody)
        {
            var managed = _managedSoftBodies.FirstOrDefault(m => m.SoftBody == softBody);
            if (managed == null) return true;
            
            return managed.ShouldUpdateCollisions(_frameCounter, collisionUpdateInterval);
        }
        
        // Legacy methods for compatibility
        private void ApplyFullSettings(SoftBodyPhysics softBody)
        {
            softBody.settings.skipUpdate = false;
            softBody.settings.solverIterations = fullQualitySolverIterations;
            softBody.settings.maxStuffingParticles = fullQualityMaxParticles;
            softBody.settings.damping = 0.01f;
        }
        
        private void ApplyReducedSettings(SoftBodyPhysics softBody)
        {
            softBody.settings.skipUpdate = false;
            softBody.settings.solverIterations = reducedQualitySolverIterations;
            softBody.settings.maxStuffingParticles = reducedQualityMaxParticles;
            softBody.settings.damping = 0.05f;
        }
        
        private void ApplyMinimalSettings(SoftBodyPhysics softBody)
        {
            softBody.settings.skipUpdate = false;
            softBody.settings.solverIterations = 1;
            softBody.settings.maxStuffingParticles = 5;
            softBody.settings.damping = 0.10f;
        }
    }
}
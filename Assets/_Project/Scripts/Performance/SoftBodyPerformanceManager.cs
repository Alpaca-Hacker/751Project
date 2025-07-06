using System.Collections.Generic;
using UnityEngine;

namespace SoftBody.Scripts.Performance
{
    public class SoftBodyPerformanceManager : MonoBehaviour
    {
        [Header("Performance Thresholds")]
        public int maxFullQualityToys = 3;
        public int maxReducedQualityToys = 6;
        public int maxActiveToys = 8;
        
        [Header("Quality Settings")]
        public int fullQualitySolverIterations = 2;
        public int reducedQualitySolverIterations = 1;
        public int fullQualityMaxParticles = 15;
        public int reducedQualityMaxParticles = 8;
        
        private readonly List<SoftBodyPhysics> _managedSoftBodies = new();
        private int _lastActiveCount = -1;
        private float _updateTimer = 0f;
        
        private void Update()
        {
            _updateTimer += Time.deltaTime;
            
            // Only check every 0.2 seconds to avoid performance hit
            if (_updateTimer < 0.2f)
            {
                return;
            }
            _updateTimer = 0f;
            
            // Clean up null references
            _managedSoftBodies.RemoveAll(sb => !sb);
            
            // Count active toys
            int activeCount = 0;
            foreach (var sb in _managedSoftBodies)
            {
                if (sb.gameObject.activeInHierarchy && !sb.settings.SkipUpdate)
                    activeCount++;
            }
            
            // Only update settings if count changed
            if (activeCount != _lastActiveCount)
            {
                UpdateQualitySettings(activeCount);
                _lastActiveCount = activeCount;
            }
        }
        
        public void RegisterSoftBody(SoftBodyPhysics softBody)
        {
            if (!_managedSoftBodies.Contains(softBody))
            {
                _managedSoftBodies.Add(softBody);
            }
        }
        
        public void UnregisterSoftBody(SoftBodyPhysics softBody)
        {
            _managedSoftBodies.Remove(softBody);
        }
        
        private void UpdateQualitySettings(int activeCount)
        {
            for (var i = 0; i < _managedSoftBodies.Count; i++)
            {
                var softBody = _managedSoftBodies[i];
                if (softBody == null || !softBody.gameObject.activeInHierarchy) continue;
                
                if (activeCount > maxActiveToys)
                {
                    // Disable oldest toys beyond limit
                    if (i >= maxActiveToys)
                    {
                        softBody.settings.SkipUpdate = true;
                    }
                }
                else if (activeCount > maxReducedQualityToys)
                {
                    // All toys use minimal settings
                    ApplyMinimalSettings(softBody);
                }
                else if (activeCount > maxFullQualityToys)
                {
                    // Use reduced settings for all
                    ApplyReducedSettings(softBody);
                }
                else
                {
                    // Use full settings for all
                    ApplyFullSettings(softBody);
                }
            }
        }
        
        private void ApplyFullSettings(SoftBodyPhysics softBody)
        {
            softBody.settings.SkipUpdate = false;
            softBody.settings.solverIterations = fullQualitySolverIterations;
            softBody.settings.maxStuffingParticles = fullQualityMaxParticles;
        }
        
        private void ApplyReducedSettings(SoftBodyPhysics softBody)
        {
            softBody.settings.SkipUpdate = false;
            softBody.settings.solverIterations = reducedQualitySolverIterations;
            softBody.settings.maxStuffingParticles = reducedQualityMaxParticles;
            softBody.settings.damping = 0.05f; // More damping to settle faster
        }
        
        private void ApplyMinimalSettings(SoftBodyPhysics softBody)
        {
            softBody.settings.SkipUpdate = false;
            softBody.settings.solverIterations = 1;
            softBody.settings.maxStuffingParticles = 5;
            softBody.settings.damping = 0.10f; // Even more damping
        }
    }
}
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace SoftBody.Scripts.Testing
{
    public class SoftBodyPerformanceMonitor : MonoBehaviour
    {
        // Singleton implementation
        private static SoftBodyPerformanceMonitor _instance;
        public static SoftBodyPerformanceMonitor Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<SoftBodyPerformanceMonitor>();
                    
                    if (_instance == null)
                    {
                        Debug.LogWarning("No SoftBodyPerformanceMonitor found in scene. Performance monitoring disabled.");
                    }
                }
                return _instance;
            }
        }

        [Header("UI References")]
        public TMPro.TextMeshProUGUI frameRateText;
        public TMPro.TextMeshProUGUI objectCountText;
        public TMPro.TextMeshProUGUI totalSpawnedText;
        public TMPro.TextMeshProUGUI particleCountText;
        public TMPro.TextMeshProUGUI constraintCountText;
        public TMPro.TextMeshProUGUI memoryUsageText;

        [Header("Performance Settings")]
        public bool enableDetailedProfiling = true;
        public bool logPerformanceWarnings = true;
        public float updateInterval = 0.1f;

        [Header("Sleep System Monitoring")]
        public TMPro.TextMeshProUGUI sleepingObjectsText;
        public TMPro.TextMeshProUGUI activeObjectsText;

        [Header("Pool Monitoring")]
        public TMPro.TextMeshProUGUI poolAvailableText;
        public TMPro.TextMeshProUGUI poolActiveText;

        // Performance tracking
        private float _frameRate;
        private float _updateTimer;
        private int _frameCounter;
        private List<SoftBodyPhysics> _allSoftBodies = new();

        // Public data access properties
        public float CurrentFPS => _frameRate;
        public int ActiveSoftBodyCount => _allSoftBodies?.Count(sb => sb != null && sb.enabled) ?? 0;
        public int TotalParticleCount => GetTotalParticles();
        public float TotalMemoryUsage => GetEstimatedMemoryUsage();
        public bool IsAvailable => _instance != null;

        private void Awake()
        {
            // Singleton setup
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Debug.LogWarning($"Multiple SoftBodyPerformanceMonitors detected. Destroying duplicate on {gameObject.name}");
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            // Initialize soft body list
            RefreshSoftBodyList();
            
            // Initial update
            UpdatePerformanceMetrics();
        }

        private void Update()
        {
            _frameCounter++;
            _updateTimer += Time.deltaTime;

            if (_updateTimer >= updateInterval)
            {
                CalculateFrameRate();
                RefreshSoftBodyList();
                UpdatePerformanceMetrics();
                
                _updateTimer = 0f;
                _frameCounter = 0;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void CalculateFrameRate()
        {
            _frameRate = _frameCounter / updateInterval;
        }

        private void RefreshSoftBodyList()
        {
            // Scan scene for all soft bodies
            var allSoftBodies = FindObjectsByType<SoftBodyPhysics>(FindObjectsSortMode.None);
            _allSoftBodies = allSoftBodies.Where(sb => sb != null).ToList();
        }

        private void UpdatePerformanceMetrics()
        {
            // Update frame rate display
            UpdateFrameRateDisplay();
            
            // Update object counts
            UpdateObjectCounts();
            
            // Update soft body metrics
            UpdateSoftBodyMetrics();
            
            if (enableDetailedProfiling)
            {
                UpdateDetailedDiagnostics();
            }
            
            UpdateSleepSystemMetrics();
            UpdatePoolMetrics();
        }

        private void UpdateFrameRateDisplay()
        {
            if (frameRateText != null)
            {
                frameRateText.text = $"{_frameRate:F1}";

                // Color code based on performance
                if (_frameRate >= 50f)
                    frameRateText.color = Color.green;
                else if (_frameRate >= 30f)
                    frameRateText.color = Color.yellow;
                else
                    frameRateText.color = Color.red;
            }
        }

        private void UpdateObjectCounts()
        {
            // Count active soft bodies in scene
            int activeCount = _allSoftBodies.Count(sb => sb != null && sb.enabled && sb.gameObject.activeInHierarchy);
            int totalCount = _allSoftBodies.Count;

            if (objectCountText != null)
            {
                objectCountText.text = $"{activeCount}";
            }

            if (totalSpawnedText != null)
            {
                totalSpawnedText.text = $"{totalCount}";
            }
        }

        private void UpdateSoftBodyMetrics()
        {
            var totalParticles = 0;
            var totalConstraints = 0;
            var totalMemory = 0f;

            foreach (var softBody in _allSoftBodies)
            {
                if (softBody != null && softBody.enabled)
                {
                    totalParticles += softBody.ParticleCount;
                    totalConstraints += softBody.ConstraintCount;
                    totalMemory += softBody.MemoryUsageMB;
                }
            }

            if (particleCountText != null)
                particleCountText.text = $"{totalParticles}";

            if (constraintCountText != null)
                constraintCountText.text = $"{totalConstraints}";

            if (memoryUsageText != null)
                memoryUsageText.text = $"{totalMemory:F1}";
        }

        private void UpdateDetailedDiagnostics()
        {
            // Additional detailed profiling if needed
            // This could include GPU timing, memory allocation tracking, etc.
        }

        private void UpdateSleepSystemMetrics()
        {
            if (sleepingObjectsText == null && activeObjectsText == null) return;

            int sleepingCount = 0;
            int activeCount = 0;

            foreach (var softBody in _allSoftBodies)
            {
                if (softBody != null && softBody.enabled)
                {
                    if (softBody.IsAsleep)
                        sleepingCount++;
                    else
                        activeCount++;
                }
            }

            if (sleepingObjectsText != null)
                sleepingObjectsText.text = $"{sleepingCount}";

            if (activeObjectsText != null)
                activeObjectsText.text = $"{activeCount}";
        }

        private void UpdatePoolMetrics()
        {
            // Try to find pool information
            var toyPool = FindFirstObjectByType<SoftBody.Scripts.Pooling.PreGeneratedToyPool>();
            
            if (toyPool != null)
            {
                if (poolAvailableText != null)
                    poolAvailableText.text = $"{toyPool.AvailableCount}";

                if (poolActiveText != null)
                    poolActiveText.text = $"{toyPool.ActiveCount}";
            }
            else
            {
                // No pool found - clear or show N/A
                if (poolAvailableText != null)
                    poolAvailableText.text = "N/A";

                if (poolActiveText != null)
                    poolActiveText.text = "N/A";
            }
        }

        // Public methods for external access
        public int GetTotalParticles()
        {
            if (_allSoftBodies == null) return 0;

            var total = 0;
            foreach (var softBody in _allSoftBodies)
            {
                if (softBody != null && softBody.enabled)
                {
                    total += softBody.ParticleCount;
                }
            }
            return total;
        }

        public float GetEstimatedMemoryUsage()
        {
            if (_allSoftBodies == null) return 0f;

            var total = 0f;
            foreach (var softBody in _allSoftBodies)
            {
                if (softBody != null && softBody.enabled)
                {
                    total += softBody.MemoryUsageMB;
                }
            }
            return total;
        }

        public void LogPerformanceMetrics()
        {
            Debug.Log($"PERFORMANCE LOG - Objects: {ActiveSoftBodyCount}, " +
                      $"FPS: {_frameRate:F1}, " +
                      $"Total Particles: {GetTotalParticles():N0}, " +
                      $"Memory: {GetEstimatedMemoryUsage():F1}MB");
        }

        // Method to force refresh (useful when objects are spawned/destroyed)
        public void ForceRefresh()
        {
            RefreshSoftBodyList();
            UpdatePerformanceMetrics();
        }
    }
}
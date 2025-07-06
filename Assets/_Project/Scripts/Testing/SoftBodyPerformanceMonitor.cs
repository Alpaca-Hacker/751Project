using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using SoftBody.Scripts.Core;
using SoftBody.Scripts.Pooling;
using SoftBody.Scripts.Spawning;
using TMPro;

namespace SoftBody.Scripts
{

    public class SoftBodyPerformanceMonitor : MonoBehaviour
    {
        [Header("UI References")] 
        public TMP_Text objectCountText;
        public TMP_Text totalSpawnedText;
        public TMP_Text frameRateText;
        public TMP_Text totalParticlesText;
        public TMP_Text totalConstraintsText;
        public TMP_Text memoryUsageText;
        public Button spawnButton;
        public Button clearButton;
        public Button toggleAutoSpawnButton;
        public Slider spawnRateSlider;
        public TMP_Text spawnRateText;
        public TMP_Text sleepCountText;
        public TMP_Text sleepEfficiencyText;
        public TMP_Text movementStatsText;
        [Header("Pool Monitoring")]
        public TMP_Text poolEfficiencyText;
        public TMP_Text poolStatsText;

        [Header("Performance Tracking")] 
        public bool enablePerformanceLogging = true;
        public float logInterval = 5f;

        [Header("Performance Diagnostics")]
        public bool enableDetailedProfiling = true;
        public TMP_Text gpuTimeText;
        public TMP_Text constraintEfficiencyText;
        public TMP_Text threadGroupUtilizationText;
        
        [Header("Spawner References")]
        public SoftBodySpawner softBodySpawner;

        private PreGeneratedToyPool[] _softBodyPools;
        
        
        private SoftBodySpawner _spawner;
        private readonly List<SoftBodyPhysics> _allSoftBodies = new();
        private GenericObjectPool[] _allPools;
        private float _lastLogTime;
        private float _frameRate;
        private int _frameCount;
        private float _frameTimer;

        private void Start()
        {
           _spawner = FindFirstObjectByType<SoftBodySpawner>();
            if (_spawner == null)
            {
                Debug.LogError("SoftBodyPerformanceMonitor: No SoftBodySpawner found in scene!");
                return;
            }

            SetupUI();
            //_spawner.OnObjectSpawned += UpdateObjectCount;
           
            _allPools = FindObjectsByType<GenericObjectPool>(FindObjectsSortMode.None);
            _softBodyPools = FindObjectsByType<PreGeneratedToyPool>(FindObjectsSortMode.None);

            InvokeRepeating(nameof(UpdatePerformanceMetrics), 0f, 0.1f);
        }

        private void SetupUI()
        {
            if (spawnButton != null)
            {
                spawnButton.onClick.AddListener(() => _spawner.SpawnObject());
            }

            if (clearButton != null)
            {
                clearButton.onClick.AddListener(() => _spawner.ReturnAllObjects());
            }

            if (toggleAutoSpawnButton != null)
            {
                toggleAutoSpawnButton.onClick.AddListener(ToggleAutoSpawn);
                UpdateAutoSpawnButtonText();
            }

            if (spawnRateSlider != null)
            {
                spawnRateSlider.value = _spawner.spawnInterval;
                spawnRateSlider.onValueChanged.AddListener(OnSpawnRateChanged);
            }

            UpdateSpawnRateText();
        }

        private int _spawnTimer = 0;

        private void Update()
        {
            UpdateFrameRate();

            if (enablePerformanceLogging && Time.time - _lastLogTime > logInterval)
            {
                LogPerformanceMetrics();
                _lastLogTime = Time.time;
            }

            if (_frameRate > 100)
            {
               // _spawnTimer++;
                if (_spawnTimer >= 50) // Log every 10 frames
                {
                    _spawner.SpawnObject();
                    _spawnTimer = 0;
                }
            }
        }

        private void UpdateFrameRate()
        {
            _frameCount++;
            _frameTimer += Time.deltaTime;

            if (_frameTimer >= 1f)
            {
                _frameRate = _frameCount / _frameTimer;
                _frameCount = 0;
                _frameTimer = 0f;
            }
        }

        private void UpdateObjectCount(GameObject obj)
        {
            var count = _spawner.ActiveObjectCount;
            if (objectCountText != null)
            {
                objectCountText.text = $"{count}";
            }

            if (totalSpawnedText != null)
            {
                totalSpawnedText.text = $"{_spawner.TotalObjectCount}";
            }
        }

        private void UpdatePerformanceMetrics()
        {
            // Update frame rate display
            if (frameRateText != null)
            {
                frameRateText.text = $"{_frameRate:F1}";

                // Colour code based on performance
                if (_frameRate >= 50f)
                    frameRateText.color = Color.green;
                else if (_frameRate >= 30f)
                    frameRateText.color = Color.yellow;
                else
                    frameRateText.color = Color.red;
            }

            // Update soft body metrics
            UpdateSoftBodyMetrics();
            
            if (enableDetailedProfiling)
            {
                UpdateDetailedDiagnostics();
            } 
            
            UpdateSleepSystemMetrics();
            UpdatePoolMetrics();
        }

        private void UpdateSoftBodyMetrics()
        {
            // Find all active soft bodies
            var allSoftBodies = SoftBodyCacheManager.GetCachedSoftBodies();

            var totalParticles = 0;
            var totalConstraints = 0;
            var totalMemory = 0f;

            foreach (var softBody in allSoftBodies)
            {
                if (softBody.enabled)
                {
                    totalParticles += softBody.ParticleCount;
                     totalConstraints += softBody.ConstraintCount;

                     totalMemory += softBody.MemoryUsageMB;
                }
            }
            
            if (objectCountText != null && softBodySpawner != null)
            {
                objectCountText.text = $"{softBodySpawner.ActiveObjectCount}";
            }
    
            if (totalSpawnedText != null && softBodySpawner != null)
            {
                totalSpawnedText.text = $"{softBodySpawner.TotalObjectCount}";
            }

            if (totalParticlesText != null)
            {
                totalParticlesText.text = $"{totalParticles:N0}";
            }

            if (totalConstraintsText != null)
            {
                totalConstraintsText.text = $"{totalConstraints:N0}";
            }

            if (memoryUsageText != null)
            {
                memoryUsageText.text = $"{totalMemory:F2} MB";
            }
        }

        private void UpdateDetailedDiagnostics()
        {
            var allSoftBodies = FindObjectsByType<SoftBodyPhysics>(FindObjectsSortMode.None);

            var totalParticles = 0;
            var totalConstraints = 0;
            var activeObjects = 0;

            foreach (var softBody in allSoftBodies)
            {
                if (softBody.enabled && softBody.gameObject.activeInHierarchy)
                {
                    totalParticles += softBody.ParticleCount;
                    // You'll need to expose constraint count
                    activeObjects++;
                }
            }

            // Calculate thread group efficiency
            var threadGroupSize = 64f;
            var particleThreadGroups = Mathf.CeilToInt(totalParticles / threadGroupSize);
            var particleUtilization = totalParticles / (particleThreadGroups * threadGroupSize) * 100f;

            if (threadGroupUtilizationText != null)
            {
                threadGroupUtilizationText.text = $"{particleUtilization:F1}%";

                // Colour code efficiency
                if (particleUtilization >= 80f)
                    threadGroupUtilizationText.color = Color.green;
                else if (particleUtilization >= 60f)
                    threadGroupUtilizationText.color = Color.yellow;
                else
                    threadGroupUtilizationText.color = Color.red;
            }

            // Estimate constraints per particle ratio
            if (constraintEfficiencyText != null && totalParticles > 0)
            {
                var constraintRatio = (float)totalConstraints / totalParticles;
                constraintEfficiencyText.text = $"{constraintRatio:F1}";
            }
        }

        private void UpdateSleepSystemMetrics()
        {
            var allSoftBodies = FindObjectsByType<SoftBodyPhysics>(FindObjectsSortMode.None);

            var sleepingCount = 0;
            var movingCount = 0;
            var dampenedCount = 0;
            var totalSleepEfficiency = 0f;

            foreach (var softBody in allSoftBodies)
            {
                if (softBody.enabled && softBody.gameObject.activeInHierarchy)
                {
                    if (softBody.IsAsleep)
                    {
                        sleepingCount++;
                    }
                    else if (softBody.MovementSpeed > 0.01f)
                    {
                        movingCount++;
                    }
                    else
                    {
                        dampenedCount++;
                    }
                    
                }
            }

            if (sleepCountText != null)
            {
                sleepCountText.text =  $"Sleep States - Sleeping: {sleepingCount}, Moving: {movingCount}, Dampened: {dampenedCount}";
            }

            if (sleepEfficiencyText != null && allSoftBodies.Length > 0)
            {
                var avgEfficiency = totalSleepEfficiency / allSoftBodies.Length * 100f;
                sleepEfficiencyText.text = $"{avgEfficiency:F1}%";

                // Colour code efficiency
                if (avgEfficiency >= 70f)
                    sleepEfficiencyText.color = Color.green;
                else if (avgEfficiency >= 40f)
                    sleepEfficiencyText.color = Color.yellow;
                else
                    sleepEfficiencyText.color = Color.red;
            }

            if (movementStatsText != null)
            {
                var activeObjects = allSoftBodies.Length - sleepingCount;
                var computationSaved = sleepingCount / (float)allSoftBodies.Length * 100f;
                movementStatsText.text =
                    $"Active Objects: {activeObjects}/{allSoftBodies.Length} ({computationSaved:F0}% computation saved)";
            }
        }
        
        private void UpdatePoolMetrics()
        {
            if (_softBodyPools == null || _softBodyPools.Length == 0) return;
    
            var totalActive = 0;
            var totalAvailable = 0;
            var totalCapacity = 0;
    
            foreach (var pool in _softBodyPools)
            {
                if (pool != null)
                {
                    totalActive += pool.ActiveCount;
                    totalAvailable += pool.AvailableCount;
                    totalCapacity += pool.TotalCount;
                }
            }
    
            if (poolStatsText != null)
            {
                poolStatsText.text = $"Pool: Active:{totalActive} Available:{totalAvailable} Total:{totalCapacity}";
            }
    
            if (poolEfficiencyText != null && totalCapacity > 0)
            {
                var efficiency = (float)totalAvailable / totalCapacity * 100f;
                poolEfficiencyText.text = $"{efficiency:F1}%";
        
                // Colour code efficiency
                if (efficiency >= 60f)
                    poolEfficiencyText.color = Color.green;
                else if (efficiency >= 30f)
                    poolEfficiencyText.color = Color.yellow;
                else
                    poolEfficiencyText.color = Color.red;
            }
        }
        
        private bool _autoSpawnEnabled = false;

        private void ToggleAutoSpawn()
        {
            _autoSpawnEnabled= !_autoSpawnEnabled;

            if (_autoSpawnEnabled)
            {
                _spawner.StartSpawning();
            }
            else
            {
                _spawner.StopSpawning();
            }

            UpdateAutoSpawnButtonText();
        }

        private void UpdateAutoSpawnButtonText()
        {
            if (toggleAutoSpawnButton != null)
            {
                var text = toggleAutoSpawnButton.GetComponentInChildren<Text>();
                if (text != null)
                {
                    text.text = _autoSpawnEnabled ? "Stop Auto Spawn" : "Start Auto Spawn";
                }
            }
        }

        private void OnSpawnRateChanged(float value)
        {
            _spawner.spawnInterval = value;
            UpdateSpawnRateText();
        }

        private void UpdateSpawnRateText()
        {
            if (spawnRateText != null && spawnRateSlider != null)
            {
                spawnRateText.text = $"{spawnRateSlider.value:F1}/sec";
            }
        }

        private void LogPerformanceMetrics()
        {
            Debug.Log($"PERFORMANCE LOG - Objects: {_spawner.ActiveObjectCount}, " +
                      $"FPS: {_frameRate:F1}, " +
                      $"Total Particles: {GetTotalParticles():N0}, " +
                      $"Memory: {GetEstimatedMemoryUsage():F1}MB");
        }

        private int GetTotalParticles()
        {
            var total = 0;
            foreach (var softBody in _allSoftBodies)
            {
                if (softBody.enabled)
                {
                    total += softBody.ParticleCount;
                }
            }

            return total;
        }

        private float GetEstimatedMemoryUsage()
        {
            var total = 0f;
            foreach (var softBody in _allSoftBodies)
            {
                if (softBody.enabled)
                {
                    total += softBody.ParticleCount * 64f / (1024f * 1024f);
                }
            }

            return total;
        }
        
    }
}
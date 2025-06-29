using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
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

        [Header("Performance Tracking")] 
        public bool enablePerformanceLogging = true;
        public float logInterval = 5f;

        [Header("Performance Diagnostics")]
        public bool enableDetailedProfiling = true;
        public TMP_Text gpuTimeText;
        public TMP_Text constraintEfficiencyText;
        public TMP_Text threadGroupUtilizationText;
        
        
        private SoftBodySpawner _spawner;
        private readonly List<SoftBodyPhysics> _allSoftBodies = new();
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
            _spawner.OnObjectCountChanged += UpdateObjectCount;

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
                clearButton.onClick.AddListener(() => _spawner.ClearAllObjects());
            }

            if (toggleAutoSpawnButton != null)
            {
                toggleAutoSpawnButton.onClick.AddListener(ToggleAutoSpawn);
                UpdateAutoSpawnButtonText();
            }

            if (spawnRateSlider != null)
            {
                spawnRateSlider.value = _spawner.spawnRate;
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
                _spawnTimer++;
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

        private void UpdateObjectCount(int count)
        {
            if (objectCountText != null)
            {
                objectCountText.text = $"{count}";
            }

            if (totalSpawnedText != null)
            {
                totalSpawnedText.text = $"{_spawner.TotalObjectsSpawned}";
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
        }

        private void UpdateSoftBodyMetrics()
        {
            // Find all active soft bodies
            _allSoftBodies.Clear();
            _allSoftBodies.AddRange(FindObjectsByType<SoftBodyPhysics>(FindObjectsSortMode.None));

            var totalParticles = 0;
            var totalConstraints = 0;
            var totalMemory = 0f;

            foreach (var softBody in _allSoftBodies)
            {
                if (softBody.enabled)
                {
                    totalParticles += softBody.ParticleCount;
                    // You might need to add a public property for constraint count
                     totalConstraints += softBody.ConstraintCount;

                    // Estimate memory usage (rough calculation)
                    totalMemory += softBody.ParticleCount * 64f / (1024f * 1024f); // ~64 bytes per particle
                }
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
                memoryUsageText.text = $"{totalMemory:F1} MB";
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

                    totalSleepEfficiency += softBody.SleepEfficiency;
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

        private void ToggleAutoSpawn()
        {
            _spawner.autoSpawn = !_spawner.autoSpawn;

            if (_spawner.autoSpawn)
            {
                _spawner.StartAutoSpawn();
            }
            else
            {
                _spawner.StopAutoSpawn();
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
                    text.text = _spawner.autoSpawn ? "Stop Auto Spawn" : "Start Auto Spawn";
                }
            }
        }

        private void OnSpawnRateChanged(float value)
        {
            _spawner.spawnRate = value;
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
            Debug.Log($"PERFORMANCE LOG - Objects: {_spawner.CurrentObjectCount}, " +
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

        private void OnDestroy()
        {
            if (_spawner != null)
            {
                _spawner.OnObjectCountChanged -= UpdateObjectCount;
            }
        }
    }
}
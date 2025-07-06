using System.Collections.Generic;
using UnityEngine;

namespace SoftBody.Scripts.Core
{
    public class SleepSystem
    {
        // --- Dependencies ---
        private readonly SoftBodySettings _settings;
        private readonly Transform _transform;

        // --- State ---
        private bool _isAsleep;
        private float _sleepTimer;
        private float _checkTimer;
        private Vector3 _lastPosition;
        private float _currentSpeed;
        
        // --- Proximity Wake-up Management ---
        private static readonly List<SleepSystem> AllSleepSystems = new();
        private static float _lastProximityCheckTime;

        // --- Public Properties ---
        public bool IsAsleep => _isAsleep;
        public float CurrentSpeed => _currentSpeed;

        public SleepSystem(SoftBodySettings settings, Transform transform)
        {
            _settings = settings;
            _transform = transform;
            _lastPosition = _transform.position;
            
            if (!AllSleepSystems.Contains(this))
            {
                AllSleepSystems.Add(this);
            }
        }

        /// <summary>
        /// Call this method from the main Update loop.
        /// </summary>
        public void Update(float deltaTime)
        {
            _checkTimer += deltaTime;

            // Only check for sleep state periodically to save performance
            if (_checkTimer < 0.1f) // Check 10 times per second
            {
                return;
            }

            // Calculate movement speed
            var currentPosition = _transform.position;
            _currentSpeed = Vector3.Distance(currentPosition, _lastPosition) / _checkTimer;
            _lastPosition = currentPosition;
            
            // Reset the check timer
            _checkTimer = 0f;

            // Update sleep state based on speed
            if (_currentSpeed < _settings.sleepVelocityThreshold)
            {
                _sleepTimer += 0.1f; // Add the interval time
                if (_sleepTimer > _settings.sleepTimeThreshold && !_isAsleep)
                {
                    GoToSleep();
                }
            }
            else
            {
                // The body is moving
                if (_isAsleep)
                {
                    WakeUp();
                }
                else
                {
                    // If it was previously still but now moving significantly, wake up others
                    if (_currentSpeed > _settings.sleepVelocityThreshold * 4f)
                    {
                        WakeUpNearby();
                    }
                }
                
                _sleepTimer = 0f;
            }
        }
        
        /// <summary>
        /// Forces the soft body to wake up.
        /// </summary>
        public void WakeUp()
        {
            if (!_isAsleep) return;
            
            _isAsleep = false;
            _sleepTimer = 0f;
            if (_settings.showSleepState)
            {
                Debug.Log($"{_transform.name} woke up.");
            }
        }
        
        /// <summary>
        /// Call this when the soft body is destroyed or disabled to clean up.
        /// </summary>
        public void Unregister()
        {
            if (AllSleepSystems.Contains(this))
            {
                AllSleepSystems.Remove(this);
            }
        }

        /// <summary>
        /// Handles external collision events to wake the body up.
        /// </summary>
        public void OnCollisionImpact(float impactForce)
        {
            if (_isAsleep && impactForce > 0.5f) // Threshold for a significant impact
            {
                WakeUp();
                if (_settings.showSleepState)
                {
                    Debug.Log($"{_transform.name} woken by collision (force: {impactForce:F2})");
                }
            }
        }

        private void GoToSleep()
        {
            _isAsleep = true;
            if (_settings.showSleepState)
            {
                Debug.Log($"{_transform.name} went to sleep (speed: {_currentSpeed:F4})");
            }
        }

        private void WakeUpNearby()
        {
            if (!_settings.enableProximityWake) return;
    
            // Throttle proximity checks
            if (Time.time - _lastProximityCheckTime < 0.1f) return;
            _lastProximityCheckTime = Time.time;

            var position = _transform.position;
            var radius = _settings.proximityWakeRadius;
    
            // Use spatial cache instead of checking all sleep systems
            var nearbySoftBodies = SoftBodyCacheManager.GetSoftBodiesNear(position, radius);
    
            foreach (var body in nearbySoftBodies)
            {
                if (body != null && body.transform != _transform && body.IsAsleep)
                {
                    body.WakeUp();
            
                    if (_settings.showSleepState)
                    {
                        Debug.Log($"{body.gameObject.name} woken by nearby movement from {_transform.gameObject.name}");
                    }
                }
            }
        }
    }
}
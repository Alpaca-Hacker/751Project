using System.Collections.Generic;
using UnityEngine;

namespace SoftBody.Scripts.Core
{
    /// <summary>
    /// Global cache manager to avoid expensive FindObjectsByType calls
    /// </summary>
    public static class SoftBodyCacheManager
    {
        private static readonly List<SoftBodyPhysics> _cachedSoftBodies = new();
        private static readonly List<Collider> _cachedColliders = new();
        private static readonly HashSet<SoftBodyPhysics> _activeSoftBodies = new();

        private static float _lastSoftBodyCacheUpdate = -1f;
        private static float _lastColliderCacheUpdate = -1f;

        // Cache update intervals (in seconds)
        private const float SOFT_BODY_CACHE_INTERVAL = 5f;
        private const float COLLIDER_CACHE_INTERVAL = 5f; // Colliders change less frequently

        // Spatial cache for performance
        private static readonly Dictionary<Vector3Int, List<SoftBodyPhysics>> _spatialGrid = new();
        private const float GRID_SIZE = 10f;


        /// <summary>
        /// Register a soft body when it's created/enabled
        /// </summary>
        public static void RegisterSoftBody(SoftBodyPhysics softBody)
        {
            if (softBody != null && !_activeSoftBodies.Contains(softBody))
            {
                _activeSoftBodies.Add(softBody);
                InvalidateSoftBodyCache();
            }
        }

        /// <summary>
        /// Unregister a soft body when it's destroyed/disabled
        /// </summary>
        public static void UnregisterSoftBody(SoftBodyPhysics softBody)
        {
            if (_activeSoftBodies.Remove(softBody))
            {
                InvalidateSoftBodyCache();
            }
        }

        /// <summary>
        /// Get cached soft bodies, updating if necessary
        /// </summary>
        public static List<SoftBodyPhysics> GetCachedSoftBodies()
        {
            UpdateSoftBodyCacheIfNeeded();
            return _cachedSoftBodies;
        }

        /// <summary>
        /// Get soft bodies near a position using spatial grid
        /// </summary>
        // public static List<SoftBodyPhysics> GetSoftBodiesNear(Vector3 position, float radius)
        // {
        //     if (Time.time - _lastSoftBodyCacheUpdate >= SOFT_BODY_CACHE_INTERVAL)
        //     {
        //         UpdateSoftBodyCache();
        //     }
        //     
        //     UpdateSpatialGrid();
        //     
        //     var result = new List<SoftBodyPhysics>();
        //     var gridPos = WorldToGrid(position);
        //     var gridRadius = Mathf.CeilToInt(radius / GRID_SIZE);
        //     
        //     // Check surrounding grid cells
        //     for (var x = -gridRadius; x <= gridRadius; x++)
        //     {
        //         for (var z = -gridRadius; z <= gridRadius; z++)
        //         {
        //             var checkPos = gridPos + new Vector3Int(x, 0, z);
        //             if (_spatialGrid.TryGetValue(checkPos, out var bodies))
        //             {
        //                 foreach (var body in bodies)
        //                 {
        //                     if (body != null && Vector3.Distance(position, body.transform.position) <= radius)
        //                     {
        //                         result.Add(body);
        //                     }
        //                 }
        //             }
        //         }
        //     }
        //     
        //     return result;
        // }

        public static List<SoftBodyPhysics> GetSoftBodiesNear(Vector3 position, float radius)
        {
            // Remove the forced update - let it update on its normal schedule
            UpdateSoftBodyCacheIfNeeded(); // Use normal throttled update

            var result = new List<SoftBodyPhysics>();

            // Simple distance check instead of complex spatial grid for now
            foreach (var body in _cachedSoftBodies)
            {
                if (body != null && body.enabled && body.gameObject.activeInHierarchy)
                {
                    var distance = Vector3.Distance(position, body.transform.position);
                    if (distance <= radius)
                    {
                        result.Add(body);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Get cached colliders, updating if necessary
        /// </summary>
        public static List<Collider> GetCachedColliders()
        {
            if (_cachedColliders.Count == 0 || _lastColliderCacheUpdate < 0f)
            {
                UpdateColliderCache();
            }
            else
            {
                UpdateColliderCacheIfNeeded();
            }

            return _cachedColliders;
        }

        /// <summary>
        /// Get colliders near a position
        /// </summary>
        public static List<Collider> GetCollidersNear(Vector3 position, float radius)
        {
            
            var result = new List<Collider>();
            var allColliders = GetCachedColliders();
            var radiusSq = radius * radius;
            
            foreach (var collider in allColliders)
            {
                if (collider != null && collider.enabled)
                {
                    var distanceSq = Vector3.SqrMagnitude(position - collider.transform.position);
                    if (distanceSq <= radiusSq)
                    {
                        result.Add(collider);
                    }
                }
            }
            
            return result;
        }

        private static void UpdateSoftBodyCacheIfNeeded()
        {
            if (Time.time - _lastSoftBodyCacheUpdate >= SOFT_BODY_CACHE_INTERVAL)
            {
                UpdateSoftBodyCache();
            }
        }

        private static void UpdateColliderCacheIfNeeded()
        {
            if (Time.time - _lastColliderCacheUpdate >= COLLIDER_CACHE_INTERVAL)
            {
                UpdateColliderCache();
            }
        }

        private static void UpdateSoftBodyCache()
        {
            _cachedSoftBodies.Clear();
            
            // First, add registered soft bodies
            foreach (var softBody in _activeSoftBodies)
            {
                if (softBody != null && softBody.enabled && softBody.gameObject.activeInHierarchy)
                {
                    _cachedSoftBodies.Add(softBody);
                }
            }
            
            // Fallback: find any that weren't registered (shouldn't happen normally)
            var foundBodies = Object.FindObjectsByType<SoftBodyPhysics>(FindObjectsSortMode.None);
            foreach (var body in foundBodies)
            {
                if (!_cachedSoftBodies.Contains(body) && body.enabled && body.gameObject.activeInHierarchy)
                {
                    _cachedSoftBodies.Add(body);
                    _activeSoftBodies.Add(body); // Register it for next time
                }
            }
            
            _lastSoftBodyCacheUpdate = Time.time;
        }

        private static void UpdateColliderCache()
        {
            _cachedColliders.Clear();
            var allColliders = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
            
            foreach (var collider in allColliders)
            {
                if (collider != null && collider.enabled && !collider.isTrigger 
                    && collider.GetComponent<SoftBodyPhysics>() == null)
                {
                    _cachedColliders.Add(collider);
                }
            }
            
            _lastColliderCacheUpdate = Time.time;
        }

        private static void UpdateSpatialGrid()
        {
            _spatialGrid.Clear();
            
            foreach (var body in _cachedSoftBodies)
            {
                if (body != null)
                {
                    var gridPos = WorldToGrid(body.transform.position);
                    if (!_spatialGrid.ContainsKey(gridPos))
                    {
                        _spatialGrid[gridPos] = new List<SoftBodyPhysics>();
                    }
                    _spatialGrid[gridPos].Add(body);
                }
            }
        }

        private static Vector3Int WorldToGrid(Vector3 worldPos)
        {
            return new Vector3Int(
                Mathf.FloorToInt(worldPos.x / GRID_SIZE),
                0, // We don't need Y for this demo
                Mathf.FloorToInt(worldPos.z / GRID_SIZE)
            );
        }

        private static void InvalidateSoftBodyCache()
        {
            _lastSoftBodyCacheUpdate = -1f; // Force update on next request
        }

        /// <summary>
        /// Clear all caches (call when scene changes)
        /// </summary>
        public static void ClearAllCaches()
        {
            _cachedSoftBodies.Clear();
            _cachedColliders.Clear();
            _activeSoftBodies.Clear();
            _spatialGrid.Clear();
            _lastSoftBodyCacheUpdate = -1f;
            _lastColliderCacheUpdate = -1f;
        }
    }
}
using System.Collections.Generic;
using UnityEngine;

namespace SoftBody.Scripts.Core
{
    /// <summary>
    /// Global cache manager to avoid expensive FindObjectsByType calls
    /// </summary>
    public static class SoftBodyCacheManager
    {
        private static readonly List<SoftBodyPhysics> CachedSoftBodies = new();
        private static readonly List<Collider> CachedColliders = new();
        private static readonly HashSet<SoftBodyPhysics> ActiveSoftBodies = new();
        
        private static float _lastSoftBodyCacheUpdate = -1f;
        private static float _lastColliderCacheUpdate = -1f;
        
        // Cache update intervals (in seconds)
        private const float SoftBodyCacheInterval = 2f;
        private const float ColliderCacheInterval = 5f; // Colliders change less frequently
        
        // Spatial cache for performance
        private static readonly Dictionary<Vector3Int, List<SoftBodyPhysics>> SpatialGrid = new();
        private const float GridSize = 10f;
        
        
        /// <summary>
        /// Register a soft body when it's created/enabled
        /// </summary>
        public static void RegisterSoftBody(SoftBodyPhysics softBody)
        {
            if (softBody != null && ActiveSoftBodies.Add(softBody))
            {
                InvalidateSoftBodyCache();
            }
        }

        /// <summary>
        /// Unregister a soft body when it's destroyed/disabled
        /// </summary>
        public static void UnregisterSoftBody(SoftBodyPhysics softBody)
        {
            if (ActiveSoftBodies.Remove(softBody))
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
            return CachedSoftBodies;
        }

        /// <summary>
        /// Get soft bodies near a position using spatial grid
        /// </summary>
        public static List<SoftBodyPhysics> GetSoftBodiesNear(Vector3 position, float radius)
        {
            UpdateSpatialGrid();
            
            var result = new List<SoftBodyPhysics>();
            var gridPos = WorldToGrid(position);
            var gridRadius = Mathf.CeilToInt(radius / GridSize);
            
            for (var x = -gridRadius; x <= gridRadius; x++)
            {
                for (var z = -gridRadius; z <= gridRadius; z++)
                {
                    var checkPos = gridPos + new Vector3Int(x, 0, z);
                    if (SpatialGrid.TryGetValue(checkPos, out var bodies))
                    {
                        foreach (var body in bodies)
                        {
                            if (body != null && Vector3.Distance(position, body.transform.position) <= radius)
                            {
                                result.Add(body);
                            }
                        }
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
            if (CachedColliders.Count == 0 || _lastColliderCacheUpdate < 0f)
            {
                UpdateColliderCache();
            }
            else
            {
                UpdateColliderCacheIfNeeded();
            }

            return CachedColliders;
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
            if (Time.time - _lastSoftBodyCacheUpdate >= SoftBodyCacheInterval)
            {
                UpdateSoftBodyCache();
            }
        }

        private static void UpdateColliderCacheIfNeeded()
        {
            if (Time.time - _lastColliderCacheUpdate >= ColliderCacheInterval)
            {
                UpdateColliderCache();
            }
        }

        private static void UpdateSoftBodyCache()
        {
            CachedSoftBodies.Clear();
            
            // First, add registered soft bodies
            foreach (var softBody in ActiveSoftBodies)
            {
                if (softBody != null && softBody.enabled && softBody.gameObject.activeInHierarchy)
                {
                    CachedSoftBodies.Add(softBody);
                }
            }
            
            // Fallback: find any that weren't registered (shouldn't happen normally)
            var foundBodies = Object.FindObjectsByType<SoftBodyPhysics>(FindObjectsSortMode.None);
            foreach (var body in foundBodies)
            {
                if (!CachedSoftBodies.Contains(body) && body.enabled && body.gameObject.activeInHierarchy)
                {
                    CachedSoftBodies.Add(body);
                    ActiveSoftBodies.Add(body);
                }
            }
            
            _lastSoftBodyCacheUpdate = Time.time;
        }

        private static void UpdateColliderCache()
        {
            CachedColliders.Clear();
            var allColliders = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
            
            foreach (var collider in allColliders)
            {
                if (collider != null && collider.enabled && !collider.isTrigger 
                    && collider.GetComponent<SoftBodyPhysics>() == null)
                {
                    CachedColliders.Add(collider);
                }
            }
            
            _lastColliderCacheUpdate = Time.time;
        }

        private static void UpdateSpatialGrid()
        {
            SpatialGrid.Clear();
            
            foreach (var body in CachedSoftBodies)
            {
                if (body != null)
                {
                    var gridPos = WorldToGrid(body.transform.position);
                    if (!SpatialGrid.ContainsKey(gridPos))
                    {
                        SpatialGrid[gridPos] = new List<SoftBodyPhysics>();
                    }
                    SpatialGrid[gridPos].Add(body);
                }
            }
        }

        private static Vector3Int WorldToGrid(Vector3 worldPos)
        {
            return new Vector3Int(
                Mathf.FloorToInt(worldPos.x / GridSize),
                0, // We don't need Y for this demo
                Mathf.FloorToInt(worldPos.z / GridSize)
            );
        }

        private static void InvalidateSoftBodyCache()
        {
            _lastSoftBodyCacheUpdate = -1f; 
        }

        /// <summary>
        /// Clear all caches (call when scene changes)
        /// </summary>
        public static void ClearAllCaches()
        {
            CachedSoftBodies.Clear();
            CachedColliders.Clear();
            ActiveSoftBodies.Clear();
            SpatialGrid.Clear();
            _lastSoftBodyCacheUpdate = -1f;
            _lastColliderCacheUpdate = -1f;
        }
    }
}
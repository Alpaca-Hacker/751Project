// Place this in a new file: SoftBody/Scripts/Core/CollisionSystem.cs
using System.Collections.Generic;
using SoftBody.Scripts.Models;
using SoftBody.Scripts.Performance;
using UnityEngine;

namespace SoftBody.Scripts.Core
{
    public class CollisionSystem
    {
        // --- Dependencies ---
        private readonly SoftBodySettings _settings;
        private readonly ComputeShaderManager _computeManager;
        private readonly BufferManager _bufferManager;
        private readonly Transform _transform;

        // --- State ---
        private readonly List<SDFCollider> _colliders = new();
        private int _lastUpdateFrame = -1;
        private int _updateInterval = 30;
        private static int _globalCollisionUpdateCounter = 0; // Shared across all systems
        private readonly int _instanceId;
        private static int _nextInstanceId = 0;

        // --- Static Management ---
        private static readonly List<CollisionSystem> AllCollisionSystems = new();

        // Public property to allow other systems to get this one's position
        public Transform Transform => _transform;

        public CollisionSystem(SoftBodySettings settings, Transform transform, ComputeShaderManager computeManager, BufferManager bufferManager)
        {
            _settings = settings;
            _transform = transform;
            _computeManager = computeManager;
            _bufferManager = bufferManager;
        
            // Assign a unique ID to this instance
            _instanceId = _nextInstanceId++;

            if (!AllCollisionSystems.Contains(this))
            {
                AllCollisionSystems.Add(this);
            }
        }

        /// <summary>
        /// Finds all relevant colliders, prepares them, and uploads them to the GPU.
        /// Call this once per frame, before the simulation step.
        /// </summary>
        public void UpdateColliders()
        {
            if (!_settings.enableCollision && !_settings.enableSoftBodyCollisions)
            {
                _computeManager.SetColliderCount(0);
                return;
            }
            
            // Reduce throttling for environment collisions - update every 10 frames instead of 60
            if (Time.frameCount - _lastUpdateFrame < 10)
            {
                return;
            }

            _colliders.Clear();

            AddEnvironmentColliders();
            
            
            if (_colliders.Count > 0)
            {
                try
                {
                    var colliderBuffer = _bufferManager.GetBuffer("colliders");
                    if (colliderBuffer != null)
                    {
                        colliderBuffer.SetData(_colliders, 0, 0, _colliders.Count);
                        _computeManager.UpdateColliderBufferBinding();
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to upload colliders: {e.Message}");
                }
            }

            _computeManager.SetColliderCount(_colliders.Count);
        }

        private void AddEnvironmentColliders()
        {
            // Prioritize floor and essential colliders
            var nearbyColliders =
                SoftBodyCacheManager.GetCollidersNear(_transform.position, _settings.maxInteractionDistance);

            var floorColliders = new List<Collider>();
            var otherColliders = new List<Collider>();

            // Separate floor/ground colliders from others
            foreach (var col in nearbyColliders)
            {
                if (col.CompareTag("Floor") || col.name.ToLower().Contains("floor") ||
                    col.name.ToLower().Contains("ground") || col.name.ToLower().Contains("platform"))
                {
                    floorColliders.Add(col);
                }
                else
                {
                    otherColliders.Add(col);
                }
            }

            // Always add floor colliders first
            foreach (var col in floorColliders)
            {
                var sdfCollider = ConvertToSDFCollider(col);
                if (sdfCollider.HasValue)
                {
                    _colliders.Add(sdfCollider.Value);
                }
            }

            // Then add other environment colliders if we have space
            foreach (var col in otherColliders)
            {
                if (_colliders.Count >= 32) break; // Leave room for soft body colliders

                var sdfCollider = ConvertToSDFCollider(col);
                if (sdfCollider.HasValue)
                {
                    _colliders.Add(sdfCollider.Value);
                }
            }
        }

        /// <summary>
        /// Dispatches the compute shader kernels to solve and apply collisions.
        /// Call this within the simulation substep loop.
        /// </summary>
        public void ApplyCollisions(int particleCount)
        {
            if (_colliders.Count == 0) return;

            // Dispatch kernels via the compute manager
            _computeManager.DispatchCollisionDetection(particleCount);
            _computeManager.DispatchCollisionResponse(particleCount);
        }

        /// <summary>
        /// Removes this system from the static list for proximity checks.
        /// </summary>
        public void Unregister()
        {
            if (AllCollisionSystems.Contains(this))
            {
                AllCollisionSystems.Remove(this);
            }
        }

        private Bounds GetEstimatedBounds(SoftBodyPhysics softBody)
        {
            var settings = softBody.settings;
            var transform = softBody.transform;

            if (settings.inputMesh != null && !settings.useProceduralCube)
            {
                var bounds = settings.inputMesh.bounds;
                bounds.size = Vector3.Scale(bounds.size, transform.localScale);
                bounds.center = transform.position;
                return bounds;
            }

            if (settings.useProceduralCube)
            {
                return new Bounds(transform.position, Vector3.Scale(settings.size, transform.localScale));
            }

            return new Bounds(transform.position, Vector3.one);
        }

        private SDFCollider? ConvertToSDFCollider(Collider col)
        {
            if (col.CompareTag("Floor"))
            {
                // Always treat floor as a plane
                var planeNormal = col.transform.up;
                var planePos = col.transform.position;

                // Offset slightly up to account for collider thickness
                planePos += planeNormal * 0.01f;

                var planeDistance = Vector3.Dot(planePos, planeNormal);
                return SDFCollider.CreatePlane(planeNormal, planeDistance);
            }

            switch (col)
            {
                case BoxCollider box:
                    var boxTransform = box.transform;
                    var center = boxTransform.TransformPoint(box.center);
                    var size = Vector3.Scale(box.size, boxTransform.lossyScale);
                    return SDFCollider.CreateBox(center, size * 0.5f, boxTransform.rotation);

                case SphereCollider sphere:
                    var sphereTransform = sphere.transform;
                    var sphereCenter = sphereTransform.TransformPoint(sphere.center);
                    var radius = sphere.radius * Mathf.Max(
                        sphereTransform.lossyScale.x,
                        sphereTransform.lossyScale.y,
                        sphereTransform.lossyScale.z);
                    return SDFCollider.CreateSphere(sphereCenter, radius);

                case CapsuleCollider capsule:
                    // Convert capsule to cylinder (approximation)
                    var capsuleTransform = capsule.transform;
                    var capsuleCenter = capsuleTransform.TransformPoint(capsule.center);
                    var capsuleRadius = capsule.radius * Mathf.Max(
                        capsuleTransform.lossyScale.x,
                        capsuleTransform.lossyScale.z);
                    var capsuleHeight = capsule.height * capsuleTransform.lossyScale.y;

                    // Capsule direction affects rotation
                    var capsuleRotation = capsuleTransform.rotation;
                    if (capsule.direction == 0) // X-axis
                    {
                        capsuleRotation *= Quaternion.Euler(0, 0, 90);
                    }
                    else if (capsule.direction == 2) // Z-axis
                    {
                        capsuleRotation *= Quaternion.Euler(90, 0, 0);
                    }

                    return SDFCollider.CreateCylinder(capsuleCenter, capsuleRadius, capsuleHeight, capsuleRotation);

                case MeshCollider mesh when mesh.convex:
                    // For now, approximate convex mesh as box
                    var bounds = mesh.bounds;
                    var meshCenter = mesh.transform.TransformPoint(bounds.center);
                    var meshSize = Vector3.Scale(bounds.size, mesh.transform.lossyScale);
                    return SDFCollider.CreateBox(meshCenter, meshSize * 0.5f, mesh.transform.rotation);

                default:
                    return null;
            }
        }

        private void AddSoftBodyColliders()
        {
            var maxSoftBodyColliders = 8; // Reduced from 32
            var maxCheckDistance = _settings.maxInteractionDistance * 0.5f; // Reduce check range

            var nearbySoftBodies = SoftBodyCacheManager.GetSoftBodiesNear(_transform.position, maxCheckDistance);

            var added = 0;
            foreach (var otherBody in nearbySoftBodies)
            {
                if (added >= maxSoftBodyColliders || _colliders.Count >= 24)
                {
                    break;
                }

                if (otherBody.transform == _transform)
                {
                    continue;
                }

                // Only interact with other high/medium quality objects
                if (!ShouldInteractWith(otherBody))
                {
                    continue;
                }

                var otherBounds = GetEstimatedBounds(otherBody);
                var radius = Mathf.Max(otherBounds.extents.x, otherBounds.extents.y, otherBounds.extents.z)
                             * _settings.interactionStrength * 0.8f; // Smaller collision spheres

                _colliders.Add(SDFCollider.CreateSphere(otherBody.transform.position, radius));
                added++;
            }
        }

        private bool ShouldInteractWith(SoftBodyPhysics otherBody)
        {
            // Only high and medium quality objects should interact with each other
            var performanceManager = SoftBodyPerformanceManager.Instance;
            if (performanceManager == null) return true;

            // Simple distance check - don't interact with very distant objects
            var distance = Vector3.Distance(_transform.position, otherBody.transform.position);
            return distance < _settings.maxInteractionDistance * 0.7f;
        }
    }
}

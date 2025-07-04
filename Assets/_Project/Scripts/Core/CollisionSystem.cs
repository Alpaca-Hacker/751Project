// Place this in a new file: SoftBody/Scripts/Core/CollisionSystem.cs
using System.Collections.Generic;
using SoftBody.Scripts.Models;
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

            _colliders.Clear();

            // Add standard Unity colliders from the environment
            if (_settings.enableCollision)
            {
                AddEnvironmentColliders();
            }

            // Add other soft bodies as approximate sphere colliders
            if (_settings.enableSoftBodyCollisions)
            {
                AddSoftBodyColliders();
            }

            // Upload the data to the GPU buffer
            if (_colliders.Count > 0)
            {
                var colliderBuffer = _bufferManager.GetBuffer("colliders");
                colliderBuffer.SetData(_colliders, 0, 0, _colliders.Count);
            }
            
            _computeManager.SetColliderCount(_colliders.Count);
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

        private void AddEnvironmentColliders()
        {
            // Note: FindObjectsByType can be slow. For a real game, you would
            // use a more optimized spatial partitioning system to find nearby colliders.
            var allColliders = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
            foreach (var col in allColliders)
            {
                // Limit colliders to save performance and buffer space
                if (_colliders.Count >= 48) // Reserve some space for soft bodies
                {
                    break;
                } 

                // Skip triggers and colliders that are part of another soft body
                if (col.isTrigger || col.GetComponent<SoftBodyPhysics>() != null)
                {
                    continue;
                }

                var sdfCollider = ConvertToSDFCollider(col);
                if (sdfCollider.HasValue)
                {
                    _colliders.Add(sdfCollider.Value);
                }
            }
        }

        private void AddSoftBodyColliders()
        {
            // Throttle this check for performance
            if (Time.frameCount % 5 != 0) return;

            foreach (var otherSystem in AllCollisionSystems)
            {
                if (_colliders.Count >= 64) break;
                if (otherSystem == this)
                {
                    continue; // Don't collide with self
                } 

                var distance = Vector3.Distance(_transform.position, otherSystem.Transform.position);

                if (distance < _settings.maxInteractionDistance)
                {
                    var otherBounds = otherSystem.GetEstimatedBounds();
                    // Approximate the other soft body as a sphere
                    var radius = Mathf.Max(otherBounds.extents.x, otherBounds.extents.y, otherBounds.extents.z) 
                                 * _settings.interactionStrength;
                    
                    _colliders.Add(SDFCollider.CreateSphere(otherSystem.Transform.position, radius));
                }
            }
        }

        private Bounds GetEstimatedBounds()
        {
            if (_settings.inputMesh != null && !_settings.useProceduralCube)
            {
                var bounds = _settings.inputMesh.bounds;
                bounds.size = Vector3.Scale(bounds.size, _transform.localScale);
                bounds.center = _transform.position;
                return bounds;
            }
            if (_settings.useProceduralCube)
            {
                return new Bounds(_transform.position, Vector3.Scale(_settings.size, _transform.localScale));
            }
            return new Bounds(_transform.position, Vector3.one);
        }

        private SDFCollider? ConvertToSDFCollider(Collider col)
        {
            
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
    }
}
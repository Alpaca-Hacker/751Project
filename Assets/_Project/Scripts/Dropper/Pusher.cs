using System.Collections.Generic;
using UnityEngine;

namespace SoftBody.Scripts
{
    public class Pusher : MonoBehaviour
    {
        [Header("Push Settings")]
        public float pushForce = 8f;
        public float carryForce = 15f; 
        public float sideForce = 2f;
        
        [Header("Direction")]
        [Tooltip("Which direction should the pusher push? Auto-detect or manual")]
        public bool autoDetectDirection = true;
        public Vector3 manualPushDirection = Vector3.back;
        
        [Header("Detection")]
        public float frontDetectionDistance = 1.5f;
        public float topDetectionHeight = 0.8f;
        public float sideDetectionWidth = 1.2f;
        
        [Header("Force Zones")]
        [Tooltip("How far in front to start pushing")]
        public float pushZoneDepth = 1f;
        [Tooltip("How high above pusher to detect toys")]
        public float carryZoneHeight = 0.5f;
        
        [Header("Physics")]
        public float maxPushSpeed = 2f;
        public bool enableCarrying = true;
        public bool enableGentlePushing = true;
        
        [Header("Debug")]
        public bool showGizmos = true;
        public Color pushZoneColour = new Color(1f, 0f, 0f, 0.3f);
        public Color carryZoneColour = new Color(0f, 1f, 0f, 0.3f);
        
        private Vector3 _velocity;
        private Vector3 _lastPosition;
        private List<SoftBodyPhysics> _detectedToys = new();
        private Bounds _pusherBounds;
        
        private void Start()
        {
            _lastPosition = transform.position;
    
            // Get pusher bounds
            var renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                _pusherBounds = renderer.bounds;
            }
            else
            {
                _pusherBounds = new Bounds(transform.position, Vector3.one);
            }
    
            // Configure colliders for platform behavior
            var colliders = GetComponents<Collider>();
            foreach (var col in colliders)
            {
                col.enabled = true;
                col.isTrigger = false;
                
                if (col.material == null)
                {
                    var physMat = new PhysicsMaterial("PusherSurface");
                    physMat.dynamicFriction = 0.8f;
                    physMat.staticFriction = 0.9f;
                    physMat.bounciness = 0.1f;
                    physMat.frictionCombine = PhysicsMaterialCombine.Maximum;
                    physMat.bounceCombine = PhysicsMaterialCombine.Minimum;
                    col.material = physMat;
                }
        
                Debug.Log($"Configured collider on pusher: {col.GetType().Name}");
            }
    
            // Ensure we have a rigidbody for the platform
            var rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
    
            // Configure rigidbody for kinematic movement
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
        
        private void FixedUpdate()
        {
            // Calculate pusher velocity
            _velocity = (transform.position - _lastPosition) / Time.fixedDeltaTime;
            _lastPosition = transform.position;
            
            // Detect toys in various zones
            DetectToysInZones();
            
            // Apply forces based on pusher movement and toy positions
            ApplyPusherForces();
        }
        
        private void DetectToysInZones()
        {
            _detectedToys.Clear();
            
            var allToys = FindObjectsByType<SoftBodyPhysics>(FindObjectsSortMode.None);
            
            foreach (var toy in allToys)
            {
                if (!toy.enabled || !toy.gameObject.activeInHierarchy) continue;
                
                var toyPos = toy.transform.position;
                var pusherPos = transform.position;
                var bounds = _pusherBounds;
                
                // Check if toy is in any of our interaction zones
                var inPushZone = IsInPushZone(toyPos, pusherPos, bounds);
                var inCarryZone = IsInCarryZone(toyPos, pusherPos, bounds);
                var inSideZone = IsInSideZone(toyPos, pusherPos, bounds);
                
                if (inPushZone || inCarryZone || inSideZone)
                {
                    _detectedToys.Add(toy);
                }
            }
        }
        
        
        private bool IsInCarryZone(Vector3 toyPos, Vector3 pusherPos, Bounds bounds)
        {
            // Zone on top of pusher for carrying
            var topCenter = pusherPos + Vector3.up * (bounds.extents.y + carryZoneHeight * 0.5f);
            var carryBounds = new Bounds(topCenter, new Vector3(
                bounds.size.x * 0.9f, // Slightly smaller than pusher
                carryZoneHeight,
                bounds.size.z * 0.9f
            ));
            
            return carryBounds.Contains(toyPos);
        }
        
        private bool IsInSideZone(Vector3 toyPos, Vector3 pusherPos, Bounds bounds)
        {
            // Zones on the sides for gentle nudging
            var sideDist = Mathf.Abs(toyPos.x - pusherPos.x);
            var frontDist = toyPos.z - pusherPos.z;
            
            return sideDist > bounds.extents.x && 
                   sideDist < bounds.extents.x + sideDetectionWidth &&
                   frontDist > -bounds.extents.z &&
                   frontDist < bounds.extents.z + frontDetectionDistance;
        }
        
        private void ApplyPusherForces()
        {
            foreach (var toy in _detectedToys)
            {
                if (toy.IsAsleep)
                {
                    toy.WakeUp();
                }
                
                var toyPos = toy.transform.position;
                var pusherPos = transform.position;
                var relativePos = toyPos - pusherPos;
                
                // Determine which zone the toy is in and apply appropriate forces
                if (enableCarrying && IsInCarryZone(toyPos, pusherPos, _pusherBounds))
                {
                    ApplyCarryingForce(toy, relativePos);
                }
                else if (enableGentlePushing && IsInPushZone(toyPos, pusherPos, _pusherBounds))
                {
                    ApplyPushingForce(toy, relativePos);
                }
                else if (IsInSideZone(toyPos, pusherPos, _pusherBounds))
                {
                    ApplySideForce(toy, relativePos);
                }
            }
        }
        
        private void ApplySideForce(SoftBodyPhysics toy, Vector3 relativePos)
        {
            // Gentle side nudging to move toys away from pusher edges
            var sideDirection = Vector3.Cross(transform.forward, Vector3.up);
            if (relativePos.x < 0) sideDirection = -sideDirection;
            
            var force = sideDirection * sideForce;
            toy.ApplyContinuousForce(toy.transform.position, force, 0.8f);
        }
        
        private Vector3 GetPushDirection()
        {
            if (autoDetectDirection)
            {
                // Use the direction the pusher is actually moving
                if (_velocity.magnitude > 0.01f)
                {
                    return _velocity.normalized;
                }
                // Fallback to transform forward
                return transform.forward;
            }
            return manualPushDirection.normalized;
        }

        private bool IsInPushZone(Vector3 toyPos, Vector3 pusherPos, Bounds bounds)
        {
            // Get the actual push direction
            var pushDir = GetPushDirection();
    
            // Zone in the direction we're pushing
            var frontCenter = pusherPos + pushDir * (bounds.extents.z + pushZoneDepth * 0.5f);
            var pushBounds = new Bounds(frontCenter, new Vector3(
                bounds.size.x + sideDetectionWidth,
                bounds.size.y,
                pushZoneDepth
            ));
    
            return pushBounds.Contains(toyPos);
        }

        private void ApplyCarryingForce(SoftBodyPhysics toy, Vector3 relativePos)
        {
            var pusherMovement = _velocity;
    
            // Moderate horizontal forces - not too weak, not too strong
            var horizontalForce = new Vector3(pusherMovement.x, 0, pusherMovement.z) * (carryForce * 0.5f); // Reduced from 2f
    
            var toyBounds = toy.GetComponent<MeshFilter>()?.mesh?.bounds ?? new Bounds(Vector3.zero, Vector3.one);
            var toyCenter = toy.transform.position;
    
            // Apply to fewer points with gentler forces
            toy.ApplyContinuousForce(toyCenter, horizontalForce, 0.8f);
    
            // Very gentle upward support
            var supportForce = Vector3.up * (carryForce * 0.1f); // Reduced from 0.3f
            toy.ApplyContinuousForce(toyCenter, supportForce, 0.3f);
        }

        private void ApplyPushingForce(SoftBodyPhysics toy, Vector3 relativePos)
        {
            var pushDirection = GetPushDirection();
    
            var pusherSpeed = Vector3.Dot(_velocity, pushDirection);
            var forceMultiplier = Mathf.Clamp01(pusherSpeed / maxPushSpeed);
    
            if (forceMultiplier > 0.05f)
            {
                var force = pushDirection * (pushForce * 1.2f * forceMultiplier); // Reduced from 3f
        
                var toyCenter = toy.transform.position;
        
                // Single application point with moderate force
                toy.ApplyContinuousForce(toyCenter, force, 1.0f);
            }
        }
        
        private void OnDrawGizmos()
        {
            if (!showGizmos) return;
    
            var bounds = _pusherBounds;
            if (!Application.isPlaying)
            {
                var renderer = GetComponent<Renderer>();
                if (renderer != null) bounds = renderer.bounds;
            }
    
            // Get push direction
            var pushDir = Vector3.forward; // Default
            if (Application.isPlaying)
            {
                pushDir = GetPushDirection();
            }
            else if (!autoDetectDirection)
            {
                pushDir = manualPushDirection.normalized;
            }
    
            // Draw push zone
            Gizmos.color = pushZoneColour;
            var pushCenter = transform.position + pushDir * (bounds.extents.z + pushZoneDepth * 0.5f);
            var pushSize = new Vector3(bounds.size.x + sideDetectionWidth, bounds.size.y, pushZoneDepth);
            Gizmos.DrawCube(pushCenter, pushSize);
    
            // Draw carry zone (on top)
            if (enableCarrying)
            {
                Gizmos.color = carryZoneColour;
                var carryCenter = transform.position + Vector3.up * (bounds.extents.y + carryZoneHeight * 0.5f);
                var carrySize = new Vector3(bounds.size.x * 0.9f, carryZoneHeight, bounds.size.z * 0.9f);
                Gizmos.DrawCube(carryCenter, carrySize);
            }
    
            // Draw directional arrow showing ACTUAL push direction
            Gizmos.color = Color.red;
            var arrowStart = transform.position;
            var arrowEnd = arrowStart + pushDir * 2f;
            Gizmos.DrawLine(arrowStart, arrowEnd);
    
            // Draw arrow head
            var right = Vector3.Cross(pushDir, Vector3.up) * 0.3f;
            Gizmos.DrawLine(arrowEnd, arrowEnd - pushDir * 0.3f + right);
            Gizmos.DrawLine(arrowEnd, arrowEnd - pushDir * 0.3f - right);
    
            // Label the direction in scene view
#if UNITY_EDITOR
            UnityEditor.Handles.Label(arrowEnd, $"Push: {pushDir:F2}");
#endif
        }
        
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;
            
            // Show detected toys
            Gizmos.color = Color.yellow;
            foreach (var toy in _detectedToys)
            {
                if (toy != null)
                {
                    Gizmos.DrawWireSphere(toy.transform.position, 0.2f);
                    Gizmos.DrawLine(transform.position, toy.transform.position);
                }
            }
            
            // Show velocity
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, _velocity);
        }
    }
}
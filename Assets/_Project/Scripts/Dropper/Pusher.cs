
using System.Collections.Generic;
using UnityEngine;

namespace SoftBody.Scripts
{
    public class Pusher : MonoBehaviour
   {
        [Header("Push Settings")]
        public float pushForce = 5f;
        public float influenceRadius = 1f;
        
        [Header("Push Direction")]
        public Vector3 pushDirection = Vector3.back; // -Z
        
        [Header("Influence Area")]
        public float forwardOffset = 0.5f;
        public float widthPadding = 0.5f;
        public float heightPadding = 1f;
        
        [Header("Performance")]
        [Tooltip("How often to search for soft bodies (per second)")]
        public float detectionRate = 10f;
        
        [Header("Debug")]
        public bool showDebugInfo = true;
        public Color gizmoColor = new Color(1f, 1f, 0f, 0.3f);
        
        private Vector3 _previousPosition;
        private Vector3 _velocity;
        private Bounds _pusherBounds;
        private float _nextDetectionTime;
        private List<SoftBodyPhysics> _nearbyBodies = new ();

        private void Start()
        {
            _previousPosition = transform.position;
            
            var rendererComponent = GetComponent<Renderer>();
            if (rendererComponent != null)
            {
                _pusherBounds = rendererComponent.bounds;
            }
            
            pushDirection.Normalize();
        }

        private void FixedUpdate()
        {
            // Calculate pusher velocity
            _velocity = (transform.position - _previousPosition) / Time.fixedDeltaTime;
            _previousPosition = transform.position;
            
            // Check if moving in push direction
            var pushVelocity = Vector3.Dot(_velocity, pushDirection);
            
            if (pushVelocity > 0.01f)
            {
                // Periodically update the list of nearby bodies
                if (Time.time >= _nextDetectionTime)
                {
                    DetectNearbyBodies();
                    _nextDetectionTime = Time.time + 1f / detectionRate;
                }
                
                // Apply forces to detected bodies
                ApplyPushForces();
            }
        }

        private void DetectNearbyBodies()
       {
    _nearbyBodies.Clear();
    
    // Get influence bounds
    var boxCenter = GetInfluenceBoxCenter();
    var boxSize = GetInfluenceBoxSize();
    var influenceBounds = new Bounds(boxCenter, boxSize);
    
    // Find all soft bodies
    var allSoftBodies = FindObjectsOfType<SoftBodyPhysics>();
    
    foreach (var softBody in allSoftBodies)
    {
        if (softBody.enabled && softBody.gameObject.activeInHierarchy)
        {
            // Get the actual mesh bounds of the soft body
            var meshFilter = softBody.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.mesh != null)
            {
                // Convert mesh bounds to world space
                var worldBounds = meshFilter.mesh.bounds;
                worldBounds.center = softBody.transform.TransformPoint(worldBounds.center);
                worldBounds.size = Vector3.Scale(worldBounds.size, softBody.transform.lossyScale);
                
                // Only add if bounds actually intersect
                if (influenceBounds.Intersects(worldBounds))
                {
                    // Additional check: how much overlap?
                    var overlapAmount = CalculateOverlapAmount(influenceBounds, worldBounds);
                    
                    if (overlapAmount > 0.1f) // Only if significantly overlapping
                    {
                        _nearbyBodies.Add(softBody);
                        
                        if (showDebugInfo)
                        {
                            Debug.Log($"Detected {softBody.name} with {overlapAmount:F1} overlap");
                        }
                    }
                }
            }
            else
            {
                // Fallback for bodies without mesh
                var distance = Vector3.Distance(softBody.transform.position, boxCenter);
                var maxDistance = influenceRadius * 0.5f; // Much stricter
                
                if (distance < maxDistance)
                {
                    _nearbyBodies.Add(softBody);
                }
            }
        }
    }
    
    if (showDebugInfo && _nearbyBodies.Count > 0)
    {
        Debug.Log($"Pusher detected {_nearbyBodies.Count} soft bodies in range");
    }
}

// Helper method to calculate overlap amount
float CalculateOverlapAmount(Bounds a, Bounds b)
{
    // Calculate how deeply B penetrates into A
    var aMin = a.min;
    var aMax = a.max;
    var bMin = b.min;
    var bMax = b.max;
    
    // Find overlap on each axis
    var overlapX = Mathf.Min(aMax.x, bMax.x) - Mathf.Max(aMin.x, bMin.x);
    var overlapY = Mathf.Min(aMax.y, bMax.y) - Mathf.Max(aMin.y, bMin.y);
    var overlapZ = Mathf.Min(aMax.z, bMax.z) - Mathf.Max(aMin.z, bMin.z);
    
    if (overlapX > 0 && overlapY > 0 && overlapZ > 0)
    {
        // Return the minimum overlap (most relevant for pushing)
        return Mathf.Min(overlapX, overlapY, overlapZ);
    }
    
    return 0;
}

// Updated ApplyPushForces() with distance-based force falloff
       void ApplyPushForces()
       {
           var pushedCount = 0;

           var influenceCenter = GetInfluenceBoxCenter();

           foreach (var softBody in _nearbyBodies)
           {
               if (softBody == null) continue;

               // Calculate actual distance from influence zone
               var closestPoint = GetClosestPointToSoftBody(softBody, influenceCenter);
               var distance = Vector3.Distance(closestPoint, influenceCenter);

               // Only push if really close enough
               if (distance > influenceRadius)
               {
                   continue; // Skip this body
               }

               // Wake sleeping bodies
               if (softBody.IsAsleep)
               {
                   softBody.WakeUp();
                   Debug.Log($"Woke up {softBody.name}");
               }

               // Calculate force with falloff based on actual distance
               var distanceFactor = 1f - (distance / influenceRadius);
               distanceFactor = Mathf.Clamp01(distanceFactor);

               // Only apply force if factor is significant
               if (distanceFactor < 0.1f) continue;

               // Calculate push direction
               var toTarget = softBody.transform.position - transform.position;
               var lateralOffset = toTarget.x / (_pusherBounds.size.x + widthPadding);

               var pushDir = pushDirection;
               pushDir += Vector3.right * (lateralOffset * 0.2f);
               pushDir += Vector3.up * 0.1f;
               pushDir.Normalize();

               // Apply force scaled by distance
               var pushSpeed = Vector3.Dot(_velocity, pushDirection);
               var forceMagnitude = pushForce * pushSpeed * distanceFactor; // Scale by distance

               // Apply at closest point for most accurate physics
               softBody.ApplyContinuousForce(closestPoint, pushDir * forceMagnitude, 1.0f);

               pushedCount++;

               if (showDebugInfo)
               {
                   Debug.Log($"Pushing {softBody.name} at distance {distance:F2} with factor {distanceFactor:F2}");
               }
           }

           if (showDebugInfo && pushedCount > 0)
           {
               Debug.Log($"Actually pushing {pushedCount} bodies");
           }
       }

// Helper to find closest point on soft body to influence center
       Vector3 GetClosestPointToSoftBody(SoftBodyPhysics softBody, Vector3 point)
       {
           // Use mesh bounds as approximation
           var meshFilter = softBody.GetComponent<MeshFilter>();
           if (meshFilter && meshFilter.mesh)
           {
               var worldBounds = meshFilter.mesh.bounds;
               worldBounds.center = softBody.transform.TransformPoint(worldBounds.center);
               worldBounds.size = Vector3.Scale(worldBounds.size, softBody.transform.lossyScale);

               return worldBounds.ClosestPoint(point);
           }

           // Fallback to transform position
           return softBody.transform.position;
       }
       
        private Vector3 GetInfluenceBoxCenter()
        {
            var boxOffset = _pusherBounds.extents.z + forwardOffset + influenceRadius * 0.5f;
            return transform.position + pushDirection * boxOffset;
        }
        
        private Vector3 GetInfluenceBoxSize()
        {
            return new Vector3(
                _pusherBounds.size.x + widthPadding * 2f,
                _pusherBounds.size.y + heightPadding * 2f,
                influenceRadius
            );
        }

        private void OnDrawGizmos()
        {
            // Update bounds if in editor
            if (!Application.isPlaying)
            {
                var component = GetComponent<Renderer>();
                if (component != null)
                {
                    _pusherBounds = component.bounds;
                }
                else
                {
                    _pusherBounds = new Bounds(Vector3.zero, transform.localScale);
                }
                
                // Ensure push direction is normalized
                if (pushDirection.magnitude > 0)
                    pushDirection.Normalize();
            }
            
            // Draw influence area
            Gizmos.color = gizmoColor;
            var boxCenter = GetInfluenceBoxCenter();
            var boxSize = GetInfluenceBoxSize();
            
            // Transform to world space for rendering
            var oldMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(boxCenter, transform.rotation, Vector3.one);
            
            // Solid box
            Gizmos.DrawCube(Vector3.zero, boxSize);
            
            // Wireframe for clarity
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
            Gizmos.DrawWireCube(Vector3.zero, boxSize);
            
            Gizmos.matrix = oldMatrix;
            
            // Draw push direction arrow
            if (showDebugInfo)
            {
                Gizmos.color = Color.red;
                var arrowStart = transform.position;
                var arrowEnd = arrowStart + pushDirection * 2f;
                Gizmos.DrawLine(arrowStart, arrowEnd);
                
                // Draw cone for arrow head
                DrawArrowHead(arrowEnd, pushDirection);
            }
        }
        
        private void DrawArrowHead(Vector3 tip, Vector3 direction)
        {
            var coneSize = 0.2f;
            var coneBase = tip - direction * coneSize;
            
            // Create perpendicular vectors
            var perp1 = Vector3.Cross(direction, Vector3.up);
            if (perp1.magnitude < 0.1f)
                perp1 = Vector3.Cross(direction, Vector3.right);
            perp1.Normalize();
            
            var perp2 = Vector3.Cross(direction, perp1).normalized;
            
            // Draw cone
            Gizmos.DrawLine(tip, coneBase + perp1 * coneSize);
            Gizmos.DrawLine(tip, coneBase - perp1 * coneSize);
            Gizmos.DrawLine(tip, coneBase + perp2 * coneSize);
            Gizmos.DrawLine(tip, coneBase - perp2 * coneSize);
        }

        private void OnDrawGizmosSelected()
        {
            // Show more detail when selected
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, _pusherBounds.size);
            
            // Show current velocity when playing
            if (Application.isPlaying && showDebugInfo)
            {
                Gizmos.color = Color.magenta;
                foreach (var body in _nearbyBodies)
                {
                    if (body != null)
                    {
                        // Draw line from pusher to detected body
                        Gizmos.DrawLine(transform.position, body.transform.position);
                
                        // Draw sphere at detection point
                        Gizmos.DrawWireSphere(body.transform.position, 0.1f);
                    }
                }
            }
            
            // Show labels
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(
                GetInfluenceBoxCenter() + Vector3.up, 
                $"Push Force: {pushForce}\n" +
                $"Influence: {influenceRadius}m\n" +
                $"Direction: {pushDirection}"
            );
            #endif
        }
    }
}
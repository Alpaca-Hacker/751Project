using System.Collections.Generic;
using SoftBody.Scripts.Core;
using SoftBody.Scripts.Models;
using UnityEngine;

namespace SoftBody.Scripts.Tools
{
    /// <summary>
    /// Controls a hydraulic press tool to crush a target soft body.
    /// Requires visual objects for the plates and a target SoftBodyPhysics instance.
    /// </summary>
    public class CrusherToolController : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The soft body to be crushed by this tool.")]
        public SoftBodyPhysics targetSoftBody;

        [Header("Press Components")]
        [Tooltip("The visual Transform for the top plate of the press.")]
        public Transform topPlate;
        [Tooltip("The visual Transform for the bottom plate of the press.")]
        public Transform bottomPlate;

        [Header("Control Settings")]
        [Tooltip("How fast the top plate moves in response to mouse input.")]
        public float moveSpeed = 2.0f;
        [Tooltip("The minimum allowed gap between the two plates.")]
        public float minGap = 0.2f;

        private readonly List<SDFCollider> _pressColliders = new List<SDFCollider>(2);
        private float _initialTopPlateY;

        private void Start()
        {
            if (topPlate != null)
            {
                _initialTopPlateY = topPlate.position.y;
            }
        }

        private void Update()
        {
            if (targetSoftBody == null || topPlate == null || bottomPlate == null)
            {
                return;
            }

            HandleInput();
            UpdatePhysicsColliders();
        }

        private void HandleInput()
        {
            // Move the press only when the left mouse button is held down
            if (Input.GetMouseButton(0))
            {
                targetSoftBody.WakeUp();
                
                var mouseY = Input.GetAxis("Mouse Y");
                
                // Calculate the intended displacement for this frame
                var displacement = mouseY * moveSpeed * Time.deltaTime;
 
                // Limit the maximum downward displacement to prevent physics explosions
                const float maxFrameDisplacement = 0.05f;
                if (displacement > maxFrameDisplacement)
                {
                    displacement = maxFrameDisplacement;
                }
                
                var newPosition = topPlate.position;
 
                // Move top plate based on the (potentially clamped) displacement
                newPosition.y -= displacement;
                
                // Clamp the position to prevent it from going through the bottom plate or too high
                var bottomLimit = bottomPlate.position.y + minGap;
                newPosition.y = Mathf.Clamp(newPosition.y, bottomLimit, _initialTopPlateY);
                
                topPlate.position = newPosition;
            }
        }
        
        /// <summary>
        /// Creates SDF colliders from the press plates and sends them to the collision system.
        /// </summary>
        private void UpdatePhysicsColliders()
        {
            _pressColliders.Clear();
            
            // Create an SDF box collider for the top plate
            var topSdf = SDFCollider.CreateBox(
                topPlate.position, 
                topPlate.lossyScale * 0.5f, // Assumes a primitive cube, using scale as size
                topPlate.rotation);
            _pressColliders.Add(topSdf);
            
            // Create an SDF box collider for the bottom plate
            var bottomSdf = SDFCollider.CreateBox(
                bottomPlate.position, 
                bottomPlate.lossyScale * 0.5f, // Assumes a primitive cube, using scale as size
                bottomPlate.rotation);
            _pressColliders.Add(bottomSdf);
            
            if (targetSoftBody.CollisionSystem != null)
            {
                targetSoftBody.CollisionSystem.SetDynamicColliders(_pressColliders);
            }
        }
    }
}

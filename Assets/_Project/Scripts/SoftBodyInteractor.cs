
using UnityEngine;
namespace SoftBody.Scripts
{
    public class SoftBodyInteractor : MonoBehaviour
    {
        [Tooltip("The soft body to interact with.")]
        [SerializeField] private SoftBody.Scripts.SoftBodyPhysics targetSoftBody;

        [Tooltip("The strength of the poke.")]
        [SerializeField] private float pokeStrength = 5f;

        private Camera mainCamera;

        private void Start()
        {
            mainCamera = Camera.main;
            if (targetSoftBody == null)
            {
                // Try to find it automatically if not assigned
                targetSoftBody = FindObjectOfType<SoftBody.Scripts.SoftBodyPhysics>();
            }
        }

        private void Update()
        {
            if (!targetSoftBody) return;

            // Check for a left mouse button click
            if (Input.GetMouseButtonDown(0))
            {
                var ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            
                // We don't need to hit anything, we just need a point in space to aim at.
                // Let's aim at a point 10 meters away along the ray.
                var pokePosition = ray.GetPoint(10);
            
                // The direction of the poke is the direction of the camera's ray
                var pokeDirection = ray.direction;

                // Calculate the impulse vector
                var impulse = pokeDirection * pokeStrength;

                // Call the public method on our soft body
                targetSoftBody.PokeParticle(pokePosition, impulse);
            }
        }
    }
}
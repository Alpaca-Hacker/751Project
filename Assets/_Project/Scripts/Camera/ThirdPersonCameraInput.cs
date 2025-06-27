using Unity.Cinemachine;
using UnityEngine;

namespace SoftBody.Scripts
{
    public class ThirdPersonCameraInput : MonoBehaviour
    {
        [Header("Third Person Settings")]
        public float rotationSpeed = 100f;
        public float zoomSpeed = 3f;
        public float minDistance = 1f;
        public float maxDistance = 8f;
    
        private CinemachineThirdPersonFollow _thirdPersonFollow;
        private Transform _target;

        private void Start()
        {
            _thirdPersonFollow = GetComponent<CinemachineThirdPersonFollow>();
            var cmCamera = GetComponent<CinemachineCamera>();
            _target = cmCamera.Follow;
        }

        private void Update()
        {
            if (!_thirdPersonFollow || !_target) return;
        
            var cmCamera = GetComponent<CinemachineCamera>();
            if (cmCamera.Priority <= 0) return;
        
            // Simple rotation around target
            if (Input.GetMouseButton(1))
            {
                var mouseX = Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;
                var mouseY = Input.GetAxis("Mouse Y") * rotationSpeed * Time.deltaTime;
            
                transform.RotateAround(_target.position, Vector3.up, mouseX);
                transform.RotateAround(_target.position, transform.right, -mouseY);
            }
        
            // Zoom by changing camera distance
            var scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                _thirdPersonFollow.CameraDistance = Mathf.Clamp(
                    _thirdPersonFollow.CameraDistance - scroll * zoomSpeed, 
                    minDistance, maxDistance);
            }
        }
    }
}
using UnityEngine;

namespace SoftBody.Scripts
{
    public class HybridCamera : MonoBehaviour
    {
        [Header("Target")] public Transform target;

        [Header("Orbit Mode")] public float orbitDistance = 5f;
        public float orbitSpeed = 120f;
        public float zoomSpeed = 5f;
        public float minDistance = 2f;
        public float maxDistance = 15f;

        [Header("Free Mode")] public float freeSpeed = 5f;
        public float fastSpeed = 10f;
        public float mouseSensitivity = 2f;

        [Header("Mode Switching")] public KeyCode switchModeKey = KeyCode.Tab;

        private bool _isOrbitMode = true;
        private float _x;
        private float _y;
        private Vector3 _freeVelocity;

        private void Start()
        {
            if (target == null)
            {
                var softBody = FindFirstObjectByType<SoftBodyPhysics>();
                if (softBody != null)
                    target = softBody.transform;
            }

            var angles = transform.eulerAngles;
            _x = angles.y;
            _y = angles.x;
        }

        private void Update()
        {
            // Switch modes
            if (Input.GetKeyDown(switchModeKey))
            {
                _isOrbitMode = !_isOrbitMode;
                Debug.Log("Camera mode: " + (_isOrbitMode ? "Orbit" : "Free"));
            }

            if (_isOrbitMode && target)
            {
                UpdateOrbitMode();
            }
            else
            {
                UpdateFreeMode();
            }
        }

        private void UpdateOrbitMode()
        {
            // Right click to orbit
            if (Input.GetMouseButton(1))
            {
                _x += Input.GetAxis("Mouse X") * orbitSpeed * 0.02f;
                _y -= Input.GetAxis("Mouse Y") * orbitSpeed * 0.02f;
                _y = Mathf.Clamp(_y, -80f, 80f);
            }

            // Scroll to zoom
            orbitDistance -= Input.GetAxis("Mouse ScrollWheel") * zoomSpeed;
            orbitDistance = Mathf.Clamp(orbitDistance, minDistance, maxDistance);

            // Position camera
            var rotation = Quaternion.Euler(_y, _x, 0);
            var position = rotation * new Vector3(0, 0, -orbitDistance) + target.position;

            transform.rotation = rotation;
            transform.position = position;
        }

        private void UpdateFreeMode()
        {
            // Right click to look around
            if (Input.GetMouseButton(1))
            {
                _x += Input.GetAxis("Mouse X") * mouseSensitivity;
                _y -= Input.GetAxis("Mouse Y") * mouseSensitivity;
                _y = Mathf.Clamp(_y, -90f, 90f);

                transform.rotation = Quaternion.Euler(_y, _x, 0);
            }

            // WASD movement
            var input = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) input += Vector3.forward;
            if (Input.GetKey(KeyCode.S)) input += Vector3.back;
            if (Input.GetKey(KeyCode.A)) input += Vector3.left;
            if (Input.GetKey(KeyCode.D)) input += Vector3.right;
            if (Input.GetKey(KeyCode.Q)) input += Vector3.down;
            if (Input.GetKey(KeyCode.E)) input += Vector3.up;

            var speed = Input.GetKey(KeyCode.LeftShift) ? fastSpeed : freeSpeed;
            var movement = transform.TransformDirection(input) * (speed * Time.deltaTime);
            transform.position += movement;
        }

        // private void OnGUI()
        // {
        //     GUI.Label(new Rect(10, 10, 200, 20),
        //         $"Camera Mode: {(isOrbitMode ? "Orbit" : "Free")} (Tab to switch)");
        //
        //     if (isOrbitMode)
        //     {
        //         GUI.Label(new Rect(10, 30, 200, 20), "Right-click + drag to orbit");
        //         GUI.Label(new Rect(10, 50, 200, 20), "Scroll wheel to zoom");
        //     }
        //     else
        //     {
        //         GUI.Label(new Rect(10, 30, 200, 20), "WASD/QE to move");
        //         GUI.Label(new Rect(10, 50, 200, 20), "Right-click + drag to look");
        //         GUI.Label(new Rect(10, 70, 200, 20), "Hold Shift for fast movement");
        //     }
        // }
    }
}
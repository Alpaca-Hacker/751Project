using Unity.Cinemachine;
using UnityEngine;

namespace SoftBody.Scripts
{
    public class FreeLookCameraInput : MonoBehaviour
    {
        [Header("Free Look Settings")] 
        public float lookSpeed = 2f;
        public float moveSpeed = 5f;
        public float fastMoveSpeed = 10f;

        [Header("Target Reference")] 
        public Transform softBodyTarget;

        private Vector3 _rotation = Vector3.zero;
        private bool _wasActive;

        private void Start()
        {
            // Find soft body if not assigned
            if (softBodyTarget == null)
            {
                var softBody = FindFirstObjectByType<SoftBodyPhysics>();
                if (softBody != null)
                    softBodyTarget = softBody.transform;
            }

            _rotation = transform.eulerAngles;
        }

        private void Update()
        {
            var cmCamera = GetComponent<CinemachineCamera>();
            var isActive = cmCamera.Priority > 0;

            if (!isActive)
            {
                _wasActive = false;
                return;
            }

            // Initialize position when camera becomes active
            if (!_wasActive)
            {
                InitializeFreeLookPosition();
                _wasActive = true;
            }

            HandleFreeLookInput();
        }

        private void InitializeFreeLookPosition()
        {
            if (softBodyTarget)
            {
                var offset = new Vector3(0, 2, -5);
                transform.position = softBodyTarget.position + offset;
                transform.LookAt(softBodyTarget.position);
                _rotation = transform.eulerAngles;
            }
        }

        private void HandleFreeLookInput()
        {
            // Mouse look when right-clicking
            if (Input.GetMouseButton(1))
            {
                Cursor.lockState = CursorLockMode.Locked;

                var mouseX = Input.GetAxis("Mouse X") * lookSpeed;
                var mouseY = Input.GetAxis("Mouse Y") * lookSpeed;

                _rotation.y += mouseX;
                _rotation.x -= mouseY;
                _rotation.x = Mathf.Clamp(_rotation.x, -90f, 90f);

                transform.eulerAngles = _rotation;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
            }

            // WASD movement
            var input = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) input += Vector3.forward;
            if (Input.GetKey(KeyCode.S)) input += Vector3.back;
            if (Input.GetKey(KeyCode.A)) input += Vector3.left;
            if (Input.GetKey(KeyCode.D)) input += Vector3.right;
            if (Input.GetKey(KeyCode.Q)) input += Vector3.down;
            if (Input.GetKey(KeyCode.E)) input += Vector3.up;

            if (input.magnitude > 0)
            {
                var speed = Input.GetKey(KeyCode.LeftShift) ? fastMoveSpeed : moveSpeed;
                var movement = transform.TransformDirection(input.normalized) * (speed * Time.deltaTime);
                transform.position += movement;
            }

            // Quick zoom with scroll
            var scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                transform.Translate(Vector3.forward * (scroll * 3f));
            }
        }

        // private void OnGUI()
        // {
        //     var cmCamera = GetComponent<CinemachineCamera>();
        //     if (cmCamera.Priority <= 0) return;
        //
        //     GUILayout.BeginArea(new Rect(Screen.width - 250, 10, 240, 120));
        //     GUILayout.Label("Free Look Camera:", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
        //     GUILayout.Label("Right-click + drag: Look around");
        //     GUILayout.Label("WASD: Move horizontally");
        //     GUILayout.Label("Q/E: Move up/down");
        //     GUILayout.Label("Shift: Fast movement");
        //     GUILayout.Label("Mouse wheel: Quick forward/back");
        //     GUILayout.EndArea();
        // }
    }
}
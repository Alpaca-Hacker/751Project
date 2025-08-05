using Unity.Cinemachine;
using UnityEngine;

namespace SoftBody.Scripts
{
    public class OrbitalCameraInput : MonoBehaviour
    {
        [Header("Camera Control")] public float rotationSpeed = 2f;
        public float zoomSpeed = 2f;
        public float minZoom = 2f;
        public float maxZoom = 15f;

        private CinemachineOrbitalFollow _orbital;

        private void Start()
        {
            _orbital = GetComponent<CinemachineOrbitalFollow>();

            if (_orbital == null)
            {
                Debug.LogError("No CinemachineOrbitalFollow found on " + gameObject.name);
            }
            else
            {
                Debug.Log("Found orbital component successfully");
            }
        }

        private void Update()
        {
            if (!_orbital) return;

            HandleInput();
        }

        private void HandleInput()
        {
            // Right mouse button for camera control
            if (Input.GetMouseButton(1))
            {
                // Horizontal rotation (around Y axis)
                var mouseX = Input.GetAxis("Mouse X") * rotationSpeed;
                _orbital.HorizontalAxis.Value += mouseX;

                // Vertical rotation (up/down)
                var mouseY = Input.GetAxis("Mouse Y") * rotationSpeed;
                _orbital.VerticalAxis.Value -= mouseY; // Negative for natural feel

                // Clamp vertical rotation
                _orbital.VerticalAxis.Value = Mathf.Clamp(_orbital.VerticalAxis.Value, -80f, 80f);
            }

            // Mouse wheel for zoom
            var scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                var newRadius = _orbital.Radius - scroll * zoomSpeed;
                _orbital.Radius = Mathf.Clamp(newRadius, minZoom, maxZoom);
            }
        }
        

        // private void OnGUI()
        // {
        //     GUILayout.BeginArea(new Rect(10, 10, 300, 100));
        //     GUILayout.Label("Camera Controls:", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
        //     GUILayout.Label("Right-click + drag: Rotate camera");
        //     GUILayout.Label("Mouse wheel: Zoom");
        //     GUILayout.Label("Left-click: Interact with soft body");
        //     if (_orbital)
        //     {
        //         GUILayout.Label($"Radius: {_orbital.Radius:F1}");
        //     }
        //
        //     GUILayout.EndArea();
        // }
    }
}
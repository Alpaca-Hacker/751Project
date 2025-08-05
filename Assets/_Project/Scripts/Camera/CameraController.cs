using Unity.Cinemachine;
using UnityEngine;

namespace SoftBody.Scripts
{
    public class CameraController : MonoBehaviour
    {

        [Header("Cameras")]
        public CinemachineCamera orbitalCamera;
        public CinemachineCamera thirdPersonCamera;
        public CinemachineCamera freeLookCamera;
    
        [Header("Settings")]
        public KeyCode switchKey = KeyCode.Tab;
    
        private CinemachineCamera[] _cameras;
        private int _currentCameraIndex;
        private readonly string[] _cameraNames = { "Orbital", "Third Person", "Free Look" };
    
        private void Start()
        {
            _cameras = new[] { orbitalCamera, thirdPersonCamera, freeLookCamera };
            SwitchToCamera(0);
        }
    
        private void Update()
        {
            if (Input.GetKeyDown(switchKey))
            {
                SwitchToNextCamera();
            }
            
            if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchToCamera(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchToCamera(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchToCamera(2);
        }
    
        private void SwitchToNextCamera()
        {
            _currentCameraIndex = (_currentCameraIndex + 1) % _cameras.Length;
            SwitchToCamera(_currentCameraIndex);
        }
    
        private void SwitchToCamera(int index)
        {
            if (index < 0 || index >= _cameras.Length) return;
        
            _currentCameraIndex = index;
        
            // Enable only the selected camera
            for (var i = 0; i < _cameras.Length; i++)
            {
                if (_cameras[i])
                {
                    _cameras[i].gameObject.SetActive(i == index);
                    _cameras[i].Priority = i == index ? 10 : 0; // Set priority for active camera
                }
            }
        
            Debug.Log($"Switched to {_cameraNames[index]} camera");
        }
    
        // private void OnGUI()
        // {
        //     if (_currentCameraIndex < _cameraNames.Length)
        //     {
        //         GUI.Label(new Rect(10, 10, 300, 20), 
        //             $"Camera: {_cameraNames[_currentCameraIndex]} (Tab/1-3 to switch)");
        //     }
        // }
    }
}

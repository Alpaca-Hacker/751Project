using UnityEngine;

namespace SoftBody.Scripts.UI
{
    public class HelpPanelController : MonoBehaviour
    {
        private System.Action _closeCallback;
        
        public void Initialize(System.Action onClose)
        {
            _closeCallback = onClose;
        }
        
        private void Update()
        {
            // Close help panel with Escape key
            if (Input.GetKeyDown(KeyCode.Escape) && gameObject.activeInHierarchy)
            {
                _closeCallback?.Invoke();
            }
            
            // Close help panel when clicking outside (optional)
            if (Input.GetMouseButtonDown(0) && gameObject.activeInHierarchy)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(
                        GetComponent<RectTransform>(), 
                        Input.mousePosition))
                {
                    _closeCallback?.Invoke();
                }
            }
        }
    }
}
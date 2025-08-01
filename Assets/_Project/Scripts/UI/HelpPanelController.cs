using UnityEngine;

namespace SoftBody.Scripts.UI
{
    public class HelpPanelController : MonoBehaviour
    {
        private System.Action closeCallback;
        
        public void Initialize(System.Action onClose)
        {
            closeCallback = onClose;
        }
        
        private void Update()
        {
            // Close help panel with Escape key
            if (Input.GetKeyDown(KeyCode.Escape) && gameObject.activeInHierarchy)
            {
                closeCallback?.Invoke();
            }
            
            // Close help panel when clicking outside (optional)
            if (Input.GetMouseButtonDown(0) && gameObject.activeInHierarchy)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(
                        GetComponent<RectTransform>(), 
                        Input.mousePosition))
                {
                    closeCallback?.Invoke();
                }
            }
        }
    }
}
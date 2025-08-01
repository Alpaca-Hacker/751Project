using UnityEngine;

namespace SoftBody.Scripts.UI
{
    public class SceneConfiguration : MonoBehaviour
    {
        [Header("Scene Information")]
        public string sceneDisplayName;
        public string sceneDescription;
        [TextArea(3, 6)]
        public string instructions;
        [TextArea(5, 10)]
        public string detailedHelp;
        
        [Header("Scene Settings")]
        public bool showPerformanceStats = true;
        public bool allowReset = true;
        public Color themeColor = Color.blue;
        
        private static SceneConfiguration _instance;
        public static SceneConfiguration Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindFirstObjectByType<SceneConfiguration>();
                return _instance;
            }
        }
        
        private void Awake()
        {
            if (_instance == null)
                _instance = this;
        }
    }
}
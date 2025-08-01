using AdvancedSceneManager.Models;
using UnityEngine;

namespace SoftBody.Scripts.UI
{
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Scene References")]
        public SceneCollection pusherDemoCollection;
        public SceneCollection proceduralCubeDemoCollection; 
        public SceneCollection singleToyDemoCollection;
        public SceneCollection materialDemoCollection;
        public SceneCollection constraintDemoCollection;
        
        [Header("UI References")]
        public UnityEngine.UI.Button pusherDemoButton;
        public UnityEngine.UI.Button proceduralCubeButton;
        public UnityEngine.UI.Button singleToyButton;
        public UnityEngine.UI.Button materialDemoButton;
        public UnityEngine.UI.Button constraintDemoButton;
        public UnityEngine.UI.Button exitButton;
        
        [Header("Demo Descriptions")]
        public TMPro.TextMeshProUGUI demoDescriptionText;
        
        private void Start()
        {
            SetupButtons();
            ShowDefaultDescription();
        }
        
        private void SetupButtons()
        {
            // Scene loading buttons
            pusherDemoButton.onClick.AddListener(() => LoadDemo(pusherDemoCollection, "Coin Pusher Demo"));
            proceduralCubeButton.onClick.AddListener(() => LoadDemo(proceduralCubeDemoCollection, "Procedural Cube Demo"));
            singleToyButton.onClick.AddListener(() => LoadDemo(singleToyDemoCollection, "Interactive Toy Demo"));
            materialDemoButton.onClick.AddListener(() => LoadDemo(materialDemoCollection, "Physics Material Demo"));
            constraintDemoButton.onClick.AddListener(() => LoadDemo(constraintDemoCollection, "Constraint Types Demo"));
            
            // Hover descriptions
            AddHoverDescription(pusherDemoButton, GetPusherDescription());
            AddHoverDescription(proceduralCubeButton, GetProceduralCubeDescription());
            AddHoverDescription(singleToyButton, GetSingleToyDescription());
            AddHoverDescription(materialDemoButton, GetMaterialDemoDescription());
            AddHoverDescription(constraintDemoButton, GetConstraintDemoDescription());
            
            // Exit button
            exitButton.onClick.AddListener(Application.Quit);
        }
        
        private void LoadDemo(SceneCollection collection, string demoName)
        {
            if (collection != null)
            {
                Debug.Log($"Loading {demoName}...");
                collection.Open();
            }
            else
            {
                Debug.LogError($"Scene collection for {demoName} is not assigned!");
            }
        }
        
        private void AddHoverDescription(UnityEngine.UI.Button button, string description)
        {
            var trigger = button.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (trigger == null)
                trigger = button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            
            // Mouse enter
            var pointerEnter = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerEnter.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
            pointerEnter.callback.AddListener((data) => ShowDescription(description));
            trigger.triggers.Add(pointerEnter);
            
            // Mouse exit  
            var pointerExit = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerExit.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
            pointerExit.callback.AddListener((data) => ShowDefaultDescription());
            trigger.triggers.Add(pointerExit);
        }
        
        private void ShowDescription(string description)
        {
            if (demoDescriptionText != null)
                demoDescriptionText.text = description;
        }
        
        private void ShowDefaultDescription()
        {
            ShowDescription("Select a demo to showcase the soft body physics engine");
        }
        
        private string GetPusherDescription()
        {
            return "Coin Pusher Demo\n\n" +
                   "• Multiple soft body toys spawning and interacting\n" +
                   "• Physics-based pusher mechanism\n" +
                   "• Object pooling and performance management\n" +
                   "• Automatic cleanup system\n\n" +
                   "Demonstrates: Multi-object interactions, performance scaling";
        }
        
        private string GetProceduralCubeDescription()
        {
            return "Procedural Cube Demo\n\n" +
                   "• Single procedurally generated soft body cube\n" +
                   "• Real-time physics simulation\n" +
                   "• Basic interaction and deformation\n" +
                   "• Material property demonstration\n\n" +
                   "Demonstrates: Core soft body physics, basic interaction";
        }
        
        private string GetSingleToyDescription()
        {
            return "Interactive Toy Demo\n\n" +
                   "• Single soft body toy with full interaction\n" +
                   "• Click and drag mechanics\n" +
                   "• Real-time deformation feedback\n" +
                   "• Material responsiveness showcase\n\n" +
                   "Demonstrates: Interactive design, user control, material behaviour";
        }
        
        private string GetMaterialDemoDescription()
        {
            return "Physics Material Demo\n\n" +
                   "• Simulates different physical materials like rubber, jelly, and balloons.\n" +
                   "• Each body uses unique SoftBodySettings for distinct behaviour.\n" +
                   "• Demonstrates how stiffness, damping, and pressure affect physics.\n\n" +
                   "Demonstrates: Physics material versatility, parameter tuning.";
        }

        private string GetConstraintDemoDescription()
        {
            return "Constraint Types Demo\n\n" +
                   "• Compares different internal constraint generation methods.\n" +
                   "• 1: Surface-only constraints\n" +
                   "• 2: Mesh-based with 'Stuffing'\n" +
                   "• 3: Tetrahedral volume constraints\n\n" +
                   "Demonstrates: Core physics structures, behaviour differences.";
        }
    }
}

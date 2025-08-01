using UnityEngine;
using UnityEngine.UI;
using AdvancedSceneManager.Models;
using SoftBody.Scripts.Testing;
using Unity.VisualScripting;

namespace SoftBody.Scripts.UI
{
    public class SharedDemoUI : MonoBehaviour
    {
        [Header("Top Bar")] public RectTransform topBar;
        public Button backButton;
        public TMPro.TextMeshProUGUI sceneTitle;
        public Button helpButton;

        [Header("Bottom Info Panel")] public RectTransform bottomPanel;
        public TMPro.TextMeshProUGUI instructionsText;
        public TMPro.TextMeshProUGUI performanceText;
        public Toggle performanceToggle;
        public bool showDetailedPerformance = false;

        [Header("Help Panel")] public GameObject helpPanel;
        public TMPro.TextMeshProUGUI helpText;
        public Button closeHelpButton;
        public ScrollRect helpScrollRect;

        [Header("Scene Management")] 
        public SceneCollection mainMenuCollection;

        private bool showPerformance = true;
        private float updateInterval = 0.5f;
        private float lastUpdateTime;

        private void Start()
        {
            SetupUI();
            UpdateSceneInfo();
            SetupHelpPanel();
        }

        private void Update()
        {
            UpdatePerformanceDisplay();
            HandleKeyboardInput();
        }

        private void SetupUI()
        {
            if (backButton != null)
                backButton.onClick.AddListener(ReturnToMainMenu);

            if (helpButton != null)
                helpButton.onClick.AddListener(() => ShowHelp(true));

            if (closeHelpButton != null)
                closeHelpButton.onClick.AddListener(() => ShowHelp(false));

            if (performanceToggle != null)
            {
                performanceToggle.isOn = showPerformance;
                performanceToggle.onValueChanged.AddListener(OnPerformanceToggleChanged);
            }

            // Initially hide help panel
            if (helpPanel != null)
                helpPanel.SetActive(false);
        }

        private void UpdateSceneInfo()
        {
            var sceneConfig = SceneConfiguration.Instance;

            if (sceneConfig != null)
            {
                // Use configuration from scene
                if (sceneTitle != null)
                    sceneTitle.text = sceneConfig.sceneDisplayName;

                if (instructionsText != null)
                    instructionsText.text = sceneConfig.instructions;

                if (helpText != null)
                    helpText.text = sceneConfig.detailedHelp;

            }
            else
            {
                // Fallback to scene name detection
                FallbackToSceneNameDetection();
            }
        }

        private void FallbackToSceneNameDetection()
        {
            var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            if (sceneTitle != null)
                sceneTitle.text = GetFriendlySceneName(sceneName);

            if (instructionsText != null)
                instructionsText.text = GetSceneInstructions(sceneName);

            if (helpText != null)
                helpText.text = GetDetailedHelp(sceneName);
        }

        private string GetFriendlySceneName(string sceneName)
        {
            return sceneName switch
            {
                "Dropper Demo" => "Coin Pusher Demo",
                "ProceduralCube" => "Procedural Cube Demo",
                "InteractiveToy" => "Interactive Toy Demo",
                _ => "Soft Body Physics Demo"
            };
        }

        private string GetSceneInstructions(string sceneName)
        {
            return sceneName switch
            {
                "Dropper Demo" =>
                    "Observe the automatic spawning and physics interactions • Multiple objects demonstrate performance scaling",

                "ProceduralCube" =>
                    "Left Click: Apply deformation force • Watch real-time physics simulation and recovery behaviour",

                "InteractiveToy" =>
                    "Left Click & Drag: Apply forces to the soft body • Experiment with different interaction points and intensities",

                _ => "Left Click to interact with soft body objects"
            };
        }

        private string GetDetailedHelp(string sceneName)
        {
            return sceneName switch
            {
                "Dropper Demo" =>
                    "COIN PUSHER TECHNICAL DEMO\n\n" +
                    "This demonstration showcases:\n" +
                    "• Object pooling for performance optimization\n" +
                    "• Multiple soft body interactions\n" +
                    "• Automatic lifecycle management\n" +
                    "• Physics-based mechanical simulation\n\n" +
                    "Watch how multiple objects interact while maintaining stable performance. " +
                    "The system automatically manages object creation, physics simulation, and cleanup.",

                "ProceduralCube" =>
                    "PROCEDURAL CUBE TECHNICAL DEMO\n\n" +
                    "This demonstration showcases:\n" +
                    "• Procedural mesh generation\n" +
                    "• XPBD constraint solving\n" +
                    "• GPU compute acceleration\n" +
                    "• Real-time deformation physics\n\n" +
                    "Click anywhere on the cube to apply forces and observe the deformation and recovery. " +
                    "The physics simulation runs entirely on the GPU for optimal performance.",

                "InteractiveToy" =>
                    "INTERACTIVE TOY TECHNICAL DEMO\n\n" +
                    "This demonstration showcases:\n" +
                    "• Direct user interaction design\n" +
                    "• Real-time force application\n" +
                    "• Responsive material behaviour\n" +
                    "• Character-like soft body physics\n\n" +
                    "Click and drag to apply forces to the soft body character. " +
                    "Experiment with different areas and force magnitudes to see varied responses.",

                _ => "Soft body physics demonstration with real-time deformation and interaction."
            };
        }
        
         private void UpdatePerformanceDisplay()
        {
            if (!showPerformance || performanceText == null) return;
            
            if (Time.time - lastUpdateTime < updateInterval) return;
            lastUpdateTime = Time.time;
            
            // Use singleton instance
            var monitor = SoftBodyPerformanceMonitor.Instance;
            
            if (monitor != null && monitor.IsAvailable)
            {
                if (showDetailedPerformance)
                {
                    performanceText.text = $"FPS: {monitor.CurrentFPS:F1}\n" +
                                          $"Bodies: {monitor.ActiveSoftBodyCount}\n" +
                                          $"Particles: {monitor.TotalParticleCount}\n" +
                                          $"Memory: {monitor.TotalMemoryUsage:F1}MB";
                }
                else
                {
                    performanceText.text = $"FPS: {monitor.CurrentFPS:F1} | " +
                                          $"Bodies: {monitor.ActiveSoftBodyCount} | " +
                                          $"Particles: {monitor.TotalParticleCount}";
                }
                
                // Apply FPS colour coding
                ApplyFPSColourCoding(monitor.CurrentFPS);
            }
            else
            {
                // Fallback when no monitor available
                var fps = 1f / Time.deltaTime;
                performanceText.text = $"FPS: {fps:F1}";
                ApplyFPSColourCoding(fps);
            }
        }
        
        private void ApplyFPSColourCoding(float fps)
        {
            if (performanceText == null) return;
            
            if (fps >= 50f)
                performanceText.color = Color.green;
            else if (fps >= 30f)
                performanceText.color = Color.yellow;
            else
                performanceText.color = Color.red;
        }
        
        public void ToggleDetailedPerformance()
        {
            showDetailedPerformance = !showDetailedPerformance;
        }

        private void OnPerformanceToggleChanged(bool value)
        {
            showPerformance = value;
            if (performanceText != null)
            {
                performanceText.gameObject.SetActive(showPerformance);
            }
        }

        private void HandleKeyboardInput()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                ReturnToMainMenu();

            if (Input.GetKeyDown(KeyCode.F1))
                ShowHelp(!helpPanel.activeInHierarchy);
        }

        // private void ShowHelp(bool show)
        // {
        //     if (helpPanel != null)
        //     {
        //         if (show)
        //         {
        //             helpPanel.SetActive(true);
        //             // Optional: Add fade-in animation
        //             StartCoroutine(FadeInHelp());
        //         }
        //         else
        //         {
        //             // Optional: Add fade-out animation
        //             StartCoroutine(FadeOutHelp());
        //         }
        //     }
        // }
        
        private void ShowHelp(bool show)
        {
            if (helpPanel != null)
            {
                helpPanel.SetActive(show);
        
                if (show && helpScrollRect != null)
                {
                    // Always start at the top when opening help
                    helpScrollRect.verticalNormalizedPosition = 1f;
                }
            }
        }

        private System.Collections.IEnumerator FadeInHelp()
        {
            var canvasGroup = helpPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = helpPanel.AddComponent<CanvasGroup>();
    
            var duration = 0.2f;
            var elapsed = 0f;
    
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
                yield return null;
            }
    
            canvasGroup.alpha = 1f;
        }

        private System.Collections.IEnumerator FadeOutHelp()
        {
            var canvasGroup = helpPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                helpPanel.SetActive(false);
                yield break;
            }
    
            var duration = 0.15f;
            var elapsed = 0f;
    
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
                yield return null;
            }
    
            canvasGroup.alpha = 0f;
            helpPanel.SetActive(false);
        }

        private void ReturnToMainMenu()
        {
            if (mainMenuCollection != null)
                mainMenuCollection.Open();
        }

        private void SetupHelpPanel()
{
    if (helpPanel == null) return;
    
    // Ensure help panel starts hidden
    helpPanel.SetActive(false);
    
    // Setup background styling
    var backgroundImage = helpPanel.GetComponent<Image>();
    if (backgroundImage != null)
    {
        var color = backgroundImage.color;
        color.a = 0.95f;
        backgroundImage.color = color;
    }
    
    // Setup ScrollRect if it exists
    var scrollRect = helpPanel.GetComponentInChildren<ScrollRect>();
    if (scrollRect != null)
    {
        SetupScrollableHelpText(scrollRect);
    }
    
    // Add escape key functionality
    var helpPanelScript = helpPanel.GetComponent<HelpPanelController>();
    if (helpPanelScript == null)
    {
        helpPanelScript = helpPanel.AddComponent<HelpPanelController>();
        helpPanelScript.Initialize(() => ShowHelp(false));
    }
}

private void SetupScrollableHelpText(ScrollRect scrollRect)
{
    // Configure scroll rect
    scrollRect.vertical = true;
    scrollRect.horizontal = false;
    scrollRect.movementType = ScrollRect.MovementType.Clamped;
    scrollRect.scrollSensitivity = 30f;
    
    // Setup content size fitter for dynamic sizing
    var content = scrollRect.content;
    if (content != null)
    {
        var sizeFitter = content.GetComponent<ContentSizeFitter>();
        if (sizeFitter == null)
            sizeFitter = content.AddComponent<ContentSizeFitter>();
            
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        
        // Setup vertical layout group for proper text sizing
        var layoutGroup = content.GetComponent<VerticalLayoutGroup>();
        if (layoutGroup == null)
        {
            layoutGroup = content.AddComponent<VerticalLayoutGroup>();
        }
            
        layoutGroup.childControlHeight = false;
        layoutGroup.childControlWidth = true;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.childForceExpandWidth = true;
    }
    
    // Find and configure the help text component
    var helpTextInScroll = scrollRect.content.GetComponentInChildren<TMPro.TextMeshProUGUI>();
    if (helpTextInScroll != null)
    {
        helpText = helpTextInScroll; // Update reference to the text inside scroll view
        
        // Configure text for scrolling
        helpText.enableWordWrapping = true;
        helpText.overflowMode = TMPro.TextOverflowModes.Overflow;
    }
}

        private void AddHoverEffect(UnityEngine.EventSystems.EventTrigger trigger, Button button)
        {
            var pointerEnter = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerEnter.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
            pointerEnter.callback.AddListener((data) =>
            {
                var colors = button.colors;
                colors.normalColor = colors.highlightedColor;
                button.colors = colors;
            });
            trigger.triggers.Add(pointerEnter);

            var pointerExit = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerExit.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
            pointerExit.callback.AddListener((data) =>
            {
                var colors = button.colors;
                colors.normalColor = Color.white;
                button.colors = colors;
            });
            trigger.triggers.Add(pointerExit);
        }
    }
}
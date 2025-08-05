using UnityEngine;
using UnityEngine.UI;

namespace SoftBody.Scripts.UI
{
    public class UIPanel : MonoBehaviour
    {

        [Header("Panel Configuration")] 
        public bool addDropShadow = true;
        public bool roundedCorners = true;
        public bool subtleBorder = true;

        private void Start()
        {
            ApplyAcademicStyling();
        }

        private void ApplyAcademicStyling()
        {
            var image = GetComponent<Image>();
            if (image != null)
            {
                // Clean background color
                image.color = new Color(0.98f, 0.98f, 1.0f, 0.95f);
            }

            if (addDropShadow)
                AddDropShadowEffect();

            if (subtleBorder)
                AddSubtleBorder();
        }

        private void AddDropShadowEffect()
        {
            // Create shadow behind panel
            var shadowGo = new GameObject("PanelShadow");
            shadowGo.transform.SetParent(transform);
            shadowGo.transform.SetAsFirstSibling(); // Behind the panel

            var shadowRect = shadowGo.AddComponent<RectTransform>();
            var shadowImage = shadowGo.AddComponent<Image>();

            // Position shadow slightly offset
            shadowRect.anchorMin = Vector2.zero;
            shadowRect.anchorMax = Vector2.one;
            shadowRect.sizeDelta = Vector2.zero;
            shadowRect.anchoredPosition = new Vector2(2f, -2f); // Subtle offset

            // Shadow styling
            shadowImage.color = new Color(0f, 0f, 0f, 0.1f); // Very subtle
            shadowImage.raycastTarget = false; // Don't interfere with clicks
        }

        private void AddSubtleBorder()
        {
            var outline = GetComponent<Outline>();
            if (outline == null)
                outline = gameObject.AddComponent<Outline>();

            outline.effectColor = new Color(0.85f, 0.85f, 0.9f, 0.6f);
            outline.effectDistance = Vector2.one;
        }
    }
}
    

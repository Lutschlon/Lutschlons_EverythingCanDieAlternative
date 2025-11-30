using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace EverythingCanDieAlternative.UI
{
    /// <summary>
    /// Custom scroll handler that normalizes mouse wheel input to prevent instant scrolling
    /// </summary>
    public class NormalizedScrollRect : MonoBehaviour, IScrollHandler
    {
        private ScrollRect scrollRect;
        
        // Maximum scroll delta per frame (normalized)
        [SerializeField]
        private float maxScrollDelta = 0.1f;
        
        // Smoothing factor for scroll (lower = smoother)
        [SerializeField]
        private float scrollSpeed = 0.15f;

        private void Awake()
        {
            scrollRect = GetComponent<ScrollRect>();
            if (scrollRect != null)
            {
                // Disable the built-in scroll sensitivity since we're handling it ourselves
                scrollRect.scrollSensitivity = 0f;
            }
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (scrollRect == null || scrollRect.content == null) return;

            // Normalize the scroll delta
            float scrollDelta = eventData.scrollDelta.y;
            
            // Clamp to a reasonable range regardless of input magnitude
            scrollDelta = Mathf.Clamp(scrollDelta, -1f, 1f);
            
            // Apply our controlled scroll speed
            float normalizedDelta = scrollDelta * scrollSpeed;
            
            // Clamp the final delta
            normalizedDelta = Mathf.Clamp(normalizedDelta, -maxScrollDelta, maxScrollDelta);

            // Calculate new position
            float newPosition = scrollRect.verticalNormalizedPosition + normalizedDelta;
            newPosition = Mathf.Clamp01(newPosition);

            // Apply the scroll
            scrollRect.verticalNormalizedPosition = newPosition;
        }

        // Static helper to add this component to a scroll view
        public static void ApplyTo(GameObject scrollViewObj, float scrollSpeed = 0.15f, float maxDelta = 0.1f)
        {
            if (scrollViewObj == null) return;

            var normalizedScroll = scrollViewObj.GetComponent<NormalizedScrollRect>();
            if (normalizedScroll == null)
            {
                normalizedScroll = scrollViewObj.AddComponent<NormalizedScrollRect>();
            }

            normalizedScroll.scrollSpeed = scrollSpeed;
            normalizedScroll.maxScrollDelta = maxDelta;
        }
    }
}
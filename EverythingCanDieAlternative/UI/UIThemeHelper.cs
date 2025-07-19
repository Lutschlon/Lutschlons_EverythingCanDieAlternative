using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace EverythingCanDieAlternative.UI
{
    // Theme helper for consistent UI styling matching the game's aesthetic
    public static class UITheme
    {
        // Color scheme that matches Lethal Company's UI
        public static readonly Color BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        public static readonly Color PanelColor = new Color(0.15f, 0.15f, 0.15f, 0.95f);
        public static readonly Color ButtonColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        public static readonly Color ButtonHighlightColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        public static readonly Color ButtonPressedColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        public static readonly Color TextColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        public static readonly Color HeaderColor = new Color(1f, 0.9f, 0.5f, 1f); // Yellowish for headers
        public static readonly Color PositiveColor = new Color(0.2f, 0.8f, 0.2f, 1f);
        public static readonly Color NegativeColor = new Color(0.8f, 0.2f, 0.2f, 1f);
        public static readonly Color NeutralColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        public static readonly Color DisabledColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);

        // Font sizes
        public static readonly float HeaderFontSize = 22f;
        public static readonly float NormalFontSize = 16f;
        public static readonly float SmallFontSize = 14f;

        // Spacing and sizing
        public static readonly float PanelPadding = 20f;
        public static readonly float ElementSpacing = 15f;
        public static readonly float ButtonHeight = 40f;
        public static readonly float InputHeight = 30f;

        // Applies theme to a button
        public static void ApplyButtonTheme(Button button)
        {
            // Set colors
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonColor;
            colors.highlightedColor = ButtonHighlightColor;
            colors.pressedColor = ButtonPressedColor;
            colors.disabledColor = DisabledColor;
            button.colors = colors;

            // Set text color if it has TextMeshProUGUI
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.color = TextColor;
                text.fontSize = NormalFontSize;
            }
        }

        // Applies theme to a panel
        public static void ApplyPanelTheme(Image panelImage)
        {
            panelImage.color = PanelColor;
        }

        // Creates a styled header
        public static TextMeshProUGUI CreateStyledHeader(GameObject textObject)
        {
            TextMeshProUGUI tmp = textObject.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.color = HeaderColor;
                tmp.fontSize = HeaderFontSize;
                tmp.fontStyle = FontStyles.Bold;
            }
            return tmp;
        }

        // Creates a styled normal text
        public static TextMeshProUGUI CreateStyledText(GameObject textObject)
        {
            TextMeshProUGUI tmp = textObject.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.color = TextColor;
                tmp.fontSize = NormalFontSize;
            }
            return tmp;
        }

        // Applies theme to a scroll view
        public static void ApplyScrollViewTheme(ScrollRect scrollRect)
        {
            if (scrollRect.GetComponent<Image>() != null)
            {
                scrollRect.GetComponent<Image>().color = PanelColor;
            }

            // Style the viewport
            if (scrollRect.viewport.GetComponent<Image>() != null)
            {
                scrollRect.viewport.GetComponent<Image>().color = PanelColor;
            }
        }

        // Apply input field theme
        public static void ApplyInputFieldTheme(TMP_InputField inputField)
        {
            if (inputField.GetComponent<Image>() != null)
            {
                inputField.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 1f);
            }

            if (inputField.textComponent != null)
            {
                inputField.textComponent.color = TextColor;
                inputField.textComponent.fontSize = NormalFontSize;
            }

            if (inputField.placeholder != null)
            {
                inputField.placeholder.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            }
        }
    }
}
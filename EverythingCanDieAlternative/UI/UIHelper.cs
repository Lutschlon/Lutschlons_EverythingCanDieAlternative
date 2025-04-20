using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace EverythingCanDieAlternative.UI
{
    /// <summary>
    /// Helper class for creating UI elements
    /// </summary>
    public static class UIHelper
    {
        public static GameObject CreatePanel(Transform parent, string name, Vector2 sizeDelta)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);

            RectTransform rectTransform = panel.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = sizeDelta;

            Image image = panel.AddComponent<Image>();
            image.color = UITheme.PanelColor;

            return panel;
        }

        public static GameObject CreateText(Transform parent, string name, string text,
    TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);

            RectTransform rectTransform = textObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 0);
            rectTransform.anchorMax = new Vector2(1, 1);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = UITheme.NormalFontSize;
            tmp.alignment = alignment;
            tmp.color = UITheme.TextColor;

            return textObj;
        }

        public static GameObject CreateButton(Transform parent, string name, string text, UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonObj = new GameObject(name);
            buttonObj.transform.SetParent(parent, false);

            RectTransform rectTransform = buttonObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(160, UITheme.ButtonHeight);

            Image image = buttonObj.AddComponent<Image>();
            image.color = UITheme.ButtonColor;

            Button button = buttonObj.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = UITheme.ButtonColor;
            colors.highlightedColor = UITheme.ButtonHighlightColor;
            colors.pressedColor = UITheme.ButtonPressedColor;
            colors.disabledColor = UITheme.DisabledColor;
            button.colors = colors;

            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            GameObject textObj = CreateText(buttonObj.transform, "Text", text);

            return buttonObj;
        }

       

        public static GameObject CreateInputField(Transform parent, string name, string placeholder, string value,
            UnityEngine.Events.UnityAction<string> onValueChanged, TMP_InputField.ContentType contentType = TMP_InputField.ContentType.Standard)
        {
            GameObject inputObj = new GameObject(name);
            inputObj.transform.SetParent(parent, false);

            RectTransform rectTransform = inputObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 0.5f);
            rectTransform.anchorMax = new Vector2(1, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(0, 30);

            Image bgImage = inputObj.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.1f);

            TMP_InputField inputField = inputObj.AddComponent<TMP_InputField>();
            inputField.contentType = contentType;

            // Placeholder
            GameObject placeholderObj = CreateText(inputObj.transform, "Placeholder", placeholder);
            TextMeshProUGUI placeholderText = placeholderObj.GetComponent<TextMeshProUGUI>();
            placeholderText.color = new Color(0.5f, 0.5f, 0.5f);
            placeholderText.enabled = string.IsNullOrEmpty(value);

            inputField.placeholder = placeholderText;

            // Text input area
            GameObject textAreaObj = CreateText(inputObj.transform, "Text", value);
            TextMeshProUGUI textArea = textAreaObj.GetComponent<TextMeshProUGUI>();

            inputField.textComponent = textArea;
            inputField.text = value;

            if (onValueChanged != null)
            {
                inputField.onValueChanged.AddListener(onValueChanged);
            }

            return inputObj;
        }

        public static GameObject CreateScrollView(Transform parent, string name, Vector2 sizeDelta)
        {
            // Create the scroll view object
            GameObject scrollViewObj = new GameObject(name);
            scrollViewObj.transform.SetParent(parent, false);

            RectTransform rectTransform = scrollViewObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = sizeDelta;

            Image image = scrollViewObj.AddComponent<Image>();
            image.color = UITheme.PanelColor;

            ScrollRect scrollRect = scrollViewObj.AddComponent<ScrollRect>();

            // Create viewport
            GameObject viewportObj = new GameObject("Viewport");
            viewportObj.transform.SetParent(scrollViewObj.transform, false);

            RectTransform viewportRect = viewportObj.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.pivot = new Vector2(0.5f, 0.5f);
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            Image viewportImage = viewportObj.AddComponent<Image>();
            viewportImage.color = UITheme.PanelColor;

            Mask mask = viewportObj.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            // Create content container
            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(viewportObj.transform, false);

            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 0); // Will be dynamically sized

            // Connect components
            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            // Add Content Size Fitter to auto-resize content based on children
            ContentSizeFitter fitter = contentObj.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Add Vertical Layout Group to arrange children
            VerticalLayoutGroup layout = contentObj.AddComponent<VerticalLayoutGroup>();
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.spacing = 5;
            layout.padding = new RectOffset(5, 5, 5, 5);

            return scrollViewObj;
        }

        /// <summary>
        /// Creates a Yes/No selector with two buttons
        /// </summary>
        /// <param name="parent">Parent transform</param>
        /// <param name="name">Name of the game object</param>
        /// <param name="label">Label text to display</param>
        /// <param name="isYes">Initial state (true = Yes selected, false = No selected)</param>
        /// <param name="onValueChanged">Callback when value changes</param>
        /// <returns>The created game object</returns>
        public static GameObject CreateYesNoSelector(Transform parent, string name, string label, bool isYes, UnityEngine.Events.UnityAction<bool> onValueChanged)
        {
            GameObject selectorObj = new GameObject(name);
            selectorObj.transform.SetParent(parent, false);

            RectTransform rectTransform = selectorObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 0.5f);
            rectTransform.anchorMax = new Vector2(1, 0.5f);
            rectTransform.pivot = new Vector2(0, 0.5f);
            rectTransform.sizeDelta = new Vector2(0, UITheme.InputHeight);

            // Create horizontal layout
            HorizontalLayoutGroup layout = selectorObj.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.padding = new RectOffset(0, 0, 0, 0);

            // Create label
            GameObject labelObj = CreateText(selectorObj.transform, "Label", label, TextAlignmentOptions.Left);
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(230, UITheme.InputHeight);

            // Create container for Yes/No buttons
            GameObject buttonContainer = new GameObject("ButtonContainer");
            buttonContainer.transform.SetParent(selectorObj.transform, false);

            RectTransform buttonContainerRect = buttonContainer.AddComponent<RectTransform>();
            buttonContainerRect.sizeDelta = new Vector2(120, UITheme.InputHeight);

            HorizontalLayoutGroup buttonLayout = buttonContainer.AddComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 5;
            buttonLayout.childForceExpandWidth = true;
            buttonLayout.childForceExpandHeight = true;
            buttonLayout.childAlignment = TextAnchor.MiddleCenter;

            // Create Yes button
            GameObject yesButton = new GameObject("YesButton");
            yesButton.transform.SetParent(buttonContainer.transform, false);

            RectTransform yesRect = yesButton.AddComponent<RectTransform>();

            Image yesImage = yesButton.AddComponent<Image>();
            yesImage.color = isYes ? UITheme.PositiveColor : UITheme.NeutralColor;

            Button yesButtonComponent = yesButton.AddComponent<Button>();

            // Create Yes text
            GameObject yesText = CreateText(yesButton.transform, "Text", "YES", TextAlignmentOptions.Center);
            TextMeshProUGUI yesTextComp = yesText.GetComponent<TextMeshProUGUI>();
            yesTextComp.fontSize = UITheme.SmallFontSize;
            yesTextComp.color = isYes ? Color.white : new Color(0.7f, 0.7f, 0.7f);

            // Create No button
            GameObject noButton = new GameObject("NoButton");
            noButton.transform.SetParent(buttonContainer.transform, false);

            RectTransform noRect = noButton.AddComponent<RectTransform>();

            Image noImage = noButton.AddComponent<Image>();
            noImage.color = !isYes ? UITheme.NegativeColor : UITheme.NeutralColor;

            Button noButtonComponent = noButton.AddComponent<Button>();

            // Create No text
            GameObject noText = CreateText(noButton.transform, "Text", "NO", TextAlignmentOptions.Center);
            TextMeshProUGUI noTextComp = noText.GetComponent<TextMeshProUGUI>();
            noTextComp.fontSize = UITheme.SmallFontSize;
            noTextComp.color = !isYes ? Color.white : new Color(0.7f, 0.7f, 0.7f);

            // Add callbacks
            yesButtonComponent.onClick.AddListener(() => {
                if (!isYes)
                {
                    isYes = true;
                    yesImage.color = UITheme.PositiveColor;
                    yesTextComp.color = Color.white;
                    noImage.color = UITheme.NeutralColor;
                    noTextComp.color = new Color(0.7f, 0.7f, 0.7f);
                    if (onValueChanged != null)
                    {
                        onValueChanged(true);
                    }
                }
            });

            noButtonComponent.onClick.AddListener(() => {
                if (isYes)
                {
                    isYes = false;
                    yesImage.color = UITheme.NeutralColor;
                    yesTextComp.color = new Color(0.7f, 0.7f, 0.7f);
                    noImage.color = UITheme.NegativeColor;
                    noTextComp.color = Color.white;
                    if (onValueChanged != null)
                    {
                        onValueChanged(false);
                    }
                }
            });

            // Store the current state in a component
            var stateHolder = selectorObj.AddComponent<StateHolder>();
            stateHolder.IsYes = isYes;
            stateHolder.YesButton = yesButtonComponent;
            stateHolder.NoButton = noButtonComponent;
            stateHolder.YesImage = yesImage;
            stateHolder.NoImage = noImage;
            stateHolder.YesText = yesTextComp;
            stateHolder.NoText = noTextComp;

            return selectorObj;
        }

        /// <summary>
        /// Utility class to hold state for the Yes/No selector
        /// </summary>
        public class StateHolder : MonoBehaviour
        {
            public bool IsYes;
            public Button YesButton;
            public Button NoButton;
            public Image YesImage;
            public Image NoImage;
            public TextMeshProUGUI YesText;
            public TextMeshProUGUI NoText;

            public void SetInteractable(bool interactable)
            {
                YesButton.interactable = interactable;
                NoButton.interactable = interactable;

                // Update colors based on interactable state
                if (!interactable)
                {
                    YesImage.color = new Color(0.4f, 0.4f, 0.4f);
                    NoImage.color = new Color(0.4f, 0.4f, 0.4f);
                    YesText.color = new Color(0.5f, 0.5f, 0.5f);
                    NoText.color = new Color(0.5f, 0.5f, 0.5f);
                }
                else
                {
                    // Restore proper colors
                    YesImage.color = IsYes ? UITheme.PositiveColor : UITheme.NeutralColor;
                    NoImage.color = !IsYes ? UITheme.NegativeColor : UITheme.NeutralColor;
                    YesText.color = IsYes ? Color.white : new Color(0.7f, 0.7f, 0.7f);
                    NoText.color = !IsYes ? Color.white : new Color(0.7f, 0.7f, 0.7f);
                }
            }

            /// <summary>
            /// Updates the visual state of the Yes/No selector to match the specified value
            /// </summary>
            /// <param name="isYes">Whether the Yes button should be selected</param>
            public void UpdateVisualState(bool isYes)
            {
                // Update internal state
                IsYes = isYes;

                // Update visual appearance
                if (YesButton.interactable) // Only update colors if the control is enabled
                {
                    YesImage.color = isYes ? UITheme.PositiveColor : UITheme.NeutralColor;
                    NoImage.color = !isYes ? UITheme.NegativeColor : UITheme.NeutralColor;
                    YesText.color = isYes ? Color.white : new Color(0.7f, 0.7f, 0.7f);
                    NoText.color = !isYes ? Color.white : new Color(0.7f, 0.7f, 0.7f);
                }
            }
        }

        /// <summary>
        /// Creates a numeric input field with up/down arrow buttons
        /// </summary>
        public static GameObject CreateNumericInputWithArrows(
    Transform parent,
    string name,
    string label,
    int initialValue,
    int minValue,
    int maxValue,
    UnityEngine.Events.UnityAction<int> onValueChanged)
        {
            // Create main container
            GameObject containerObj = new GameObject(name);
            containerObj.transform.SetParent(parent, false);

            RectTransform containerRect = containerObj.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0, 0.5f);
            containerRect.anchorMax = new Vector2(1, 0.5f);
            containerRect.pivot = new Vector2(0, 0.5f);
            containerRect.sizeDelta = new Vector2(0, UITheme.InputHeight);

            // Add horizontal layout
            HorizontalLayoutGroup layout = containerObj.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.padding = new RectOffset(0, 0, 0, 0);

            // Create label
            GameObject labelObj = CreateText(containerObj.transform, "Label", label, TextAlignmentOptions.Left);
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(80, 30);

            // Create input field
            GameObject inputObj = new GameObject("InputField");
            inputObj.transform.SetParent(containerObj.transform, false);

            RectTransform inputRect = inputObj.AddComponent<RectTransform>();
            inputRect.sizeDelta = new Vector2(60, 30);

            Image inputBg = inputObj.AddComponent<Image>();
            inputBg.color = new Color(0.12f, 0.12f, 0.12f, 1f);

            // Create input field component
            TMP_InputField inputField = inputObj.AddComponent<TMP_InputField>();
            inputField.contentType = TMP_InputField.ContentType.IntegerNumber;

            // Create text area for input
            GameObject textAreaObj = CreateText(inputObj.transform, "Text", initialValue.ToString());
            TextMeshProUGUI textArea = textAreaObj.GetComponent<TextMeshProUGUI>();
            textArea.alignment = TextAlignmentOptions.Center;

            inputField.textComponent = textArea;
            inputField.text = initialValue.ToString();

            // Create placeholder
            GameObject placeholderObj = CreateText(inputObj.transform, "Placeholder", "Enter value");
            TextMeshProUGUI placeholderText = placeholderObj.GetComponent<TextMeshProUGUI>();
            placeholderText.color = new Color(0.5f, 0.5f, 0.5f);
            placeholderText.alignment = TextAlignmentOptions.Center;
            placeholderText.enabled = string.IsNullOrEmpty(initialValue.ToString());

            inputField.placeholder = placeholderText;

            // Store the current value
            int currentValue = initialValue;

            // Create a container for arrow buttons
            GameObject arrowsContainer = new GameObject("ArrowsContainer");
            arrowsContainer.transform.SetParent(containerObj.transform, false);

            RectTransform arrowsRect = arrowsContainer.AddComponent<RectTransform>();
            arrowsRect.sizeDelta = new Vector2(30, 30);

            // Add vertical layout for arrows
            VerticalLayoutGroup arrowsLayout = arrowsContainer.AddComponent<VerticalLayoutGroup>();
            arrowsLayout.spacing = 2;
            arrowsLayout.childForceExpandWidth = true;
            arrowsLayout.childForceExpandHeight = true;
            arrowsLayout.childAlignment = TextAnchor.MiddleCenter;
            arrowsLayout.padding = new RectOffset(0, 0, 0, 0);

            // Create up arrow button
            GameObject upButton = new GameObject("UpButton");
            upButton.transform.SetParent(arrowsContainer.transform, false);

            RectTransform upRect = upButton.AddComponent<RectTransform>();

            Image upImage = upButton.AddComponent<Image>();
            upImage.color = UITheme.ButtonColor;

            Button upButtonComponent = upButton.AddComponent<Button>();
            ColorBlock upColors = upButtonComponent.colors;
            upColors.normalColor = UITheme.ButtonColor;
            upColors.highlightedColor = UITheme.ButtonHighlightColor;
            upColors.pressedColor = UITheme.ButtonPressedColor;
            upButtonComponent.colors = upColors;

            // Create up arrow text
            GameObject upText = CreateText(upButton.transform, "Text", "▲", TextAlignmentOptions.Center);
            TextMeshProUGUI upTextComp = upText.GetComponent<TextMeshProUGUI>();
            upTextComp.fontSize = 12;

            // Create down arrow button
            GameObject downButton = new GameObject("DownButton");
            downButton.transform.SetParent(arrowsContainer.transform, false);

            RectTransform downRect = downButton.AddComponent<RectTransform>();

            Image downImage = downButton.AddComponent<Image>();
            downImage.color = UITheme.ButtonColor;

            Button downButtonComponent = downButton.AddComponent<Button>();
            ColorBlock downColors = downButtonComponent.colors;
            downColors.normalColor = UITheme.ButtonColor;
            downColors.highlightedColor = UITheme.ButtonHighlightColor;
            downColors.pressedColor = UITheme.ButtonPressedColor;
            downButtonComponent.colors = downColors;

            // Create down arrow text
            GameObject downText = CreateText(downButton.transform, "Text", "▼", TextAlignmentOptions.Center);
            TextMeshProUGUI downTextComp = downText.GetComponent<TextMeshProUGUI>();
            downTextComp.fontSize = 12;

            // Add button handlers
            upButtonComponent.onClick.AddListener(() => {
                // Increment value
                currentValue = Mathf.Min(currentValue + 1, maxValue);
                // Update input field
                inputField.text = currentValue.ToString();
                // Notify callback
                if (onValueChanged != null)
                {
                    onValueChanged(currentValue);
                }
            });

            downButtonComponent.onClick.AddListener(() => {
                // Decrement value
                currentValue = Mathf.Max(currentValue - 1, minValue);
                // Update input field
                inputField.text = currentValue.ToString();
                // Notify callback
                if (onValueChanged != null)
                {
                    onValueChanged(currentValue);
                }
            });

            // Handle manual input changes
            inputField.onEndEdit.AddListener((newValue) => {
                // Try to parse the value
                if (int.TryParse(newValue, out int parsedValue))
                {
                    // Clamp to min/max
                    currentValue = Mathf.Clamp(parsedValue, minValue, maxValue);
                    // Update input field in case it was clamped
                    if (parsedValue != currentValue)
                    {
                        inputField.text = currentValue.ToString();
                    }
                    // Notify callback
                    if (onValueChanged != null)
                    {
                        onValueChanged(currentValue);
                    }
                }
                else
                {
                    // Reset to current value if parsing failed
                    inputField.text = currentValue.ToString();
                }
            });

            // Store the components for external access
            var numericInputState = containerObj.AddComponent<NumericInputState>();
            numericInputState.CurrentValue = currentValue;
            numericInputState.InputField = inputField;
            numericInputState.UpButton = upButtonComponent;
            numericInputState.DownButton = downButtonComponent;
            numericInputState.MinValue = minValue;
            numericInputState.MaxValue = maxValue;
            numericInputState.OnValueChanged = onValueChanged;

            return containerObj;
        }

        /// <summary>
        /// Utility class to hold state for numeric input with arrows
        /// </summary>
        public class NumericInputState : MonoBehaviour
        {
            public int CurrentValue;
            public int MinValue;
            public int MaxValue;
            public TMP_InputField InputField;
            public Button UpButton;
            public Button DownButton;
            public UnityEngine.Events.UnityAction<int> OnValueChanged;

            public void SetInteractable(bool interactable)
            {
                InputField.interactable = interactable;
                UpButton.interactable = interactable;
                DownButton.interactable = interactable;
            }

            public void SetValue(int value)
            {
                CurrentValue = Mathf.Clamp(value, MinValue, MaxValue);
                InputField.text = CurrentValue.ToString();
            }
        }
    }
}
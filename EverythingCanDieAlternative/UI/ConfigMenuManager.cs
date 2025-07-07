using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

namespace EverythingCanDieAlternative.UI
{
    /// <summary>
    /// Manager for the configuration menu UI
    /// </summary>
    public class ConfigMenuManager : MonoBehaviour
    {
        private static GameObject menuPrefab;

        // UI components
        private GameObject menuPanel;
        private RectTransform enemyListContent;
        private GameObject enemyConfigPanel;
        private GameObject globalSettingsButton;
        private bool isShowingGlobalSettings = false;

        // Configuration data
        private List<EnemyConfigData> enemyConfigs = new List<EnemyConfigData>();
        private Dictionary<string, GameObject> enemyEntries = new Dictionary<string, GameObject>();

        // Current selection
        private string selectedEnemyName;

        // Constants
        private const float ENTRY_HEIGHT = 35f;

        // Flag to prevent duplicate refresh
        private static bool refreshScheduled = false;

        // Search field
        private TMP_InputField searchInputField;
        private string lastSearchText = "";

        // Add a helper method to clear search
        private void ClearSearch()
        {
            if (searchInputField != null)
            {
                searchInputField.text = "";
                lastSearchText = "";
                FilterEnemyList(lastSearchText);
            }
        }

        private void ScheduleRefresh()
        {
            if (!refreshScheduled)
            {
                refreshScheduled = true;
                ClearSearch(); // Clear search when refreshing
                StartCoroutine(DelayedRefresh());
            }
        }

        public void RefreshEnemyData()
        {
            Plugin.LogInfo("RefreshEnemyData called");

            // Clear existing enemy entries
            foreach (Transform child in enemyListContent)
            {
                Destroy(child.gameObject);
            }
            enemyEntries.Clear();

            // Load all enemy configurations
            enemyConfigs = ConfigBridge.LoadAllEnemyConfigs();

            // Sort the enemy configurations alphabetically by name
            enemyConfigs.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            Plugin.LogInfo($"Sorted {enemyConfigs.Count} enemies alphabetically");

            // Create list entries for each enemy
            foreach (var config in enemyConfigs)
            {
                CreateEnemyListEntry(config);
            }

            // Apply initial filter with last search text
            FilterEnemyList(lastSearchText);

            // Clear selection
            selectedEnemyName = null;
            UpdateConfigPanel();

            Plugin.LogInfo($"Loaded {enemyConfigs.Count} enemy configurations");
        }

        // Sound methods
        private static void PlayConfirmSFX()
        {
            var menuManager = GameObject.FindObjectOfType<MenuManager>();
            if (menuManager != null && menuManager.MenuAudio != null)
            {
                menuManager.PlayConfirmSFX();
            }
        }

        private static void PlayCancelSFX()
        {
            var menuManager = GameObject.FindObjectOfType<MenuManager>();
            if (menuManager != null && menuManager.MenuAudio != null)
            {
                menuManager.PlayCancelSFX();
            }
        }

        public static void ToggleConfigMenu()
        {
            // Check if the config menu is enabled in settings
            if (!UIConfiguration.Instance.IsConfigMenuEnabled())
            {
                Plugin.LogWarning("Attempted to open config menu, but it's disabled in settings");
                return;
            }

            Plugin.LogInfo("ToggleConfigMenu called");

            // Create menu if it doesn't exist yet
            if (menuPrefab == null)
            {
                CreateMenuPrefab();
            }

            // Toggle menu visibility
            bool wasActive = menuPrefab.activeSelf;
            menuPrefab.SetActive(!wasActive);

            // Refresh enemy data when opening
            if (!wasActive)
            {
                Plugin.LogInfo("Menu opened, scheduling refresh");
                var menuManager = menuPrefab.GetComponent<ConfigMenuManager>();
                menuManager.ScheduleRefresh();
            }
        }

        private IEnumerator DelayedRefresh()
        {
            Plugin.LogInfo("Starting delayed refresh");
            yield return new WaitForSeconds(0.1f);
            RefreshEnemyData();
            refreshScheduled = false;
            Plugin.LogInfo("Delayed refresh completed");
        }

        private static void CreateMenuPrefab()
        {
            try
            {
                Plugin.LogInfo("Creating menu prefab");

                // Create the menu object that will be instantiated when button is clicked
                menuPrefab = new GameObject("ECDAConfigMenu");
                var menuManager = menuPrefab.AddComponent<ConfigMenuManager>();
                menuPrefab.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(menuPrefab);

                // Create menu canvas
                var canvas = menuPrefab.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000; // Make sure it's on top

                var scaler = menuPrefab.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);

                menuPrefab.AddComponent<GraphicRaycaster>();

                // Create background overlay
                var overlayObj = UIHelper.CreatePanel(menuPrefab.transform, "Overlay", new Vector2(2000, 2000));
                if (overlayObj == null)
                {
                    Plugin.LogError("Failed to create overlay panel");
                    return;
                }

                var overlayImage = overlayObj.GetComponent<Image>();
                if (overlayImage != null)
                {
                    overlayImage.color = new Color(0, 0, 0, 0.7f);
                }

                // Create main panel with Lethal Company style
                var mainPanelObj = UIHelper.CreatePanel(menuPrefab.transform, "MainPanel", new Vector2(800, 600));
                if (mainPanelObj == null)
                {
                    Plugin.LogError("Failed to create main panel");
                    return;
                }

                menuManager.menuPanel = mainPanelObj;

                // Add a background gradient image for more LC-like appearance
                var panelImage = mainPanelObj.GetComponent<Image>();
                if (panelImage != null)
                {
                    panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
                }

                // Create close button styled like in-game
                var closeButtonObj = UIHelper.CreateButton(mainPanelObj.transform, "CloseButton", "X", () => {
                    // Play cancel sound when closing
                    PlayCancelSFX();
                    menuPrefab.SetActive(false);
                });

                if (closeButtonObj != null)
                {
                    var closeButtonRectTransform = closeButtonObj.GetComponent<RectTransform>();
                    closeButtonRectTransform.anchorMin = new Vector2(1, 1);
                    closeButtonRectTransform.anchorMax = new Vector2(1, 1);
                    closeButtonRectTransform.pivot = new Vector2(1, 1);
                    closeButtonRectTransform.sizeDelta = new Vector2(40, 40);
                    closeButtonRectTransform.anchoredPosition = new Vector2(-10, -10);
                }

                // Add the "Hide Menu" Yes/No toggle using the same method as enemy settings
                var hideMenuSelector = UIHelper.CreateYesNoSelector(
                    mainPanelObj.transform,
                    "HideMenuSelector",
                    "Hide Menu:",
                    !UIConfiguration.Instance.IsConfigMenuEnabled(), // Inverted - "Yes" to hide
                    (isHideMenu) => {
                        PlayConfirmSFX();
                        UIConfiguration.Instance.SetConfigMenuEnabled(!isHideMenu); // Inverted - "Yes" to hide means disable menu
                        UIConfiguration.Instance.Save();
                    });

                if (hideMenuSelector != null)
                {
                    var hideMenuRect = hideMenuSelector.GetComponent<RectTransform>();
                    hideMenuRect.anchorMin = new Vector2(0, 1);
                    hideMenuRect.anchorMax = new Vector2(0, 1);
                    hideMenuRect.pivot = new Vector2(0, 1);
                    hideMenuRect.sizeDelta = new Vector2(160, 30);
                    hideMenuRect.anchoredPosition = new Vector2(40, -15); // Changed from 10 to 40 to move right

                    // Fix internal spacing - get the Label component and adjust it
                    var label = hideMenuSelector.transform.Find("Label")?.GetComponent<RectTransform>();
                    if (label != null)
                    {
                        label.sizeDelta = new Vector2(80, 30);
                    }
                }

                // Add the "Less Logs" Yes/No toggle
                var lessLogsSelector = UIHelper.CreateYesNoSelector(
                    mainPanelObj.transform,
                    "LessLogsSelector",
                    "Less Logs:",
                    !UIConfiguration.Instance.ShouldLogInfo(), // Inverted - "Yes" for less logs means disable info logs
                    (isLessLogs) => {
                        PlayConfirmSFX();
                        UIConfiguration.Instance.SetInfoLogsEnabled(!isLessLogs); // Inverted - "Yes" for less logs means disable logs
                        UIConfiguration.Instance.Save();
                    });

                if (lessLogsSelector != null)
                {
                    var lessLogsRect = lessLogsSelector.GetComponent<RectTransform>();
                    lessLogsRect.anchorMin = new Vector2(0, 1);
                    lessLogsRect.anchorMax = new Vector2(0, 1);
                    lessLogsRect.pivot = new Vector2(0, 1);
                    lessLogsRect.sizeDelta = new Vector2(150, 30);
                    lessLogsRect.anchoredPosition = new Vector2(260, -15);

                    // Fix internal spacing - get the Label component and adjust it
                    var label = lessLogsSelector.transform.Find("Label")?.GetComponent<RectTransform>();
                    if (label != null)
                    {
                        label.sizeDelta = new Vector2(70, 30);
                    }
                }

                // Add the "Show Images" Yes/No toggle 
                var showImagesSelector = UIHelper.CreateYesNoSelector(
                    mainPanelObj.transform,
                    "ShowImagesSelector",
                    "Show Images:",
                    UIConfiguration.Instance.ShouldShowEnemyImages(),
                    (showImages) => {
                        PlayConfirmSFX();
                        UIConfiguration.Instance.SetShowEnemyImages(showImages);
                        UIConfiguration.Instance.Save();

                        // Update the current enemy display if one is selected
                        var configManager = menuPrefab.GetComponent<ConfigMenuManager>();
                        if (configManager != null)
                        {
                            if (!string.IsNullOrEmpty(configManager.selectedEnemyName))
                            {
                                configManager.UpdateConfigPanel();
                            }
                        }
                    });

                if (showImagesSelector != null)
                {
                    var showImagesRect = showImagesSelector.GetComponent<RectTransform>();
                    showImagesRect.anchorMin = new Vector2(0, 1);
                    showImagesRect.anchorMax = new Vector2(0, 1);
                    showImagesRect.pivot = new Vector2(0, 1);
                    showImagesRect.sizeDelta = new Vector2(170, 30);
                    showImagesRect.anchoredPosition = new Vector2(470, -15);

                    // Fix internal spacing - get the Label component and adjust it
                    var label = showImagesSelector.transform.Find("Label")?.GetComponent<RectTransform>();
                    if (label != null)
                    {
                        label.sizeDelta = new Vector2(100, 30);
                    }
                }

                // Create split view
                var contentPanel = UIHelper.CreatePanel(mainPanelObj.transform, "ContentPanel", new Vector2(780, 520));
                if (contentPanel == null)
                {
                    Plugin.LogError("Failed to create content panel");
                    return;
                }

                var contentRectTransform = contentPanel.GetComponent<RectTransform>();
                contentRectTransform.anchorMin = new Vector2(0, 0);
                contentRectTransform.anchorMax = new Vector2(1, 1);
                contentRectTransform.pivot = new Vector2(0.5f, 0.5f);
                contentRectTransform.offsetMin = new Vector2(10, 60);
                contentRectTransform.offsetMax = new Vector2(-10, -50); // Original offsetMax value

                // Create enemy list (left side)
                var enemyListPanel = UIHelper.CreatePanel(contentPanel.transform, "EnemyListPanel", new Vector2(250, 520));
                if (enemyListPanel == null)
                {
                    Plugin.LogError("Failed to create enemy list panel");
                    return;
                }

                var enemyListRectTransform = enemyListPanel.GetComponent<RectTransform>();
                enemyListRectTransform.anchorMin = new Vector2(0, 0);
                enemyListRectTransform.anchorMax = new Vector2(0, 1);
                enemyListRectTransform.pivot = new Vector2(0, 0.5f);
                enemyListRectTransform.sizeDelta = new Vector2(250, 0);

                // Create section label for the enemy list
                var enemyListLabel = UIHelper.CreateText(enemyListPanel.transform, "ListLabel", "ENEMIES");
                if (enemyListLabel != null)
                {
                    var enemyListLabelRect = enemyListLabel.GetComponent<RectTransform>();
                    enemyListLabelRect.anchorMin = new Vector2(0, 1);
                    enemyListLabelRect.anchorMax = new Vector2(1, 1);
                    enemyListLabelRect.pivot = new Vector2(0.5f, 1);
                    enemyListLabelRect.sizeDelta = new Vector2(0, 30);
                    enemyListLabelRect.anchoredPosition = new Vector2(0, -5);

                    // Style the list label
                    var listLabelText = enemyListLabel.GetComponent<TextMeshProUGUI>();
                    if (listLabelText != null)
                    {
                        listLabelText.fontSize = 16;
                        listLabelText.fontStyle = FontStyles.Bold;
                        listLabelText.color = new Color(1f, 0.9f, 0.5f, 1f);
                    }
                }

                // CREATE GLOBAL SETTINGS BUTTON
                var globalSettingsButtonObj = UIHelper.CreateButton(enemyListPanel.transform, "GlobalSettingsButton", "Global Settings", () => {
                    PlayConfirmSFX();
                    menuManager.ToggleGlobalSettings(); // Use the menuManager instance
                });

                if (globalSettingsButtonObj != null)
                {
                    menuManager.globalSettingsButton = globalSettingsButtonObj;

                    var globalButtonRect = globalSettingsButtonObj.GetComponent<RectTransform>();
                    globalButtonRect.anchorMin = new Vector2(0, 1);
                    globalButtonRect.anchorMax = new Vector2(1, 1);
                    globalButtonRect.pivot = new Vector2(0.5f, 1);
                    globalButtonRect.sizeDelta = new Vector2(-20, 35);
                    globalButtonRect.anchoredPosition = new Vector2(0, -40); // Position below the ENEMIES label

                    // Style the button with LC colors
                    var buttonComponent = globalSettingsButtonObj.GetComponent<Button>();
                    if (buttonComponent != null)
                    {
                        ColorBlock colors = buttonComponent.colors;
                        colors.normalColor = new Color(0.3f, 0.3f, 0.3f, 1f);
                        colors.highlightedColor = new Color(0.4f, 0.4f, 0.4f, 1f);
                        colors.pressedColor = new Color(0.2f, 0.2f, 0.2f, 1f);
                        buttonComponent.colors = colors;
                    }

                    // Style the button text
                    var buttonText = globalSettingsButtonObj.GetComponentInChildren<TextMeshProUGUI>();
                    if (buttonText != null)
                    {
                        buttonText.fontSize = 14;
                        buttonText.color = new Color(1f, 0.9f, 0.5f, 1f);
                        buttonText.fontStyle = FontStyles.Bold;
                    }
                }

                // Create enemy list scroll view
                var enemyScrollView = UIHelper.CreateScrollView(enemyListPanel.transform, "EnemyScrollView", new Vector2(240, 450));
                if (enemyScrollView == null)
                {
                    Plugin.LogError("Failed to create enemy scroll view");
                    return;
                }

                var enemyScrollRectTransform = enemyScrollView.GetComponent<RectTransform>();
                enemyScrollRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                enemyScrollRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                enemyScrollRectTransform.pivot = new Vector2(0.5f, 0.5f);
                enemyScrollRectTransform.anchoredPosition = new Vector2(0, -90);
                enemyScrollRectTransform.sizeDelta = new Vector2(240, 385);

                var scrollRect = enemyScrollView.GetComponent<ScrollRect>();
                if (scrollRect != null)
                {
                    scrollRect.scrollSensitivity = 15f;
                }

                // Check if viewport exists
                var viewport = enemyScrollView.transform.Find("Viewport");
                if (viewport == null)
                {
                    Plugin.LogError("Viewport not found in scroll view");
                    return;
                }

                // Check if content exists
                var content = viewport.Find("Content");
                if (content == null)
                {
                    Plugin.LogError("Content not found in viewport");
                    return;
                }

                // Store reference to enemy list content
                menuManager.enemyListContent = content.GetComponent<RectTransform>();
                if (menuManager.enemyListContent == null)
                {
                    Plugin.LogError("Failed to get RectTransform for enemyListContent");
                    return;
                }

                // Create search field for enemies with LC styling
                var searchContainer = new GameObject("SearchContainer");
                searchContainer.transform.SetParent(enemyListPanel.transform, false);

                var searchContainerRect = searchContainer.AddComponent<RectTransform>();
                searchContainerRect.anchorMin = new Vector2(0, 1);
                searchContainerRect.anchorMax = new Vector2(1, 1);
                searchContainerRect.pivot = new Vector2(0.5f, 1);
                searchContainerRect.sizeDelta = new Vector2(-20, 30);
                searchContainerRect.anchoredPosition = new Vector2(0, -80);

                // Create search field background
                var searchBg = searchContainer.AddComponent<Image>();
                searchBg.color = new Color(0.12f, 0.12f, 0.12f, 1f);

                // Create search icon
                var searchIconObj = new GameObject("SearchIcon");
                searchIconObj.transform.SetParent(searchContainer.transform, false);

                var searchIconRect = searchIconObj.AddComponent<RectTransform>();
                searchIconRect.anchorMin = new Vector2(0, 0.5f);
                searchIconRect.anchorMax = new Vector2(0, 0.5f);
                searchIconRect.pivot = new Vector2(0.5f, 0.5f);
                searchIconRect.sizeDelta = new Vector2(20, 20);
                searchIconRect.anchoredPosition = new Vector2(15, 0);

                var searchIconText = searchIconObj.AddComponent<TextMeshProUGUI>();
                searchIconText.text = ">"; // Search icon character
                searchIconText.fontSize = 16;
                searchIconText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                searchIconText.alignment = TextAlignmentOptions.Center;

                // Create search input field
                var searchInput = searchContainer.AddComponent<TMP_InputField>();

                // Create input text area
                var searchTextObj = new GameObject("Text");
                searchTextObj.transform.SetParent(searchContainer.transform, false);

                var searchTextRect = searchTextObj.AddComponent<RectTransform>();
                searchTextRect.anchorMin = new Vector2(0, 0);
                searchTextRect.anchorMax = new Vector2(1, 1);
                searchTextRect.offsetMin = new Vector2(35, 2);
                searchTextRect.offsetMax = new Vector2(-5, -2);

                var searchText = searchTextObj.AddComponent<TextMeshProUGUI>();
                searchText.fontSize = 14;
                searchText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
                searchText.alignment = TextAlignmentOptions.Left;

                // Create placeholder
                var placeholderObj = new GameObject("Placeholder");
                placeholderObj.transform.SetParent(searchContainer.transform, false);

                var placeholderRect = placeholderObj.AddComponent<RectTransform>();
                placeholderRect.anchorMin = new Vector2(0, 0);
                placeholderRect.anchorMax = new Vector2(1, 1);
                placeholderRect.offsetMin = new Vector2(35, 2);
                placeholderRect.offsetMax = new Vector2(-5, -2);

                var placeholder = placeholderObj.AddComponent<TextMeshProUGUI>();
                placeholder.text = "Search enemies...";
                placeholder.fontSize = 14;
                placeholder.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                placeholder.alignment = TextAlignmentOptions.Left;

                // Configure input field
                searchInput.textComponent = searchText;
                searchInput.placeholder = placeholder;
                searchInput.text = "";
                menuManager.searchInputField = searchInput;

                // Add event listener for search input
                searchInput.onValueChanged.AddListener((searchText) => {
                    menuManager.FilterEnemyList(searchText);
                });

                // Use original positioning for scroll view
                enemyScrollRectTransform.anchoredPosition = new Vector2(0, -70);
                enemyScrollRectTransform.sizeDelta = new Vector2(240, 405);

                // Create config panel (right side)
                var configPanelObj = UIHelper.CreatePanel(contentPanel.transform, "ConfigPanel", new Vector2(500, 520));
                if (configPanelObj == null)
                {
                    Plugin.LogError("Failed to create config panel");
                    return;
                }

                var configPanelRectTransform = configPanelObj.GetComponent<RectTransform>();
                configPanelRectTransform.anchorMin = new Vector2(1, 0);
                configPanelRectTransform.anchorMax = new Vector2(1, 1);
                configPanelRectTransform.pivot = new Vector2(1, 0.5f);
                configPanelRectTransform.sizeDelta = new Vector2(500, 0);

                menuManager.enemyConfigPanel = configPanelObj;

                // Create "No Selection" text
                var noSelectionObj = UIHelper.CreateText(configPanelObj.transform, "NoSelection", "Select an enemy from the list");
                if (noSelectionObj != null)
                {
                    var noSelectionRectTransform = noSelectionObj.GetComponent<RectTransform>();
                    noSelectionRectTransform.anchorMin = new Vector2(0, 0);
                    noSelectionRectTransform.anchorMax = new Vector2(1, 1);
                    noSelectionRectTransform.offsetMin = Vector2.zero;
                    noSelectionRectTransform.offsetMax = Vector2.zero;
                }

                // Add a message about missing enemies with LC styling
                var missingEnemiesObj = UIHelper.CreatePanel(mainPanelObj.transform, "MissingEnemiesMessage", new Vector2(780, 40));
                if (missingEnemiesObj != null)
                {
                    var missingEnemiesRect = missingEnemiesObj.GetComponent<RectTransform>();
                    missingEnemiesRect.anchorMin = new Vector2(0.5f, 0);
                    missingEnemiesRect.anchorMax = new Vector2(0.5f, 0);
                    missingEnemiesRect.pivot = new Vector2(0.5f, 0);
                    missingEnemiesRect.anchoredPosition = new Vector2(0, 10);

                    var missingEnemiesImage = missingEnemiesObj.GetComponent<Image>();
                    if (missingEnemiesImage != null)
                    {
                        missingEnemiesImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                    }

                    var missingEnemiesText = UIHelper.CreateText(missingEnemiesObj.transform, "Text",
                        "If enemies are missing, start a round to generate their entries.");

                    if (missingEnemiesText != null)
                    {
                        var textComponent = missingEnemiesText.GetComponent<TextMeshProUGUI>();
                        if (textComponent != null)
                        {
                            textComponent.fontSize = 14;
                            textComponent.color = new Color(1f, 0.9f, 0.5f, 1f);
                        }
                    }
                }

                // Initialize the image loader
                EnemyImageLoader.Initialize();

                Plugin.LogInfo("Config menu created successfully");
            }
            catch (Exception ex)
            {
                Plugin.LogError($"Error creating menu prefab: {ex.Message}\nStack trace: {ex.StackTrace}");
            }
        }

        private void ToggleGlobalSettings()
        {
            isShowingGlobalSettings = !isShowingGlobalSettings;

            if (isShowingGlobalSettings)
            {
                ShowGlobalSettings();
                UpdateGlobalSettingsButtonStyle(true);
            }
            else
            {
                // Clear selection and go back to normal view
                selectedEnemyName = null;
                UpdateConfigPanel();
                UpdateGlobalSettingsButtonStyle(false);
            }
        }

        private void UpdateGlobalSettingsButtonStyle(bool isActive)
        {
            if (globalSettingsButton == null) return;

            var buttonComponent = globalSettingsButton.GetComponent<Button>();
            var buttonText = globalSettingsButton.GetComponentInChildren<TextMeshProUGUI>();

            if (buttonComponent != null)
            {
                ColorBlock colors = buttonComponent.colors;
                if (isActive)
                {
                    // Highlighted style when showing global settings
                    colors.normalColor = new Color(0.5f, 0.5f, 0.5f, 1f);
                    colors.highlightedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
                }
                else
                {
                    // Normal style
                    colors.normalColor = new Color(0.3f, 0.3f, 0.3f, 1f);
                    colors.highlightedColor = new Color(0.4f, 0.4f, 0.4f, 1f);
                }
                buttonComponent.colors = colors;
            }

            if (buttonText != null)
            {
                buttonText.color = isActive ? Color.white : new Color(1f, 0.9f, 0.5f, 1f);
            }
        }

        private void ShowGlobalSettings()
        {
            try
            {
                // Clear the right panel
                if (enemyConfigPanel == null) return;

                // Clear existing config controls
                foreach (Transform child in enemyConfigPanel.transform)
                {
                    if (child != null && child.name != "NoSelection")
                    {
                        Destroy(child.gameObject);
                    }
                }

                // Hide "No Selection" text
                var noSelectionObj = enemyConfigPanel.transform.Find("NoSelection");
                if (noSelectionObj != null)
                {
                    noSelectionObj.gameObject.SetActive(false);
                }

                // Create global settings title
                var titleObj = UIHelper.CreateText(enemyConfigPanel.transform, "Title", "Global Settings");
                if (titleObj != null)
                {
                    var titleRectTransform = titleObj.GetComponent<RectTransform>();
                    titleRectTransform.anchorMin = new Vector2(0, 1);
                    titleRectTransform.anchorMax = new Vector2(1, 1);
                    titleRectTransform.pivot = new Vector2(0.5f, 1);
                    titleRectTransform.sizeDelta = new Vector2(0, 40);

                    // Style the title like LC headers
                    var titleText = titleObj.GetComponent<TextMeshProUGUI>();
                    if (titleText != null)
                    {
                        titleText.fontSize = 22;
                        titleText.color = new Color(1f, 0.9f, 0.5f, 1f);
                        titleText.fontStyle = FontStyles.Bold;
                    }
                }

                // Create main panel for global settings
                var globalSettingsPanel = UIHelper.CreatePanel(enemyConfigPanel.transform, "GlobalSettingsPanel", new Vector2(480, 400));
                if (globalSettingsPanel == null) return;

                var globalSettingsRect = globalSettingsPanel.GetComponent<RectTransform>();
                globalSettingsRect.anchorMin = new Vector2(0.5f, 0.5f);
                globalSettingsRect.anchorMax = new Vector2(0.5f, 0.5f);
                globalSettingsRect.pivot = new Vector2(0.5f, 0.5f);
                globalSettingsRect.anchoredPosition = new Vector2(0, 30);
                globalSettingsRect.sizeDelta = new Vector2(480, 380);

                // Style the panel
                var panelImage = globalSettingsPanel.GetComponent<Image>();
                if (panelImage != null)
                {
                    panelImage.color = new Color(0.0f, 0.0f, 0.0f, 0.0f);
                }

                // Add content layout
                var layout = globalSettingsPanel.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(20, 20, 20, 20);
                layout.spacing = 20;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                layout.childControlWidth = true;
                layout.childControlHeight = false;
                layout.childAlignment = TextAnchor.UpperLeft;

                // Section header for traps
                var trapsHeaderPanel = new GameObject("TrapsHeaderPanel");
                trapsHeaderPanel.transform.SetParent(globalSettingsPanel.transform, false);

                var trapsHeaderRect = trapsHeaderPanel.AddComponent<RectTransform>();
                trapsHeaderRect.sizeDelta = new Vector2(0, 30);

                var trapsHeaderText = UIHelper.CreateText(trapsHeaderPanel.transform, "TrapsHeaderText",
                    "Trap Configuration", TextAlignmentOptions.Left);

                var trapsHeaderTextComp = trapsHeaderText?.GetComponent<TextMeshProUGUI>();
                if (trapsHeaderTextComp != null)
                {
                    trapsHeaderTextComp.fontSize = 18;
                    trapsHeaderTextComp.fontStyle = FontStyles.Bold;
                    trapsHeaderTextComp.color = new Color(1f, 0.9f, 0.5f, 1f);
                }

                // Spike traps setting
                var spikeTrapsSelector = UIHelper.CreateYesNoSelector(globalSettingsPanel.transform, "SpikeTrapsSelector",
                    "Allow spike traps to kill enemies:", Plugin.AllowSpikeTrapsToKillEnemies.Value, (allowSpikeTraps) => {
                        Plugin.AllowSpikeTrapsToKillEnemies.Value = allowSpikeTraps;
                        Plugin.Instance.Config.Save();
                        PlayConfirmSFX();
                        Plugin.LogInfo($"Spike trap kills {(allowSpikeTraps ? "enabled" : "disabled")}");
                    });

                if (spikeTrapsSelector != null)
                {
                    var spikeTrapsRect = spikeTrapsSelector.GetComponent<RectTransform>();
                    spikeTrapsRect.sizeDelta = new Vector2(0, 30);
                }

                // Add description text for spike traps
                var spikeDescPanel = new GameObject("SpikeDescPanel");
                spikeDescPanel.transform.SetParent(globalSettingsPanel.transform, false);

                var spikeDescRect = spikeDescPanel.AddComponent<RectTransform>();
                spikeDescRect.sizeDelta = new Vector2(0, 40);

                var spikeDescText = UIHelper.CreateText(spikeDescPanel.transform, "SpikeDescText",
                    "When disabled, spike roof traps will not be able to kill enemies managed by this mod.", TextAlignmentOptions.Left);

                var spikeDescTextComp = spikeDescText?.GetComponent<TextMeshProUGUI>();
                if (spikeDescTextComp != null)
                {
                    spikeDescTextComp.fontSize = 12;
                    spikeDescTextComp.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                    spikeDescTextComp.fontStyle = FontStyles.Italic;
                }

                Plugin.LogInfo("Global settings panel created successfully");
            }
            catch (Exception ex)
            {
                Plugin.LogError($"Error creating global settings panel: {ex.Message}");
            }
        }
        private void CreateEnemyListEntry(EnemyConfigData config)
        {
            // Create stylized list entry
            var entryObj = UIHelper.CreatePanel(enemyListContent, $"Enemy_{config.SanitizedName}", new Vector2(230, ENTRY_HEIGHT));

            // Add some margin between entries
            var layoutElement = entryObj.AddComponent<LayoutElement>();
            layoutElement.minHeight = ENTRY_HEIGHT;
            layoutElement.preferredHeight = ENTRY_HEIGHT;
            layoutElement.flexibleHeight = 0;

            // Create entry button with LC-styled colors
            var buttonObj = UIHelper.CreateButton(entryObj.transform, "Button", config.Name, () => {
                SelectEnemy(config.Name);
            });

            var buttonRectTransform = buttonObj.GetComponent<RectTransform>();
            buttonRectTransform.anchorMin = Vector2.zero;
            buttonRectTransform.anchorMax = Vector2.one;
            buttonRectTransform.offsetMin = new Vector2(5, 3);  // Add padding inside the button
            buttonRectTransform.offsetMax = new Vector2(-5, -3); // Add padding inside the button

            // Add indicator for enemy status
            var statusObj = new GameObject("Status");
            statusObj.transform.SetParent(buttonObj.transform, false);

            var statusRectTransform = statusObj.AddComponent<RectTransform>();
            statusRectTransform.anchorMin = new Vector2(0, 0.5f);
            statusRectTransform.anchorMax = new Vector2(0, 0.5f);
            statusRectTransform.pivot = new Vector2(0, 0.5f);
            statusRectTransform.sizeDelta = new Vector2(10, 10);
            statusRectTransform.anchoredPosition = new Vector2(10, 0);

            var statusImage = statusObj.AddComponent<Image>();
            statusImage.color = config.GetStatusColor();

            // Style the text with LC font settings
            var textObj = buttonObj.transform.Find("Text");
            if (textObj != null)
            {
                var textRectTransform = textObj.GetComponent<RectTransform>();
                textRectTransform.offsetMin = new Vector2(25, 0);  // Increase text offset to make room for status indicator

                var tmpText = textObj.GetComponent<TextMeshProUGUI>();
                tmpText.fontSize = UITheme.NormalFontSize;
                tmpText.alignment = TextAlignmentOptions.Left;
            }

            // Store entry for filtering
            enemyEntries[config.Name] = entryObj;
        }

        // Simplified FilterEnemyList method that only uses search text
        private void FilterEnemyList(string searchText = "")
        {
            // Normalize search text for case-insensitive comparison
            searchText = searchText?.ToLower() ?? "";

            // Store the last search text
            lastSearchText = searchText;

            foreach (var entry in enemyEntries)
            {
                var config = enemyConfigs.Find(c => c.Name == entry.Key);
                if (config == null) continue;

                // Show/hide based on search text
                bool visibleBySearch = string.IsNullOrEmpty(searchText) ||
                                      config.Name.ToLower().Contains(searchText);

                entry.Value.SetActive(visibleBySearch);
            }

            // Update UI to indicate if no results found
            bool anyVisible = enemyEntries.Any(e => e.Value.activeSelf);

            // Check if we have a "NoResults" text object
            GameObject noResultsObj = enemyListContent.transform.Find("NoResults")?.gameObject;

            // Create one if it doesn't exist and there are no visible results
            if (!anyVisible && noResultsObj == null && !string.IsNullOrEmpty(searchText))
            {
                noResultsObj = UIHelper.CreateText(enemyListContent.transform, "NoResults",
                    $"No enemies found matching \"{searchText}\"");

                var noResultsText = noResultsObj.GetComponent<TextMeshProUGUI>();
                if (noResultsText != null)
                {
                    noResultsText.fontSize = 14;
                    noResultsText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                    noResultsText.alignment = TextAlignmentOptions.Center;
                }
            }
            else if (anyVisible && noResultsObj != null)
            {
                // Remove the "no results" message if there are visible results
                Destroy(noResultsObj);
            }
            else if (noResultsObj != null && string.IsNullOrEmpty(searchText))
            {
                // Remove the "no results" message if the search field is empty
                Destroy(noResultsObj);
            }
        }

        private void SelectEnemy(string enemyName)
        {
            PlayConfirmSFX();
            selectedEnemyName = enemyName;

            // If we were showing global settings, turn that off
            if (isShowingGlobalSettings)
            {
                isShowingGlobalSettings = false;
                UpdateGlobalSettingsButtonStyle(false);
            }

            UpdateConfigPanel();
        }

        private void UpdateConfigPanel()
        {
            try
            {
                // Check if enemyConfigPanel exists
                if (enemyConfigPanel == null)
                {
                    Plugin.LogError("enemyConfigPanel is null in UpdateConfigPanel");
                    return;
                }

                // Clear existing config controls
                foreach (Transform child in enemyConfigPanel.transform)
                {
                    if (child != null && child.name != "NoSelection")
                    {
                        Destroy(child.gameObject);
                    }
                }

                // Show/hide "No Selection" text
                var noSelectionObj = enemyConfigPanel.transform.Find("NoSelection");
                if (noSelectionObj != null)
                {
                    noSelectionObj.gameObject.SetActive(string.IsNullOrEmpty(selectedEnemyName));
                }

                if (string.IsNullOrEmpty(selectedEnemyName)) return;

                // Find the selected enemy's config
                var config = enemyConfigs.Find(c => c.Name == selectedEnemyName);
                if (config == null) return;

                // Create config controls with LC styling

                // Title
                var titleObj = UIHelper.CreateText(enemyConfigPanel.transform, "Title", config.Name);
                if (titleObj != null)
                {
                    var titleRectTransform = titleObj.GetComponent<RectTransform>();
                    titleRectTransform.anchorMin = new Vector2(0, 1);
                    titleRectTransform.anchorMax = new Vector2(1, 1);
                    titleRectTransform.pivot = new Vector2(0.5f, 1);
                    titleRectTransform.sizeDelta = new Vector2(0, 40);

                    // Style the title like LC headers
                    var titleText = titleObj.GetComponent<TextMeshProUGUI>();
                    if (titleText != null)
                    {
                        titleText.fontSize = 22;
                        titleText.color = new Color(1f, 0.9f, 0.5f, 1f);
                        titleText.fontStyle = FontStyles.Bold;
                    }
                }

                // Main panel for controls
                var controlsPanel = UIHelper.CreatePanel(enemyConfigPanel.transform, "ControlsPanel", new Vector2(480, 400));
                if (controlsPanel == null)
                {
                    Plugin.LogError("Failed to create controls panel");
                    return;
                }

                var controlsRectTransform = controlsPanel.GetComponent<RectTransform>();
                controlsRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                controlsRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                controlsRectTransform.pivot = new Vector2(0.5f, 0.5f);
                controlsRectTransform.anchoredPosition = new Vector2(0, 30);
                controlsRectTransform.sizeDelta = new Vector2(480, 380);

                // Style the controls panel
                var panelImage = controlsPanel.GetComponent<Image>();
                if (panelImage != null)
                {
                    panelImage.color = new Color(0.0f, 0.0f, 0.0f, 0.0f);
                }

                // Add content layout
                var layout = controlsPanel.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(20, 20, 20, 20);
                layout.spacing = 15;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                layout.childControlWidth = true;
                layout.childControlHeight = false;
                layout.childAlignment = TextAnchor.UpperLeft;

                // Enemy status display
                var statusPanel = new GameObject("StatusPanel");
                statusPanel.transform.SetParent(controlsPanel.transform, false);

                var statusPanelRect = statusPanel.AddComponent<RectTransform>();
                statusPanelRect.sizeDelta = new Vector2(0, 30);

                var statusText = UIHelper.CreateText(statusPanel.transform, "StatusText",
                    $"Current Status: {config.GetStatusText()}", TextAlignmentOptions.Left);

                var statusTextComp = statusText?.GetComponent<TextMeshProUGUI>();
                if (statusTextComp != null)
                {
                    statusTextComp.color = config.GetStatusColor();
                }

                // Add auto-save notification text
                var autoSavePanel = new GameObject("AutoSavePanel");
                autoSavePanel.transform.SetParent(controlsPanel.transform, false);

                var autoSavePanelRect = autoSavePanel.AddComponent<RectTransform>();
                autoSavePanelRect.sizeDelta = new Vector2(0, 25);

                var autoSaveText = UIHelper.CreateText(autoSavePanel.transform, "AutoSaveText",
                    "All changes are saved immediately", TextAlignmentOptions.Left);

                var autoSaveTextComp = autoSaveText?.GetComponent<TextMeshProUGUI>();
                if (autoSaveTextComp != null)
                {
                    autoSaveTextComp.fontSize = 14;
                    autoSaveTextComp.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                    autoSaveTextComp.fontStyle = FontStyles.Italic;
                }

                // Yes/No selectors with LC styling
                var enabledSelector = UIHelper.CreateYesNoSelector(controlsPanel.transform, "EnabledSelector",
                    "Affected by ECDA mod:", config.IsEnabled, (isYes) => {
                        config.IsEnabled = isYes;
                        UpdateSelectorEnablements(controlsPanel, isYes);

                        // Update status text
                        if (statusTextComp != null)
                        {
                            statusTextComp.text = $"Current Status: {config.GetStatusText()}";
                            statusTextComp.color = config.GetStatusColor();
                        }

                        // Auto-save the config immediately
                        SaveCurrentEnemyConfig();

                        // Play sound for feedback
                        PlayConfirmSFX();
                    });

                if (enabledSelector != null)
                {
                    var enabledRect = enabledSelector.GetComponent<RectTransform>();
                    enabledRect.sizeDelta = new Vector2(0, 30);
                }

                // Can die selector
                var canDieSelector = UIHelper.CreateYesNoSelector(controlsPanel.transform, "CanDieSelector",
                    "Can be damaged and killed:", config.CanDie, (isYes) => {
                        config.CanDie = isYes;

                        // Update status text
                        if (statusTextComp != null)
                        {
                            statusTextComp.text = $"Current Status: {config.GetStatusText()}";
                            statusTextComp.color = config.GetStatusColor();
                        }

                        // Auto-save the config immediately
                        SaveCurrentEnemyConfig();

                        // Play sound for feedback
                        PlayConfirmSFX();
                    });

                if (canDieSelector != null)
                {
                    var canDieRect = canDieSelector.GetComponent<RectTransform>();
                    canDieRect.sizeDelta = new Vector2(0, 30);
                }

                // Despawn selector
                var despawnSelector = UIHelper.CreateYesNoSelector(controlsPanel.transform, "DespawnSelector",
                    "Despawn after death:", config.ShouldDespawn, (isYes) => {
                        config.ShouldDespawn = isYes;

                        // Auto-save the config immediately
                        SaveCurrentEnemyConfig();

                        // Play sound for feedback
                        PlayConfirmSFX();
                    });

                if (despawnSelector != null)
                {
                    var despawnRect = despawnSelector.GetComponent<RectTransform>();
                    despawnRect.sizeDelta = new Vector2(0, 30);
                }

                // Health input with arrows - styled like LC
                var healthInput = UIHelper.CreateNumericInputWithArrows(
                    controlsPanel.transform,
                    "HealthInput",
                    "Health:",
                    config.Health,
                    1,  // Minimum health value of 1
                    100, // Maximum health value of 100 (adjust as needed)
                    (newValue) => {
                        config.Health = newValue;

                        // Auto-save the config immediately
                        SaveCurrentEnemyConfig();

                        // Play sound for feedback
                        PlayConfirmSFX();
                    }
                );

                // Store reference to health input for enabling/disabling
                if (healthInput != null)
                {
                    healthInput.name = "HealthPanel"; // Keep the same name for consistency with other code
                }

                // Update selector enablements based on current settings
                UpdateSelectorEnablements(controlsPanel, config.IsEnabled);

                // Update the enemy image display
                UpdateEnemyImageVisibility(controlsPanel, config.Name);
            }
            catch (Exception ex)
            {
                Plugin.LogError($"Error in UpdateConfigPanel: {ex.Message}\nStack trace: {ex.StackTrace}");
            }
        }

        private void UpdateSelectorEnablements(GameObject controlsPanel, bool isEnabled)
        {
            // Find all other controls 
            var canDieSelector = controlsPanel.transform.Find("CanDieSelector")?.GetComponent<UIHelper.StateHolder>();
            var despawnSelector = controlsPanel.transform.Find("DespawnSelector")?.GetComponent<UIHelper.StateHolder>();
            var healthInput = controlsPanel.transform.Find("HealthPanel")?.GetComponent<UIHelper.NumericInputState>();

            if (canDieSelector != null)
            {
                // Get the current config
                var config = enemyConfigs.Find(c => c.Name == selectedEnemyName);
                if (config != null)
                {
                    // Set interactable state based on isEnabled
                    canDieSelector.SetInteractable(isEnabled);

                    // Also sync the visual state with the actual config value
                    canDieSelector.UpdateVisualState(config.CanDie);
                }
            }

            if (despawnSelector != null)
            {
                // Get the current config
                var config = enemyConfigs.Find(c => c.Name == selectedEnemyName);
                if (config != null)
                {
                    // Set interactable state based on isEnabled  
                    despawnSelector.SetInteractable(isEnabled);

                    // Also sync the visual state with the actual config value
                    despawnSelector.UpdateVisualState(config.ShouldDespawn);
                }
            }

            if (healthInput != null)
            {
                // Get the current config
                var config = enemyConfigs.Find(c => c.Name == selectedEnemyName);
                if (config != null)
                {
                    // Set interactable state based on isEnabled
                    healthInput.SetInteractable(isEnabled);

                    // Update the health input value to match the config
                    healthInput.SetValue(config.Health);
                }
            }
        }

        /// <summary>
        /// Shows or hides the enemy image based on the configuration setting
        /// </summary>
        private void UpdateEnemyImageVisibility(GameObject controlsPanel, string enemyName)
        {
            try
            {
                // Find the image container if it exists
                var imageContainer = controlsPanel.transform.Find("ImageContainer")?.gameObject;

                // If it doesn't exist and images are enabled, create it
                if (imageContainer == null && UIConfiguration.Instance.ShouldShowEnemyImages())
                {
                    // Create a container for the image - no background
                    imageContainer = new GameObject("ImageContainer");
                    imageContainer.transform.SetParent(controlsPanel.transform, false);

                    // Position the container at the BOTTOM of the layout
                    imageContainer.transform.SetAsLastSibling();

                    // Set up the rect transform
                    var imageContainerRect = imageContainer.AddComponent<RectTransform>();
                    imageContainerRect.sizeDelta = new Vector2(0, 180);

                    // Add layout element for spacing
                    var layoutElement = imageContainer.AddComponent<LayoutElement>();
                    layoutElement.minHeight = 180;
                    layoutElement.preferredHeight = 180;
                    layoutElement.flexibleHeight = 0;
                    

                    // Create the image display
                    var imageObj = new GameObject("EnemyImage");
                    imageObj.transform.SetParent(imageContainer.transform, false);

                    var imageRect = imageObj.AddComponent<RectTransform>();
                    imageRect.anchorMin = new Vector2(0.5f, 0.5f);
                    imageRect.anchorMax = new Vector2(0.5f, 0.5f);
                    imageRect.pivot = new Vector2(0.5f, 0.5f);
                    imageRect.sizeDelta = new Vector2(170, 170);

                    // Add the actual image component
                    var imageComponent = imageObj.AddComponent<Image>();
                    imageComponent.preserveAspect = true;
                }

                // Now show/hide the image
                if (imageContainer != null)
                {
                    if (UIConfiguration.Instance.ShouldShowEnemyImages() && !string.IsNullOrEmpty(enemyName))
                    {
                        // Try to load the texture first to see if we should show the container
                        var texture = EnemyImageLoader.GetEnemyTexture(enemyName);
                        if (texture != null)
                        {
                            imageContainer.SetActive(true);

                            // Find the image component
                            var imageObj = imageContainer.transform.Find("EnemyImage");
                            if (imageObj != null)
                            {
                                var image = imageObj.GetComponent<Image>();
                                if (image != null)
                                {
                                    // Create a sprite from the texture
                                    var sprite = Sprite.Create(
                                        texture,
                                        new Rect(0, 0, texture.width, texture.height),
                                        new Vector2(0.5f, 0.5f)
                                    );

                                    // Set the sprite
                                    image.sprite = sprite;
                                    image.preserveAspect = true;
                                }
                            }
                        }
                        else
                        {
                            // No image available, hide the container
                            imageContainer.SetActive(false);
                        }
                    }
                    else
                    {
                        // Images disabled or no enemy selected, hide the container
                        imageContainer.SetActive(false);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error updating enemy image visibility: {ex.Message}");
            }
        }

        private void SaveCurrentEnemyConfig()
        {
            if (string.IsNullOrEmpty(selectedEnemyName)) return;

            var config = enemyConfigs.Find(c => c.Name == selectedEnemyName);
            if (config == null) return;

            // Save the configuration
            ConfigBridge.SaveEnemyConfig(config);

            // Update the list entry
            if (enemyEntries.TryGetValue(config.Name, out var entryObj))
            {
                var statusObj = entryObj.transform.Find("Button/Status");
                if (statusObj != null)
                {
                    var statusImage = statusObj.GetComponent<Image>();
                    if (statusImage != null)
                    {
                        statusImage.color = config.GetStatusColor();
                    }
                }
            }
        }

        private void OnDisable()
        {
            // Clear the image cache when the menu is closed
            EnemyImageLoader.ClearCache();
        }
    }
}
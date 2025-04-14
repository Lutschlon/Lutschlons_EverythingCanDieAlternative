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
        private TMP_Dropdown categoryDropdown;

        // Configuration data
        private List<EnemyConfigData> enemyConfigs = new List<EnemyConfigData>();
        private Dictionary<string, GameObject> enemyEntries = new Dictionary<string, GameObject>();

        // Current selection
        private string selectedEnemyName;

        // Constants
        private const float ENTRY_HEIGHT = 35f;

        // Flag to prevent duplicate refresh
        private static bool refreshScheduled = false;

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
            Plugin.Log.LogInfo("ToggleConfigMenu called");

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
                Plugin.Log.LogInfo("Menu opened, scheduling refresh");
                var menuManager = menuPrefab.GetComponent<ConfigMenuManager>();
                menuManager.ScheduleRefresh();
            }
        }

        // Schedule a refresh with a slight delay to avoid multiple refreshes
        private void ScheduleRefresh()
        {
            if (!refreshScheduled)
            {
                refreshScheduled = true;
                StartCoroutine(DelayedRefresh());
            }
        }

        private IEnumerator DelayedRefresh()
        {
            Plugin.Log.LogInfo("Starting delayed refresh");
            yield return new WaitForSeconds(0.1f);
            RefreshEnemyData();
            refreshScheduled = false;
            Plugin.Log.LogInfo("Delayed refresh completed");
        }

        private static void CreateMenuPrefab()
        {
            try
            {
                Plugin.Log.LogInfo("Creating menu prefab");

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
                    Plugin.Log.LogError("Failed to create overlay panel");
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
                    Plugin.Log.LogError("Failed to create main panel");
                    return;
                }

                menuManager.menuPanel = mainPanelObj;

                // Add a background gradient image for more LC-like appearance
                var panelImage = mainPanelObj.GetComponent<Image>();
                if (panelImage != null)
                {
                    panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
                }

                // Create title with stylized text
                var titleObj = UIHelper.CreateText(mainPanelObj.transform, "Title", "ENEMY CONFIGURATION");
                if (titleObj != null)
                {
                    var titleRectTransform = titleObj.GetComponent<RectTransform>();
                    titleRectTransform.anchorMin = new Vector2(0, 1);
                    titleRectTransform.anchorMax = new Vector2(1, 1);
                    titleRectTransform.pivot = new Vector2(0.5f, 1);
                    titleRectTransform.sizeDelta = new Vector2(0, 50);
                    titleRectTransform.anchoredPosition = new Vector2(0, 0);

                    // Style the title text
                    var titleText = titleObj.GetComponent<TextMeshProUGUI>();
                    if (titleText != null)
                    {
                        titleText.fontSize = 24;
                        titleText.fontStyle = FontStyles.Bold;
                        titleText.color = new Color(1f, 0.9f, 0.5f, 1f);
                    }
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

                // Create split view
                var contentPanel = UIHelper.CreatePanel(mainPanelObj.transform, "ContentPanel", new Vector2(780, 520));
                if (contentPanel == null)
                {
                    Plugin.Log.LogError("Failed to create content panel");
                    return;
                }

                var contentRectTransform = contentPanel.GetComponent<RectTransform>();
                contentRectTransform.anchorMin = new Vector2(0, 0);
                contentRectTransform.anchorMax = new Vector2(1, 1);
                contentRectTransform.pivot = new Vector2(0.5f, 0.5f);
                contentRectTransform.offsetMin = new Vector2(10, 60);
                contentRectTransform.offsetMax = new Vector2(-10, -50);

                // Create enemy list (left side)
                var enemyListPanel = UIHelper.CreatePanel(contentPanel.transform, "EnemyListPanel", new Vector2(250, 520));
                if (enemyListPanel == null)
                {
                    Plugin.Log.LogError("Failed to create enemy list panel");
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

                // Create enemy list scroll view
                var enemyScrollView = UIHelper.CreateScrollView(enemyListPanel.transform, "EnemyScrollView", new Vector2(240, 450));
                if (enemyScrollView == null)
                {
                    Plugin.Log.LogError("Failed to create enemy scroll view");
                    return;
                }

                var enemyScrollRectTransform = enemyScrollView.GetComponent<RectTransform>();
                enemyScrollRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                enemyScrollRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                enemyScrollRectTransform.pivot = new Vector2(0.5f, 0.5f);
                enemyScrollRectTransform.anchoredPosition = new Vector2(0, -25);

                // Check if viewport exists
                var viewport = enemyScrollView.transform.Find("Viewport");
                if (viewport == null)
                {
                    Plugin.Log.LogError("Viewport not found in scroll view");
                    return;
                }

                // Check if content exists
                var content = viewport.Find("Content");
                if (content == null)
                {
                    Plugin.Log.LogError("Content not found in viewport");
                    return;
                }

                // Store reference to enemy list content
                menuManager.enemyListContent = content.GetComponent<RectTransform>();
                if (menuManager.enemyListContent == null)
                {
                    Plugin.Log.LogError("Failed to get RectTransform for enemyListContent");
                    return;
                }

                // Create category dropdown with LC style
                var dropdownObj = new GameObject("CategoryDropdown");
                dropdownObj.transform.SetParent(enemyListPanel.transform, false);

                var dropdownRectTransform = dropdownObj.AddComponent<RectTransform>();
                dropdownRectTransform.anchorMin = new Vector2(0, 1);
                dropdownRectTransform.anchorMax = new Vector2(1, 1);
                dropdownRectTransform.pivot = new Vector2(0.5f, 1);
                dropdownRectTransform.sizeDelta = new Vector2(-20, 30);
                dropdownRectTransform.anchoredPosition = new Vector2(0, -35);

                var dropdown = dropdownObj.AddComponent<TMP_Dropdown>();
                menuManager.categoryDropdown = dropdown;

                // Style the dropdown to match LC
                var dropdownImage = dropdownObj.GetComponent<Image>();
                if (dropdownImage != null)
                {
                    dropdownImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
                }

                // Add dropdown options
                dropdown.options.Add(new TMP_Dropdown.OptionData("All Enemies"));
                dropdown.options.Add(new TMP_Dropdown.OptionData("Enabled"));
                dropdown.options.Add(new TMP_Dropdown.OptionData("Disabled"));
                dropdown.options.Add(new TMP_Dropdown.OptionData("Killable"));
                dropdown.options.Add(new TMP_Dropdown.OptionData("Immortal"));

                dropdown.onValueChanged.AddListener((index) => {
                    menuManager.FilterEnemyList(index);
                });

                // Create config panel (right side)
                var configPanelObj = UIHelper.CreatePanel(contentPanel.transform, "ConfigPanel", new Vector2(500, 520));
                if (configPanelObj == null)
                {
                    Plugin.Log.LogError("Failed to create config panel");
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
                        "If enemies are missing, start a round to generate their entries." +
                        "\nThis menu reads existing config files only.");

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

                // Create a refresh button with LC style
                var refreshButtonObj = UIHelper.CreateButton(mainPanelObj.transform, "RefreshButton", "REFRESH", () => {
                    PlayConfirmSFX();
                    menuManager.ScheduleRefresh();
                });

                if (refreshButtonObj != null)
                {
                    var refreshButtonRectTransform = refreshButtonObj.GetComponent<RectTransform>();
                    refreshButtonRectTransform.anchorMin = new Vector2(0, 1);
                    refreshButtonRectTransform.anchorMax = new Vector2(0, 1);
                    refreshButtonRectTransform.pivot = new Vector2(0, 1);
                    refreshButtonRectTransform.sizeDelta = new Vector2(100, 40);
                    refreshButtonRectTransform.anchoredPosition = new Vector2(10, -10);
                }

                Plugin.Log.LogInfo("Config menu created successfully");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error creating menu prefab: {ex.Message}\nStack trace: {ex.StackTrace}");
            }
        }

        public void RefreshEnemyData()
        {
            Plugin.Log.LogInfo("RefreshEnemyData called");

            // Clear existing enemy entries
            foreach (Transform child in enemyListContent)
            {
                Destroy(child.gameObject);
            }
            enemyEntries.Clear();

            // Load all enemy configurations
            enemyConfigs = ConfigBridge.LoadAllEnemyConfigs();

            // Create list entries for each enemy
            foreach (var config in enemyConfigs)
            {
                CreateEnemyListEntry(config);
            }

            // Apply initial filter
            FilterEnemyList(categoryDropdown.value);

            // Clear selection
            selectedEnemyName = null;
            UpdateConfigPanel();

            Plugin.Log.LogInfo($"Loaded {enemyConfigs.Count} enemy configurations");
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

        private void FilterEnemyList(int filterIndex)
        {
            foreach (var entry in enemyEntries)
            {
                var config = enemyConfigs.Find(c => c.Name == entry.Key);
                if (config == null) continue;

                bool visible = false;

                switch (filterIndex)
                {
                    case 0: // All
                        visible = true;
                        break;
                    case 1: // Enabled
                        visible = config.IsEnabled;
                        break;
                    case 2: // Disabled
                        visible = !config.IsEnabled;
                        break;
                    case 3: // Killable
                        visible = config.IsEnabled && config.CanDie;
                        break;
                    case 4: // Immortal
                        visible = config.IsEnabled && !config.CanDie;
                        break;
                }

                entry.Value.SetActive(visible);
            }
        }

        private void SelectEnemy(string enemyName)
        {
            PlayConfirmSFX();
            selectedEnemyName = enemyName;
            UpdateConfigPanel();
        }

        private void UpdateConfigPanel()
        {
            try
            {
                // Check if enemyConfigPanel exists
                if (enemyConfigPanel == null)
                {
                    Plugin.Log.LogError("enemyConfigPanel is null in UpdateConfigPanel");
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
                    Plugin.Log.LogError("Failed to create controls panel");
                    return;
                }

                var controlsRectTransform = controlsPanel.GetComponent<RectTransform>();
                controlsRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                controlsRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                controlsRectTransform.pivot = new Vector2(0.5f, 0.5f);
                controlsRectTransform.anchoredPosition = new Vector2(0, -30);

                // Style the controls panel
                var panelImage = controlsPanel.GetComponent<Image>();
                if (panelImage != null)
                {
                    panelImage.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);
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
                        Plugin.Log.LogInfo($"Health for {config.Name} changed to {newValue}");
                    }
                );

                // Store reference to health input for enabling/disabling
                if (healthInput != null)
                {
                    healthInput.name = "HealthPanel"; // Keep the same name for consistency with other code
                }

                // Save button with LC styling
                var saveButton = UIHelper.CreateButton(enemyConfigPanel.transform, "SaveButton", "SAVE CHANGES", () => {
                    PlayConfirmSFX();
                    SaveCurrentEnemyConfig();
                });

                if (saveButton != null)
                {
                    var saveButtonRect = saveButton.GetComponent<RectTransform>();
                    saveButtonRect.anchorMin = new Vector2(0.5f, 0);
                    saveButtonRect.anchorMax = new Vector2(0.5f, 0);
                    saveButtonRect.pivot = new Vector2(0.5f, 0);
                    saveButtonRect.sizeDelta = new Vector2(150, 40);
                    saveButtonRect.anchoredPosition = new Vector2(0, 20);

                    // Style the save button to match LC
                    var saveButtonImage = saveButton.GetComponent<Image>();
                    if (saveButtonImage != null)
                    {
                        saveButtonImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
                    }
                }

                // Update selector enablements based on current settings
                UpdateSelectorEnablements(controlsPanel, config.IsEnabled);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in UpdateConfigPanel: {ex.Message}\nStack trace: {ex.StackTrace}");
            }
        }

        private void UpdateSelectorEnablements(GameObject controlsPanel, bool isEnabled)
        {
            // Find all other controls and disable them if the main enabled selector is set to No
            var canDieSelector = controlsPanel.transform.Find("CanDieSelector")?.GetComponent<UIHelper.StateHolder>();
            var despawnSelector = controlsPanel.transform.Find("DespawnSelector")?.GetComponent<UIHelper.StateHolder>();
            var healthInput = controlsPanel.transform.Find("HealthPanel")?.GetComponent<UIHelper.NumericInputState>();

            if (canDieSelector != null) canDieSelector.SetInteractable(isEnabled);
            if (despawnSelector != null) despawnSelector.SetInteractable(isEnabled);
            if (healthInput != null) healthInput.SetInteractable(isEnabled);
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

            // Show brief confirmation
            StartCoroutine(ShowSavedConfirmation());
        }

        private IEnumerator ShowSavedConfirmation()
        {
            // Create a temporary confirmation text with LC styling
            var confirmObj = UIHelper.CreateText(enemyConfigPanel.transform, "SavedConfirmation", "CONFIGURATION SAVED");
            var confirmRect = confirmObj.GetComponent<RectTransform>();
            confirmRect.anchorMin = new Vector2(0.5f, 0);
            confirmRect.anchorMax = new Vector2(0.5f, 0);
            confirmRect.pivot = new Vector2(0.5f, 0);
            confirmRect.sizeDelta = new Vector2(200, 30);
            confirmRect.anchoredPosition = new Vector2(0, 70);

            var confirmText = confirmObj.GetComponent<TextMeshProUGUI>();
            confirmText.fontSize = UITheme.NormalFontSize;
            confirmText.color = UITheme.PositiveColor;
            confirmText.fontStyle = FontStyles.Bold;

            // Wait for 2 seconds
            yield return new WaitForSeconds(2f);

            // Fade out
            float startTime = Time.time;
            float duration = 0.5f;

            while (Time.time - startTime < duration)
            {
                float t = (Time.time - startTime) / duration;
                confirmText.color = new Color(confirmText.color.r, confirmText.color.g, confirmText.color.b, 1 - t);
                yield return null;
            }

            // Remove the confirmation text
            Destroy(confirmObj);
        }
    }
}
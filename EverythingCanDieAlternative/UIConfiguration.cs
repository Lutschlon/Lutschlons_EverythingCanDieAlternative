using BepInEx.Configuration;
using System;

namespace EverythingCanDieAlternative.UI
{
    /// <summary>
    /// Configuration class for UI settings
    /// </summary>
    public class UIConfiguration
    {
        // Static config entries that LethalConfig will automatically detect
        public static ConfigEntry<bool> EnableConfigMenu;
        public static ConfigEntry<bool> EnableInfoLogs;
        public static ConfigEntry<bool> ShowEnemyImages;

        // Private instance
        private static UIConfiguration _instance;
        public static UIConfiguration Instance => _instance ??= new UIConfiguration();

        // Flag to track if configuration is fully initialized
        public bool IsInitialized { get; private set; }

        private UIConfiguration()
        {
            try
            {
                // Initialize config entries in the main plugin config
                EnableConfigMenu = Plugin.Instance.Config.Bind(
                    "General",
                    "EnableConfigMenu",
                    true,
                    "If set to true, the config menu button will be shown in the main menu"
                );

                EnableInfoLogs = Plugin.Instance.Config.Bind(
                    "General",
                    "EnableInfoLogs",
                    true,
                    "If set to false, info logs will be suppressed to reduce console spam"
                );

                ShowEnemyImages = Plugin.Instance.Config.Bind(
                    "General",
                    "ShowEnemyImages",
                    false,
                    "If set to true, preview images for enemies will be shown in the config menu if available"
                );

                //Plugin.Log.LogInfo("UI configuration loaded from plugin config");

                // Mark as initialized
                IsInitialized = true;
            }
            catch (Exception ex)
            {
                // Use raw logger for errors
                Plugin.Log.LogError($"Error initializing UI configuration: {ex.Message}");
                IsInitialized = false;
            }
        }

        /// <summary>
        /// Check if config menu should be enabled
        /// </summary>
        public bool IsConfigMenuEnabled()
        {
            // In case of initialization error, default to true
            if (!IsInitialized || EnableConfigMenu == null) return true;
            return EnableConfigMenu.Value;
        }

        /// <summary>
        /// Check if info logs should be shown
        /// </summary>
        public bool ShouldLogInfo()
        {
            // In case of initialization error, default to true
            if (!IsInitialized || EnableInfoLogs == null) return true;
            return EnableInfoLogs.Value;
        }

        /// <summary>
        /// Check if enemy images should be shown
        /// </summary>
        public bool ShouldShowEnemyImages()
        {
            // In case of initialization error, default to false
            if (!IsInitialized || ShowEnemyImages == null) return false;
            return ShowEnemyImages.Value;
        }

        /// <summary>
        /// Set whether info logs should be shown
        /// </summary>
        public void SetInfoLogsEnabled(bool enabled)
        {
            if (IsInitialized && EnableInfoLogs != null)
            {
                EnableInfoLogs.Value = enabled;
                Plugin.Log.LogInfo($"Info logs {(enabled ? "enabled" : "disabled")}");
            }
        }

        /// <summary>
        /// Set whether the config menu should be enabled
        /// </summary>
        public void SetConfigMenuEnabled(bool enabled)
        {
            if (IsInitialized && EnableConfigMenu != null)
            {
                EnableConfigMenu.Value = enabled;
                Plugin.Log.LogInfo($"Config menu {(enabled ? "enabled" : "disabled")}");
            }
        }

        /// <summary>
        /// Set whether enemy images should be shown
        /// </summary>
        public void SetShowEnemyImages(bool enabled)
        {
            if (IsInitialized && ShowEnemyImages != null)
            {
                ShowEnemyImages.Value = enabled;
                Plugin.Log.LogInfo($"Enemy images {(enabled ? "enabled" : "disabled")}");
            }
        }

        /// <summary>
        /// Save any pending changes to the config file
        /// </summary>
        public void Save()
        {
            try
            {
                // Force a save of the config file
                Plugin.Instance.Config.Save();

                // Reload ALL config files
                Plugin.Instance.Config.Reload();
                EnemyControlConfiguration.Instance.ReloadConfig();
                DespawnConfiguration.Instance.ReloadConfig();

                Plugin.LogInfo("All UI settings saved and reloaded");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error saving UI settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Reload all config values from files
        /// </summary>
        public void ReloadAllConfigs()
        {
            try
            {
                // Force a reload of all config files
                Plugin.Instance.Config.Reload();
                EnemyControlConfiguration.Instance.ReloadConfig();
                DespawnConfiguration.Instance.ReloadConfig();

                Plugin.LogInfo("All configs reloaded from files");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error reloading configs: {ex.Message}");
            }
        }
    }
}
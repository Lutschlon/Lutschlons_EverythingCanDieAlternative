using BepInEx.Configuration;
using System;

namespace EverythingCanDieAlternative.UI
{
    // Configuration class for UI settings
    public class UIConfiguration
    {
        public enum HealthBarDisplayMode
        {
            Off,
            NumberOnly,
            BarOnly,
            Both
        }

        public enum HealthBarSize
        {
            Small,
            Medium,
            Large
        }

        public enum HealthBarVisibilityDistance
        {
            Close,
            Medium,
            Far
        }

        // Static config entries that LethalConfig will automatically detect
        public static ConfigEntry<bool> EnableConfigMenu;
        public static ConfigEntry<bool> EnableInfoLogs;
        public static ConfigEntry<bool> ShowEnemyImages;
        public static ConfigEntry<HealthBarDisplayMode> HealthBarMode;
        public static ConfigEntry<HealthBarSize> HealthBarSizeOption;
        public static ConfigEntry<bool> HideHealthBarForFullHpEnemies;
        public static ConfigEntry<HealthBarVisibilityDistance> HealthBarRange;

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

                HealthBarMode = Plugin.Instance.Config.Bind(
                    "General",
                    "EnemyHealthBar",
                    HealthBarDisplayMode.Off,
                    "Show enemy health above damageable enemies. Off / NumberOnly / BarOnly / Both"
                );

                HealthBarSizeOption = Plugin.Instance.Config.Bind(
                    "General",
                    "EnemyHealthBarSize",
                    HealthBarSize.Medium,
                    "Size of the floating enemy health bar and number. Small / Medium / Large"
                );

                HideHealthBarForFullHpEnemies = Plugin.Instance.Config.Bind(
                    "General",
                    "HideHealthBarForFullHpEnemies",
                    true,
                    "If true, the floating health bar is only shown after the enemy has taken damage. Prevents giving away hiding enemies."
                );

                HealthBarRange = Plugin.Instance.Config.Bind(
                    "General",
                    "EnemyHealthBarRange",
                    HealthBarVisibilityDistance.Close,
                    "Maximum distance at which the floating health bar is visible. Close / Medium / Far"
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

        // Check if config menu should be enabled
        public bool IsConfigMenuEnabled()
        {
            // In case of initialization error, default to true
            if (!IsInitialized || EnableConfigMenu == null) return true;
            return EnableConfigMenu.Value;
        }

        // Check if info logs should be shown
        public bool ShouldLogInfo()
        {
            // In case of initialization error, default to true
            if (!IsInitialized || EnableInfoLogs == null) return true;
            return EnableInfoLogs.Value;
        }

        // Check if enemy images should be shown
        public bool ShouldShowEnemyImages()
        {
            // In case of initialization error, default to false
            if (!IsInitialized || ShowEnemyImages == null) return false;
            return ShowEnemyImages.Value;
        }

        // Set whether info logs should be shown
        public void SetInfoLogsEnabled(bool enabled)
        {
            if (IsInitialized && EnableInfoLogs != null)
            {
                EnableInfoLogs.Value = enabled;
                Plugin.Log.LogInfo($"Info logs {(enabled ? "enabled" : "disabled")}");
                
                // Refresh the cached logging state in Plugin immediately
                Plugin.RefreshLoggingState();
            }
        }

        // Set whether the config menu should be enabled
        public void SetConfigMenuEnabled(bool enabled)
        {
            if (IsInitialized && EnableConfigMenu != null)
            {
                EnableConfigMenu.Value = enabled;
                Plugin.Log.LogInfo($"Config menu {(enabled ? "enabled" : "disabled")}");
            }
        }

        // Set whether enemy images should be shown
        public void SetShowEnemyImages(bool enabled)
        {
            if (IsInitialized && ShowEnemyImages != null)
            {
                ShowEnemyImages.Value = enabled;
                Plugin.Log.LogInfo($"Enemy images {(enabled ? "enabled" : "disabled")}");
            }
        }

        public HealthBarDisplayMode GetHealthBarMode()
        {
            if (!IsInitialized || HealthBarMode == null) return HealthBarDisplayMode.Off;
            return HealthBarMode.Value;
        }

        public void SetHealthBarMode(HealthBarDisplayMode mode)
        {
            if (IsInitialized && HealthBarMode != null)
            {
                HealthBarMode.Value = mode;
                Plugin.Log.LogInfo($"Enemy health bar mode set to {mode}");
            }
        }

        public HealthBarSize GetHealthBarSize()
        {
            if (!IsInitialized || HealthBarSizeOption == null) return HealthBarSize.Medium;
            return HealthBarSizeOption.Value;
        }

        public void SetHealthBarSize(HealthBarSize size)
        {
            if (IsInitialized && HealthBarSizeOption != null)
            {
                HealthBarSizeOption.Value = size;
                Plugin.Log.LogInfo($"Enemy health bar size set to {size}");
            }
        }

        public bool ShouldHideHealthBarForFullHp()
        {
            if (!IsInitialized || HideHealthBarForFullHpEnemies == null) return true;
            return HideHealthBarForFullHpEnemies.Value;
        }

        public void SetHideHealthBarForFullHp(bool hide)
        {
            if (IsInitialized && HideHealthBarForFullHpEnemies != null)
            {
                HideHealthBarForFullHpEnemies.Value = hide;
                Plugin.Log.LogInfo($"Hide health bar for full HP enemies {(hide ? "enabled" : "disabled")}");
            }
        }

        public HealthBarVisibilityDistance GetHealthBarRange()
        {
            if (!IsInitialized || HealthBarRange == null) return HealthBarVisibilityDistance.Close;
            return HealthBarRange.Value;
        }

        public void SetHealthBarRange(HealthBarVisibilityDistance range)
        {
            if (IsInitialized && HealthBarRange != null)
            {
                HealthBarRange.Value = range;
                Plugin.Log.LogInfo($"Enemy health bar range set to {range}");
            }
        }

        // Save any pending changes to the config file
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

        // Reload all config values from files
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
using BepInEx;
using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
namespace EverythingCanDieAlternative
{
    public class EnemyControlConfiguration
    {
        private static EnemyControlConfiguration _instance;
        public static EnemyControlConfiguration Instance => _instance ??= new EnemyControlConfiguration();
        private ConfigFile _configFile;
        // Dictionary to cache per-enemy mod enabled settings
        private readonly Dictionary<string, bool> _enemyModEnabled = new Dictionary<string, bool>();
        // Flag to track if we've already created the entries
        private bool _configEntriesCreated = false;

        private EnemyControlConfiguration()
        {
            string configPath = Path.Combine(Paths.ConfigPath, "nwnt.EverythingCanDieAlternative_Enemy_Control.cfg");
            _configFile = new ConfigFile(configPath, true);

            // Add a comment entry for the Enemies section
            _configFile.Bind("Enemies",
                "_INFO",
                "Control which enemies are affected by this mod",
                new ConfigDescription("This is an Experimental Feature: If you set an enemy's value to 'false', it will not be affected by the EverythingCanDieAlternative mod. The health and hit synconization, and the despawn feature will not take effect for the configured enemy. " +
                "This can be useful if specific enemies have built-in hit/health/death mechanisms that you want to preserve, but that ECDA overwrites. Now you can preserve them by setting the enemy to false."));

            //Plugin.Log.LogInfo($"Enemy control configuration loaded");
        }

        // Reload configuration from disk and clear cache
        public void ReloadConfig()
        {
            try
            {
                // Reload the config file from disk
                _configFile.Reload();

                // Clear cached values to force re-reading from config
                _enemyModEnabled.Clear();

                Plugin.LogInfo("Enemy control configuration reloaded from disk");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error reloading enemy control config: {ex.Message}");
            }
        }

        // Pre-create configuration entries for all known enemies
        public void PreCreateEnemyConfigEntries()
        {
            // Only create entries once
            if (_configEntriesCreated)
            {
                return;
            }

            if (Plugin.enemies != null && Plugin.enemies.Count > 0)
            {
                Plugin.LogInfo($"Creating control entries for {Plugin.enemies.Count} enemy types");

                foreach (var enemyType in Plugin.enemies)
                {
                    if (enemyType == null || string.IsNullOrEmpty(enemyType.enemyName)) continue;
                    string sanitizedName = Plugin.RemoveInvalidCharacters(enemyType.enemyName).ToUpper();

                    // Create config with proper ConfigDescription to ensure correct comment format
                    _configFile.Bind("Enemies",
                        $"{sanitizedName}.Enabled",
                        true,
                        new ConfigDescription($"If set to false, {enemyType.enemyName} will not be affected by this mod"));
                }

                // Mark as completed
                _configEntriesCreated = true;
            }
        }

        // Check if the mod should be enabled for a specific enemy
        public bool IsModEnabledForEnemy(string enemyName)
        {
            string sanitizedName = Plugin.RemoveInvalidCharacters(enemyName).ToUpper();

            // Check if we already have a cached value
            if (_enemyModEnabled.TryGetValue(sanitizedName, out bool enabled))
                return enabled;

            try
            {
                // Load from config with proper ConfigDescription
                var configEntry = _configFile.Bind("Enemies",
                    $"{sanitizedName}.Enabled",
                    true,
                    new ConfigDescription($"If set to false, {enemyName} will not be affected by this mod"));

                // Cache the result
                _enemyModEnabled[sanitizedName] = configEntry.Value;
                return configEntry.Value;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error checking if mod is enabled for {enemyName}: {ex.Message} - defaulting to true");
                return true; // Default to true in case of error
            }
        }

        // Clear cache to force re-reading values from config
        public void ClearCache()
        {
            _enemyModEnabled.Clear();
            Plugin.LogInfo("Enemy control cache cleared");
        }
    }
}
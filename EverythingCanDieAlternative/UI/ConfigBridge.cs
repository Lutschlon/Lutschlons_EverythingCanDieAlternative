using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using BepInEx;
using static EverythingCanDieAlternative.Plugin;

namespace EverythingCanDieAlternative.UI
{
    // Bridge between UI and configuration system - reads files directly
    public static class ConfigBridge
    {
        // Pre-compiled regex for better performance
        private static readonly Regex sectionRegex = new Regex(@"^\[(.*)\]$", RegexOptions.Compiled);

        // Cache the config file paths
        private static string pluginConfigPath = Path.Combine(Paths.ConfigPath, "nwnt.EverythingCanDieAlternative.cfg");
        private static string enemyControlConfigPath = Path.Combine(Paths.ConfigPath, "nwnt.EverythingCanDieAlternative_Enemy_Control.cfg");
        private static string despawnConfigPath = Path.Combine(Paths.ConfigPath, "nwnt.EverythingCanDieAlternative_Despawn_Rules.cfg");

        // Cache of existing config entries
        private static Dictionary<string, Dictionary<string, object>> cachedConfigEntries = new Dictionary<string, Dictionary<string, object>>();

        // Flag to prevent double loading
        private static bool isLoading = false;

        // Load all enemy configurations from existing config files only

        public static List<EnemyConfigData> LoadAllEnemyConfigs()
        {
            // Prevent double loading
            if (isLoading) return new List<EnemyConfigData>();
            isLoading = true;

            var result = new List<EnemyConfigData>();

            try
            {
                // Clear the cache to ensure fresh reads
                cachedConfigEntries.Clear();

                // Check if files exist and log their details
                LogFileDetails(pluginConfigPath, "Main plugin config");
                LogFileDetails(enemyControlConfigPath, "Enemy control config");
                LogFileDetails(despawnConfigPath, "Despawn config");

                // Read the config files directly as text
                ReadConfigFileDirectly(pluginConfigPath, "Mobs");
                ReadConfigFileDirectly(enemyControlConfigPath, "Enemies");
                ReadConfigFileDirectly(despawnConfigPath, "Enemies");

                // Get all enemies that have any configuration entries
                var configuredEnemies = GetAllConfiguredEnemyNames();

                if (configuredEnemies.Count == 0)
                {
                    Plugin.Log.LogWarning("No configured enemies found in any config files. Start a round to generate configurations.");
                    isLoading = false;
                    return result;
                }

                Plugin.LogInfo($"Found {configuredEnemies.Count} configured enemies in config files");

                // Process each configured enemy
                foreach (var enemyName in configuredEnemies)
                {
                    // Direct lookup in cachedConfigEntries instead of using helper methods
                    bool isEnabled = false;
                    bool canDie = true;
                    bool shouldDespawn = true;
                    float health = 3f; // Default fallback

                    if (cachedConfigEntries.TryGetValue(enemyName, out var entries))
                    {
                        // Get Enabled status
                        if (entries.TryGetValue(".ENABLED", out var enabledValue) && enabledValue is bool enabledBool)
                        {
                            isEnabled = enabledBool;
                        }

                        // Get Can Die status
                        if (entries.TryGetValue(".UNIMMORTAL", out var unimmortalValue) && unimmortalValue is bool unimmortalBool)
                        {
                            canDie = unimmortalBool;
                        }

                        // Get Despawn status
                        if (entries.TryGetValue(".DESPAWN", out var despawnValue) && despawnValue is bool despawnBool)
                        {
                            shouldDespawn = despawnBool;
                        }

                        // Get Health value
                        if (entries.TryGetValue(".HEALTH", out var healthValue))
                        {
                            if (healthValue is float healthFloat)
                            {
                                health = healthFloat;
                            }
                            else if (healthValue is int healthInt)
                            {
                                health = healthInt;
                            }
                            else if (healthValue is string healthStr && float.TryParse(healthStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsedHealth))
                            {
                                health = parsedHealth;
                            }
                        }
                    }

                    // Create the config data object with the correct values
                    result.Add(new EnemyConfigData(enemyName, isEnabled, canDie, shouldDespawn, health));
                }

                Plugin.LogInfo($"Loaded {result.Count} enemy configurations successfully");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error loading enemy configurations: {ex}");
            }
            finally
            {
                isLoading = false;
            }

            return result;
        }

        // Log file details
        private static void LogFileDetails(string path, string description)
        {
            bool exists = File.Exists(path);

            if (exists)
            {
                var fileInfo = new FileInfo(path);
            }
        }

        // Read a config file directly
        private static void ReadConfigFileDirectly(string path, string targetSection)
        {
            if (!File.Exists(path)) return;

            try
            {
                string[] lines = File.ReadAllLines(path);
                Plugin.LogInfo($"Reading config file {Path.GetFileName(path)} ({lines.Length} lines)");

                string currentSection = "";
                int entriesFound = 0;

                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();

                    // Skip empty lines and comments
                    if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#") || trimmedLine.StartsWith("//"))
                        continue;

                    // Check if this is a section header [SectionName]
                    Match sectionMatch = sectionRegex.Match(trimmedLine);
                    if (sectionMatch.Success)
                    {
                        currentSection = sectionMatch.Groups[1].Value;
                        continue;
                    }

                    // Only process entries in the target section
                    if (currentSection != targetSection)
                        continue;

                    // Parse key-value pairs (Key = Value)
                    int equalsPos = trimmedLine.IndexOf('=');
                    if (equalsPos > 0)
                    {
                        string key = trimmedLine.Substring(0, equalsPos).Trim();
                        string value = trimmedLine.Substring(equalsPos + 1).Trim();

                        // Process the entry
                        ProcessConfigEntry(currentSection, key, value);
                        entriesFound++;
                    }
                }

                Plugin.LogInfo($"Found {entriesFound} entries in section [{targetSection}]");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error reading config file {path}: {ex.Message}");
            }
        }

        // Process a config entry
        private static void ProcessConfigEntry(string section, string key, string value)
        {
            // Try to extract enemy name and setting
            string enemyName = null;
            string setting = null;

            // Special handling for each section
            if (section == "Mobs")
            {
                // In Mobs section, keys are like "ENEMYNAME.HEALTH" or "ENEMYNAME.UNIMMORTAL"
                int dotPos = key.LastIndexOf('.');
                if (dotPos > 0)
                {
                    enemyName = key.Substring(0, dotPos).ToUpper();
                    setting = key.Substring(dotPos).ToUpper();
                }
            }
            else if (section == "Enemies")
            {
                // In Enemies section, keys are like "ENEMYNAME.ENABLED" or "ENEMYNAME.DESPAWN"
                int dotPos = key.LastIndexOf('.');
                if (dotPos > 0)
                {
                    enemyName = key.Substring(0, dotPos).ToUpper();
                    setting = key.Substring(dotPos).ToUpper();
                }
            }

            // If we successfully parsed the enemy name and setting
            if (!string.IsNullOrEmpty(enemyName) && !string.IsNullOrEmpty(setting))
            {
                // Add to cache
                if (!cachedConfigEntries.ContainsKey(enemyName))
                {
                    cachedConfigEntries[enemyName] = new Dictionary<string, object>();
                }

                // Convert value based on setting type
                if (setting == ".ENABLED" || setting == ".UNIMMORTAL" || setting == ".DESPAWN")
                {
                    // Handle boolean values
                    bool boolValue;
                    if (bool.TryParse(value, out boolValue))
                    {
                        cachedConfigEntries[enemyName][setting] = boolValue;
                    }
                }
                else if (setting == ".HEALTH")
                {
                    // Handle float values using invariant culture
                    float floatValue;
                    if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out floatValue))
                    {
                        cachedConfigEntries[enemyName][setting] = floatValue;
                    }
                    else
                    {
                        // Store as string if we can't parse as float
                        cachedConfigEntries[enemyName][setting] = value;
                        Plugin.Log.LogWarning($"Could not parse health value {value} for {enemyName}");
                    }
                }
            }
        }

        // Get all enemy names that have configuration entries
        private static List<string> GetAllConfiguredEnemyNames()
        {
            return cachedConfigEntries.Keys.ToList();
        }

        // Get cached bool value
        private static bool GetCachedBoolValue(string enemyName, string suffix, bool defaultValue)
        {
            if (cachedConfigEntries.TryGetValue(enemyName, out var entries))
            {
                if (entries.TryGetValue(suffix, out var value) && value is bool boolValue)
                {
                    return boolValue;
                }
            }
            return defaultValue;
        }

        // Get cached int value
        private static float GetCachedFloatValue(string enemyName, string suffix, float defaultValue)
        {
            if (cachedConfigEntries.TryGetValue(enemyName, out var entries))
            {
                if (entries.TryGetValue(suffix, out var value))
                {
                    // Handle both int and string values
                    if (value is float floatValue)
                    {
                        return floatValue;
                    }
                    else if (value is int intValue)
                    {
                        return intValue;
                    }
                    else if (value is string stringValue && float.TryParse(stringValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsedValue))
                    {
                        return parsedValue;
                    }
                    else
                    {
                        Plugin.Log.LogWarning($"Invalid health value for {enemyName}: {value} (type: {value?.GetType().Name ?? "null"})");
                    }
                }
            }

            return defaultValue;
        }

        // Save a single enemy configuration by updating existing entries
        public static void SaveEnemyConfig(EnemyConfigData config)
        {
            if (config == null) return;

            string sanitizedName = config.SanitizedName;
            string enemyName = config.Name;

            // Get existing entries from cache
            if (!cachedConfigEntries.ContainsKey(sanitizedName))
            {
                Plugin.Log.LogWarning($"Cannot save config for {enemyName} as no existing entries were found");
                return;
            }

            try
            {
                // Update files directly
                bool mainFileUpdated = false;
                bool enemyControlFileUpdated = false;
                bool despawnFileUpdated = false;

                // Update main plugin config file - BUT ONLY FOR UNIMMORTAL AND HEALTH, NOT ENABLED
                if (cachedConfigEntries[sanitizedName].ContainsKey(".UNIMMORTAL") ||
                    cachedConfigEntries[sanitizedName].ContainsKey(".HEALTH"))
                {
                    // Create a dictionary without the Enabled setting
                    Dictionary<string, string> mainConfigUpdates = new Dictionary<string, string>();

                    // Only include Unimmortal and Health settings for main config
                    if (cachedConfigEntries[sanitizedName].ContainsKey(".UNIMMORTAL"))
                    {
                        mainConfigUpdates[$"{sanitizedName}.Unimmortal"] = config.CanDie.ToString().ToLower();
                    }

                    if (cachedConfigEntries[sanitizedName].ContainsKey(".HEALTH"))
                    {
                        mainConfigUpdates[$"{sanitizedName}.Health"] = config.Health.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    }

                    // Update main config file without Enabled setting
                    mainFileUpdated = UpdateConfigFileDirectly(
                        pluginConfigPath, "Mobs", mainConfigUpdates
                    );
                }

                // Update enemy control config file
                if (cachedConfigEntries[sanitizedName].ContainsKey(".ENABLED"))
                {
                    enemyControlFileUpdated = UpdateConfigFileDirectly(
                        enemyControlConfigPath, "Enemies",
                        new Dictionary<string, string> {
                    { $"{sanitizedName}.Enabled", config.IsEnabled.ToString().ToLower() }
                        }
                    );
                }

                // Update despawn config file
                if (cachedConfigEntries[sanitizedName].ContainsKey(".DESPAWN"))
                {
                    despawnFileUpdated = UpdateConfigFileDirectly(
                        despawnConfigPath, "Enemies",
                        new Dictionary<string, string> {
                    { $"{sanitizedName}.Despawn", config.ShouldDespawn.ToString().ToLower() }
                        }
                    );
                }

                // Update cache
                cachedConfigEntries[sanitizedName][".ENABLED"] = config.IsEnabled;
                cachedConfigEntries[sanitizedName][".UNIMMORTAL"] = config.CanDie;
                cachedConfigEntries[sanitizedName][".HEALTH"] = config.Health;
                cachedConfigEntries[sanitizedName][".DESPAWN"] = config.ShouldDespawn;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error saving configuration: {ex.Message}");
            }
        }

        // Update a config file directly
        private static bool UpdateConfigFileDirectly(string path, string section, Dictionary<string, string> updates)
        {
            if (!File.Exists(path)) return false;

            try
            {
                string[] lines = File.ReadAllLines(path);
                List<string> newLines = new List<string>();

                string currentSection = "";
                bool updatedAny = false;
                Dictionary<string, bool> updatedKeys = new Dictionary<string, bool>();

                // Initialize all keys as not updated
                foreach (var key in updates.Keys)
                {
                    updatedKeys[key.ToUpper()] = false;
                }

                // Process each line
                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();

                    // Check if this is a section header
                    Match sectionMatch = sectionRegex.Match(trimmedLine);
                    if (sectionMatch.Success)
                    {
                        currentSection = sectionMatch.Groups[1].Value;
                        newLines.Add(line);
                        continue;
                    }

                    // If we're in the target section, look for keys to update
                    if (currentSection == section)
                    {
                        bool lineUpdated = false;

                        // Parse key-value pairs
                        int equalsPos = trimmedLine.IndexOf('=');
                        if (equalsPos > 0)
                        {
                            string key = trimmedLine.Substring(0, equalsPos).Trim();

                            // Check if this key needs to be updated
                            foreach (var updateKey in updates.Keys)
                            {
                                // Compare keys case-insensitively
                                if (key.Equals(updateKey, StringComparison.OrdinalIgnoreCase) ||
                                    Plugin.RemoveInvalidCharacters(key.ToUpper()) == Plugin.RemoveInvalidCharacters(updateKey.ToUpper()))
                                {
                                    newLines.Add($"{key} = {updates[updateKey]}");
                                    updatedKeys[updateKey.ToUpper()] = true;
                                    updatedAny = true;
                                    lineUpdated = true;
                                    break;
                                }
                            }
                        }

                        if (!lineUpdated)
                        {
                            newLines.Add(line);
                        }
                    }
                    else
                    {
                        newLines.Add(line);
                    }
                }

                // Check if any keys weren't updated (not found in file)
                bool needToAddKeys = false;
                foreach (var entry in updatedKeys)
                {
                    if (!entry.Value)
                    {
                        needToAddKeys = true;
                        break;
                    }
                }

                // If we need to add new keys, add the section if it doesn't exist
                if (needToAddKeys)
                {
                    bool foundSection = false;
                    for (int i = 0; i < newLines.Count; i++)
                    {
                        if (newLines[i].Trim() == $"[{section}]")
                        {
                            foundSection = true;
                            break;
                        }
                    }

                    if (!foundSection)
                    {
                        // Add a blank line if the file isn't empty
                        if (newLines.Count > 0 && !string.IsNullOrWhiteSpace(newLines[newLines.Count - 1]))
                        {
                            newLines.Add("");
                        }

                        // Add the section
                        newLines.Add($"[{section}]");
                    }

                    // Add any keys that weren't updated
                    foreach (var entry in updatedKeys)
                    {
                        if (!entry.Value)
                        {
                            string originalKey = updates.Keys.First(k => k.ToUpper() == entry.Key);
                            string value = updates[originalKey];
                            newLines.Add($"{originalKey} = {value}");
                            updatedAny = true;
                        }
                    }
                }

                // Only write the file if we made changes
                if (updatedAny)
                {
                    File.WriteAllLines(path, newLines);
                }

                return updatedAny;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error updating config file {path}: {ex.Message}");
                return false;
            }
        }
    }
}
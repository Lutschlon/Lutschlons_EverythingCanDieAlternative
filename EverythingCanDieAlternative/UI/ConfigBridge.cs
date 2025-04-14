using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using BepInEx;

namespace EverythingCanDieAlternative.UI
{
    /// <summary>
    /// Bridge between UI and configuration system - reads files directly
    /// </summary>
    public static class ConfigBridge
    {
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

                Plugin.Log.LogInfo($"Found {configuredEnemies.Count} configured enemies in config files");

                // Process each configured enemy
                foreach (var enemyName in configuredEnemies)
                {
                    // Log which entries we found for this enemy
                    LogEnemyEntries(enemyName);

                    // FIXED: Direct lookup in cachedConfigEntries instead of using helper methods
                    bool isEnabled = false;
                    bool canDie = true;
                    bool shouldDespawn = true;
                    int health = 3; // Default fallback

                    if (cachedConfigEntries.TryGetValue(enemyName, out var entries))
                    {
                        // Get Enabled status
                        if (entries.TryGetValue(".ENABLED", out var enabledValue) && enabledValue is bool enabledBool)
                        {
                            isEnabled = enabledBool;
                            Plugin.Log.LogInfo($"  Using enabled={isEnabled} for {enemyName}");
                        }

                        // Get Can Die status
                        if (entries.TryGetValue(".UNIMMORTAL", out var unimmortalValue) && unimmortalValue is bool unimmortalBool)
                        {
                            canDie = unimmortalBool;
                            Plugin.Log.LogInfo($"  Using canDie={canDie} for {enemyName}");
                        }

                        // Get Despawn status
                        if (entries.TryGetValue(".DESPAWN", out var despawnValue) && despawnValue is bool despawnBool)
                        {
                            shouldDespawn = despawnBool;
                            Plugin.Log.LogInfo($"  Using shouldDespawn={shouldDespawn} for {enemyName}");
                        }

                        // Get Health value
                        if (entries.TryGetValue(".HEALTH", out var healthValue))
                        {
                            if (healthValue is int healthInt)
                            {
                                health = healthInt;
                                Plugin.Log.LogInfo($"  Using health={health} for {enemyName}");
                            }
                            else if (healthValue is string healthStr && int.TryParse(healthStr, out int parsedHealth))
                            {
                                health = parsedHealth;
                                Plugin.Log.LogInfo($"  Using parsed health={health} for {enemyName}");
                            }
                        }
                    }

                    // Create the config data object with the correct values
                    result.Add(new EnemyConfigData(enemyName, isEnabled, canDie, shouldDespawn, health));
                }

                Plugin.Log.LogInfo($"Loaded {result.Count} enemy configurations successfully");
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
            Plugin.Log.LogInfo($"{description} file {(exists ? "exists" : "DOES NOT EXIST")}: {path}");

            if (exists)
            {
                var fileInfo = new FileInfo(path);
                Plugin.Log.LogInfo($"  Size: {fileInfo.Length} bytes, Last modified: {fileInfo.LastWriteTime}");

                // Log the first few lines to see the format
                try
                {
                    string[] lines = File.ReadAllLines(path);
                    Plugin.Log.LogInfo($"  First {Math.Min(5, lines.Length)} lines of {lines.Length} total:");
                    for (int i = 0; i < Math.Min(5, lines.Length); i++)
                    {
                        Plugin.Log.LogInfo($"    {i + 1}: {lines[i]}");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"  Error reading file: {ex.Message}");
                }
            }
        }

        // Read a config file directly
        private static void ReadConfigFileDirectly(string path, string targetSection)
        {
            if (!File.Exists(path)) return;

            try
            {
                string[] lines = File.ReadAllLines(path);
                Plugin.Log.LogInfo($"Reading config file {Path.GetFileName(path)} ({lines.Length} lines)");

                string currentSection = "";
                int entriesFound = 0;

                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();

                    // Skip empty lines and comments
                    if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#") || trimmedLine.StartsWith("//"))
                        continue;

                    // Check if this is a section header [SectionName]
                    Match sectionMatch = Regex.Match(trimmedLine, @"^\[(.*)\]$");
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

                Plugin.Log.LogInfo($"Found {entriesFound} entries in section [{targetSection}]");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error reading config file {path}: {ex.Message}");
            }
        }

        // Process a config entry
        private static void ProcessConfigEntry(string section, string key, string value)
        {
            Plugin.Log.LogInfo($"  Found entry: [{section}] {key} = {value}");

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

                    Plugin.Log.LogInfo($"    Parsed as enemy: {enemyName}, setting: {setting}");
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

                    Plugin.Log.LogInfo($"    Parsed as enemy: {enemyName}, setting: {setting}");
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
                    // Handle integer values
                    int intValue;
                    if (int.TryParse(value, out intValue))
                    {
                        cachedConfigEntries[enemyName][setting] = intValue;
                        Plugin.Log.LogInfo($"    Parsed health value {value} to {intValue} for {enemyName}");
                    }
                    else
                    {
                        // Store as string if we can't parse as int
                        cachedConfigEntries[enemyName][setting] = value;
                        Plugin.Log.LogWarning($"    Could not parse health value {value} for {enemyName}");
                    }
                }
            }
        }

        // Log entries found for a specific enemy
        private static void LogEnemyEntries(string enemyName)
        {
            if (cachedConfigEntries.TryGetValue(enemyName, out var entries))
            {
                Plugin.Log.LogInfo($"Enemy {enemyName} has these config entries:");
                foreach (var entry in entries)
                {
                    Plugin.Log.LogInfo($"  - {entry.Key} = {entry.Value}");
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
        private static int GetCachedIntValue(string enemyName, string suffix, int defaultValue)
        {
            if (cachedConfigEntries.TryGetValue(enemyName, out var entries))
            {
                if (entries.TryGetValue(suffix, out var value))
                {
                    // Handle both int and string values
                    if (value is int intValue)
                    {
                        Plugin.Log.LogInfo($"Found integer health for {enemyName}: {intValue}");
                        return intValue;
                    }
                    else if (value is string stringValue && int.TryParse(stringValue, out int parsedValue))
                    {
                        Plugin.Log.LogInfo($"Found string health for {enemyName}, parsed to: {parsedValue}");
                        return parsedValue;
                    }
                    else
                    {
                        Plugin.Log.LogWarning($"Invalid health value for {enemyName}: {value} (type: {value?.GetType().Name ?? "null"})");
                    }
                }
            }

            Plugin.Log.LogInfo($"Using default health for {enemyName}: {defaultValue}");
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

                // Update main plugin config file
                if (cachedConfigEntries[sanitizedName].ContainsKey(".ENABLED") ||
                    cachedConfigEntries[sanitizedName].ContainsKey(".UNIMMORTAL") ||
                    cachedConfigEntries[sanitizedName].ContainsKey(".HEALTH"))
                {
                    mainFileUpdated = UpdateConfigFileDirectly(
                        pluginConfigPath, "Mobs",
                        new Dictionary<string, string> {
                            { $"{sanitizedName}.Enabled", config.IsEnabled.ToString().ToLower() },
                            { $"{sanitizedName}.Unimmortal", config.CanDie.ToString().ToLower() },
                            { $"{sanitizedName}.Health", config.Health.ToString() }
                        }
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
                cachedConfigEntries[sanitizedName][".Enabled"] = config.IsEnabled;
                cachedConfigEntries[sanitizedName][".Unimmortal"] = config.CanDie;
                cachedConfigEntries[sanitizedName][".Health"] = config.Health;
                cachedConfigEntries[sanitizedName][".Despawn"] = config.ShouldDespawn;

                Plugin.Log.LogInfo($"Saved configuration for {config.Name}: Enabled={config.IsEnabled}, CanDie={config.CanDie}, ShouldDespawn={config.ShouldDespawn}, Health={config.Health}");
                Plugin.Log.LogInfo($"Files updated: Main={mainFileUpdated}, Control={enemyControlFileUpdated}, Despawn={despawnFileUpdated}");
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
                    Match sectionMatch = Regex.Match(trimmedLine, @"^\[(.*)\]$");
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
                                    Plugin.Log.LogInfo($"Updated [{section}] {key} to {updates[updateKey]} in {Path.GetFileName(path)}");
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
                            Plugin.Log.LogInfo($"Added [{section}] {originalKey} = {value} to {Path.GetFileName(path)}");
                        }
                    }
                }

                // Only write the file if we made changes
                if (updatedAny)
                {
                    File.WriteAllLines(path, newLines);
                    Plugin.Log.LogInfo($"Saved updated config file: {path}");
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
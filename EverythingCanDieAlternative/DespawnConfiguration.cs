using BepInEx;
using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EverythingCanDieAlternative
{
    public class DespawnConfiguration
    {
        private static DespawnConfiguration _instance;
        public static DespawnConfiguration Instance => _instance ??= new DespawnConfiguration();

        private ConfigFile _configFile;

        // Configuration values
        public ConfigEntry<bool> EnableDespawnFeature { get; private set; }

        // Dictionary to store per-enemy despawn settings
        private readonly Dictionary<string, bool> _enemyDespawnEnabled = new Dictionary<string, bool>();

        // List of enemies that should default to not despawning (have proper death animations)
        private readonly string[] _enemiesWithProperDeathAnimations = new string[]
        {
            "DOGDAY",
            "DOGDAYCRITTER",
            "PICKYCRITTER",
            "CATNAPCRITTER",
            "BOBBYCRITTER",
            "CRAFTYCRITTER",
            "HOPPYCRITTER",
            "BUBBACRITTER",
            "KICKINCRITTER",
            "BABOONHAWK",
            "MANEATER",
            "CENTIPEDE",
            "CRAWLER",
            "TULIPSNAKE",
            "FLOWERMAN",
            "FORESTGIANT",
            "HOARDINGBUG",
            "MASKED",
            "MOUTHDOG",
            "NUTCRACKER",
            "BUNKERSPIDER",
            "BUSHWOLF"
        };

        private DespawnConfiguration()
        {
            string configPath = Path.Combine(Paths.ConfigPath, "nwnt.EverythingCanDieAlternative_Despawn_Rules.cfg");
            _configFile = new ConfigFile(configPath, true);

            // Load global settings - using the ordered section name
            EnableDespawnFeature = _configFile.Bind("General",
                "EnableDespawnFeature",
                true,
                "If true, dead enemies can despawn based on other settings");

            Plugin.Log.LogInfo($"Despawn configuration loaded from {configPath}");
        }

        public bool ShouldDespawnEnemy(string enemyName)
        {
            if (!EnableDespawnFeature.Value)
                return false;

            string sanitizedName = Plugin.RemoveInvalidCharacters(enemyName).ToUpper();

            // Check if we already have a cached value
            if (_enemyDespawnEnabled.TryGetValue(sanitizedName, out bool enabled))
                return enabled;

            // Otherwise, load from config
            // Check if this enemy should default to not despawning
            bool defaultValue = true;
            foreach (var noAnimEnemy in _enemiesWithProperDeathAnimations)
            {
                if (sanitizedName == noAnimEnemy)
                {
                    defaultValue = false;
                    break;
                }
            }

            var configEntry = _configFile.Bind("Enemies",
                $"{sanitizedName}.Despawn",
                defaultValue,
                $"If true, {enemyName} will despawn after death");

            // Cache the result
            _enemyDespawnEnabled[sanitizedName] = configEntry.Value;

            return configEntry.Value;
        }
    }
}
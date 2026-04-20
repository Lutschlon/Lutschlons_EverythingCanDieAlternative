using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using EverythingCanDieAlternative.ModCompatibility;
using EverythingCanDieAlternative.ModCompatibility.Handlers;
using EverythingCanDieAlternative.UI;

namespace EverythingCanDieAlternative
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("LethalNetworkAPI")]
    [BepInDependency("Entity378.sellbodies", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("SlapitNow.LethalHands", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("SoftDiamond.BrutalCompanyMinusExtraReborn", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("ainavt.lc.lethalconfig", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.github.zehsteam.Hitmarker", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("LethalMin", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("HexiBetterShotgunFixed", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        public static ManualLogSource Log { get; private set; }
        public static Harmony Harmony { get; private set; }
        public static List<EnemyType> enemies = new List<EnemyType>();
        public static ConfigEntry<bool> PatchCruiserDamage { get; private set; }
        public static ConfigEntry<float> CruiserDamageAtHighSpeeds { get; private set; }

        // Trap configuration
        public static ConfigEntry<bool> AllowSpikeTrapsToKillEnemies { get; private set; }

        // Immortal enemy protection configuration
        public static ConfigEntry<bool> ProtectImmortalEnemiesFromInstaKill { get; private set; }
        public static ConfigEntry<bool> ProtectOldBirdsFromOwnRockets { get; private set; }
        // Mute enemy corpses configuration
        public static ConfigEntry<bool> MuteDeadEnemies { get; private set; }
        // Flag to indicate if logging should be conditionally suppressed
        private static bool _infoLogsEnabled = true;

        // Caches for configuration values to avoid repeated lookups
        private static readonly Dictionary<string, ConfigEntry<bool>> boolConfigCache = new Dictionary<string, ConfigEntry<bool>>();
        private static readonly Dictionary<string, ConfigEntry<float>> floatConfigCache = new Dictionary<string, ConfigEntry<float>>();

        // Cache for sanitized strings to avoid repeated processing
        private static readonly Dictionary<string, string> sanitizedNameCache = new Dictionary<string, string>();

        // Index of plugin config keys by sanitized+uppercase key — built lazily on first use,
        // invalidated when the config file is reloaded. Replaces O(N) scans of Instance.Config.Keys.
        private static Dictionary<string, ConfigDefinition> _configKeyIndex = null;

        // Drop the cached config-key index so the next CanMob/GetMobHealth call rebuilds it.
        // Call this whenever Instance.Config is reloaded.
        public static void InvalidateConfigKeyIndex()
        {
            _configKeyIndex = null;
        }

        private static Dictionary<string, ConfigDefinition> GetConfigKeyIndex()
        {
            if (_configKeyIndex != null) return _configKeyIndex;

            var index = new Dictionary<string, ConfigDefinition>();
            foreach (ConfigDefinition entry in Instance.Config.Keys)
            {
                string sanitized = RemoveInvalidCharacters(entry.Key.ToUpper());
                // Last write wins on collision — same behavior as the previous linear-scan code
                index[sanitized] = entry;
            }
            _configKeyIndex = index;
            return _configKeyIndex;
        }


        private void Awake()
        {
            Instance = this;
            Log = Logger;
            Harmony = new Harmony(PluginInfo.PLUGIN_GUID);

            try
            {
                // DIRECT LOGGING - never suppressed
                //Plugin.LogInfo($"Initializing {PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION}...");

                // Initialize the UI configuration first
                //Plugin.LogInfo("Initializing UIConfiguration...");
                _ = UIConfiguration.Instance;

                // Now we can safely check if info logs should be enabled
                if (UIConfiguration.Instance != null && UIConfiguration.Instance.IsInitialized)
                {
                    _infoLogsEnabled = UIConfiguration.Instance.ShouldLogInfo();
                    //Plugin.LogInfo($"UI configuration loaded, info logging is {(_infoLogsEnabled ? "enabled" : "disabled")}");
                }

                // Initialize trap configuration
                AllowSpikeTrapsToKillEnemies = Config.Bind("Traps",
                    "AllowSpikeTrapsToKillEnemies",
                    true,
                    "If true, spike roof traps can kill enemies. If false, spike traps will not affect enemies managed by this mod.");

                // Initialize immortal enemy protection configuration
                ProtectImmortalEnemiesFromInstaKill = Config.Bind("General",
                    "ProtectImmortalEnemiesFromInstaKill",
                    true,
                    "If true, enemies set as immortal will be protected from insta-kill effects by setting their canDie property to false.");

                ProtectOldBirdsFromOwnRockets = Config.Bind("General",
                    "ProtectOldBirdsFromOwnRockets",
                    true,
                    "If true, Old Birds will be protected from damage caused by their own rocket explosions.");

                MuteDeadEnemies = Config.Bind("General",
                    "MuteDeadEnemies",
                    true,
                    "If true, all audio sources on dead enemies are faded out and silenced when despawn is disabled.");
                
                // Initialize the configuration systems
                //Plugin.LogInfo("Initializing DespawnConfiguration...");
                _ = DespawnConfiguration.Instance;

                //Plugin.LogInfo("Initializing EnemyControlConfiguration...");
                _ = EnemyControlConfiguration.Instance;

                ModCompatibilityManager.Instance.Initialize();

                // Apply our patches
                //Plugin.LogInfo("Applying patches...");
                Patches.Initialize(Harmony);

                // Initialize the configuration menu if enabled
                if (UIConfiguration.Instance.IsConfigMenuEnabled())
                {
                    //Plugin.LogInfo("Initializing config menu...");
                    ConfigMenuPatch.Initialize(Harmony);
                    Log.LogInfo($"{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} is loaded with network support and configuration menu!");
                }
                else
                {
                    Log.LogInfo($"{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} is loaded with network support (configuration menu disabled)");
                }
            }

            catch (Exception ex)
            {
                Log.LogError($"Error initializing {PluginInfo.PLUGIN_NAME}: {ex.Message}");
                Log.LogError($"Stack trace: {ex.StackTrace}");
            }
        }


        /// Check if a specific mod is installed using the compatibility framework
        public bool IsModInstalled(string modId)
        {
            return ModCompatibilityManager.Instance.IsModInstalled(modId);
        }
        public bool IsSellBodiesModDetected => IsModInstalled("Entity378.sellbodies");

        public static bool IsModEnabledForEnemy(string mobName)
        {
            return EnemyControlConfiguration.Instance.IsModEnabledForEnemy(mobName);
        }

        // Utility method for sanitizing names
        public static string RemoveInvalidCharacters(string source)
        {
            if (string.IsNullOrEmpty(source)) return string.Empty;

            // Check cache first
            if (sanitizedNameCache.TryGetValue(source, out string cached))
                return cached;

            // Pre-size StringBuilder for efficiency
            StringBuilder sb = new StringBuilder(source.Length);
            foreach (char c in source)
            {
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
                {
                    sb.Append(c);
                }
            }

            string result = sb.ToString();
            sanitizedNameCache[source] = result; // Cache it
            return result;
        }

        // Conditional logging methods
        public static void LogInfo(string message)
        {
            // Check the simple flag first instead of accessing UIConfiguration directly
            if (_infoLogsEnabled)
            {
                Log.LogInfo(message);
            }
        }

        // Keep original logging for errors and warnings
        public static void LogError(string message)
        {
            Log.LogError(message);
        }

        public static void LogWarning(string message)
        {
            Log.LogWarning(message);
        }

        // Refresh the cached logging state when the config changes
        public static void RefreshLoggingState()
        {
            if (UIConfiguration.Instance != null && UIConfiguration.Instance.IsInitialized)
            {
                _infoLogsEnabled = UIConfiguration.Instance.ShouldLogInfo();
                LogInfo($"Logging state refreshed: info logs {(_infoLogsEnabled ? "enabled" : "disabled")}");
            }
        }

        // Check if an enemy is killable based on config
        public static bool CanMob(string identifier, string mobName)
        {
            try
            {
                string mob = RemoveInvalidCharacters(mobName).ToUpper();
                string mobConfigKey = mob + identifier.ToUpper();

                // Check cache first
                if (boolConfigCache.TryGetValue(mobConfigKey, out var cachedEntry))
                {
                    return cachedEntry.Value;
                }

                // O(1) lookup by sanitized key instead of scanning every config entry
                ConfigEntry<bool> configEntry = null;
                string sanitizedLookup = RemoveInvalidCharacters(mobConfigKey);
                if (GetConfigKeyIndex().TryGetValue(sanitizedLookup, out var existingDef))
                {
                    configEntry = (ConfigEntry<bool>)Instance.Config[existingDef];
                    boolConfigCache[mobConfigKey] = configEntry;
                    return configEntry.Value;
                }

                // Create new config if not found
                configEntry = Instance.Config.Bind("Mobs", mob + identifier, true, $"If true, {mobName} will be damageable");
                boolConfigCache[mobConfigKey] = configEntry; // Cache it
                // Keep the index consistent with the newly bound entry
                if (_configKeyIndex != null)
                    _configKeyIndex[sanitizedLookup] = new ConfigDefinition("Mobs", mob + identifier);
                return configEntry.Value;
            }
            catch (Exception e)
            {
                Log.LogError($"Error in config check for mob {mobName}: {e.Message}");
                return false;
            }
        }

        // Get enemy health from config
        public static float GetMobHealth(string mobName, float defaultHealth)
        {
            try
            {
                if (defaultHealth <= 0)
                {
                    defaultHealth = 1;
                    Plugin.LogInfo($"Enforcing minimum health of 1 for {mobName}");
                }

                string mob = RemoveInvalidCharacters(mobName).ToUpper();
                string healthKey = mob + ".HEALTH";

                // Check cache first
                if (floatConfigCache.TryGetValue(healthKey, out var cachedEntry))
                {
                    float cachedHealth = cachedEntry.Value;
                    if (cachedHealth <= 0)
                    {
                        cachedHealth = 1;
                        cachedEntry.Value = 1;
                    }
                    return cachedHealth;
                }

                // O(1) lookup by sanitized key — index keys are sanitized (no '.')
                ConfigEntry<float> configEntry = null;
                string sanitizedLookup = mob + "HEALTH";
                if (GetConfigKeyIndex().TryGetValue(sanitizedLookup, out var existingDef))
                {
                    configEntry = (ConfigEntry<float>)Instance.Config[existingDef];
                    floatConfigCache[healthKey] = configEntry;

                    float health = configEntry.Value;
                    if (health <= 0)
                    {
                        health = 1;
                        configEntry.Value = 1;
                    }
                    return health;
                }

                // Create new config if not found
                configEntry = Instance.Config.Bind("Mobs", mob + ".Health", defaultHealth, $"Health for {mobName}");
                floatConfigCache[healthKey] = configEntry; // Cache it
                if (_configKeyIndex != null)
                    _configKeyIndex[sanitizedLookup] = new ConfigDefinition("Mobs", mob + ".Health");
                return configEntry.Value;
            }
            catch (Exception e)
            {
                Log.LogError($"Error getting health for {mobName}: {e.Message}");
                return Math.Max(1f, defaultHealth);
            }
        }

        // Delegate to DespawnConfiguration class
        public static bool ShouldDespawn(string mobName)
        {
            return DespawnConfiguration.Instance.ShouldDespawnEnemy(mobName);
        }
    }

    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "nwnt.EverythingCanDieAlternative";
        public const string PLUGIN_NAME = "EverythingCanDieAlternative";
        public const string PLUGIN_VERSION = "1.1.71";
    }
}
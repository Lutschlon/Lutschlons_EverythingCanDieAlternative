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
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        public static ManualLogSource Log { get; private set; }
        public static Harmony Harmony { get; private set; }
        public static List<EnemyType> enemies = new List<EnemyType>();
        public static ConfigEntry<bool> PatchCruiserDamage { get; private set; }
        public static ConfigEntry<int> CruiserDamageAtHighSpeeds { get; private set; }

        // Trap configuration
        public static ConfigEntry<bool> AllowSpikeTrapsToKillEnemies { get; private set; }
        
        // Immortal enemy protection configuration
        public static ConfigEntry<bool> ProtectImmortalEnemiesFromInstaKill { get; private set; }

        // Flag to indicate if logging should be conditionally suppressed
        private static bool _infoLogsEnabled = true;

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

        /// Convenience method to check if SellBodies mod is installed
        public bool IsSellBodiesModDetected => IsModInstalled("Entity378.sellbodies");

        public static bool IsModEnabledForEnemy(string mobName)
        {
            return EnemyControlConfiguration.Instance.IsModEnabledForEnemy(mobName);
        }

        // Utility method for sanitizing names
        public static string RemoveInvalidCharacters(string source)
        {
            if (string.IsNullOrEmpty(source)) return string.Empty;

            StringBuilder sb = new StringBuilder();
            foreach (char c in source)
            {
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
                {
                    sb.Append(c);
                }
            }
            return string.Join("", sb.ToString().Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
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

        // Check if an enemy is killable based on config
        public static bool CanMob(string identifier, string mobName)
        {
            try
            {
                string mob = RemoveInvalidCharacters(mobName).ToUpper();
                string mobConfigKey = mob + identifier.ToUpper();

                // Create configEntry variable to store the entry we find
                ConfigEntry<bool> configEntry = null;

                foreach (ConfigDefinition entry in Instance.Config.Keys)
                {
                    if (RemoveInvalidCharacters(entry.Key.ToUpper()).Equals(RemoveInvalidCharacters(mobConfigKey)))
                    {
                        // Get the actual ConfigEntry object to ensure we get current value
                        configEntry = (ConfigEntry<bool>)Instance.Config[entry];
                        bool result = configEntry.Value;
                        //Plugin.LogInfo($"Mob config: [Mobs] {mobConfigKey} = {result}");
                        return result;
                    }
                }

                // If config doesn't exist yet, create it
                configEntry = Instance.Config.Bind("Mobs", mob + identifier, true, $"If true, {mobName} will be damageable");
                //Plugin.LogInfo($"No config found for [Mobs] {mobConfigKey}, defaulting to true");
                return configEntry.Value;
            }
            catch (Exception e)
            {
                Log.LogError($"Error in config check for mob {mobName}: {e.Message}");
                return false;
            }
        }

        // Get enemy health from config
        public static int GetMobHealth(string mobName, int defaultHealth)
        {
            try
            {
                // Ensure minimum default health of 1
                if (defaultHealth <= 0)
                {
                    defaultHealth = 1;
                    Plugin.LogInfo($"Enforcing minimum health of 1 for {mobName}");
                }

                string mob = RemoveInvalidCharacters(mobName).ToUpper();
                string healthKey = mob + ".HEALTH";

                // Create configEntry variable to store the entry we find
                ConfigEntry<int> configEntry = null;

                foreach (ConfigDefinition entry in Instance.Config.Keys)
                {
                    if (RemoveInvalidCharacters(entry.Key.ToUpper()).Equals(healthKey))
                    {
                        // Cast to correct type to ensure we access the real current value
                        configEntry = (ConfigEntry<int>)Instance.Config[entry];
                        int health = configEntry.Value;

                        // Ensure configured health is also at least 1
                        if (health <= 0)
                        {
                            health = 1;
                            Plugin.LogInfo($"Enforcing minimum configured health of 1 for {mobName}");

                            // Update the config value to 1
                            configEntry.Value = 1;
                        }

                        Plugin.LogInfo($"Enemy {mobName} health from config: {health}");
                        return health;
                    }
                }

                // If config doesn't exist yet, create it
                configEntry = Instance.Config.Bind("Mobs", mob + ".Health", defaultHealth, $"Health for {mobName}");
                Plugin.LogInfo($"Using config for {mobName} health: {configEntry.Value}");
                return configEntry.Value;
            }
            catch (Exception e)
            {
                Log.LogError($"Error getting health for {mobName}: {e.Message}");
                return Math.Max(1, defaultHealth); // Ensure at least 1 HP even in error cases
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
        public const string PLUGIN_VERSION = "1.1.61";
    }
}
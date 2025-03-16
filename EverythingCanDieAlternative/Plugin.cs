using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace EverythingCanDieAlternative
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("LethalNetworkAPI")]
    [BepInDependency("Entity378.sellbodies", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        public static ManualLogSource Log { get; private set; }
        public static Harmony Harmony { get; private set; }
        public static List<EnemyType> enemies = new List<EnemyType>();
        public bool IsSellBodiesModDetected { get; private set; } = false;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            Harmony = new Harmony(PluginInfo.PLUGIN_GUID);

            try
            {
                // Initialize the separate despawn configuration system
                _ = DespawnConfiguration.Instance;

                // Apply our patches
                Patches.Initialize(Harmony);

                Log.LogInfo($"{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} is loaded with network support!");
            }
            catch (Exception ex)
            {
                Log.LogError($"Error initializing {PluginInfo.PLUGIN_NAME}: {ex}");
            }
            // Check for SellBodies mod
            IsSellBodiesModDetected = AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => a.GetName().Name == "SellBodies" ||
                         a.GetTypes().Any(t => t.Namespace?.Contains("CleaningCompany") == true));

            if (IsSellBodiesModDetected)
                Log.LogInfo("SellBodies mod detected - enabling compatibility mode");
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

        // Check if an enemy is killable based on config
        public static bool CanMob(string identifier, string mobName)
        {
            try
            {
                string mob = RemoveInvalidCharacters(mobName).ToUpper();
                string mobConfigKey = mob + identifier.ToUpper();

                foreach (ConfigDefinition entry in Instance.Config.Keys)
                {
                    if (RemoveInvalidCharacters(entry.Key.ToUpper()).Equals(RemoveInvalidCharacters(mobConfigKey)))
                    {
                        bool result = Instance.Config[entry].BoxedValue.ToString().ToUpper().Equals("TRUE");
                        Log.LogDebug($"Mob config: [Mobs] {mobConfigKey} = {result}");
                        return result;
                    }
                }

                // If config doesn't exist yet, create it
                Instance.Config.Bind("Mobs", mob + identifier, true, $"If true, {mobName} will be damageable");
                Log.LogDebug($"No config found for [Mobs] {mobConfigKey}, defaulting to true");
                return true;
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
                string mob = RemoveInvalidCharacters(mobName).ToUpper();
                string healthKey = mob + ".HEALTH";

                foreach (ConfigDefinition entry in Instance.Config.Keys)
                {
                    if (RemoveInvalidCharacters(entry.Key.ToUpper()).Equals(healthKey))
                    {
                        int health = Convert.ToInt32(Instance.Config[entry].BoxedValue);
                        Log.LogInfo($"Enemy {mobName} health from config: {health}");
                        return health;
                    }
                }

                // If config doesn't exist yet, create it
                var configEntry = Instance.Config.Bind("Mobs", mob + ".Health", defaultHealth, $"Health for {mobName}");
                Log.LogInfo($"Created config for {mobName} health: {configEntry.Value}");
                return configEntry.Value;
            }
            catch (Exception e)
            {
                Log.LogError($"Error getting health for {mobName}: {e.Message}");
                return defaultHealth;
            }
        }

        // Moved to DespawnConfiguration class
        public static bool ShouldDespawn(string mobName)
        {
            return DespawnConfiguration.Instance.ShouldDespawnEnemy(mobName);
        }

    }

    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "nwnt.EverythingCanDieAlternative";
        public const string PLUGIN_NAME = "EverythingCanDieAlternative";
        public const string PLUGIN_VERSION = "1.1.2";
    }
}
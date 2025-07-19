using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace EverythingCanDieAlternative.ModCompatibility.Handlers
{
    // Compatibility handler for the LethalHands mod
    public class LethalHandsCompatibility : BaseModCompatibility
    {
        public override string ModId => "SlapitNow.LethalHands";
        public override string ModName => "Lethal Hands";

        // Cache the result to avoid repeated reflection
        private bool? _isInstalled = null;

        // Cache for the punch damage value from LethalHands config
        private float? _punchDamage = null;

        // Override to use more reliable detection methods
        public override bool IsInstalled
        {
            get
            {
                // Return cached result if available
                if (_isInstalled.HasValue)
                    return _isInstalled.Value;

                try
                {
                    // Multiple detection strategies
                    var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                    // Look for assembly with matching name first (safest)
                    foreach (var assembly in assemblies)
                    {
                        try
                        {
                            if (assembly.GetName().Name == "LethalHands" ||
                                assembly.GetName().Name.Contains("LethalHands"))
                            {
                                _isInstalled = true;
                                return true;
                            }
                        }
                        catch
                        {
                            // Ignore errors for individual assemblies
                            continue;
                        }
                    }

                    // Check if a key type exists that would indicate LethalHands is installed
                    Type lethalHandsType = Type.GetType("LethalHands.LethalHands, LethalHands");
                    if (lethalHandsType != null)
                    {
                        _isInstalled = true;
                        return true;
                    }

                    // Check for LethalHandsNetworker which is another key class
                    Type networkerType = Type.GetType("LethalHands.LethalHandsNetworker, LethalHands");
                    if (networkerType != null)
                    {
                        _isInstalled = true;
                        return true;
                    }

                    // Check if the plugin GUID exists in BepInEx plugins
                    bool pluginExists = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(ModId);
                    _isInstalled = pluginExists;
                    return pluginExists;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"Error detecting LethalHands mod: {ex.Message}");
                    _isInstalled = false;
                    return false;
                }
            }
        }

        protected override void OnModInitialize()
        {
            //Plugin.LogInfo("LethalHands compatibility initialized");

            // Try to find and cache the punch damage value from LethalHands
            TryGetPunchDamageValue();
        }

        // Get the punch damage value from LethalHands mod
        public float GetPunchDamage()
        {
            if (_punchDamage.HasValue)
                return _punchDamage.Value;

            // Default value if we can't get the actual value
            return 1;
        }

        // Tries to extract the punch damage value from LethalHands using reflection
        private void TryGetPunchDamageValue()
        {
            try
            {
                if (!IsInstalled)
                    return;

                // First try to read the config file directly (most reliable)
                string configPath = Path.Combine(BepInEx.Paths.ConfigPath, "SlapitNow.LethalHands.cfg");
                if (File.Exists(configPath))
                {
                    try
                    {
                        string[] lines = File.ReadAllLines(configPath);
                        foreach (string line in lines)
                        {
                            if (line.StartsWith("EnemyPunchDamage = "))
                            {
                                string valueStr = line.Substring("EnemyPunchDamage = ".Length).Trim();
                                if (float.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float configValue))
                                {
                                    _punchDamage = configValue;
                                    Plugin.LogInfo($"Found LethalHands punch damage from config: {_punchDamage}");
                                    return;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogWarning($"Error reading LethalHands config file: {ex.Message}");
                    }
                }

                // Fallback: Try to get the value using reflection
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    try
                    {
                        if (assembly.GetName().Name.Contains("LethalHands"))
                        {
                            // Look for the LethalHands type that contains the damage value
                            var lethalHandsType = assembly.GetTypes()
                                .FirstOrDefault(t => t.Name == "LethalHands");

                            if (lethalHandsType != null)
                            {
                                // Try to get the instance
                                var instanceProperty = lethalHandsType.GetProperty("Instance",
                                    BindingFlags.Public | BindingFlags.Static);

                                if (instanceProperty != null)
                                {
                                    var instance = instanceProperty.GetValue(null);
                                    if (instance != null)
                                    {
                                        // Try to get the enemyPunchDamage field/property
                                        var damageField = lethalHandsType.GetField("enemyPunchDamage",
                                            BindingFlags.Public | BindingFlags.Instance);

                                        if (damageField != null)
                                        {
                                            var damageValue = damageField.GetValue(instance);
                                            if (damageValue != null)
                                            {
                                                _punchDamage = Convert.ToSingle(damageValue);
                                                Plugin.LogInfo($"Found LethalHands punch damage: {_punchDamage}");
                                                return;
                                            }
                                        }
                                    }
                                }
                            }

                            // Try checking LocalConfig or NetworkConfig
                            Type[] configTypes = new Type[]
                            {
                                assembly.GetTypes().FirstOrDefault(t => t.Name == "LocalConfig"),
                                assembly.GetTypes().FirstOrDefault(t => t.Name == "NetworkConfig")
                            };

                            foreach (var configType in configTypes)
                            {
                                if (configType != null)
                                {
                                    var configField = configType.GetField("enemyPunchDamage",
                                        BindingFlags.Public | BindingFlags.Static);

                                    if (configField != null)
                                    {
                                        var damageValue = configField.GetValue(null);
                                        if (damageValue != null)
                                        {
                                            _punchDamage = Convert.ToSingle(damageValue);
                                            Plugin.LogInfo($"Found LethalHands punch damage from config: {_punchDamage}");
                                            return;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogWarning($"Error inspecting LethalHands assembly: {ex.Message}");
                        continue;
                    }
                }

                // If we couldn't get the value through reflection or config, use the default
                _punchDamage = 1f;
                Plugin.LogInfo($"Using default LethalHands punch damage: {_punchDamage}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error getting LethalHands punch damage: {ex.Message}");
                _punchDamage = 1f;
            }
        }

        // Converts the special LethalHands punch force (-22) to a proper damage value
        public float ConvertPunchForceToDamage(float force)
        {
            // Only convert if this is a LethalHands punch (-22 force)
            if (force == -22)
            {
                // Use the punch damage value from LethalHands if available
                float punchDamage = GetPunchDamage();

                // Return the punch damage as float, allowing values less than 1.0
                return punchDamage;
            }

            // Return the original force for non-LethalHands hits
            return force;
        }
    }
}
using System;
using System.Linq;
using System.Reflection;

namespace EverythingCanDieAlternative.ModCompatibility.Handlers
{
    /// <summary>
    /// Compatibility handler for the SellBodies mod
    /// </summary>
    public class SellBodiesCompatibility : BaseModCompatibility
    {
        public override string ModId => "Entity378.sellbodies";
        public override string ModName => "Sell Bodies";

        // Cache the result to avoid repeated reflection
        private bool? _isInstalled = null;

        // Safer override for IsInstalled - avoids loading types from all assemblies
        public override bool IsInstalled
        {
            get
            {
                // Return cached result if available
                if (_isInstalled.HasValue)
                    return _isInstalled.Value;

                try
                {
                    // First try the BepInEx plugin GUID approach - safer
                    var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                    // Look for assembly with matching name first (safest)
                    foreach (var assembly in assemblies)
                    {
                        try
                        {
                            if (assembly.GetName().Name == "SellBodies" ||
                                assembly.GetName().Name.Contains("sellbodies"))
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

                    // Check if the plugin GUID exists in BepInEx plugins
                    foreach (var assembly in assemblies)
                    {
                        try
                        {
                            // Look for the BepInPlugin attribute with our expected GUID
                            var types = assembly.GetTypes();
                            foreach (var type in types)
                            {
                                var attributes = type.GetCustomAttributes(false);
                                foreach (var attr in attributes)
                                {
                                    var attrType = attr.GetType();
                                    if (attrType.Name == "BepInPlugin" || attrType.Name == "BepInPluginAttribute")
                                    {
                                        var guidProperty = attrType.GetProperty("GUID") ??
                                                          attrType.GetProperty("guid") ??
                                                          attrType.GetField("GUID")?.GetValue(attr);

                                        if (guidProperty != null)
                                        {
                                            string guid = guidProperty.ToString();
                                            if (guid == ModId)
                                            {
                                                _isInstalled = true;
                                                return true;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Skip errors for individual assemblies or types
                            continue;
                        }
                    }

                    // Finally, use a hacky approach - check if a SellBodies specific type exists
                    // This avoids scanning all types in all assemblies
                    var sellBodiesType = Type.GetType("SellBodies.SellBodiesNetworkManager, SellBodies");
                    if (sellBodiesType != null)
                    {
                        _isInstalled = true;
                        return true;
                    }

                    // Use a simple existence check instead of checking namespaces
                    bool pluginExists = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(ModId);
                    _isInstalled = pluginExists;
                    return pluginExists;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"Error detecting SellBodies mod: {ex.Message}");

                    // Fall back to using a direct check from the Plugin class
                    // This uses the existing detection that was already working
                    _isInstalled = Plugin.Instance.IsSellBodiesModDetected;
                    return _isInstalled.Value;
                }
            }
        }

        protected override void OnModInitialize()
        {
            // SellBodies specific initialization
            Plugin.Log.LogInfo("SellBodies compatibility initialized");
        }

        /// <summary>
        /// Get the appropriate despawn delay when SellBodies mod is active
        /// </summary>
        /// <returns>The despawn delay in seconds</returns>
        public float GetDespawnDelay()
        {
            // With SellBodies, we need a longer delay to allow for body selling
            return 4.5f; // 4.5 seconds is slightly longer than SellBodies' 4-second timer
        }
    }
}
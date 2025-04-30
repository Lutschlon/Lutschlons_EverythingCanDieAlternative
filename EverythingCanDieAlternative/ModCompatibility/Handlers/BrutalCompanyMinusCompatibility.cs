using System;
using System.Linq;
using System.Reflection;

namespace EverythingCanDieAlternative.ModCompatibility.Handlers
{
    /// <summary>
    /// Compatibility handler for the BrutalCompanyMinus mod
    /// </summary>
    public class BrutalCompanyMinusCompatibility : BaseModCompatibility
    {
        public override string ModId => "SoftDiamond.BrutalCompanyMinusExtraReborn";
        public override string ModName => "BrutalCompanyMinusExtraReborn";

        // Cache the result to avoid repeated reflection
        private bool? _isInstalled = null;

        // Cache for BrutalCompanyMinus bonus HP value
        private int? _cachedBonusHp = null;

        // Field references obtained via reflection
        private FieldInfo _bonusEnemyHpField = null;
        private bool _fieldsInitialized = false;

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
                    // First try the BepInEx plugin GUID approach - safer
                    var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                    // Look for assembly with matching name first (safest)
                    foreach (var assembly in assemblies)
                    {
                        try
                        {
                            if (assembly.GetName().Name == "BrutalCompanyMinus" ||
                                assembly.GetName().Name.Contains("BrutalCompanyMinusExtraReborn"))
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
                    bool pluginExists = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(ModId);
                    _isInstalled = pluginExists;
                    return pluginExists;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"Error detecting BrutalCompanyMinus mod: {ex.Message}");
                    _isInstalled = false;
                    return false;
                }
            }
        }

        protected override void OnModInitialize()
        {
            // BrutalCompanyMinus specific initialization
            //Plugin.LogInfo("BrutalCompanyMinus compatibility initialized");

            // Initialize reflection references
            InitializeReflectionFields();
        }

        private void InitializeReflectionFields()
        {
            if (_fieldsInitialized || !IsInstalled)
                return;

            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    try
                    {
                        if (assembly.GetName().Name == "BrutalCompanyMinus" ||
                            assembly.GetName().Name.Contains("BrutalCompanyMinusExtraReborn"))
                        {
                            // Get the Manager class
                            var managerType = assembly.GetTypes()
                                .FirstOrDefault(t => t.Name == "Manager" && t.Namespace == "BrutalCompanyMinus.Minus");

                            if (managerType != null)
                            {
                                // Get the bonusEnemyHp field - this is a static field
                                _bonusEnemyHpField = managerType.GetField("bonusEnemyHp",
                                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                                if (_bonusEnemyHpField != null)
                                {
                                    Plugin.LogInfo("Successfully located BrutalCompanyMinus.Minus.Manager.bonusEnemyHp field");
                                    _fieldsInitialized = true;
                                    return;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogWarning($"Error inspecting assembly {assembly.GetName().Name}: {ex.Message}");
                    }
                }

                Plugin.Log.LogWarning("Could not find all required BrutalCompanyMinus fields");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error initializing reflection fields for BrutalCompanyMinus: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the bonus HP value from BrutalCompanyMinus
        /// Called when we need to apply the bonus HP to an enemy
        /// </summary>
        public int GetBonusHp()
        {
            // Return cached value if available and not null
            if (_cachedBonusHp.HasValue)
                return _cachedBonusHp.Value;

            try
            {
                if (!IsInstalled)
                    return 0;

                // Make sure we have initialized the fields
                if (!_fieldsInitialized)
                    InitializeReflectionFields();

                // Get the bonus HP value through reflection
                if (_bonusEnemyHpField != null)
                {
                    var bonusHp = _bonusEnemyHpField.GetValue(null);
                    if (bonusHp != null)
                    {
                        _cachedBonusHp = Convert.ToInt32(bonusHp);
                        Plugin.LogInfo($"Retrieved BrutalCompanyMinus bonus HP: {_cachedBonusHp}");
                        return _cachedBonusHp.Value;
                    }
                }

                // If we couldn't get a value, check for StrongEnemies event
                _cachedBonusHp = CheckForStrongEnemiesEvent();
                return _cachedBonusHp.Value;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error getting BrutalCompanyMinus bonus HP: {ex.Message}");
                _cachedBonusHp = 0;
                return 0;
            }
        }

        private int CheckForStrongEnemiesEvent()
        {
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    try
                    {
                        if (assembly.GetName().Name == "BrutalCompanyMinus" ||
                            assembly.GetName().Name.Contains("BrutalCompanyMinusExtraReborn"))
                        {
                            // Check for StrongEnemies event class
                            var strongEnemiesType = assembly.GetTypes()
                                .FirstOrDefault(t => t.Name == "StrongEnemies" && t.Namespace == "BrutalCompanyMinus.Minus.Events");

                            if (strongEnemiesType != null)
                            {
                                // Try to find a static Instance field
                                var instanceField = strongEnemiesType.GetField("Instance",
                                    BindingFlags.Public | BindingFlags.Static);

                                if (instanceField != null)
                                {
                                    var instance = instanceField.GetValue(null);
                                    if (instance != null)
                                    {
                                        // Try to check if the event is active
                                        var activeProperty = strongEnemiesType.GetProperty("Active",
                                            BindingFlags.Public | BindingFlags.Instance);

                                        if (activeProperty != null)
                                        {
                                            var isActive = Convert.ToBoolean(activeProperty.GetValue(instance));
                                            if (isActive)
                                            {
                                                // StrongEnemies event is active, try to get the actual value
                                                // From the code we see it adds a random amount between Min and Max HP
                                                Plugin.LogInfo("BrutalCompanyMinus StrongEnemies event is active");
                                                return 3; // Average of typically 1-6 range as fallback
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Ignore errors for individual assemblies
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error checking for StrongEnemies event: {ex.Message}");
            }

            return 0;
        }

        /// <summary>
        /// Invalidate the cached bonus HP value to force a refresh on the next call to GetBonusHp()
        /// </summary>
        public void InvalidateCache()
        {
            _cachedBonusHp = null;
        }

        /// <summary>
        /// Called to refresh the bonus HP cache when BrutalCompanyMinus might have changed the value
        /// </summary>
        public void RefreshBonusHp()
        {
            InvalidateCache();
            int bonusHp = GetBonusHp();
            Plugin.LogInfo($"Refreshed BrutalCompanyMinus bonus HP: {bonusHp}");
        }

        /// <summary>
        /// Applies the bonus HP from BrutalCompanyMinus to the given base health value
        /// </summary>
        public int ApplyBonusHp(int baseHealth)
        {
            int bonusHp = GetBonusHp();
            if (bonusHp > 0)
            {
                Plugin.LogInfo($"Applying BrutalCompanyMinus bonus HP: +{bonusHp} to base health {baseHealth}");
                return baseHealth + bonusHp;
            }
            return baseHealth;
        }
    }
}
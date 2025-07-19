using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace EverythingCanDieAlternative.ModCompatibility.Handlers
{
    // Compatibility handler for the LethalMin mod - ensures pikmin can properly deal damage to other enemies
    public class LethalMinCompatibility : BaseModCompatibility
    {
        public override string ModId => "NoteBoxz.LethalMin";
        public override string ModName => "LethalMin";
        
        // Override IsInstalled to use a more robust detection method
        public override bool IsInstalled
        {
            get
            {
                try
                {
                    // Look for LethalMin assembly specifically
                    var lethalMinAssembly = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name.Contains("LethalMin"));
                    
                    if (lethalMinAssembly != null)
                    {
                        Plugin.LogInfo($"LethalMin assembly found: {lethalMinAssembly.GetName().Name}");
                        return true;
                    }
                    
                    // Alternative: look for specific LethalMin types
                    return GetTypeFromAssembly("PikminAI") != null || GetTypeFromAssembly("PikminEnemy") != null;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"Error detecting {ModName}: {ex.Message}");
                    return false;
                }
            }
        }
        
        // Cache for pikmin type checking
        private Type _pikminAIType;
        private Type _pikminEnemyType;
        
        // Thread-safe tracking for pikmin damage sources
        private readonly Dictionary<int, float> _pikminDamageRequests = new Dictionary<int, float>();
        private readonly object _damageTrackingLock = new object();
        
        protected override void OnModInitialize()
        {
            try
            {
                // Get pikmin types for identification
                _pikminAIType = GetTypeFromAssembly("PikminAI");
                _pikminEnemyType = GetTypeFromAssembly("PikminEnemy");
                
                if (_pikminAIType == null || _pikminEnemyType == null)
                {
                    Plugin.Log.LogWarning($"Could not find pikmin types in {ModName} - some features may not work");
                    return;
                }
                
                // Apply patches to intercept pikmin damage calls
                ApplyPikminDamagePatches();
                
                Plugin.LogInfo($"{ModName} compatibility initialized successfully");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error initializing {ModName} compatibility: {ex.Message}");
            }
        }
        
        // Apply Harmony patches to intercept pikmin damage calls
        private void ApplyPikminDamagePatches()
        {
            try
            {
                // Patch PikminEnemy.HitEnemyServerRpc to register damage before it's applied
                var networkObjectRefType = Type.GetType("Unity.Netcode.NetworkObjectReference, Unity.Netcode.Runtime");
                var hitEnemyServerRpcMethod = GetTypeFromAssembly("PikminEnemy")?.GetMethod("HitEnemyServerRpc", 
                    new[] { typeof(float), networkObjectRefType });
                
                if (hitEnemyServerRpcMethod != null)
                {
                    var prefixMethod = AccessTools.Method(typeof(LethalMinCompatibility), nameof(PikminDamagePrefix));
                    Plugin.Harmony.Patch(hitEnemyServerRpcMethod, new HarmonyMethod(prefixMethod));
                    Plugin.LogInfo("Successfully patched PikminEnemy.HitEnemyServerRpc");
                }
                
                // Also patch the single-parameter version
                var hitEnemyServerRpcSingleMethod = GetTypeFromAssembly("PikminEnemy")?.GetMethod("HitEnemyServerRpc", 
                    new[] { typeof(float) });
                
                if (hitEnemyServerRpcSingleMethod != null)
                {
                    var prefixMethod = AccessTools.Method(typeof(LethalMinCompatibility), nameof(PikminDamageSinglePrefix));
                    Plugin.Harmony.Patch(hitEnemyServerRpcSingleMethod, new HarmonyMethod(prefixMethod));
                    Plugin.LogInfo("Successfully patched PikminEnemy.HitEnemyServerRpc (single param)");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error applying pikmin damage patches: {ex.Message}");
            }
        }
        
        // Prefix patch for PikminEnemy.HitEnemyServerRpc to register damage
        public static void PikminDamagePrefix(object __instance, float Damage, object PikRef)
        {
            try
            {
                var handler = ModCompatibilityManager.Instance.GetHandler<LethalMinCompatibility>("NoteBoxz.LethalMin");
                if (handler != null && handler.IsInstalled)
                {
                    // Register the damage for this enemy instance
                    if (__instance != null)
                    {
                        int instanceId = __instance.GetHashCode(); // Use GetHashCode as a fallback
                        handler.RegisterPikminDamage(instanceId, Damage);
                        Plugin.LogInfo($"Registered pikmin damage: {Damage} for instance {instanceId}");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in PikminDamagePrefix: {ex.Message}");
            }
        }
        
        // Prefix patch for PikminEnemy.HitEnemyServerRpc (single param) to register damage
        public static void PikminDamageSinglePrefix(object __instance, float Damage)
        {
            try
            {
                var handler = ModCompatibilityManager.Instance.GetHandler<LethalMinCompatibility>("NoteBoxz.LethalMin");
                if (handler != null && handler.IsInstalled)
                {
                    // Register the damage for this enemy instance
                    if (__instance != null)
                    {
                        int instanceId = __instance.GetHashCode(); // Use GetHashCode as a fallback
                        handler.RegisterPikminDamage(instanceId, Damage);
                        Plugin.LogInfo($"Registered pikmin damage: {Damage} for instance {instanceId}");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in PikminDamageSinglePrefix: {ex.Message}");
            }
        }
        
        // Check if an enemy is a pikmin entity
        public bool IsPikmin(EnemyAI enemy)
        {
            if (enemy == null || !IsInstalled) return false;
            
            try
            {
                // Check if the enemy is a pikmin type
                return _pikminAIType != null && _pikminAIType.IsInstanceOfType(enemy);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error checking if enemy is pikmin: {ex.Message}");
                return false;
            }
        }
        
        // Check if an enemy is a pikmin enemy (can be attacked by pikmin)
        public bool IsPikminEnemy(EnemyAI enemy)
        {
            if (enemy == null || !IsInstalled) return false;
            
            try
            {
                // Check if the enemy is a pikmin enemy type
                return _pikminEnemyType != null && _pikminEnemyType.IsInstanceOfType(enemy);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error checking if enemy is pikmin enemy: {ex.Message}");
                return false;
            }
        }
        
        // Check if damage is coming from a pikmin source - helps identify when the damage should be processed normally
        public bool IsPikminDamageSource(EnemyAI potentialPikmin)
        {
            return IsPikmin(potentialPikmin);
        }
        
        // Get the current damage multiplier for pikmin
        public float GetPikminDamageMultiplier()
        {
            if (!IsInstalled) return 1.0f;
            
            try
            {
                // Try to get the damage multiplier from LethalMin's config
                // This would need to be implemented based on LethalMin's actual API
                return 1.0f; // Default fallback
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error getting pikmin damage multiplier: {ex.Message}");
                return 1.0f;
            }
        }
        
        // Register a pikmin damage request (called before the actual hit)
        public void RegisterPikminDamage(int targetInstanceId, float damage)
        {
            if (!IsInstalled) return;
            
            lock (_damageTrackingLock)
            {
                _pikminDamageRequests[targetInstanceId] = damage;
            }
        }
        
        // Check if a damage request is from a pikmin and consume it
        public bool ConsumePikminDamage(int targetInstanceId, out float damage)
        {
            damage = 0f;
            if (!IsInstalled) return false;
            
            lock (_damageTrackingLock)
            {
                if (_pikminDamageRequests.TryGetValue(targetInstanceId, out damage))
                {
                    _pikminDamageRequests.Remove(targetInstanceId);
                    return true;
                }
            }
            
            return false;
        }
        
        // Process a hit from a pikmin to ensure it's handled correctly
        public bool ProcessPikminHit(EnemyAI target, int damage, EnemyAI pikminSource)
        {
            if (!IsInstalled || target == null || pikminSource == null) return false;
            
            try
            {
                // Verify this is actually a pikmin doing the damage
                if (!IsPikmin(pikminSource))
                {
                    return false;
                }
                
                Plugin.LogInfo($"Processing pikmin hit: {pikminSource.enemyType.enemyName} -> {target.enemyType.enemyName} (damage: {damage})");
                
                // Apply damage multiplier if available
                float multiplier = GetPikminDamageMultiplier();
                int finalDamage = Mathf.RoundToInt(damage * multiplier);
                
                // Process the hit through our health system
                // We pass null as the player since this is enemy-vs-enemy damage
                HealthManager.ProcessHit(target, finalDamage, null);
                
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error processing pikmin hit: {ex.Message}");
                return false;
            }
        }
        
        // Clear expired damage requests (called periodically)
        public void ClearExpiredDamageRequests()
        {
            if (!IsInstalled) return;
            
            lock (_damageTrackingLock)
            {
                _pikminDamageRequests.Clear();
            }
        }
        
        // Helper method to get a type from the LethalMin assembly
        private Type GetTypeFromAssembly(string typeName)
        {
            try
            {
                // Search through all loaded assemblies for the type
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        // Skip assemblies that are likely to cause issues
                        if (assembly.FullName.Contains("MonoDetour") || 
                            assembly.FullName.Contains("RuntimeDetour") ||
                            assembly.FullName.Contains("HarmonyLib"))
                            continue;
                            
                        // Try to get all types from the assembly, handling ReflectionTypeLoadException
                        Type[] types = null;
                        try
                        {
                            types = assembly.GetTypes();
                        }
                        catch (ReflectionTypeLoadException ex)
                        {
                            // Use the types that loaded successfully
                            types = ex.Types.Where(t => t != null).ToArray();
                        }
                        
                        if (types != null)
                        {
                            // Look for the type by name
                            var type = types.FirstOrDefault(t => t.Name == typeName);
                            if (type != null)
                            {
                                return type;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log but continue - some assemblies may not be accessible
                        Plugin.Log.LogDebug($"Could not search assembly {assembly.FullName}: {ex.Message}");
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error getting type {typeName}: {ex.Message}");
                return null;
            }
        }
    }
}
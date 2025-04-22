using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using GameNetcodeStuff;

namespace EverythingCanDieAlternative.ModCompatibility.Handlers
{
    /// <summary>
    /// Compatibility handler for the Hitmarker mod
    /// </summary>
    public class HitmarkerCompatibility : BaseModCompatibility
    {
        public override string ModId => "com.github.zehsteam.Hitmarker";
        public override string ModName => "Hitmarker";

        // Dictionary to track last damage source - only created if mod is installed
        private Dictionary<int, PlayerControllerB> _lastDamageSources;

        // Cache reflection info
        private Type _hitmarkerCanvasBehaviourType;
        private FieldInfo _instanceField; // Changed from PropertyInfo to FieldInfo
        private MethodInfo _showHitmarkerMethod;
        private MethodInfo _showKillMessageMethod;

        // Track initialization status
        private bool _initialized = false;

        // Override IsInstalled to use a more robust detection method
        public override bool IsInstalled
        {
            get
            {
                try
                {
                    // First check using base implementation
                    if (base.IsInstalled)
                        return true;

                    // Then check for specific assembly
                    return AppDomain.CurrentDomain.GetAssemblies()
                        .Any(a => a.GetName().Name == "com.github.zehsteam.Hitmarker" ||
                              a.GetName().Name.Contains("Hitmarker"));
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"Error detecting {ModName}: {ex.Message}");
                    return false;
                }
            }
        }

        protected override void OnModInitialize()
        {
            try
            {
                // Initialize the damage tracking dictionary ONLY if the mod is installed
                if (IsInstalled)
                {
                    _lastDamageSources = new Dictionary<int, PlayerControllerB>();
                    Plugin.LogInfo($"Initializing damage tracking for {ModName} compatibility");
                }
                else
                {
                    Plugin.LogInfo($"{ModName} mod not detected, skipping compatibility initialization");
                    return;
                }

                // Find all loaded assemblies for debugging
                Plugin.LogInfo("Looking for Hitmarker in loaded assemblies:");
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name.Contains("Hitmarker"))
                    {
                        Plugin.LogInfo($"Found assembly: {assembly.GetName().Name}, {assembly.GetName().Version}");
                    }
                }

                // Search for the HitmarkerCanvasBehaviour type using a more robust approach
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        if (assembly.GetName().Name.Contains("Hitmarker"))
                        {
                            // Look for the HitmarkerCanvasBehaviour class in this assembly
                            foreach (Type type in assembly.GetTypes())
                            {
                                if (type.Name == "HitmarkerCanvasBehaviour")
                                {
                                    _hitmarkerCanvasBehaviourType = type;
                                    Plugin.LogInfo($"Found HitmarkerCanvasBehaviour in assembly {assembly.GetName().Name}");
                                    break;
                                }
                            }

                            if (_hitmarkerCanvasBehaviourType != null)
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Just log and continue to next assembly
                        Plugin.Log.LogWarning($"Error checking assembly {assembly.GetName().Name}: {ex.Message}");
                    }
                }

                if (_hitmarkerCanvasBehaviourType != null)
                {
                    // Get the Instance FIELD (not property)
                    _instanceField = _hitmarkerCanvasBehaviourType.GetField("Instance",
                        BindingFlags.Public | BindingFlags.Static);

                    if (_instanceField != null)
                        Plugin.LogInfo("Found Instance field");
                    else
                    {
                        Plugin.Log.LogWarning("Could not find Instance field, looking for fields:");
                        foreach (var field in _hitmarkerCanvasBehaviourType.GetFields(BindingFlags.Public | BindingFlags.Static))
                        {
                            Plugin.Log.LogWarning($"  - {field.Name}");
                        }
                    }

                    // Get the methods we need with diagnostic info
                    _showHitmarkerMethod = _hitmarkerCanvasBehaviourType.GetMethod("ShowHitmarker",
                        BindingFlags.Public | BindingFlags.Instance);

                    if (_showHitmarkerMethod != null)
                        Plugin.LogInfo("Found ShowHitmarker method");
                    else
                        Plugin.Log.LogWarning("Could not find ShowHitmarker method");

                    _showKillMessageMethod = _hitmarkerCanvasBehaviourType.GetMethod("ShowKillMessage",
                        BindingFlags.Public | BindingFlags.Instance);

                    if (_showKillMessageMethod != null)
                        Plugin.LogInfo("Found ShowKillMessage method");
                    else
                        Plugin.Log.LogWarning("Could not find ShowKillMessage method");

                    // Check if all needed components are found
                    if (_instanceField != null && _showHitmarkerMethod != null && _showKillMessageMethod != null)
                    {
                        _initialized = true;
                        Plugin.LogInfo($"Successfully initialized {ModName} compatibility");
                    }
                    else
                    {
                        Plugin.Log.LogWarning($"Could not find all required methods for {ModName} compatibility");
                    }
                }
                else
                {
                    Plugin.Log.LogWarning($"Could not find HitmarkerCanvasBehaviour class for {ModName} compatibility");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error initializing {ModName} compatibility: {ex.Message}");
                Plugin.Log.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Track which player damaged an enemy - only used when compatibility is active
        /// </summary>
        public void TrackDamageSource(int enemyInstanceId, PlayerControllerB playerWhoHit)
        {
            if (!_initialized || _lastDamageSources == null || playerWhoHit == null)
                return;

            try
            {
                _lastDamageSources[enemyInstanceId] = playerWhoHit;
                Plugin.LogInfo($"Tracked damage source for enemy ID {enemyInstanceId}: {playerWhoHit.playerUsername}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error tracking damage source: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the player who last damaged an enemy
        /// </summary>
        public PlayerControllerB GetLastDamageSource(int enemyInstanceId)
        {
            if (!_initialized || _lastDamageSources == null)
                return null;

            if (_lastDamageSources.TryGetValue(enemyInstanceId, out PlayerControllerB player))
            {
                Plugin.LogInfo($"Found last damage source for enemy ID {enemyInstanceId}: {player.playerUsername}");
                return player;
            }

            Plugin.LogInfo($"No damage source found for enemy ID {enemyInstanceId}");
            return null;
        }

        /// <summary>
        /// Clear tracking when a level is unloaded
        /// </summary>
        public void ClearTracking()
        {
            if (_lastDamageSources != null)
            {
                _lastDamageSources.Clear();
                Plugin.LogInfo("Cleared Hitmarker damage tracking data");
            }
        }

        /// <summary>
        /// Notify the Hitmarker mod when an enemy is killed
        /// </summary>
        public void NotifyEnemyKilled(EnemyAI enemy, PlayerControllerB playerWhoKilled)
        {
            if (!_initialized || enemy == null)
                return;

            try
            {
                // Try to get the HitmarkerCanvasBehaviour instance from the field (not property)
                object hitmarkerInstance = _instanceField.GetValue(null);
                if (hitmarkerInstance == null)
                {
                    Plugin.Log.LogWarning("Hitmarker canvas instance is null, cannot notify of kill");
                    return;
                }

                // Determine if it's the local player
                bool isLocalPlayer = false;
                string playerName = "Unknown";

                if (playerWhoKilled != null)
                {
                    isLocalPlayer = playerWhoKilled == StartOfRound.Instance.localPlayerController;
                    playerName = playerWhoKilled.playerUsername;
                }

                Plugin.LogInfo($"Notifying Hitmarker: Enemy {enemy.enemyType.enemyName} killed by {playerName} (local: {isLocalPlayer})");

                // Call ShowHitmarker with killed=true for the red color
                _showHitmarkerMethod.Invoke(hitmarkerInstance, new object[] { true });

                // Call ShowKillMessage
                _showKillMessageMethod.Invoke(hitmarkerInstance, new object[] {
                    enemy.enemyType.enemyName,
                    isLocalPlayer,
                    playerName
                });

                Plugin.LogInfo($"Successfully notified Hitmarker of kill: {enemy.enemyType.enemyName} by {(isLocalPlayer ? "local player" : playerName)}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error notifying Hitmarker of enemy kill: {ex.Message}");
                Plugin.Log.LogError($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}
using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using LethalNetworkAPI;
using EverythingCanDieAlternative.ModCompatibility;
using EverythingCanDieAlternative.ModCompatibility.Handlers;
using static EverythingCanDieAlternative.Plugin;

namespace EverythingCanDieAlternative
{
    public static class HealthManager
    {
        // Add a counter to ensure unique network variable names
        private static int networkVarCounter = 0;

        // Dictionary to map enemy instance IDs to their NetworkVariables
        private static readonly Dictionary<int, LNetworkVariable<int>> enemyHealthVars = new Dictionary<int, LNetworkVariable<int>>();

        // Dictionary to store max health values
        private static readonly Dictionary<int, int> enemyMaxHealth = new Dictionary<int, int>();

        // Dictionary to track which enemies we've processed
        private static readonly Dictionary<int, bool> processedEnemies = new Dictionary<int, bool>();

        // Dictionary to map enemy IDs to their network object ID - for cross-client lookup
        private static readonly Dictionary<int, ulong> enemyNetworkIds = new Dictionary<int, ulong>();

        // Dictionary to track the network variable names for each enemy instance ID
        private static readonly Dictionary<int, string> enemyNetworkVarNames = new Dictionary<int, string>();

        // Dictionary to track which enemies are in despawn process
        private static readonly Dictionary<int, bool> enemiesInDespawnProcess = new Dictionary<int, bool>();

        // NEW: Dictionary to track which enemies should be immortal (Enabled=true, Unimmortal=false)
        private static readonly Dictionary<int, bool> immortalEnemies = new Dictionary<int, bool>();

        // Network message for despawning enemies
        private static LNetworkMessage<int> despawnMessage;

        // IMPORTANT: Static hit message reference available to the whole class
        private static LNetworkMessage<HitData> hitMessage;

        // Structure to send hit data
        [Serializable]
        public struct HitData
        {
            public int EnemyInstanceId;      // Local instance ID
            public ulong EnemyNetworkId;     // Network ID (from NetworkObject)
            public int EnemyIndex;           // Enemy index (more stable across network)
            public string EnemyName;         // Enemy name (for better logging)
            public int Damage;
            public ulong PlayerClientId;     // Store the client ID of the player who hit the enemy

            public override string ToString()
            {
                return $"HitData(EnemyId={EnemyInstanceId}, NetworkId={EnemyNetworkId}, Index={EnemyIndex}, Name={EnemyName}, Damage={Damage}, PlayerClientId={PlayerClientId})";
            }
        }

        public static void Initialize()
        {
            // Clear all dictionaries
            enemyHealthVars.Clear();
            enemyMaxHealth.Clear();
            processedEnemies.Clear();
            enemyNetworkIds.Clear();
            enemyNetworkVarNames.Clear();
            enemiesInDespawnProcess.Clear();
            immortalEnemies.Clear();
            InitializeCompatibilityHandlerCache();
            CreateNetworkMessages();

            Plugin.LogInfo("Networked Health Manager initialized");

        }

        private static void InitializeCompatibilityHandlerCache()
        {
            cachedLethalHandsHandler = ModCompatibilityManager.Instance.GetHandler<LethalHandsCompatibility>("SlapitNow.LethalHands");
            cachedHitmarkerHandler = ModCompatibilityManager.Instance.GetHandler<HitmarkerCompatibility>("com.github.zehsteam.Hitmarker");
            cachedBrutalCompanyHandler = ModCompatibilityManager.Instance.GetHandler<BrutalCompanyMinusCompatibility>("SoftDiamond.BrutalCompanyMinusExtraReborn");
        }

        private static void CreateNetworkMessages()
        {
            try
            {
                // Create the hit message
                hitMessage = LNetworkMessage<HitData>.Create("ECD_HitMessage",
                // First param: server receive callback
                (hitData, clientId) =>
                {
                    Plugin.LogInfo($"[HOST] Received hit message from client {clientId}: {hitData}");
                    if (StartOfRound.Instance.IsHost)
                    {
                        // Try to find the enemy using multiple methods
                        EnemyAI enemy = FindEnemyMultiMethod(hitData);

                        // Find the player who hit the enemy
                        PlayerControllerB playerWhoHit = null;
                        if (hitData.PlayerClientId != 0UL)
                        {
                            foreach (var player in StartOfRound.Instance.allPlayerScripts)
                            {
                                if (player.actualClientId == hitData.PlayerClientId)
                                {
                                    playerWhoHit = player;
                                    break;
                                }
                            }
                        }

                        if (enemy != null && !enemy.isEnemyDead)
                        {
                            // Get the instance ID
                            int instanceId = enemy.GetInstanceID();

                            // Get or create the sanitized name
                            string sanitizedName;
                            if (!cachedSanitizedNames.TryGetValue(instanceId, out sanitizedName))
                            {
                                sanitizedName = Plugin.RemoveInvalidCharacters(enemy.enemyType.enemyName).ToUpper();
                                cachedSanitizedNames[instanceId] = sanitizedName;
                            }

                            // Pass all parameters to ProcessDamageDirectly
                            ProcessDamageDirectly(enemy, hitData.Damage, playerWhoHit, instanceId, sanitizedName);
                        }
                        else
                        {
                            Plugin.Log.LogWarning($"Could not find enemy: {hitData.EnemyName} (NetworkID: {hitData.EnemyNetworkId}, Index: {hitData.EnemyIndex})");
                        }
                    }
                });

                // Create the despawn message (server to clients)
                despawnMessage = LNetworkMessage<int>.Create("ECD_DespawnMessage",
                    // This is the client-side receiver
                    (enemyIndex, clientId) =>
                    {
                        if (!StartOfRound.Instance.IsHost)
                        {
                            // Find the enemy by index and destroy it on clients
                            EnemyAI enemy = FindEnemyByIndex(enemyIndex);
                            if (enemy != null)
                            {
                                Plugin.LogInfo($"[CLIENT] Received despawn message for enemy index {enemyIndex}");
                                GameObject.Destroy(enemy.gameObject);
                            }
                        }
                    });

                Plugin.LogInfo("Network messages created successfully");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error creating network messages: {ex}");
            }
        }

        // Helper method to find enemy by index
        private static EnemyAI FindEnemyByIndex(int index)
        {
            foreach (var enemy in UnityEngine.Object.FindObjectsOfType<EnemyAI>())
            {
                if (enemy.thisEnemyIndex == index)
                {
                    return enemy;
                }
            }
            return null;
        }

        // Use multiple methods to find the enemy across network boundaries
        private static EnemyAI FindEnemyMultiMethod(HitData hitData)
        {
            // Method 1: Try using thisEnemyIndex (most reliable)
            if (hitData.EnemyIndex >= 0)
            {
                foreach (var enemy in RoundManager.Instance.SpawnedEnemies)
                {
                    if (enemy.thisEnemyIndex == hitData.EnemyIndex)
                    {
                        Plugin.LogInfo($"Found enemy by index: {hitData.EnemyIndex}");
                        return enemy;
                    }
                }
            }

            // Method 2: Try using NetworkObjectId if available
            if (hitData.EnemyNetworkId != 0)
            {
                foreach (var enemy in UnityEngine.Object.FindObjectsOfType<EnemyAI>())
                {
                    if (enemy.NetworkObjectId == hitData.EnemyNetworkId)
                    {
                        Plugin.LogInfo($"Found enemy by NetworkObjectId: {hitData.EnemyNetworkId}");
                        return enemy;
                    }
                }
            }

            // Method 3: Try name matching as last resort
            if (!string.IsNullOrEmpty(hitData.EnemyName))
            {
                foreach (var enemy in UnityEngine.Object.FindObjectsOfType<EnemyAI>())
                {
                    if (enemy.enemyType.enemyName == hitData.EnemyName)
                    {
                        Plugin.LogInfo($"Found enemy by name: {hitData.EnemyName}");
                        return enemy;
                    }
                }
            }

            // Log diagnostic information
            Plugin.Log.LogWarning($"Could not find enemy. All enemies in scene:");
            foreach (var enemy in UnityEngine.Object.FindObjectsOfType<EnemyAI>())
            {
                Plugin.Log.LogWarning($"  - {enemy.enemyType.enemyName}, Index: {enemy.thisEnemyIndex}, NetworkId: {enemy.NetworkObjectId}");
            }

            return null;
        }

        // Cache sanitized enemy names
        private static readonly Dictionary<int, string> cachedSanitizedNames = new Dictionary<int, string>();

        // Cache mod enabled status for enemies
        private static readonly Dictionary<string, bool> cachedModEnabled = new Dictionary<string, bool>();

        // Cache compatibility handlers that are used frequently
        private static LethalHandsCompatibility cachedLethalHandsHandler;
        private static HitmarkerCompatibility cachedHitmarkerHandler;
        private static BrutalCompanyMinusCompatibility cachedBrutalCompanyHandler;

        public static void SetupEnemy(EnemyAI enemy)
        {
            if (enemy == null || enemy.enemyType == null) return;

            try
            {
                int instanceId = enemy.GetInstanceID();

                // Fast path for already processed enemies
                if (processedEnemies.TryGetValue(instanceId, out bool processed) && processed)
                {
                    return;
                }

                // Store network ID for lookup - less important, do this once
                if (enemy.NetworkObject != null)
                {
                    enemyNetworkIds[instanceId] = enemy.NetworkObjectId;
                }

                string enemyName = enemy.enemyType.enemyName;

                // Get or create cached sanitized name
                string sanitizedName;
                if (!cachedSanitizedNames.TryGetValue(instanceId, out sanitizedName))
                {
                    sanitizedName = Plugin.RemoveInvalidCharacters(enemyName).ToUpper();
                    cachedSanitizedNames[instanceId] = sanitizedName;
                }

                // Check mod enabled status with caching
                bool modEnabled;
                if (!cachedModEnabled.TryGetValue(sanitizedName, out modEnabled))
                {
                    modEnabled = Plugin.IsModEnabledForEnemy(sanitizedName);
                    cachedModEnabled[sanitizedName] = modEnabled;
                }

                // Skip processing for disabled enemies
                if (!modEnabled)
                {
                    processedEnemies[instanceId] = true;
                    return;
                }

                bool canDamage = Plugin.CanMob(".Unimmortal", sanitizedName);

                if (canDamage)
                {
                    // Create network variable only if not already created
                    if (!enemyHealthVars.ContainsKey(instanceId))
                    {
                        // Get health from config once
                        int configHealth = Plugin.GetMobHealth(sanitizedName, enemy.enemyHP);

                        // Apply bonus health only if needed
                        if (cachedBrutalCompanyHandler != null && cachedBrutalCompanyHandler.IsInstalled)
                        {
                            configHealth = cachedBrutalCompanyHandler.ApplyBonusHp(configHealth);
                        }

                        // Create network variable - more efficient variable naming
                        string varName = $"ECD_H_{enemy.thisEnemyIndex}_{networkVarCounter++}";

                        try
                        {
                            // Create and store in one operation
                            var healthVar = LNetworkVariable<int>.Create(varName, configHealth);
                            healthVar.OnValueChanged += (oldHealth, newHealth) => HandleHealthChange(instanceId, newHealth);
                            enemyHealthVars[instanceId] = healthVar;
                            enemyNetworkVarNames[instanceId] = varName;

                            // Store additional data in single operations
                            enemyMaxHealth[instanceId] = configHealth;
                            immortalEnemies[instanceId] = false;
                        }
                        catch (Exception)
                        {
                            // Simplified retry - only if first attempt failed
                            string retryVarName = $"ECD_H_{enemy.thisEnemyIndex}_{networkVarCounter++}_R";
                            var healthVar = LNetworkVariable<int>.Create(retryVarName, configHealth);
                            healthVar.OnValueChanged += (oldHealth, newHealth) => HandleHealthChange(instanceId, newHealth);
                            enemyHealthVars[instanceId] = healthVar;
                            enemyNetworkVarNames[instanceId] = retryVarName;

                            enemyMaxHealth[instanceId] = configHealth;
                            immortalEnemies[instanceId] = false;

                            Plugin.Log.LogWarning($"Retried network variable creation for {enemyName}");
                        }
                    }

                    // One-time enemy property configurations
                    enemy.enemyType.canDie = true;
                    enemy.enemyType.canBeDestroyed = true;
                    enemy.enemyHP = 999;
                }
                else
                {
                    // Simplified immortal enemy setup
                    enemy.enemyHP = 999;
                    immortalEnemies[instanceId] = true;
                }

                // Mark as processed - do this only once at the end
                processedEnemies[instanceId] = true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error setting up enemy: {ex.Message}");
            }
        }

        // Handle health changes from NetworkVariable updates
        private static void HandleHealthChange(int instanceId, int newHealth)
        {
            // Get the enemy
            EnemyAI enemy = FindEnemyById(instanceId);
            if (enemy == null) return;

            Plugin.LogInfo($"Health changed for enemy {enemy.enemyType.enemyName} (ID: {instanceId}): new health = {newHealth}");

            // If health reached zero, kill the enemy (only on host)
            if (newHealth <= 0 && !enemy.isEnemyDead && StartOfRound.Instance.IsHost)
            {
                // Check for Hitmarker compatibility and notify it of the kill
                var hitmarkerHandler = ModCompatibilityManager.Instance.GetHandler<ModCompatibility.Handlers.HitmarkerCompatibility>("com.github.zehsteam.Hitmarker");
                if (hitmarkerHandler != null && hitmarkerHandler.IsInstalled)
                {
                    // Get the player who caused the last damage
                    PlayerControllerB lastDamageSource = hitmarkerHandler.GetLastDamageSource(instanceId);

                    // Notify the Hitmarker mod
                    hitmarkerHandler.NotifyEnemyKilled(enemy, lastDamageSource);
                }

                // Plugin.Log.LogInfo($"Found enemy with name: {enemy.enemyType.enemyName}");
                KillEnemy(enemy);
            }
        }

        // Find enemy by local instance ID (used internally)
        private static EnemyAI FindEnemyById(int instanceId)
        {
            // Find all enemies in the scene
            var allEnemies = UnityEngine.Object.FindObjectsOfType<EnemyAI>();

            // Look for the one matching our instance ID
            foreach (var enemy in allEnemies)
            {
                if (enemy.GetInstanceID() == instanceId)
                {
                    return enemy;
                }
            }

            return null;
        }

        // This is called from our HitEnemyOnLocalClient patch
        public static void ProcessHit(EnemyAI enemy, int damage, PlayerControllerB playerWhoHit)
        {
            if (enemy == null || enemy.isEnemyDead) return;

            int instanceId = enemy.GetInstanceID();

            // Get cached sanitized name or create it
            string sanitizedName;
            if (!cachedSanitizedNames.TryGetValue(instanceId, out sanitizedName))
            {
                sanitizedName = Plugin.RemoveInvalidCharacters(enemy.enemyType.enemyName).ToUpper();
                cachedSanitizedNames[instanceId] = sanitizedName;
            }

            // Get cached mod enabled status or check it
            bool modEnabled;
            if (!cachedModEnabled.TryGetValue(sanitizedName, out modEnabled))
            {
                modEnabled = Plugin.IsModEnabledForEnemy(sanitizedName);
                cachedModEnabled[sanitizedName] = modEnabled;
            }

            // Fast return for disabled enemies
            if (!modEnabled) return;

            // Fast check for immortal enemies
            if (immortalEnemies.TryGetValue(instanceId, out bool isImmortal) && isImmortal)
            {
                enemy.enemyHP = 999;
                return;
            }

            // Handle LethalHands special case
            if (damage == -22 && cachedLethalHandsHandler != null && cachedLethalHandsHandler.IsInstalled)
            {
                damage = cachedLethalHandsHandler.ConvertPunchForceToDamage(damage);
            }
            else if (damage < 0)
            {
                damage = 0;
            }

            // Skip processing if no actual damage
            if (damage <= 0) return;

            // Process damage either locally or via network
            if (StartOfRound.Instance.IsHost)
            {
                ProcessDamageDirectly(enemy, damage, playerWhoHit, instanceId, sanitizedName);
            }
            else
            {
                // Only create HitData if actually sending
                HitData hitData = new HitData
                {
                    EnemyInstanceId = instanceId,
                    EnemyNetworkId = enemy.NetworkObjectId,
                    EnemyIndex = enemy.thisEnemyIndex,
                    EnemyName = enemy.enemyType.enemyName,
                    Damage = damage,
                    PlayerClientId = playerWhoHit != null ? playerWhoHit.actualClientId : 0UL
                };

                try
                {
                    // Ensure hit message exists - this should rarely happen
                    if (hitMessage == null)
                    {
                        CreateNetworkMessages();
                    }

                    hitMessage.SendServer(hitData);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"Error sending hit message: {ex}");
                }
            }
        }

        private static void ProcessDamageDirectly(EnemyAI enemy, int damage, PlayerControllerB playerWhoHit,
                                         int instanceId, string sanitizedName)
        {
            if (enemy == null || enemy.isEnemyDead) return;

            // Track damage source for hitmarker if installed
            if (playerWhoHit != null && cachedHitmarkerHandler != null && cachedHitmarkerHandler.IsInstalled)
            {
                cachedHitmarkerHandler.TrackDamageSource(instanceId, playerWhoHit);
            }

            // Fast setup check - only do setup if needed (rare case)
            if (!processedEnemies.ContainsKey(instanceId) || !processedEnemies[instanceId])
            {
                SetupEnemy(enemy);
            }

            // Get and update health (most common path)
            if (enemyHealthVars.TryGetValue(instanceId, out var healthVar))
            {
                int currentHealth = healthVar.Value;
                int newHealth = Mathf.Max(0, currentHealth - damage);

                // Only update network if health actually changed
                if (newHealth != currentHealth)
                {
                    healthVar.Value = newHealth;
                }
            }
        }

        // Notify clients to destroy an enemy by index
        public static void NotifyClientsOfDestroy(int enemyIndex)
        {
            // Inform clients to destroy this enemy
            if (despawnMessage != null)
                despawnMessage.SendClients(enemyIndex);
        }

        // Kill an enemy (only called on host)
        private static void KillEnemy(EnemyAI enemy)
        {
            if (enemy == null || enemy.isEnemyDead) return;

            Plugin.LogInfo($"Killing enemy {enemy.enemyType.enemyName}");

            // Check for special handling for problematic enemies with SellBodies
            var sellBodiesHandler = ModCompatibilityManager.Instance.GetHandler<ModCompatibility.Handlers.SellBodiesCompatibility>("Entity378.sellbodies");
            if (sellBodiesHandler != null && sellBodiesHandler.IsInstalled &&
                sellBodiesHandler.IsProblemEnemy(enemy.enemyType.enemyName))
            {
                // Handle special loot spawning for this enemy before it's killed
                sellBodiesHandler.HandleProblemEnemyDeath(enemy);
            }

            // Force ownership back to host before killing
            if (!enemy.IsOwner)
            {
                Plugin.LogInfo($"Attempting to take ownership of {enemy.enemyType.enemyName} to kill it");
                ulong hostId = StartOfRound.Instance.allPlayerScripts[0].actualClientId;
                enemy.ChangeOwnershipOfEnemy(hostId);
            }

            // Use our new LastResortKiller compatibility handler for robust killing
            var lastResortKiller = ModCompatibilityManager.Instance.GetHandler<ModCompatibility.Handlers.LastResortKillerCompatibility>("nwnt.EverythingCanDieAlternative.LastResortKiller");
            if (lastResortKiller != null)
            {
                // Check if this enemy should despawn after death
                bool shouldDespawn = DespawnConfiguration.Instance.ShouldDespawnEnemy(enemy.enemyType.enemyName);

                // Let the handler attempt to kill the enemy using progressive methods
                lastResortKiller.AttemptToKillEnemy(enemy, shouldDespawn);
            }
            else
            {
                // Fallback to the original killing method if handler not found (shouldn't happen)
                enemy.KillEnemyOnOwnerClient(false);

                // For problematic enemies like Spring, try again with destroy=true as fallback
                if (enemy.enemyType.enemyName.Contains("Spring"))
                {
                    Plugin.LogInfo($"Using fallback kill method for {enemy.enemyType.enemyName}");
                    enemy.KillEnemyOnOwnerClient(true);
                }
            }

            // Check if this enemy should despawn after death
            if (DespawnConfiguration.Instance.ShouldDespawnEnemy(enemy.enemyType.enemyName))
            {
                // Start a coroutine to wait for death animation, then despawn
                StartDespawnProcess(enemy);
            }
        }

        // Start the despawn process for a dead enemy
        private static void StartDespawnProcess(EnemyAI enemy)
        {
            if (enemy == null) return;

            int instanceId = enemy.GetInstanceID();

            // Check if we're already despawning this enemy
            if (enemiesInDespawnProcess.ContainsKey(instanceId) && enemiesInDespawnProcess[instanceId])
            {
                return;
            }

            // Mark as in despawn process
            enemiesInDespawnProcess[instanceId] = true;

            Plugin.LogInfo($"Starting despawn process for {enemy.enemyType.enemyName} (Index: {enemy.thisEnemyIndex})");

            // Start a coroutine to check for animation completion and despawn the enemy
            if (StartOfRound.Instance != null)
            {
                StartOfRound.Instance.StartCoroutine(WaitForDeathAnimationAndDespawn(enemy));
            }
        }

        // Simplified despawn coroutine that just waits a fixed time
        private static IEnumerator WaitForDeathAnimationAndDespawn(EnemyAI enemy)
        {
            if (enemy == null) yield break;

            int instanceId = enemy.GetInstanceID();
            int enemyIndex = enemy.thisEnemyIndex;

            // Get the appropriate despawn delay based on installed mods
            float waitTime = 0.5f; // Default delay without any compatibility

            // Check for SellBodies compatibility using the framework
            var sellBodiesHandler = ModCompatibilityManager.Instance.GetHandler<ModCompatibility.Handlers.SellBodiesCompatibility>("Entity378.sellbodies");
            if (sellBodiesHandler != null && sellBodiesHandler.IsInstalled)
            {
                waitTime = sellBodiesHandler.GetDespawnDelay();
                Plugin.LogInfo($"Using SellBodies compatibility despawn delay: {waitTime}s for {enemy.enemyType.enemyName}");
            }

            yield return new WaitForSeconds(waitTime);

            // Only continue if the enemy still exists and is dead
            if (enemy != null && enemy.isEnemyDead)
            {
                // Inform clients to destroy this enemy
                if (despawnMessage != null)
                    despawnMessage.SendClients(enemyIndex);

                // Destroy the enemy on the server
                GameObject.Destroy(enemy.gameObject);
            }

            // Clear tracking flag
            if (enemiesInDespawnProcess.ContainsKey(instanceId))
                enemiesInDespawnProcess.Remove(instanceId);
        }

        public static int GetEnemyHealth(EnemyAI enemy)
        {
            if (enemy == null) return 0;

            int instanceId = enemy.GetInstanceID();
            if (enemyHealthVars.TryGetValue(instanceId, out var healthVar))
            {
                return healthVar.Value;
            }

            return 0;
        }

        public static int GetEnemyMaxHealth(EnemyAI enemy)
        {
            if (enemy == null) return 0;

            int instanceId = enemy.GetInstanceID();
            if (enemyMaxHealth.TryGetValue(instanceId, out int maxHealth))
            {
                return maxHealth;
            }

            return 0;
        }
    }
}
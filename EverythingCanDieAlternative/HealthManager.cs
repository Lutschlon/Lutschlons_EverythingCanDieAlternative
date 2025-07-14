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
        private static readonly Dictionary<int, LNetworkVariable<float>> enemyHealthVars = new Dictionary<int, LNetworkVariable<float>>();

        // Dictionary to store max health values
        private static readonly Dictionary<int, float> enemyMaxHealth = new Dictionary<int, float>();

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

        // Dictionary to cache enemy references for fast lookup
        private static readonly Dictionary<int, EnemyAI> enemyInstanceCache = new Dictionary<int, EnemyAI>();

        // Network message batching to reduce traffic
        private static readonly Dictionary<int, float> pendingDamage = new Dictionary<int, float>();
        private static float lastBatchTime = 0f;
        private const float BATCH_INTERVAL = 0.1f; // Batch every 100ms

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
            public float Damage;
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
            enemyInstanceCache.Clear();
            pendingDamage.Clear();
            lastBatchTime = 0f;

            // Create our hit message IMMEDIATELY at startup - not waiting for network
            CreateNetworkMessages();

            Plugin.LogInfo("Networked Health Manager initialized");
        }

        private static void CreateNetworkMessages()
        {
            try
            {
                // Create the hit message
                hitMessage = LNetworkMessage<HitData>.Create("ECDA_HitMessage",
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
                                // Pass the player information to ProcessDamageDirectly
                                ProcessDamageDirectly(enemy, hitData.Damage, playerWhoHit);
                            }
                            else
                            {
                                Plugin.Log.LogWarning($"Could not find enemy: {hitData.EnemyName} (NetworkID: {hitData.EnemyNetworkId}, Index: {hitData.EnemyIndex})");
                            }
                        }
                    });

                // Create the despawn message (server to clients)
                despawnMessage = LNetworkMessage<int>.Create("ECDA_DespawnMessage",
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

        public static void SetupEnemy(EnemyAI enemy)
        {
            if (enemy == null || enemy.enemyType == null) return;

            try
            {
                int instanceId = enemy.GetInstanceID();

                // Store network object ID for later lookup
                if (enemy.NetworkObject != null)
                {
                    enemyNetworkIds[instanceId] = enemy.NetworkObjectId;
                }

                // Cache the enemy reference for fast lookup
                enemyInstanceCache[instanceId] = enemy;

                // Check if we've already processed this enemy by instance ID
                if (processedEnemies.ContainsKey(instanceId) && processedEnemies[instanceId])
                {
                    Plugin.LogInfo($"Enemy {enemy.enemyType.enemyName} (ID: {instanceId}) already processed, skipping setup");
                    return;
                }

                string enemyName = enemy.enemyType.enemyName;
                string sanitizedName = Plugin.RemoveInvalidCharacters(enemyName).ToUpper();

                // Check if mod is enabled for this enemy from the control configuration
                if (!Plugin.IsModEnabledForEnemy(sanitizedName))
                {
                    Plugin.LogInfo($"Mod disabled for enemy {enemyName} via config, using vanilla behavior");
                    processedEnemies[instanceId] = true; // Mark as processed to avoid re-checking
                    return;
                }

                bool canDamage = Plugin.CanMob(".Unimmortal", sanitizedName);

                // MODIFIED: Handle both damageable and immortal-but-enabled enemies differently
                if (canDamage)
                {
                    // Get configured health
                    float configHealth = Plugin.GetMobHealth(sanitizedName, enemy.enemyHP);

                    // Apply bonus health from BrutalCompanyMinus if installed
                    var brutalCompanyHandler = ModCompatibilityManager.Instance.GetHandler<ModCompatibility.Handlers.BrutalCompanyMinusCompatibility>("SoftDiamond.BrutalCompanyMinusExtraReborn");
                    if (brutalCompanyHandler != null && brutalCompanyHandler.IsInstalled)
                    {
                        configHealth = brutalCompanyHandler.ApplyBonusHp(configHealth);
                    }

                    // Create a unique identifier for this enemy's health
                    // Add a counter to ensure uniqueness over multiple moons
                    string varName = $"ECDA_Health_{enemy.thisEnemyIndex}_{networkVarCounter++}";

                    // Store the variable name for this instance ID
                    enemyNetworkVarNames[instanceId] = varName;

                    Plugin.LogInfo($"Creating network variable {varName} for enemy {enemyName} (ID: {instanceId})");

                    // Create the health variable
                    LNetworkVariable<float> healthVar;
                    if (!enemyHealthVars.ContainsKey(instanceId))
                    {
                        try
                        {
                            // Create a new NetworkVariable
                            healthVar = LNetworkVariable<float>.Create(varName, configHealth);

                            // Subscribe to value changes
                            healthVar.OnValueChanged += (oldHealth, newHealth) => HandleHealthChange(instanceId, newHealth);

                            enemyHealthVars[instanceId] = healthVar;
                        }
                        catch (Exception ex)
                        {
                            Plugin.Log.LogError($"Failed to create network variable {varName}: {ex.Message}");

                            // Try with a different name if there was a duplicate
                            varName = $"ECDA_Health_{enemy.thisEnemyIndex}_{networkVarCounter++}_Retry";
                            Plugin.LogInfo($"Retrying with new variable name: {varName}");

                            // Store the new variable name
                            enemyNetworkVarNames[instanceId] = varName;

                            // Create the variable with the new name
                            healthVar = LNetworkVariable<float>.Create(varName, configHealth);
                            healthVar.OnValueChanged += (oldHealth, newHealth) => HandleHealthChange(instanceId, newHealth);
                            enemyHealthVars[instanceId] = healthVar;
                        }
                    }
                    else
                    {
                        healthVar = enemyHealthVars[instanceId];
                        Plugin.LogInfo($"Using existing health variable for enemy {enemyName} (ID: {instanceId})");
                    }

                    // Store max health
                    enemyMaxHealth[instanceId] = configHealth;

                    // Make enemy killable in the game system
                    enemy.enemyType.canDie = true;
                    enemy.enemyType.canBeDestroyed = true;

                    // Set high HP value in the original system so our networked system controls when it dies
                    enemy.enemyHP = 999;

                    // Mark as processed
                    processedEnemies[instanceId] = true;

                    // Not immortal
                    immortalEnemies[instanceId] = false;

                    Plugin.LogInfo($"Setup enemy {enemyName} (ID: {instanceId}, NetID: {enemy.NetworkObjectId}, Index: {enemy.thisEnemyIndex}) with {configHealth} networked health");
                }
                else
                {
                    // Handle immortal-but-enabled enemies
                    Plugin.LogInfo($"Enemy {enemyName} is configured as immortal (Unimmortal=false, Enabled=true)");

                    // Set high HP value to make them effectively immortal
                    enemy.enemyHP = 999;

                    // Check if we should protect immortal enemies from insta-kill effects
                    if (Plugin.ProtectImmortalEnemiesFromInstaKill.Value)
                    {
                        // Set canDie to false to protect from insta-kill effects like spike traps
                        enemy.enemyType.canDie = false;
                        Plugin.LogInfo($"Protected immortal enemy {enemyName} from insta-kill effects (canDie = false)");
                    }
                    else
                    {
                        // Keep canDie as true, allowing insta-kill effects
                        enemy.enemyType.canDie = true;
                        Plugin.LogInfo($"Immortal enemy {enemyName} can still be killed by insta-kill effects (canDie = true)");
                    }

                    // Mark as immortal for hit processing
                    immortalEnemies[instanceId] = true;

                    // Mark as processed
                    processedEnemies[instanceId] = true;

                    Plugin.LogInfo($"Set enemy {enemyName} (ID: {instanceId}) to be immortal with 999 HP");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error setting up enemy: {ex.Message}");
                Plugin.Log.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        // Handle health changes from NetworkVariable updates
        private static void HandleHealthChange(int instanceId, float newHealth)
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
            // Try cache first for O(1) lookup
            if (enemyInstanceCache.TryGetValue(instanceId, out EnemyAI cached))
            {
                // Validate the cached reference is still valid
                if (cached != null)
                    return cached;
                else
                    enemyInstanceCache.Remove(instanceId);
            }
            
            // Fallback to searching (and update cache)
            var allEnemies = UnityEngine.Object.FindObjectsOfType<EnemyAI>();
            foreach (var enemy in allEnemies)
            {
                if (enemy.GetInstanceID() == instanceId)
                {
                    enemyInstanceCache[instanceId] = enemy;
                    return enemy;
                }
            }
            
            return null;
        }

        // This is called from our HitEnemyOnLocalClient patch
        public static void ProcessHit(EnemyAI enemy, float damage, PlayerControllerB playerWhoHit)
        {
            if (enemy == null || enemy.isEnemyDead) return;

            int instanceId = enemy.GetInstanceID();

            // Check if mod is enabled for this enemy from the control configuration
            string sanitizedName = Plugin.RemoveInvalidCharacters(enemy.enemyType.enemyName).ToUpper();
            if (!Plugin.IsModEnabledForEnemy(sanitizedName))
            {
                Plugin.LogInfo($"Mod disabled for enemy {enemy.enemyType.enemyName}, not processing hit");
                return; // Skip processing hit for disabled enemies
            }

            // NEW CODE: Check if this is an immortal enemy (Enabled=true, Unimmortal=false)
            if (immortalEnemies.TryGetValue(instanceId, out bool isImmortal) && isImmortal)
            {
                // For immortal enemies, just refresh their HP to 999 and don't process damage
                enemy.enemyHP = 999;
                Plugin.LogInfo($"Refreshed immortal enemy {enemy.enemyType.enemyName} HP to 999");
                return;
            }

            // Check for LethalHands compatibility to handle special punch damage (-22)
            var lethalHandsHandler = ModCompatibilityManager.Instance.GetHandler<ModCompatibility.Handlers.LethalHandsCompatibility>("SlapitNow.LethalHands");
            if (lethalHandsHandler != null && lethalHandsHandler.IsInstalled && damage == -22)
            {
                damage = lethalHandsHandler.ConvertPunchForceToDamage(damage);
                Plugin.LogInfo($"Converted LethalHands punch to damage: {damage}");
            }
            else if (damage < 0)
            {
                // Prevent negative damage from other sources
                Plugin.Log.LogWarning($"Received negative damage value: {damage}, setting to 0");
                damage = 0;
            }

            // Skip processing if damage is zero (prevents wasting network traffic)
            if (damage <= 0) return;

            // If we're the host, process damage directly
            if (StartOfRound.Instance.IsHost)
            {
                Plugin.LogInfo($"Processing hit locally as host: Enemy {enemy.enemyType.enemyName}, Damage {damage}");
                ProcessDamageDirectly(enemy, damage, playerWhoHit);
            }
            else
            {
                // Batch damage for non-host clients
                if (!pendingDamage.ContainsKey(instanceId))
                    pendingDamage[instanceId] = 0f;
                
                pendingDamage[instanceId] += damage;
                
                // Track the player who hit
                var hitmarkerHandler = ModCompatibilityManager.Instance.GetHandler<ModCompatibility.Handlers.HitmarkerCompatibility>("com.github.zehsteam.Hitmarker");
                if (hitmarkerHandler != null && hitmarkerHandler.IsInstalled && playerWhoHit != null)
                {
                    hitmarkerHandler.TrackDamageSource(instanceId, playerWhoHit);
                }
                
                // Check if we should send batch
                if (Time.time - lastBatchTime >= BATCH_INTERVAL)
                {
                    SendDamageBatch();
                }
            }
        }

        private static void SendDamageBatch()
        {
            if (pendingDamage.Count == 0) return;
            
            foreach (var kvp in pendingDamage)
            {
                var enemy = FindEnemyById(kvp.Key);
                if (enemy != null && !enemy.isEnemyDead)
                {
                    HitData hitData = new HitData
                    {
                        EnemyInstanceId = kvp.Key,
                        EnemyNetworkId = enemy.NetworkObjectId,
                        EnemyIndex = enemy.thisEnemyIndex,
                        EnemyName = enemy.enemyType.enemyName,
                        Damage = kvp.Value,
                        PlayerClientId = StartOfRound.Instance.localPlayerController?.actualClientId ?? 0UL
                    };
                    
                    try
                    {
                        if (hitMessage == null)
                        {
                            Plugin.Log.LogWarning("Hit message is null, recreating it");
                            CreateNetworkMessages();
                        }
                        
                        hitMessage.SendServer(hitData);
                        Plugin.LogInfo($"Sent batched damage: {kvp.Value} to {enemy.enemyType.enemyName}");
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogError($"Error sending batched hit message: {ex}");
                    }
                }
            }
            
            pendingDamage.Clear();
            lastBatchTime = Time.time;
        }

        // Process damage directly (only called on host)
        private static void ProcessDamageDirectly(EnemyAI enemy, float damage, PlayerControllerB playerWhoHit = null)
        {
            if (enemy == null || enemy.isEnemyDead) return;

            int instanceId = enemy.GetInstanceID();

            // Check if mod is enabled for this enemy from the control configuration
            string sanitizedName = Plugin.RemoveInvalidCharacters(enemy.enemyType.enemyName).ToUpper();
            if (!Plugin.IsModEnabledForEnemy(sanitizedName))
            {
                Plugin.LogInfo($"Mod disabled for enemy {enemy.enemyType.enemyName}, not processing damage");
                return; // Skip processing damage for disabled enemies
            }

            // Check for Hitmarker compatibility and track the player who caused this damage
            if (playerWhoHit != null)
            {
                var hitmarkerHandler = ModCompatibilityManager.Instance.GetHandler<ModCompatibility.Handlers.HitmarkerCompatibility>("com.github.zehsteam.Hitmarker");
                if (hitmarkerHandler != null && hitmarkerHandler.IsInstalled)
                {
                    hitmarkerHandler.TrackDamageSource(instanceId, playerWhoHit);
                }
            }

            // NEW CODE: Check if this is an immortal enemy (Enabled=true, Unimmortal=false)
            if (immortalEnemies.TryGetValue(instanceId, out bool isImmortal) && isImmortal)
            {
                // For immortal enemies, just refresh their HP to 999 and don't process damage
                enemy.enemyHP = 999;
                Plugin.LogInfo($"Refreshed immortal enemy {enemy.enemyType.enemyName} HP to 999");
                return;
            }

            // Ensure enemy is set up
            if (!processedEnemies.ContainsKey(instanceId) || !processedEnemies[instanceId])
            {
                SetupEnemy(enemy);
            }

            // Get the health variable
            if (enemyHealthVars.TryGetValue(instanceId, out var healthVar))
            {
                // Calculate new health
                float currentHealth = healthVar.Value;
                float newHealth = Mathf.Max(0f, currentHealth - damage);

                Plugin.LogInfo($"Enemy {enemy.enemyType.enemyName} damaged for {damage}: {currentHealth} -> {newHealth}");

                // Update the NetworkVariable (this will sync to all clients)
                healthVar.Value = newHealth;
            }
            else
            {
                Plugin.Log.LogWarning($"No health variable found for enemy {enemy.enemyType.enemyName} (ID: {instanceId})");
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

        public static float GetEnemyHealth(EnemyAI enemy)
        {
            if (enemy == null) return 0;

            int instanceId = enemy.GetInstanceID();
            if (enemyHealthVars.TryGetValue(instanceId, out var healthVar))
            {
                return healthVar.Value;
            }

            return 0;
        }

        public static float GetEnemyMaxHealth(EnemyAI enemy)
        {
            if (enemy == null) return 0;

            int instanceId = enemy.GetInstanceID();
            if (enemyMaxHealth.TryGetValue(instanceId, out float maxHealth))
            {
                return maxHealth;
            }

            return 0;
        }

        /// <summary>
        /// Check if an enemy is being tracked by our health system
        /// </summary>
        public static bool IsEnemyTracked(EnemyAI enemy)
        {
            if (enemy == null) return false;
            int instanceId = enemy.GetInstanceID();
            return enemyHealthVars.ContainsKey(instanceId) || immortalEnemies.ContainsKey(instanceId);
        }

        /// Clean up tracking data for an externally killed enemy
        public static void CleanupExternallyKilledEnemy(EnemyAI enemy)
        {
            if (enemy == null) return;

            int instanceId = enemy.GetInstanceID();

            // Clean up our tracking dictionaries
            if (enemyHealthVars.ContainsKey(instanceId))
                enemyHealthVars.Remove(instanceId);
            if (enemyMaxHealth.ContainsKey(instanceId))
                enemyMaxHealth.Remove(instanceId);
            if (processedEnemies.ContainsKey(instanceId))
                processedEnemies.Remove(instanceId);
            if (enemyNetworkIds.ContainsKey(instanceId))
                enemyNetworkIds.Remove(instanceId);
            if (enemyNetworkVarNames.ContainsKey(instanceId))
                enemyNetworkVarNames.Remove(instanceId);
            if (immortalEnemies.ContainsKey(instanceId))
                immortalEnemies.Remove(instanceId);
            enemyInstanceCache.Remove(instanceId);

            Plugin.LogInfo($"Cleaned up tracking data for externally killed enemy {enemy.enemyType.enemyName}");
        }
    }
}
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
using EverythingCanDieAlternative.UI;
using static EverythingCanDieAlternative.Plugin;

namespace EverythingCanDieAlternative
{
    public static class HealthManager
    {
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

        // Reverse lookup: NetworkObjectId -> EnemyAI, used by client-side message handlers
        private static readonly Dictionary<ulong, EnemyAI> enemyByNetworkId = new Dictionary<ulong, EnemyAI>();

        // Sanitized + uppercase enemy name keyed by instanceId — avoids re-sanitizing on every hit
        private static readonly Dictionary<int, string> sanitizedEnemyNames = new Dictionary<int, string>();

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

        private static bool networkMessagesCreated = false;

        public static void Initialize()
        {
            // Dispose old network variables so their identifiers are freed for reuse.
            // NetworkObjectIds restart in a new lobby, so stale variables from a previous
            // lobby would otherwise be picked up by Connect with outdated values.
            foreach (var oldHealthVar in enemyHealthVars.Values)
            {
                try { oldHealthVar?.Dispose(); }
                catch (Exception ex) { Plugin.Log.LogWarning($"Error disposing health variable: {ex.Message}"); }
            }

            // Clear all dictionaries
            enemyHealthVars.Clear();
            enemyMaxHealth.Clear();
            processedEnemies.Clear();
            enemyNetworkIds.Clear();
            enemyNetworkVarNames.Clear();
            enemiesInDespawnProcess.Clear();
            immortalEnemies.Clear();
            enemyInstanceCache.Clear();
            enemyByNetworkId.Clear();
            sanitizedEnemyNames.Clear();
            pendingDamage.Clear();
            lastBatchTime = 0f;

            // Create our hit message IMMEDIATELY at startup - not waiting for network
            CreateNetworkMessages();

            Plugin.LogInfo("Networked Health Manager initialized");
        }

        private static void CreateNetworkMessages()
        {
            // Check if messages already exist
            if (networkMessagesCreated)
            {
                Plugin.LogInfo("Network messages already created, skipping recreation");
                return;
            }

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

                networkMessagesCreated = true;
                Plugin.LogInfo("Network messages created successfully");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error creating network messages: {ex}");
            }
        }

        // Get the sanitized + uppercase name for an enemy, caching it per instance to avoid re-sanitizing
        public static string GetSanitizedName(EnemyAI enemy)
        {
            if (enemy == null || enemy.enemyType == null) return string.Empty;
            int id = enemy.GetInstanceID();
            if (sanitizedEnemyNames.TryGetValue(id, out string cached))
                return cached;
            string sanitized = Plugin.RemoveInvalidCharacters(enemy.enemyType.enemyName).ToUpper();
            sanitizedEnemyNames[id] = sanitized;
            return sanitized;
        }

        // Helper method to find enemy by index — prefers RoundManager.SpawnedEnemies over a full scene scan
        private static EnemyAI FindEnemyByIndex(int index)
        {
            var roundManager = RoundManager.Instance;
            if (roundManager?.SpawnedEnemies != null)
            {
                foreach (var enemy in roundManager.SpawnedEnemies)
                {
                    if (enemy != null && enemy.thisEnemyIndex == index)
                    {
                        return enemy;
                    }
                }
            }
            return null;
        }

        // Use multiple methods to find the enemy across network boundaries
        private static EnemyAI FindEnemyMultiMethod(HitData hitData)
        {
            // Method 1: NetworkObjectId via O(1) dict lookup (populated in SetupEnemy)
            if (hitData.EnemyNetworkId != 0 &&
                enemyByNetworkId.TryGetValue(hitData.EnemyNetworkId, out var byNet) &&
                byNet != null)
            {
                Plugin.LogInfo($"Found enemy by NetworkObjectId (cache): {hitData.EnemyNetworkId}");
                return byNet;
            }

            // Method 2: thisEnemyIndex via RoundManager.SpawnedEnemies (small, already-tracked list)
            if (hitData.EnemyIndex >= 0)
            {
                var byIndex = FindEnemyByIndex(hitData.EnemyIndex);
                if (byIndex != null)
                {
                    Plugin.LogInfo($"Found enemy by index: {hitData.EnemyIndex}");
                    return byIndex;
                }
            }

            // Method 3: name fallback (still uses SpawnedEnemies — no full scene scan)
            if (!string.IsNullOrEmpty(hitData.EnemyName))
            {
                var roundManager = RoundManager.Instance;
                if (roundManager?.SpawnedEnemies != null)
                {
                    foreach (var enemy in roundManager.SpawnedEnemies)
                    {
                        if (enemy != null && enemy.enemyType != null && enemy.enemyType.enemyName == hitData.EnemyName)
                        {
                            Plugin.LogInfo($"Found enemy by name: {hitData.EnemyName}");
                            return enemy;
                        }
                    }
                }
            }

            // Log diagnostic information from the tracked spawn list rather than the entire scene
            Plugin.Log.LogWarning($"Could not find enemy. SpawnedEnemies in round:");
            var rm = RoundManager.Instance;
            if (rm?.SpawnedEnemies != null)
            {
                foreach (var enemy in rm.SpawnedEnemies)
                {
                    if (enemy != null)
                        Plugin.Log.LogWarning($"  - {enemy.enemyType?.enemyName}, Index: {enemy.thisEnemyIndex}, NetworkId: {enemy.NetworkObjectId}");
                }
            }

            return null;
        }

        public static void SetupEnemy(EnemyAI enemy)
        {
            if (enemy == null || enemy.enemyType == null) return;

            try
            {
                int instanceId = enemy.GetInstanceID();

                // Cache the enemy reference for fast lookup
                enemyInstanceCache[instanceId] = enemy;

                // Store network object ID for later lookup
                if (enemy.NetworkObject != null)
                {
                    enemyNetworkIds[instanceId] = enemy.NetworkObjectId;
                    enemyByNetworkId[enemy.NetworkObjectId] = enemy;
                }

                // Check if we've already processed this enemy by instance ID
                if (processedEnemies.TryGetValue(instanceId, out bool alreadyProcessed) && alreadyProcessed)
                {
                    Plugin.LogInfo($"Enemy {enemy.enemyType.enemyName} (ID: {instanceId}) already processed, skipping setup");
                    return;
                }

                string enemyName = enemy.enemyType.enemyName;
                string sanitizedName = GetSanitizedName(enemy);

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
                    var brutalCompanyHandler = ModCompatibilityManager.Instance.BrutalCompanyMinus;
                    if (brutalCompanyHandler != null && brutalCompanyHandler.IsInstalled)
                    {
                        configHealth = brutalCompanyHandler.ApplyBonusHp(configHealth);
                    }

                    // Deterministic identifier for this enemy's health variable.
                    // LethalNetworkAPI links variables across machines purely by identifier,
                    // so host and clients MUST derive the exact same name. NetworkObjectId is
                    // assigned by Netcode and identical on every machine. (A machine-local
                    // counter previously desynced the names between host and clients, which
                    // left client-side health values stuck at their initial value.)
                    string varName;
                    if (enemy.NetworkObject != null && enemy.NetworkObjectId != 0)
                    {
                        varName = $"ECDA_Health_{enemy.NetworkObjectId}";
                    }
                    else
                    {
                        // Fallback: thisEnemyIndex is also assigned by the game and synced
                        varName = $"ECDA_Health_Index_{enemy.thisEnemyIndex}";
                        Plugin.Log.LogWarning($"Enemy {enemyName} has no NetworkObject, using fallback health variable name {varName}");
                    }

                    // Store the variable name for this instance ID
                    enemyNetworkVarNames[instanceId] = varName;

                    Plugin.LogInfo($"Connecting network variable {varName} for enemy {enemyName} (ID: {instanceId})");

                    // Connect the health variable
                    LNetworkVariable<float> healthVar;
                    if (!enemyHealthVars.TryGetValue(instanceId, out healthVar))
                    {
                        // Connect instead of Create: creates the variable if it doesn't exist
                        // yet, otherwise attaches to the existing one. Host and clients can
                        // therefore initialize in any order without duplicate-identifier errors.
                        healthVar = LNetworkVariable<float>.Connect(varName, configHealth,
                            onValueChanged: (oldHealth, newHealth) => HandleHealthChange(instanceId, newHealth));

                        enemyHealthVars[instanceId] = healthVar;
                    }
                    else
                    {
                        // healthVar already populated by TryGetValue above
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

                    // Attach the floating health bar UI (visibility is gated by config at runtime)
                    try { EnemyHealthBarUI.Attach(enemy); }
                    catch (Exception uiEx) { Plugin.Log.LogWarning($"Failed to attach health bar UI to {enemyName}: {uiEx.Message}"); }

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

        // Handle health changes from NetworkVariable updates.
        // NOTE: on clients this callback does not reliably fire for mid-session variables.
        // The health bar reads GetEnemyHealth() directly instead of relying on this.
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
                var hitmarkerHandler = ModCompatibilityManager.Instance.Hitmarker;
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
            string sanitizedName = GetSanitizedName(enemy);
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
            var lethalHandsHandler = ModCompatibilityManager.Instance.LethalHands;
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
                pendingDamage.TryGetValue(instanceId, out float current);
                pendingDamage[instanceId] = current + damage;
                
                // Track the player who hit
                var hitmarkerHandler = ModCompatibilityManager.Instance.Hitmarker;
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
                            Plugin.LogWarning("Hit message is null, recreating it");
                            CreateNetworkMessages();
                        }
                        
                        hitMessage.SendServer(hitData);
                        Plugin.LogInfo($"Sent batched damage: {kvp.Value} to {enemy.enemyType.enemyName}");
                    }
                    catch (Exception ex)
                    {
                        Plugin.LogError($"Error sending batched hit message: {ex}");
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
            string sanitizedName = GetSanitizedName(enemy);
            if (!Plugin.IsModEnabledForEnemy(sanitizedName))
            {
                Plugin.LogInfo($"Mod disabled for enemy {enemy.enemyType.enemyName}, not processing damage");
                return; // Skip processing damage for disabled enemies
            }

            // Check for Hitmarker compatibility and track the player who caused this damage
            if (playerWhoHit != null)
            {
                var hitmarkerHandler = ModCompatibilityManager.Instance.Hitmarker;
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
            if (!processedEnemies.TryGetValue(instanceId, out bool isProcessed) || !isProcessed)
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
            var sellBodiesHandler = ModCompatibilityManager.Instance.SellBodies;
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
            var lastResortKiller = ModCompatibilityManager.Instance.LastResortKiller;
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
            bool willDespawn = DespawnConfiguration.Instance.ShouldDespawnEnemy(enemy.enemyType.enemyName);

            // Silence the enemy regardless of whether it despawns. Some enemies (notably the
            // Ghost Girl) drive audio sources that are NOT children of their GameObject, so
            // destroying the enemy does not stop those sounds.
            if (Plugin.MuteDeadEnemies.Value)
            {
                Plugin.LogInfo($"Starting audio fade for {enemy.enemyType.enemyName}");
                SilenceDeadEnemy(enemy);
            }

            if (willDespawn)
            {
                StartDespawnProcess(enemy);
            }
        }

        // Collect every AudioSource belonging to an enemy.
        //
        // GetComponentsInChildren only finds sources parented under the enemy. Some enemies
        // reference audio sources that live elsewhere in the scene — the Ghost Girl's
        // heartbeatMusic is the player's heartbeat and is not part of her hierarchy — so those
        // are added explicitly from their typed fields.
        private static AudioSource[] CollectEnemyAudioSources(EnemyAI enemy)
        {
            var sources = new List<AudioSource>();

            if (enemy == null || enemy.gameObject == null) return sources.ToArray();

            // Standard case: everything parented under the enemy
            sources.AddRange(enemy.GetComponentsInChildren<AudioSource>(includeInactive: true));

            // Generic EnemyAI audio fields — these usually are children, but not always
            AddAudioSource(sources, enemy.creatureVoice);
            AddAudioSource(sources, enemy.creatureSFX);

            // Ghost Girl: heartbeatMusic is a separate AudioSource outside her hierarchy.
            // Her Update() also stops lerping its volume once isEnemyDead is true, which
            // freezes the heartbeat at whatever volume it had when she died.
            // (The muffled mixer snapshot is reset separately in Patches.KillEnemyPostfix,
            // since that is global audio state and must be reset regardless of this setting.)
            if (enemy is DressGirlAI dressGirl)
            {
                AddAudioSource(sources, dressGirl.heartbeatMusic);
            }

            return sources.ToArray();
        }

        // Add an audio source if it exists and is not already in the list
        private static void AddAudioSource(List<AudioSource> list, AudioSource source)
        {
            if (source != null && !list.Contains(source))
                list.Add(source);
        }

        public static void SilenceDeadEnemy(EnemyAI enemy)
        {
            if (enemy == null) return;
            try
            {
                if (StartOfRound.Instance == null)
                {
                    Plugin.Log.LogError("SilenceDeadEnemy: StartOfRound.Instance is null");
                    return;
                }

                // Collect the sources NOW, while the enemy still exists. If the enemy is about
                // to despawn, its GameObject may be destroyed before the fade starts — but
                // external sources (e.g. the Ghost Girl's heartbeat) survive that destruction
                // and still need fading.
                AudioSource[] sources = CollectEnemyAudioSources(enemy);
                if (sources.Length == 0)
                {
                    Plugin.LogInfo($"No audio sources found for {enemy.enemyType?.enemyName}");
                    return;
                }

                StartOfRound.Instance.StartCoroutine(DelayedSilence(sources, enemy.enemyType?.enemyName));
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to start DelayedSilence coroutine: {ex.Message}");
            }
        }

        private static IEnumerator DelayedSilence(AudioSource[] sources, string enemyName)
        {
            Plugin.LogInfo($"DelayedSilence started for {enemyName} ({sources.Length} audio sources)");

            // Brief delay so death sounds can play before the fade begins
            yield return new WaitForSeconds(0.5f);

            yield return FadeOutEnemyAudio(sources, 1f);
        }

        private static IEnumerator FadeOutEnemyAudio(AudioSource[] sources, float duration)
        {
            // Snapshot starting volumes so each source fades proportionally
            float[] startVolumes = new float[sources.Length];
            for (int i = 0; i < sources.Length; i++)
                startVolumes[i] = sources[i] != null ? sources[i].volume : 0f;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                for (int i = 0; i < sources.Length; i++)
                {
                    if (sources[i] != null)
                        sources[i].volume = Mathf.Lerp(startVolumes[i], 0f, t);
                }
                yield return null;
            }

            // Guarantee silence and stop playback after the fade completes
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] != null)
                {
                    sources[i].volume = 0f;
                    sources[i].Stop();
                }
            }
        }
        // Start the despawn process for a dead enemy
        private static void StartDespawnProcess(EnemyAI enemy)
        {
            if (enemy == null) return;

            int instanceId = enemy.GetInstanceID();

            // Check if we're already despawning this enemy
            if (enemiesInDespawnProcess.TryGetValue(instanceId, out bool inProcess) && inProcess)
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
            var sellBodiesHandler = ModCompatibilityManager.Instance.SellBodies;
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

            // Clear tracking flag (Remove returns false if missing — no need to pre-check)
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

        // Check if an enemy is being tracked by our health system
        public static bool IsEnemyTracked(EnemyAI enemy)
        {
            if (enemy == null) return false;
            int instanceId = enemy.GetInstanceID();
            return enemyHealthVars.ContainsKey(instanceId) || immortalEnemies.ContainsKey(instanceId);
        }

        public static void DirectHealthChange(EnemyAI enemy, int damage, PlayerControllerB playerWhoHit = null)
        {
            if (enemy == null || enemy.isEnemyDead || damage <= 0) return;

            int instanceId = enemy.GetInstanceID();
            
            // Check if this enemy has a network health variable
            if (!enemyHealthVars.ContainsKey(instanceId))
            {
                Plugin.LogInfo($"No network health variable found for {enemy.enemyType.enemyName}, probably set to enabled = false");
                return;
            }

            // Track damage source for hitmarker compatibility
            var hitmarkerHandler = ModCompatibilityManager.Instance.Hitmarker;
            if (hitmarkerHandler != null && hitmarkerHandler.IsInstalled && playerWhoHit != null)
            {
                hitmarkerHandler.TrackDamageSource(instanceId, playerWhoHit);
            }

            // Get current health and apply damage
            var healthVar = enemyHealthVars[instanceId];
            float currentHealth = healthVar.Value;
            float newHealth = Mathf.Max(0, currentHealth - damage);
            
            Plugin.LogInfo($"DirectHealthChange: {enemy.enemyType.enemyName} damaged for {damage}: {currentHealth} -> {newHealth}");
            
            // Directly set the network health value - this will automatically sync to all clients
            healthVar.Value = newHealth;
            
            // The OnValueChanged callback will handle death checking and other logic
        }
        // Clean up tracking data for an externally killed enemy
        public static void CleanupExternallyKilledEnemy(EnemyAI enemy)
        {
            if (enemy == null) return;

            int instanceId = enemy.GetInstanceID();

            // Clean up our tracking dictionaries — Dictionary.Remove is a no-op if missing
            if (enemyHealthVars.TryGetValue(instanceId, out var healthVarToDispose))
            {
                try { healthVarToDispose.Dispose(); }
                catch (Exception ex) { Plugin.Log.LogWarning($"Error disposing health variable: {ex.Message}"); }
            }
            enemyHealthVars.Remove(instanceId);
            enemyMaxHealth.Remove(instanceId);
            processedEnemies.Remove(instanceId);
            if (enemyNetworkIds.TryGetValue(instanceId, out ulong netId))
            {
                enemyNetworkIds.Remove(instanceId);
                enemyByNetworkId.Remove(netId);
            }
            enemyNetworkVarNames.Remove(instanceId);
            immortalEnemies.Remove(instanceId);
            enemyInstanceCache.Remove(instanceId);
            sanitizedEnemyNames.Remove(instanceId);

            Plugin.LogInfo($"Cleaned up tracking data for externally killed enemy {enemy.enemyType.enemyName}");
        }
    }
}
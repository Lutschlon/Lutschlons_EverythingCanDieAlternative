using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using LethalNetworkAPI;

namespace EverythingCanDieAlternative
{
    public static class NetworkedHealthManager
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

            public override string ToString()
            {
                return $"HitData(EnemyId={EnemyInstanceId}, NetworkId={EnemyNetworkId}, Index={EnemyIndex}, Name={EnemyName}, Damage={Damage})";
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

            // Reset the counter
            networkVarCounter = 0;

            // Create our hit message IMMEDIATELY at startup - not waiting for network
            CreateNetworkMessages();

            Plugin.Log.LogInfo("Networked Health Manager initialized");
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
                        Plugin.Log.LogInfo($"[HOST] Received hit message from client {clientId}: {hitData}");
                        if (StartOfRound.Instance.IsHost)
                        {
                            // Try to find the enemy using multiple methods
                            EnemyAI enemy = FindEnemyMultiMethod(hitData);

                            if (enemy != null && !enemy.isEnemyDead)
                            {
                                ProcessDamageDirectly(enemy, hitData.Damage);
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
                                Plugin.Log.LogInfo($"[CLIENT] Received despawn message for enemy index {enemyIndex}");
                                GameObject.Destroy(enemy.gameObject);
                            }
                        }
                    });

                Plugin.Log.LogInfo("Network messages created successfully");
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
                        Plugin.Log.LogInfo($"Found enemy by index: {hitData.EnemyIndex}");
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
                        Plugin.Log.LogInfo($"Found enemy by NetworkObjectId: {hitData.EnemyNetworkId}");
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
                        Plugin.Log.LogInfo($"Found enemy by name: {hitData.EnemyName}");
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

                // Check if we've already processed this enemy by instance ID
                if (processedEnemies.ContainsKey(instanceId) && processedEnemies[instanceId])
                {
                    Plugin.Log.LogInfo($"Enemy {enemy.enemyType.enemyName} (ID: {instanceId}) already processed, skipping setup");
                    return;
                }

                string enemyName = enemy.enemyType.enemyName;
                string sanitizedName = Plugin.RemoveInvalidCharacters(enemyName).ToUpper();
                bool canDamage = Plugin.CanMob(".Unimmortal", sanitizedName);

                if (canDamage)
                {
                    // Get configured health
                    int configHealth = Plugin.GetMobHealth(sanitizedName, enemy.enemyHP);

                    // Create a unique identifier for this enemy's health
                    // Add a counter to ensure uniqueness over multiple moons
                    string varName = $"ECD_Health_{enemy.thisEnemyIndex}_{networkVarCounter++}";

                    // Store the variable name for this instance ID
                    enemyNetworkVarNames[instanceId] = varName;

                    Plugin.Log.LogInfo($"Creating network variable {varName} for enemy {enemyName} (ID: {instanceId})");

                    // Create the health variable
                    LNetworkVariable<int> healthVar;
                    if (!enemyHealthVars.ContainsKey(instanceId))
                    {
                        try
                        {
                            // Create a new NetworkVariable
                            healthVar = LNetworkVariable<int>.Create(varName, configHealth);

                            // Subscribe to value changes
                            healthVar.OnValueChanged += (oldHealth, newHealth) => HandleHealthChange(instanceId, newHealth);

                            enemyHealthVars[instanceId] = healthVar;
                        }
                        catch (Exception ex)
                        {
                            Plugin.Log.LogError($"Failed to create network variable {varName}: {ex.Message}");

                            // Try with a different name if there was a duplicate
                            varName = $"ECD_Health_{enemy.thisEnemyIndex}_{networkVarCounter++}_Retry";
                            Plugin.Log.LogInfo($"Retrying with new variable name: {varName}");

                            // Store the new variable name
                            enemyNetworkVarNames[instanceId] = varName;

                            // Create the variable with the new name
                            healthVar = LNetworkVariable<int>.Create(varName, configHealth);
                            healthVar.OnValueChanged += (oldHealth, newHealth) => HandleHealthChange(instanceId, newHealth);
                            enemyHealthVars[instanceId] = healthVar;
                        }
                    }
                    else
                    {
                        healthVar = enemyHealthVars[instanceId];
                        Plugin.Log.LogInfo($"Using existing health variable for enemy {enemyName} (ID: {instanceId})");
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

                    Plugin.Log.LogInfo($"Setup enemy {enemyName} (ID: {instanceId}, NetID: {enemy.NetworkObjectId}, Index: {enemy.thisEnemyIndex}) with {configHealth} networked health");
                }
                else
                {
                    Plugin.Log.LogInfo($"Enemy {enemyName} is not configured to be damageable");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error setting up enemy: {ex.Message}");
                Plugin.Log.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        // Handle health changes from NetworkVariable updates
        private static void HandleHealthChange(int instanceId, int newHealth)
        {
            // Get the enemy
            EnemyAI enemy = FindEnemyById(instanceId);
            if (enemy == null) return;

            Plugin.Log.LogInfo($"Health changed for enemy {enemy.enemyType.enemyName} (ID: {instanceId}): new health = {newHealth}");

            // If health reached zero, kill the enemy (only on host)
            if (newHealth <= 0 && !enemy.isEnemyDead && StartOfRound.Instance.IsHost)
            {
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

            // If we're the host, process damage directly
            if (StartOfRound.Instance.IsHost)
            {
                Plugin.Log.LogInfo($"Processing hit locally as host: Enemy {enemy.enemyType.enemyName}, Damage {damage}");
                ProcessDamageDirectly(enemy, damage);
            }
            else
            {
                // Otherwise send a message to the host with ALL identifiers
                HitData hitData = new HitData
                {
                    EnemyInstanceId = instanceId,
                    EnemyNetworkId = enemy.NetworkObjectId,
                    EnemyIndex = enemy.thisEnemyIndex,
                    EnemyName = enemy.enemyType.enemyName,
                    Damage = damage
                };

                try
                {
                    // Make sure the message exists
                    if (hitMessage == null)
                    {
                        Plugin.Log.LogWarning("Hit message is null, recreating it");
                        CreateNetworkMessages();
                    }

                    // Send the message to the server
                    hitMessage.SendServer(hitData);
                    Plugin.Log.LogInfo($"Sent hit message to server: Enemy {enemy.enemyType.enemyName}, Damage {damage}, Index {enemy.thisEnemyIndex}");
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"Error sending hit message: {ex}");
                }
            }
        }

        // Process damage directly (only called on host)
        private static void ProcessDamageDirectly(EnemyAI enemy, int damage)
        {
            if (enemy == null || enemy.isEnemyDead) return;

            int instanceId = enemy.GetInstanceID();

            // Ensure enemy is set up
            if (!processedEnemies.ContainsKey(instanceId) || !processedEnemies[instanceId])
            {
                SetupEnemy(enemy);
            }

            // Get the health variable
            if (enemyHealthVars.TryGetValue(instanceId, out var healthVar))
            {
                // Calculate new health
                int currentHealth = healthVar.Value;
                int newHealth = Mathf.Max(0, currentHealth - damage);

                Plugin.Log.LogInfo($"Enemy {enemy.enemyType.enemyName} damaged for {damage}: {currentHealth} -> {newHealth}");

                // Update the NetworkVariable (this will sync to all clients)
                healthVar.Value = newHealth;
            }
            else
            {
                Plugin.Log.LogWarning($"No health variable found for enemy {enemy.enemyType.enemyName} (ID: {instanceId})");
            }
        }

        // Kill an enemy (only called on host)
        private static void KillEnemy(EnemyAI enemy)
        {
            if (enemy == null || enemy.isEnemyDead) return;

            Plugin.Log.LogInfo($"Killing enemy {enemy.enemyType.enemyName}");

            // Force ownership back to host before killing
            if (!enemy.IsOwner)
            {
                Plugin.Log.LogInfo($"Attempting to take ownership of {enemy.enemyType.enemyName} to kill it");
                ulong hostId = StartOfRound.Instance.allPlayerScripts[0].actualClientId;
                enemy.ChangeOwnershipOfEnemy(hostId);
            }

            // Try killing the enemy
            enemy.KillEnemyOnOwnerClient(false);

            // For problematic enemies like Spring, try again with destroy=true as fallback
            if (enemy.enemyType.enemyName.Contains("Spring"))
            {
                Plugin.Log.LogInfo($"Using fallback kill method for {enemy.enemyType.enemyName}");
                enemy.KillEnemyOnOwnerClient(true);
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

            Plugin.Log.LogInfo($"Starting despawn process for {enemy.enemyType.enemyName} (Index: {enemy.thisEnemyIndex})");

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

            // Wait 4.5 seconds if SellBodies is detected (slightly longer than its 4-second timer)
            // or just 0.5 seconds if not
            float waitTime = Plugin.Instance.IsSellBodiesModDetected ? 4.5f : 0.5f;
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
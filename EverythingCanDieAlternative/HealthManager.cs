using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using LethalNetworkAPI;

namespace EverythingCanDieAlternative
{
    public static class NetworkedHealthManager
    {
        // Dictionary to map enemy instance IDs to their NetworkVariables
        private static readonly Dictionary<int, LNetworkVariable<int>> enemyHealthVars = new Dictionary<int, LNetworkVariable<int>>();

        // Dictionary to store max health values
        private static readonly Dictionary<int, int> enemyMaxHealth = new Dictionary<int, int>();

        // Dictionary to track which enemies we've processed
        private static readonly Dictionary<int, bool> processedEnemies = new Dictionary<int, bool>();

        // Dictionary to map enemy IDs to their network object ID - for cross-client lookup
        private static readonly Dictionary<int, ulong> enemyNetworkIds = new Dictionary<int, ulong>();

        // Animator hash for damage animation trigger
        private static readonly int DamageAnimTrigger = Animator.StringToHash("damage");

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

            // Create our hit message IMMEDIATELY at startup - not waiting for network
            CreateNetworkMessage();

            Plugin.Log.LogInfo("Networked Health Manager initialized");
        }

        private static void CreateNetworkMessage()
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

                Plugin.Log.LogInfo("Network message created successfully");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error creating network message: {ex}");
            }
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

                // Check if we've already processed this enemy
                if (processedEnemies.ContainsKey(instanceId) && processedEnemies[instanceId])
                {
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
                    string varName = $"ECD_Health_{enemy.thisEnemyIndex}";

                    // Create the health variable
                    LNetworkVariable<int> healthVar;
                    if (!enemyHealthVars.ContainsKey(instanceId))
                    {
                        // Create a new NetworkVariable
                        healthVar = LNetworkVariable<int>.Create(varName, configHealth);

                        // Subscribe to value changes
                        healthVar.OnValueChanged += (oldHealth, newHealth) => HandleHealthChange(instanceId, newHealth);

                        enemyHealthVars[instanceId] = healthVar;
                    }
                    else
                    {
                        healthVar = enemyHealthVars[instanceId];
                    }

                    // Store max health
                    enemyMaxHealth[instanceId] = configHealth;

                    // Make enemy killable in the game system
                    enemy.enemyType.canDie = true;
                    enemy.enemyType.canBeDestroyed = true;

                    // Set high HP value
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
            }
        }

        // Handle health changes from NetworkVariable updates
        private static void HandleHealthChange(int instanceId, int newHealth)
        {
            // Get the enemy
            EnemyAI enemy = FindEnemyById(instanceId);
            if (enemy == null) return;

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
                        CreateNetworkMessage();
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
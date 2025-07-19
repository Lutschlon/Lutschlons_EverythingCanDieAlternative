using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;

namespace EverythingCanDieAlternative.ModCompatibility.Handlers
{
    // Compatibility handler for the SellBodies mod
    public class SellBodiesCompatibility : BaseModCompatibility
    {
        public override string ModId => "Entity378.sellbodies";
        public override string ModName => "Sell Bodies";

        // Dictionary mapping problematic enemy names to their corresponding SellBodies PowerLevel
        private readonly Dictionary<string, int> _problemEnemyPowerLevels = new Dictionary<string, int>();

        // Cache the result to avoid repeated reflection
        private bool? _isInstalled = null;

        // Track which enemies we've already spawned loot for
        private static HashSet<int> _handledEnemies = new HashSet<int>();

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

                    // Use a simple existence check instead of checking namespaces
                    bool pluginExists = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(ModId);
                    _isInstalled = pluginExists;
                    return pluginExists;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"Error detecting SellBodies mod: {ex.Message}");

                    // Fall back to using a direct check from the Plugin class
                    _isInstalled = Plugin.Instance.IsSellBodiesModDetected;
                    return _isInstalled.Value;
                }
            }
        }

        protected override void OnModInitialize()
        {
            // Initialize the list of problematic enemies
            InitializeProblemEnemies();

            // Clear the handled enemies set when initializing the mod
            _handledEnemies.Clear();
        }

        private void InitializeProblemEnemies()
        {
            // Just identify problematic enemies and what power level they should use
            _problemEnemyPowerLevels.Add("Baldi", 2);
            _problemEnemyPowerLevels.Add("The Fiend", 2);
            _problemEnemyPowerLevels.Add("Gorefield", 2);
            _problemEnemyPowerLevels.Add("Rabbit?", 1);
            _problemEnemyPowerLevels.Add("Light Eater", 2);
            _problemEnemyPowerLevels.Add("SCP682", 2);
            _problemEnemyPowerLevels.Add("Nancy", 1);
            // Add more problematic enemies here as they're discovered
            // _problemEnemyPowerLevels.Add("OtherEnemy", 2);
        }

        public float GetDespawnDelay()
        {
            // With SellBodies, we need a longer delay to allow for body selling
            return 4.5f; // 4.5 seconds is slightly longer than SellBodies' 4-second timer
        }

        /// Check if an enemy needs special handling
        public bool IsProblemEnemy(string enemyName)
        {
            return _problemEnemyPowerLevels.ContainsKey(enemyName);
        }

        /// Handle a problematic enemy's death by spawning appropriate loot
        public void HandleProblemEnemyDeath(EnemyAI enemy)
        {
            if (enemy == null || !IsInstalled) return;

            int instanceId = enemy.GetInstanceID();

            // Check if we've already handled this enemy
            if (_handledEnemies.Contains(instanceId))
            {
                Plugin.LogInfo($"SellBodies: Already handled enemy {enemy.enemyType.enemyName} (ID: {instanceId}), skipping");
                return;
            }

            // Mark this enemy as handled
            _handledEnemies.Add(instanceId);

            string enemyName = enemy.enemyType.enemyName;
            if (!IsProblemEnemy(enemyName))
            {
                return;
            }

            // Store details before enemy is destroyed
            Vector3 enemyPosition = enemy.transform.position;
            Quaternion enemyRotation = enemy.transform.rotation;
            int powerLevel = _problemEnemyPowerLevels[enemyName];

            Plugin.LogInfo($"SellBodies: Handling problem enemy death for {enemyName} with power level {powerLevel}");

            // Start coroutine to spawn loot after delay
            if (StartOfRound.Instance != null)
            {
                StartOfRound.Instance.StartCoroutine(
                    SpawnLootAfterDelay(enemyName, enemyPosition, enemyRotation, powerLevel));
            }
        }

        private IEnumerator SpawnLootAfterDelay(string enemyName, Vector3 position, Quaternion rotation, int powerLevel)
        {
            // Wait for 1 second before spawning loot
            yield return new WaitForSeconds(1f);

            try
            {
                //Plugin.Log.LogInfo($"SellBodies: Attempting to spawn loot for {enemyName} (Power Level: {powerLevel})");

                // Find the appropriate prefab based on power level from SellBodies
                string prefabName = $"ModdedEnemyPowerLevel{powerLevel}Body";
                GameObject prefab = FindPrefabAtRuntime(prefabName);

                if (prefab == null)
                {
                    Plugin.Log.LogError($"SellBodies: Could not find prefab {prefabName} for {enemyName}");
                    yield break;
                }

                //Plugin.Log.LogInfo($"SellBodies: Found prefab: {prefab.name}");

                // Spawn position with offset
                Vector3 spawnPos = position + new Vector3(0, 1f, 0);

                // Instantiate and spawn the loot object
                GameObject spawnedObj = GameObject.Instantiate(prefab, spawnPos, rotation,
                    RoundManager.Instance.mapPropsContainer.transform);

                //Plugin.Log.LogInfo($"SellBodies: Instantiated loot object: {spawnedObj.name}");

                // Spawn the network object
                var networkObj = spawnedObj.GetComponent<NetworkObject>();
                if (networkObj != null)
                {
                   // Plugin.Log.LogInfo($"SellBodies: Attempting to spawn network object");
                    networkObj.Spawn(true);
                    Plugin.LogInfo($"SellBodies: Successfully spawned SellBodies loot for {enemyName}");
                }
                else
                {
                    Plugin.Log.LogError($"SellBodies: Loot object has no NetworkObject component, destroying it");
                    GameObject.Destroy(spawnedObj);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error spawning SellBodies loot: {ex.Message}");
                Plugin.Log.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        private GameObject FindPrefabAtRuntime(string prefabName)
        {
            string searchName = prefabName.Replace("Body", "");
            //Plugin.Log.LogInfo($"SellBodies: Searching for prefab with name containing '{searchName}'");

            try
            {
                // Make sure we have the item list
                if (StartOfRound.Instance == null || StartOfRound.Instance.allItemsList == null)
                {
                    Plugin.Log.LogError("SellBodies: StartOfRound.Instance or allItemsList is null");
                    return null;
                }

                //Plugin.Log.LogInfo($"SellBodies: Checking {StartOfRound.Instance.allItemsList.itemsList.Count} items");

                // Look for items by name pattern
                foreach (var item in StartOfRound.Instance.allItemsList.itemsList)
                {
                    if (item == null) continue;

                    // Find by item name or object name
                    if ((item.name != null && item.name.Contains(searchName)) ||
                        (item.itemName != null && item.itemName.Contains(searchName)) ||
                        (item.spawnPrefab != null && item.spawnPrefab.name.Contains(searchName)))
                    {
                        //Plugin.Log.LogInfo($"SellBodies: Found matching item: {item.name}");
                        return item.spawnPrefab;
                    }
                }

                // Method 2: Try to match by power level identifier
                if (prefabName.Contains("PowerLevel"))
                {
                    int powerLevel = 1;
                    if (prefabName.Contains("PowerLevel2")) powerLevel = 2;
                    else if (prefabName.Contains("PowerLevel3")) powerLevel = 3;

                    // Look for any item containing the right power level
                    foreach (var item in StartOfRound.Instance.allItemsList.itemsList)
                    {
                        if (item == null) continue;

                        // Check item name for power level
                        if (item.name.Contains($"PowerLevel{powerLevel}"))
                        {
                            //Plugin.Log.LogInfo($"SellBodies: Found power level match: {item.name}");
                            return item.spawnPrefab;
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"SellBodies: Error finding prefab: {ex.Message}");
                return null;
            }
        }

        // Direct access method to try and create a loot body for problem enemies
        public void CreateSellBodyLoot(string enemyName, Vector3 position, Quaternion rotation)
        {
            if (!IsInstalled || !IsProblemEnemy(enemyName))
                return;

            int powerLevel = _problemEnemyPowerLevels[enemyName];

            // Start the coroutine to spawn the loot
            if (StartOfRound.Instance != null)
            {
                StartOfRound.Instance.StartCoroutine(
                    SpawnLootAfterDelay(enemyName, position, rotation, powerLevel));
            }
        }
    }
}
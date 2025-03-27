using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;

namespace EverythingCanDieAlternative.ModCompatibility.Handlers
{
    /// <summary>
    /// Compatibility handler for the SellBodies mod
    /// </summary>
    public class SellBodiesCompatibility : BaseModCompatibility
    {
        public override string ModId => "Entity378.sellbodies";
        public override string ModName => "Sell Bodies";

        // Dictionary mapping problematic enemy names to their corresponding SellBodies PowerLevel
        private readonly Dictionary<string, int> _problemEnemyPowerLevels = new Dictionary<string, int>();

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
        }

        private void InitializeProblemEnemies()
        {
            // Just identify problematic enemies and what power level they should use
            _problemEnemyPowerLevels.Add("Baldi", 2);
            _problemEnemyPowerLevels.Add("The Fiend", 2);

            // Add more problematic enemies here as they're discovered
            // _problemEnemyPowerLevels.Add("OtherEnemy", 2);
        }

        public float GetDespawnDelay()
        {
            // With SellBodies, we need a longer delay to allow for body selling
            return 4.5f; // 4.5 seconds is slightly longer than SellBodies' 4-second timer
        }

        /// <summary>
        /// Check if an enemy needs special handling
        /// </summary>
        public bool IsProblemEnemy(string enemyName)
        {
            return _problemEnemyPowerLevels.ContainsKey(enemyName);
        }

        /// <summary>
        /// Handle a problematic enemy's death by spawning appropriate loot
        /// </summary>
        public void HandleProblemEnemyDeath(EnemyAI enemy)
        {
            if (enemy == null || !IsInstalled) return;

            string enemyName = enemy.enemyType.enemyName;
            if (!IsProblemEnemy(enemyName))
            {
                return;
            }

            // Store details before enemy is destroyed
            Vector3 enemyPosition = enemy.transform.position;
            Quaternion enemyRotation = enemy.transform.rotation;
            int powerLevel = _problemEnemyPowerLevels[enemyName];

            // Start coroutine to spawn loot after delay
            if (StartOfRound.Instance != null)
            {
                StartOfRound.Instance.StartCoroutine(
                    SpawnLootAfterDelay(enemyName, enemyPosition, enemyRotation, powerLevel));
            }
        }

        private IEnumerator SpawnLootAfterDelay(string enemyName, Vector3 position, Quaternion rotation, int powerLevel)
        {
            // Wait for 4 seconds like SellBodies does
            yield return new WaitForSeconds(1f);

            try
            {
                // Find the appropriate prefab based on power level from SellBodies
                string prefabName = $"ModdedEnemyPowerLevel{powerLevel}Body";
                GameObject prefab = FindPrefabAtRuntime(prefabName);

                if (prefab == null)
                {
                    yield break;
                }

                // Spawn position with offset
                Vector3 spawnPos = position + new Vector3(0, 1f, 0);

                // Instantiate and spawn the loot object
                GameObject spawnedObj = GameObject.Instantiate(prefab, spawnPos, rotation,
                    RoundManager.Instance.mapPropsContainer.transform);

                // Spawn the network object
                var networkObj = spawnedObj.GetComponent<NetworkObject>();
                if (networkObj != null)
                {
                    networkObj.Spawn(true);
                    Plugin.Log.LogInfo($"Successfully spawned SellBodies loot for {enemyName}");
                }
                else
                {
                    GameObject.Destroy(spawnedObj);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error spawning SellBodies loot: {ex.Message}");
            }
        }

        private GameObject FindPrefabAtRuntime(string prefabName)
        {
            string searchName = prefabName.Replace("Body", "");

            try
            {
                // Make sure we have the item list
                if (StartOfRound.Instance == null || StartOfRound.Instance.allItemsList == null)
                {
                    return null;
                }

                // Look for items by name pattern
                foreach (var item in StartOfRound.Instance.allItemsList.itemsList)
                {
                    // Find by item name or object name
                    if (item.name.Contains(searchName) ||
                        item.itemName.Contains(searchName) ||
                        (item.spawnPrefab != null && item.spawnPrefab.name.Contains(searchName)))
                    {
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
                        // Check item name for power level
                        if (item.name.Contains($"PowerLevel{powerLevel}"))
                        {
                            return item.spawnPrefab;
                        }
                    }
                }

                return null;
            }
            catch
            {
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
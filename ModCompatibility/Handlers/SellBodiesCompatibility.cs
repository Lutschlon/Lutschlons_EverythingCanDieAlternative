using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Unity.Netcode;

namespace EverythingCanDieAlternative.ModCompatibility.Handlers
{
    /// <summary>
    /// Enhanced compatibility handler for the SellBodies mod with special handling for problematic enemies
    /// </summary>
    public class SellBodiesCompatibility : BaseModCompatibility
    {
        public override string ModId => "Entity378.sellbodies";
        public override string ModName => "Sell Bodies";

        // Dictionary mapping problematic enemy names to their corresponding SellBodies loot types and PowerLevel
        private readonly Dictionary<string, EnemyLootInfo> _problemEnemyLootMap = new Dictionary<string, EnemyLootInfo>();


        // Cache the result to avoid repeated reflection
        private bool? _isInstalled = null;

        // Store prefabs for different power levels
        private GameObject _powerLevel1Prefab;
        private GameObject _powerLevel2Prefab;
        private GameObject _powerLevel3Prefab;

        // Structure to hold enemy loot data
        private class EnemyLootInfo
        {
            public int PowerLevel { get; set; }
            public int MinValue { get; set; }
            public int MaxValue { get; set; }

            public EnemyLootInfo(int powerLevel, int minValue, int maxValue)
            {
                PowerLevel = powerLevel;
                MinValue = minValue;
                MaxValue = maxValue;
            }
        }

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

                    // Check if the plugin GUID exists in BepInEx plugins
                    foreach (var assembly in assemblies)
                    {
                        try
                        {
                            // Look for the BepInPlugin attribute with our expected GUID
                            var types = assembly.GetTypes();
                            foreach (var type in types)
                            {
                                var attributes = type.GetCustomAttributes(false);
                                foreach (var attr in attributes)
                                {
                                    var attrType = attr.GetType();
                                    if (attrType.Name == "BepInPlugin" || attrType.Name == "BepInPluginAttribute")
                                    {
                                        var guidProperty = attrType.GetProperty("GUID") ??
                                                          attrType.GetProperty("guid") ??
                                                          attrType.GetField("GUID")?.GetValue(attr);

                                        if (guidProperty != null)
                                        {
                                            string guid = guidProperty.ToString();
                                            if (guid == ModId)
                                            {
                                                _isInstalled = true;
                                                return true;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Skip errors for individual assemblies or types
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
                    // This uses the existing detection that was already working
                    _isInstalled = Plugin.Instance.IsSellBodiesModDetected;
                    return _isInstalled.Value;
                }
            }
        }

        protected override void OnModInitialize()
        {
            // Initialize the problem enemy map
            InitializeProblemEnemyMap();

            // Initialize prefabs from SellBodies assetbundle
            try
            {
                // Find already loaded prefabs from the SellBodies mod
                _powerLevel1Prefab = FindSellBodiesPrefab("ModdedEnemyPowerLevel1Body");
                _powerLevel2Prefab = FindSellBodiesPrefab("ModdedEnemyPowerLevel2Body");
                _powerLevel3Prefab = FindSellBodiesPrefab("ModdedEnemyPowerLevel3Body");

                if (_powerLevel1Prefab != null && _powerLevel2Prefab != null && _powerLevel3Prefab != null)
                {
                    Plugin.Log.LogInfo("Successfully found SellBodies loot prefabs");
                }
                else
                {
                    Plugin.Log.LogWarning("Some SellBodies loot prefabs couldn't be found. Special handling may not work correctly.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error initializing SellBodies compatibility prefabs: {ex.Message}");
            }

            Plugin.Log.LogInfo("SellBodies compatibility initialized with special handling for problematic enemies");
        }

        private void InitializeProblemEnemyMap()
        {
            // Add problematic enemies to the map with appropriate power levels and value ranges
            _problemEnemyLootMap.Add("Baldi", new EnemyLootInfo(1, 75, 100));

            // Add more problematic enemies here as they're discovered
            // _problemEnemyLootMap.Add("OtherEnemy", new EnemyLootInfo(2, 125, 150));
            // _problemEnemyLootMap.Add("BigBossEnemy", new EnemyLootInfo(3, 175, 200));

            Plugin.Log.LogInfo($"Initialized problem enemy map with {_problemEnemyLootMap.Count} entries");
        }

        private GameObject FindSellBodiesPrefab(string prefabName)
        {
            string searchName = prefabName.Replace("Body", "");

            // Look through all items in the game's item list to find the SellBodies items
            try
            {
                // Look for items by name pattern (ModdedEnemyPowerLevel etc.)
                foreach (var item in StartOfRound.Instance.allItemsList.itemsList)
                {
                    // Find by item name or object name
                    if (item.name.Contains(searchName) ||
                        item.itemName.Contains(searchName) ||
                        (item.spawnPrefab != null && item.spawnPrefab.name.Contains(searchName)))
                    {
                        Plugin.Log.LogInfo($"Found SellBodies item: {item.name} for type {prefabName}");
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
                            Plugin.Log.LogInfo($"Found power level {powerLevel} item: {item.name}");
                            return item.spawnPrefab;
                        }
                    }
                }

                // Just try to find any modded enemy corpus items
                foreach (var item in StartOfRound.Instance.allItemsList.itemsList)
                {
                    if (item.name.Contains("ModdedEnemy") && item.name.Contains("Body"))
                    {
                        Plugin.Log.LogInfo($"Found backup modded enemy item: {item.name}");
                        return item.spawnPrefab;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Error finding SellBodies prefab: {ex.Message}");
            }

            Plugin.Log.LogWarning($"Could not find SellBodies prefab: {prefabName}");
            return null;
        }

        /// <summary>
        /// Get the appropriate despawn delay when SellBodies mod is active
        /// </summary>
        /// <returns>The despawn delay in seconds</returns>
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
            return _problemEnemyLootMap.ContainsKey(enemyName);
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

            Plugin.Log.LogInfo($"Special handling for {enemyName} with SellBodies compatibility");

            // Store details before enemy is destroyed
            Vector3 enemyPosition = enemy.transform.position;
            Quaternion enemyRotation = enemy.transform.rotation;

            // Get loot info
            EnemyLootInfo lootInfo = _problemEnemyLootMap[enemyName];

            // Start coroutine to spawn loot after delay
            if (StartOfRound.Instance != null)
            {
                StartOfRound.Instance.StartCoroutine(
                    SpawnLootAfterDelay(enemyPosition, enemyRotation, lootInfo));
            }
        }

        private IEnumerator SpawnLootAfterDelay(Vector3 position, Quaternion rotation, EnemyLootInfo lootInfo)
        {
            // Wait for 4 seconds like SellBodies does
            yield return new WaitForSeconds(4f);

            try
            {
                // Get the appropriate prefab based on power level
                GameObject prefab = GetPrefabByPowerLevel(lootInfo.PowerLevel);
                if (prefab == null)
                {
                    // Try finding a modded enemy body in the item list as fallback
                    foreach (var item in StartOfRound.Instance.allItemsList.itemsList)
                    {
                        // Look for any ModdedEnemy item as a fallback
                        if (item.name.Contains("ModdedEnemy") ||
                            (item.itemName != null && item.itemName.Contains("Modded Enemy")))
                        {
                            prefab = item.spawnPrefab;
                            break;
                        }
                    }

                    if (prefab == null)
                    {
                        Plugin.Log.LogError($"No prefab found for power level {lootInfo.PowerLevel} and no fallback found");
                        yield break;
                    }

                    Plugin.Log.LogInfo("Using fallback modded enemy prefab");
                }

                // Spawn position with offset
                Vector3 spawnPos = position + new Vector3(0, 1f, 0);

                // Instantiate and spawn the loot object
                GameObject spawnedObj = GameObject.Instantiate(prefab, spawnPos, rotation,
                    RoundManager.Instance.mapPropsContainer.transform);

                // Set the value
                int scrapValue = UnityEngine.Random.Range(lootInfo.MinValue, lootInfo.MaxValue + 1);

                // Update the value properties if it has a GrabbableObject component
                var grabbable = spawnedObj.GetComponent<GrabbableObject>();
                if (grabbable != null)
                {
                    grabbable.scrapValue = scrapValue;

                    // Update text on scan node if it exists
                    var scanNode = spawnedObj.GetComponentInChildren<ScanNodeProperties>();
                    if (scanNode != null)
                    {
                        scanNode.subText = $"Value: ${scrapValue}";
                    }

                    Plugin.Log.LogInfo($"Set scrap value to {scrapValue}");
                }

                // Spawn the network object
                var networkObj = spawnedObj.GetComponent<NetworkObject>();
                if (networkObj != null)
                {
                    networkObj.Spawn(true);
                    Plugin.Log.LogInfo($"Successfully spawned power level {lootInfo.PowerLevel} loot at {spawnPos}");
                }
                else
                {
                    Plugin.Log.LogError("Spawned object has no NetworkObject component");
                    GameObject.Destroy(spawnedObj);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in SpawnLootAfterDelay: {ex.Message}");
                Plugin.Log.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        private GameObject GetPrefabByPowerLevel(int powerLevel)
        {
            switch (powerLevel)
            {
                case 1:
                    return _powerLevel1Prefab;
                case 2:
                    return _powerLevel2Prefab;
                case 3:
                    return _powerLevel3Prefab;
                default:
                    return _powerLevel1Prefab;
            }
        }

        // Direct access method to try and create a loot body for problem enemies
        public void CreateSellBodyLoot(string enemyName, Vector3 position, Quaternion rotation)
        {
            if (!IsInstalled || !IsProblemEnemy(enemyName))
                return;

            // Get loot info
            EnemyLootInfo lootInfo = _problemEnemyLootMap[enemyName];

            // Use our fallback method that doesn't rely on reflection
            if (StartOfRound.Instance != null)
            {
                StartOfRound.Instance.StartCoroutine(
                    SpawnLootAfterDelay(position, rotation, lootInfo));
            }
        }
    }
}
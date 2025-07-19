using EverythingCanDieAlternative.ModCompatibility;
using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static EverythingCanDieAlternative.Plugin;

namespace EverythingCanDieAlternative
{
    public static class Patches
    {
        public static void Initialize(Harmony harmony)
        {
            try
            {
                //Plugin.Log.LogInfo("Applying Harmony patches...");

                // Patch StartOfRound.Start to initialize our system
                var startOfRoundStartMethod = AccessTools.Method(typeof(StartOfRound), "Start");
                if (startOfRoundStartMethod == null)
                {
                    Plugin.Log.LogError("Could not find StartOfRound.Start method - patches failed!");
                    return;
                }
                var startOfRoundPostfix = AccessTools.Method(typeof(Patches), nameof(StartOfRoundPostfix));
                harmony.Patch(startOfRoundStartMethod, null, new HarmonyMethod(startOfRoundPostfix));
                //Plugin.Log.LogInfo("StartOfRound.Start patched successfully");

                // Patch EnemyAI.Start to catch newly spawned enemies
                var enemyAIStartMethod = AccessTools.Method(typeof(EnemyAI), "Start");
                var enemyAIStartPostfix = AccessTools.Method(typeof(Patches), nameof(EnemyAIStartPostfix));
                harmony.Patch(enemyAIStartMethod, null, new HarmonyMethod(enemyAIStartPostfix));
                //Plugin.Log.LogInfo("EnemyAI.Start patched successfully");

                // Only patch HitEnemyOnLocalClient
                var hitLocalMethod = AccessTools.Method(typeof(EnemyAI), "HitEnemyOnLocalClient");
                var hitLocalPrefix = AccessTools.Method(typeof(Patches), nameof(HitEnemyOnLocalClientPrefix));
                harmony.Patch(hitLocalMethod, new HarmonyMethod(hitLocalPrefix));
                //Plugin.Log.LogInfo("EnemyAI.HitEnemyOnLocalClient patched successfully");

                // Patch HitEnemy to ensure vanilla hits are also captured for immortal enemies
                var hitEnemyMethod = AccessTools.Method(typeof(EnemyAI), "HitEnemy");
                var hitEnemyPrefix = AccessTools.Method(typeof(Patches), nameof(HitEnemyPrefix));
                harmony.Patch(hitEnemyMethod, new HarmonyMethod(hitEnemyPrefix));
                //Plugin.Log.LogInfo("EnemyAI.HitEnemy patched successfully");

                // Patch RoundManager.FinishGeneratingLevel to refresh BrutalCompanyMinus compatibility
                var finishGeneratingLevelMethod = AccessTools.Method(typeof(RoundManager), "FinishGeneratingLevel");
                var finishGeneratingLevelPostfix = AccessTools.Method(typeof(Patches), nameof(FinishGeneratingLevelPostfix));
                harmony.Patch(finishGeneratingLevelMethod, null, new HarmonyMethod(finishGeneratingLevelPostfix));
                //Plugin.Log.LogInfo("RoundManager.FinishGeneratingLevel patched successfully");

                var shipLeaveMethod = AccessTools.Method(typeof(StartOfRound), "ShipLeave");
                var shipLeavePostfix = AccessTools.Method(typeof(Patches), nameof(ShipLeavePostfix));
                harmony.Patch(shipLeaveMethod, null, new HarmonyMethod(shipLeavePostfix));
                //Plugin.LogInfo("StartOfRound.ShipLeave patched successfully");

                // Patch SpikeRoofTrap for trap configuration
                var spikeRoofTrapMethod = AccessTools.Method(typeof(SpikeRoofTrap), "OnTriggerStay");
                var spikeRoofTrapPrefix = AccessTools.Method(typeof(Patches), nameof(SpikeRoofTrapPrefix));
                harmony.Patch(spikeRoofTrapMethod, new HarmonyMethod(spikeRoofTrapPrefix));
                Plugin.Log.LogInfo("SpikeRoofTrap.OnTriggerStay patched successfully");

                Plugin.Log.LogInfo("All Harmony patches applied successfully");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error applying Harmony patches: {ex.Message}");
                Plugin.Log.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        public static void StartOfRoundPostfix(StartOfRound __instance)
        {
            try
            {
                Plugin.Log.LogInfo("Game starting, initializing networked enemy health system...");

                // Reload ALL config files
                Plugin.Instance.Config.Reload();
                EnemyControlConfiguration.Instance.ReloadConfig();
                DespawnConfiguration.Instance.ReloadConfig();
                Plugin.LogInfo("All configurations reloaded from files");

                // Initialize our network health system
                HealthManager.Initialize();

                // Find all available enemy types in the game
                Plugin.enemies = new List<EnemyType>(Resources.FindObjectsOfTypeAll<EnemyType>());
                Plugin.LogInfo($"Found {Plugin.enemies.Count} enemy types");

                // Maximum HP when a new config gets generated
                const int CAPPED_DEFAULT_HP = 30;

                // Load config for all enemy types
                foreach (var enemyType in Plugin.enemies)
                {
                    if (enemyType == null || string.IsNullOrEmpty(enemyType.enemyName))
                    {
                        Plugin.Log.LogWarning("Found null or invalid enemy type, skipping");
                        continue;
                    }

                    string sanitizedName = Plugin.RemoveInvalidCharacters(enemyType.enemyName).ToUpper();
                    Plugin.LogInfo($"Processing enemy type: {enemyType.enemyName}");
                    Plugin.CanMob(".Unimmortal", sanitizedName); // This will create config if it doesn't exist

                    // Get vanilla HP value from prefab if available
                    float defaultHealth = 3f; // Default fallback value
                    if (enemyType.enemyPrefab != null)
                    {
                        EnemyAI enemyAI = enemyType.enemyPrefab.GetComponentInChildren<EnemyAI>();
                        if (enemyAI != null)
                        {
                            // Ensure minimum HP of 1
                            if (enemyAI.enemyHP <= 0)
                            {
                                defaultHealth = 1f;
                                Plugin.LogInfo($"Detected 0 or negative HP for {enemyType.enemyName}, setting default to 1");
                            }
                            else
                            {
                                // Cap the HP at CAPPED_DEFAULT_HP
                                defaultHealth = Math.Min((float)enemyAI.enemyHP, CAPPED_DEFAULT_HP);

                                if (enemyAI.enemyHP > CAPPED_DEFAULT_HP)
                                {
                                    Plugin.LogInfo($"Capped HP for {enemyType.enemyName} from {enemyAI.enemyHP} to {defaultHealth}");
                                }
                                else
                                {
                                    //Plugin.LogInfo($"Found vanilla HP value for {enemyType.enemyName}: {defaultHealth}");
                                }
                            }
                        }
                    }

                    Plugin.GetMobHealth(sanitizedName, defaultHealth); // Use capped prefab HP value or fallback
                    Plugin.ShouldDespawn(sanitizedName); // Create despawn config entries
                }

                // Create enemy control config entries
                EnemyControlConfiguration.Instance.PreCreateEnemyConfigEntries();

                // Process existing enemies in the scene
                ProcessExistingEnemies();

                Log.LogInfo("StartOfRoundPostfix completed successfully");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in StartOfRoundPostfix: {ex.Message}");
                Plugin.Log.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        private static void ProcessExistingEnemies()
        {
            try
            {
                var enemies = UnityEngine.Object.FindObjectsOfType<EnemyAI>();
                Plugin.LogInfo($"Found {enemies.Length} active enemies");

                foreach (var enemy in enemies)
                {
                    if (enemy?.enemyType == null) continue;
                    Plugin.LogInfo($"Setting up enemy: {enemy.enemyType.enemyName}");
                    HealthManager.SetupEnemy(enemy);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error processing existing enemies: {ex.Message}");
                Plugin.Log.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        public static void EnemyAIStartPostfix(EnemyAI __instance)
        {
            try
            {
                if (__instance?.enemyType == null) return;

                // When a new enemy spawns, set it up in our system
                HealthManager.SetupEnemy(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in EnemyAIStartPostfix: {ex.Message}");
            }

        }

        // This is the only hit interception point we need
        public static bool HitEnemyOnLocalClientPrefix(EnemyAI __instance, int force, Vector3 hitDirection, PlayerControllerB playerWhoHit, bool playHitSFX, int hitID)
        {
            try
            {
                if (__instance == null || __instance.isEnemyDead) return true;

                Plugin.LogInfo($"Local hit detected on {__instance.enemyType.enemyName} from {(playerWhoHit?.playerUsername ?? "unknown")} with force {force}");

                // Check for LethalHands compatibility
                var lethalHandsHandler = ModCompatibilityManager.Instance.GetHandler<ModCompatibility.Handlers.LethalHandsCompatibility>("SlapitNow.LethalHands");
                bool isLethalHandsPunch = (lethalHandsHandler != null && lethalHandsHandler.IsInstalled && force == -22);

                if (isLethalHandsPunch)
                {
                    Plugin.LogInfo($"Detected LethalHands punch with force {force}");
                }

                // Process with our health system
                HealthManager.ProcessHit(__instance, force, playerWhoHit);

                // For LethalHands punches, we need special handling
                if (isLethalHandsPunch)
                {
                    // Let LethalHands process its own effects like sounds and animations
                    // but don't let it apply damage via the vanilla system
                    return true;
                }

                // Let the vanilla method run for animations, effects, and networking
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in HitEnemyOnLocalClientPrefix: {ex.Message}");
                return true;
            }
        }

        // Patch for EnemyAI.HitEnemy to handle immortal enemies
        public static bool HitEnemyPrefix(EnemyAI __instance, int force, PlayerControllerB playerWhoHit)
        {
            try
            {
                if (__instance == null || __instance.isEnemyDead) return true;

                string enemyName = __instance.enemyType.enemyName;
                string sanitizedName = Plugin.RemoveInvalidCharacters(enemyName).ToUpper();

                // Only handle enemies that are enabled but set as immortal (Unimmortal=false)
                if (Plugin.IsModEnabledForEnemy(sanitizedName) && !Plugin.CanMob(".Unimmortal", sanitizedName))
                {
                    // Reset HP to 999 to ensure immortality
                    __instance.enemyHP = 999;
                    Plugin.LogInfo($"HitEnemyPrefix: Refreshed immortal enemy {enemyName} HP to 999");

                    // Still let the vanilla method run for sound effects and animations
                    return true;
                }

                // For other enemies, let the normal process happen
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in HitEnemyPrefix: {ex.Message}");
                return true;
            }
        }

        // Spike trap patch for simple trap configuration
        public static bool SpikeRoofTrapPrefix(SpikeRoofTrap __instance, Collider other)
        {
            try
            {
                // Only intercept enemy collisions
                if (!other.CompareTag("Enemy")) return true;

                EnemyAICollisionDetect enemyCollision = other.gameObject.GetComponent<EnemyAICollisionDetect>();
                if (enemyCollision?.mainScript == null) return true;

                EnemyAI enemy = enemyCollision.mainScript;
                if (enemy == null || enemy.isEnemyDead) return true;

                string enemyName = enemy.enemyType.enemyName;
                string sanitizedName = Plugin.RemoveInvalidCharacters(enemyName).ToUpper();

                // Check if this enemy is managed by our mod
                if (!Plugin.IsModEnabledForEnemy(sanitizedName))
                {
                    // Not managed by us, let vanilla handle it
                    return true;
                }

                // Check the global spike trap setting
                if (!Plugin.AllowSpikeTrapsToKillEnemies.Value)
                {
                    Plugin.LogInfo($"Spike trap blocked from killing {enemyName} due to global setting");
                    return false; // Block the spike trap from processing this enemy
                }

                Plugin.LogInfo($"Spike trap allowed to kill {enemyName}");

                // If we reach here, the spike trap is allowed to kill this enemy
                // But we still need to handle our cleanup when it dies

                // Check if this enemy should despawn after death and schedule cleanup
                if (DespawnConfiguration.Instance.ShouldDespawnEnemy(enemyName) && StartOfRound.Instance.IsHost)
                {
                    // Schedule cleanup after the kill
                    __instance.StartCoroutine(HandleSpikeKillCleanup(enemy));
                }

                return true; // Allow the vanilla spike trap logic to proceed
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in SpikeRoofTrapPrefix: {ex.Message}");
                return true; // Always let vanilla proceed on error
            }
        }

        private static IEnumerator HandleSpikeKillCleanup(EnemyAI enemy)
        {
            if (enemy == null) yield break;

            string enemyName = enemy.enemyType.enemyName;

            // Wait for the enemy to actually die
            float timeout = 2f;
            while (!enemy.isEnemyDead && timeout > 0)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (enemy.isEnemyDead)
            {
                Plugin.LogInfo($"Cleaning up spike-killed enemy: {enemyName}");

                // Clean up our tracking data
                HealthManager.CleanupExternallyKilledEnemy(enemy);

                // Handle compatibility (SellBodies, Hitmarker, etc.)
                int instanceId = enemy.GetInstanceID();
                HandleKillCompatibility(enemy, instanceId, enemyName);

                // Handle despawn if needed
                if (DespawnConfiguration.Instance.ShouldDespawnEnemy(enemyName))
                {
                    yield return new WaitForSeconds(GetDespawnDelay());

                    if (enemy != null && enemy.isEnemyDead)
                    {
                        Plugin.LogInfo($"Despawning spike-killed enemy: {enemyName}");

                        if (enemy.thisEnemyIndex >= 0)
                        {
                            HealthManager.NotifyClientsOfDestroy(enemy.thisEnemyIndex);
                        }

                        UnityEngine.Object.Destroy(enemy.gameObject);
                    }
                }
            }
        }

        private static float GetDespawnDelay()
        {
            // Check for SellBodies compatibility
            var sellBodiesHandler = ModCompatibilityManager.Instance.GetHandler<ModCompatibility.Handlers.SellBodiesCompatibility>("Entity378.sellbodies");
            if (sellBodiesHandler != null && sellBodiesHandler.IsInstalled)
            {
                return sellBodiesHandler.GetDespawnDelay();
            }

            return 0.5f; // Default delay
        }

        private static void HandleKillCompatibility(EnemyAI enemy, int instanceId, string enemyName)
        {
            // SellBodies compatibility
            var sellBodiesHandler = ModCompatibilityManager.Instance.GetHandler<ModCompatibility.Handlers.SellBodiesCompatibility>("Entity378.sellbodies");
            if (sellBodiesHandler != null && sellBodiesHandler.IsInstalled &&
                sellBodiesHandler.IsProblemEnemy(enemyName))
            {
                sellBodiesHandler.HandleProblemEnemyDeath(enemy);
            }

            // Hitmarker compatibility
            var hitmarkerHandler = ModCompatibilityManager.Instance.GetHandler<ModCompatibility.Handlers.HitmarkerCompatibility>("com.github.zehsteam.Hitmarker");
            if (hitmarkerHandler != null && hitmarkerHandler.IsInstalled)
            {
                PlayerControllerB lastDamageSource = hitmarkerHandler.GetLastDamageSource(instanceId);
                if (lastDamageSource != null)
                {
                    hitmarkerHandler.NotifyEnemyKilled(enemy, lastDamageSource);
                }
            }
        }

        [HarmonyPostfix]
        public static void ShipLeavePostfix()
        {
            try
            {
                // Clear Hitmarker compatibility tracking data
                var hitmarkerHandler = ModCompatibilityManager.Instance.GetHandler<ModCompatibility.Handlers.HitmarkerCompatibility>("com.github.zehsteam.Hitmarker");
                if (hitmarkerHandler != null && hitmarkerHandler.IsInstalled)
                {
                    Plugin.LogInfo("Clearing Hitmarker compatibility data on level unload");
                    hitmarkerHandler.ClearTracking();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in ShipLeavePostfix: {ex.Message}");
            }
        }

        public static void FinishGeneratingLevelPostfix()
        {
            try
            {
                // Refresh BrutalCompanyMinus bonus HP cache when a new level is generated
                var brutalCompanyHandler = ModCompatibilityManager.Instance.GetHandler<ModCompatibility.Handlers.BrutalCompanyMinusCompatibility>("SoftDiamond.BrutalCompanyMinusExtraReborn");
                if (brutalCompanyHandler != null && brutalCompanyHandler.IsInstalled)
                {
                    Plugin.LogInfo("Refreshing BrutalCompanyMinus compatibility data after level generation");
                    brutalCompanyHandler.RefreshBonusHp();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in FinishGeneratingLevelPostfix: {ex.Message}");
            }
        }
    }
}
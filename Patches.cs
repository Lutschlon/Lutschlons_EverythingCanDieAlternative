using EverythingCanDieAlternative.ModCompatibility;
using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace EverythingCanDieAlternative
{
    public static class Patches
    {
        public static void Initialize(Harmony harmony)
        {
            try
            {
                // Patch StartOfRound.Start to initialize our system
                var startOfRoundStartMethod = AccessTools.Method(typeof(StartOfRound), "Start");
                var startOfRoundPostfix = AccessTools.Method(typeof(Patches), nameof(StartOfRoundPostfix));
                harmony.Patch(startOfRoundStartMethod, null, new HarmonyMethod(startOfRoundPostfix));

                // Patch EnemyAI.Start to catch newly spawned enemies
                var enemyAIStartMethod = AccessTools.Method(typeof(EnemyAI), "Start");
                var enemyAIStartPostfix = AccessTools.Method(typeof(Patches), nameof(EnemyAIStartPostfix));
                harmony.Patch(enemyAIStartMethod, null, new HarmonyMethod(enemyAIStartPostfix));

                // Only patch HitEnemyOnLocalClient
                var hitLocalMethod = AccessTools.Method(typeof(EnemyAI), "HitEnemyOnLocalClient");
                var hitLocalPrefix = AccessTools.Method(typeof(Patches), nameof(HitEnemyOnLocalClientPrefix));
                harmony.Patch(hitLocalMethod, new HarmonyMethod(hitLocalPrefix));

                // Patch RoundManager.FinishGeneratingLevel to refresh BrutalCompanyMinus compatibility
                var finishGeneratingLevelMethod = AccessTools.Method(typeof(RoundManager), "FinishGeneratingLevel");
                var finishGeneratingLevelPostfix = AccessTools.Method(typeof(Patches), nameof(FinishGeneratingLevelPostfix));
                harmony.Patch(finishGeneratingLevelMethod, null, new HarmonyMethod(finishGeneratingLevelPostfix));

                Plugin.Log.LogInfo("Harmony patches applied successfully");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error applying Harmony patches: {ex}");
            }
        }

        public static void StartOfRoundPostfix(StartOfRound __instance)
        {
            try
            {
                Plugin.Log.LogInfo("Game starting, initializing networked enemy health system...");

                // Initialize our network health system
                NetworkedHealthManager.Initialize();

                // Find all available enemy types in the game
                Plugin.enemies = new List<EnemyType>(Resources.FindObjectsOfTypeAll<EnemyType>());
                Plugin.Log.LogInfo($"Found {Plugin.enemies.Count} enemy types");

                // Maximum HP when a new config gets generated
                const int CAPPED_DEFAULT_HP = 15;

                // Load config for all enemy types
                foreach (var enemyType in Plugin.enemies)
                {
                    string sanitizedName = Plugin.RemoveInvalidCharacters(enemyType.enemyName).ToUpper();
                    Plugin.CanMob(".Unimmortal", sanitizedName); // This will create config if it doesn't exist

                    // Get vanilla HP value from prefab if available
                    int defaultHealth = 3; // Default fallback value
                    if (enemyType.enemyPrefab != null)
                    {
                        EnemyAI enemyAI = enemyType.enemyPrefab.GetComponentInChildren<EnemyAI>();
                        if (enemyAI != null)
                        {
                            // Ensure minimum HP of 1
                            if (enemyAI.enemyHP <= 0)
                            {
                                defaultHealth = 1;
                                Plugin.Log.LogInfo($"Detected 0 or negative HP for {enemyType.enemyName}, setting default to 1");
                            }
                            else
                            {
                                // Cap the HP at CAPPED_DEFAULT_HP
                                defaultHealth = Math.Min(enemyAI.enemyHP, CAPPED_DEFAULT_HP);

                                if (enemyAI.enemyHP > CAPPED_DEFAULT_HP)
                                {
                                    Plugin.Log.LogInfo($"Capped HP for {enemyType.enemyName} from {enemyAI.enemyHP} to {defaultHealth}");
                                }
                                else
                                {
                                    Plugin.Log.LogInfo($"Found vanilla HP value for {enemyType.enemyName}: {defaultHealth}");
                                }
                            }
                        }
                    }

                    Plugin.GetMobHealth(sanitizedName, defaultHealth); // Use capped prefab HP value or fallback
                    Plugin.ShouldDespawn(sanitizedName); // Create despawn config entries
                }

                // Process existing enemies in the scene
                ProcessExistingEnemies();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in StartOfRoundPostfix: {ex}");
            }
        }

        private static void ProcessExistingEnemies()
        {
            try
            {
                var enemies = UnityEngine.Object.FindObjectsOfType<EnemyAI>();
                Plugin.Log.LogInfo($"Found {enemies.Length} active enemies");

                foreach (var enemy in enemies)
                {
                    if (enemy?.enemyType == null) continue;
                    NetworkedHealthManager.SetupEnemy(enemy);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error processing existing enemies: {ex}");
            }
        }

        public static void EnemyAIStartPostfix(EnemyAI __instance)
        {
            try
            {
                if (__instance?.enemyType == null) return;

                // When a new enemy spawns, set it up in our system
                NetworkedHealthManager.SetupEnemy(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in EnemyAIStartPostfix: {ex}");
            }
        }

        // This is the only hit interception point we need
        public static bool HitEnemyOnLocalClientPrefix(EnemyAI __instance, int force, Vector3 hitDirection, PlayerControllerB playerWhoHit, bool playHitSFX, int hitID)
        {
            try
            {
                if (__instance == null || __instance.isEnemyDead) return true;

                Plugin.Log.LogInfo($"Local hit detected on {__instance.enemyType.enemyName} from {(playerWhoHit?.playerUsername ?? "unknown")} with force {force}");

                // Check for LethalHands compatibility
                var lethalHandsHandler = ModCompatibilityManager.Instance.GetHandler<ModCompatibility.Handlers.LethalHandsCompatibility>("SlapitNow.LethalHands");
                bool isLethalHandsPunch = (lethalHandsHandler != null && lethalHandsHandler.IsInstalled && force == -22);

                if (isLethalHandsPunch)
                {
                    Plugin.Log.LogInfo($"Detected LethalHands punch with force {force}");
                }

                // Process with our health system
                NetworkedHealthManager.ProcessHit(__instance, force, playerWhoHit);

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
                Plugin.Log.LogError($"Error in HitEnemyOnLocalClientPrefix: {ex}");
                return true;
            }
        }

        [HarmonyPostfix]
        public static void FinishGeneratingLevelPostfix()
        {
            try
            {
                // Refresh BrutalCompanyMinus bonus HP cache when a new level is generated
                var brutalCompanyHandler = ModCompatibilityManager.Instance.GetHandler<ModCompatibility.Handlers.BrutalCompanyMinusCompatibility>("SoftDiamond.BrutalCompanyMinusExtraReborn");
                if (brutalCompanyHandler != null && brutalCompanyHandler.IsInstalled)
                {
                    Plugin.Log.LogInfo("Refreshing BrutalCompanyMinus compatibility data after level generation");
                    brutalCompanyHandler.RefreshBonusHp();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in FinishGeneratingLevelPostfix: {ex}");
            }
        }
    }
}
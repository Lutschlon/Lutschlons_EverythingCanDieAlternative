﻿using GameNetcodeStuff;
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

                // Maximum vanilla HP allowed (based on Forest Giant)
                const int MAX_VANILLA_HP = 38;

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
                            // Cap the HP at MAX_VANILLA_HP
                            defaultHealth = Math.Min(enemyAI.enemyHP, MAX_VANILLA_HP);

                            if (enemyAI.enemyHP > MAX_VANILLA_HP)
                            {
                                Plugin.Log.LogInfo($"Capped HP for {enemyType.enemyName} from {enemyAI.enemyHP} to {defaultHealth}");
                            }
                            else
                            {
                                Plugin.Log.LogInfo($"Found vanilla HP value for {enemyType.enemyName}: {defaultHealth}");
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

                // Process with our health system
                NetworkedHealthManager.ProcessHit(__instance, force, playerWhoHit);

                // Let the vanilla method run for animations, effects, and networking
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in HitEnemyOnLocalClientPrefix: {ex}");
                return true;
            }
        }
    }
}
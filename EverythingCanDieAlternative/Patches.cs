using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace EverythingCanDieAlternative
{
    public static class Patches
    {
        public static void Initialize(Harmony harmony)
        {
            try
            {
                // Patch StartOfRound.Start to initialize our system and setup existing enemies
                var startOfRoundStartMethod = AccessTools.Method(typeof(StartOfRound), "Start");
                var startOfRoundPostfix = AccessTools.Method(typeof(Patches), nameof(StartOfRoundPostfix));
                harmony.Patch(startOfRoundStartMethod, null, new HarmonyMethod(startOfRoundPostfix));

                // Patch EnemyAI.Start to catch newly spawned enemies
                var enemyAIStartMethod = AccessTools.Method(typeof(EnemyAI), "Start");
                var enemyAIStartPostfix = AccessTools.Method(typeof(Patches), nameof(EnemyAIStartPostfix));
                harmony.Patch(enemyAIStartMethod, null, new HarmonyMethod(enemyAIStartPostfix));

                // NETWORK HITS: Client side - intercept hits from the local client
                var hitLocalMethod = AccessTools.Method(typeof(EnemyAI), "HitEnemyOnLocalClient");
                var hitLocalPrefix = AccessTools.Method(typeof(Patches), nameof(HitEnemyOnLocalClientPrefix));
                harmony.Patch(hitLocalMethod, new HarmonyMethod(hitLocalPrefix));

                // NETWORK HITS: Server side - handle the RPC from clients
                var hitServerMethod = AccessTools.Method(typeof(EnemyAI), "HitEnemyServerRpc");
                var hitServerPrefix = AccessTools.Method(typeof(Patches), nameof(HitEnemyServerRpcPrefix));
                harmony.Patch(hitServerMethod, new HarmonyMethod(hitServerPrefix));

                // NETWORK HITS: Client side - sync back from server to clients
                var hitClientMethod = AccessTools.Method(typeof(EnemyAI), "HitEnemyClientRpc");
                var hitClientPrefix = AccessTools.Method(typeof(Patches), nameof(HitEnemyClientRpcPrefix));
                harmony.Patch(hitClientMethod, new HarmonyMethod(hitClientPrefix));

                // DIRECT HITS: Handle direct calls to HitEnemy
                var hitEnemyMethod = AccessTools.Method(typeof(EnemyAI), "HitEnemy");
                var hitEnemyPrefix = AccessTools.Method(typeof(Patches), nameof(HitEnemyPrefix));
                harmony.Patch(hitEnemyMethod, new HarmonyMethod(hitEnemyPrefix));

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
                Plugin.Log.LogInfo("Game starting, initializing enemy health system...");

                // Clear our tracking
                HealthManager.Initialize();

                // Find all available enemy types in the game
                Plugin.enemies = new List<EnemyType>(Resources.FindObjectsOfTypeAll<EnemyType>());
                Plugin.Log.LogInfo($"Found {Plugin.enemies.Count} enemy types");

                // Load config for all enemy types
                foreach (var enemyType in Plugin.enemies)
                {
                    string sanitizedName = Plugin.RemoveInvalidCharacters(enemyType.enemyName).ToUpper();
                    Plugin.CanMob(".Unimmortal", sanitizedName); // This will create config if it doesn't exist
                    Plugin.GetMobHealth(sanitizedName, 3); // Default health value of 3
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
                    HealthManager.SetupEnemy(enemy);
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
                HealthManager.SetupEnemy(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in EnemyAIStartPostfix: {ex}");
            }
        }

        // LOCAL CLIENT HIT: Client-side hit detection
        public static bool HitEnemyOnLocalClientPrefix(EnemyAI __instance, int force, Vector3 hitDirection, PlayerControllerB playerWhoHit, bool playHitSFX, int hitID)
        {
            try
            {
                // If we're the host, handle the hit directly
                if (StartOfRound.Instance.IsHost)
                {
                    Plugin.Log.LogInfo($"Local hit on {__instance.enemyType.enemyName} as host");
                    HealthManager.DamageEnemy(__instance, force, playerWhoHit, playHitSFX, hitID);
                }
                else
                {
                    Plugin.Log.LogInfo($"Local hit on {__instance.enemyType.enemyName} as client - sending to host");
                    // Let original method send the RPC to the host
                }

                // Continue with the original method to handle effects and network communication
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in HitEnemyOnLocalClientPrefix: {ex}");
                return true;
            }
        }

        // NETWORK HITS: Server side - handle the RPC from clients
        public static bool HitEnemyServerRpcPrefix(EnemyAI __instance, int force, int playerWhoHit, bool playHitSFX, int hitID)
        {
            try
            {
                // Always process on the host, regardless of enemy ownership
                if (StartOfRound.Instance.IsHost)
                {
                    Plugin.Log.LogInfo($"Host received hit RPC for {__instance.enemyType.enemyName}");

                    // Get the player who hit the enemy
                    PlayerControllerB player = null;
                    if (playerWhoHit >= 0 && playerWhoHit < StartOfRound.Instance.allPlayerScripts.Length)
                    {
                        player = StartOfRound.Instance.allPlayerScripts[playerWhoHit];
                    }

                    // Check if we need to force-kill a 0-health enemy
                    int currentHealth = HealthManager.GetEnemyHealth(__instance);
                    if (currentHealth <= 0 && !__instance.isEnemyDead)
                    {
                        Plugin.Log.LogInfo($"Force-killing {__instance.enemyType.enemyName} that has 0 health but isn't dead");

                        // Force ownership to host and kill with destruction
                        ulong hostId = StartOfRound.Instance.allPlayerScripts[0].actualClientId;
                        __instance.ChangeOwnershipOfEnemy(hostId);
                        __instance.KillEnemyOnOwnerClient(true);
                    }
                    else
                    {
                        // Apply normal damage with our system
                        HealthManager.DamageEnemy(__instance, force, player, playHitSFX, hitID);
                    }
                }

                // Continue with original method to sync to other clients
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in HitEnemyServerRpcPrefix: {ex}");
                return true;
            }
        }

        // CLIENT RPC HIT: Clients receive hit confirmations from the host
        public static bool HitEnemyClientRpcPrefix(EnemyAI __instance, int force, int playerWhoHit, bool playHitSFX, int hitID)
        {
            try
            {
                // Skip for the host since they already processed this
                if (__instance.IsOwner) return true;

                Plugin.Log.LogInfo($"Client received hit sync for {__instance.enemyType.enemyName}");

                // We let the original method handle visual effects and animations for clients
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in HitEnemyClientRpcPrefix: {ex}");
                return true;
            }
        }

        // DIRECT HIT: Processing direct HitEnemy calls
        // DIRECT HIT: Processing direct HitEnemy calls
        public static bool HitEnemyPrefix(EnemyAI __instance, int force, PlayerControllerB playerWhoHit, bool playHitSFX, int hitID)
        {
            try
            {
                // Only process on host, let clients use the network sync
                if (!StartOfRound.Instance.IsHost) return true;

                Plugin.Log.LogInfo($"Direct HitEnemy called for {__instance.enemyType.enemyName} (game HP: {__instance.enemyHP})");

                // If game HP isn't 999, force it to be
                if (__instance.enemyHP != 999 && !__instance.isEnemyDead)
                {
                    // Just reset it silently
                    __instance.enemyHP = 999;
                }

                // Apply our damage
                bool wasProcessed = HealthManager.DamageEnemy(__instance, force, playerWhoHit, playHitSFX, hitID);

                // Always run some of the original method for effects, but don't let it change HP or kill
                __instance.creatureAnimator?.SetTrigger("damage");
                if (playHitSFX && __instance.enemyType.hitBodySFX != null)
                {
                    __instance.creatureSFX?.PlayOneShot(__instance.enemyType.hitBodySFX);
                }

                // Skip the rest of the original method
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in HitEnemyPrefix: {ex}");
                return true;
            }
        }
    }
}
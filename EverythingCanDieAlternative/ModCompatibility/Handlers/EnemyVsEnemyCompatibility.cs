using System;
using System.Reflection;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace EverythingCanDieAlternative.ModCompatibility.Handlers
{
    // Compatibility handler for enemy-vs-enemy combat - handles interactions between different enemy types to ensure damage is properly tracked
    public class EnemyVsEnemyCompatibility : BaseModCompatibility
    {
        public override string ModId => "nwnt.EnemyVsEnemyCompat";
        public override string ModName => "Enemy Vs Enemy Combat";

        // Always enabled since this is part of the base mod
        public override bool IsInstalled => true;

        // Store original method for detour
        private static MethodInfo originalHitEnemyMethod;

        protected override void OnModInitialize()
        {
            //Plugin.LogInfo($"Initializing {ModName} compatibility");

            try
            {
                // Apply our patch to EnemyAI.HitEnemy
                PatchHitEnemyMethod();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to initialize {ModName} compatibility: {ex.Message}");
                Plugin.Log.LogError(ex.StackTrace);
            }
        }

        private void PatchHitEnemyMethod()
        {
            // Get the original method for EnemyAI.HitEnemy
            originalHitEnemyMethod = AccessTools.Method(typeof(EnemyAI), "HitEnemy",
                new Type[] { typeof(int), typeof(PlayerControllerB), typeof(bool), typeof(int) });

            if (originalHitEnemyMethod == null)
            {
                Plugin.Log.LogError("Could not find EnemyAI.HitEnemy method for patching");
                return;
            }

            // Create our Harmony patch
            try
            {
                Plugin.Harmony.Patch(
                    originalHitEnemyMethod,
                    prefix: new HarmonyMethod(typeof(EnemyVsEnemyCompatibility), nameof(HitEnemyPrefix))
                );

                //Plugin.LogInfo($"Successfully patched EnemyAI.HitEnemy for {ModName}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to patch EnemyAI.HitEnemy: {ex.Message}");
            }
        }

        // Prefix patch for EnemyAI.HitEnemy to intercept all damage
        public static bool HitEnemyPrefix(EnemyAI __instance, int force, PlayerControllerB playerWhoHit, bool playHitSFX, int hitID)
        {
            try
            {
                // Check if this is HexiBetterShotgun damage
                var hexiHandler = ModCompatibilityManager.Instance.GetHandler<ModCompatibility.Handlers.HexiBetterShotgunCompatibility>("HexiBetterShotgun");
                if (hexiHandler != null && hexiHandler.IsInstalled && 
                    HexiBetterShotgunCompatibility.IsHexiBetterShotgunDamage(__instance, force, playerWhoHit))
                {
                    Plugin.Log.LogInfo($"HexiBetterShotgun damage detected: {force} to {__instance.enemyType.enemyName}");
                    HealthManager.DirectHealthChange(__instance, force, playerWhoHit);
                    return true; // Let original run for sound effects
                }

                // Check if this hit has no player (enemy vs enemy damage)
                if (playerWhoHit == null)
                {
                    Plugin.Log.LogInfo($"Enemy vs enemy hit detected on {__instance.enemyType.enemyName} with force {force}");
                    HealthManager.ProcessHit(__instance, force, null);
                }

                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in HitEnemyPrefix: {ex.Message}");
                return true;
            }
        }
    }
}
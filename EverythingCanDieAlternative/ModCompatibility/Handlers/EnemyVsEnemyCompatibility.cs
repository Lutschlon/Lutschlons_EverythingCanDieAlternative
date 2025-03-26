using System;
using System.Reflection;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace EverythingCanDieAlternative.ModCompatibility.Handlers
{
    /// <summary>
    /// Compatibility handler for enemy-vs-enemy combat
    /// This handles interactions between different enemy types to ensure damage is properly tracked
    /// </summary>
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
            Plugin.Log.LogInfo($"Initializing {ModName} compatibility");

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

                Plugin.Log.LogInfo($"Successfully patched EnemyAI.HitEnemy for {ModName}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to patch EnemyAI.HitEnemy: {ex.Message}");
            }
        }

        /// <summary>
        /// Prefix patch for EnemyAI.HitEnemy to intercept all damage
        /// </summary>
        public static bool HitEnemyPrefix(EnemyAI __instance, int force, PlayerControllerB playerWhoHit, bool playHitSFX, int hitID)
        {
            try
            {
                // Skip processing if the enemy is already dead
                if (__instance == null || __instance.isEnemyDead) return true;

                // When playerWhoHit is null, this is likely enemy-vs-enemy damage
                if (playerWhoHit == null)
                {
                    Plugin.Log.LogInfo($"Enemy vs enemy hit detected on {__instance.enemyType.enemyName} with force {force}");

                    // Process the hit with our networked health system
                    NetworkedHealthManager.ProcessHit(__instance, force, null);

                    // Let vanilla method run for sound effects, but we've already handled damage
                    return true;
                }

                // For player hits, let the existing HitEnemyOnLocalClient patch handle it
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in HitEnemyPrefix: {ex.Message}");
                Plugin.Log.LogError(ex.StackTrace);
                return true; // Always continue to original method on error
            }
        }
    }
}
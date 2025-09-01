using System;
using GameNetcodeStuff;

namespace EverythingCanDieAlternative.ModCompatibility.Handlers
{
    public class HexiBetterShotgunCompatibility : BaseModCompatibility
    {
        public override string ModId => "HexiBetterShotgun";
        public override string ModName => "HexiBetterShotgun";

        public override bool IsInstalled 
        { 
            get
            {
                try
                {
                    return BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("HexiBetterShotgun");
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"Error detecting {ModName}: {ex.Message}");
                    return false;
                }
            }
        }

        protected override void OnModInitialize()
        {
            Plugin.LogInfo($"{ModName} compatibility initialized (detection-only mode)");
            // No patching needed - we'll detect shotgun damage in EnemyVsEnemyCompatibility
        }

        /// <summary>
        /// Check if this hit is from HexiBetterShotgun based on the player holding a shotgun
        /// </summary>
        public static bool IsHexiBetterShotgunDamage(EnemyAI enemy, int damage, PlayerControllerB playerWhoHit)
        {
            if (playerWhoHit == null) return false;

            // Check if player is holding a shotgun
            if (playerWhoHit.currentlyHeldObjectServer is ShotgunItem shotgun)
            {
                // Additional check: HexiBetterShotgun typically does damage in specific ranges
                // It does damage = pelletCount / 2 + 1, so damage should be 1-6 typically
                if (damage >= 1 && damage <= 10) // Reasonable range for shotgun pellet damage
                {
                    Plugin.Log.LogInfo($"Detected HexiBetterShotgun damage: {damage} to {enemy.enemyType.enemyName}");
                    return true;
                }
            }

            return false;
        }
    }
}
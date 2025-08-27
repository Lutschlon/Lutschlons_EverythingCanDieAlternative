using System;
using System.Linq;
using System.Reflection;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace EverythingCanDieAlternative.ModCompatibility.Handlers
{
    public class LethalMinCompatibility : BaseModCompatibility
    {
        public override string ModId => "NoteBoxz.LethalMin";
        public override string ModName => "LethalMin";

        // Override IsInstalled to avoid the problematic reflection
        public override bool IsInstalled 
        { 
            get
            {
                try
                {
                    return BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("NoteBoxz.LethalMin");
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"Error detecting {ModName}: {ex.Message}");
                    return false;
                }
            }
        }

        private Type _pikminEnemyType;
        private Type _leaderType;
        private bool _patchApplied = false;

        protected override void OnModInitialize()
        {
            Plugin.LogInfo($"{ModName} compatibility initialized");

            try
            {
                var lethalMinPlugin = BepInEx.Bootstrap.Chainloader.PluginInfos["NoteBoxz.LethalMin"];
                var lethalMinAssembly = lethalMinPlugin.Instance.GetType().Assembly;

                _pikminEnemyType = lethalMinAssembly.GetType("LethalMin.PikminEnemy");
                _leaderType = lethalMinAssembly.GetType("LethalMin.Leader");

                if (_pikminEnemyType == null || _leaderType == null)
                {
                    Plugin.Log.LogWarning($"Could not find required {ModName} types");
                    return;
                }

                ApplyPatches();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to initialize {ModName} compatibility: {ex.Message}");
            }
        }

        private void ApplyPatches()
        {
            try
            {
                var damageEnemyMethod = _pikminEnemyType.GetMethod("DamageEnemy",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new Type[] { typeof(float), _leaderType },
                    null);

                if (damageEnemyMethod == null)
                {
                    Plugin.Log.LogWarning($"Could not find DamageEnemy method in {ModName}");
                    return;
                }

                Plugin.Harmony.Patch(
                    damageEnemyMethod,
                    postfix: new HarmonyMethod(typeof(LethalMinCompatibility), nameof(DamageEnemyPostfix))
                );

                _patchApplied = true;
                Plugin.LogInfo($"Successfully patched {ModName} DamageEnemy method with postfix");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to patch {ModName} methods: {ex.Message}");
            }
        }

        [HarmonyPostfix]
        static void DamageEnemyPostfix(object __instance, float Counter, object pikminLeader)
        {
            try
            {
                var handler = ModCompatibilityManager.Instance.GetHandler<LethalMinCompatibility>("NoteBoxz.LethalMin");
                if (handler == null || !handler._patchApplied) return;

                var enemyScriptField = handler._pikminEnemyType.GetField("enemyScript");
                if (!(enemyScriptField?.GetValue(__instance) is EnemyAI enemyAI)) return;

                if (enemyAI.isEnemyDead) return;

                // Only process if counter >= 1 (same logic as LethalMin)
                int damageToApply = Mathf.FloorToInt(Counter);
                if (damageToApply <= 0) return;

                // Get the player who caused the damage
                PlayerControllerB playerWhoHit = null;
                if (pikminLeader != null)
                {
                    var controllerProperty = handler._leaderType.GetProperty("Controller");
                    playerWhoHit = controllerProperty?.GetValue(pikminLeader) as PlayerControllerB;
                }

                Plugin.LogInfo($"LethalMin damage: {damageToApply} to {enemyAI.enemyType.enemyName} from {(playerWhoHit?.playerUsername ?? "pikmin")}");

                // Directly modify network health - bypasses all base game hit logic
                HealthManager.DirectHealthChange(enemyAI, damageToApply, playerWhoHit);

                Plugin.LogInfo($"LethalMin damage applied directly to network health");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in LethalMin postfix: {ex.Message}");
            }
        }


        public bool IsPatchWorking() => _patchApplied;
    }
}
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using BepInEx.Bootstrap;

namespace EverythingCanDieAlternative.ModCompatibility.Handlers
{
    // Compatibility handler for NaturalSelection (fandovec03.NaturalSelection).
    //
    // NaturalSelection has three enemy-vs-enemy damage paths that bypass EnemyAI.HitEnemy
    // entirely, making them invisible to EnemyVsEnemyCompatibility's HitEnemy prefix:
    //
    //   1. HoarderBugPatch.CustomOnHit   — Blob attacking HoarderBug: does enemyHP -= force
    //   2. PufferAIPatch.CustomOnHit     — Blob/Spider attacking Puffer: does enemyHP -= force
    //   3. ForestGiantPatch.UpdatePrefix — Giant burning mechanic: does enemyHP -= 20
    //
    // All other NaturalSelection combat uses HitEnemy(force, null, ...) which is already
    // intercepted by EnemyVsEnemyCompatibility. This handler fills the remaining three gaps.
    public class NaturalSelectionCompatibility : BaseModCompatibility
    {
        public override string ModId   => "fandovec03.NaturalSelection";
        public override string ModName => "NaturalSelection";

        public override bool IsInstalled
        {
            get
            {
                try   { return Chainloader.PluginInfos.ContainsKey("fandovec03.NaturalSelection"); }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"Error detecting {ModName}: {ex.Message}");
                    return false;
                }
            }
        }

        protected override void OnModInitialize()
        {
            try
            {
                var pluginInfo = Chainloader.PluginInfos["fandovec03.NaturalSelection"];
                Assembly assembly = pluginInfo.Instance.GetType().Assembly;

                PatchHoarderBugCustomOnHit(assembly);
                PatchPufferCustomOnHit(assembly);
                PatchForestGiantUpdate();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to initialize {ModName} compatibility: {ex.Message}");
                Plugin.Log.LogError(ex.StackTrace);
            }
        }

        // ── Patching helpers ─────────────────────────────────────────────────────────

        private void PatchHoarderBugCustomOnHit(Assembly assembly)
        {
            // NaturalSelection.EnemyPatches.HoarderBugPatch.CustomOnHit
            // Signature: static void CustomOnHit(int force, EnemyAI enemyWhoHit, bool playHitSFX, HoarderBugAI __instance)
            Type type = assembly.GetType("NaturalSelection.EnemyPatches.HoarderBugPatch");
            if (type == null)
            {
                Plugin.Log.LogWarning($"[{ModName}] Could not find HoarderBugPatch type — skipping HoarderBug patch");
                return;
            }

            MethodInfo method = AccessTools.Method(type, "CustomOnHit");
            if (method == null)
            {
                Plugin.Log.LogWarning($"[{ModName}] Could not find HoarderBugPatch.CustomOnHit — skipping HoarderBug patch");
                return;
            }

            Plugin.Harmony.Patch(method,
                prefix: new HarmonyMethod(typeof(NaturalSelectionCompatibility), nameof(HoarderBugCustomOnHitPrefix)));

            Plugin.Log.LogInfo($"[{ModName}] Patched HoarderBugPatch.CustomOnHit");
        }

        private void PatchPufferCustomOnHit(Assembly assembly)
        {
            // NaturalSelection.EnemyPatches.PufferAIPatch.CustomOnHit
            // Signature: static void CustomOnHit(int force, EnemyAI enemyWhoHit, bool playHitSFX, PufferAI instance)
            Type type = assembly.GetType("NaturalSelection.EnemyPatches.PufferAIPatch");
            if (type == null)
            {
                Plugin.Log.LogWarning($"[{ModName}] Could not find PufferAIPatch type — skipping Puffer patch");
                return;
            }

            MethodInfo method = AccessTools.Method(type, "CustomOnHit");
            if (method == null)
            {
                Plugin.Log.LogWarning($"[{ModName}] Could not find PufferAIPatch.CustomOnHit — skipping Puffer patch");
                return;
            }

            Plugin.Harmony.Patch(method,
                prefix: new HarmonyMethod(typeof(NaturalSelectionCompatibility), nameof(PufferCustomOnHitPrefix)));

            Plugin.Log.LogInfo($"[{ModName}] Patched PufferAIPatch.CustomOnHit");
        }

        private void PatchForestGiantUpdate()
        {
            // NaturalSelection's ForestGiantPatch.UpdatePrefix subtracts enemyHP -= 20 inside
            // ForestGiantAI.Update when the burning-extinguish mechanic fires. We can't access
            // NaturalSelection's internal GiantData to detect when that condition is active, so
            // we snapshot enemyHP before Update and compare in the postfix. The only thing that
            // modifies ForestGiantAI.enemyHP inside Update is NaturalSelection, so the delta is
            // always attributable to NaturalSelection enemy-vs-enemy (burning) damage.
            MethodInfo updateMethod = AccessTools.Method(typeof(ForestGiantAI), "Update");
            if (updateMethod == null)
            {
                Plugin.Log.LogWarning($"[{ModName}] Could not find ForestGiantAI.Update — skipping ForestGiant patch");
                return;
            }

            Plugin.Harmony.Patch(updateMethod,
                prefix:  new HarmonyMethod(typeof(NaturalSelectionCompatibility), nameof(ForestGiantUpdatePrefix)),
                postfix: new HarmonyMethod(typeof(NaturalSelectionCompatibility), nameof(ForestGiantUpdatePostfix)));

            Plugin.Log.LogInfo($"[{ModName}] Patched ForestGiantAI.Update for burning-HP monitoring");
        }

        // ── Patch implementations ─────────────────────────────────────────────────────

        // Intercepts HoarderBugPatch.CustomOnHit before it subtracts enemyHP directly.
        // Positional injection: __0 = force, __3 = the HoarderBugAI being hit.
        // (NaturalSelection names the 4th param "__instance" but this is just a local
        //  parameter name in a static method — Harmony maps __N to the Nth argument.)
        public static void HoarderBugCustomOnHitPrefix(int __0, HoarderBugAI __3)
        {
            try
            {
                EnemyAI target = __3 as EnemyAI;
                if (target == null || target.isEnemyDead || __0 <= 0) return;

                Plugin.LogInfo($"[NaturalSelection] HoarderBug hit detected: {__0} damage from enemy-vs-enemy");
                HealthManager.ProcessHit(target, __0, null);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[NaturalSelection] HoarderBugCustomOnHitPrefix error: {ex.Message}");
            }
        }

        // Intercepts PufferAIPatch.CustomOnHit before it subtracts enemyHP directly.
        // Positional injection: __0 = force, __3 = the PufferAI being hit.
        public static void PufferCustomOnHitPrefix(int __0, PufferAI __3)
        {
            try
            {
                EnemyAI target = __3 as EnemyAI;
                if (target == null || target.isEnemyDead || __0 <= 0) return;

                Plugin.LogInfo($"[NaturalSelection] Puffer hit detected: {__0} damage from enemy-vs-enemy");
                HealthManager.ProcessHit(target, __0, null);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[NaturalSelection] PufferCustomOnHitPrefix error: {ex.Message}");
            }
        }

        // ForestGiant HP monitoring: NaturalSelection may subtract enemyHP directly during burning.
        // We snapshot HP before Update, then detect any decrease in the postfix.
        private static readonly Dictionary<int, int> _forestGiantHpSnapshot = new Dictionary<int, int>();

        public static void ForestGiantUpdatePrefix(ForestGiantAI __instance)
        {
            try
            {
                EnemyAI enemy = __instance as EnemyAI;
                if (enemy == null || enemy.isEnemyDead) return;
                _forestGiantHpSnapshot[__instance.GetInstanceID()] = enemy.enemyHP;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[NaturalSelection] ForestGiantUpdatePrefix error: {ex.Message}");
            }
        }

        public static void ForestGiantUpdatePostfix(ForestGiantAI __instance)
        {
            try
            {
                int id = __instance.GetInstanceID();
                if (!_forestGiantHpSnapshot.TryGetValue(id, out int before)) return;

                EnemyAI enemy = __instance as EnemyAI;
                if (enemy == null || enemy.isEnemyDead) return;

                int delta = before - enemy.enemyHP;
                if (delta > 0)
                {
                    Plugin.LogInfo($"[NaturalSelection] ForestGiant burning mechanic: {delta} HP removed (routing to ECDA)");
                    HealthManager.ProcessHit(enemy, delta, null);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[NaturalSelection] ForestGiantUpdatePostfix error: {ex.Message}");
            }
        }
    }
}

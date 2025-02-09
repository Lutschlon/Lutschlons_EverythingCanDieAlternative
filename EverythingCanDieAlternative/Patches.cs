using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;

namespace EverythingCanDie
{
    internal class Patches
    {
        private static readonly Dictionary<string, int> currentHealthValues = new Dictionary<string, int>();
        private static readonly List<string> DamagableEnemies = new List<string>();
        private static readonly List<string> InvalidEnemies = new List<string>();
        private static readonly int Damage = Animator.StringToHash("damage");
        private static readonly Dictionary<EnemyAI, float> lastHitTime = new Dictionary<EnemyAI, float>();
        private const float HIT_COOLDOWN = 0.1f; // 100ms cooldown between hits

        public static void StartOfRoundPatch()
        {
            try
            {
                Plugin.Log.LogInfo("Starting enemies setup...");

                // Patch for local client hits
                var hitLocalMethod = typeof(EnemyAI).GetMethod("HitEnemyOnLocalClient",
                    BindingFlags.Public | BindingFlags.Instance);

                if (hitLocalMethod != null)
                {
                    Plugin.Log.LogInfo("Found HitEnemyOnLocalClient method, creating patch");
                    Plugin.Harmony.Patch(hitLocalMethod,
                        prefix: new HarmonyMethod(typeof(Patches).GetMethod(nameof(HitEnemyOnLocalClientPatch),
                            BindingFlags.Static | BindingFlags.Public)));
                }

                // Patch for server-side hit registration
                var serverRpcMethod = typeof(EnemyAI).GetMethod("HitEnemyServerRpc",
                    BindingFlags.Public | BindingFlags.Instance);
                if (serverRpcMethod != null)
                {
                    Plugin.Log.LogInfo("Found HitEnemyServerRpc method, creating patch");
                    Plugin.Harmony.Patch(serverRpcMethod,
                        prefix: new HarmonyMethod(typeof(Patches).GetMethod(nameof(HitEnemyServerRpcPatch),
                            BindingFlags.Static | BindingFlags.Public)));
                }

                // Patch for client-side hit synchronization
                var clientRpcMethod = typeof(EnemyAI).GetMethod("HitEnemyClientRpc",
                    BindingFlags.Public | BindingFlags.Instance);
                if (clientRpcMethod != null)
                {
                    Plugin.Log.LogInfo("Found HitEnemyClientRpc method, creating patch");
                    Plugin.Harmony.Patch(clientRpcMethod,
                        prefix: new HarmonyMethod(typeof(Patches).GetMethod(nameof(HitEnemyClientRpcPatch),
                            BindingFlags.Static | BindingFlags.Public)));
                }

                // Find all EnemyTypes
                Plugin.enemies = UnityEngine.Resources.FindObjectsOfTypeAll(typeof(EnemyType))
                    .Cast<EnemyType>()
                    .Where(e => e != null)
                    .ToList();

                Plugin.items = UnityEngine.Resources.FindObjectsOfTypeAll(typeof(Item))
                    .Cast<Item>()
                    .Where(i => i != null)
                    .ToList();

                // Find all active enemies in the scene
                var activeEnemies = UnityEngine.Object.FindObjectsOfType<EnemyAI>();
                ProcessEnemies(activeEnemies);

                // Find inactive enemies as well
                var inactiveEnemies = UnityEngine.Resources.FindObjectsOfTypeAll<EnemyAI>();
                ProcessEnemies(inactiveEnemies);

                Plugin.Log.LogInfo($"Setup complete. Damageable enemies: {string.Join(", ", DamagableEnemies)}");
                if (InvalidEnemies.Any())
                {
                    Plugin.Log.LogWarning($"Invalid enemies: {string.Join(", ", InvalidEnemies)}");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Error in StartOfRoundPatch: {e}");
            }
        }

        private static void ProcessEnemies(EnemyAI[] enemies)
        {
            foreach (EnemyAI enemy in enemies)
            {
                if (enemy?.enemyType == null) continue;

                string mobName = Plugin.RemoveInvalidCharacters(enemy.enemyType.enemyName).ToUpper();
                try
                {
                    Plugin.Log.LogInfo($"Processing enemy: {enemy.enemyType.enemyName} (Type: {enemy.GetType().Name})");

                    // Configure enemy immortality
                    if (!Plugin.Instance.Config.ContainsKey(new ConfigDefinition("Mobs", mobName + ".Unimmortal")))
                    {
                        Plugin.Instance.Config.Bind("Mobs",
                            mobName + ".Unimmortal",
                            true,
                            "If true this mob will be damageable");
                    }

                    // Configure health
                    ConfigEntry<int> healthConfig;
                    if (!Plugin.Instance.Config.ContainsKey(new ConfigDefinition("Mobs", mobName + ".Health")))
                    {
                        healthConfig = Plugin.Instance.Config.Bind("Mobs",
                            mobName + ".Health",
                            enemy.enemyHP,
                            "The value of the mobs health");
                    }
                    else
                    {
                        healthConfig = Plugin.Instance.Config.Bind<int>("Mobs",
                            mobName + ".Health",
                            enemy.enemyHP,
                            "The value of the mobs health");
                    }

                    // Check if enemy should be damageable
                    if (Plugin.CanMob(".Unimmortal", mobName))
                    {
                        enemy.enemyType.canDie = true;
                        enemy.enemyHP = healthConfig.Value;
                        string enemyKey = $"{enemy.enemyType.enemyName}_{enemy.GetInstanceID()}";
                        currentHealthValues[enemyKey] = enemy.enemyHP;

                        if (!DamagableEnemies.Contains(enemy.enemyType.enemyName))
                        {
                            DamagableEnemies.Add(enemy.enemyType.enemyName);
                            Plugin.Log.LogInfo($"Made {enemy.enemyType.enemyName} damageable");
                            Plugin.Log.LogInfo($"Set HP to {enemy.enemyHP} (Config value: {healthConfig.Value}) - Instance ID: {enemy.GetInstanceID()}");

                            var hittables = enemy.GetComponentsInChildren<IHittable>();
                            Plugin.Log.LogInfo($"Enemy {enemy.enemyType.enemyName} has {hittables.Length} IHittable components");
                        }
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Error configuring enemy {enemy.enemyType.enemyName}: {e.Message}");
                    if (!InvalidEnemies.Contains(enemy.enemyType.enemyName))
                    {
                        InvalidEnemies.Add(enemy.enemyType.enemyName);
                    }
                }
            }
        }

        public static bool HitPrefixPatch(IHittable __instance, int force, Vector3 hitDirection, PlayerControllerB playerWhoHit, bool playHitSFX = false, int hitID = -1)
        {
            try
            {
                // Try to get the EnemyAI component from the instance
                EnemyAI enemy = null;
                if (__instance is Component component)
                {
                    enemy = component.GetComponent<EnemyAI>();
                    if (enemy == null)
                    {
                        enemy = component.GetComponentInParent<EnemyAI>();
                    }
                }

                if (enemy != null)
                {
                    Plugin.Log.LogInfo($"[EverythingCanDie] Intercepted IHittable.Hit for {enemy.enemyType.enemyName}");
                    // Process the hit using our existing logic
                    HitEnemyPatch(ref enemy, force, playerWhoHit, playHitSFX, hitID);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Error in HitPrefixPatch: {e}");
            }

            // Always return true to allow the original hit to proceed
            return true;
        }

        public static bool HitEnemyOnLocalClientPatch(EnemyAI __instance, int force, Vector3 hitDirection, PlayerControllerB playerWhoHit, bool playHitSFX, int hitID)
        {
            try
            {
                Plugin.Log.LogInfo($"[EverythingCanDie] Intercepted local client hit on {__instance.enemyType.enemyName}");
                HitEnemyPatch(ref __instance, force, playerWhoHit, playHitSFX, hitID);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Error in HitEnemyOnLocalClientPatch: {e}");
            }

            return true;
        }

        public static bool HitEnemyServerRpcPatch(EnemyAI __instance, int force, int playerWhoHit, bool playHitSFX, int hitID)
        {
            try
            {
                if (__instance?.IsOwner == false) // Only process on server/host
                {
                    Plugin.Log.LogInfo($"[EverythingCanDie] Intercepted server RPC hit on {__instance.enemyType.enemyName}");
                    PlayerControllerB player =
                        (playerWhoHit >= 0 && playerWhoHit < StartOfRound.Instance.allPlayerScripts.Length)
                            ? StartOfRound.Instance.allPlayerScripts[playerWhoHit]
                            : null;
                    HitEnemyPatch(ref __instance, force, player, playHitSFX, hitID);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Error in HitEnemyServerRpcPatch: {e}");
            }

            return true;
        }

        public static bool HitEnemyClientRpcPatch(EnemyAI __instance, int force, int playerWhoHit, bool playHitSFX, int hitID)
        {
            try
            {
                ulong? localClientId = GameNetworkManager.Instance?.localPlayerController?.playerClientId;
                // Only process on clients that aren't the hitting player
                if (localClientId.HasValue && localClientId.Value != (ulong)playerWhoHit)
                {
                    Plugin.Log.LogInfo($"[EverythingCanDie] Intercepted client RPC hit on {__instance.enemyType.enemyName}");
                    PlayerControllerB player =
                        (playerWhoHit >= 0 && playerWhoHit < StartOfRound.Instance.allPlayerScripts.Length)
                            ? StartOfRound.Instance.allPlayerScripts[playerWhoHit]
                            : null;
                    HitEnemyPatch(ref __instance, force, player, playHitSFX, hitID);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Error in HitEnemyClientRpcPatch: {e}");
            }

            return true;
        }

        private static void HandleConfigReload()
        {
            Plugin.Log.LogInfo("Config reload handler triggered");
            try
            {
                var allEnemies = UnityEngine.Object.FindObjectsOfType<EnemyAI>();
                Plugin.Log.LogInfo($"Found {allEnemies.Length} active enemies to update");

                foreach (var enemy in allEnemies)
                {
                    if (enemy?.enemyType == null) continue;

                    string mobName = Plugin.RemoveInvalidCharacters(enemy.enemyType.enemyName).ToUpper();
                    string enemyKey = $"{enemy.enemyType.enemyName}_{enemy.GetInstanceID()}";

                    if (Plugin.Instance.Config.TryGetEntry<int>(new ConfigDefinition("Mobs", mobName + ".Health"), out var healthConfig))
                    {
                        int oldHP = currentHealthValues.ContainsKey(enemyKey) ? currentHealthValues[enemyKey] : enemy.enemyHP;
                        int configuredHP = healthConfig.Value;

                        if (oldHP > 0 && enemy.enemyHP < oldHP)
                        {
                            float healthPercentage = (float)enemy.enemyHP / oldHP;
                            enemy.enemyHP = Mathf.RoundToInt(configuredHP * healthPercentage);
                        }
                        else
                        {
                            enemy.enemyHP = configuredHP;
                        }

                        currentHealthValues[enemyKey] = configuredHP;
                        Plugin.Log.LogInfo($"Updated {enemy.enemyType.enemyName} HP from {oldHP} to {enemy.enemyHP} (Config: {configuredHP}) - Instance ID: {enemy.GetInstanceID()}");
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Error during config reload: {e}");
            }
        }

        public static void HitEnemyPatch(ref EnemyAI __instance, int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
        {
            try
            {
                if (__instance == null) return;

                if (lastHitTime.TryGetValue(__instance, out float lastHit))
                {
                    if (Time.time - lastHit < HIT_COOLDOWN)
                    {
                        return;
                    }
                }
                lastHitTime[__instance] = Time.time;

                string enemyName = __instance.enemyType.enemyName;
                Plugin.Log.LogInfo($"[EverythingCanDie] Processing hit on {enemyName} with force {force}");

                if (__instance.isEnemyDead || InvalidEnemies.Contains(enemyName))
                {
                    Plugin.Log.LogInfo($"[EverythingCanDie] Enemy {enemyName} is dead or invalid, skipping");
                    return;
                }

                if (__instance.enabled != true || __instance.isEnemyDead)
                {
                    return;
                }

                string name = Plugin.RemoveInvalidCharacters(__instance.enemyType.enemyName).ToUpper();
                bool canDamage = Plugin.CanMob(".Unimmortal", name);
                if (!canDamage)
                {
                    Plugin.Log.LogInfo($"[EverythingCanDie] {enemyName} is not damageable");
                    return;
                }

                if (__instance.creatureAnimator != null)
                {
                    __instance.creatureAnimator.SetTrigger(Damage);
                }

                int oldHP = __instance.enemyHP;
                __instance.enemyHP = Math.Max(0, __instance.enemyHP - force);
                Plugin.Log.LogInfo($"[EverythingCanDie] Hit {enemyName}: HP {oldHP} -> {__instance.enemyHP}");

                if (__instance.enemyHP <= 0)
                {
                    Plugin.Log.LogInfo($"[EverythingCanDie] Killing {enemyName}");
                    __instance.KillEnemyOnOwnerClient(false);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[EverythingCanDie] Error in HitEnemyPatch: {e}");
            }
        }

        public static void KillEnemyPatch(ref EnemyAI __instance)
        {
            if (__instance != null && !InvalidEnemies.Contains(__instance.enemyType.enemyName))
            {
                Plugin.Log.LogInfo($"[EverythingCanDie] {__instance.enemyType.enemyName} died");
            }
        }
    }
}
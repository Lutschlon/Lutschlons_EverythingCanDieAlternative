using BepInEx.Configuration;
using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;

namespace EverythingCanDie
{
    public class Patches
    {
        public static List<string> DamagableEnemies = new List<string>();
        public static List<string> InvalidEnemies = new List<string>();
        private static readonly int Damage = Animator.StringToHash("damage");

        public static void StartOfRoundPatch()
        {
            Plugin.enemies = Resources.FindObjectsOfTypeAll(typeof(EnemyType)).Cast<EnemyType>().Where(e => e != null).ToList();
            Plugin.items = Resources.FindObjectsOfTypeAll(typeof(Item)).Cast<Item>().Where(i => i != null).ToList();

            foreach (EnemyType enemy in Plugin.enemies)
            {
                string mobName = Plugin.RemoveInvalidCharacters(enemy.enemyName).ToUpper();

                try
                {
                    if (!Plugin.Instance.Config.ContainsKey(new ConfigDefinition("Mobs", mobName + ".Unimmortal")))
                    {
                        Plugin.Instance.Config.Bind("Mobs",
                                            mobName + ".Unimmortal",
                                            true,
                                            "If true this mob will be damageable");
                    }

                    if (!Plugin.Instance.Config.ContainsKey(new ConfigDefinition("Mobs", mobName + ".Health")))
                    {
                        EnemyAI enemyAI = enemy.enemyPrefab.GetComponent<EnemyAI>();
                        Plugin.Instance.Config.Bind("Mobs",
                                            mobName + ".Health",
                                            enemyAI.enemyHP,
                                            "The value of the mobs health");
                        enemyAI.enemyHP = enemyAI.enemyHP;
                        Plugin.Log.LogInfo($"Set {enemy.name} HP to {enemyAI.enemyHP}");
                    }

                    if (Plugin.CanMob(".Unimmortal", mobName))
                    {
                        enemy.canDie = true;
                        DamagableEnemies.Add(enemy.enemyName);
                        Plugin.Log.LogInfo($"Made {enemy.enemyName} damageable");
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogInfo($"Error configuring enemy {enemy.enemyName}: {e.Message}");
                    InvalidEnemies.Add(enemy.enemyName);
                }
            }
        }

        public static bool IsEnemyDamageable(EnemyAI enemy)
        {
            if (enemy == null || enemy.isEnemyDead || !enemy.enabled)
                return false;

            string name = Plugin.RemoveInvalidCharacters(enemy.enemyType.enemyName).ToUpper();
            return Plugin.CanMob(".Unimmortal", name);
        }

        public static void HitEnemyPatch(ref EnemyAI __instance, int force = 1, PlayerControllerB playerWhoHit = null)
        {
            if (__instance == null || __instance.isEnemyDead || InvalidEnemies.Contains(__instance.enemyType.enemyName))
                return;

            string enemyName = __instance.enemyType.enemyName;
            Plugin.Log.LogInfo($"Attempting to hit {enemyName}");

            if (!IsEnemyDamageable(__instance))
            {
                Plugin.Log.LogInfo($"{enemyName} is not damageable");
                return;
            }

            if (__instance.creatureAnimator != null)
            {
                __instance.creatureAnimator.SetTrigger(Damage);
            }

            int oldHP = __instance.enemyHP;
            __instance.enemyHP = Math.Max(0, __instance.enemyHP - force);
            Plugin.Log.LogInfo($"Hit {enemyName}: HP {oldHP} -> {__instance.enemyHP}");

            if (__instance.enemyHP <= 0)
            {
                Plugin.Log.LogInfo($"Killing {enemyName}");
                __instance.KillEnemyOnOwnerClient(false);
            }
        }

        public static void KillEnemyPatch(ref EnemyAI __instance)
        {
            if (__instance != null && !InvalidEnemies.Contains(__instance.enemyType.enemyName))
            {
                Plugin.Log.LogInfo($"{__instance.enemyType.enemyName} died");
            }
        }
    }
}
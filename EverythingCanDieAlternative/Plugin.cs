using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace EverythingCanDie
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "nwnt.EverythingCanDieAlternative";
        public const string Name = "EverythingCanDieAlternative";
        public const string Version = "1.0.1";

        public static Plugin Instance;
        public static Harmony Harmony;
        public static ManualLogSource Log;
        public static List<EnemyType> enemies;
        public static List<Item> items;

        private void Awake()
        {
            Instance = this;
            Log = base.Logger;
            Harmony = new Harmony(Guid);

            try
            {
                CreateHarmonyPatch(typeof(StartOfRound), "Start", null, typeof(Patches), nameof(Patches.StartOfRoundPatch), false);
                CreateHarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.HitEnemy),
                    new[] { typeof(int), typeof(PlayerControllerB), typeof(bool), typeof(int) },
                    typeof(Patches), nameof(Patches.HitEnemyPatch), false);
                CreateHarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.KillEnemy),
                    new[] { typeof(bool) }, typeof(Patches), nameof(Patches.KillEnemyPatch), false);

                Log.LogInfo("Patching complete");
            }
            catch (Exception e)
            {
                Log.LogError($"Error in Awake: {e}");
            }
        }

        private void CreateHarmonyPatch(Type typeToPatch, string methodToPatch,
            Type[] parameters, Type patchType, string patchMethod, bool isPrefix)
        {
            try
            {
                if (typeToPatch == null || patchType == null)
                {
                    Log.LogError("Type is either incorrect or does not exist!");
                    return;
                }

                MethodInfo Method = AccessTools.Method(typeToPatch, methodToPatch, parameters, null);
                MethodInfo Patch_Method = AccessTools.Method(patchType, patchMethod, null, null);

                if (Method == null)
                {
                    Log.LogError($"Could not find method {methodToPatch} in {typeToPatch.Name}");
                    return;
                }

                if (Patch_Method == null)
                {
                    Log.LogError($"Could not find patch method {patchMethod} in {patchType.Name}");
                    return;
                }

                if (isPrefix)
                {
                    Harmony.Patch(Method, new HarmonyMethod(Patch_Method));
                    Log.LogInfo($"Created prefix patch for {Method.Name}");
                }
                else
                {
                    Harmony.Patch(Method, null, new HarmonyMethod(Patch_Method));
                    Log.LogInfo($"Created postfix patch for {Method.Name}");
                }
            }
            catch (Exception e)
            {
                Log.LogError($"Error creating harmony patch: {e}");
            }
        }

        public static string RemoveInvalidCharacters(string source)
        {
            if (string.IsNullOrEmpty(source)) return string.Empty;

            StringBuilder sb = new StringBuilder();
            foreach (char c in source)
            {
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
                {
                    sb.Append(c);
                }
            }
            return string.Join("", sb.ToString().Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
        }

        public static bool CanMob(string identifier, string mobName)
        {
            try
            {
                string mob = RemoveInvalidCharacters(mobName).ToUpper();
                string mobConfigKey = mob + identifier.ToUpper();

                foreach (ConfigDefinition entry in Instance.Config.Keys)
                {
                    if (RemoveInvalidCharacters(entry.Key.ToUpper()).Equals(RemoveInvalidCharacters(mobConfigKey)))
                    {
                        bool result = Instance.Config[entry].BoxedValue.ToString().ToUpper().Equals("TRUE");
                        Log.LogInfo($"Mob config: [Mobs] {mobConfigKey} = {result}");
                        return result;
                    }
                }

                Log.LogInfo($"No config found for [Mobs] {mobConfigKey}, defaulting to true");
                return true;
            }
            catch (Exception e)
            {
                Log.LogError($"Error in config check for mob {mobName}: {e.Message}");
                return false;
            }
        }
    }
}
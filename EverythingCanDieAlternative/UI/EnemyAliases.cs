using System.Collections.Generic;
using UnityEngine;
using BepInEx.Configuration;

namespace EverythingCanDieAlternative.UI
{
    public static class EnemyAliases
    {
        // Static curated list — manually maintained, always checked first
    private static readonly Dictionary<string, string[]> StaticAliases = new Dictionary<string, string[]>
    {
        { "BABOONHAWK",       new[] { "baboon hawk" } },
        { "BLOB",             new[] { "hygrodere" } },
        { "BUTLER",           new[] { "butler" } },
        { "BUTLERBEES",       new[] { "mask hornets" } },
        { "MANEATER",         new[] { "maneater" } },
        { "CENTIPEDE",        new[] { "snare flea" } },
        { "CLAYSURGEON",      new[] { "barber" } },
        { "CRAWLER",          new[] { "thumper" } },
        { "DOCILELOCUSTBEES", new[] { "roaming locusts" } },
        { "MANTICOIL",        new[] { "manticoil" } },
        { "TULIPSNAKE",       new[] { "tulip snake" } },
        { "REDLOCUSTBEES",    new[] { "circuit bees" } },
        { "FLOWERMAN",        new[] { "bracken" } },
        { "FORESTGIANT",      new[] { "forest giant" } },
        { "GIANTKIWI",        new[] { "giant sapsucker" } },
        { "HOARDINGBUG",      new[] { "hoarding bug" } },
        { "JESTER",           new[] { "jester" } },
        { "LASSO",            new[] { "lasso" } },
        { "MOUTHDOG",         new[] { "eyeless dog" } },
        { "NUTCRACKER",       new[] { "nutcracker" } },
        { "PUFFER",           new[] { "spore lizard" } },
        { "RADMECH",          new[] { "old bird" } },
        { "BUNKERSPIDER",     new[] { "bunker spider" } },
        { "EARTHLEVIATHAN",   new[] { "earth leviathan" } },
        { "SPRING",           new[] { "coil-head" } },
        { "BUSHWOLF",         new[] { "kidnapper fox" } },
    };

        // Runtime cache of persisted scan names — loaded once on first use
        private static Dictionary<string, string> _persistedScanNames = null;

        private static Dictionary<string, string> PersistedScanNames
        {
            get
            {
                if (_persistedScanNames == null) LoadPersistedAliases();
                return _persistedScanNames;
            }
        }

        // Load already-persisted aliases from config into the runtime cache
        private static void LoadPersistedAliases()
        {
            // Initialize if null rather than assuming it exists
            if (_persistedScanNames == null)
                _persistedScanNames = new Dictionary<string, string>();
            
            _persistedScanNames.Clear();

            string configPath = System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "nwnt.EverythingCanDieAlternative.cfg");
            if (!System.IO.File.Exists(configPath)) return;

            bool inAliasSection = false;

            foreach (string line in System.IO.File.ReadAllLines(configPath))
            {
                string trimmed = line.Trim();

                if (trimmed.StartsWith("["))
                {
                    inAliasSection = trimmed == "[EnemyAliases]";
                    continue;
                }

                if (!inAliasSection) continue;
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#")) continue;

                int equalsPos = trimmed.IndexOf('=');
                if (equalsPos <= 0) continue;

                string key = trimmed.Substring(0, equalsPos).Trim();
                string value = trimmed.Substring(equalsPos + 1).Trim();

                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                    _persistedScanNames[key.ToUpper()] = value.ToLower();
            }

            Plugin.LogInfo($"[EnemyAliases] Loaded {_persistedScanNames.Count} persisted scan names");
        }

        // Called once per round start — scans spawned enemies and persists any new names
        public static void ScanAndPersistDisplayNames()
        {
            if (Plugin.Instance?.Config == null || Plugin.enemies == null) return;

            // Force reload of persisted names from config before checking what's already there
            // This ensures we don't re-write entries that exist from a previous session
            LoadPersistedAliases();

            int newEntries = 0;

            foreach (var enemyType in Plugin.enemies)
            {
                if (enemyType?.enemyPrefab == null || string.IsNullOrWhiteSpace(enemyType.enemyName)) continue;

                string internalName = enemyType.enemyName;
                if (string.IsNullOrWhiteSpace(internalName)) continue;

                if (PersistedScanNames.ContainsKey(internalName.ToUpper())) continue;

                var scanNode = enemyType.enemyPrefab.GetComponentInChildren<ScanNodeProperties>();
                if (scanNode == null || string.IsNullOrWhiteSpace(scanNode.headerText)) continue;

                string displayName = scanNode.headerText.Trim();

                Plugin.Instance.Config.Bind(
                    "EnemyAliases",
                    internalName,
                    displayName,
                    $"Display name discovered from prefab for {internalName}. Used as a search alias.");

                PersistedScanNames[internalName.ToUpper()] = displayName.ToLower();

                //Plugin.LogInfo($"[EnemyAliases] Discovered: '{internalName}' → '{displayName}'");
                newEntries++;
            }

            if (newEntries > 0)
            {
                Plugin.Instance.Config.Save();
                //Plugin.LogInfo($"[EnemyAliases] Saved {newEntries} new alias entries to config");
            }
            else
            {
                //Plugin.LogInfo($"[EnemyAliases] All {PersistedScanNames.Count} aliases already persisted, nothing new to save");
            }
        }

        public static bool MatchesAlias(string enemyName, string searchText)
        {
            string key = enemyName.ToUpper();

      

            if (StaticAliases.TryGetValue(key, out string[] staticAliases))
            {
               ;
                foreach (var alias in staticAliases)
                {
                    if (alias.Contains(searchText))
                    {
                        
                        return true;
                    }
                }
            }

            if (PersistedScanNames.TryGetValue(key, out string scanName))
            {
                if (scanName.Contains(searchText))
                {
                    return true;
                }
            }
            else
            {
            }

            return false;
        }
        // Returns the display name for an enemy if one exists, otherwise null
        public static string GetDisplayName(string enemyName)
        {
            string key = enemyName.ToUpper();

            // Persisted scan names are authoritative
            if (PersistedScanNames.TryGetValue(key, out string scanName))
                return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(scanName);

            // Fall back to first static alias
            if (StaticAliases.TryGetValue(key, out string[] aliases) && aliases.Length > 0)
                return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(aliases[0]);

            return null;
        }
    }
}
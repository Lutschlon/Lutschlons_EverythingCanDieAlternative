using System;
using UnityEngine;

namespace EverythingCanDieAlternative.UI
{
    /// <summary>
    /// Class to hold enemy configuration data for the UI
    /// </summary>
    public class EnemyConfigData
    {
        public string Name;
        public string SanitizedName;
        public bool IsEnabled;
        public bool CanDie;
        public bool ShouldDespawn;
        public int Health;

        public EnemyConfigData(string name, bool isEnabled, bool canDie, bool shouldDespawn, int health)
        {
            Name = name;
            SanitizedName = Plugin.RemoveInvalidCharacters(name).ToUpper();
            IsEnabled = isEnabled;
            CanDie = canDie;
            ShouldDespawn = shouldDespawn;
            Health = health;

            // Log the actual values being stored in the config data
            Plugin.Log.LogInfo($"Created EnemyConfigData for {name}: Enabled={isEnabled}, CanDie={canDie}, ShouldDespawn={shouldDespawn}, Health={health}");
        }

        public Color GetStatusColor()
        {
            if (!IsEnabled)
            {
                // Red for disabled
                return Color.red;
            }
            else if (!CanDie)
            {
                // Yellow for immortal
                return Color.yellow;
            }
            else
            {
                // Green for enabled and killable
                return Color.green;
            }
        }

        public string GetStatusText()
        {
            if (!IsEnabled)
            {
                return "Disabled";
            }
            else if (!CanDie)
            {
                return "Immortal";
            }
            else
            {
                return "Killable";
            }
        }
    }
}
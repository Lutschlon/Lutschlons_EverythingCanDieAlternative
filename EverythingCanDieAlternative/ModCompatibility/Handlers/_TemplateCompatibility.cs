using System;
using UnityEngine;

namespace EverythingCanDieAlternative.ModCompatibility.Handlers
{
    /// <summary>
    /// Compatibility handler for the [MOD_NAME] mod
    /// </summary>
    public class TemplateCompatibility : BaseModCompatibility
    {
        // Replace these values with the actual mod information
        public override string ModId => "ModAuthor.ModName";
        public override string ModName => "User Friendly Mod Name";
        
        // You can customize the detection logic if needed
        public override bool IsInstalled => base.IsInstalled || 
            // Add custom detection logic here if needed
            false;
        
        protected override void OnModInitialize()
        {
            // Add initialization code specific to this mod
            Plugin.LogInfo($"{ModName} compatibility initialized");
        }
        
        /// <summary>
        /// Example method for mod-specific functionality
        /// </summary>
        public void ExampleMethod()
        {
            // Add compatibility code here
        }
        
        /// <summary>
        /// Example of how to handle enemy modifications for this mod
        /// </summary>
        public void ProcessEnemy(EnemyAI enemy)
        {
            if (enemy == null) return;
            
            // Add mod-specific enemy processing
            Plugin.LogInfo($"Processing {enemy.enemyType.enemyName} for {ModName} compatibility");
        }
    }
}
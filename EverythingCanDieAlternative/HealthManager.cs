using GameNetcodeStuff;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace EverythingCanDieAlternative
{
    public static class HealthManager
    {
        // Dictionary to track enemy health by instance ID
        private static Dictionary<int, int> enemyCurrentHealth = new Dictionary<int, int>();
        private static Dictionary<int, int> enemyMaxHealth = new Dictionary<int, int>();

        // Track hit cooldowns to prevent rapid-fire hits
        private static Dictionary<int, float> lastHitTime = new Dictionary<int, float>();
        private const float HIT_COOLDOWN = 0.1f; // 100ms cooldown between hits

        // Animator hash for damage animation trigger
        private static readonly int DamageAnimTrigger = Animator.StringToHash("damage");

        public static void Initialize()
        {
            enemyCurrentHealth.Clear();
            enemyMaxHealth.Clear();
            lastHitTime.Clear();
            Plugin.Log.LogInfo("Health Manager initialized");
        }

        public static void SetupEnemy(EnemyAI enemy)
        {
            if (enemy == null || enemy.enemyType == null) return;

            try
            {
                string enemyName = enemy.enemyType.enemyName;
                string sanitizedName = Plugin.RemoveInvalidCharacters(enemyName).ToUpper();
                bool canDamage = Plugin.CanMob(".Unimmortal", sanitizedName);
                int instanceId = enemy.GetInstanceID();

                if (canDamage)
                {
                    // Get configured health
                    int configHealth = Plugin.GetMobHealth(sanitizedName, enemy.enemyHP);

                    // Store the health in our dictionaries
                    enemyMaxHealth[instanceId] = configHealth;
                    enemyCurrentHealth[instanceId] = configHealth;

                    // Make enemy killable in the game system but with high HP
                    enemy.enemyType.canDie = true;

                    // Force high HP value and verify it was set
                    enemy.enemyHP = 999;
                    Plugin.Log.LogInfo($"Set {enemyName} game HP to 999, config health is {configHealth}");

                    // Ensure canBeDestroyed is true to allow proper killing
                    enemy.enemyType.canBeDestroyed = true;

                    Plugin.Log.LogInfo($"Setup enemy {enemyName} (ID: {instanceId}) with {configHealth} health");
                }
                else
                {
                    Plugin.Log.LogInfo($"Enemy {enemyName} is not configured to be damageable");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error setting up enemy: {ex.Message}");
            }
        }

        public static bool DamageEnemy(EnemyAI enemy, int damage, PlayerControllerB playerWhoHit, bool playHitSFX, int hitID)
        {
            if (enemy == null || enemy.isEnemyDead) return false;

            // Only the host should track and apply damage
            if (!StartOfRound.Instance.IsHost) return false;

            int instanceId = enemy.GetInstanceID();
            string enemyName = enemy.enemyType.enemyName;

            // Check for hit cooldown
            if (lastHitTime.TryGetValue(instanceId, out float lastHit) && Time.time - lastHit < HIT_COOLDOWN)
            {
                return false;
            }
            lastHitTime[instanceId] = Time.time;

            // Check if enemy is configured to be damageable
            string sanitizedName = Plugin.RemoveInvalidCharacters(enemyName).ToUpper();
            if (!Plugin.CanMob(".Unimmortal", sanitizedName))
            {
                return false;
            }

            // Initialize health if not already tracked
            if (!enemyCurrentHealth.ContainsKey(instanceId))
            {
                SetupEnemy(enemy);
            }

            // Track current health and apply damage
            int currentHealth = enemyCurrentHealth[instanceId];
            int newHealth = Mathf.Max(0, currentHealth - damage);
            enemyCurrentHealth[instanceId] = newHealth;

            Plugin.Log.LogInfo($"Enemy {enemyName} damaged for {damage}: {currentHealth} -> {newHealth}");

            // Trigger damage animation if available
            if (enemy.creatureAnimator != null)
            {
                enemy.creatureAnimator.SetTrigger(DamageAnimTrigger);
            }

            // Kill the enemy if health reaches zero
            if (newHealth <= 0 && currentHealth > 0)
            {
                Plugin.Log.LogInfo($"Enemy {enemyName} died from damage");

                // Force ownership back to host before killing (helps with Spring)
                if (!enemy.IsOwner)
                {
                    Plugin.Log.LogInfo($"Attempting to take ownership of {enemyName} to kill it");
                    ulong hostId = StartOfRound.Instance.allPlayerScripts[0].actualClientId;
                    enemy.ChangeOwnershipOfEnemy(hostId);
                }

                // Try both kill methods to ensure it dies
                enemy.KillEnemyOnOwnerClient(false);

                // For problematic enemies like Spring, try again with destroy=true as fallback
                if (enemyName.Contains("Spring"))
                {
                    Plugin.Log.LogInfo($"Using fallback kill method for {enemyName}");
                    enemy.KillEnemyOnOwnerClient(true);
                }
            }

            return true;
        }

        public static int GetEnemyHealth(EnemyAI enemy)
        {
            if (enemy == null) return 0;

            int instanceId = enemy.GetInstanceID();
            if (enemyCurrentHealth.TryGetValue(instanceId, out int health))
            {
                return health;
            }

            return 0;
        }
    }
}
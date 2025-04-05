using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;

namespace EverythingCanDieAlternative.ModCompatibility.Handlers
{
    /// <summary>
    /// Compatibility handler to handle problematic enemies that don't die properly with standard kill methods
    /// </summary>
    public class LastResortKillerCompatibility : BaseModCompatibility
    {
        public override string ModId => "nwnt.EverythingCanDieAlternative.LastResortKiller";
        public override string ModName => "Last Resort Enemy Killer";

        // This is a built-in compatibility, so it's always "installed"
        public override bool IsInstalled => true;

        // Keep track of enemies we've attempted to kill, with counts of attempts
        private Dictionary<int, int> enemyKillAttempts = new Dictionary<int, int>();

        // Time between kill attempts
        private const float RETRY_DELAY = 0.5f;

        protected override void OnModInitialize()
        {
            Plugin.Log.LogInfo($"{ModName} compatibility initialized");
            enemyKillAttempts.Clear();
        }

        /// <summary>
        /// Try to kill an enemy with progressive fallbacks if initial attempts fail
        /// </summary>
        /// <param name="enemy">The enemy to kill</param>
        /// <param name="allowDespawn">Whether this enemy is allowed to despawn based on config</param>
        /// <returns>True if a kill attempt was initiated</returns>
        public bool AttemptToKillEnemy(EnemyAI enemy, bool allowDespawn)
        {
            if (enemy == null || enemy.isEnemyDead) return false;

            int instanceId = enemy.GetInstanceID();

            // Record this attempt
            if (!enemyKillAttempts.ContainsKey(instanceId))
            {
                enemyKillAttempts[instanceId] = 1;
            }
            else
            {
                enemyKillAttempts[instanceId]++;
            }

            int attempts = enemyKillAttempts[instanceId];
            Plugin.Log.LogInfo($"Kill attempt #{attempts} for {enemy.enemyType.enemyName} (Despawn allowed: {allowDespawn})");

            // No need to call HandleSellBodiesCompatibility here - it's already handled in NetworkedHealthManager.KillEnemy

            // If we've already tried the standard approaches, use progressively more forceful methods
            if (attempts == 1)
            {
                // First attempt: Standard kill without destroy (lets death animations play)
                Plugin.Log.LogInfo($"Standard kill attempt for {enemy.enemyType.enemyName}");
                enemy.KillEnemyOnOwnerClient(false);
                ScheduleKillCheck(enemy);
                return true;
            }
            else if (attempts == 2)
            {
                // Second attempt: Kill with destroy flag + notify clients to despawn (if allowed)
                Plugin.Log.LogInfo($"Destroy-enabled kill attempt for {enemy.enemyType.enemyName}");
                enemy.KillEnemyOnOwnerClient(true);

                // Only notify clients to destroy if despawning is allowed by config
                if (allowDespawn && enemy.thisEnemyIndex >= 0)
                {
                    Plugin.Log.LogInfo($"Notifying clients to despawn enemy {enemy.enemyType.enemyName}");
                    NetworkedHealthManager.NotifyClientsOfDestroy(enemy.thisEnemyIndex);
                }

                ScheduleKillCheck(enemy);
                return true;
            }
            else if (attempts == 3)
            {
                // Third attempt: Force-set internal states + aggressive component disabling
                //Plugin.Log.LogInfo($"Aggressive force-state and component disabling for {enemy.enemyType.enemyName}");
                ForceDeadState(enemy);

                // More forceful approach - disable all MonoBehaviours except the main EnemyAI
                DisableAllComponents(enemy);

                // Force network despawn only if despawning is allowed by config
                if (allowDespawn)
                {
                    if (enemy.NetworkObject != null && enemy.NetworkObject.IsSpawned)
                    {
                        try
                        {
                            // This is a very aggressive approach that might cause issues
                            Plugin.Log.LogInfo($"Forcing network despawn for {enemy.enemyType.enemyName}");
                            enemy.NetworkObject.Despawn();
                        }
                        catch (Exception ex)
                        {
                            Plugin.Log.LogError($"Failed to force despawn: {ex.Message}");
                        }
                    }

                    // Also notify clients to destroy this enemy
                    if (enemy.thisEnemyIndex >= 0)
                    {
                        NetworkedHealthManager.NotifyClientsOfDestroy(enemy.thisEnemyIndex);
                    }
                }

                ScheduleKillCheck(enemy);
                return true;
            }
            else
            {
                // Final attempt: Destroy the GameObject entirely - most drastic solution
                Plugin.Log.LogInfo($"Final solution: Destroying game object for {enemy.enemyType.enemyName}");

                // Only notify clients to destroy if despawning is allowed by config
                if (allowDespawn && enemy.thisEnemyIndex >= 0)
                {
                    Plugin.Log.LogInfo($"Informing clients to destroy {enemy.enemyType.enemyName}");
                    NetworkedHealthManager.NotifyClientsOfDestroy(enemy.thisEnemyIndex);
                }

                GameObject.Destroy(enemy.gameObject);

                // Clean up our tracking
                enemyKillAttempts.Remove(instanceId);

                return true;
            }
        }


        /// <summary>
        /// Schedule a check to see if the enemy actually died
        /// </summary>
        private void ScheduleKillCheck(EnemyAI enemy)
        {
            if (StartOfRound.Instance != null)
            {
                // Get the despawn configuration for this enemy
                bool allowDespawn = DespawnConfiguration.Instance.ShouldDespawnEnemy(enemy.enemyType.enemyName);
                StartOfRound.Instance.StartCoroutine(CheckIfEnemyDied(enemy, allowDespawn));
            }
        }

        /// <summary>
        /// Check if the enemy is actually dead after a brief delay, retry if not
        /// </summary>
        private IEnumerator CheckIfEnemyDied(EnemyAI enemy, bool allowDespawn)
        {
            yield return new WaitForSeconds(RETRY_DELAY);

            if (enemy != null && !enemy.isEnemyDead)
            {
                Plugin.Log.LogWarning($"Kill attempt failed for {enemy.enemyType.enemyName}, will try again with more force");
                AttemptToKillEnemy(enemy, allowDespawn);
            }
            else
            {
                // Success! Remove from our tracking
                int instanceId = enemy?.GetInstanceID() ?? 0;
                if (instanceId != 0 && enemyKillAttempts.ContainsKey(instanceId))
                {
                    enemyKillAttempts.Remove(instanceId);
                    Plugin.Log.LogInfo($"Enemy {(enemy != null ? enemy.enemyType.enemyName : "unknown")} successfully killed");
                }
            }
        }

        /// <summary>
        /// Attempt to force enemy properties to behave as if it were dead
        /// </summary>
        private void ForceDeadState(EnemyAI enemy)
        {
            try
            {
                // Set every death-related property we can find
                enemy.isEnemyDead = true;

                // Some enemies use an agent for movement, disable it
                if (enemy.agent != null && enemy.agent.enabled)
                {
                    enemy.agent.isStopped = true;
                    enemy.agent.enabled = false;
                }

                // Try triggering death animations if they exist
                if (enemy.creatureAnimator != null)
                {
                    try
                    {
                        enemy.creatureAnimator.SetTrigger("Death");
                    }
                    catch (Exception) { }

                    try
                    {
                        enemy.creatureAnimator.SetTrigger("death");
                    }
                    catch (Exception) { }

                    try
                    {
                        enemy.creatureAnimator.SetBool("Dead", true);
                    }
                    catch (Exception) { }

                    try
                    {
                        enemy.creatureAnimator.SetBool("dead", true);
                    }
                    catch (Exception) { }
                }

                // Set the enemy HP to 0 in case it's checking that
                enemy.enemyHP = 0;

                // Some enemies might have colliders that need disabling
                Collider[] colliders = enemy.GetComponentsInChildren<Collider>();
                foreach (Collider collider in colliders)
                {
                    if (collider.enabled)
                    {
                        collider.enabled = false;
                    }
                }

                // Now actually try the kill method with destroy=true as a backup
                enemy.KillEnemyOnOwnerClient(true);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error during force-state kill attempt: {ex.Message}");
            }
        }

        /// <summary>
        /// Aggressively disable all components on the enemy except its main EnemyAI component
        /// This helps ensure the enemy stops all activity and doesn't interfere with the game
        /// </summary>
        private void DisableAllComponents(EnemyAI enemy)
        {
            try
            {
                // Disable all MonoBehaviours except the main EnemyAI
                MonoBehaviour[] components = enemy.GetComponentsInChildren<MonoBehaviour>();
                foreach (MonoBehaviour component in components)
                {
                    // Skip the main EnemyAI component
                    if (component != enemy && component.enabled)
                    {
                        //Plugin.Log.LogInfo($"Disabling component {component.GetType().Name} on {enemy.enemyType.enemyName}");
                        component.enabled = false;
                    }
                }

                // Disable all colliders
                Collider[] colliders = enemy.GetComponentsInChildren<Collider>();
                foreach (Collider collider in colliders)
                {
                    if (collider.enabled)
                    {
                        collider.enabled = false;
                    }
                }

                // Disable all renderers to make it invisible (optional)
                Renderer[] renderers = enemy.GetComponentsInChildren<Renderer>();
                foreach (Renderer renderer in renderers)
                {
                    if (renderer.enabled)
                    {
                        renderer.enabled = false;
                    }
                }

                // Disable all audio sources
                AudioSource[] audioSources = enemy.GetComponentsInChildren<AudioSource>();
                foreach (AudioSource audioSource in audioSources)
                {
                    if (audioSource.enabled)
                    {
                        audioSource.enabled = false;
                        if (audioSource.isPlaying)
                        {
                            audioSource.Stop();
                        }
                    }
                }

                // If there's a Rigidbody, make it kinematic and zero its velocity
                Rigidbody[] rigidbodies = enemy.GetComponentsInChildren<Rigidbody>();
                foreach (Rigidbody rb in rigidbodies)
                {
                    if (!rb.isKinematic)
                    {
                        rb.velocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                        rb.isKinematic = true;
                    }
                }

                // Try to disable any particle systems
                ParticleSystem[] particleSystems = enemy.GetComponentsInChildren<ParticleSystem>();
                foreach (ParticleSystem ps in particleSystems)
                {
                    if (ps.isPlaying)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error disabling components: {ex.Message}");
            }
        }

        /// <summary>
        /// Reset tracking data (e.g., when starting a new game)
        /// </summary>
        public void Reset()
        {
            enemyKillAttempts.Clear();
        }
    }
}
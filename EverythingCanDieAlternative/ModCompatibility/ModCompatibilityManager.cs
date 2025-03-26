using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace EverythingCanDieAlternative.ModCompatibility
{
    /// <summary>
    /// Central manager for all mod compatibility handlers
    /// </summary>
    public class ModCompatibilityManager
    {
        private static ModCompatibilityManager _instance;
        public static ModCompatibilityManager Instance => _instance ??= new ModCompatibilityManager();

        private readonly Dictionary<string, IModCompatibility> _handlers = new Dictionary<string, IModCompatibility>();

        private ModCompatibilityManager()
        {
            // Private constructor for singleton
        }

        /// <summary>
        /// Initialize the compatibility manager and all registered handlers
        /// </summary>
        public void Initialize()
        {
            Plugin.Log.LogInfo("Initializing mod compatibility framework...");

            // Register handlers manually - more reliable than auto-discovery
            RegisterKnownHandlers();

            // Initialize all registered handlers
            foreach (var handler in _handlers.Values)
            {
                try
                {
                    handler.Initialize();
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"Error initializing compatibility for {handler.ModName}: {ex.Message}");
                }
            }

            Plugin.Log.LogInfo($"Mod compatibility framework initialized with {_handlers.Count} handlers");
        }

        /// <summary>
        /// Register known compatibility handlers manually
        /// </summary>
        private void RegisterKnownHandlers()
        {
            try
            {
                RegisterHandler(new Handlers.SellBodiesCompatibility());
                RegisterHandler(new Handlers.LethalHandsCompatibility());
                RegisterHandler(new Handlers.BrutalCompanyMinusCompatibility());
                RegisterHandler(new Handlers.EnemyVsEnemyCompatibility());

                // Add other handlers here as you create them
                // RegisterHandler(new Handlers.OtherModCompatibility());
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error registering known handlers: {ex.Message}");
            }
        }

        /// <summary>
        /// Manually register a compatibility handler
        /// </summary>
        public void RegisterHandler(IModCompatibility handler)
        {
            if (!_handlers.ContainsKey(handler.ModId))
            {
                _handlers.Add(handler.ModId, handler);
                Plugin.Log.LogInfo($"Registered compatibility handler for {handler.ModName}");
            }
            else
            {
                Plugin.Log.LogWarning($"Compatibility handler for {handler.ModName} already registered");
            }
        }

        /// <summary>
        /// Check if a specific mod is installed
        /// </summary>
        public bool IsModInstalled(string modId)
        {
            return _handlers.TryGetValue(modId, out var handler) && handler.IsInstalled;
        }

        /// <summary>
        /// Get a specific mod compatibility handler
        /// </summary>
        public T GetHandler<T>(string modId) where T : class, IModCompatibility
        {
            if (_handlers.TryGetValue(modId, out var handler) && handler is T typedHandler)
            {
                return typedHandler;
            }
            return null;
        }

        /// <summary>
        /// Get all registered compatibility handlers
        /// </summary>
        public IEnumerable<IModCompatibility> GetAllHandlers()
        {
            return _handlers.Values;
        }
    }
}
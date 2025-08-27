using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace EverythingCanDieAlternative.ModCompatibility
{
    // Central manager for all mod compatibility handlers
    public class ModCompatibilityManager
    {
        private static ModCompatibilityManager _instance;
        public static ModCompatibilityManager Instance => _instance ??= new ModCompatibilityManager();

        private readonly Dictionary<string, IModCompatibility> _handlers = new Dictionary<string, IModCompatibility>();

        private ModCompatibilityManager()
        {
            // Private constructor for singleton
        }

        // Initialize the compatibility manager and all registered handlers
        public void Initialize()
        {
            try
            {
                //Plugin.LogInfo("Initializing mod compatibility framework...");

                // Register handlers manually - more reliable than auto-discovery
                RegisterKnownHandlers();

                // Initialize all registered handlers - with additional error handling
                foreach (var handler in _handlers.Values)
                {
                    try
                    {
                        //Plugin.LogInfo($"Initializing {handler.ModName} compatibility handler...");
                        handler.Initialize();
                        Plugin.LogInfo($"Successfully initialized {handler.ModName} compatibility handler");
                    }
                    catch (Exception ex)
                    {
                        // More detailed error logging
                        Plugin.Log.LogError($"Error initializing compatibility for {handler.ModName}: {ex.Message}");
                        Plugin.Log.LogError($"Stack trace: {ex.StackTrace}");
                    }
                }

                //Plugin.LogInfo($"Mod compatibility framework initialized with {_handlers.Count} handlers");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Fatal error in compatibility framework initialization: {ex.Message}");
                Plugin.Log.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        // Register known compatibility handlers manually
        private void RegisterKnownHandlers()
        {
            try
            {
                // Register each handler individually with try/catch to isolate issues
                SafeRegisterHandler(() => new Handlers.SellBodiesCompatibility(), "SellBodies");
                SafeRegisterHandler(() => new Handlers.LethalHandsCompatibility(), "LethalHands");
                SafeRegisterHandler(() => new Handlers.BrutalCompanyMinusCompatibility(), "BrutalCompanyMinus");
                SafeRegisterHandler(() => new Handlers.EnemyVsEnemyCompatibility(), "EnemyVsEnemy");
                SafeRegisterHandler(() => new Handlers.LastResortKillerCompatibility(), "LastResortKiller");
                SafeRegisterHandler(() => new Handlers.HitmarkerCompatibility(), "Hitmarker");
                SafeRegisterHandler(() => new Handlers.LethalMinCompatibility(), "NoteBoxz.LethalMin");

            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error registering known handlers: {ex.Message}");
                Plugin.Log.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        // Safely register a compatibility handler with error handling
        private void SafeRegisterHandler(Func<IModCompatibility> handlerFactory, string handlerName)
        {
            try
            {
                var handler = handlerFactory();
                RegisterHandler(handler);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error creating compatibility handler for {handlerName}: {ex.Message}");
            }
        }

        // Manually register a compatibility handler
        public void RegisterHandler(IModCompatibility handler)
        {
            try
            {
                if (!_handlers.ContainsKey(handler.ModId))
                {
                    _handlers.Add(handler.ModId, handler);
                    //Plugin.LogInfo($"Registered compatibility handler for {handler.ModName}");
                }
                else
                {
                    Plugin.Log.LogWarning($"Compatibility handler for {handler.ModName} already registered");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error registering handler for {handler.ModName}: {ex.Message}");
            }
        }

        // Check if a specific mod is installed
        public bool IsModInstalled(string modId)
        {
            try
            {
                return _handlers.TryGetValue(modId, out var handler) && handler.IsInstalled;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error checking if mod {modId} is installed: {ex.Message}");
                return false;
            }
        }

        // Get a specific mod compatibility handler
        public T GetHandler<T>(string modId) where T : class, IModCompatibility
        {
            try
            {
                if (_handlers.TryGetValue(modId, out var handler) && handler is T typedHandler)
                {
                    return typedHandler;
                }
                return null;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error getting handler for {modId}: {ex.Message}");
                return null;
            }
        }

        // Get all registered compatibility handlers
        public IEnumerable<IModCompatibility> GetAllHandlers()
        {
            return _handlers.Values;
        }
    }
}
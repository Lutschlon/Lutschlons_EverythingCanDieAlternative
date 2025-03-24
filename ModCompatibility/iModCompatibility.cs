using System;
using System.Linq;

namespace EverythingCanDieAlternative.ModCompatibility
{
    /// <summary>
    /// Interface for all mod compatibility handlers
    /// </summary>
    public interface IModCompatibility
    {
        /// <summary>
        /// Unique identifier for the mod
        /// </summary>
        string ModId { get; }
        
        /// <summary>
        /// Human-readable name of the mod
        /// </summary>
        string ModName { get; }
        
        /// <summary>
        /// Whether the mod is currently installed
        /// </summary>
        bool IsInstalled { get; }
        
        /// <summary>
        /// Initialize the compatibility handler
        /// </summary>
        void Initialize();
    }

    /// <summary>
    /// Base class for mod compatibility implementations
    /// </summary>
    public abstract class BaseModCompatibility : IModCompatibility
    {
        public abstract string ModId { get; }
        public abstract string ModName { get; }
        
        // Default implementation to detect if a mod is installed
        public virtual bool IsInstalled 
        { 
            get
            {
                try
                {
                    return AppDomain.CurrentDomain.GetAssemblies()
                        .Any(a => a.GetName().Name == ModId || 
                                 a.GetTypes().Any(t => t.Namespace?.Contains(ModId) == true));
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"Error detecting mod {ModName}: {ex.Message}");
                    return false;
                }
            }
        }
        
        public virtual void Initialize()
        {
            if (IsInstalled)
            {
                Plugin.Log.LogInfo($"{ModName} detected - enabling compatibility features");
                OnModInitialize();
            }
        }
        
        /// <summary>
        /// Called when the mod is detected as installed
        /// </summary>
        protected abstract void OnModInitialize();
    }
}
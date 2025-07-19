using System;
using System.Linq;

namespace EverythingCanDieAlternative.ModCompatibility
{
    // Interface for all mod compatibility handlers
    public interface IModCompatibility
    {
        // Unique identifier for the mod
        string ModId { get; }
        
        // Human-readable name of the mod
        string ModName { get; }
        
        // Whether the mod is currently installed
        bool IsInstalled { get; }
        
        // Initialize the compatibility handler
        void Initialize();
    }

    // Base class for mod compatibility implementations
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
                //Plugin.LogInfo($"{ModName} detected - enabling compatibility features");
                OnModInitialize();
            }
        }
        
        // Called when the mod is detected as installed
        protected abstract void OnModInitialize();
    }
}
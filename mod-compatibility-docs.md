# EverythingCanDieAlternative - Mod Compatibility Framework

## Table of Contents
1. [Overview](#overview)
2. [Architecture](#architecture)
3. [File Structure](#file-structure)
4. [Core Components](#core-components)
5. [Adding New Mod Compatibilities](#adding-new-mod-compatibilities)
6. [Existing Compatibility Implementations](#existing-compatibility-implementations)
7. [Integration with Main Mod](#integration-with-main-mod)
8. [Troubleshooting](#troubleshooting)
9. [Best Practices](#best-practices)

## Overview

The Mod Compatibility Framework provides a structured approach for adding compatibility with other Lethal Company mods to the EverythingCanDieAlternative mod. It uses a modular, object-oriented design to make compatibility additions maintainable, isolated, and easy to implement.

Key features:
- Centralized management of mod compatibilities
- Standardized interface for all compatibility handlers
- Robust mod detection with fallback strategies
- Isolated compatibility logic to prevent cross-mod conflicts
- Easy addition of new mod compatibilities without changing core code

## Architecture

The framework implements a combination of design patterns:

1. **Singleton Pattern** - `ModCompatibilityManager` is a singleton that provides global access to the compatibility system
2. **Interface Segregation** - All compatibility handlers implement `IModCompatibility`
3. **Template Method Pattern** - `BaseModCompatibility` defines the structure while specific implementations provide details
4. **Registry Pattern** - Handlers are registered with the manager that maintains their lifecycle

The architecture promotes loose coupling by allowing each compatibility handler to function independently of the others. This means:
- Issues in one compatibility handler won't affect others
- Handlers can be added or removed without changing existing code
- The main mod logic isn't affected by compatibility code

## File Structure

```
EverythingCanDieAlternative/
├── Plugin.cs                        # Main plugin entry point
├── Patches.cs                       # Harmony patches
├── HealthManager.cs                 # Enemy health management
├── DespawnConfiguration.cs          # Despawn settings
├── ModCompatibility/                # Compatibility framework
│   ├── IModCompatibility.cs         # Interface and base class
│   ├── ModCompatibilityManager.cs   # Central manager
│   └── Handlers/                    # Individual compatibility implementations
│       ├── SellBodiesCompatibility.cs    # SellBodies compatibility
│       └── _TemplateCompatibility.cs     # Template for new handlers
```

## Core Components

### IModCompatibility Interface

The primary interface all compatibility handlers must implement:

```csharp
public interface IModCompatibility
{
    string ModId { get; }
    string ModName { get; }
    bool IsInstalled { get; }
    void Initialize();
}
```

- **ModId**: Unique identifier for the mod (typically its GUID)
- **ModName**: Human-readable name of the mod
- **IsInstalled**: Whether the mod is currently installed
- **Initialize()**: Called to set up the compatibility handler

### BaseModCompatibility Class

Abstract base class providing default implementations:

```csharp
public abstract class BaseModCompatibility : IModCompatibility
{
    public abstract string ModId { get; }
    public abstract string ModName { get; }
    public virtual bool IsInstalled { get; }
    public virtual void Initialize() { ... }
    protected abstract void OnModInitialize();
}
```

This class handles common logic like checking if a mod is installed while specific handlers override specialized methods.

### ModCompatibilityManager

Central registry for compatibility handlers:

```csharp
public class ModCompatibilityManager
{
    public static ModCompatibilityManager Instance { get; }
    public void Initialize();
    public void RegisterHandler(IModCompatibility handler);
    public bool IsModInstalled(string modId);
    public T GetHandler<T>(string modId) where T : class, IModCompatibility;
    public IEnumerable<IModCompatibility> GetAllHandlers();
}
```

The manager provides:
- Registration of compatibility handlers
- Initialization of all handlers
- Retrieval of specific handlers by mod ID
- Checking if specific mods are installed

## Adding New Mod Compatibilities

To add compatibility with a new mod:

1. Create a new class in the `ModCompatibility/Handlers` folder using the template:

```csharp
public class NewModCompatibility : BaseModCompatibility
{
    public override string ModId => "ModAuthor.ModName"; // Use exact GUID
    public override string ModName => "User Friendly Mod Name";
    
    protected override void OnModInitialize()
    {
        // Add initialization code here
        Plugin.Log.LogInfo($"{ModName} compatibility initialized");
    }
    
    // Add mod-specific methods
    public void ModSpecificMethod()
    {
        // Implementation
    }
}
```

2. Register the new handler in `ModCompatibilityManager.RegisterKnownHandlers()`:

```csharp
private void RegisterKnownHandlers()
{
    try
    {
        // Existing handlers
        RegisterHandler(new Handlers.SellBodiesCompatibility());
        
        // Add your new handler
        RegisterHandler(new Handlers.NewModCompatibility());
    }
    catch (Exception ex)
    {
        Plugin.Log.LogError($"Error registering known handlers: {ex.Message}");
    }
}
```

3. Use the handler in your mod's code where compatibility is needed:

```csharp
var newModHandler = ModCompatibilityManager.Instance.GetHandler<Handlers.NewModCompatibility>("ModAuthor.ModName");
if (newModHandler != null && newModHandler.IsInstalled)
{
    // Use compatibility methods
    newModHandler.ModSpecificMethod();
}
```

## Existing Compatibility Implementations

### SellBodies Compatibility

The SellBodies compatibility handler provides:

- Robust detection of the SellBodies mod using multiple strategies
- Configuration of appropriate despawn delay to not interfere with SellBodies
- Caching of detection results to avoid repeated reflection scans

Key implementation details:

```csharp
// Multi-layered detection strategy
public override bool IsInstalled 
{ 
    get
    {
        // Return cached result if available
        if (_isInstalled.HasValue)
            return _isInstalled.Value;
        
        try
        {
            // Assembly name check
            // Plugin GUID check
            // Type existence check
            // BepInEx plugin check
        }
        catch (Exception ex)
        {
            // Fallback to existing detection
            _isInstalled = Plugin.Instance.IsSellBodiesModDetected;
            return _isInstalled.Value;
        }
    }
}

// Compatibility method for despawn delay
public float GetDespawnDelay()
{
    return 4.5f; // 4.5 seconds is slightly longer than SellBodies' 4-second timer
}
```

## Integration with Main Mod

The framework is integrated with the main mod in several key places:

### Plugin.cs

The main plugin initializes the compatibility system:

```csharp
private void Awake()
{
    // Initialize the mod compatibility framework
    ModCompatibilityManager.Instance.Initialize();
    
    // Rest of plugin initialization
}

// Convenience method for checking SellBodies
public bool IsSellBodiesModDetected => IsModInstalled("Entity378.sellbodies");
```

### HealthManager.cs

The health manager uses the framework for despawn logic:

```csharp
private static IEnumerator WaitForDeathAnimationAndDespawn(EnemyAI enemy)
{
    // Default delay without compatibility
    float waitTime = 0.5f;

    // Check for SellBodies compatibility
    var sellBodiesHandler = ModCompatibilityManager.Instance.GetHandler<ModCompatibility.Handlers.SellBodiesCompatibility>("Entity378.sellbodies");
    if (sellBodiesHandler != null && sellBodiesHandler.IsInstalled)
    {
        waitTime = sellBodiesHandler.GetDespawnDelay();
    }

    yield return new WaitForSeconds(waitTime);
    
    // Despawn logic
}
```

## Troubleshooting

### Common Issues and Solutions

1. **"Handler not found" errors:**
   - Ensure the handler is registered in `ModCompatibilityManager.RegisterKnownHandlers()`
   - Check that the ModId exactly matches the target mod's GUID

2. **Reflection errors when detecting mods:**
   - Implement multi-layered detection like in SellBodiesCompatibility
   - Add try/catch blocks around assembly scanning code
   - Cache results to avoid repeated reflection

3. **Compatibility methods not being called:**
   - Verify the compatibility handler is being properly retrieved using the correct ModId
   - Ensure IsInstalled is working correctly and returning true when appropriate

4. **Conflicts with other mods:**
   - Isolate all compatibility code in the handlers
   - Use proper namespaces to avoid naming conflicts
   - Avoid patching the same methods as other mods when possible

## Best Practices

1. **Mod Detection:**
   - Use multiple fallback strategies when detecting if a mod is installed
   - Cache detection results to improve performance
   - Handle all exceptions to prevent one mod from breaking others

2. **Code Organization:**
   - Keep all compatibility logic in the handler, not in the main mod code
   - Follow the single responsibility principle - one handler per mod
   - Document all compatibility points with clear comments

3. **Configuration:**
   - Consider adding config options for enabling/disabling specific compatibilities
   - Make compatibility behavior configurable where appropriate

4. **Future-Proofing:**
   - Version check target mods when possible
   - Design compatibilities to fail gracefully if the target mod changes
   - Log detailed information for debugging

5. **Performance:**
   - Avoid heavy reflection operations at runtime
   - Minimize overhead in frequently called compatibility methods
   - Use caching for expensive operations

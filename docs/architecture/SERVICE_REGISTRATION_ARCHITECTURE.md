# Service Registration Architecture

**Last Updated:** 2026-02-23

## Overview

Modular service registration with each module having its own `ServiceExtensions.cs` for clear separation of concerns.

## Structure

```
D3dxSkinManager/Modules/{ModuleName}/
└── {ModuleName}ServiceExtensions.cs
```

## Main Orchestrator

```csharp
public static IServiceCollection AddD3dxSkinManagerServices(
    this IServiceCollection services,
    string dataPath)
{
    // Register in dependency order
    services.AddCoreServices();           // Shared utilities
    services.AddSettingsServices();       // Global settings
    services.AddSystemServices();         // System utilities
    services.AddProfileServices();        // Profile management

    // Profile-scoped services registered via ProfileServiceRouter
    // These are instantiated per-profile as needed

    services.AddSingleton<IAppFacade, AppFacade>();
    services.AddPluginInfrastructure(dataPath);

    return services;
}
```

## Module Registration Pattern

```csharp
public static class ModsServiceExtensions
{
    public static IServiceCollection AddModsServices(
        this IServiceCollection services,
        string dataPath)
    {
        // 1. Register repositories
        services.AddSingleton<IModRepository>(sp =>
            new ModRepository(dataPath));

        // 2. Register services
        services.AddSingleton<IModService, ModService>();
        services.AddSingleton<IModArchiveService, ModArchiveService>();

        // 3. Register facade
        services.AddSingleton<IModFacade, ModFacade>();

        return services;
    }
}
```

## Module Registration Summary

| Module | Key Services | Scope |
|--------|-------------|--------|
| **Core** | FileService, ProcessService, ImageService | Global |
| **Settings** | SettingsFacade, GlobalSettings | Global |
| **System** | SystemUtilsFacade, FileDialogService | Global |
| **Profiles** | ProfileService, ProfileFacade | Global |
| **Mods** | ModRepository, ModService, ModFacade | Profile |
| **Launch** | LaunchService, D3DMigotoService | Profile |
| **Tools** | CacheService, ValidationService | Profile |
| **Migration** | MigrationService, ParserRegistry | Profile |
| **Plugins** | PluginRegistry, PluginEventBus | Global |
| **Warehouse** | WarehouseService | Global |

## Profile-Scoped Services

Services that operate per-profile are registered through `ProfileServiceRouter`:

```csharp
public class ProfileServiceRouter
{
    private readonly Dictionary<string, IServiceProvider> _profileProviders;

    public T GetService<T>(string profileId)
    {
        var provider = GetOrCreateProvider(profileId);
        return provider.GetRequiredService<T>();
    }

    private IServiceProvider GetOrCreateProvider(string profileId)
    {
        if (!_profileProviders.ContainsKey(profileId))
        {
            var services = new ServiceCollection();
            services.AddModsServices(_dataPath);
            services.AddLaunchServices();
            services.AddToolsServices();
            _profileProviders[profileId] = services.BuildServiceProvider();
        }
        return _profileProviders[profileId];
    }
}
```

## Service Lifetime Guidelines

### Use Singleton for:
- Stateless services
- Shared resources
- Facades
- Repositories (with thread-safe operations)

### Use Scoped for:
- Database contexts
- Request-specific state

### Use Transient for:
- Lightweight, stateless operations
- Factory-created instances

## Registration Best Practices

1. **Module Independence** - Each module registers its own services
2. **Dependency Order** - Register dependencies before consumers
3. **Interface-Based** - Always register interface → implementation
4. **Factory Pattern** - Use factories for complex initialization
5. **Path Injection** - Pass dataPath via constructor/factory

## Common Registration Patterns

### Simple Registration
```csharp
services.AddSingleton<IService, Service>();
```

### Factory Registration
```csharp
services.AddSingleton<IRepository>(sp =>
    new Repository(dataPath));
```

### Conditional Registration
```csharp
if (!services.Any(x => x.ServiceType == typeof(ILogger)))
{
    services.AddSingleton<ILogger, DefaultLogger>();
}
```

### Multiple Implementations
```csharp
services.AddSingleton<IParser, JsonParser>();
services.AddSingleton<IParser, XmlParser>();
// Resolve: IEnumerable<IParser>
```

## AddSingleton Helper Limitation

**CRITICAL:** The custom `AddSingleton` helper does NOT support factory functions:

```csharp
// ❌ WRONG - Helper doesn't support factories
services.AddSingleton<IService>(sp => new Service(sp.GetService<IDep>()));

// ✅ CORRECT - Direct call for factories
services.AddSingleton<IService>(sp => new Service(sp.GetService<IDep>()));

// ✅ CORRECT - Simple registration with helper
services.AddSingleton<IService, Service>();
```

## Related Documentation

- [CURRENT_ARCHITECTURE.md](CURRENT_ARCHITECTURE.md) - System overview
- [MODULE_ARCHITECTURE.md](MODULE_ARCHITECTURE.md) - Module structure
- [APP_FACADE_REFACTORING.md](APP_FACADE_REFACTORING.md) - Facade patterns
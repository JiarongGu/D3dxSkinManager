# Service Registration Architecture

## Overview

The application uses a modular service registration architecture where each module has its own dedicated service registration extension. This provides clear separation of concerns and makes the dependency injection setup easier to understand and maintain.

## Architecture Pattern

### Modular Registration Extensions

Each module has its own `ServiceExtensions.cs` file in a `Configuration` folder:

```
D3dxSkinManager/Modules/
├── Core/Configuration/
│   └── CoreServiceExtensions.cs
├── Mods/Configuration/
│   └── ModsServiceExtensions.cs
├── D3DMigoto/Configuration/
│   └── D3DMigotoServiceExtensions.cs
├── Game/Configuration/
│   └── GameServiceExtensions.cs
├── Tools/Configuration/
│   └── ToolsServiceExtensions.cs
├── Settings/Configuration/
│   └── SettingsServiceExtensions.cs
├── Plugins/Configuration/
│   └── PluginsServiceExtensions.cs
├── Warehouse/Configuration/
│   └── WarehouseServiceExtensions.cs
├── Migration/Configuration/
│   └── MigrationServiceExtensions.cs
└── Profiles/Configuration/
    └── ProfilesServiceExtensions.cs
```

### Main Orchestrator

The main `ServiceCollectionExtensions.cs` orchestrates all module registrations:

```csharp
public static IServiceCollection AddD3dxSkinManagerServices(
    this IServiceCollection services,
    string dataPath)
{
    // Register modules in dependency order
    services.AddCoreServices();
    services.AddProfilesServices(dataPath);
    services.AddToolsServices(dataPath);
    services.AddModsServices(dataPath);
    services.AddD3DMigotoServices(dataPath);
    services.AddGameServices();
    services.AddSettingsServices();
    services.AddPluginsServices();
    services.AddWarehouseServices();
    services.AddMigrationServices(dataPath);

    // Register plugin infrastructure
    services.AddPluginInfrastructure(dataPath);

    return services;
}
```

## Module Registration Details

### 1. Core Module

**File:** `Modules/Core/Configuration/CoreServiceExtensions.cs`

**Responsibilities:**
- Register shared services used across all modules
- File operations, process management, image processing

**Services Registered:**
- `IFileService` → `FileService`
- `IFileSystemService` → `FileSystemService`
- `IProcessService` → `ProcessService`
- `IFileDialogService` → `FileDialogService`
- `IImageService` → `ImageService`

**Extension Methods:**
```csharp
services.AddCoreServices();           // Core services
services.AddImageService(dataPath);   // Image service with path
```

### 2. Mods Module

**File:** `Modules/Mods/Configuration/ModsServiceExtensions.cs`

**Responsibilities:**
- Register mod management services and repository
- Register mod facade

**Services Registered:**
- `IModRepository` → `ModRepository`
- `IModArchiveService` → `ModArchiveService`
- `IModImportService` → `ModImportService`
- `IModQueryService` → `ModQueryService`
- `IModFacade` → `ModFacade`

**Extension Method:**
```csharp
services.AddModsServices(dataPath);
```

**Dependencies:** Core.FileService

### 3. D3DMigoto Module

**File:** `Modules/D3DMigoto/Configuration/D3DMigotoServiceExtensions.cs`

**Responsibilities:**
- Register 3DMigoto version management services
- Register D3DMigoto facade

**Services Registered:**
- `I3DMigotoService` → `D3DMigotoService`
- `ID3DMigotoFacade` → `D3DMigotoFacade`

**Extension Method:**
```csharp
services.AddD3DMigotoServices(dataPath);
```

**Dependencies:**
- Core.FileService
- Tools.IConfigurationService
- Core.ProcessService

### 4. Game Module

**File:** `Modules/Game/Configuration/GameServiceExtensions.cs`

**Responsibilities:**
- Register game launching facade

**Services Registered:**
- `IGameFacade` → `GameFacade`

**Extension Method:**
```csharp
services.AddGameServices();
```

**Dependencies:**
- Core.ProcessService
- Profiles.ProfileService

### 5. Tools Module

**File:** `Modules/Tools/Configuration/ToolsServiceExtensions.cs`

**Responsibilities:**
- Register cache, classification, validation services
- Register configuration service
- Register tools facade

**Services Registered:**
- `IConfigurationService` → `ConfigurationService`
- `IClassificationService` → `ClassificationService`
- `ICacheService` → `CacheService`
- `IStartupValidationService` → `StartupValidationService`
- `IToolsFacade` → `ToolsFacade`

**Extension Method:**
```csharp
services.AddToolsServices(dataPath);
```

**Dependencies:** Mods.IModRepository (for CacheService)

### 6. Settings Module

**File:** `Modules/Settings/Configuration/SettingsServiceExtensions.cs`

**Responsibilities:**
- Register settings facade

**Services Registered:**
- `ISettingsFacade` → `SettingsFacade`

**Extension Method:**
```csharp
services.AddSettingsServices();
```

**Dependencies:**
- Core.FileSystemService
- Core.FileDialogService
- Core.ProcessService

### 7. Plugins Module

**File:** `Modules/Plugins/Configuration/PluginsServiceExtensions.cs`

**Responsibilities:**
- Register plugin management facade

**Services Registered:**
- `IPluginsFacade` → `PluginsFacade`

**Extension Method:**
```csharp
services.AddPluginsServices();
```

**Dependencies:** PluginRegistry (root-level)

### 8. Warehouse Module

**File:** `Modules/Warehouse/Configuration/WarehouseServiceExtensions.cs`

**Responsibilities:**
- Register warehouse facade (placeholder for future)

**Services Registered:**
- `IWarehouseFacade` → `WarehouseFacade`

**Extension Method:**
```csharp
services.AddWarehouseServices();
```

**Status:** Placeholder for future implementation

### 9. Migration Module

**File:** `Modules/Migration/Configuration/MigrationServiceExtensions.cs`

**Responsibilities:**
- Register Python to React migration services
- Register migration facade

**Services Registered:**
- `IMigrationService` → `MigrationService`
- `IMigrationFacade` → `MigrationFacade`

**Extension Method:**
```csharp
services.AddMigrationServices(dataPath);
```

**Dependencies:**
- Mods.IModRepository
- Core.FileService
- Core.ImageService
- Tools.IConfigurationService

### 10. Profiles Module

**File:** `Modules/Profiles/Configuration/ProfilesServiceExtensions.cs`

**Responsibilities:**
- Register profile management services
- Register profile facade

**Services Registered:**
- `IProfileService` → `ProfileService`
- `IProfileFacade` → `ProfileFacade`

**Extension Method:**
```csharp
services.AddProfilesServices(dataPath);
```

## Dependency Order

Services must be registered in the correct order to satisfy dependencies:

```
1. Core           (no dependencies)
2. Profiles       (no dependencies)
3. Tools          (depends on Mods for Cache)
4. Mods           (depends on Core)
5. D3DMigoto      (depends on Core, Tools)
6. Game           (depends on Core, Profiles)
7. Settings       (depends on Core)
8. Plugins        (depends on PluginRegistry)
9. Warehouse      (no dependencies - placeholder)
10. Migration     (depends on Mods, Core, Tools)
```

## Benefits

### 1. Clear Separation
Each module manages its own service registration, making it clear what services belong to which module.

### 2. Easy to Navigate
Developers can quickly find service registration by looking in the module's Configuration folder.

### 3. Testable
Each module's services can be registered independently for testing.

### 4. Maintainable
Adding or modifying services only requires changes to the specific module's extension file.

### 5. Scalable
New modules can be added by creating a new ServiceExtensions file and calling it from the main orchestrator.

### 6. Self-Documenting
The extension method names clearly indicate what they register:
- `AddCoreServices()` - registers core services
- `AddModsServices(dataPath)` - registers mod services

## Example: Adding a New Module

To add a new module:

1. **Create Configuration folder:**
```
D3dxSkinManager/Modules/NewModule/Configuration/
```

2. **Create ServiceExtensions file:**
```csharp
// NewModuleServiceExtensions.cs
public static class NewModuleServiceExtensions
{
    public static IServiceCollection AddNewModuleServices(
        this IServiceCollection services,
        string dataPath)
    {
        // Register services
        services.AddSingleton<INewService, NewService>();

        // Register facade
        services.AddSingleton<INewModuleFacade, NewModuleFacade>();

        return services;
    }
}
```

3. **Update main ServiceCollectionExtensions:**
```csharp
public static IServiceCollection AddD3dxSkinManagerServices(...)
{
    // ... existing modules ...
    services.AddNewModuleServices(dataPath);
    return services;
}
```

## Testing

### Unit Testing Individual Modules

```csharp
[Test]
public void TestModsModule()
{
    var services = new ServiceCollection();
    services.AddCoreServices();  // Add dependencies first
    services.AddModsServices(testDataPath);

    var provider = services.BuildServiceProvider();
    var facade = provider.GetRequiredService<IModFacade>();

    Assert.NotNull(facade);
}
```

### Integration Testing

```csharp
[Test]
public void TestFullApplicationServices()
{
    var services = new ServiceCollection();
    services.AddD3dxSkinManagerServices(testDataPath);

    var provider = services.BuildServiceProvider();

    // Verify all facades are registered
    Assert.NotNull(provider.GetRequiredService<IModFacade>());
    Assert.NotNull(provider.GetRequiredService<ID3DMigotoFacade>());
    // ... etc
}
```

## Related Documentation

- [REFACTORING_COMPLETE.md](REFACTORING_COMPLETE.md) - Complete refactoring summary
- [FACADE_REFACTORING.md](FACADE_REFACTORING.md) - Facade architecture
- [MODULE_STRUCTURE.md](MODULE_STRUCTURE.md) - Module organization

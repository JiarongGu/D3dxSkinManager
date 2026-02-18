# D3dxSkinManager - Current Architecture Guide

**Last Updated:** 2024

## Overview

D3dxSkinManager is a modern .NET 8 + React application for managing game mods with a clean module-based architecture that aligns frontend and backend components.

## Architecture Principles

1. **Module-Based Organization** - Code organized by business domain (Mods, Profiles, Tools, etc.)
2. **Explicit IPC Routing** - Messages use `{ module, type, payload }` format
3. **Type Safety** - Strong typing across the IPC boundary
4. **Separation of Concerns** - Clear boundaries between modules
5. **DI-First** - Dependency injection throughout

## System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Frontend (React + TypeScript)             │
├─────────────────────────────────────────────────────────────┤
│  Components/Hooks                                            │
│      ↓                                                       │
│  Module Services (ModService, ProfileService, etc.)         │
│      ↓                                                       │
│  BaseModuleService (encapsulates module name)               │
│      ↓                                                       │
│  PhotinoService (IPC bridge)                                │
└──────────────────────────┬──────────────────────────────────┘
                           │ IPC Messages
                           │ { module, type, payload }
┌──────────────────────────┴──────────────────────────────────┐
│                    Backend (.NET 8 + Photino)                │
├─────────────────────────────────────────────────────────────┤
│  Program.cs (IPC Handler)                                    │
│      ↓                                                       │
│  Plugin Interception (optional)                              │
│      ↓                                                       │
│  AppFacade (Top-Level Router)                               │
│      ↓                                                       │
│  Module Facade (ModFacade, ProfileFacade, etc.)             │
│      ↓                                                       │
│  Services (Business Logic)                                   │
│      ↓                                                       │
│  Repositories/External Systems                               │
└─────────────────────────────────────────────────────────────┘
```

## Module Structure

### Backend Modules

Location: `D3dxSkinManager/Modules/{ModuleName}/`

Each module contains:
```
Modules/{ModuleName}/
├── I{ModuleName}Facade.cs        # Facade interface
├── {ModuleName}Facade.cs         # Facade implementation (IPC routing)
├── {ModuleName}ServiceExtensions.cs  # DI registration
├── Models/                       # Module-specific models
│   └── *.cs
└── Services/                     # Module-specific services
    ├── I*.cs                     # Service interfaces
    └── *.cs                      # Service implementations
```

**Available Modules:**
- **Core** - Shared services (file system, process management, image handling)
- **Mods** - Mod management and operations
- **Profiles** - Profile management and switching
- **D3DMigoto** - 3DMigoto version management
- **Game** - Game detection and launching
- **Tools** - Cache management, classification, validation
- **Settings** - Application settings and file dialogs
- **Plugins** - Plugin management
- **Warehouse** - Mod discovery (future)
- **Migration** - Python-to-React migration

### Frontend Modules

Location: `D3dxSkinManager.Client/src/modules/{moduleName}/`

Each module contains:
```
modules/{moduleName}/
├── components/              # React components
│   └── *.tsx
├── hooks/                   # Custom hooks
│   └── use*.ts
├── services/                # Module service
│   └── {moduleName}Service.ts
└── types/                   # TypeScript types
    └── *.types.ts
```

## IPC Message Format

### Message Structure

```typescript
interface PhotinoMessage {
  id: string;           // Unique message ID
  module: ModuleName;   // Target module (e.g., 'MOD', 'PROFILE')
  type: string;         // Action within module (e.g., 'GET_ALL', 'CREATE')
  payload?: any;        // Optional data
}
```

### Example Messages

```typescript
// Get all mods
{
  id: "msg_1_1234567890",
  module: "MOD",
  type: "GET_ALL",
  payload: undefined
}

// Load a specific mod
{
  id: "msg_2_1234567891",
  module: "MOD",
  type: "LOAD",
  payload: { sha: "abc123" }
}

// Create a profile
{
  id: "msg_3_1234567892",
  module: "PROFILE",
  type: "CREATE",
  payload: {
    name: "My Profile",
    workDirectory: "C:\\Games\\MyGame"
  }
}
```

## Frontend Service Pattern

### Base Service Class

```typescript
// All module services extend BaseModuleService
abstract class BaseModuleService {
  protected readonly moduleName: ModuleName;

  constructor(moduleName: ModuleName) {
    this.moduleName = moduleName;
  }

  // Core method
  protected async sendMessage<T>(type: string, payload?: any): Promise<T> {
    return photinoService.sendMessage<T>(this.moduleName, type, payload);
  }

  // Convenience methods
  protected async sendBooleanMessage(type: string, payload?: any): Promise<boolean>
  protected async sendArrayMessage<T>(type: string, payload?: any): Promise<T[]>
  protected async sendNullableMessage<T>(type: string, payload?: any): Promise<T | null>
}
```

### Module Service Example

```typescript
class ModService extends BaseModuleService {
  constructor() {
    super('MOD');  // Module name set once
  }

  async getAllMods(): Promise<ModInfo[]> {
    return this.sendArrayMessage<ModInfo>('GET_ALL');
  }

  async loadMod(sha: string): Promise<boolean> {
    return this.sendBooleanMessage('LOAD', { sha });
  }
}

export const modService = new ModService();
```

### Usage in Components

```typescript
import { modService } from '../services/modService';

const MyComponent = () => {
  const [mods, setMods] = useState<ModInfo[]>([]);

  useEffect(() => {
    modService.getAllMods().then(setMods);
  }, []);

  const handleLoad = (sha: string) => {
    modService.loadMod(sha);
  };

  return (/* ... */);
};
```

## Backend Routing Pattern

### AppFacade (Top-Level Router)

```csharp
public class AppFacade : IAppFacade
{
    private readonly IModFacade _modFacade;
    private readonly IProfileFacade _profileFacade;
    // ... other facades

    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        // Validate Module field is present
        if (string.IsNullOrEmpty(request.Module))
        {
            throw new InvalidOperationException("Module field required");
        }

        return await RouteByModule(request);
    }

    private IModuleFacade? GetFacadeByModuleName(string moduleName)
    {
        return moduleName.ToUpperInvariant() switch
        {
            "MOD" or "MODS" => _modFacade,
            "PROFILE" or "PROFILES" => _profileFacade,
            // ... other modules
            _ => null
        };
    }
}
```

### Module Facade (Module-Level Router)

```csharp
public class ModFacade : IModFacade
{
    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        try
        {
            object? responseData = request.Type switch
            {
                "GET_ALL" => await GetAllModsAsync(),
                "LOAD" => await LoadModAsync(request),
                "UNLOAD" => await UnloadModAsync(request),
                "DELETE" => await DeleteModAsync(request),
                _ => throw new InvalidOperationException($"Unknown type: {request.Type}")
            };

            return MessageResponse.CreateSuccess(request.Id, responseData);
        }
        catch (Exception ex)
        {
            return MessageResponse.CreateError(request.Id, ex.Message);
        }
    }

    private async Task<List<ModInfo>> GetAllModsAsync()
    {
        return await _repository.GetAllAsync();
    }

    // ... other methods
}
```

## Dependency Injection

### Service Registration

```csharp
// Main registration (orchestrates modules)
public static IServiceCollection AddD3dxSkinManagerServices(
    this IServiceCollection services,
    string dataPath)
{
    // Register modules in dependency order
    services.AddCoreServices();
    services.AddImageService(dataPath);
    services.AddProfilesServices(dataPath);
    services.AddToolsServices(dataPath);
    services.AddModsServices(dataPath);
    services.AddD3DMigotoServices(dataPath);
    services.AddGameServices();
    services.AddSettingsServices();
    services.AddPluginsServices();
    services.AddWarehouseServices();
    services.AddMigrationServices(dataPath);

    // Register top-level facade
    services.AddSingleton<IAppFacade, AppFacade>();

    // Register plugin infrastructure
    services.AddSingleton<ILogger, ConsoleLogger>();
    services.AddSingleton<PluginRegistry>();
    services.AddSingleton<PluginEventBus>();
    services.AddSingleton<PluginContext>();

    return services;
}
```

### Module Registration Example

```csharp
// ModsServiceExtensions.cs
public static IServiceCollection AddModsServices(
    this IServiceCollection services,
    string dataPath)
{
    // Register repositories
    services.AddSingleton<IModRepository>(sp =>
        new ModRepository(dataPath));

    // Register services
    services.AddSingleton<IModArchiveService, ModArchiveService>();
    services.AddSingleton<IModImportService, ModImportService>();
    services.AddSingleton<IModQueryService, ModQueryService>();

    // Register facade
    services.AddSingleton<IModFacade, ModFacade>();

    return services;
}
```

## Plugin System

### Plugin Architecture

```csharp
public interface IPlugin
{
    string Name { get; }
    string Version { get; }
    Task InitializeAsync(IPluginContext context);
    Task ShutdownAsync();
}

public interface IMessageHandlerPlugin : IPlugin
{
    bool CanHandleMessage(string messageType);
    Task<MessageResponse> HandleMessageAsync(MessageRequest request);
}
```

### Plugin Registration

Plugins are discovered from `data/plugins/` directory and can:
- Register services with DI container
- Handle custom IPC messages
- Subscribe to application events
- Access core services through `IPluginContext`

### Plugin Interception Flow

```
IPC Message → Program.cs
    ↓
Check PluginRegistry.CanHandleMessage()
    ↓
If Yes: Route to plugin
If No: Route to AppFacade → Module Facade
```

## Key Design Patterns

### 1. Facade Pattern
- **Purpose:** Provide unified interface for module operations
- **Implementation:** Each module has a facade that routes IPC messages
- **Example:** `ModFacade` routes to `ModRepository`, `ModImportService`, etc.

### 2. Repository Pattern
- **Purpose:** Abstract data access
- **Implementation:** `ModRepository` handles mod storage
- **Example:** `IModRepository.GetAllAsync()` retrieves mods from file system

### 3. Service Layer Pattern
- **Purpose:** Encapsulate business logic
- **Implementation:** Services like `ModImportService`, `CacheService`
- **Example:** `ModImportService.ImportAsync()` handles mod extraction and metadata

### 4. Dependency Injection
- **Purpose:** Loose coupling and testability
- **Implementation:** .NET DI container with interface-based registration
- **Example:** Facades depend on service interfaces, not implementations

### 5. Event Bus Pattern
- **Purpose:** Decouple plugins from core application
- **Implementation:** `PluginEventBus` emits events (ApplicationStarted, ModLoaded, etc.)
- **Example:** Plugins subscribe to events without tight coupling

## Data Flow Examples

### Example 1: Loading a Mod

```
1. User clicks "Load" button
   ↓
2. Component calls: modService.loadMod(sha)
   ↓
3. ModService sends: { module: 'MOD', type: 'LOAD', payload: { sha } }
   ↓
4. PhotinoService → IPC → Program.cs
   ↓
5. Program.cs → AppFacade.HandleMessageAsync()
   ↓
6. AppFacade routes to ModFacade
   ↓
7. ModFacade.HandleMessageAsync() routes to LoadModAsync()
   ↓
8. LoadModAsync() calls ModRepository.LoadAsync(sha)
   ↓
9. Repository loads mod files, emits ModLoaded event
   ↓
10. Success response travels back to frontend
   ↓
11. Component updates UI
```

### Example 2: Creating a Profile

```
1. User fills profile form, clicks "Create"
   ↓
2. Component calls: profileService.createProfile(request)
   ↓
3. ProfileService sends: { module: 'PROFILE', type: 'CREATE', payload: request }
   ↓
4. IPC → AppFacade → ProfileFacade
   ↓
5. ProfileFacade.CreateProfileAsync()
   ↓
6. ProfileService.CreateAsync() creates profile directories
   ↓
7. ProfileConfiguration written to file
   ↓
8. ProfileCreated event emitted
   ↓
9. New Profile returned to frontend
   ↓
10. UI updates with new profile
```

## File Structure

```
D3dxSkinManager/
├── D3dxSkinManager/                 # Backend (.NET 8)
│   ├── Configuration/               # DI registration
│   ├── Facades/                     # Top-level facades
│   │   ├── IAppFacade.cs
│   │   └── AppFacade.cs
│   ├── Modules/                     # Module implementations
│   │   ├── Core/
│   │   ├── Mods/
│   │   ├── Profiles/
│   │   └── ...
│   ├── Plugins/                     # Plugin infrastructure
│   └── Program.cs                   # Entry point
│
├── D3dxSkinManager.Client/          # Frontend (React + TypeScript)
│   └── src/
│       ├── modules/                 # Feature modules
│       │   ├── mods/
│       │   ├── profiles/
│       │   └── ...
│       ├── shared/                  # Shared utilities
│       │   ├── services/
│       │   │   ├── baseModuleService.ts
│       │   │   ├── photino.ts
│       │   │   └── ...
│       │   └── types/
│       └── App.tsx
│
├── Plugins/                         # Plugin projects
│   ├── ExamplePlugin/
│   └── ...
│
└── docs/                            # Documentation
    ├── architecture/
    ├── core/
    └── features/
```

## Adding a New Feature

### 1. Backend (if new module needed)

```csharp
// 1. Create module folder: Modules/NewModule/

// 2. Create facade interface
public interface INewModuleFacade : IModuleFacade
{
    Task<MessageResponse> HandleMessageAsync(MessageRequest request);
    // Module-specific methods
}

// 3. Create facade implementation
public class NewModuleFacade : INewModuleFacade
{
    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        object? responseData = request.Type switch
        {
            "ACTION1" => await Action1Async(request),
            _ => throw new InvalidOperationException($"Unknown: {request.Type}")
        };
        return MessageResponse.CreateSuccess(request.Id, responseData);
    }
}

// 4. Create service registration
public static class NewModuleServiceExtensions
{
    public static IServiceCollection AddNewModuleServices(
        this IServiceCollection services)
    {
        services.AddSingleton<INewModuleFacade, NewModuleFacade>();
        return services;
    }
}

// 5. Register in ServiceCollectionExtensions.cs
services.AddNewModuleServices();

// 6. Add to AppFacade.GetFacadeByModuleName()
"NEWMODULE" => _newModuleFacade,
```

### 2. Frontend

```typescript
// 1. Create service class
class NewModuleService extends BaseModuleService {
  constructor() {
    super('NEWMODULE');
  }

  async doAction(param: string): Promise<Result> {
    return this.sendMessage<Result>('ACTION1', { param });
  }
}

export const newModuleService = new NewModuleService();

// 2. Use in components
import { newModuleService } from '../services/newModuleService';

const result = await newModuleService.doAction('value');
```

## Related Documentation

- [APP_FACADE_REFACTORING.md](APP_FACADE_REFACTORING.md) - AppFacade design details
- [FRONTEND_SERVICE_ARCHITECTURE.md](FRONTEND_SERVICE_ARCHITECTURE.md) - Frontend service pattern
- [LEGACY_REMOVAL_COMPLETE.md](LEGACY_REMOVAL_COMPLETE.md) - Legacy IPC removal details
- [SERVICE_REGISTRATION_ARCHITECTURE.md](SERVICE_REGISTRATION_ARCHITECTURE.md) - DI registration details
- [MODULE_STRUCTURE.md](MODULE_STRUCTURE.md) - Module organization guidelines
- [../../README.md](../../README.md) - Project overview

## Summary

D3dxSkinManager uses a modern, clean architecture with:
- ✅ Module-based organization (frontend and backend aligned)
- ✅ Explicit IPC routing with `{ module, type, payload }` format
- ✅ Type-safe communication across IPC boundary
- ✅ BaseModuleService pattern for frontend services
- ✅ AppFacade for centralized backend routing
- ✅ Module facades for domain-specific logic
- ✅ Dependency injection throughout
- ✅ Plugin system for extensibility
- ✅ Clear separation of concerns

This architecture provides excellent maintainability, testability, and scalability! 🚀

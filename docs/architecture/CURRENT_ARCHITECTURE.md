# D3dxSkinManager - Current Architecture

**Last Updated:** 2026-02-23
**Status:** Current Implementation

## Overview

.NET 10 + WinForms + WebView2 + React application with module-based architecture aligned across frontend and backend.

## System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Frontend (React + TypeScript)             │
├─────────────────────────────────────────────────────────────┤
│  Components → Module Services → BaseModuleService            │
│                      ↓                                       │
│              BridgeService (WebView2 IPC)                    │
└──────────────────────────┬──────────────────────────────────┘
                           │ { id, module, type, payload }
┌──────────────────────────┴──────────────────────────────────┐
│                Backend (.NET 10 + WinForms)                  │
├─────────────────────────────────────────────────────────────┤
│  ApplicationHost → IpcCommunicationHandler                   │
│         ↓                                                    │
│  MessageDispatcher → AppFacade (Top Router)                  │
│         ↓                                                    │
│  Module Facades → Services → Repositories                    │
└─────────────────────────────────────────────────────────────┘
```

## Module Structure

### Backend: `Modules/{ModuleName}/`
```
├── I{ModuleName}Facade.cs        # Facade interface
├── {ModuleName}Facade.cs         # IPC routing
├── {ModuleName}ServiceExtensions.cs  # DI registration
├── Models/                       # Domain models
└── Services/                     # Business logic
```

### Frontend: `src/modules/{moduleName}/`
```
├── components/              # React components
├── hooks/                   # Custom hooks
├── services/                # Module service
└── types/                   # TypeScript types
```

### Available Modules
- **Core** - Shared infrastructure
- **Context** - Profile-scoped services
- **Mods** - Mod management
- **Profiles** - Profile management
- **Launch** - Game launching + D3DMigoto
- **Tools** - Cache, validation, classification
- **Settings** - Application settings
- **System** - System utilities
- **Plugins** - Plugin management
- **Migration** - Python migration
- **Warehouse** - Mod discovery (planned)

## IPC Message Format

```typescript
interface IpcRequest<TPayload = unknown> {
  id: string;                // Unique message ID
  module: ModuleName;        // Target module (union type)
  type: string;              // Action within module
  profileId?: string;        // Profile context (top-level)
  payload?: TPayload;        // Typed data
}

interface IpcResponse<TData = unknown> {
  id: string;                // Matches request ID
  success: boolean;          // Operation status
  data?: TData;              // Result data
  error?: string;            // Error message if failed
}

type ModuleName = 'MOD' | 'PROFILE' | 'SETTING' | 'SYSTEM' |
                  'TOOL' | 'PLUGIN' | 'WAREHOUSE' | 'MIGRATION' | 'LAUNCH';
```

## Frontend Patterns

### BaseModuleService
```typescript
abstract class BaseModuleService {
  protected readonly moduleName: ModuleName;

  protected async sendMessage<T, TPayload = unknown>(
    type: string,
    profileId?: string,
    payload?: TPayload
  ): Promise<T> {
    return bridgeService.sendMessage<T>({
      module: this.moduleName,
      type,
      profileId,
      payload
    });
  }
}

// Example implementation
class ModService extends BaseModuleService {
  constructor() { super('MOD'); }

  async getAllMods(): Promise<ModInfo[]> {
    return this.sendArrayMessage<ModInfo>('GET_ALL');
  }
}
```

### Error Handling
```typescript
// Always use unknown + type guard
catch (error: unknown) {
  const msg = error instanceof Error ? error.message : 'Unknown error';
  message.error(msg);
}
```

### Compact Components
Use `CompactButton`, `CompactCard`, etc. from `shared/components/compact/` for consistent UI.

## Backend Patterns

### AppFacade (Top Router)
```csharp
public class AppFacade : IAppFacade {
  public async Task<MessageResponse> HandleMessageAsync(MessageRequest request) {
    var facade = GetFacadeByModuleName(request.Module);
    return await facade.HandleMessageAsync(request);
  }

  private IModuleFacade? GetFacadeByModuleName(string moduleName) =>
    moduleName.ToUpperInvariant() switch {
      "MOD" => _modFacade,
      "PROFILE" => _profileFacade,
      // ... other modules
      _ => null
    };
}
```

### Module Facade
```csharp
public class ModFacade : IModFacade {
  public async Task<MessageResponse> HandleMessageAsync(MessageRequest request) {
    object? responseData = request.Type switch {
      "GET_ALL" => await GetAllModsAsync(),
      "LOAD" => await LoadModAsync(request),
      _ => throw new InvalidOperationException($"Unknown: {request.Type}")
    };
    return MessageResponse.CreateSuccess(request.Id, responseData);
  }
}
```

### Service Registration
```csharp
// Module registration
public static IServiceCollection AddModsServices(this IServiceCollection services) {
  services.AddSingleton<IModRepository, ModRepository>();
  services.AddSingleton<IModService, ModService>();
  services.AddSingleton<IModFacade, ModFacade>();
  return services;
}

// Main registration
services.AddCoreServices();
services.AddSettingsServices();
services.AddProfileServices();
services.AddSingleton<IAppFacade, AppFacade>();
```

## Plugin System

```csharp
public interface IPlugin {
  string Name { get; }
  Task InitializeAsync(IPluginContext context);
}

public interface IMessageHandlerPlugin : IPlugin {
  bool CanHandleMessage(string messageType);
  Task<MessageResponse> HandleMessageAsync(MessageRequest request);
}
```

Plugins can:
- Handle custom IPC messages
- Subscribe to events
- Access core services via IPluginContext

## Data Flow Example

**Loading a Mod:**
```
User Click → modService.loadMod(sha)
    ↓
{ module: 'MOD', type: 'LOAD', payload: { sha } }
    ↓
IPC → AppFacade → ModFacade → ModService → ModRepository
    ↓
Response → IPC → Component Update
```

## Adding New Features

### Backend Module
1. Create `Modules/NewModule/` folder
2. Add facade interface & implementation
3. Create service registration extension
4. Register in AppFacade router

### Frontend Module
1. Create service extending BaseModuleService
2. Define TypeScript types
3. Build components using service

## Key Design Patterns

1. **Facade Pattern** - Unified module interfaces
2. **Repository Pattern** - Data access abstraction
3. **Service Layer** - Business logic encapsulation
4. **Dependency Injection** - Interface-based registration
5. **Event Bus** - Plugin decoupling

## Related Documentation

- [MODULE_ARCHITECTURE.md](MODULE_ARCHITECTURE.md) - Module details
- [APP_FACADE_REFACTORING.md](APP_FACADE_REFACTORING.md) - Routing details
- [DOMAIN_DESIGN.md](DOMAIN_DESIGN.md) - Domain boundaries
- [FRONTEND_CONTEXT_ARCHITECTURE.md](FRONTEND_CONTEXT_ARCHITECTURE.md) - React context
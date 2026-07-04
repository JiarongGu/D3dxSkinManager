# D3dxSkinManager - Current Architecture

**Last Updated:** 2026-03-04
**Status:** Current Implementation
**Scope:** System overview and startup flow. For module boundaries → [DOMAIN_DESIGN.md](DOMAIN_DESIGN.md). For service registration → [SERVICE_REGISTRATION_ARCHITECTURE.md](SERVICE_REGISTRATION_ARCHITECTURE.md).

## Overview

.NET 10 + WinForms + WebView2 + React application with module-based architecture aligned across frontend and backend.

## Application Startup Flow

### Splash Screen
The application shows a minimal splash screen overlay while WebView2 compiles JavaScript (~2 seconds).

**Implementation:**
- **Panel-based overlay** - `SplashScreenPanel` overlays the WebView2 control
- **Theme-aware** - Defaults to dark theme, matches app container colors
- **Minimal design** - 400x4px progress bar, no text
- **Automatic removal** - Frontend sends `APP.INITIALIZED` IPC message when React app is ready

**Colors (from theme-colors.css):**
- Dark theme: Background `#1f1f1f`, Progress bar `#177ddc`
- Light theme: Background `#e6f4ff`, Progress bar `#1890ff`

**Lifecycle:**
1. ApplicationHost creates `SplashScreenPanel` before showing main form
2. Panel added on top of WebView2 control (DockStyle.Fill)
3. WebView2 initializes and loads React app
4. React's `AppInitializer` component sends `APP.INITIALIZED` message
5. ApplicationHost removes splash screen panel

**Files:**
- Backend: `Infrastructure/WebView/SplashScreen.cs`
- Backend: `Infrastructure/ApplicationHost.cs` (ShowSplashScreen/HideSplashScreen)
- Frontend: `shared/services/bridgeService.ts` (notifyAppInitialized)
- Frontend: `shared/components/AppInitializer.tsx` (sends message)

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
│  ApplicationHost → IpcHandler (WebView2 bridge)              │
│         ↓                                                    │
│  MessageDispatcher (middleware pipeline)                     │
│         ↓                                                    │
│  ProfileServiceRouter (module → profile-scoped facade)       │
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

See [MODULE_ARCHITECTURE.md](MODULE_ARCHITECTURE.md) for the full per-module list, status, and
directory detail — this document stays high-level (system flow, IPC format, patterns).

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
  notification.error(msg);
}
```

### Compact Components
Use `CompactButton`, `CompactCard`, etc. from `shared/components/compact/` for consistent UI.

## Backend Patterns

### IPC Routing (MessageDispatcher + ProfileServiceRouter)

There is no central "AppFacade" class. Routing is two pieces:

- **`MessageDispatcher`** (`Modules/Core/Services/MessageDispatcher.cs`) — singleton middleware
  pipeline; supports explicit `UseRoute(module, type, handler)` registrations and falls through to
  the module-facade middleware.
- **`ProfileServiceRouter`** (`Infrastructure/ProfileServiceRouter.cs`) — maps each module name to a
  facade resolver over the **profile-scoped** service provider (`MapModule<TFacade>(moduleName, configureServices)`),
  creates/caches per-profile providers, and runs profile DB migrations on first use.

```csharp
// Registration (per module)
router.MapModule<IModFacade>("MOD", services => services.AddModsServices());

// Dispatch: request.Module → profile-scoped provider → facade.HandleMessageAsync(request)
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
```

## Plugin System

```csharp
public interface IPlugin {
  string Name { get; }
  Task InitAsync(IPluginContext context);
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
User Click → modService.loadMod(id)
    ↓
{ module: 'MOD', type: 'LOAD', payload: { id } }
    ↓
IPC → MessageDispatcher → ProfileServiceRouter → ModFacade → ModService → ModRepository
    ↓
Response → IPC → Component Update
```

## Adding New Features

### Backend Module
1. Create `Modules/NewModule/` folder
2. Add facade interface & implementation (`Modules/NewModule/NewModuleFacade.cs`)
3. Create service registration extension
4. Map the module in `ProfileServiceRouter` (`MapModule<INewModuleFacade>("NEWMODULE", ...)`)

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
- [DOMAIN_DESIGN.md](DOMAIN_DESIGN.md) - Domain boundaries
- [FRONTEND_CONTEXT_ARCHITECTURE.md](FRONTEND_CONTEXT_ARCHITECTURE.md) - React context
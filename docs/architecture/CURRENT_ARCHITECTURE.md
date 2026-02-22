# D3dxSkinManager - Current Architecture Guide

**Last Updated:** 2026-02-22

## Overview

D3dxSkinManager is a modern .NET 10 + WinForms + WebView2 + React application for managing game mods with a clean module-based architecture that aligns frontend and backend components.

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
│  BridgeService (WebView2 IPC bridge)                         │
└──────────────────────────┬──────────────────────────────────┘
                           │ IPC Messages
                           │ { id, module, type, payload, profileId }
┌──────────────────────────┴──────────────────────────────────┐
│                Backend (.NET 10 + WinForms + WebView2)        │
├─────────────────────────────────────────────────────────────┤
│  ApplicationHost (WinForms Form + WebView2)                  │
│      ↓                                                       │
│  IpcCommunicationHandler (WebView2 messages)                 │
│      ↓                                                       │
│  MessageDispatcher (Middleware pipeline)                     │
│      ↓                                                       │
│  ServiceRouter / ProfileServiceRouter                        │
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
- **Settings** - Application settings (global settings, settings files)
- **SystemUtils** - System-level operations (file dialogs, file system operations, path utilities, process launching)
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

## Module Separation: Settings vs SystemUtils

### Settings Module (SETTINGS_*)
**Responsibility:** Application settings and configuration management

**IPC Prefix:** `SETTINGS_*`

**Operations:**
- Global settings (theme, log level, annotation level, window settings)
- Settings file management (get, save, delete, list settings files)

**Example Messages:**
- `SETTINGS_GET_GLOBAL` - Retrieve global settings
- `SETTINGS_UPDATE_GLOBAL` - Update global settings
- `SETTINGS_GET_FILE` - Get a settings file content

### SystemUtils Module (SYSTEM_*)
**Responsibility:** System-level operations and utilities

**IPC Prefix:** `SYSTEM_*`

**Operations:**
- File system operations (open file, open directory, open in explorer)
- File dialogs (open file, open folder, save file)
- Path utilities (convert relative to absolute paths)
- Process operations (launch external processes)

**Example Messages:**
- `SYSTEM_OPEN_FILE_DIALOG` - Show open file dialog
- `SYSTEM_OPEN_FILE_IN_EXPLORER` - Open file location in file explorer
- `SYSTEM_GET_ABSOLUTE_PATH` - Convert relative path to absolute

**Design Rationale:**
Previously, SettingsFacade handled both settings and system operations, violating the single responsibility principle. The refactoring separates these concerns:
- **Settings** focuses on application configuration
- **SystemUtils** focuses on OS-level interactions

This separation improves maintainability and makes each module's purpose clearer.

## IPC Message Format

### Message Structure

**Type-Safe Generic Message Types (2026-02-19):**

```typescript
// Generic message type with type-safe payload
interface IpcRequest<TPayload = unknown> {
  id: string;                // Unique message ID
  module: ModuleName;        // Target module (union type, not string)
  type: MessageType;         // Action within module
  profileId?: string;        // Profile context (top-level, not in payload)
  payload?: TPayload;        // Optional typed data
}

// Generic response type with type-safe data
interface IpcResponse<TData = unknown> {
  id: string;                // Matches request ID
  success: boolean;          // Operation status
  data?: TData;              // Optional typed result
  error?: string;            // Error message if failed
}

// ModuleName is a union type, not string
type ModuleName = 'MOD' | 'PROFILE' | 'SETTINGS' | 'SYSTEM' | 'TOOL' | 'PLUGIN' |
                  'WAREHOUSE' | 'MIGRATION' | 'LAUNCH' | 'D3DMIGOTO';

// MessageType is also a union type
type MessageType = string; // Specific types per module
```

**Key Type Safety Features:**
- Generic payload types eliminate `any` usage
- `ModuleName` union type prevents typos
- Default generic parameters (`= unknown`) maintain backward compatibility
- Profile ID at top level, not in payload

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

## Frontend UI Components Architecture

### Compact Components System

**Location:** `D3dxSkinManager.Client/src/shared/components/compact/`

The application uses a custom Compact components system for consistent UI styling and sizing:

**Available Components:**
- `CompactButton` - Consistent button sizing and styling
- `CompactCard` - Card containers with proper spacing
- `CompactSpace` - Layout spacing component
- `CompactDivider` - Section dividers
- `CompactText` - Typography with proper sizing
- `CompactAlert` - Alert/notification messages
- `CompactSection` - Page sections with consistent padding

**Design Principles:**
- **Consistent Sizing:** All components use standardized sizes for visual harmony
- **Dark Theme Support:** Flat design (no shadows) to avoid Ant Design style mismatches
- **Clean Imports:** All components exported through `compact/index.ts`
- **Ant Design Wrapper:** Wraps Ant Design components with custom styling

**Usage Pattern:**
```typescript
// ❌ Old way (inconsistent)
import { Button, Card, Space } from 'antd';

// ✅ New way (consistent)
import { CompactButton, CompactCard, CompactSpace } from '../../../shared/components/compact';

// Usage
<CompactCard>
  <CompactSpace direction="vertical">
    <CompactButton type="primary" onClick={handleClick}>
      Save
    </CompactButton>
  </CompactSpace>
</CompactCard>
```

**Implementation:**
```typescript
// compact/CompactButton.tsx
import { Button, ButtonProps } from 'antd';
import './CompactButton.css';

export const CompactButton: React.FC<ButtonProps> = (props) => {
  return <Button {...props} className={`compact-button ${props.className || ''}`} />;
};

// CSS provides consistent sizing and dark theme support
```

## Error Handling Architecture

### Frontend Error Handling Pattern (2026-02-19)

**Standardized Pattern:**

```typescript
// ✅ Correct pattern
try {
  await someAsyncOperation();
} catch (error: unknown) {
  const errorMessage = error instanceof Error ? error.message : 'An unexpected error occurred';
  message.error(errorMessage);
  console.error('Operation failed:', error);
}

// ❌ Incorrect patterns (avoid)
catch (error: any) { ... }           // Uses 'any' type
catch (error) { ... }                // Implicit 'any'
catch (error: Error) { ... }         // Assumes error is Error type
```

**Key Principles:**
- Always use `catch (error: unknown)` for type safety
- Use type guards to check error type: `error instanceof Error`
- Provide fallback message for non-Error objects
- Log errors with context for debugging
- User-facing messages should be clear and actionable

**Example with Silent Handling:**
```typescript
try {
  const config = await profileService.getProfileConfig(profileId);
} catch (error: unknown) {
  const errorMessage = error instanceof Error ? error.message : '';
  // Silent handling for expected errors
  if (!errorMessage.includes('Profile ID is required')) {
    message.error('Failed to load profile configuration');
    console.error('Failed to load profile config:', error);
  }
}
```

## Frontend Service Pattern

### Base Service Class

**Type-Safe BaseModuleService (2026-02-19):**

```typescript
// All module services extend BaseModuleService
abstract class BaseModuleService {
  protected readonly moduleName: ModuleName;

  constructor(moduleName: ModuleName) {
    this.moduleName = moduleName;
  }

  // Core method with dual generics for type safety
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

  // Convenience methods with generic payload types
  protected async sendBooleanMessage<TPayload = unknown>(
    type: string,
    profileId?: string,
    payload?: TPayload
  ): Promise<boolean> {
    return this.sendMessage<boolean, TPayload>(type, profileId, payload);
  }

  protected async sendArrayMessage<T, TPayload = unknown>(
    type: string,
    profileId?: string,
    payload?: TPayload
  ): Promise<T[]> {
    return this.sendMessage<T[], TPayload>(type, profileId, payload);
  }

  protected async sendNullableMessage<T, TPayload = unknown>(
    type: string,
    profileId?: string,
    payload?: TPayload
  ): Promise<T | null> {
    return this.sendMessage<T | null, TPayload>(type, profileId, payload);
  }
}
```

**Key Changes:**
- Dual generic parameters: `<T, TPayload = unknown>` for both request and response types
- Profile ID as separate parameter (not in payload)
- Default `unknown` type maintains backward compatibility
- Eliminates all `any` types

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
4. BridgeService → IPC (chrome.webview.postMessage) → IpcCommunicationHandler
   ↓
5. IpcCommunicationHandler → MessageDispatcher → ProfileServiceRouter
   ↓
6. Router dispatches to ModFacade
   ↓
7. ModFacade.HandleMessageAsync() routes to LoadModAsync()
   ↓
8. LoadModAsync() calls ModRepository.LoadAsync(sha)
   ↓
9. Repository loads mod files, emits ModLoaded event
   ↓
10. Success response travels back to frontend via CoreWebView2.PostWebMessageAsJson
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
├── D3dxSkinManager/                 # Backend (.NET 10)
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
│       │   │   ├── bridgeService.ts
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

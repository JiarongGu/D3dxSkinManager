# Module Architecture

**Last Updated:** 2026-02-23
**Status:** Current Implementation

> **Consolidation Note:** This document replaces and consolidates:
> - MODULE_STRUCTURE.md (deprecated)
> - FINAL_MODULE_STRUCTURE.md (deprecated)
> - MODULE_QUICK_REFERENCE.md (deprecated)

---

## Overview

D3dxSkinManager uses a modular architecture where functionality is organized into domain-specific modules. Each module encapsulates related services, models, and facades.

## Module List

| Module | Purpose | Frontend Tab | Status |
|--------|---------|--------------|--------|
| **Core** | Shared infrastructure (file I/O, dialogs, utilities) | N/A | Active |
| **Context** | Profile-scoped services and image handling | N/A | Active |
| **Mods** | Mod management (CRUD, load/unload, metadata) | Mod Management | Active |
| **Profiles** | Profile management and switching | Profile Selector | Active |
| **Settings** | Application settings and preferences | Settings | Active |
| **System** | System utilities and version info | N/A | Active |
| **Tools** | Cache management, validation tools | Tools | Active |
| **Launch** | Game launching, D3DMigoto integration | Game Launch | Active |
| **Migration** | Python to .NET migration utilities | Migration | Active |
| **Plugins** | Plugin system and management | Plugins | Active |

## Directory Structure

```
D3dxSkinManager/
├── Modules/
│   ├── Core/                    # Shared infrastructure
│   │   ├── Models/
│   │   ├── Services/
│   │   ├── Utilities/
│   │   └── CoreServiceExtensions.cs
│   │
│   ├── Context/                 # Profile-scoped context
│   │   ├── Services/
│   │   │   ├── ProfileContext.cs
│   │   │   ├── ImageService.cs
│   │   │   ├── ModAutoDetectionService.cs
│   │   │   ├── ProfilePathService.cs
│   │   │   └── ProfileServerService.cs
│   │   └── ContextServiceExtensions.cs
│   │
│   ├── Mods/                    # Mod management
│   │   ├── Models/
│   │   │   ├── ModInfo.cs
│   │   │   └── ModStatistics.cs
│   │   ├── Services/
│   │   │   ├── ModRepository.cs
│   │   │   ├── ModManagementService.cs
│   │   │   └── ModValidationService.cs
│   │   ├── ModFacade.cs
│   │   └── ModsServiceExtensions.cs
│   │
│   ├── Profiles/                # Profile management
│   │   ├── Models/
│   │   ├── Services/
│   │   ├── ProfilesFacade.cs
│   │   └── ProfilesServiceExtensions.cs
│   │
│   ├── Launch/                  # Game launching
│   │   ├── Services/
│   │   │   ├── D3DMigotoService.cs
│   │   │   ├── GameLaunchService.cs
│   │   │   └── CustomProgramService.cs
│   │   ├── LaunchFacade.cs
│   │   └── LaunchServiceExtensions.cs
│   │
│   └── [Other modules...]
```

## Service Registration

Services are registered using extension methods in each module:

```csharp
// In ApplicationHost.cs or ServiceConfiguration
services.AddCoreServices();
services.AddSettingsServices();
services.AddSystemServices();
services.AddProfileServices();

// Profile-scoped services (registered via ProfileServiceRouter)
services.AddModsServices(dataPath);
services.AddLaunchServices(dataPath);
services.AddToolsServices(dataPath);
```

## Key Services by Module

### Core Module
- **FileService**: File operations (read, write, copy, delete)
- **FileSystemService**: OS file system operations
- **ProcessService**: Process launching
- **FileDialogService**: File/folder picker dialogs
- **PathHelper**: Path resolution and conversion
- **GlobalPathService**: Centralized path management

### Context Module
- **ProfileContext**: Current profile state management
- **ImageService**: Image processing and thumbnails
- **ModAutoDetectionService**: Auto-detection of mod changes
- **ProfilePathService**: Profile-specific path resolution
- **ProfileServerService**: Profile server operations

### Mods Module
- **ModRepository**: Database operations (SQLite)
- **ModManagementService**: Mod CRUD operations
- **ModValidationService**: Mod integrity checks
- **ModFacade**: IPC message handler for mod operations

### Launch Module
- **D3DMigotoService**: 3DMigoto version management
- **GameLaunchService**: Game launching logic
- **CustomProgramService**: Custom program execution
- **LaunchFacade**: IPC message handler for launch operations

## IPC Message Routing

Messages are routed based on module name in the message:

```typescript
// Frontend
type ModuleName = 'MOD' | 'LAUNCH' | 'WAREHOUSE' | 'TOOL' |
                  'PLUGIN' | 'SETTING' | 'SYSTEM' | 'MIGRATION' | 'PROFILE';

interface BridgeMessage {
  module: ModuleName;
  type: string;
  profileId?: string;
  payload?: unknown;
}
```

```csharp
// Backend - MessageDispatcher routes to appropriate facade
switch (request.Module)
{
    case "MOD":
        return await _modFacade.HandleMessageAsync(request);
    case "LAUNCH":
        return await _launchFacade.HandleMessageAsync(request);
    // ... other modules
}
```

## Module Communication Patterns

### 1. Direct Service Injection
Modules can inject services from other modules when needed:

```csharp
public class ModManagementService
{
    private readonly IImageService _imageService; // From Context module
    private readonly IFileService _fileService;   // From Core module
}
```

### 2. Event-Based Communication
Modules can publish events that other modules subscribe to:

```csharp
// Publisher (Mods module)
_eventBus.Publish(new ModLoadedEvent { ModId = modId });

// Subscriber (UI module)
_eventBus.Subscribe<ModLoadedEvent>(OnModLoaded);
```

### 3. Profile-Scoped Services
Services that operate within a profile context are managed by ProfileServiceRouter:

```csharp
var profileServices = _profileRouter.GetServices(profileId);
var modService = profileServices.GetService<IModManagementService>();
```

## Best Practices

1. **Module Independence**: Modules should be as independent as possible
2. **Clear Boundaries**: Don't access repositories from other modules directly
3. **Service Layer**: Always go through service interfaces
4. **Dependency Injection**: Use constructor injection for dependencies
5. **Profile Awareness**: Services handling profile data should be profile-scoped

## Migration Notes

When migrating from the old structure:
1. Look for services in their respective module folders
2. Facades are now inside module folders (e.g., `Mods/ModFacade.cs`)
3. Models are organized by module (e.g., `Mods/Models/ModInfo.cs`)
4. Extension methods for DI are in `{Module}ServiceExtensions.cs`

---

**See Also:**
- [CURRENT_ARCHITECTURE.md](CURRENT_ARCHITECTURE.md) - Overall system architecture
- [APP_FACADE_REFACTORING.md](APP_FACADE_REFACTORING.md) - Centralized AppFacade pattern
- [DOMAIN_DESIGN.md](DOMAIN_DESIGN.md) - Domain-driven design principles
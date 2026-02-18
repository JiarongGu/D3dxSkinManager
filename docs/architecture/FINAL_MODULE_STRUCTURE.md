# Final Backend Module Structure

## Overview

Backend modules restructured to match frontend tabs, with facades moved into their respective module folders following .NET naming conventions.

## Directory Structure

```
D3dxSkinManager/Modules/
├── Core/                           [Shared services]
│   ├── Models/
│   │   ├── MessageRequest.cs
│   │   ├── MessageResponse.cs
│   │   └── FileDialogResult.cs
│   ├── Services/
│   │   ├── FileService.cs
│   │   ├── FileSystemService.cs
│   │   ├── IFileSystemService.cs
│   │   ├── ProcessService.cs
│   │   ├── IProcessService.cs
│   │   ├── FileDialogService.cs
│   │   ├── IFileDialogService.cs
│   │   ├── ImageService.cs
│   │   └── PayloadHelper.cs
│   └── ...
│
├── Mods/                           [Mod Management - Tab 1]
│   ├── Models/
│   │   ├── ModInfo.cs
│   │   └── ModStatistics.cs
│   ├── Services/
│   │   ├── IModRepository.cs
│   │   ├── ModRepository.cs
│   │   ├── IModArchiveService.cs
│   │   ├── ModArchiveService.cs
│   │   ├── IModImportService.cs
│   │   ├── ModImportService.cs
│   │   ├── IModQueryService.cs
│   │   └── ModQueryService.cs
│   ├── Facades/
│   │   ├── IModFacade.cs           [Interface]
│   │   └── ModFacade.cs            [Implementation]
│   └── ...
│
├── D3DMigoto/                      [3DMigoto - Tab 2] - NEW
│   ├── Models/
│   │   ├── D3DMigotoVersion.cs     [from Tools]
│   │   └── DeploymentResult.cs     [from Tools]
│   ├── Services/
│   │   ├── I3DMigotoService.cs     [from Tools]
│   │   └── D3DMigotoService.cs     [from Tools]
│   ├── Facades/
│   │   ├── ID3DMigotoFacade.cs     [Interface]
│   │   └── D3DMigotoFacade.cs      [Implementation]
│   └── ...
│
├── Game/                           [Game Launch - Tab 3] - NEW
│   ├── Models/
│   │   └── (future: GameConfiguration.cs)
│   ├── Services/
│   │   └── (uses Core/ProcessService, Profiles/ProfileService)
│   ├── Facades/
│   │   ├── IGameFacade.cs          [Interface]
│   │   └── GameFacade.cs           [Implementation]
│   └── ...
│
├── Warehouse/                      [Mod Warehouse - Tab 4] - NEW (placeholder)
│   ├── Models/
│   ├── Services/
│   ├── Facades/
│   │   ├── IWarehouseFacade.cs     [Interface]
│   │   └── WarehouseFacade.cs      [Implementation]
│   └── ...
│
├── Tools/                          [Tools - Tab 5]
│   ├── Models/
│   │   ├── CacheModels.cs
│   │   └── ValidationModels.cs
│   ├── Services/
│   │   ├── ICacheService.cs
│   │   ├── CacheService.cs
│   │   ├── IClassificationService.cs
│   │   ├── ClassificationService.cs
│   │   ├── IConfigurationService.cs
│   │   ├── ConfigurationService.cs
│   │   ├── IStartupValidationService.cs
│   │   ├── StartupValidationService.cs
│   │   └── DevelopmentServerManager.cs
│   ├── Facades/
│   │   ├── IToolsFacade.cs         [Interface]
│   │   └── ToolsFacade.cs          [Implementation]
│   └── ...
│
├── Plugins/                        [Plugins - Tab 6] - NEW
│   ├── Models/
│   │   └── PluginInfo.cs
│   ├── Services/
│   │   └── (uses PluginRegistry from root /Plugins folder)
│   ├── Facades/
│   │   ├── IPluginsFacade.cs       [Interface]
│   │   └── PluginsFacade.cs        [Implementation]
│   └── ...
│
├── Settings/                       [Settings - Tab 7] - NEW
│   ├── Models/
│   │   └── (file dialog models could move here from Core)
│   ├── Services/
│   │   └── (uses Core/FileSystemService, Core/FileDialogService)
│   ├── Facades/
│   │   ├── ISettingsFacade.cs      [Interface]
│   │   └── SettingsFacade.cs       [Implementation]
│   └── ...
│
├── Migration/                      [Migration] - EXISTING
│   ├── Models/
│   ├── Services/
│   ├── Facades/
│   │   ├── IMigrationFacade.cs     [Interface - NEW]
│   │   └── MigrationFacade.cs      [Implementation - NEW]
│   └── ...
│
└── Profiles/                       [Profiles] - EXISTING
    ├── Models/
    ├── Services/
    ├── Facades/
    │   ├── IProfileFacade.cs       [Interface - NEW]
    │   └── ProfileFacade.cs        [Implementation - NEW]
    └── ...
```

## Naming Conventions (.NET Standard)

### Interfaces
- Pattern: `I{Module}Facade`
- Examples:
  - `IModFacade.cs`
  - `ID3DMigotoFacade.cs`
  - `IGameFacade.cs`

### Implementations
- Pattern: `{Module}Facade`
- Examples:
  - `ModFacade.cs`
  - `D3DMigotoFacade.cs`
  - `GameFacade.cs`

### Namespaces
- Interface: `D3dxSkinManager.Modules.{Module}.Facades`
- Implementation: `D3dxSkinManager.Modules.{Module}.Facades`

Examples:
```csharp
// Interface
namespace D3dxSkinManager.Modules.Mods.Facades;
public interface IModFacade { ... }

// Implementation
namespace D3dxSkinManager.Modules.Mods.Facades;
public class ModFacade : IModFacade { ... }
```

## File Organization

### Each Module Contains:
1. **Models/** - Data transfer objects, entities
2. **Services/** - Business logic, repository pattern
3. **Facades/** - IPC message handlers, orchestration layer

### Cross-Cutting Concerns (Core module):
- File system operations
- Process management
- Image processing
- Common utilities

## IPC Message Routing

### Message Prefix → Facade Mapping
```
MOD_*           → Modules/Mods/Facades/ModFacade
D3DMIGOTO_*     → Modules/D3DMigoto/Facades/D3DMigotoFacade
GAME_*          → Modules/Game/Facades/GameFacade
WAREHOUSE_*     → Modules/Warehouse/Facades/WarehouseFacade
TOOLS_*         → Modules/Tools/Facades/ToolsFacade
PLUGINS_*       → Modules/Plugins/Facades/PluginsFacade
SETTINGS_*      → Modules/Settings/Facades/SettingsFacade
MIGRATION_*     → Modules/Migration/Facades/MigrationFacade
PROFILE_*       → Modules/Profiles/Facades/ProfileFacade
```

## Implementation Steps

### Step 1: Create New Module Folders
```bash
mkdir D3dxSkinManager/Modules/D3DMigoto
mkdir D3dxSkinManager/Modules/D3DMigoto/Facades
mkdir D3dxSkinManager/Modules/D3DMigoto/Models
mkdir D3dxSkinManager/Modules/D3DMigoto/Services

mkdir D3dxSkinManager/Modules/Game
mkdir D3dxSkinManager/Modules/Game/Facades

mkdir D3dxSkinManager/Modules/Warehouse
mkdir D3dxSkinManager/Modules/Warehouse/Facades

mkdir D3dxSkinManager/Modules/Plugins
mkdir D3dxSkinManager/Modules/Plugins/Facades
mkdir D3dxSkinManager/Modules/Plugins/Models

mkdir D3dxSkinManager/Modules/Settings
mkdir D3dxSkinManager/Modules/Settings/Facades

mkdir D3dxSkinManager/Modules/Mods/Facades
mkdir D3dxSkinManager/Modules/Tools/Facades
mkdir D3dxSkinManager/Modules/Migration/Facades
mkdir D3dxSkinManager/Modules/Profiles/Facades
```

### Step 2: Move Facade Files
Move facade interfaces and implementations from `D3dxSkinManager/Facades/` to their respective module folders.

### Step 3: Extract Services to New Modules
Move 3DMigoto-related code from `Tools` to `D3DMigoto` module.

### Step 4: Update Namespaces
Update all namespaces to reflect new locations.

### Step 5: Update Service Registration
Update `ServiceCollectionExtensions.cs` with new namespaces.

### Step 6: Update Program.cs
Update facade resolution to use new namespaces.

## Benefits

1. **Clear Module Boundaries:** Each frontend tab has a corresponding backend module
2. **Consistent Structure:** Every module follows the same pattern (Models/Services/Facades)
3. **Easy Navigation:** Developers can easily find code related to a specific feature
4. **Scalability:** New features can be added as new modules
5. **Testability:** Each module can be tested independently
6. **.NET Conventions:** Follows standard .NET naming patterns

## Example: Mods Module

```
D3dxSkinManager/Modules/Mods/
├── Models/
│   ├── ModInfo.cs
│   └── ModStatistics.cs
├── Services/
│   ├── IModRepository.cs
│   ├── ModRepository.cs
│   ├── IModArchiveService.cs
│   ├── ModArchiveService.cs
│   ├── IModImportService.cs
│   ├── ModImportService.cs
│   ├── IModQueryService.cs
│   └── ModQueryService.cs
└── Facades/
    ├── IModFacade.cs
    └── ModFacade.cs
```

### IModFacade.cs
```csharp
namespace D3dxSkinManager.Modules.Mods.Facades;

public interface IModFacade
{
    Task<MessageResponse> HandleMessageAsync(MessageRequest request);
    Task<List<ModInfo>> GetAllModsAsync();
    // ... other methods
}
```

### ModFacade.cs
```csharp
namespace D3dxSkinManager.Modules.Mods.Facades;

public class ModFacade : IModFacade
{
    // Implementation
}
```

## Related Documents
- [Facade Refactoring](FACADE_REFACTORING.md)
- [Module Structure](MODULE_STRUCTURE.md)

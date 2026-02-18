# Backend Facade Refactoring

## Overview

This document describes the refactoring of the backend architecture to align with frontend module structure. The refactoring splits the monolithic `ModFacade` into module-specific facades that correspond to the frontend tabs.

## Frontend Tabs → Backend Facades Mapping

| Frontend Tab | Frontend Module | Backend Facade | IPC Prefix | Responsibility |
|--------------|-----------------|----------------|------------|----------------|
| Mod Management | `modules/mods` | `ModFacade` | `MOD_*` | Mod operations, metadata |
| 3DMigoto | `modules/d3dmigoto` | `D3DMigotoFacade` | `D3DMIGOTO_*` | 3DMigoto version management, launch |
| Game Launch | `modules/game` | `GameFacade` | `GAME_*` | Game launching, custom programs |
| Mod Warehouse | `modules/warehouse` | `WarehouseFacade` | `WAREHOUSE_*` | Mod discovery, download (future) |
| Tools | `modules/tools` | `ToolsFacade` | `TOOLS_*` | Cache management, validation |
| Plugins | `modules/plugins` | `PluginsFacade` | `PLUGINS_*` | Plugin management |
| Settings | `modules/settings` | `SettingsFacade` | `SETTINGS_*` | App settings, file dialogs |
| (Migration) | `modules/migration` | `MigrationFacade` | `MIGRATION_*` | Python→React migration |
| (Profiles) | `modules/profiles` | `ProfileFacade` | `PROFILE_*` | Profile management |

## Architecture Benefits

### 1. **Clear Separation of Concerns**
Each facade handles operations for a specific domain, making the codebase easier to understand and maintain.

### 2. **Consistent IPC Message Naming**
Message types now follow a consistent pattern: `{MODULE}_{ACTION}`
- Example: `MOD_GET_ALL`, `D3DMIGOTO_DEPLOY`, `PROFILE_SWITCH`

### 3. **Easier Testing**
Module-specific facades can be tested independently with focused unit tests.

### 4. **Scalable Architecture**
New features can be added to specific facades without affecting others.

### 5. **Better Plugin Integration**
Plugins can register handlers for specific module message types.

## Facade Implementations

### ModFacade (MOD_*)
**Location:** `D3dxSkinManager/Facades/Implementations/ModFacadeImpl.cs`

**Responsibilities:**
- Mod CRUD operations
- Load/unload mods
- Metadata management
- Search and filtering

**Dependencies:**
- `IModRepository`
- `IModArchiveService`
- `IModImportService`
- `IModQueryService`

**IPC Messages:**
- `MOD_GET_ALL` - Get all mods
- `MOD_GET_BY_ID` - Get mod by SHA
- `MOD_LOAD` - Load mod archive
- `MOD_UNLOAD` - Unload mod archive
- `MOD_IMPORT` - Import new mod
- `MOD_DELETE` - Delete mod
- `MOD_UPDATE_METADATA` - Update mod metadata
- `MOD_BATCH_UPDATE_METADATA` - Batch update metadata
- `MOD_SEARCH` - Search mods
- `MOD_GET_AUTHORS` - Get author list
- `MOD_GET_TAGS` - Get tag list
- `MOD_GET_OBJECTS` - Get object names

### D3DMigotoFacade (D3DMIGOTO_*)
**Location:** `D3dxSkinManager/Facades/Implementations/D3DMigotoFacadeImpl.cs`

**Responsibilities:**
- Manage 3DMigoto versions
- Deploy 3DMigoto to game directory
- Launch 3DMigoto

**Dependencies:**
- `I3DMigotoService`

**IPC Messages:**
- `D3DMIGOTO_GET_VERSIONS` - Get available versions
- `D3DMIGOTO_GET_CURRENT` - Get current version
- `D3DMIGOTO_DEPLOY` - Deploy version
- `D3DMIGOTO_LAUNCH` - Launch 3DMigoto

### GameFacade (GAME_*)
**Location:** `D3dxSkinManager/Facades/Implementations/GameFacadeImpl.cs`

**Responsibilities:**
- Launch game with 3DMigoto
- Launch custom programs
- Manage launch arguments

**Dependencies:**
- `IProcessService`
- `IProfileService` (for game path/args)

**IPC Messages:**
- `GAME_LAUNCH` - Launch game
- `GAME_LAUNCH_CUSTOM` - Launch custom program

### ToolsFacade (TOOLS_*)
**Location:** `D3dxSkinManager/Facades/Implementations/ToolsFacadeImpl.cs`

**Responsibilities:**
- Cache management
- Startup validation
- System diagnostics

**Dependencies:**
- `ICacheService`
- `IStartupValidationService`

**IPC Messages:**
- `TOOLS_SCAN_CACHE` - Scan cache directories
- `TOOLS_GET_CACHE_STATS` - Get cache statistics
- `TOOLS_CLEAN_CACHE` - Clean cache by category
- `TOOLS_DELETE_CACHE_ITEM` - Delete specific cache item
- `TOOLS_VALIDATE_STARTUP` - Run startup validation

### SettingsFacade (SETTINGS_*)
**Location:** `D3dxSkinManager/Facades/Implementations/SettingsFacadeImpl.cs`

**Responsibilities:**
- Application settings
- File system operations (open file/folder)
- File dialogs

**Dependencies:**
- `IFileSystemService`
- `IFileDialogService`

**IPC Messages:**
- `SETTINGS_OPEN_FILE` - Open file in default app
- `SETTINGS_OPEN_FOLDER` - Open folder in explorer
- `SETTINGS_OPEN_FILE_IN_EXPLORER` - Show file in explorer
- `SETTINGS_LAUNCH_PROCESS` - Launch external process
- `SETTINGS_OPEN_FILE_DIALOG` - Show open file dialog
- `SETTINGS_OPEN_FOLDER_DIALOG` - Show open folder dialog
- `SETTINGS_SAVE_FILE_DIALOG` - Show save file dialog

### PluginsFacade (PLUGINS_*)
**Location:** `D3dxSkinManager/Facades/Implementations/PluginsFacadeImpl.cs`

**Responsibilities:**
- List plugins
- Enable/disable plugins
- Plugin metadata

**Dependencies:**
- `PluginRegistry`
- `PluginEventBus`

**IPC Messages:**
- `PLUGINS_GET_ALL` - Get all plugins
- `PLUGINS_ENABLE` - Enable plugin
- `PLUGINS_DISABLE` - Disable plugin

### WarehouseFacade (WAREHOUSE_*)
**Location:** `D3dxSkinManager/Facades/Implementations/WarehouseFacadeImpl.cs`

**Responsibilities:**
- Mod discovery (future)
- Mod download (future)
- Warehouse search (future)

**Status:** Placeholder for future implementation

**IPC Messages:**
- `WAREHOUSE_SEARCH` - Search warehouse (future)
- `WAREHOUSE_DOWNLOAD` - Download mod (future)

### MigrationFacade (MIGRATION_*)
**Location:** `D3dxSkinManager/Facades/Implementations/MigrationFacadeImpl.cs`

**Responsibilities:**
- Python installation detection
- Migration analysis
- Data migration from Python version

**Dependencies:**
- `IMigrationService`

**IPC Messages:**
- `MIGRATION_AUTO_DETECT` - Auto-detect Python installation
- `MIGRATION_ANALYZE` - Analyze Python source
- `MIGRATION_START` - Start migration process
- `MIGRATION_VALIDATE` - Validate migration result

### ProfileFacade (PROFILE_*)
**Location:** `D3dxSkinManager/Facades/Implementations/ProfileFacadeImpl.cs`

**Responsibilities:**
- Profile CRUD operations
- Profile switching
- Profile configuration

**Dependencies:**
- `IProfileService`

**IPC Messages:**
- `PROFILE_GET_ALL` - Get all profiles
- `PROFILE_GET_ACTIVE` - Get active profile
- `PROFILE_GET_BY_ID` - Get profile by ID
- `PROFILE_CREATE` - Create profile
- `PROFILE_UPDATE` - Update profile
- `PROFILE_DELETE` - Delete profile
- `PROFILE_SWITCH` - Switch profile
- `PROFILE_DUPLICATE` - Duplicate profile
- `PROFILE_EXPORT_CONFIG` - Export configuration
- `PROFILE_GET_CONFIG` - Get configuration
- `PROFILE_UPDATE_CONFIG` - Update configuration

## Program.cs Refactoring

The main entry point (`Program.cs`) will:

1. Initialize all facades via DI
2. Route IPC messages to appropriate facade based on message prefix
3. Handle plugin message interception

```csharp
private static Dictionary<string, IFacade> _facades = new();

private static void InitializeFacades()
{
    _facades["MOD"] = _serviceProvider.GetRequiredService<IModFacade>();
    _facades["D3DMIGOTO"] = _serviceProvider.GetRequiredService<ID3DMigotoFacade>();
    _facades["GAME"] = _serviceProvider.GetRequiredService<IGameFacade>();
    _facades["TOOLS"] = _serviceProvider.GetRequiredService<IToolsFacade>();
    _facades["SETTINGS"] = _serviceProvider.GetRequiredService<ISettingsFacade>();
    _facades["PLUGINS"] = _serviceProvider.GetRequiredService<IPluginsFacade>();
    _facades["WAREHOUSE"] = _serviceProvider.GetRequiredService<IWarehouseFacade>();
    _facades["MIGRATION"] = _serviceProvider.GetRequiredService<IMigrationFacade>();
    _facades["PROFILE"] = _serviceProvider.GetRequiredService<IProfileFacade>();
}

private static IFacade? GetFacadeForMessage(string messageType)
{
    var prefix = messageType.Split('_')[0];
    return _facades.GetValueOrDefault(prefix);
}
```

## Service Registration

Update `ServiceCollectionExtensions.cs`:

```csharp
public static IServiceCollection AddD3dxSkinManagerServices(
    this IServiceCollection services,
    string dataPath)
{
    // ... existing services ...

    // Register Facades
    services.AddSingleton<IModFacade, ModFacadeImpl>();
    services.AddSingleton<ID3DMigotoFacade, D3DMigotoFacadeImpl>();
    services.AddSingleton<IGameFacade, GameFacadeImpl>();
    services.AddSingleton<IToolsFacade, ToolsFacadeImpl>();
    services.AddSingleton<ISettingsFacade, SettingsFacadeImpl>();
    services.AddSingleton<IPluginsFacade, PluginsFacadeImpl>();
    services.AddSingleton<IWarehouseFacade, WarehouseFacadeImpl>();
    services.AddSingleton<IMigrationFacade, MigrationFacadeImpl>();
    services.AddSingleton<IProfileFacade, ProfileFacadeImpl>();

    return services;
}
```

## Frontend Updates

Frontend services will be updated to use new IPC message types:

### Before:
```typescript
await photino.sendMessage('GET_ALL_MODS', {});
await photino.sendMessage('LOAD_MOD', { sha });
```

### After:
```typescript
await photino.sendMessage('MOD_GET_ALL', {});
await photino.sendMessage('MOD_LOAD', { sha });
```

## Migration Strategy

### Phase 1: Backend Refactoring
1. ✅ Create facade interfaces
2. ✅ Create `ModFacadeImpl` with MOD_* messages
3. ⏳ Create remaining facade implementations
4. ⏳ Update `Program.cs` to route by prefix
5. ⏳ Update service registration

### Phase 2: Backward Compatibility
- Keep support for legacy message types (e.g., `GET_ALL_MODS` → `MOD_GET_ALL`)
- Add deprecation warnings in console logs

### Phase 3: Frontend Updates
1. Update photino service message types
2. Update individual module services
3. Test all frontend functionality

### Phase 4: Cleanup
1. Remove legacy message type support
2. Remove old `ModFacade.cs`
3. Update documentation

## Testing Checklist

- [ ] All MOD_* messages work
- [ ] All D3DMIGOTO_* messages work
- [ ] All GAME_* messages work
- [ ] All TOOLS_* messages work
- [ ] All SETTINGS_* messages work
- [ ] All PROFILE_* messages work
- [ ] All MIGRATION_* messages work
- [ ] Legacy messages still work (backward compatibility)
- [ ] Plugin message routing works
- [ ] All frontend tabs functional

## Benefits Summary

1. **Modularity**: Each facade is self-contained
2. **Clarity**: Message types clearly indicate which module they belong to
3. **Maintainability**: Easier to locate and modify functionality
4. **Testability**: Focused unit tests per facade
5. **Scalability**: Easy to add new features to specific modules
6. **Documentation**: Clear mapping between frontend and backend

## Related Documents

- [Module Structure](MODULE_STRUCTURE.md)
- [Project Architecture](../core/ARCHITECTURE.md)
- [IPC Communication](../core/IPC_PROTOCOL.md) (to be created)

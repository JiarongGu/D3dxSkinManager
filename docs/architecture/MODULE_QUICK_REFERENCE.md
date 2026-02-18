# Module Structure Quick Reference

**Date**: 2026-02-17
**Purpose**: Quick lookup for finding files in the new structure

---

## 🗂️ Backend Module Map

### Core Module (`Modules/Core/`)
**Purpose**: Infrastructure services (file I/O, process management, dialogs)

**Services**:
- `IFileService` / `FileService` - File operations (read, write, copy, delete, extract archives)
- `IFileSystemService` / `FileSystemService` - OS file system operations
- `IProcessService` / `ProcessService` - Process launching
- `IFileDialogService` / `FileDialogService` - File/folder picker dialogs
- `IImageService` / `ImageService` - Image processing (thumbnails, previews)

**Models**:
- `MessageRequest` - IPC request DTO
- `MessageResponse` - IPC response DTO

---

### Mods Module (`Modules/Mods/`)
**Purpose**: Mod management (CRUD, import, export, querying)

**Services**:
- `IModRepository` / `ModRepository` - Database operations (SQLite)
- `IModArchiveService` / `ModArchiveService` - Load/unload mods to work directory
- `IModImportService` / `ModImportService` - Import mod archives
- `IModQueryService` / `ModQueryService` - Search and filter mods

**Models**:
- `ModInfo` - Main mod entity

---

### Profiles Module (`Modules/Profiles/`)
**Purpose**: Profile management (multiple mod configurations)

**Services**:
- `IProfileService` / `ProfileService` - Profile CRUD and switching

**Models**:
- `Profile` - Profile entity
- `ProfileConfiguration` - Profile settings
- `CreateProfileRequest` - Creation DTO
- `UpdateProfileRequest` - Update DTO
- `ProfileSwitchResult` - Switch result DTO
- `ProfileListResponse` - List response DTO

---

### Migration Module (`Modules/Migration/`)
**Purpose**: Python d3dxSkinManage migration

**Services**:
- `IMigrationService` / `MigrationService` - Python to React migration

**Models**:
- `MigrationAnalysis` - Analysis result
- `MigrationOptions` - Migration configuration
- `MigrationProgress` - Progress tracking
- `MigrationResult` - Migration outcome
- `PythonModEntry` - Python mod structure

---

### Tools Module (`Modules/Tools/`)
**Purpose**: Utilities (cache, validation, 3DMigoto, classification)

**Services**:
- `ICacheService` / `CacheService` - Cache management
- `IClassificationService` / `ClassificationService` - Mod classification
- `IConfigurationService` / `ConfigurationService` - App configuration
- `IStartupValidationService` / `StartupValidationService` - Startup checks
- `I3DMigotoService` / `D3DMigotoService` - 3DMigoto version management

**Models**:
- `CacheItem` - Cache entry
- `CacheStatistics` - Cache stats
- `ValidationResult` - Validation outcome
- `StartupValidationReport` - Validation report
- `D3DMigotoVersion` - 3DMigoto version info
- `DeploymentResult` - Deployment outcome

---

### Plugins Module (`Modules/Plugins/`)
**Purpose**: Plugin system

**Classes**:
- `PluginLoader` - Load plugins from DLL
- `PluginRegistry` - Plugin registry
- `PluginEventBus` - Event pub/sub
- `PluginContext` - Plugin execution context
- `IPlugin` - Plugin interface
- `ILogger` / `ConsoleLogger` - Logging

---

## 🗂️ Frontend Module Map

### Core Module (`modules/core/`)
**Purpose**: Infrastructure (layout, navigation, utilities)

**Components**:
- `layout/AppHeader` - Top header with profile switcher
- `layout/AppSider` - Side navigation
- `layout/AppStatusBar` - Bottom status bar
- `common/TooltipSystem` - Tooltip provider
- `windows/HelpWindow` - Help window

**Services**:
- `photino` - IPC communication
- `fileDialogService` - File/folder dialogs
- `fileSystemService` - File system operations

**Utils**:
- `KeyboardShortcutManager` - Keyboard shortcuts

---

### Mods Module (`modules/mods/`)
**Purpose**: Mod UI and operations

**Components**:
- `ModHierarchicalView` - Main mod view
- `ModCard` - Mod card display
- `ModGrid` - Grid layout
- `ModListView` - List layout
- `dialogs/ModEditDialog` - Edit mod metadata
- `dialogs/ModDetailDialog` - View mod details
- `dialogs/TagSelectDialog` - Tag selection

**Services**:
- `modService` - Mod operations (IPC)

**Hooks**:
- `useModData` - Load mod data
- `useModFilters` - Filter logic
- `useModActions` - Mod actions (load, unload, delete)

---

### Profiles Module (`modules/profiles/`)
**Purpose**: Profile UI and management

**Components**:
- `ProfileSwitcher` - Header dropdown
- `ProfileManager` - Management dialog
- `ProfileCard` - Profile display card

**Services**:
- `profileService` - Profile operations (IPC)

---

### Migration Module (`modules/migration/`)
**Purpose**: Python migration UI

**Components**:
- `MigrationWizard` - 4-step migration wizard
- `MigrationProgress` - Progress display

**Services**:
- `migrationService` - Migration operations (IPC)

---

### Tools Module (`modules/tools/`)
**Purpose**: Utility UIs

**Components**:
- `ToolsView` - Main tools page
- `CacheManager` - Cache management UI
- `ValidationPanel` - Validation display
- `dialogs/KeyboardShortcutsDialog` - Shortcuts help
- `dialogs/AboutDialog` - About dialog
- `dialogs/UnityArgsDialog` - Unity arguments

**Services**:
- `cacheService` - Cache operations
- `validationService` - Validation operations
- `d3dMigotoService` - 3DMigoto operations

---

### Settings Module (`modules/settings/`)
**Purpose**: Application settings

**Components**:
- `SettingsView` - Settings page

**Services**:
- `settingsService` - Settings operations

---

### Warehouse Module (`modules/warehouse/`)
**Purpose**: Mod warehouse/download

**Components**:
- `WarehouseView` - Warehouse page

**Services**:
- `warehouseService` - Warehouse operations

---

### Plugins Module (`modules/plugins/`)
**Purpose**: Plugin UI and SDK

**Components**:
- `PluginsView` - Plugin management page

**Services**:
- `pluginService` - Plugin operations

**SDK**:
- Plugin examples and templates

---

## 🔍 Quick Find

### "Where is the mod database code?"
→ Backend: `Modules/Mods/Services/ModRepository.cs`

### "Where is the profile switcher?"
→ Frontend: `modules/profiles/components/ProfileSwitcher.tsx`

### "Where is the migration wizard?"
→ Frontend: `modules/migration/components/MigrationWizard.tsx`

### "Where is the IPC communication?"
→ Backend: `Facades/ModFacade.cs`
→ Frontend: `modules/core/services/photino.ts`

### "Where are the mod CRUD operations?"
→ Backend: `Modules/Mods/Services/` (ModRepository, ModArchiveService, ModImportService)
→ Frontend: `modules/mods/services/modService.ts`

### "Where is the cache management?"
→ Backend: `Modules/Tools/Services/CacheService.cs`
→ Frontend: `modules/tools/services/cacheService.ts`

### "Where is the profile management?"
→ Backend: `Modules/Profiles/Services/ProfileService.cs`
→ Frontend: `modules/profiles/services/profileService.ts`

### "Where is the 3DMigoto version management?"
→ Backend: `Modules/Tools/Services/D3DMigotoService.cs`
→ Frontend: `modules/tools/services/d3dMigotoService.ts`

### "Where is the file dialog?"
→ Backend: `Modules/Core/Services/FileDialogService.cs`
→ Frontend: `modules/core/services/fileDialogService.ts`

### "Where is the startup validation?"
→ Backend: `Modules/Tools/Services/StartupValidationService.cs`
→ Frontend: `modules/tools/services/validationService.ts`

---

## 📝 Namespace Reference

### Backend Namespaces

```csharp
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Mods.Models;
using D3dxSkinManager.Modules.Mods.Services;
using D3dxSkinManager.Modules.Profiles.Models;
using D3dxSkinManager.Modules.Profiles.Services;
using D3dxSkinManager.Modules.Migration.Models;
using D3dxSkinManager.Modules.Migration.Services;
using D3dxSkinManager.Modules.Tools.Models;
using D3dxSkinManager.Modules.Tools.Services;
using D3dxSkinManager.Modules.Plugins;
```

### Frontend Import Paths

```typescript
// Core
import { photinoService } from '@modules/core/services/photino';
import { AppHeader } from '@modules/core/components/layout/AppHeader';

// Mods
import { modService } from '@modules/mods/services/modService';
import { ModCard } from '@modules/mods/components/ModCard';
import { useModData } from '@modules/mods/hooks/useModData';

// Profiles
import { profileService } from '@modules/profiles/services/profileService';
import { ProfileSwitcher } from '@modules/profiles/components/ProfileSwitcher';

// Migration
import { migrationService } from '@modules/migration/services/migrationService';
import { MigrationWizard } from '@modules/migration/components/MigrationWizard';

// Tools
import { cacheService } from '@modules/tools/services/cacheService';
import { ToolsView } from '@modules/tools/components/ToolsView';
```

---

## 🎯 Module Responsibilities

| Module | Backend Responsibilities | Frontend Responsibilities |
|--------|-------------------------|--------------------------|
| **Core** | File I/O, Process, Dialogs, Images | Layout, Navigation, IPC, Utilities |
| **Mods** | Database, Import, Archive, Query | Mod UI, Filters, Actions |
| **Profiles** | Profile CRUD, Switching | Profile Switcher, Manager |
| **Migration** | Python Migration Logic | Migration Wizard UI |
| **Tools** | Cache, Validation, 3DMigoto, Config | Tools UI, Utilities |
| **Settings** | - | Settings UI |
| **Warehouse** | - | Warehouse UI |
| **Plugins** | Plugin System | Plugin UI |

---

**Created**: 2026-02-17
**Status**: 📚 Reference Guide
**Use**: Quick lookup for file locations

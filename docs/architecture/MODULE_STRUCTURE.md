# Module Structure Reorganization

**Date**: 2026-02-17
**Purpose**: Introduce modular architecture for better code organization

---

## 🎯 Goals

1. Group related functionality into modules
2. Clear separation of concerns
3. Better discoverability for new developers/AI assistants
4. Follow domain-driven design principles
5. Maintain backward compatibility during transition

---

## 📂 Backend Structure (C#)

### Current Issues
- All services in flat `Services/` folder (30+ files)
- Models mixed together (mod models, profile models, migration models)
- No clear module boundaries

### New Structure

```
D3dxSkinManager/
├── Modules/
│   ├── Core/                    # Core infrastructure
│   │   ├── Models/
│   │   │   ├── MessageRequest.cs
│   │   │   └── MessageResponse.cs
│   │   ├── Services/
│   │   │   ├── IFileService.cs
│   │   │   ├── FileService.cs
│   │   │   ├── IFileSystemService.cs
│   │   │   ├── FileSystemService.cs
│   │   │   ├── IProcessService.cs
│   │   │   ├── ProcessService.cs
│   │   │   ├── IFileDialogService.cs
│   │   │   ├── FileDialogService.cs
│   │   │   ├── IImageService.cs
│   │   │   └── ImageService.cs
│   │   └── README.md
│   │
│   ├── Mods/                    # Mod management
│   │   ├── Models/
│   │   │   └── ModInfo.cs
│   │   ├── Services/
│   │   │   ├── IModRepository.cs
│   │   │   ├── ModRepository.cs
│   │   │   ├── IModArchiveService.cs
│   │   │   ├── ModArchiveService.cs
│   │   │   ├── IModImportService.cs
│   │   │   ├── ModImportService.cs
│   │   │   ├── IModQueryService.cs
│   │   │   └── ModQueryService.cs
│   │   └── README.md
│   │
│   ├── Profiles/                # Profile management
│   │   ├── Models/
│   │   │   ├── Profile.cs
│   │   │   ├── ProfileConfiguration.cs
│   │   │   ├── CreateProfileRequest.cs
│   │   │   ├── UpdateProfileRequest.cs
│   │   │   ├── ProfileSwitchResult.cs
│   │   │   └── ProfileListResponse.cs
│   │   ├── Services/
│   │   │   ├── IProfileService.cs
│   │   │   └── ProfileService.cs
│   │   └── README.md
│   │
│   ├── Migration/               # Python migration
│   │   ├── Models/
│   │   │   └── MigrationModels.cs
│   │   ├── Services/
│   │   │   ├── IMigrationService.cs
│   │   │   └── MigrationService.cs
│   │   └── README.md
│   │
│   ├── Tools/                   # Tools & utilities
│   │   ├── Models/
│   │   │   ├── CacheItem.cs
│   │   │   ├── CacheStatistics.cs
│   │   │   ├── ValidationResult.cs
│   │   │   ├── D3DMigotoVersion.cs
│   │   │   └── DeploymentResult.cs
│   │   ├── Services/
│   │   │   ├── ICacheService.cs
│   │   │   ├── CacheService.cs
│   │   │   ├── IClassificationService.cs
│   │   │   ├── ClassificationService.cs
│   │   │   ├── IStartupValidationService.cs
│   │   │   ├── StartupValidationService.cs
│   │   │   ├── I3DMigotoService.cs
│   │   │   ├── D3DMigotoService.cs
│   │   │   ├── IConfigurationService.cs
│   │   │   └── ConfigurationService.cs
│   │   └── README.md
│   │
│   └── Plugins/                 # Plugin system
│       ├── Models/
│       ├── Services/
│       │   ├── PluginLoader.cs
│       │   ├── PluginRegistry.cs
│       │   ├── PluginEventBus.cs
│       │   └── PluginContext.cs
│       └── README.md
│
├── Facades/
│   ├── IModFacade.cs
│   └── ModFacade.cs
│
├── Configuration/
│   └── ServiceCollectionExtensions.cs
│
└── Program.cs
```

### Namespace Changes

**Old**:
```csharp
D3dxSkinManager.Models
D3dxSkinManager.Services
D3dxSkinManager.Services.Migration
```

**New**:
```csharp
D3dxSkinManager.Modules.Core.Models
D3dxSkinManager.Modules.Core.Services
D3dxSkinManager.Modules.Mods.Models
D3dxSkinManager.Modules.Mods.Services
D3dxSkinManager.Modules.Profiles.Models
D3dxSkinManager.Modules.Profiles.Services
D3dxSkinManager.Modules.Migration.Models
D3dxSkinManager.Modules.Migration.Services
D3dxSkinManager.Modules.Tools.Models
D3dxSkinManager.Modules.Tools.Services
D3dxSkinManager.Modules.Plugins
```

---

## 📂 Frontend Structure (React/TypeScript)

### Current Issues
- Components scattered in many folders (10+ folders)
- Services in flat structure (15+ files)
- No clear feature boundaries

### New Structure

```
D3dxSkinManager.Client/src/
├── modules/
│   ├── core/                    # Core infrastructure
│   │   ├── components/
│   │   │   ├── layout/
│   │   │   │   ├── AppHeader.tsx
│   │   │   │   ├── AppSider.tsx
│   │   │   │   └── AppStatusBar.tsx
│   │   │   ├── common/
│   │   │   │   ├── TooltipSystem.tsx
│   │   │   │   └── LoadingSpinner.tsx
│   │   │   └── windows/
│   │   │       └── HelpWindow.tsx
│   │   ├── services/
│   │   │   ├── photino.ts
│   │   │   ├── fileDialogService.ts
│   │   │   └── fileSystemService.ts
│   │   ├── hooks/
│   │   │   └── useKeyboardShortcuts.ts
│   │   ├── utils/
│   │   │   └── KeyboardShortcutManager.ts
│   │   └── README.md
│   │
│   ├── mods/                    # Mod management
│   │   ├── components/
│   │   │   ├── ModHierarchicalView.tsx
│   │   │   ├── ModCard.tsx
│   │   │   ├── ModGrid.tsx
│   │   │   ├── ModListView.tsx
│   │   │   └── dialogs/
│   │   │       ├── ModEditDialog.tsx
│   │   │       ├── ModDetailDialog.tsx
│   │   │       └── TagSelectDialog.tsx
│   │   ├── services/
│   │   │   └── modService.ts
│   │   ├── hooks/
│   │   │   ├── useModData.ts
│   │   │   ├── useModFilters.ts
│   │   │   └── useModActions.ts
│   │   ├── types/
│   │   │   └── ModTypes.ts
│   │   └── README.md
│   │
│   ├── profiles/                # Profile management
│   │   ├── components/
│   │   │   ├── ProfileSwitcher.tsx
│   │   │   ├── ProfileManager.tsx
│   │   │   └── ProfileCard.tsx
│   │   ├── services/
│   │   │   └── profileService.ts
│   │   ├── types/
│   │   │   └── ProfileTypes.ts
│   │   └── README.md
│   │
│   ├── migration/               # Python migration
│   │   ├── components/
│   │   │   ├── MigrationWizard.tsx
│   │   │   └── MigrationProgress.tsx
│   │   ├── services/
│   │   │   └── migrationService.ts
│   │   ├── types/
│   │   │   └── MigrationTypes.ts
│   │   └── README.md
│   │
│   ├── tools/                   # Tools & utilities
│   │   ├── components/
│   │   │   ├── ToolsView.tsx
│   │   │   ├── CacheManager.tsx
│   │   │   ├── ValidationPanel.tsx
│   │   │   └── dialogs/
│   │   │       ├── KeyboardShortcutsDialog.tsx
│   │   │       ├── AboutDialog.tsx
│   │   │       └── UnityArgsDialog.tsx
│   │   ├── services/
│   │   │   ├── cacheService.ts
│   │   │   ├── validationService.ts
│   │   │   └── d3dMigotoService.ts
│   │   └── README.md
│   │
│   ├── settings/                # Settings
│   │   ├── components/
│   │   │   └── SettingsView.tsx
│   │   ├── services/
│   │   │   └── settingsService.ts
│   │   └── README.md
│   │
│   ├── warehouse/               # Warehouse/Download
│   │   ├── components/
│   │   │   └── WarehouseView.tsx
│   │   ├── services/
│   │   │   └── warehouseService.ts
│   │   └── README.md
│   │
│   └── plugins/                 # Plugin system
│       ├── components/
│       │   └── PluginsView.tsx
│       ├── services/
│       │   └── pluginService.ts
│       ├── examples/
│       └── README.md
│
├── App.tsx
├── App.css
└── index.tsx
```

### Import Path Changes

**Old**:
```typescript
import { modService } from '../../services/modService';
import { ModCard } from '../mods/ModCard';
import { ProfileSwitcher } from '../profile/ProfileSwitcher';
```

**New**:
```typescript
import { modService } from '@modules/mods/services/modService';
import { ModCard } from '@modules/mods/components/ModCard';
import { ProfileSwitcher } from '@modules/profiles/components/ProfileSwitcher';
```

---

## 🔄 Migration Strategy

### Phase 1: Backend (C#)
1. Create `Modules/` folder structure
2. Move files to appropriate module folders
3. Update namespaces in all moved files
4. Update `using` statements in all files
5. Update `ServiceCollectionExtensions.cs`
6. Build and verify

### Phase 2: Frontend (React/TypeScript)
1. Create `modules/` folder structure
2. Configure TypeScript path aliases in `tsconfig.json`
3. Move files to appropriate module folders
4. Update import paths (can use find/replace)
5. Update `App.tsx` imports
6. Build and verify

### Phase 3: Documentation
1. Update `AI_GUIDE.md` with new structure
2. Update `KEYWORDS_INDEX.md`
3. Create README.md in each module folder
4. Update architecture diagrams

---

## 📝 Module README Template

Each module should have a README.md explaining:

```markdown
# {Module Name} Module

## Purpose
Brief description of module responsibility

## Components/Services
- Service1: Description
- Service2: Description

## Key Models
- Model1: Description
- Model2: Description

## Dependencies
- Module dependencies
- External dependencies

## Usage Examples
```csharp / ```typescript
// Code examples
```

## Related Modules
- Links to related modules
```

---

## ✅ Benefits

1. **Better Organization**: Clear module boundaries
2. **Improved Discoverability**: Easy to find related code
3. **Reduced Cognitive Load**: Smaller, focused folders
4. **Easier Testing**: Test per module
5. **Better Documentation**: README per module
6. **Scalability**: Easy to add new modules
7. **Team Collaboration**: Clear ownership boundaries

---

## 🚧 Backward Compatibility

During transition:
- Keep old folder structure temporarily with symlinks (if needed)
- Use namespace aliases to maintain compatibility
- Update gradually, module by module

---

## 📊 Impact Analysis

### Files to Move
- **Backend**: ~60 files (Services + Models)
- **Frontend**: ~40 files (Components + Services)

### Files to Update
- **Backend**: ~20 files (using statements)
- **Frontend**: ~30 files (import paths)

### Estimated Time
- Backend restructure: 2-3 hours
- Frontend restructure: 2-3 hours
- Testing & verification: 1-2 hours
- Documentation: 1 hour

**Total**: 6-9 hours

---

**Status**: 📋 Planning Complete - Ready for Implementation

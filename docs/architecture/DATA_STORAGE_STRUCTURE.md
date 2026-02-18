# Data Storage Structure

This document describes how D3dxSkinManager stores all application data, configuration, and user files.

## Table of Contents
- [Overview](#overview)
- [Directory Structure](#directory-structure)
- [Profile-Based Storage](#profile-based-storage)
- [Global Application Data](#global-application-data)
- [File Types and Purposes](#file-types-and-purposes)
- [Storage Locations by Module](#storage-locations-by-module)

---

## Overview

D3dxSkinManager uses a **profile-based storage system** where:
- **Base Data Path**: `{ExecutableDirectory}/data/`
- **Multiple Profiles**: Each profile has its own isolated data directory
- **Default Profile**: Uses base data path directly for backward compatibility
- **Custom Profiles**: Stored in `{BaseDataPath}/profiles/{ProfileId}/`

### Storage Philosophy
1. **Profile Isolation**: Each profile has independent mods, database, cache, and configuration
2. **Centralized Global Data**: Shared resources (3DMigoto versions, plugins) stored once
3. **Portable**: All data stored relative to executable for easy backup/migration

---

## Directory Structure

### Complete Structure Tree

```
{ExecutableDirectory}/
├── D3dxSkinManager.exe           # Main executable
├── wwwroot/                       # Production build of React frontend
│   ├── index.html
│   ├── static/
│   │   ├── js/
│   │   └── css/
│   └── assets/
│
└── data/                          # Base data directory (ALL application data)
    │
    ├── mods.db                    # Default profile mod database (SQLite)
    ├── profiles.json              # Profile registry/list
    ├── active_profile.txt         # Currently active profile ID
    │
    ├── mods/                      # Default profile: Original mod archives
    │   ├── {sha256_hash}.zip      # Original imported mod files
    │   └── ...
    │
    ├── work_mods/                 # Default profile: Extracted/working mod files
    │   ├── {sha256_hash}/         # Extracted mod folder (loaded into game)
    │   │   ├── DISABLED_{hash}/   # Disabled mod (renamed with prefix)
    │   │   └── ...
    │   └── ...
    │
    ├── thumbnails/                # Default profile: Mod thumbnails (200x400)
    │   ├── {sha256_hash}.png
    │   └── ...
    │
    ├── previews/                  # Default profile: Preview images (450x900)
    │   ├── {sha256_hash}.png
    │   └── ...
    │
    ├── preview_screen/            # Default profile: Full-size preview cache
    │   ├── {sha256_hash}.png
    │   └── ...
    │
    ├── cache/                     # Default profile: Temporary cache
    │   ├── temp_extracts/         # Temporary extraction folder
    │   └── ...
    │
    ├── 3dmigoto_versions/         # GLOBAL: 3DMigoto version storage
    │   ├── 3dmigoto/              # Stable version files
    │   │   ├── d3dx.dll
    │   │   ├── d3dxLoader.exe
    │   │   └── ...
    │   ├── 3dmigoto-dev/          # Development version
    │   └── custom/                # Custom versions
    │
    ├── plugins/                   # GLOBAL: Plugin DLLs and data
    │   ├── PluginName.dll         # Plugin assemblies
    │   ├── {pluginId}/            # Plugin-specific data directory
    │   │   ├── config.json        # Plugin configuration
    │   │   └── data/              # Plugin data files
    │   └── ...
    │
    └── profiles/                  # Profile storage directory
        │
        ├── {ProfileId-1}/         # Individual profile data directory
        │   ├── mods.db            # Profile-specific mod database
        │   ├── profile_config.json # Profile configuration
        │   ├── mods/              # Profile mod archives
        │   ├── work_mods/         # Profile working mods
        │   ├── thumbnails/        # Profile thumbnails
        │   ├── previews/          # Profile previews
        │   ├── preview_screen/    # Profile full-size cache
        │   └── cache/             # Profile cache
        │
        ├── {ProfileId-2}/         # Another profile
        │   └── ...
        │
        └── ...
```

---

## Profile-Based Storage

### Default Profile

The **Default Profile** uses the base data directory directly:

| Data Type | Path |
|-----------|------|
| Database | `{BaseDataPath}/mods.db` |
| Mod Archives | `{BaseDataPath}/mods/` |
| Working Mods | `{BaseDataPath}/work_mods/` |
| Thumbnails | `{BaseDataPath}/thumbnails/` |
| Previews | `{BaseDataPath}/previews/` |
| Preview Screen | `{BaseDataPath}/preview_screen/` |
| Cache | `{BaseDataPath}/cache/` |

**Location:** [Profile.cs:87](../D3dxSkinManager/Modules/Profiles/Models/Profile.cs#L87)

### Custom Profiles

Each custom profile has an **isolated data directory**:

```
Path: {BaseDataPath}/profiles/{ProfileId}/
```

Where `{ProfileId}` is a unique GUID generated when the profile is created.

**Profile Structure:**
```
profiles/a1b2c3d4-e5f6.../
├── mods.db                 # Profile-specific database
├── profile_config.json     # Profile configuration
├── mods/                   # Profile mod archives
├── work_mods/              # Profile extracted mods
├── thumbnails/             # Profile thumbnails
├── previews/               # Profile preview images
├── preview_screen/         # Profile full-size cache
└── cache/                  # Profile temporary cache
```

**Location:** [ProfileService.cs:137](../D3dxSkinManager/Modules/Profiles/Services/ProfileService.cs#L137)

### Profile Registry

**profiles.json** - Stores list of all profiles:
```json
[
  {
    "id": "default-profile-id",
    "name": "Default",
    "description": "Default profile",
    "workDirectory": "{BaseDataPath}/work_mods",
    "dataDirectory": "{BaseDataPath}",
    "isActive": true,
    "createdAt": "2024-01-01T00:00:00Z",
    "colorTag": "#1890ff",
    "iconName": "home",
    "gameName": "My Game"
  }
]
```

**active_profile.txt** - Stores ID of currently active profile

**Location:** [ProfileService.cs:27-36](../D3dxSkinManager/Modules/Profiles/Services/ProfileService.cs#L27-36)

---

## Global Application Data

These directories are **shared across all profiles**:

### 3DMigoto Versions
**Path:** `{BaseDataPath}/3dmigoto_versions/`

Stores different versions of 3DMigoto:
- `3dmigoto/` - Stable release
- `3dmigoto-dev/` - Development version
- `custom/` - User-provided custom versions

**Location:** [D3DMigotoService.cs:36](../D3dxSkinManager/Modules/D3DMigoto/Services/D3DMigotoService.cs#L36)

### Plugins
**Path:** `{BaseDataPath}/plugins/`

Plugin storage structure:
```
plugins/
├── MyPlugin.dll              # Plugin assembly
├── AnotherPlugin.dll
└── {pluginId}/               # Plugin data directory
    ├── config.json
    ├── cache/
    └── logs/
```

**Plugin Data Access:** Each plugin can request its data directory via `IPluginContext.GetPluginDataPath(pluginId)`

**Location:** [PluginContext.cs:50](../D3dxSkinManager/Plugins/PluginContext.cs#L50)

---

## File Types and Purposes

### Database Files (SQLite)

**mods.db** - SQLite database storing mod metadata

**Schema:**
```sql
CREATE TABLE Mods (
    SHA TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    Author TEXT,
    ObjectName TEXT,
    Description TEXT,
    Tags TEXT,  -- JSON array
    Grading TEXT DEFAULT 'G',
    OriginalPath TEXT,
    WorkPath TEXT,
    CachePath TEXT,
    PreviewPath TEXT,
    ThumbnailPath TEXT,
    ImportedDate TEXT,
    LastUsedDate TEXT,
    Size INTEGER,
    FileType TEXT,
    Metadata TEXT  -- JSON for extensibility
)
```

**Location:** [ModRepository.cs:41](../D3dxSkinManager/Modules/Mods/Services/ModRepository.cs#L41)

### JSON Configuration Files

| File | Purpose | Example |
|------|---------|---------|
| `profiles.json` | Profile registry | List of all profiles |
| `profile_config.json` | Profile settings | Archive mode, grading, 3DMigoto version |
| `active_profile.txt` | Active profile ID | Simple text file with profile ID |

### Image Files

| Directory | Size | Purpose |
|-----------|------|---------|
| `thumbnails/` | 200x400 | Small previews for mod list |
| `previews/` | 450x900 | Medium-size preview images |
| `preview_screen/` | Original | Full-size cached preview images |

**Supported Formats:** PNG, JPG, JPEG, BMP, GIF

**Location:** [ImageService.cs:32-56](../D3dxSkinManager/Modules/Core/Services/ImageService.cs#L32-56)

### Mod Files

| Directory | Purpose | Notes |
|-----------|---------|-------|
| `mods/` | Original archives | Read-only, kept as backup |
| `work_mods/` | Extracted files | Actively loaded/modified |
| `work_mods/DISABLED_*/` | Disabled mods | Prefixed with `DISABLED_` |

**Mod Archive Structure:**
```
mods/
└── a1b2c3d4e5f6...{sha256}.zip    # Original imported file

work_mods/
├── a1b2c3d4e5f6...{sha256}/       # Active mod (loaded)
│   ├── ShaderFixes/
│   │   ├── *.ini
│   │   └── *.txt
│   ├── *.dds
│   ├── *.buf
│   └── preview.png
│
└── DISABLED_a1b2...{sha256}/      # Disabled mod
    └── ...
```

**Location:** [ModArchiveService.cs:21-27](../D3dxSkinManager/Modules/Mods/Services/ModArchiveService.cs#L21-27)

---

## Storage Locations by Module

### Mods Module
- **Archives:** `{ProfileDataPath}/mods/`
- **Working Directory:** `{ProfileDataPath}/work_mods/`
- **Database:** `{ProfileDataPath}/mods.db`
- **Services:** ModRepository, ModArchiveService, ModImportService

### Profiles Module
- **Profile List:** `{BaseDataPath}/profiles.json`
- **Active Profile:** `{BaseDataPath}/active_profile.txt`
- **Profile Data:** `{BaseDataPath}/profiles/{ProfileId}/`
- **Service:** ProfileService

### D3DMigoto Module
- **Versions:** `{BaseDataPath}/3dmigoto_versions/`
- **Deployment Target:** Profile's `WorkDirectory` (user-specified)
- **Service:** D3DMigotoService

### Tools Module (Cache/Config/Validation)
- **Cache Directory:** `{ProfileDataPath}/cache/`
- **Temp Extracts:** `{ProfileDataPath}/cache/temp_extracts/`
- **Services:** CacheService, ConfigurationService, ValidationService

### Image Service (Core)
- **Thumbnails:** `{ProfileDataPath}/thumbnails/`
- **Previews:** `{ProfileDataPath}/previews/`
- **Preview Screen:** `{ProfileDataPath}/preview_screen/`
- **Service:** ImageService

### Plugins Module
- **Plugin DLLs:** `{BaseDataPath}/plugins/*.dll`
- **Plugin Data:** `{BaseDataPath}/plugins/{pluginId}/`
- **Services:** PluginLoader, PluginContext

### Migration Module
- **No Persistent Storage** (scans external Python installation)
- **Service:** MigrationService

---

## Key Implementation Details

### Path Resolution

**Base Data Path:**
```csharp
var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
```
**Location:** [Program.cs:135](../D3dxSkinManager/Program.cs#L135)

**Profile Data Path:**
```csharp
// Default profile
DataDirectory = baseDataPath;

// Custom profile
DataDirectory = Path.Combine(profilesDirectory, Guid.NewGuid().ToString());
```
**Location:** [ProfileService.cs:87, 137](../D3dxSkinManager/Modules/Profiles/Services/ProfileService.cs#L87,137)

### Directory Creation

All directories are created automatically on service initialization:
```csharp
Directory.CreateDirectory(dataPath);
Directory.CreateDirectory(modsDirectory);
Directory.CreateDirectory(thumbnailsPath);
// etc.
```

### Thread Safety

- **SQLite Database:** Uses connection pooling, thread-safe
- **File Operations:** Atomic operations with proper locking
- **Profile Switching:** Serialized through ProfileService

---

## Best Practices

### For Developers

1. **Use Profile Data Path:** Always use `Profile.DataDirectory` for profile-specific data
2. **Use Base Data Path:** Use injected `dataPath` for global/shared resources
3. **Create Directories:** Always ensure directories exist before writing files
4. **Use SHA256 Hashes:** Use mod SHA256 as unique identifier for file names
5. **Handle Profile Switches:** Services should reload data when active profile changes

### For Users

1. **Backup:** Copy entire `data/` folder to backup all profiles and settings
2. **Portable Installation:** Move entire application folder (including `data/`)
3. **Profile Isolation:** Each profile is completely independent
4. **Disk Space:** Monitor `work_mods/` as it contains extracted mod files

---

## Migration Notes

### From Python Version

The .NET version maintains compatibility with Python data structure:
- Database schema is identical
- Directory names match Python version
- Default profile uses same paths

**Migration Process:**
1. Python installation detected via MigrationService
2. Mods imported from Python database
3. Files copied to new profile structure
4. Configuration migrated to JSON format

**Location:** [MigrationService.cs](../D3dxSkinManager/Modules/Migration/Services/MigrationService.cs)

---

## Troubleshooting

### Common Issues

**Issue:** "Database locked" error
- **Cause:** Another process accessing database
- **Solution:** Ensure only one instance of application is running

**Issue:** Mods not loading
- **Cause:** Work directory not set correctly
- **Solution:** Check Profile > WorkDirectory points to game folder

**Issue:** Missing thumbnails
- **Cause:** Image cache not generated
- **Solution:** Re-import mods or clear cache and restart

**Issue:** Profile data not found
- **Cause:** Profile directory deleted or corrupted
- **Solution:** Check `{BaseDataPath}/profiles/{ProfileId}/` exists

---

## See Also

- [Module Structure](MODULE_STRUCTURE.md) - How modules are organized
- [Current Architecture](CURRENT_ARCHITECTURE.md) - Complete system architecture
- [Profile System](../features/PROFILE_SYSTEM.md) - Profile management features

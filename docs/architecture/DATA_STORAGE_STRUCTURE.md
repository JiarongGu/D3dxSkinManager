# Data Storage Structure

This document describes the complete directory structure and file organization for d3dx Skin Manager.

## Overview

The application uses a profile-based data isolation architecture where each profile has its own independent data directory containing mods, configuration, and runtime files.

## Directory Structure

```
{AppDataPath}/
└── profiles/
    ├── profiles.json              # Profile registry and active profile tracking
    └── {ProfileId}/               # Individual profile directory
        ├── mods/                  # Mod archive storage
        │   ├── {SHA}.7z          # Compressed mod archives (primary format)
        │   └── {SHA}.zip         # Alternative archive format
        │
        ├── thumbnails/            # Mod thumbnail images
        │   └── {SHA}.png         # Generated or imported thumbnails
        │
        ├── previews/              # Mod preview images (per-mod folders)
        │   └── {SHA}/            # Preview folder for each mod
        │       ├── preview1.png  # First preview image
        │       ├── preview2.png  # Additional preview images
        │       └── ...
        │
        ├── work/                  # Work directory (can be external via WorkDirectory config)
        │   └── Mods/             # Runtime mod deployment folder
        │       ├── {SHA}/        # Active (loaded) mod files
        │       │   ├── mod.ini
        │       │   ├── textures/
        │       │   └── ...
        │       └── DISABLED-{SHA}/  # Disabled mod cache (for fast re-enable)
        │           ├── mod.ini
        │           └── ...
        │
        ├── logs/                  # Profile-specific log files
        │   ├── app.log           # Application logs
        │   ├── migration.log     # Migration operation logs
        │   └── error.log         # Error logs
        │
        ├── mods.db               # SQLite database (mod metadata, classifications)
        ├── config.json           # Profile configuration (game path, launch args, etc.)
        └── auto_detection_rules.json  # Classification auto-detection rules
```

## Directory Purposes

### Root Level (`{AppDataPath}/profiles/`)

| File/Folder | Purpose | Managed By |
|------------|---------|------------|
| `profiles.json` | Profile registry containing all profiles and tracking active profile | ProfileService |
| `{ProfileId}/` | Individual profile data directory | ProfileService |

### Profile Level (`{ProfileId}/`)

| Folder | Purpose | Contents | Service Responsible |
|--------|---------|----------|-------------------|
| `mods/` | **Mod archive storage** | Compressed mod archives (.7z, .zip) identified by SHA hash | ModFileService |
| `thumbnails/` | **Mod thumbnails** | PNG images (one per mod) for UI display | ImageService |
| `previews/` | **Mod preview folders** | Per-mod folders containing multiple preview images | ImageService |
| `work/` | **Work directory base** | Base directory for mod deployment (configurable) | Profile.WorkDirectory |
| `work/Mods/` | **Runtime mod folder** | Active and disabled mod directories | ModFileService |
| `logs/` | **Log files** | Profile-specific application, migration, and error logs | (Future) LogService |
| `mods.db` | **Mod database** | SQLite database storing mod metadata, tags, classifications | ModRepository |
| `config.json` | **Profile config** | Game path, launch arguments, 3DMigoto settings | ConfigurationService |
| `auto_detection_rules.json` | **Detection rules** | Rules for automatic mod classification | ModAutoDetectionService |

## Mod Lifecycle

### 1. Import
```
Source Archive → SHA Calculation → Copy to mods/{SHA}.7z
                                 ↓
                    Generate thumbnails & previews
                                 ↓
                    Extract temporarily for metadata
                                 ↓
                    Save to database (mods.db)
```

### 2. Load (Enable)
```
mods/{SHA}.7z → Extract → work/Mods/{SHA}/
                          (Active mod files)
```

### 3. Unload (Disable)
```
work/Mods/{SHA}/ → Rename → work/Mods/DISABLED-{SHA}/
                            (Cached for fast re-enable)
```

### 4. Re-enable
```
work/Mods/DISABLED-{SHA}/ → Rename → work/Mods/{SHA}/
                                     (Instant, no extraction)
```

### 5. Delete
```
Delete: mods/{SHA}.7z
        work/Mods/{SHA}/
        work/Mods/DISABLED-{SHA}/
        thumbnails/{SHA}.png
        previews/{SHA}/
        Database record
```

## Work Directory Configuration

The `work/` directory can be configured to point to different locations:

### Internal (Default)
```
WorkDirectory: {DataDirectory}/work/
Use Case: Testing, isolated profiles
```

### External (Game Directory)
```
WorkDirectory: C:/Games/MyGame/
Use Case: Direct mod deployment to game folder
Mods deployed to: C:/Games/MyGame/Mods/{SHA}/
```

### External (Custom)
```
WorkDirectory: D:/ModCache/
Use Case: Separate SSD for faster loading
```

## Cache Management

Disabled mods are cached as `DISABLED-{SHA}` directories to enable instant re-activation without re-extraction.

### Cache Categories
- **Invalid**: SHA not in database (orphaned cache)
- **Rarely Used**: Mod exists but not currently loaded
- **Frequently Used**: Should never be in cache (loaded mods)

### Cache Operations
- **Scan**: List all disabled mod caches with sizes and categories
- **Clean by Category**: Delete all caches in specific category
- **Delete Specific**: Remove cache for specific mod SHA

## Database Schema

### mods.db (SQLite)
- `Mods` table: SHA, Name, Author, Version, Category, Tags, IsLoaded, etc.
- `Classifications` table: Classification hierarchy and mod assignments
- Future: Tags, Favorites, Custom metadata tables

## Path Resolution

### Relative vs Absolute
- **Stored in DB/Config**: Relative paths (for portability)
- **Runtime Operations**: Converted to absolute paths via PathHelper

### Example
```
Stored:    profiles/default/work/
Resolved:  C:/Users/Username/AppData/Local/D3dxSkinManager/profiles/default/work/
```

## Migration

When migrating from old structure or importing from other tools:
1. Copy mod archives to `mods/`
2. Copy previews to `previews/{SHA}/`
3. Rebuild database from archives
4. Generate missing thumbnails

## Best Practices

### For Developers
1. Always use ModFileService for mod file operations
2. Never hardcode paths - use ProfileContext.GetProfileDataPath()
3. Store relative paths in database/config
4. Resolve to absolute paths at runtime using PathHelper

### For Services
- **FileService**: Low-level file operations (SHA, extract, compress)
- **ModFileService**: High-level mod orchestration (import, load, unload, cache)
- **ProfileService**: Profile management and directory initialization
- **ConfigurationService**: Profile-specific settings (config.json)

## See Also
- [Service Architecture](SERVICE_ARCHITECTURE.md)
- [Profile System](../features/PROFILE_SYSTEM.md)
- [Path Conventions](PATH_CONVENTIONS.md)

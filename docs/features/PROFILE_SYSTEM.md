# Profile System

**Last Updated:** 2026-03-05
**Status:** Implemented

## Overview

Multi-profile mod management system allowing independent configurations per game/setup.

### Features
- Independent work directories (3DMigoto location)
- Separate data directories (mods, cache, database)
- Profile-specific settings
- Visual identification (colors, icons, game names)

### Use Cases
- Multiple games (Genshin, Endfield, etc.)
- Test vs Production environments
- Character-specific mod collections
- Migration from Python version

## Architecture

### Storage Structure
```
data/
├── profiles.json            # Profile metadata
└── profiles/{profile-id}/
    ├── mods/                # Mod archives
    ├── mods.db              # SQLite database
    ├── cache/               # Cache files
    ├── thumbnails/          # Thumbnails
    ├── previews/            # Previews
    └── config.json          # Profile config
```

### Profile Model
```csharp
public class Profile {
    public string Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public string WorkDirectory { get; set; }
    public string DataDirectory { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public string? ColorTag { get; set; }
    public string? IconName { get; set; }
    public string? GameName { get; set; }
    public int ModCount { get; set; }
}
```

## Backend Implementation

### ProfileService Operations

| Operation | Method | Description |
|-----------|--------|-------------|
| Create | `CreateProfileAsync(request)` | Create new profile with directories |
| Switch | `SwitchProfileAsync(profileId)` | Activate different profile |
| Update | `UpdateProfileAsync(id, request)` | Update metadata |
| Delete | `DeleteProfileAsync(id)` | Remove profile (if not active) |
| Duplicate | `DuplicateProfileAsync(sourceId, name)` | Copy entire profile |
| List | `GetAllProfilesAsync()` | Get all profiles |

### Profile Configuration
```csharp
/// <summary>
/// Profile configuration settings stored in {profileId}/config.json
/// </summary>
public class ProfileConfiguration {
    public string ProfileId { get; set; }
    public string MigotoVersion { get; set; }  // "3dmigoto", "3dmigoto-dev", "custom"
    public WorkDirectoryConfiguration Work { get; set; }
    public Dictionary<string, WindowConfiguration> Windows { get; set; }  // Window positions/sizes
}

/// <summary>
/// Work directory configuration (parent of Mods folder)
/// </summary>
public class WorkDirectoryConfiguration {
    public string Mode { get; set; }  // "internal" or "external"
    public string? Directory { get; set; }  // Custom path when Mode is "external"
    public string? InternalWorkDirectory { get; set; }  // Computed by backend (not persisted)
}

/// <summary>
/// Window position and size configuration for secondary windows
/// </summary>
public class WindowConfiguration {
    public int? X { get; set; }
    public int? Y { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
}
```

**Work Directory Modes:**
- **Internal Mode**: Uses `{profile folder}/work` as work directory (default)
  - Mods extracted to: `{profile folder}/work/Mods/{ID}/`
- **External Mode**: Uses custom directory path specified by user
  - Mods extracted to: `{custom path}/Mods/{ID}/`
  - Useful for: game directories, separate SSDs, shared locations

**Windows Configuration:**
- **Generic System**: Supports multiple window types (capture, debug, tools, etc.)
- **Per-Window Storage**: Each window saves position (X, Y) and size (Width, Height)
- **Thread-Safe**: Managed via `ConcurrentDictionary` in SecondaryWindowService
- **Profile-Scoped**: Each profile remembers its own window positions
- **Examples**:
  - `"capture"` - Screen capture control panel window
  - `"debug"` - Debug console window (future)
  - `"tools"` - Tool windows (future)

**Implementation Details:**
- `ProfilePathService.WorkDirectory` property is dynamic and cached, reading from configuration
- Both `WorkDirectory` and `CacheModsDirectory` are resolved asynchronously via `LoadCacheDirectoryAsync()`
- Configuration changes trigger cache invalidation and reload via event subscription
- `InternalWorkDirectory` is computed by backend for UI display only (not saved to config.json)
- Window configurations managed by `ProfileService.UpdateWindowConfigurationAsync()`
- All config updates preserve existing fields (Work, MigotoVersion, Windows)

## Frontend Implementation

### ProfileContext
```typescript
interface ProfileContextValue {
  selectedProfile: Profile | undefined;
  selectedProfileId: string | undefined;
  profiles: Profile[];
  loading: boolean;

  actions: {
    selectProfile: (id: string) => Promise<void>;
    createProfile: (name: string, desc?: string) => Promise<Profile>;
    updateProfile: (id: string, name: string, desc?: string) => Promise<void>;
    deleteProfile: (id: string) => Promise<void>;
  };
}
```

### Components

| Component | Purpose | Location |
|-----------|---------|----------|
| `ProfileSwitcher` | Dropdown for quick switching | AppHeader |
| `ProfileManager` | Full management dialog | Settings |
| `ProfileCard` | Profile display card | ProfileManager |
| `ProfileConfigDialog` | Edit profile settings | ProfileManager |

## IPC Messages

### Message Types
```typescript
// Frontend → Backend
{ module: 'PROFILE', type: 'GET_ALL' }
{ module: 'PROFILE', type: 'CREATE', payload: { name, description } }
{ module: 'PROFILE', type: 'SWITCH', payload: { profileId } }
{ module: 'PROFILE', type: 'DELETE', payload: { profileId } }
{ module: 'PROFILE', type: 'DUPLICATE', payload: { sourceId, name } }

// Backend → Frontend
{ success: true, data: Profile[] }  // List
{ success: true, data: Profile }     // Single
{ success: true, data: { profileId, modCount } }  // Switch result
```

## Profile Switching Flow

```
User selects profile → ProfileSwitcher
    ↓
profileService.switchProfile(id)
    ↓
IPC: { module: 'PROFILE', type: 'SWITCH', payload: { profileId } }
    ↓
Backend: Deactivate current → Activate new → Update timestamps
    ↓
Response: { success: true, data: switchResult }
    ↓
ProfileContext updates → UI refreshes
    ↓
ModsContext reloads for new profile
```

## Migration Integration

The migration wizard can create profiles:

```typescript
// Migration creates profile with specific work directory
const profile = await profileService.createProfile({
  name: 'Migrated Profile',
  description: 'From Python d3dxSkinManage',
  workDirectory: 'C:\\Games\\GenshinImpact',
  copyFromActive: false
});
```

## Profile-Scoped Services

Services operate within profile context:

```typescript
// All mod operations use active profile
await modService.getAllMods(selectedProfileId);
await modService.loadMod(selectedProfileId, id);

// Profile switch triggers reload
useEffect(() => {
  if (selectedProfileId) {
    loadModsForProfile(selectedProfileId);
  }
}, [selectedProfileId]);
```

## Best Practices

1. **Always check active profile** before operations
2. **Prevent deletion** of active profile
3. **Create default profile** on first run
4. **Auto-save** profile configuration changes
5. **Validate paths** when creating profiles
6. **Handle profile switch** in all contexts

## Related Documentation

- [FRONTEND_CONTEXT_ARCHITECTURE.md](../architecture/FRONTEND_CONTEXT_ARCHITECTURE.md) - ProfileContext details
- [MODULE_ARCHITECTURE.md](../architecture/MODULE_ARCHITECTURE.md) - Profile module structure
- [MIGRATION_TOOL.md](MIGRATION_TOOL.md) - Profile creation via migration
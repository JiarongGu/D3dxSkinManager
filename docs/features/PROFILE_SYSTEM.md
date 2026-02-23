# Profile System

**Last Updated:** 2026-02-23
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
public class ProfileConfiguration {
    public string ProfileId { get; set; }
    public string WorkDirectory { get; set; }
    public ModLoadBehavior LoadBehavior { get; set; }
    public bool EnableAutoDetection { get; set; }
    public bool ShowNotifications { get; set; }
    public List<string> IgnoredPaths { get; set; }
}
```

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
await modService.loadMod(selectedProfileId, sha);

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
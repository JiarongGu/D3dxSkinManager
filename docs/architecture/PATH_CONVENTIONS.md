# Path Conventions and Portability

**Last Updated:** 2026-02-18
**Version:** 1.0

---

## Table of Contents

1. [Overview](#overview)
2. [The Problem](#the-problem)
3. [The Solution: Relative Paths](#the-solution-relative-paths)
4. [PathHelper Service](#pathhelper-service)
5. [When to Use Relative vs Absolute Paths](#when-to-use-relative-vs-absolute-paths)
6. [Implementation Guidelines](#implementation-guidelines)
7. [Common Patterns](#common-patterns)
8. [Examples](#examples)
9. [Testing Path Portability](#testing-path-portability)

---

## Overview

D3dxSkinManager is designed to be **portable** - the entire application folder can be moved or renamed without breaking. This is achieved by storing all internal paths as **relative paths** instead of absolute paths.

### Key Principle
> **All paths stored in databases and configuration files MUST be relative to the `data/` folder**

---

## The Problem

### Before (Broken Portability)

When paths are stored as absolute paths:

```
Database:
  ThumbnailPath: "D:\MyApps\D3dxSkinManager\data\thumbnails\abc123.png"

Configuration:
  DataDirectory: "D:\MyApps\D3dxSkinManager\data\profiles\default"
```

**What happens when you:**
- Move the folder: `D:\MyApps\` → `E:\Games\`
- Rename the folder: `D3dxSkinManager` → `ModManager`
- Share with another user who has different drive letters

**Result:** ❌ All paths break, images don't load, profiles don't work

---

## The Solution: Relative Paths

Store paths relative to the `data/` folder:

```
Database:
  ThumbnailPath: "thumbnails/abc123.png"

Configuration:
  DataDirectory: "profiles/default"
```

**What happens when you move/rename:**
- Folder location changes → ✅ Still works
- Different drive letter → ✅ Still works
- Shared with another user → ✅ Still works

**Result:** ✅ Application remains portable

---

## PathHelper Service

The `PathHelper` service handles conversion between absolute and relative paths.

### Location
[D3dxSkinManager/Modules/Core/Services/PathHelper.cs](../../D3dxSkinManager/Modules/Core/Services/PathHelper.cs)

### Key Methods

#### `ToRelativePath(string? absolutePath)`
Converts an absolute path to relative path (relative to data folder)

```csharp
var helper = new PathHelper("D:\\App\\data");

// Path under data folder → converts to relative
var result = helper.ToRelativePath("D:\\App\\data\\thumbnails\\abc.png");
// Returns: "thumbnails/abc.png"

// Path outside data folder → keeps absolute (e.g., game directory)
var result = helper.ToRelativePath("C:\\Games\\MyGame\\");
// Returns: "C:\\Games\\MyGame\\"
```

#### `ToAbsolutePath(string? relativePath)`
Converts a relative path to absolute path

```csharp
var helper = new PathHelper("D:\\App\\data");

// Relative path → converts to absolute
var result = helper.ToAbsolutePath("thumbnails/abc.png");
// Returns: "D:\\App\\data\\thumbnails\\abc.png"

// Already absolute → returns as-is
var result = helper.ToAbsolutePath("C:\\Games\\MyGame\\");
// Returns: "C:\\Games\\MyGame\\"
```

### Dependency Injection

PathHelper is registered as a singleton in the DI container:

```csharp
// CoreServiceExtensions.cs
services.AddSingleton(sp => new PathHelper(dataPath));
```

**Always inject PathHelper through constructor:**

```csharp
// ✅ CORRECT
public class MyService
{
    private readonly PathHelper _pathHelper;

    public MyService(PathHelper pathHelper)
    {
        _pathHelper = pathHelper ?? throw new ArgumentNullException(nameof(pathHelper));
    }
}

// ❌ WRONG - Never create manually
public class MyService
{
    private readonly PathHelper _pathHelper = new PathHelper("some/path");
}
```

---

## When to Use Relative vs Absolute Paths

### ALWAYS Use Relative Paths

Paths that are **stored** in database or configuration files:

- ✅ Thumbnail paths (`data/thumbnails/`)
- ✅ Preview paths (`data/previews/`)
- ✅ Mod archive paths (`data/mods/`)
- ✅ Profile data directories (`data/profiles/{id}/`)
- ✅ Cache directories (`data/cache/`)
- ✅ Plugin data directories (`data/plugins/{id}/`)

**Rule:** If the path is within the `data/` folder, store it as relative.

### Use Absolute Paths (When External)

Paths that point **outside** the data folder:

- ✅ Game install directory (e.g., `C:\Games\MyGame\`)
- ✅ User-selected work directory (e.g., `E:\ModWorkspace\`)
- ✅ External mod sources

**Rule:** External paths remain absolute because they're outside our control.

### Use Absolute Paths (During File Operations)

When actually accessing files, convert to absolute:

```csharp
// ✅ CORRECT - Convert to absolute before file operations
var relativePath = mod.ThumbnailPath; // "thumbnails/abc.png"
var absolutePath = _pathHelper.ToAbsolutePath(relativePath);
if (File.Exists(absolutePath))
{
    var bytes = await File.ReadAllBytesAsync(absolutePath);
}

// ❌ WRONG - Using relative path directly for file operations
if (File.Exists(mod.ThumbnailPath)) // Will fail!
{
    // ...
}
```

---

## Implementation Guidelines

### 1. Storing Paths in Database

```csharp
// When saving to database
public async Task<ModInfo> CreateModAsync(ModInfo mod)
{
    // Generate thumbnail (returns absolute path)
    var absoluteThumbnailPath = await _imageService.GenerateThumbnailAsync(modDir, sha);

    // Convert to relative before saving
    mod.ThumbnailPath = _pathHelper.ToRelativePath(absoluteThumbnailPath);

    await _repository.InsertAsync(mod);
    return mod;
}
```

### 2. Loading Paths from Database

```csharp
// When loading from database
public async Task<byte[]?> GetThumbnailAsync(string sha)
{
    var mod = await _repository.GetByIdAsync(sha);
    if (mod?.ThumbnailPath == null)
        return null;

    // Convert to absolute before file operations
    var absolutePath = _pathHelper.ToAbsolutePath(mod.ThumbnailPath);
    if (!File.Exists(absolutePath))
        return null;

    return await File.ReadAllBytesAsync(absolutePath);
}
```

### 3. Profile Configuration

```csharp
public async Task<Profile> CreateProfileAsync(CreateProfileRequest request)
{
    var dataDir = Path.Combine(_profilesDirectory, Guid.NewGuid().ToString());

    var profile = new Profile
    {
        // DataDirectory is within data folder → store as relative
        DataDirectory = _pathHelper.ToRelativePath(dataDir) ?? dataDir,

        // WorkDirectory might be external → convert if possible
        WorkDirectory = _pathHelper.ToRelativePath(request.WorkDirectory) ?? request.WorkDirectory
    };

    // For file operations, use absolute path
    var absoluteDataDir = _pathHelper.ToAbsolutePath(profile.DataDirectory) ?? profile.DataDirectory;
    Directory.CreateDirectory(absoluteDataDir);

    return profile;
}
```

---

## Common Patterns

### Pattern 1: Service Returns Path

Services that generate/find paths should return relative paths:

```csharp
public async Task<string?> GenerateThumbnailAsync(string modDirectory, string sha)
{
    var targetPath = Path.Combine(_thumbnailsPath, $"{sha}.png");
    await ResizeImageAsync(sourcePath, targetPath, 200, 400);

    // Return relative path for storage
    return _pathHelper.ToRelativePath(targetPath);
}
```

### Pattern 2: Service Uses Path

Services that use paths should convert to absolute first:

```csharp
public async Task<bool> DeleteThumbnailAsync(string? relativePath)
{
    if (string.IsNullOrEmpty(relativePath))
        return false;

    // Convert to absolute before file operations
    var absolutePath = _pathHelper.ToAbsolutePath(relativePath);
    if (File.Exists(absolutePath))
    {
        File.Delete(absolutePath);
        return true;
    }
    return false;
}
```

### Pattern 3: Migration/Import

When importing from external sources, convert to relative:

```csharp
public async Task<ModInfo> ImportModAsync(string externalPath)
{
    // Calculate SHA and copy to our storage
    var sha = await CalculateSha256Async(externalPath);
    var archivePath = Path.Combine(_modsDirectory, $"{sha}.7z");
    File.Copy(externalPath, archivePath);

    // Store relative path
    var mod = new ModInfo
    {
        SHA = sha,
        OriginalPath = _pathHelper.ToRelativePath(archivePath)
    };

    return mod;
}
```

---

## Examples

### Example 1: ImageService

```csharp
public class ImageService : IImageService
{
    private readonly string _thumbnailsPath;
    private readonly PathHelper _pathHelper;

    public ImageService(string dataPath, PathHelper pathHelper)
    {
        _thumbnailsPath = Path.Combine(dataPath, "thumbnails");
        _pathHelper = pathHelper ?? throw new ArgumentNullException(nameof(pathHelper));
    }

    public async Task<string?> GenerateThumbnailAsync(string modDir, string sha)
    {
        var targetPath = Path.Combine(_thumbnailsPath, $"{sha}.png");

        // Create thumbnail (file operations use absolute path)
        await ResizeImageAsync(sourcePath, targetPath, 200, 400);

        // Return relative path for database storage
        return _pathHelper.ToRelativePath(targetPath);
    }

    public async Task<byte[]?> LoadThumbnailAsync(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            return null;

        // Convert to absolute for file operations
        var absolutePath = _pathHelper.ToAbsolutePath(relativePath);
        if (!File.Exists(absolutePath))
            return null;

        return await File.ReadAllBytesAsync(absolutePath);
    }
}
```

### Example 2: ProfileService

```csharp
public class ProfileService : IProfileService
{
    private readonly string _baseDataPath;
    private readonly PathHelper _pathHelper;

    public ProfileService(string baseDataPath, PathHelper pathHelper)
    {
        _baseDataPath = baseDataPath ?? throw new ArgumentNullException(nameof(baseDataPath));
        _pathHelper = pathHelper ?? throw new ArgumentNullException(nameof(pathHelper));
    }

    private async Task CreateDefaultProfileAsync()
    {
        var workDir = Path.Combine(_baseDataPath, "work_mods");
        var dataDir = Path.Combine(_baseDataPath, "profiles", "default");

        var profile = new Profile
        {
            // Store as relative paths for portability
            WorkDirectory = _pathHelper.ToRelativePath(workDir) ?? workDir,
            DataDirectory = _pathHelper.ToRelativePath(dataDir) ?? dataDir,
        };

        // Use absolute paths for file operations
        var absoluteDataDir = _pathHelper.ToAbsolutePath(profile.DataDirectory) ?? profile.DataDirectory;
        Directory.CreateDirectory(absoluteDataDir);

        await SaveProfileAsync(profile);
    }
}
```

---

## Testing Path Portability

### Manual Testing

1. **Create test data:**
   - Import a mod
   - Create a profile
   - Generate some thumbnails

2. **Move the application:**
   ```bash
   # Rename the folder
   ren "D3dxSkinManager" "D3dxSkinManager_Test"

   # Or move to different location
   move "D:\Apps\D3dxSkinManager" "E:\Games\ModManager"
   ```

3. **Verify everything works:**
   - Launch the application
   - Check if mods list loads
   - Verify thumbnails display
   - Switch profiles
   - Load/unload mods

### Automated Testing

```csharp
[Fact]
public void PathHelper_ShouldConvertToRelativePath()
{
    var helper = new PathHelper(@"C:\App\data");

    // Path under data folder
    var result = helper.ToRelativePath(@"C:\App\data\thumbnails\abc.png");
    Assert.Equal(@"thumbnails\abc.png", result);

    // Path outside data folder
    var result2 = helper.ToRelativePath(@"D:\Games\MyGame");
    Assert.Equal(@"D:\Games\MyGame", result2);
}

[Fact]
public void PathHelper_ShouldConvertToAbsolutePath()
{
    var helper = new PathHelper(@"C:\App\data");

    // Relative path
    var result = helper.ToAbsolutePath(@"thumbnails\abc.png");
    Assert.Equal(@"C:\App\data\thumbnails\abc.png", result);

    // Already absolute
    var result2 = helper.ToAbsolutePath(@"D:\Games\MyGame");
    Assert.Equal(@"D:\Games\MyGame", result2);
}
```

---

## Checklist for New Features

When adding new features that involve file paths:

```
[ ] Does this feature store paths in database/config?
    [ ] YES → Use _pathHelper.ToRelativePath() before storing
    [ ] NO → Skip

[ ] Does this feature read paths from database/config?
    [ ] YES → Use _pathHelper.ToAbsolutePath() before file operations
    [ ] NO → Skip

[ ] Does this feature create new files?
    [ ] YES → Return relative paths from service methods
    [ ] NO → Skip

[ ] Is PathHelper injected via constructor?
    [ ] YES → Good!
    [ ] NO → Fix dependency injection

[ ] Are all file operations using absolute paths?
    [ ] YES → Good!
    [ ] NO → Convert to absolute first

[ ] Have you tested after moving the folder?
    [ ] YES → Good!
    [ ] NO → Do manual portability test
```

---

## Related Documentation

- [DATA_STORAGE_STRUCTURE.md](DATA_STORAGE_STRUCTURE.md) - Data folder organization
- [MIGRATION_DESIGN.md](../migration/MIGRATION_DESIGN.md) - Python to .NET path migration
- [AI_GUIDE.md](../AI_GUIDE.md) - AI assistant guidelines (includes path conventions)
- [PathHelper.cs](../../D3dxSkinManager/Modules/Core/Services/PathHelper.cs) - Implementation

---

**Remember: If it's in the `data/` folder and gets stored in a file, it MUST be a relative path!**

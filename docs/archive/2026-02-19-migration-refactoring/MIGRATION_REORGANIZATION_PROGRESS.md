# Migration Reorganization - Session Progress

**Date**: 2026-02-19
**Status**: 🚧 IN PROGRESS
**Scope**: Complete migration step reorganization with parser-based architecture

---

## Completed in This Session ✅

### 1. Created All Python Parsers ✅

| Parser | Status | Purpose |
|--------|--------|---------|
| **IPythonConfigurationParser** | ✅ Already existed | Parse local/configuration and home/{env}/configuration |
| **IPythonRedirectionFileParser** | ✅ Renamed | Parse _redirection.ini (character→thumbnail mappings) |
| **IPythonClassificationFileParser** | ✅ Created | Parse classification/* text files (category→objects) |
| **IPythonModIndexParser** | ✅ Created | Parse modsIndex/index_*.json (mod metadata) |

All parsers now have "Python" prefix to indicate Python-to-React migration usage.

### 2. Renamed Existing Steps for New Order ✅

| Old Name | New Name | Changes |
|----------|----------|---------|
| MigrationStep6MigrateConfiguration | MigrationStep2MigrateConfiguration | StepNumber 6→2, PercentComplete 90→15 |
| MigrationStep4MigrateClassifications | MigrationStep3MigrateClassifications | StepNumber 4→3, PercentComplete 60→30 |

---

## Remaining Work 📋

### 3. Update Step3 to Use Parser

**File**: `MigrationStep3MigrateClassifications.cs`

**Current**: Has inline parsing logic
**Target**: Use `IPythonClassificationFileParser`

**Changes Needed**:
```csharp
// BEFORE:
var files = Directory.GetFiles(classDir);
foreach (var file in files)
{
    var categoryName = Path.GetFileName(file);
    var lines = await File.ReadAllLinesAsync(file);
    // ... parse inline
}

// AFTER:
var classifications = await _classificationParser.ParseAsync(classDir);
foreach (var (categoryName, objects) in classifications)
{
    // ... just create nodes
}
```

### 4. Create New Step4: Migrate Classification Thumbnails

**File**: `MigrationStep4MigrateClassificationThumbnails.cs` 🆕

**Purpose**: Associate thumbnails with classification nodes from _redirection.ini

**Dependencies**:
- `IPythonRedirectionFileParser` - Parse _redirection.ini
- `IClassificationService` - Find/update nodes
- `IFileService` - Verify files exist

**Logic** (extract from old Step5 lines 197-235):
```csharp
// Parse redirection file
var stats = await _redirectionParser.GetStatisticsAsync(redirectionFile);
var mappings = await _redirectionParser.ParseAsync(redirectionFile);

// Associate with nodes
foreach (var (characterName, thumbnailPath) in mappings)
{
    var node = await _classificationService.GetNodeByNameAsync(characterName);
    if (node != null)
        await _classificationService.SetNodeThumbnailAsync(node.Id, thumbnailPath);
}
```

### 5. Create New Step5: Migrate Mod Archives + Metadata

**File**: `MigrationStep5MigrateModArchives.cs` 🆕

**Purpose**: Copy/move mod archives AND create mod entries (merged operation)

**Dependencies**:
- `IPythonModIndexParser` - Parse mod index files
- `IFileService` - Copy/move archives
- `IModManagementService` - Create mod entries

**Logic** (merge old Step2 + old Step3):
```csharp
// Parse mod metadata
var modEntries = await _modIndexParser.ParseAsync(modsIndexPath);

foreach (var entry in modEntries)
{
    // Copy/move archive file
    var sourceFile = Path.Combine(sourcePath, "resources", "mods", entry.Sha);
    var destFile = Path.Combine(destPath, "mods", entry.Sha);

    if (context.Options.ArchiveMode == ArchiveHandling.Copy)
        await _fileService.CopyFileAsync(sourceFile, destFile);
    else
        await _fileService.MoveFileAsync(sourceFile, destFile);

    // Create mod entry
    var createRequest = new CreateModRequest
    {
        SHA = entry.Sha,
        Name = entry.Name,
        ...
    };
    await _modManagementService.GetOrCreateModAsync(entry.Sha, createRequest);
}
```

### 6. Create New Step6: Migrate Mod Previews

**File**: `MigrationStep6MigrateModPreviews.cs` 🆕

**Purpose**: Copy mod preview images from resources/preview/

**Dependencies**:
- `IFileService` - Copy files
- `IImageService` - Get supported extensions

**Logic** (extract from old Step5 lines 103-164):
```csharp
// Find preview images
var previewPath = Path.Combine(sourcePath, "resources", "preview");
var imageExtensions = _imageService.GetSupportedImageExtensions();

var allFiles = new List<string>();
foreach (var ext in imageExtensions)
{
    allFiles.AddRange(Directory.GetFiles(previewPath, $"*{ext}", SearchOption.AllDirectories));
}

// Copy to destination
foreach (var sourceFile in allFiles)
{
    var relativePath = Path.GetRelativePath(previewPath, sourceFile);
    var destFile = Path.Combine(destPreviewPath, relativePath);

    await _fileService.EnsureDirectoryExistsAsync(Path.GetDirectoryName(destFile));
    await _fileService.CopyFileAsync(sourceFile, destFile, overwrite: false);
}
```

### 7. Delete Old Files

Files to delete after creating new steps:
- `MigrationStep2MigrateMetadata.cs` (merged into new Step5)
- `MigrationStep3MigrateArchives.cs` (merged into new Step5)
- `MigrationStep5MigratePreviews.cs` (split into Step4 + Step6)

### 8. Update DI Registration

**File**: `MigrationServiceExtensions.cs`

```csharp
public static IServiceCollection AddMigrationServices(this IServiceCollection services)
{
    // Register parsers (all with Python prefix)
    services.AddSingleton<IPythonConfigurationParser, PythonConfigurationParser>();
    services.AddSingleton<IPythonRedirectionFileParser, PythonRedirectionFileParser>();
    services.AddSingleton<IPythonClassificationFileParser, PythonClassificationFileParser>();
    services.AddSingleton<IPythonModIndexParser, PythonModIndexParser>();

    // Register migration steps (NEW ORDER)
    services.AddSingleton<MigrationStep1AnalyzeSource>();
    services.AddSingleton<MigrationStep2MigrateConfiguration>();
    services.AddSingleton<MigrationStep3MigrateClassifications>();
    services.AddSingleton<MigrationStep4MigrateClassificationThumbnails>();
    services.AddSingleton<MigrationStep5MigrateModArchives>();
    services.AddSingleton<MigrationStep6MigrateModPreviews>();

    // Register migration service (orchestrator)
    services.AddSingleton<IMigrationService, MigrationService>();
    services.AddSingleton<IMigrationFacade, MigrationFacade>();

    return services;
}
```

### 9. Update Tests

**File**: `MigrationServiceTests.cs`

**Changes Needed**:
- Add mock for `IPythonClassificationFileParser`
- Add mock for `IPythonModIndexParser`
- Update mock for `IPythonRedirectionFileParser` (renamed)
- Create steps in new order with new dependencies
- Update test expectations

---

## New Migration Flow (After Complete)

### Step Order & Percentages:

| Step | Range | Description | Services Used |
|------|-------|-------------|---------------|
| 1 | 0-10% | Analyze Python installation | IImageService, IPythonConfigurationParser |
| 2 | 10-20% | Migrate configuration | IConfigurationService |
| 3 | 20-40% | Create classification nodes | IPythonClassificationFileParser, IClassificationService |
| 4 | 40-50% | Associate thumbnails | IPythonRedirectionFileParser, IClassificationService |
| 5 | 50-75% | Migrate mod archives + metadata | IPythonModIndexParser, IFileService, IModManagementService |
| 6 | 75-100% | Copy mod preview images | IFileService, IImageService |

### Logical Dependencies:

```
Step 1 (Analyze) → Validates everything
       ↓
Step 2 (Config) → Sets up profile
       ↓
Step 3 (Classifications) → Creates nodes
       ↓
Step 4 (Class Thumbnails) → Attaches to nodes
       ↓
Step 5 (Mod Archives) → Creates mods (attached to nodes)
       ↓
Step 6 (Mod Previews) → Copies preview images for mods
```

**Why This Order is Correct**:
- Configuration first → Profile ready
- Classifications before mods → Nodes exist for attachment
- Thumbnails after classifications → Nodes exist
- Mods after classifications → Can attach to existing nodes
- Previews last → Mods exist

---

## Parser-Based Architecture Benefits

### Consistent Pattern Across All Steps:

**Step 2**: Parser (config) → Update (ConfigurationService)
**Step 3**: Parser (classifications) → Create (ClassificationService)
**Step 4**: Parser (redirection) → Update (ClassificationService)
**Step 5**: Parser (mod index) → Copy (FileService) + Create (ModManagementService)
**Step 6**: No parser → Copy (FileService) only

### Clean Separation:

- **Parsers**: Know Python file formats
- **Steps**: Know migration workflow
- **Services**: Know domain logic

### No More:

- ❌ JSON parsing in steps
- ❌ Direct file reading in steps
- ❌ Direct repository access
- ❌ Domain logic in steps

### Always:

- ✅ Use parsers for Python files
- ✅ Use FileService for file operations
- ✅ Use domain services for CRUD
- ✅ Steps orchestrate only

---

## Files Created This Session

### Parsers:
- ✅ `PythonClassificationFileParser.cs` (130 lines)
- ✅ `PythonModIndexParser.cs` (101 lines)

### Renamed:
- ✅ `PythonRedirectionFileParser.cs` (was RedirectionFileParser.cs)

### Steps Renamed:
- ✅ `MigrationStep2MigrateConfiguration.cs` (was Step6)
- ✅ `MigrationStep3MigrateClassifications.cs` (was Step4)

---

## Next Session Tasks

1. Update Step3 to use `IPythonClassificationFileParser`
2. Create Step4 `MigrateClassificationThumbnails`
3. Create Step5 `MigrateModArchives` (merge Step2+Step3)
4. Create Step6 `MigrateModPreviews`
5. Delete old Step2, Step3, Step5
6. Update DI registration
7. Update tests
8. Build and verify all 173 tests pass

---

**Estimated Remaining Time**: 2-3 hours

**Current Token Usage**: ~137k/200k (need fresh session for completion)

**Recommendation**: Commit current progress (parsers created, steps renamed), continue in next session with step creation and integration.

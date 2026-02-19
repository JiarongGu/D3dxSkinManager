# Migration Service Architecture

**Last Updated**: 2026-02-20
**Status**: ✅ Complete and refactored

## Overview

The Migration Service migrates data from the Python d3dxSkinManage application to the React/.NET version. It uses a clean **step-based architecture** where each migration task is a separate, focused class.

## Architecture Principles

### 1. Thin Orchestrator Pattern
- `MigrationService` is a **thin orchestrator** (205 lines)
- Delegates all work to specialized step classes
- No business logic in the orchestrator
- Steps are automatically ordered by `StepNumber`

### 2. Step-Based Workflow
Each step is a separate class implementing `IMigrationStep`:
```csharp
public interface IMigrationStep
{
    int StepNumber { get; }
    string StepName { get; }
    Task ExecuteAsync(MigrationContext context, IProgress<MigrationProgress>? progress, CancellationToken cancellationToken);
}
```

### 3. Service Layer Usage
- Steps use domain services, NOT repositories directly
- Example: `ModManagementService` (not `ModRepository`)
- Example: `FileService` for file operations
- Example: Parser services for reading Python config files

## Migration Steps

### Step 1: Analyze Source
**File**: `MigrationStep1AnalyzeSource.cs`
**Responsibility**: Validate Python installation structure

- Checks for `resources/` and `home/` directories
- Detects available environments
- Counts mods, previews, cache
- Parses Python configuration
- Stores analysis in `MigrationContext` for other steps

### Step 2: Migrate Configuration
**File**: `MigrationStep2MigrateConfiguration.cs`
**Responsibility**: Migrate Python configuration to React version

- Reads `local/configuration` and `home/{ENV}/configuration`
- Migrates game path, work directory, and launch arguments
- Uses `ConfigurationService` (not manual JSON parsing)
- Adds `migratedFrom: "python"` marker

### Step 3: Migrate Classifications
**File**: `MigrationStep3MigrateClassifications.cs`
**Responsibility**: Migrate classification tree structure

- Reads `classification.ini` files from Python
- Uses `PythonClassificationFileParser`
- Creates classification nodes (categories)
- Uses `ClassificationRepository` (with service layer)

### Step 4: Migrate Classification Thumbnails
**File**: `MigrationStep4MigrateClassificationThumbnails.cs`
**Responsibility**: Copy classification thumbnail images

- Reads `thumbnail/_redirection.ini` from Python
- Uses `PythonRedirectionFileParser`
- Copies thumbnail images to new location
- Uses `FileService` for copying

### Step 5: Migrate Mod Archives
**File**: `MigrationStep5MigrateModArchives.cs`
**Responsibility**: Copy mod archives and create mod entries

- Parses `modsIndex/index_*.json` files
- Uses `PythonModIndexParser`
- **Copies archives WITHOUT extensions** (matches Python format)
- Auto-detects archive type using SharpCompress when needed
- Creates mod entries using `ModManagementService`

**Archive Storage** (as of 2026-02-20):
- Python: `resources/mods/{SHA}` (no extension)
- .NET: `data/profiles/{id}/mods/{SHA}` (no extension)
- SharpCompress auto-detects format (ZIP/7z/RAR/TAR)

### Step 6: Migrate Mod Previews
**File**: `MigrationStep6MigrateModPreviews.cs`
**Responsibility**: Copy mod preview images

- Copies from `resources/preview/{SHA}/` to new location
- Scans for all image types
- Uses `FileService` for copying

## Data Flow

```
User Request
    ↓
MigrationFacade (IPC layer)
    ↓
MigrationService (orchestrator)
    ↓
Step 1: Analyze Source
    ↓
Step 2: Migrate Configuration
    ↓
Step 3: Migrate Classifications
    ↓
Step 4: Migrate Classification Thumbnails
    ↓
Step 5: Migrate Mod Archives
    ↓
Step 6: Migrate Mod Previews
    ↓
Migration Complete
```

## Parser Services

Migration uses specialized parsers for Python file formats:

### `IPythonConfigurationParser`
- Reads `local/configuration` and `home/{ENV}/configuration`
- Returns `PythonConfiguration` model

### `IPythonClassificationFileParser`
- Reads `.ini` files with classification data
- Returns list of classification entries

### `IPythonRedirectionFileParser`
- Reads `thumbnail/_redirection.ini`
- Maps SHA → thumbnail path

### `IPythonModIndexParser`
- Reads `modsIndex/index_*.json` files
- Returns list of `PythonModEntry` with metadata

## Models

### `MigrationContext`
Shared state across all steps:
```csharp
public class MigrationContext
{
    public MigrationOptions Options { get; set; }
    public string LogPath { get; set; }
    public MigrationAnalysis? Analysis { get; set; }
    public string? EnvironmentName { get; set; }
    public string? EnvironmentPath { get; set; }
    public MigrationResult Result { get; set; }
}
```

### `MigrationOptions`
```csharp
public class MigrationOptions
{
    public string SourcePath { get; set; }
    public string? EnvironmentName { get; set; }
    public bool MigrateArchives { get; set; } = true;
    public bool MigrateMetadata { get; set; } = true;
    public bool MigratePreviews { get; set; } = true;
    public bool MigrateConfiguration { get; set; } = true;
    public bool MigrateClassifications { get; set; } = true;
    public bool MigrateCache { get; set; } = false;
    public ArchiveHandling ArchiveMode { get; set; } = ArchiveHandling.Copy;
    public PostMigrationAction PostAction { get; set; } = PostMigrationAction.Keep;
}
```

### `MigrationResult`
```csharp
public class MigrationResult
{
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
    public int ModsMigrated { get; set; }
    public int ArchivesCopied { get; set; }
    public int PreviewsCopied { get; set; }
    public int ClassificationRulesCreated { get; set; }
    public List<string> Errors { get; set; } = new();
    public string? LogFilePath { get; set; }
}
```

## Progress Reporting

Migration reports progress through `IProgress<MigrationProgress>`:

```csharp
public class MigrationProgress
{
    public MigrationStage Stage { get; set; }
    public string CurrentTask { get; set; }
    public int ProcessedItems { get; set; }
    public int TotalItems { get; set; }
    public int PercentComplete { get; set; }
    public string? ErrorMessage { get; set; }
}
```

Stages:
- `Analyzing` (0-10%)
- `MigratingConfiguration` (10-20%)
- `MigratingClassifications` (20-30%)
- `MigratingThumbnails` (30-40%)
- `MigratingMetadata` (40-70%)
- `MigratingPreviews` (70-90%)
- `Finalizing` (90-100%)
- `Complete`
- `Error`

## Logging

- All steps log to a migration log file: `logs/migration_{timestamp}.log`
- Format: `[HH:mm:ss] message`
- Also logs to `ILogHelper` for console output
- Log path returned in `MigrationResult.LogFilePath`

## Error Handling

- Each step has try-catch for individual operations
- Errors logged but migration continues for remaining items
- Critical failures throw exceptions to stop migration
- All errors collected in `MigrationResult.Errors`

## Testing

- Each step is independently testable
- Mock services for unit tests
- Integration tests with real Python test data
- See: `D3dxSkinManager.Tests/Modules/Migration/`

## Adding a New Migration Step

1. Create new class implementing `IMigrationStep`
2. Set `StepNumber` and `StepName`
3. Inject required services in constructor
4. Implement `ExecuteAsync` method
5. Add to DI in `MigrationServiceExtensions.cs`
6. Inject in `MigrationService` constructor
7. Add to `_steps` list (auto-sorted by StepNumber)

Example:
```csharp
public class MigrationStep7Example : IMigrationStep
{
    private readonly IServiceToUse _service;
    private readonly ILogHelper _logger;

    public int StepNumber => 7;
    public string StepName => "Example Step";

    public MigrationStep7Example(IServiceToUse service, ILogHelper logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        MigrationContext context,
        IProgress<MigrationProgress>? progress,
        CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

## Related Documentation

- [Migration Parsers](../MIGRATION_PARSER_ARCHITECTURE.md) - Parser service details
- [Migration Design](../migration/MIGRATION_DESIGN.md) - Original design doc
- [Archive: 2026-02-19 Refactoring](../archive/2026-02-19-migration-refactoring/) - Historical refactoring docs

## Key Achievements

✅ Reduced from 991 lines (god class) to 205 lines (thin orchestrator)
✅ Each step is focused and testable
✅ Proper service layer usage (no repository bypassing)
✅ Clean separation of concerns
✅ Extensible: Easy to add new steps
✅ Archives stored without extensions (matches Python)
✅ Auto-detects archive types using SharpCompress

---

*Last refactored: 2026-02-19*
*Archive storage fix: 2026-02-20*

# Migration Parser Architecture - Complete Design

**Date**: 2026-02-19 (Updated: 2026-02-26)
**User Insight**: *"we probably need parser for each step and the step itself mostly just data update and file copy"*

---

## Core Principle

**Each migration step should**:
1. **Parse** Python files using a dedicated parser
2. **Copy** files using FileService
3. **Create/Update** data using module services (Mods, Core, Tools)

**Steps are thin orchestrators** - no parsing logic, no business logic, just coordination.

---

## Complete Architecture

### Step 1: Analyze Source
**Parser**: `IPythonInstallationAnalyzer`
**What it parses**: Python directory structure, counts resources
**Step does**: Validate and store analysis results
**Services used**: IImageService (for extensions)

### Step 2: Migrate Configuration
**Parser**: `IPythonConfigurationParser` ✅ EXISTS
**What it parses**: `local/configuration` and `home/{env}/configuration` files
**Step does**: Apply parsed configuration to current profile
**Services used**: IConfigurationService

### Step 3: Migrate Categories
**Parser**: `IPythonCategoryFileParser` ✅ EXISTS
**What it parses**: `home/{env}/classification/*` text files (one category per line)
**Step does**: Create category nodes with auto-detection wildcard patterns
**Services used**: ICategoryService

### Step 4: Migrate Category Thumbnails
**Parser**: `IRedirectionFileParser` ✅ EXISTS
**What it parses**: `_redirection.ini` (character→thumbnail mappings)
**Step does**: Copy thumbnails, associate with category nodes
**Services used**: IFileService (copy), ICategoryService (associate)

### Step 5: Migrate Mod Archives + Metadata
**Parser**: `IPythonModIndexParser` 🆕 NEED TO CREATE
**What it parses**: `home/{env}/modsIndex/index_*.json` files
**Step does**: Copy/move archives, create mod entries
**Services used**: IFileService (copy/move), IModManagementService (create)

### Step 6: Migrate Mod Previews
**Parser**: None needed (directory scan only)
**What it parses**: N/A
**Step does**: Copy mod preview images from `resources/preview/`
**Services used**: IFileService (copy), IImageService (extensions)

---

## Parsers to Create

### 1. IPythonCategoryFileParser ✅ DONE

```csharp
namespace D3dxSkinManager.Modules.Migration.Parsers;

/// <summary>
/// Parser for Python category files (from classification directory)
/// Each file represents a parent category, each line is a child category name
/// </summary>
public interface IPythonCategoryFileParser
{
    /// <summary>
    /// Parse classification directory
    /// </summary>
    /// <returns>Dictionary of parentCategoryName → List of childCategoryNames</returns>
    Task<Dictionary<string, List<string>>> ParseAsync(string classificationDirectory);
}
```

**Example**:
- File: `home/Endfield/classification/Characters`
- Content:
  ```
  Alice
  Bob
  Charlie
  ```
- Returns: `{ "Characters": ["Alice", "Bob", "Charlie"] }`
- Creates: Parent category "Characters" with children "Alice", "Bob", "Charlie"

### 2. IPythonModIndexParser 🆕

```csharp
namespace D3dxSkinManager.Modules.Migration.Parsers;

/// <summary>
/// Parser for Python mod index files
/// Parses index_*.json files containing mod metadata
/// </summary>
public interface IPythonModIndexParser
{
    /// <summary>
    /// Parse mod index directory
    /// </summary>
    /// <returns>List of mod entries with metadata</returns>
    Task<List<PythonModEntry>> ParseAsync(string modsIndexDirectory);
}
```

**Example**:
- File: `home/Endfield/modsIndex/index_1.json`
- Content:
  ```json
  {
    "mods": {
      "abc123": {
        "name": "Cool Mod",
        "author": "ModAuthor",
        "object": "Alice",
        ...
      }
    }
  }
  ```
- Returns: `[PythonModEntry { Sha="abc123", Name="Cool Mod", ... }]`

### 3. IPythonInstallationAnalyzer (Refactor Step1) 📋 FUTURE

Currently Step1 has analysis logic embedded. Could extract to parser for consistency.

---

## Step Implementations (After Parsers)

### Step 3: Migrate Categories (With Parser)

```csharp
public class MigrationStep3MigrateCategories : IMigrationStep
{
    private readonly IPythonCategoryFileParser _categoryParser;  // ✅ Parser
    private readonly ICategoryService _categoryService;  // ✅ Service

    public async Task ExecuteAsync(MigrationContext context, ...)
    {
        // 1. PARSE: Read Python category files from classification directory
        var categories = await _categoryParser.ParseAsync(classificationPath);

        // 2. CREATE: Create category nodes using service
        foreach (var (parentCategoryName, childCategoryNames) in categories)
        {
            // Generate GUID for parent category
            var parentId = Guid.NewGuid().ToString();

            // Create parent category (root level)
            await _categoryService.CreateAsync(parentId, parentCategoryName, null, ...);

            // Create child categories with wildcard patterns
            foreach (var childCategoryName in childCategoryNames)
            {
                var childId = Guid.NewGuid().ToString();
                await _categoryService.CreateAsync(childId, childCategoryName, parentId, ...);

                // Set wildcard pattern for auto-detection
                await _categoryService.UpdateCategoryAsync(childId, ..., "Wildcard", $"*{childCategoryName}*");
            }
        }
    }
}
```

**No parsing logic in step** - all parsing in `IPythonCategoryFileParser`!

### Step 5: Migrate Mod Archives + Metadata (With Parser)

```csharp
public class MigrationStep5MigrateModArchives : IMigrationStep
{
    private readonly IPythonModIndexParser _modIndexParser;  // ✅ Parser
    private readonly IFileService _fileService;  // ✅ File operations
    private readonly IModManagementService _modManagementService;  // ✅ Mod CRUD

    public async Task ExecuteAsync(MigrationContext context, ...)
    {
        // 1. PARSE: Read mod metadata from index files
        var modEntries = await _modIndexParser.ParseAsync(modsIndexPath);

        foreach (var entry in modEntries)
        {
            // 2. COPY: Copy/move archive file
            var sourceFile = Path.Combine(sourcePath, "resources", "mods", entry.Sha);
            var destFile = Path.Combine(destPath, "mods", entry.Sha);

            if (context.Options.ArchiveMode == ArchiveHandling.Copy)
                await _fileService.CopyFileAsync(sourceFile, destFile);
            else
                await _fileService.MoveFileAsync(sourceFile, destFile);

            // 3. CREATE: Create mod entry with metadata
            var createRequest = new CreateModRequest
            {
                SHA = entry.Sha,
                Name = entry.Name,
                Author = entry.Author,
                Category = entry.Object,
                Description = entry.Explain,
                ...
            };

            await _modManagementService.GetOrCreateModAsync(entry.Sha, createRequest);
        }
    }
}
```

**No JSON parsing in step** - all parsing in `IPythonModIndexParser`!

---

## Benefits of Parser Architecture

### 1. Separation of Concerns
- **Parsers**: Know Python file formats
- **Steps**: Know migration workflow
- **Services**: Know domain logic

### 2. Testability
- Test parsers independently with sample Python files
- Test steps with mocked parsers
- Test services independently

### 3. Reusability
- Parsers can be used outside migration (e.g., validation tools)
- Steps are simple orchestration (easy to understand)
- Services are reused across application

### 4. Maintainability
- Python format changes? Update parser only
- Migration workflow changes? Update step only
- Domain logic changes? Update service only

---

## Current Status

| Component | Status | Notes |
|-----------|--------|-------|
| IPythonConfigurationParser | ✅ EXISTS | Parses configuration files |
| IRedirectionFileParser | ✅ EXISTS | Parses _redirection.ini |
| IPythonCategoryFileParser | ✅ DONE | Parses classification directory text files |
| IPythonModIndexParser | 🆕 NEED TO CREATE | Parse mod index JSON files |
| Step1 (Analyze) | ✅ EXISTS | Could extract analyzer |
| Step2 (Configuration) | ✅ DONE | Uses IPythonConfigurationParser |
| Step3 (Categories) | ✅ DONE | Uses IPythonCategoryFileParser |
| Step4 (Category Thumbnails) | 🆕 NEED TO CREATE | Uses IRedirectionFileParser |
| Step5 (Mod Archives) | 🆕 NEED TO CREATE | Needs IPythonModIndexParser |
| Step6 (Mod Previews) | 🆕 NEED TO CREATE | No parser needed |

---

## Implementation Order

### Phase 1: Create Missing Parsers
1. ✅ IPythonConfigurationParser (exists)
2. ✅ IRedirectionFileParser (exists)
3. 🆕 Create IClassificationFileParser
4. 🆕 Create IPythonModIndexParser

### Phase 2: Create/Update Steps
1. ✅ Step1: AnalyzeSource (no changes needed)
2. ✅ Step2: MigrateConfiguration (renamed, uses parser)
3. ✅ Step3: MigrateCategories (done, uses IPythonCategoryFileParser)
4. 🆕 Step4: MigrateCategoryThumbnails (create, uses parser)
5. 🆕 Step5: MigrateModArchives (create, uses parser)
6. 🆕 Step6: MigrateModPreviews (create, no parser)

### Phase 3: Register & Test
1. Update DI registration
2. Update tests with new mocks
3. Build and verify

---

## Key Principle (User Insight)

> **"We probably need parser for each step and the step itself mostly just data update and file copy"**

**Implementation**:
- Step 2: Parser (config) → Update (ConfigurationService)
- Step 3: Parser (categories from classification dir) → Create (CategoryService) with wildcard patterns
- Step 4: Parser (redirection) → Copy (FileService) → Update (CategoryService)
- Step 5: Parser (mod index) → Copy (FileService) → Create (ModManagementService)
- Step 6: No parser → Copy (FileService) only

**Steps do NOT**:
- ❌ Parse JSON directly
- ❌ Read files directly (except via parsers)
- ❌ Implement domain logic
- ❌ Access repositories

**Steps DO**:
- ✅ Use parsers to get data structures
- ✅ Use FileService to copy files
- ✅ Use domain services to create/update data
- ✅ Orchestrate the workflow

---

**Status Update (2026-02-26)**:
- ✅ IPythonCategoryFileParser created and integrated
- ✅ Step3 refactored to use Category terminology (Classification → Category)
- ✅ Migration now reads from `classification/` directory and creates Category hierarchy
- 🆕 Next: Create IPythonModIndexParser and remaining migration steps

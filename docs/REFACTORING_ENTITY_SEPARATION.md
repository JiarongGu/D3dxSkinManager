# Entity-Domain Model Separation Plan

**Created:** 2026-03-09
**Priority:** CRITICAL - Architectural Foundation
**Related:** REFACTORING_PLAN.md

---

## 📌 Problem Statement

The current codebase mixes **database entity models** (what's stored) with **domain models** (what business logic uses). This violates clean architecture principles and creates confusion about data sources.

### Example from ModInfo.cs

```csharp
public class ModInfo
{
    // ✅ Database properties (should be in Entity)
    public string SHA { get; set; }
    public string Name { get; set; }
    public string Author { get; set; }
    public List<string> Tags { get; set; }
    public DateTime CreatedAt { get; set; }

    // ❌ Computed properties (should ONLY be in Domain model)
    public string CategoryName { get; set; }        // Joined from Category table
    public List<Tag> TagsWithMetadata { get; set; }  // Joined from Tags table
    public bool IsLoaded { get; set; }               // Computed from file system
    public bool IsAvailable { get; set; }            // Computed from file system
    public string? CachePath { get; set; }           // Computed from file system
    public string? PreviewFolderPath { get; set; }   // Computed from file system
}
```

---

## 🎯 Solution: Three-Layer Model Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    PRESENTATION LAYER                        │
│                    (Frontend / IPC)                          │
│                Uses: Domain Models (ModInfo)                 │
└─────────────────────────────────────────────────────────────┘
                              ↕
┌─────────────────────────────────────────────────────────────┐
│                     SERVICE LAYER                            │
│              (Business Logic / Facades)                      │
│                Uses: Domain Models (ModInfo)                 │
│                Maps: Entity ↔ Domain                         │
└─────────────────────────────────────────────────────────────┘
                              ↕
┌─────────────────────────────────────────────────────────────┐
│                   REPOSITORY LAYER                           │
│                 (Data Access / CRUD)                         │
│                Uses: Entity Models (ModEntity)               │
│                Talks to: SQLite Database                     │
└─────────────────────────────────────────────────────────────┘
```

---

## 📦 Model Types

### 1. Entity Models (New)
**Location:** `Modules/{Module}/Entities/`
**Purpose:** Pure database representation
**Characteristics:**
- Properties map 1:1 to database columns
- No computed properties
- No navigation properties (unless using EF Core)
- Serializable to/from SQLite
- Used ONLY in repository layer

**Example:** `ModEntity.cs`

```csharp
namespace D3dxSkinManager.Modules.Mod.Entities;

/// <summary>
/// Database entity for Mods table
/// Maps 1:1 to database columns
/// </summary>
public class ModEntity
{
    // Primary key
    public string SHA { get; set; } = string.Empty;

    // Foreign keys
    public string CategoryId { get; set; } = string.Empty;

    // Basic properties
    public string Name { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = "7z";
    public string Grading { get; set; } = "G";

    // JSON-serialized properties
    public string TagsJson { get; set; } = "[]";  // Stored as JSON array

    // Boolean flags (stored as 0/1 in SQLite)
    public bool DisablePreview { get; set; } = false;

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Extension field for future use
    public string? Metadata { get; set; }

    // Note: No computed properties, no file system paths, no joined data
}
```

### 2. Domain Models (Keep Existing)
**Location:** `Modules/{Module}/Models/`
**Purpose:** Business logic representation
**Characteristics:**
- Rich domain objects with computed properties
- Contains data from multiple sources (DB + file system + joins)
- Used in services, facades, and frontend
- May contain validation logic

**Example:** `ModInfo.cs` (cleaned up)

```csharp
namespace D3dxSkinManager.Modules.Mod.Models;

/// <summary>
/// Domain model for mod with computed properties
/// Used in business logic and presentation layers
/// </summary>
public class ModInfo
{
    // ===== Core properties (from ModEntity) =====
    public string SHA { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = "7z";
    public string Grading { get; set; } = "G";
    public List<string> Tags { get; set; } = new();
    public bool DisablePreview { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? Metadata { get; set; }

    // ===== Computed properties (joined from other tables) =====
    public string CategoryName { get; set; } = string.Empty;  // Joined from Categories table
    public List<Tag> TagsWithMetadata { get; set; } = new();   // Joined from Tags table

    // ===== Runtime properties (computed from file system) =====
    public bool IsLoaded { get; set; }         // Computed: work directory exists
    public bool IsAvailable { get; set; }      // Computed: archive file exists
    public bool HasCache { get; set; }         // Computed: cache directory exists
    public bool HasPreviewFolder { get; set; } // Computed: preview directory exists
    public bool IsOrphaned { get; set; }       // Computed: in cache but not in DB

    // ===== File paths (computed at runtime) =====
    public string? CachePath { get; set; }          // Computed from SHA
    public string? PreviewFolderPath { get; set; }  // Computed from SHA
    public string? ArchiveFolderPath { get; set; }  // Computed from profile paths
}
```

### 3. Mapper Classes (New)
**Location:** `Modules/{Module}/Mappers/`
**Purpose:** Convert between Entity ↔ Domain models
**Characteristics:**
- Static methods for conversions
- Handles JSON serialization/deserialization
- Performs data transformations
- No business logic (pure mapping)

**Example:** `ModMapper.cs`

```csharp
namespace D3dxSkinManager.Modules.Mod.Mappers;

/// <summary>
/// Maps between ModEntity (database) and ModInfo (domain)
/// </summary>
public static class ModMapper
{
    /// <summary>
    /// Convert entity to domain model (basic properties only)
    /// </summary>
    public static ModInfo ToDomain(ModEntity entity)
    {
        return new ModInfo
        {
            SHA = entity.SHA,
            Category = entity.CategoryId,
            Name = entity.Name,
            Author = entity.Author,
            Description = entity.Description,
            Type = entity.Type,
            Grading = entity.Grading,
            Tags = JsonHelper.Deserialize<List<string>>(entity.TagsJson) ?? new List<string>(),
            DisablePreview = entity.DisablePreview,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            Metadata = entity.Metadata,

            // Computed properties are NOT set here - set by services
            CategoryName = string.Empty,
            TagsWithMetadata = new List<Tag>(),
            IsLoaded = false,
            IsAvailable = false,
            HasCache = false,
            HasPreviewFolder = false,
            IsOrphaned = false,
            CachePath = null,
            PreviewFolderPath = null,
            ArchiveFolderPath = null
        };
    }

    /// <summary>
    /// Convert domain model to entity (for database storage)
    /// </summary>
    public static ModEntity ToEntity(ModInfo domainModel)
    {
        return new ModEntity
        {
            SHA = domainModel.SHA,
            CategoryId = domainModel.Category,
            Name = domainModel.Name,
            Author = domainModel.Author,
            Description = domainModel.Description,
            Type = domainModel.Type,
            Grading = domainModel.Grading,
            TagsJson = JsonHelper.Serialize(domainModel.Tags),
            DisablePreview = domainModel.DisablePreview,
            CreatedAt = domainModel.CreatedAt,
            UpdatedAt = domainModel.UpdatedAt,
            Metadata = domainModel.Metadata

            // Note: Computed properties are ignored - they don't belong in the database
        };
    }

    /// <summary>
    /// Update entity from domain model (for updates)
    /// </summary>
    public static void UpdateEntity(ModEntity entity, ModInfo domainModel)
    {
        // Do NOT update SHA (primary key)
        entity.CategoryId = domainModel.Category;
        entity.Name = domainModel.Name;
        entity.Author = domainModel.Author;
        entity.Description = domainModel.Description;
        entity.Type = domainModel.Type;
        entity.Grading = domainModel.Grading;
        entity.TagsJson = JsonHelper.Serialize(domainModel.Tags);
        entity.DisablePreview = domainModel.DisablePreview;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.Metadata = domainModel.Metadata;
    }
}
```

---

## 🔧 Updated BaseRepository<TEntity>

The base repository now works exclusively with **entity models**:

```csharp
namespace D3dxSkinManager.Modules.Core.Data;

/// <summary>
/// Base repository for database operations using entity models
/// TEntity: Database entity type (e.g., ModEntity, CategoryEntity)
/// </summary>
public abstract class BaseRepository<TEntity> where TEntity : class
{
    protected readonly string ConnectionString;
    protected readonly ILogHelper Logger;

    protected BaseRepository(string connectionString, ILogHelper logger)
    {
        ConnectionString = connectionString;
        Logger = logger;
    }

    /// <summary>
    /// Execute query and return list of entities
    /// </summary>
    protected async Task<List<TEntity>> ExecuteQueryAsync(
        string sql,
        Action<SqliteCommand>? configureParameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = sql;
        configureParameters?.Invoke(command);

        var results = new List<TEntity>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(MapToEntity(reader));
        }

        return results;
    }

    /// <summary>
    /// Execute query and return single entity
    /// </summary>
    protected async Task<TEntity?> ExecuteQuerySingleAsync(
        string sql,
        Action<SqliteCommand>? configureParameters = null,
        CancellationToken cancellationToken = default)
    {
        var results = await ExecuteQueryAsync(sql, configureParameters, cancellationToken);
        return results.FirstOrDefault();
    }

    /// <summary>
    /// Execute non-query command (INSERT/UPDATE/DELETE)
    /// </summary>
    protected async Task<int> ExecuteNonQueryAsync(
        string sql,
        Action<SqliteCommand>? configureParameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = sql;
        configureParameters?.Invoke(command);

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Execute scalar query (COUNT, EXISTS, etc.)
    /// </summary>
    protected async Task<T?> ExecuteScalarAsync<T>(
        string sql,
        Action<SqliteCommand>? configureParameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = sql;
        configureParameters?.Invoke(command);

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result != null ? (T)result : default;
    }

    /// <summary>
    /// Check if entity exists by ID
    /// </summary>
    protected async Task<bool> ExistsAsync(
        string tableName,
        string idColumnName,
        string idValue,
        CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT COUNT(*) FROM [{tableName}] WHERE {idColumnName} = @id";
        var count = await ExecuteScalarAsync<long>(sql, cmd =>
        {
            cmd.Parameters.AddWithValue("@id", idValue);
        }, cancellationToken);

        return count > 0;
    }

    /// <summary>
    /// Abstract method: Map SqliteDataReader to entity
    /// Subclasses implement this to map database rows to entity objects
    /// </summary>
    protected abstract TEntity MapToEntity(SqliteDataReader reader);
}
```

---

## 🔄 Updated Repository Pattern

Repositories now:
1. Work exclusively with **entity models**
2. Return entities from queries
3. Accept entities for inserts/updates

**Example:** `ModRepository.cs`

```csharp
namespace D3dxSkinManager.Modules.Mod.Services;

public interface IModRepository
{
    Task<List<ModEntity>> GetAllAsync();
    Task<ModEntity?> GetByIdAsync(string sha);
    Task<bool> ExistsAsync(string sha);
    Task<ModEntity> InsertAsync(ModEntity entity);
    Task<bool> UpdateAsync(ModEntity entity);
    Task<bool> DeleteAsync(string sha);
    Task<List<ModEntity>> GetByCategoryAsync(string categoryId);
    // ... other methods
}

public class ModRepository : BaseRepository<ModEntity>, IModRepository
{
    public ModRepository(IProfilePathService profilePaths, ILogHelper logger)
        : base($"Data Source={profilePaths.ProfileDatabasePath}", logger)
    {
    }

    public async Task<List<ModEntity>> GetAllAsync()
    {
        return await ExecuteQueryAsync("SELECT * FROM Mods ORDER BY SHA");
    }

    public async Task<ModEntity?> GetByIdAsync(string sha)
    {
        return await ExecuteQuerySingleAsync(
            "SELECT * FROM Mods WHERE SHA = @sha",
            cmd => cmd.Parameters.AddWithValue("@sha", sha)
        );
    }

    public async Task<bool> ExistsAsync(string sha)
    {
        return await base.ExistsAsync("Mods", "SHA", sha);
    }

    public async Task<ModEntity> InsertAsync(ModEntity entity)
    {
        var sql = @"
            INSERT INTO Mods (SHA, Category, Name, Author, Description, Type, Grading, Tags, DisablePreview, CreatedAt, UpdatedAt, Metadata)
            VALUES (@sha, @category, @name, @author, @description, @type, @grading, @tags, @disablePreview, @createdAt, @updatedAt, @metadata)";

        await ExecuteNonQueryAsync(sql, cmd =>
        {
            cmd.Parameters.AddWithValue("@sha", entity.SHA);
            cmd.Parameters.AddWithValue("@category", entity.CategoryId);
            cmd.Parameters.AddWithValue("@name", entity.Name);
            cmd.Parameters.AddWithValue("@author", entity.Author);
            cmd.Parameters.AddWithValue("@description", entity.Description);
            cmd.Parameters.AddWithValue("@type", entity.Type);
            cmd.Parameters.AddWithValue("@grading", entity.Grading);
            cmd.Parameters.AddWithValue("@tags", entity.TagsJson);
            cmd.Parameters.AddWithValue("@disablePreview", entity.DisablePreview ? 1 : 0);
            cmd.Parameters.AddWithValue("@createdAt", entity.CreatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("@updatedAt", entity.UpdatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("@metadata", entity.Metadata ?? (object)DBNull.Value);
        });

        return entity;
    }

    public async Task<bool> UpdateAsync(ModEntity entity)
    {
        var sql = @"
            UPDATE Mods
            SET Category = @category, Name = @name, Author = @author, Description = @description,
                Type = @type, Grading = @grading, Tags = @tags, DisablePreview = @disablePreview,
                UpdatedAt = @updatedAt, Metadata = @metadata
            WHERE SHA = @sha";

        var rowsAffected = await ExecuteNonQueryAsync(sql, cmd =>
        {
            cmd.Parameters.AddWithValue("@sha", entity.SHA);
            cmd.Parameters.AddWithValue("@category", entity.CategoryId);
            cmd.Parameters.AddWithValue("@name", entity.Name);
            cmd.Parameters.AddWithValue("@author", entity.Author);
            cmd.Parameters.AddWithValue("@description", entity.Description);
            cmd.Parameters.AddWithValue("@type", entity.Type);
            cmd.Parameters.AddWithValue("@grading", entity.Grading);
            cmd.Parameters.AddWithValue("@tags", entity.TagsJson);
            cmd.Parameters.AddWithValue("@disablePreview", entity.DisablePreview ? 1 : 0);
            cmd.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("@metadata", entity.Metadata ?? (object)DBNull.Value);
        });

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(string sha)
    {
        var rowsAffected = await ExecuteNonQueryAsync(
            "DELETE FROM Mods WHERE SHA = @sha",
            cmd => cmd.Parameters.AddWithValue("@sha", sha)
        );

        return rowsAffected > 0;
    }

    public async Task<List<ModEntity>> GetByCategoryAsync(string categoryId)
    {
        return await ExecuteQueryAsync(
            "SELECT * FROM Mods WHERE Category = @category",
            cmd => cmd.Parameters.AddWithValue("@category", categoryId)
        );
    }

    /// <summary>
    /// Map database row to entity (1:1 with database columns)
    /// </summary>
    protected override ModEntity MapToEntity(SqliteDataReader reader)
    {
        return new ModEntity
        {
            SHA = reader.GetStringOrDefault("SHA"),
            CategoryId = reader.GetStringOrDefault("Category"),
            Name = reader.GetStringOrDefault("Name"),
            Author = reader.GetStringOrDefault("Author"),
            Description = reader.GetStringOrDefault("Description"),
            Type = reader.GetStringOrDefault("Type", "7z"),
            Grading = reader.GetStringOrDefault("Grading", "G"),
            TagsJson = reader.GetStringOrDefault("Tags", "[]"),
            DisablePreview = reader.GetBoolFromInt("DisablePreview"),
            CreatedAt = reader.GetDateTimeOrDefault("CreatedAt"),
            UpdatedAt = reader.GetDateTimeOrDefault("UpdatedAt"),
            Metadata = reader.GetStringOrNull("Metadata")
        };
    }
}
```

---

## 🔄 Updated Service Pattern

Services now:
1. Call repositories (get entities)
2. Map entities to domain models
3. Populate computed properties
4. Return domain models to facades

**Example:** `ModService.cs`

```csharp
public class ModService : IModService
{
    private readonly IModRepository _repository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ITagRepository _tagRepository;
    private readonly IProfilePathService _profilePaths;
    private readonly ILogHelper _logger;

    public async Task<ModInfo?> GetByIdAsync(string sha)
    {
        // 1. Get entity from repository
        var entity = await _repository.GetByIdAsync(sha);
        if (entity == null)
            return null;

        // 2. Map entity to domain model
        var modInfo = ModMapper.ToDomain(entity);

        // 3. Populate computed properties
        await EnrichModInfoAsync(modInfo);

        return modInfo;
    }

    public async Task<List<ModInfo>> GetAllAsync()
    {
        // 1. Get entities from repository
        var entities = await _repository.GetAllAsync();

        // 2. Map to domain models
        var mods = entities.Select(ModMapper.ToDomain).ToList();

        // 3. Enrich with computed properties (batch operation)
        await EnrichModInfoBatchAsync(mods);

        return mods;
    }

    public async Task<ModInfo> CreateAsync(ModInfo modInfo)
    {
        // 1. Map domain model to entity
        var entity = ModMapper.ToEntity(modInfo);

        // 2. Insert entity via repository
        var insertedEntity = await _repository.InsertAsync(entity);

        // 3. Map back to domain model
        var result = ModMapper.ToDomain(insertedEntity);

        // 4. Populate computed properties
        await EnrichModInfoAsync(result);

        return result;
    }

    public async Task<bool> UpdateAsync(ModInfo modInfo)
    {
        // 1. Map domain model to entity
        var entity = ModMapper.ToEntity(modInfo);

        // 2. Update via repository
        return await _repository.UpdateAsync(entity);
    }

    /// <summary>
    /// Enrich single mod with computed properties
    /// </summary>
    private async Task EnrichModInfoAsync(ModInfo modInfo)
    {
        // Populate CategoryName from Categories table
        if (!string.IsNullOrEmpty(modInfo.Category))
        {
            var category = await _categoryRepository.GetByIdAsync(modInfo.Category);
            modInfo.CategoryName = category?.Name ?? string.Empty;
        }

        // Populate TagsWithMetadata from Tags table
        if (modInfo.Tags.Any())
        {
            modInfo.TagsWithMetadata = await _tagRepository.GetByNamesAsync(modInfo.Tags);
        }

        // Populate file system status
        var cachePath = _profilePaths.GetModCachePath(modInfo.SHA);
        modInfo.IsLoaded = Directory.Exists(cachePath) && !Path.GetFileName(cachePath).StartsWith("DISABLED-");
        modInfo.HasCache = Directory.Exists(cachePath);
        modInfo.CachePath = Directory.Exists(cachePath) ? cachePath : null;

        var archivePath = _profilePaths.GetModArchivePath(modInfo.SHA);
        modInfo.IsAvailable = File.Exists(archivePath);
        modInfo.ArchiveFolderPath = File.Exists(archivePath) ? Path.GetDirectoryName(archivePath) : null;

        var previewPath = _profilePaths.GetModPreviewPath(modInfo.SHA);
        modInfo.HasPreviewFolder = Directory.Exists(previewPath);
        modInfo.PreviewFolderPath = Directory.Exists(previewPath) ? previewPath : null;

        modInfo.IsOrphaned = false; // Set by orphan detection service
    }

    /// <summary>
    /// Enrich multiple mods with computed properties (optimized batch operation)
    /// </summary>
    private async Task EnrichModInfoBatchAsync(List<ModInfo> mods)
    {
        // Batch load categories
        var categoryIds = mods.Select(m => m.Category).Distinct().ToList();
        var categories = await _categoryRepository.GetByIdsAsync(categoryIds);
        var categoryDict = categories.ToDictionary(c => c.Id, c => c.Name);

        // Batch load tags
        var allTags = mods.SelectMany(m => m.Tags).Distinct().ToList();
        var tagsWithMetadata = await _tagRepository.GetByNamesAsync(allTags);
        var tagDict = tagsWithMetadata.ToDictionary(t => t.Name);

        // Enrich each mod
        foreach (var mod in mods)
        {
            // Set category name
            if (categoryDict.TryGetValue(mod.Category, out var categoryName))
            {
                mod.CategoryName = categoryName;
            }

            // Set tags with metadata
            mod.TagsWithMetadata = mod.Tags
                .Where(tagName => tagDict.ContainsKey(tagName))
                .Select(tagName => tagDict[tagName])
                .ToList();

            // Set file system status
            var cachePath = _profilePaths.GetModCachePath(mod.SHA);
            mod.IsLoaded = Directory.Exists(cachePath) && !Path.GetFileName(cachePath).StartsWith("DISABLED-");
            mod.HasCache = Directory.Exists(cachePath);
            mod.CachePath = Directory.Exists(cachePath) ? cachePath : null;

            var archivePath = _profilePaths.GetModArchivePath(mod.SHA);
            mod.IsAvailable = File.Exists(archivePath);
            mod.ArchiveFolderPath = File.Exists(archivePath) ? Path.GetDirectoryName(archivePath) : null;

            var previewPath = _profilePaths.GetModPreviewPath(mod.SHA);
            mod.HasPreviewFolder = Directory.Exists(previewPath);
            mod.PreviewFolderPath = Directory.Exists(previewPath) ? previewPath : null;

            mod.IsOrphaned = false;
        }
    }
}
```

---

## 📁 New Directory Structure

```
Modules/
├── Mod/
│   ├── Entities/              # NEW - Database entities
│   │   └── ModEntity.cs       # Pure database model
│   ├── Models/                # EXISTING - Domain models
│   │   ├── ModInfo.cs         # Rich domain model
│   │   └── Tag.cs
│   ├── Mappers/               # NEW - Entity ↔ Domain converters
│   │   └── ModMapper.cs       # Conversion logic
│   ├── Services/
│   │   ├── ModRepository.cs   # Works with ModEntity
│   │   └── ModService.cs      # Works with ModInfo
│   └── ModFacade.cs
│
├── Category/
│   ├── Entities/              # NEW
│   │   └── CategoryEntity.cs
│   ├── Models/                # EXISTING
│   │   └── CategoryInfo.cs
│   ├── Mappers/               # NEW
│   │   └── CategoryMapper.cs
│   └── Services/
│
├── Workflow/
│   ├── Entities/              # NEW
│   │   └── WorkflowEntity.cs
│   ├── Models/                # EXISTING
│   │   └── WorkflowInfo.cs
│   ├── Mappers/               # NEW
│   │   └── WorkflowMapper.cs
│   └── Repositories/
│
└── Core/
    ├── Data/                  # NEW - Base repository
    │   └── BaseRepository.cs
    └── Utilities/
        ├── DataReaderExtensions.cs  # NEW
        └── JsonHelper.cs            # EXISTING
```

---

## 📋 Implementation Steps

### Phase 1: Create Infrastructure (Week 1)
1. ✅ Create `Core/Data/BaseRepository<TEntity>`
2. ✅ Create `Core/Utilities/DataReaderExtensions`
3. ✅ Add unit tests for BaseRepository
4. ✅ Add unit tests for DataReaderExtensions

### Phase 2: Mod Module Refactoring (Week 2)
5. ✅ Create `Mod/Entities/ModEntity.cs`
6. ✅ Create `Mod/Mappers/ModMapper.cs`
7. ✅ Refactor `ModRepository` to use `BaseRepository<ModEntity>`
8. ✅ Update `ModService` to use mapper and enrich domain models
9. ✅ Run integration tests for Mod module
10. ✅ Verify mod CRUD operations work correctly

### Phase 3: Category Module Refactoring (Week 2)
11. ✅ Create `Category/Entities/CategoryEntity.cs`
12. ✅ Create `Category/Mappers/CategoryMapper.cs`
13. ✅ Refactor `CategoryRepository` to use `BaseRepository<CategoryEntity>`
14. ✅ Update `CategoryService` to use mapper
15. ✅ Run integration tests for Category module

### Phase 4: Workflow Module Refactoring (Week 3)
16. ✅ Create `Workflow/Entities/WorkflowEntity.cs`
17. ✅ Create `Workflow/Mappers/WorkflowMapper.cs`
18. ✅ Refactor `WorkflowRepository` to use `BaseRepository<WorkflowEntity>`
19. ✅ Update `WorkflowService` to use mapper
20. ✅ Run integration tests for Workflow module

### Phase 5: Remaining Modules (Week 3-4)
21. ✅ Refactor Tag repository/models
22. ✅ Refactor Profile repository/models
23. ✅ Refactor Setting repository/models
24. ✅ Run full test suite

### Phase 6: Documentation & Cleanup (Week 4)
25. ✅ Update AI_GUIDE.md with entity-domain pattern
26. ✅ Document mapper usage
27. ✅ Create migration guide for developers
28. ✅ Remove obsolete comments about "populated dynamically"

---

## 🎯 Benefits of Entity-Domain Separation

### 1. **Clear Separation of Concerns**
- Database layer only knows about entities (what's stored)
- Business layer only knows about domain models (what's used)
- No confusion about data sources

### 2. **Improved Testability**
- Mock repositories with entities
- Test mappers independently
- Test enrichment logic separately

### 3. **Database Schema Independence**
- Change database schema → update entities only
- Domain models remain stable
- Frontend unchanged

### 4. **Performance Optimization**
- Batch load computed properties when needed
- Skip enrichment for simple queries
- Lazy loading for expensive operations

### 5. **Type Safety**
- Entity properties map 1:1 to DB columns
- Domain properties clearly marked as computed
- Compile-time verification

---

## ⚠️ Breaking Changes

### For Developers

**Before:**
```csharp
// Repository returns ModInfo directly
var mod = await _repository.GetByIdAsync(sha);
Console.WriteLine(mod.IsLoaded);  // Worked (but unclear if from DB or computed)
```

**After:**
```csharp
// Repository returns ModEntity
var entity = await _repository.GetByIdAsync(sha);
// Must map to domain model
var mod = ModMapper.ToDomain(entity);
// Must explicitly enrich
await _service.EnrichModInfoAsync(mod);
Console.WriteLine(mod.IsLoaded);  // Clear that it's computed
```

### Migration Guide

1. **Repository calls**: Change return type from `ModInfo` to `ModEntity`
2. **Service calls**: Add mapper calls: `ModMapper.ToDomain(entity)`
3. **Enrichment**: Add explicit enrichment calls where needed
4. **Database writes**: Use `ModMapper.ToEntity(domainModel)`

---

## 🔍 Example: Before vs After

### Before (Mixed Model)

```csharp
// ModRepository.cs - returns ModInfo with mixed properties
public async Task<ModInfo?> GetByIdAsync(string sha)
{
    await using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync();

    var command = connection.CreateCommand();
    command.CommandText = "SELECT * FROM Mods WHERE SHA = @sha";
    command.Parameters.AddWithValue("@sha", sha);

    await using var reader = await command.ExecuteReaderAsync();
    if (await reader.ReadAsync())
    {
        return new ModInfo
        {
            SHA = reader.GetString("SHA"),
            Name = reader.GetString("Name"),
            // ... database properties
            IsLoaded = false,  // ❌ Unclear: not from database, set to default
            CategoryName = string.Empty  // ❌ Unclear: needs join, set to default
        };
    }
    return null;
}

// ModService.cs - must "fix up" properties after getting from repository
public async Task<ModInfo?> GetModAsync(string sha)
{
    var mod = await _repository.GetByIdAsync(sha);
    if (mod == null) return null;

    // ❌ "Fixing up" properties that should have been computed
    mod.IsLoaded = CheckIfLoaded(sha);
    mod.CategoryName = await GetCategoryName(mod.Category);

    return mod;
}
```

### After (Separated Models)

```csharp
// ModRepository.cs - returns ModEntity (pure database)
public async Task<ModEntity?> GetByIdAsync(string sha)
{
    return await ExecuteQuerySingleAsync(
        "SELECT * FROM Mods WHERE SHA = @sha",
        cmd => cmd.Parameters.AddWithValue("@sha", sha)
    );
}

protected override ModEntity MapToEntity(SqliteDataReader reader)
{
    return new ModEntity
    {
        SHA = reader.GetStringOrDefault("SHA"),
        Name = reader.GetStringOrDefault("Name"),
        CategoryId = reader.GetStringOrDefault("Category"),
        // Only database properties - clear and explicit
    };
}

// ModService.cs - clear conversion and enrichment
public async Task<ModInfo?> GetModAsync(string sha)
{
    // 1. Get entity from database
    var entity = await _repository.GetByIdAsync(sha);
    if (entity == null) return null;

    // 2. ✅ Map to domain model
    var mod = ModMapper.ToDomain(entity);

    // 3. ✅ Enrich with computed properties
    await EnrichModInfoAsync(mod);

    return mod;
}

private async Task EnrichModInfoAsync(ModInfo mod)
{
    // ✅ Clear: these are computed properties
    mod.IsLoaded = CheckIfLoaded(mod.SHA);
    mod.CategoryName = await GetCategoryName(mod.Category);
    mod.CachePath = GetCachePath(mod.SHA);
}
```

---

## 📚 Related Documents

- [REFACTORING_PLAN.md](./REFACTORING_PLAN.md) - Overall refactoring strategy
- [AI_GUIDE.md](./AI_GUIDE.md) - Development guidelines
- [DESIGN_DECISIONS.md](./core/DESIGN_DECISIONS.md) - Architecture decisions

---

**Document Version:** 1.0
**Last Updated:** 2026-03-09
**Status:** Ready for Review

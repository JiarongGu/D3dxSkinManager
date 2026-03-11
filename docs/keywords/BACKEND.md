# Backend Keywords Index

> **Purpose:** Backend C# classes, services, and architecture (.NET 10)
> **Parent Index:** [KEYWORDS_INDEX.md](../KEYWORDS_INDEX.md)

**Framework:** net10.0-windows
**Last Updated:** 2026-03-02

---

## Entry Point

- **Program** → `D3dxSkinManager/Program.cs`
  - Main method → Entry point for application
  - Uses ApplicationBootstrapper for initialization

## Infrastructure Layer (WebView2 Architecture)

- **ApplicationBootstrapper** → `D3dxSkinManager/Infrastructure/ApplicationBootstrapper.cs`
  - Application initialization and DI setup
  - Creates ApplicationHost with WebView2

- **ApplicationHost** → `D3dxSkinManager/Infrastructure/ApplicationHost.cs`
  - WinForms main form with WebView2 control
  - Manages window state and lifecycle
  - Loads window icon from embedded resource (favicon.ico)

- **WebViewInitializer** → `D3dxSkinManager/Infrastructure/WebViewInitializer.cs`
  - WebView2 environment setup
  - Custom scheme handler registration
  - GPU acceleration settings

- **IpcCommunicationHandler** → `D3dxSkinManager/Infrastructure/IpcCommunicationHandler.cs`
  - Handles WebView2 ↔ C# IPC messages
  - PostWebMessageAsString / WebMessageReceived

- **MessageDispatcher** → `D3dxSkinManager/Infrastructure/MessageDispatcher.cs`
  - Middleware pipeline for message processing
  - Lazy<T> caching for performance

- **ProfileServiceRouter** → `D3dxSkinManager/Infrastructure/ProfileServiceRouter.cs`
  - Profile-scoped service providers
  - Routes messages to profile-specific services

- **ServiceContainer** → `D3dxSkinManager/Infrastructure/ServiceContainer.cs`
  - Global DI container setup
  - Service registration

---

## Modules

### Core Module

#### Models

- **ErrorCodes** → `Modules/Core/Models/ErrorCodes.cs`
  - Standard error codes for application-wide error handling
  - MOD_FOLDER_IN_USE, MOD_ARCHIVE_NOT_FOUND, MOD_NOT_FOUND, etc.
  - Created: 2026-02-21

- **OperationException** → `Modules/Core/Exceptions/OperationException.cs`
  - Unified exception for all operations with structured error information
  - Properties: Code, Parameters (Dictionary<string, string>)
  - Serializes to JSON: `{ "code": "ERROR_CODE", "parameters": {...} }`
  - Frontend uses pattern: `errors.{Code}` for i18n lookup
  - Created: 2026-03-09 (replaces ModException and WorkflowException)


#### Utilities

- **LruCache<TKey,TValue>** → `Modules/Core/Utilities/LruCache.cs`
  - Thread-safe LRU cache with capacity limit
  - Automatic eviction of least recently used items
  - ReaderWriterLockSlim for concurrent access
  - Used by CustomSchemeHandler for path caching
  - Created: 2026-02-22

#### Services

- **FileService** → `Modules/Core/Services/FileService.cs:24`
  - CalculateSha256Async → `:26-48`
  - ExtractArchiveAsync → `:51-90`
  - CopyDirectoryAsync → `:93-120`
  - DeleteDirectoryAsync → `:123-137`
  - Is7ZipAvailable → `:140-142`
  - Get7ZipPath → `:145-165`

- **ImageService** → `Modules/Core/Services/ImageService.cs:26`
  - GetThumbnailPathAsync → `:72-85`
  - GetPreviewPathsAsync → `:87-107` (scans previews/{ID}/ folder)
  - GenerateThumbnailAsync → `:110-167`
  - GeneratePreviewsAsync → `:169-246` (creates per-mod preview folders)
  - CacheImageAsync → `:249-277`
  - ResizeImageAsync → `:280-314`
  - ClearModCacheAsync → `:316-357`
  - GetSupportedImageExtensions → `:360-366`
  - GetImageAsDataUriAsync → `:368-394`
  - GetThumbnailAsDataUriAsync → `:396-400`
  - GetPreviewsAsDataUriAsync → `:402-414`

- **GlobalPathService** → `Modules/Core/Services/GlobalPathService.cs`
  - Global path resolution for application directories

- **LogHelper** → `Modules/Core/Services/LogHelper.cs`
  - Centralized logging infrastructure

- **FileSystemService** → `Modules/Core/Services/FileSystemService.cs`
  - File system abstraction layer

- **ProcessService** → `Modules/Core/Services/ProcessService.cs`
  - Process management and execution

- **ImageService** → `Modules/Core/Services/ImageService.cs`
  - Image processing (thumbnails, resizing, caching)
  - Note: Image serving now handled by CustomSchemeHandler

- **CustomSchemeHandler** → `Modules/Core/Services/CustomSchemeHandler.cs`
  - Handles custom `app://` scheme requests for serving local files
  - URL format: `app://encoded_file_path`
  - Security check removed 2026-02-21 (safe for desktop app)
  - Registered as singleton via DI in CoreServiceExtensions
  - Interface: ICustomSchemeHandler
  - Created: 2026-02-20

- **FileTransferService** → `Modules/Core/Services/FileTransferService.cs`
  - Reusable service for copying files with SHA-256 deduplication
  - CopyToManagedDirectoryAsync - copies file with hash-based naming
  - IsExternalToDirectory - checks if file is outside target directory
  - Automatic collision prevention through SHA-256 naming
  - Returns relative paths for database storage
  - Interface: IFileTransferService
  - Created: 2026-02-21

- **HashService** → `Modules/Core/Services/HashService.cs`
  - SHA-256 hash calculation for files
  - CalculateFileSHA256Async - computes hash from file stream
  - Used by FileTransferService for deduplication
  - Interface: IHashService
  - Created: 2026-02-21

- **OperationNotificationService** → `Modules/Core/Services/OperationNotificationService.cs`
  - Manages active operations and emits progress notifications
  - Interface: IOperationNotificationService
  - CreateOperation() returns IProgressReporter for tracking progress
  - Event-driven push notifications to frontend via IPC
  - Thread-safe with ConcurrentDictionary
  - Created: 2026-02-21

- **IProgressReporter** → `Modules/Core/Services/IProgressReporter.cs`
  - Interface for operations to report progress (0-100%)
  - ReportProgressAsync, ReportCompletionAsync, ReportFailureAsync
  - NullProgressReporter for operations that don't need tracking
  - Created: 2026-02-21

- **IEagerLoadingService** → `Modules/Core/Services/IEagerLoadingService.cs`
  - Service for eager loading operations during application startup
  - EagerLoadAsync() - performs database initialization and profile loading
  - Updates splash screen with progress status
  - Non-critical - failures are logged but don't crash the app
  - Created: 2026-03-04

- **EagerLoadingService** → `Modules/Core/Services/EagerLoadingService.cs`
  - Implementation of eager loading service
  - Initializes database connections early
  - Loads active profile information
  - Pre-warms profile-scoped caches via MessageDispatcher
  - Category tree generation (60%) - sends CATEGORY.GET_TREE message
  - Mod statistics loading (80%) - sends MOD.GET_ALL message
  - Reports progress to splash screen via IProgress
  - Operations: Database init (10%), Active profile load (30%), Category tree (60%), Mod stats (80%), Complete (100%)
  - Uses MessageDispatcher to route through ProfileServiceRouter for profile-scoped operations
  - Created: 2026-03-04
  - Updated: 2026-03-04 - Added profile-scoped cache warming via MessageDispatcher

- **PathCache** → `Modules/Core/Services/PathCache.cs`
  - IMemoryCache implementation with size-limited LRU eviction
  - Used by CustomSchemeHandler for caching normalized file paths
  - Size limit: 500 cached paths with 25% compaction on overflow
  - Interface: IPathCache (extends IMemoryCache)
  - Created: 2026

#### IMemoryCache Usage Pattern

**General Pattern** (used by CategoryService, ModQueryService, etc.):
```csharp
public class SomeService {
    private readonly IMemoryCache _cache;
    private readonly string _cacheKey;

    public SomeService(IMemoryCache cache, IProfileContext profileContext) {
        _cache = cache;
        // Use profile-specific cache key since IMemoryCache is shared across profiles
        _cacheKey = $"CacheName_{profileContext.ProfileId}";

        // Subscribe to events to invalidate cache
        _eventBus.Subscribe(ModuleNames.MOD, ModEvents.CACHE_CHANGED, _ => {
            _cache.Remove(_cacheKey);
            return Task.CompletedTask;
        });
    }

    public async Task<List<Data>> GetDataAsync() {
        // Check cache first
        if (_cache.TryGetValue(_cacheKey, out List<Data>? cached))
            return cached!;

        // Cache miss - build list
        var data = await BuildDataAsync();

        // Cache result
        _cache.Set(_cacheKey, data);
        return data;
    }
}
```

**Key Points**:
- Always use profile-specific cache keys (IMemoryCache is singleton)
- Subscribe to relevant events to invalidate cache
- Use `_cache.Remove(key)` or `_cache.Set(key, newValue)` to invalidate/update

#### Helpers

- **ArchiveHelper** → `Modules/Core/Helpers/ArchiveHelper.cs`
  - Archive extraction and compression using **SharpSevenZip** with native 7z.dll (~10x faster than SharpCompress)
  - **7z.dll Initialization** → Automatic architecture-specific DLL loading (x64/x86) on first use
  - **DetectArchiveTypeAsync** → Async wrapper for archive type detection
  - **DetectArchiveType** → Synchronous archive type detection (7z, zip, rar, tar, etc.)
  - **ExtractArchiveAsync** → Async wrapper for archive extraction using native 7z.dll
  - **ExtractArchive** → Synchronous archive extraction with SharpSevenZipExtractor
  - **CompressDirectoryAsync** → Create 7z archives with configurable compression level
  - **ValidateArchiveAsync** → Check if archive is valid and detect password protection
  - **Performance**: Native 7z.dll provides ~10x faster extraction for LZMA/7z archives vs pure managed code
  - **Native DLL**: Requires `libs/7z.dll` (architecture-specific, copied during build from libs/x64/ or libs/x86/)
  - Created: 2024 | Updated: 2026-03-05 (migrated to SharpSevenZip, added native DLL initialization)

#### Utilities

- **FileUtilities** → `Modules/Core/Utilities/FileUtilities.cs`
- **JsonHelper** → `Modules/Core/Utilities/JsonHelper.cs`
- **ValidationHelper** → `Modules/Core/Utilities/ValidationHelper.cs`

#### Facades

- **BaseFacade** → `Modules/Core/Facades/BaseFacade.cs`
  - Base class for all module facades

---

### Mods Module

#### Facade

- **ModFacade** → `Modules/Mods/ModFacade.cs:14`
  - Constructor (DI) → `:69-88`
  - RouteMessageAsync → `:93-124` (IPC message routing)
  - GetAllModsAsync → `:128-132`
  - PopulateCategoryNamesBulkAsync → `:956-996` (Maps classification IDs to names)
  - LoadModAsync → `:140-149`
  - UnloadModAsync → `:151-160`
  - ImportModAsync → `:167-177`
  - SearchModsAsync → `:217-222`
  - GetLoadedModsAsync → `:162-165`
  - DeleteModAsync → `:179-194`
  - UpdateMetadataAsync → `:228-246`
  - UpdateCategoryAsync → `:248-262` (NEW: Drag-and-drop support)
  - GetClassificationTreeAsync → `:424-427`
  - CreateClassificationNodeAsync - IPC handler with duplicate validation
  - UpdateClassificationNodeAsync - IPC handler for name updates
  - DeleteClassificationNodeAsync - IPC handler with thumbnail cleanup
  - CheckClassificationNodeExistsAsync - IPC handler for form validation
  - Updated: 2026-02-21 (classification IPC handlers, validation)

#### Services

**Service Architecture (Updated 2026-03-07):**
- **Lifecycle Layer**: Business logic + event emission (ModLifecycleService)
- **Operation Layer**: Pure file operations, no business logic (ModArchiveService, ModCacheService)
- **Query Layer**: Data retrieval + enrichment (ModQueryService, ModEnrichmentService)
- **Event Layer**: Event consolidation for frontend (ModListEventHandler, CategoryTreeEventHandler)

**Core Services:**

- **ModLifecycleService** → `Modules/Mod/Services/ModLifecycleService.cs` (NEW - 284 lines)
  - **Purpose**: Business logic for mod load/unload with category conflict resolution
  - LoadAsync → Load mod, handle category conflicts, auto-import previews
  - UnloadAsync → Unload mod
  - **Responsibilities**:
    - Category conflict resolution (one loaded mod per category)
    - Coordinates archive extraction via ModArchiveService
    - Coordinates cache enable/disable via ModCacheService
    - Emits LOADED/UNLOADED events after successful operations
  - **Pattern**: Injects IProfileEventBus, emits events on completion
  - Created: 2026-03-06 (refactored from ModFileService)

- **ModArchiveService** → `Modules/Mod/Services/ModArchiveService.cs` (NEW - 206 lines)
  - **Purpose**: Pure archive file operations (no business logic, no events)
  - ExtractAsync → Extract archive to directory
  - DeleteArchiveAsync → Delete archive file
  - CopyArchiveAsync → Copy archive to mod storage
  - ArchiveExists → Check archive presence
  - GetArchivePath → Get archive file path
  - **Pattern**: No EventBus injection, no event emission, pure operations
  - Created: 2026-03-06 (refactored from ModFileService)

- **ModCacheService** → `Modules/Mod/Services/ModCacheService.cs` (NEW - 408 lines)
  - **Purpose**: Cache directory management (no business logic, no events)
  - EnableCacheAsync → Rename `DISABLED-{ID}` → `{ID}`
  - DisableCacheAsync → Rename `{ID}` → `DISABLED-{ID}`
  - DeleteCacheAsync → Delete cache directory
  - ScanCacheAsync → Scan all cache directories
  - CleanCacheAsync → Clean orphaned/invalid caches
  - GetCachePath → Get cache path (checks both active and disabled)
  - **Pattern**: No EventBus injection, no event emission, pure operations
  - **Bug Fix**: Now checks both loaded and disabled caches (was only checking disabled)
  - Created: 2026-03-06 (refactored from ModFileService)

- **ModEnrichmentService** → `Modules/Mod/Services/ModEnrichmentService.cs` (NEW - 172 lines)
  - **Purpose**: Enrich mod data with status flags and metadata
  - PopulateStatusFlags → Set IsLoaded, HasCache, IsAvailable by scanning directories
  - PopulateCategoryNames → Resolve category IDs to names
  - PopulateTagMetadata → Enrich tag information
  - EnrichAsync / EnrichAllAsync → Full enrichment pipeline
  - **Critical**: Must be called before returning statistics (was missing, caused stats to show 0)
  - Created: 2026-03-06 (refactored from ModQueryService)

- **ModQueryService** → `Modules/Mod/Services/ModQueryService.cs`
  - GetStatisticsAsync → Returns mod statistics (now calls ModEnrichmentService.PopulateStatusFlags)
  - SearchModsAsync → Search with filters
  - GetAllAsync → Get all mods for profile
  - GetByIdAsync → Get mod by ID
  - **GetActiveModsAsync** → Cache-first scanning of active mods in cache folder
    - Uses IMemoryCache for performance (profile-scoped cache key)
    - Scans cache folder for active mods (not DISABLED-)
    - Matches with database and enriches ModInfo
    - Returns orphaned mods (in cache but not in DB) with IsOrphaned flag
    - Cache invalidated on CACHE_CHANGED event (FileSystemWatcher detects load/unload/delete)
  - **Bug Fix**: Statistics calculation now correctly populates IsLoaded flags via enrichment service
  - Created: 2026-03-06
  - Updated: 2026-03-08 - Added IMemoryCache caching for GetActiveModsAsync
  - Updated: 2026-03-06 (integrated ModEnrichmentService)

- **ModMetadataService** → `Modules/Mod/Services/ModMetadataService.cs`
  - UpdateMetadataAsync → Update mod metadata
  - UpdateCategoryAsync → Update mod category (no longer takes callbacks)
  - BatchUpdateCategoryAsync → Bulk category update for multi-select (NEW - 2026-03-05)
    - Accepts array of mod IDs
    - Auto-unloads loaded mods before category change
    - Emits CATEGORY_UPDATED event after batch completion
  - **Merged**: ModManagementService functionality merged into this service
  - **Removed**: Callback anti-patterns (no more `Func<>` parameters)
  - Updated: 2026-03-06 (service refactoring), 2026-03-05 (batch update)

- **ModTagService** → `Modules/Mod/Services/ModTagService.cs`
  - Tag management operations
  - **Renamed**: Was `TagService` (renamed for consistency with "Mod" prefix)
  - Updated: 2026-03-06

- **ModImportService** → `Modules/Mod/Services/ModImportService.cs:14`
  - Constructor → `:26-40`
  - ImportAsync → `:43-120` (complete import workflow)
  - ReadMetadataAsync → `:123-145`
  - GenerateNameFromDirectory → `:148-160`
  - ScanAndImportPreviewsFromFolderAsync → Auto-import previews from source folder (NEW)
  - **Feature**: Intelligent folder descent (max 5 levels), 3D texture filtering
  - Updated: 2026-03-06 (added preview auto-import)

**Event Handlers (NEW - Event Consolidation Pattern):**

- **ModListEventHandler** → `Modules/Mod/EventHandlers/ModListEventHandler.cs` (NEW)
  - **Purpose**: Consolidate 8 mod events into single MOD_LIST_UPDATED event for frontend
  - **Subscribes to**: LOADED, UNLOADED, DELETED, IMPORTED, METADATA_UPDATED, CATEGORY_UPDATED, CACHE_CHANGED, REFRESHED
  - **Emits**: MOD_LIST_UPDATED (consolidated event)
  - **Benefits**: Reduces frontend event subscriptions from 8+ to 1, prevents event storms
  - Created: 2026-03-06

- **CategoryTreeEventHandler** → `Modules/Mod/EventHandlers/CategoryTreeEventHandler.cs` (NEW)
  - **Purpose**: Consolidate category-affecting events into CATEGORY_TREE_UPDATED
  - **Subscribes to**: MOD.CATEGORY_UPDATED, MOD.IMPORTED, MOD.DELETED
  - **Emits**: CATEGORY_TREE_UPDATED (consolidated event)
  - **Invalidates**: Category tree cache on relevant changes
  - Created: 2026-03-06

**Deleted Services (Removed 2026-03-06):**
- ~~**ModFileService**~~ → Split into ModArchiveService, ModCacheService, ModLifecycleService (856 lines deleted)
- ~~**ModManagementService**~~ → Merged into ModMetadataService

**Moved to Core Module:**
- **FileOperationPlanner** → `Modules/Core/Utilities/FileOperationPlanner.cs`
  - Atomic file system operation planner with two-plan batch processing
  - **Two-Plan Model**: Processing Plan (currently executing) + Queued Plan (accumulating operations)
  - **Operations**: ExtractArchive, MoveDirectory, CopyFile, DeleteDirectory, DeleteFile
  - **Deduplication**: Identical operations are automatically merged in the Queued Plan
  - **No Cancellation**: All operations execute sequentially, no cancellation logic
  - **Background Worker**: Processes plans atomically in a dedicated thread
  - **Usage**: SubmitOperationAsync() → returns Task that completes when operation finishes
  - **Benefits**: No deadlocks, batches operations during slow extractions, prevents file conflicts
  - Created: 2026-03-05
  - Moved: 2026-03-06 (from Mod module to Core module - reusable infrastructure)

- **ModRepository** → `Modules/Mods/Services/ModRepository.cs:32`
  - Constructor → `:37-42`
  - InitializeDatabaseAsync → `:45-80`
  - GetAllAsync → `:83-105`
  - GetByIdAsync → `:108-131`
  - ExistsAsync → `:134-149`
  - InsertAsync → `:152-179`
  - UpdateAsync → `:182-207`
  - DeleteAsync → `:210-224`
  - GetByObjectNameAsync → `:227-249`
  - GetLoadedIdsAsync → `:252-269`
  - GetDistinctObjectNamesAsync → `:272-289`
  - GetDistinctAuthorsAsync → `:292-309`
  - GetAllTagsAsync → `:312-329`
  - SetLoadedStateAsync → `:332-347`

- **ClassificationService** → `Modules/Mods/Services/ClassificationService.cs:22`
  - ClassifyModAsync → `:29-65`
  - LoadRulesAsync → `:68-90`
  - GetRules → `:93`
  - AddRule → `:96`
  - SaveRulesAsync → `:99-110`
  - CreateNodeAsync - creates classification with SHA-256 thumbnail deduplication
  - DeleteNodeAsync - deletes node with thumbnail cleanup
  - NodeExistsAsync - checks if nodeId exists in database
  - UpdateNodeAsync - updates classification name
  - MoveNodeAsync - moves node to new parent
  - DeleteNodeAndChildrenRecursiveAsync - recursive deletion with thumbnail-first order
  - CleanupThumbnailIfUnusedAsync - deletes unused thumbnails with file lock detection
  - Updated: 2026-02-21 (thumbnail management, validation, file lock detection)

- **ClassificationRepository** → `Modules/Mods/Services/ClassificationRepository.cs`
  - Database access for classifications

#### Models

- **ModInfo** → `Modules/Mods/Models/ModInfo.cs:5`
  - Properties: Id, ObjectName, Name, Author, Description, Type, Grading, Tags, IsLoaded, IsAvailable, ThumbnailPath, OriginalPath, WorkPath, CachePath, Category

---

### Fluent Module (Database Migrations)

**📖 Detailed Documentation:** [architecture/DATABASE_MIGRATION_ARCHITECTURE.md](../architecture/DATABASE_MIGRATION_ARCHITECTURE.md)

> **Purpose:** Fluent API for SQLite database schema migrations
> **Design:** FluentMigrator-style with `[Migration]` attribute and Up()/Down() methods
> **Integration:** Profile-scoped, runs automatically at profile startup

#### Core Classes

- **Migration** → `Modules/Fluent/Migration.cs`
  - Base class for all migrations with fluent API
  - Properties: Create, Delete, Alter, Execute (expression roots)
  - Abstract method: Up() - forward migration
  - Virtual method: Down() - rollback migration (optional)

- **MigrationAttribute** → `Modules/Fluent/MigrationAttribute.cs`
  - Marks migration classes with version numbers (YYYYMMDDHHmm format)
  - Properties: Version (long), Description (string, optional)

#### Services

- **IDatabaseMigrationService** → `Modules/Fluent/Services/IDatabaseMigrationService.cs`
- **DatabaseMigrationService** → `Modules/Fluent/Services/DatabaseMigrationService.cs`
  - Entry point for migration system
  - RunStartupMigrationsAsync() - executes pending migrations

- **IMigrationRunner** → `Modules/Fluent/Services/IMigrationRunner.cs`
- **MigrationRunner** → `Modules/Fluent/Services/MigrationRunner.cs`
  - Discovers migrations via reflection
  - Executes migrations in version order
  - Uses single connection/transaction for atomicity
  - Profile-scoped via IProfileContext

- **IMigrationHistoryRepository** → `Modules/Fluent/Services/IMigrationHistoryRepository.cs`
- **MigrationHistoryRepository** → `Modules/Fluent/Services/MigrationHistoryRepository.cs`
  - Tracks applied migrations in `_MigrationHistory` table
  - GetAppliedVersionsAsync() - returns list of applied versions
  - RecordMigrationAsync() - records successful migration
  - Internal overloads accept existing connection/transaction to prevent SQLite deadlock

#### Fluent API Builders

- **ICreateExpressionRoot** → `Modules/Fluent/Expressions/ICreateExpressionRoot.cs`
- **CreateExpressionRoot** → `Modules/Fluent/Expressions/CreateExpressionRoot.cs`
  - Table(name) → CreateTableBuilder
  - Index(name) → CreateIndexBuilder

- **CreateTableBuilder** → `Modules/Fluent/Builders/CreateTableBuilder.cs`
  - WithColumn(name) → ColumnDefinitionBuilder
  - Complete() → generates CREATE TABLE SQL

- **CreateIndexBuilder** → `Modules/Fluent/Builders/CreateIndexBuilder.cs`
  - OnTable(name) → OnColumn(name) → Complete()
  - Supports Unique(), Ascending(), Descending()

- **AlterTableBuilder** → `Modules/Fluent/Builders/AlterTableBuilder.cs`
  - AddColumn(name) → ColumnDefinitionBuilder
  - RenameTo(newName) → Complete()

- **ColumnDefinitionBuilder** → `Modules/Fluent/Builders/ColumnDefinitionBuilder.cs`
  - Type methods: AsText(), AsInteger(), AsReal(), AsBoolean(), AsDateTime(), AsBlob()
  - Constraints: NotNullable(), PrimaryKey(), Unique(), Identity(), WithDefaultValue()

#### Base Schema Migrations

- **Migration_202603080001_CreateModsTable** → `Modules/Fluent/Migrations/Migration_202603080001_CreateModsTable.cs`
- **Migration_202603080002_CreateCategoriesTable** → `Modules/Fluent/Migrations/Migration_202603080002_CreateCategoriesTable.cs`
- **Migration_202603080003_CreateTagsTable** → `Modules/Fluent/Migrations/Migration_202603080003_CreateTagsTable.cs`
- **Migration_202603080004_CreateWorkflowsTable** → `Modules/Fluent/Migrations/Migration_202603080004_CreateWorkflowsTable.cs`

#### Integration

- **FluentServiceExtensions** → `Modules/Fluent/FluentServiceExtensions.cs`
  - AddFluentMigrationServices() - registers services as TryAddSingleton
  - Profile-scoped pattern: each profile gets its own ServiceProvider with singleton services

- **ProfileServiceRouter** → `Infrastructure/ProfileServiceRouter.cs:189`
  - Executes migrations synchronously during profile initialization
  - Uses `.GetAwaiter().GetResult()` to avoid async deadlock
  - Runs before other profile services to ensure schema is ready

#### Key Design Patterns

- **Synchronous Execution**: Migrations run synchronously to maintain thread affinity for SQLite transactions
- **Single Connection**: Migration history updates use same connection/transaction to prevent deadlock
- **Transactional**: Each migration runs in a transaction (all-or-nothing)
- **Profile-Scoped**: Each profile has its own database and migration history
- **Version-Based**: YYYYMMDDHHmm format ensures chronological ordering

#### Repository Schema Management

**IMPORTANT**: Repositories no longer contain `CREATE TABLE IF NOT EXISTS` code. All schema management is now handled by migrations.

- ~~ModRepository~~ - InitializeDatabaseAsync removed (2026-03-08)
- ~~CategoryRepository~~ - Table initialization removed (2026-03-08)
- ~~TagRepository~~ - Table initialization removed (2026-03-08)
- ~~WorkflowRepository~~ - Table initialization removed (2026-03-08)

---

### Migration Module (Python → React)

> **Key Update (2026-02-20):** Archives now stored WITHOUT extensions (matches Python format)
> **Architecture:** Step-based migration system with 6 steps
> **Documentation:** [architecture/MIGRATION_ARCHITECTURE.md](../architecture/MIGRATION_ARCHITECTURE.md)

#### Facade

- **MigrationFacade** → `Modules/Migration/MigrationFacade.cs`
  - IPC entry point for migration operations

#### Orchestrator

- **MigrationService** → `Modules/Migration/Services/MigrationService.cs:44`
  - Thin orchestrator (205 lines, down from 991)
  - AnalyzeSourceAsync → `:73`
  - MigrateAsync → `:97` (executes all steps in order)
  - ValidateMigrationAsync → `:184`

#### Migration Steps

- **IMigrationStep** → `Modules/Migration/Steps/IMigrationStep.cs:5`
- **MigrationStep1AnalyzeSource** → `Modules/Migration/Steps/MigrationStep1AnalyzeSource.cs:18`
- **MigrationStep2MigrateConfiguration** → `Modules/Migration/Steps/MigrationStep2MigrateConfiguration.cs`
- **MigrationStep3MigrateClassifications** → `Modules/Migration/Steps/MigrationStep3MigrateClassifications.cs`
- **MigrationStep4MigrateClassificationThumbnails** → `Modules/Migration/Steps/MigrationStep4MigrateClassificationThumbnails.cs`
- **MigrationStep5MigrateModArchives** → `Modules/Migration/Steps/MigrationStep5MigrateModArchives.cs:23`
  - Copies archives WITHOUT extensions → `:98`
  - DetectArchiveTypeAsync (SharpCompress) → `:166`
  - Creates mod entries using ModManagementService → `:118`
- **MigrationStep6MigrateModPreviews** → `Modules/Migration/Steps/MigrationStep6MigrateModPreviews.cs`

#### Python Parsers

- **IPythonConfigurationParser** → `Modules/Migration/Parsers/PythonConfigurationParser.cs`
- **IPythonClassificationFileParser** → `Modules/Migration/Parsers/PythonClassificationFileParser.cs`
- **IPythonRedirectionFileParser** → `Modules/Migration/Parsers/PythonRedirectionFileParser.cs`
- **IPythonModIndexParser** → `Modules/Migration/Parsers/PythonModIndexParser.cs`

#### Models

- **MigrationContext** → `Modules/Migration/Models/MigrationContext.cs`
- **MigrationOptions** → `Modules/Migration/Models/MigrationOptions.cs`
- **MigrationResult** → `Modules/Migration/Models/MigrationResult.cs`
- **MigrationProgress** → `Modules/Migration/Models/MigrationProgress.cs`
- **MigrationAnalysis** → `Modules/Migration/Models/MigrationAnalysis.cs`
- **PythonConfiguration** → `Modules/Migration/Models/PythonConfiguration.cs`

---

### Settings Module

#### Facade

- **SettingsFacade** → `Modules/Settings/SettingsFacade.cs`
  - HandleMessageAsync → `:37-79` (routes UPDATE_FIELD, GET_GLOBAL, etc.)
  - UpdateGlobalSettingHandlerAsync → `:247-255`

#### Services

- **GlobalSettingsService** → `Modules/Settings/Services/GlobalSettingsService.cs`
  - **File Location:** `data/settings/global.json`
  - **FIXED 2026-02-18:** Deadlock in UpdateSettingAsync resolved
  - GetSettingsAsync → `:41-81`
  - UpdateSettingsAsync → `:85-98`
  - UpdateSettingAsync → `:104-158` (fixed deadlock - no nested lock)
  - ResetSettingsAsync → `:163-174`

- **LanguageService** → `Modules/Settings/Services/LanguageService.cs`
  - **NEW 2026-02-21:** Language file management service
  - GetLanguageAsync(languageCode) → Load language from JSON file
  - GetAvailableLanguagesAsync() → List available language files
  - LanguageExistsAsync(languageCode) → Check if language file exists
  - SaveLanguageAsync(language) → Save language file (future feature)
  - **Language Files:** `Languages/en.json`, `Languages/cn.json` (auto-copied to `data/languages/`)
  - Created: 2026-02-21

- **SettingsFileService** → `Modules/Settings/Services/SettingsFileService.cs`
  - Generic file-based storage service

- **WindowStateService** → `Modules/Settings/Services/WindowStateService.cs`
  - Window size/position persistence service
  - LoadWindowState → Returns (width, height, x, y, maximized) tuple
  - SaveWindowState(Form) → Saves current window state
  - IsPositionValid → Validates window is visible on at least one monitor
  - Handles screen resolution changes and multi-monitor setups
  - Works with WinForms and WebView2 window management

#### Models

- **GlobalSettings** → `Modules/Settings/Models/GlobalSettings.cs`
  - Theme, AnnotationLevel, LogLevel properties
  - Window (WindowSettings) → Window state persistence (added 2026-02-20)

- **WindowSettings** → `Modules/Settings/Models/GlobalSettings.cs:37`
  - X, Y, Width, Height (nullable int) → Window position and size
  - Maximized (bool) → Window maximized state
  - Used by Program.cs for window state persistence

---

### Profile Module

#### Facade

- **ProfileFacade** → `Modules/Profiles/ProfileFacade.cs`
  - IPC entry point for profile operations

#### Services

- **ProfileService** → `Modules/Profiles/Services/ProfileService.cs`
  - Profile CRUD operations
  - Profile switching and management

- **ProfilePathService** → `Modules/Profiles/Services/ProfilePathService.cs`
  - Profile-specific path resolution
  - Per-profile data directory management

- **ProfileServiceProvider** → `Modules/Profiles/Services/ProfileServiceProvider.cs`
  - Service provider for profile-scoped services

- **ProfileServerService** → `Modules/Profiles/ProfileServerService.cs`
  - Profile server coordination

#### Models

- **Profile** → `Modules/Profiles/Models/Profile.cs`
  - Profile data model

---

### Plugins Module

> **Architecture:** External plugin system with dynamic loading
> **Plugin Location:** `Plugins/` directory (27 external projects)
> **Infrastructure:** `Modules/Plugins/Services/`

#### Facade

- **PluginsFacade** → `Modules/Plugins/PluginsFacade.cs`
  - Handles plugin-related IPC messages

#### Services

- **PluginLoader** → `Modules/Plugins/Services/PluginLoader.cs`
  - Loads plugins from plugins directory
  - Constructor requires: pluginsPath, registry, services, logger

- **PluginRegistry** → `Modules/Plugins/Services/PluginRegistry.cs`
  - Registry of loaded plugins

- **PluginEventBus** → `Modules/Plugins/Services/PluginEventBus.cs`
  - Event bus for plugin communication
  - EmitAsync (virtual for mocking) → `:45`

- **PluginContext** → `Modules/Plugins/Services/PluginContext.cs`
  - Context for plugin execution

#### Interfaces

- **IPlugin** → `Modules/Plugins/Services/IPlugin.cs`
  - Base plugin interface
  - Properties: Id, Name, Version, Author, Description

- **IServicePlugin** → `Modules/Plugins/Services/IServicePlugin.cs`
  - Interface for plugins that provide services

- **IMessageHandlerPlugin** → `Modules/Plugins/Services/IMessageHandlerPlugin.cs`
  - Interface for plugins that handle IPC messages

- **IPluginContext** → `Modules/Plugins/Services/IPluginContext.cs`

#### Models

- **PluginInfo** → `Modules/Plugins/Models/PluginInfo.cs`
  - DTO for plugin information (IPC)

#### External Plugins

Located in `Plugins/` directory (external to backend):
- ScreenCapture, BatchProcessingTools, CacheClearup, etc. (27 projects)
- **Namespace:** All use `D3dxSkinManager.Modules.Plugins.Services` for infrastructure
- **Target Framework:** net8.0-windows

---

### Launch Module

#### Facade

- **LaunchFacade** → `Modules/Launch/LaunchFacade.cs`
  - IPC entry point for game launch operations

#### Services

- **D3DMigotoService** → `Modules/Launch/Services/D3DMigotoService.cs`
  - Game launch with 3DMigoto integration
  - Unity game launch configuration

---

### Tools Module

#### Facade

- **ToolsFacade** → `Modules/Tools/ToolsFacade.cs`
  - IPC entry point for utility operations

#### Services

- **ConfigurationService** → `Modules/Tools/Services/ConfigurationService.cs`
  - Configuration management utilities

- **ModAutoDetectionService** → `Modules/Tools/Services/ModAutoDetectionService.cs`
  - Automatic mod detection

- **StartupValidationService** → `Modules/Tools/Services/StartupValidationService.cs`
  - Application startup validation

---

### Workflow Module

**📖 Detailed Documentation:** [architecture/WORKFLOW_ARCHITECTURE.md](../architecture/WORKFLOW_ARCHITECTURE.md)

#### Facade

- **WorkflowFacade** → `Modules/Workflow/WorkflowFacade.cs`
  - IPC routing for workflow operations
  - Handles GET_WORKFLOW, START_MOD_IMPORT, PROVIDE_METADATA, CANCEL_MOD_IMPORT
  - Batch operations: BATCH_DELETE_WORKFLOWS, BATCH_RESUME_WORKFLOWS

#### Repositories

- **IWorkflowRepository** → `Modules/Workflow/Repositories/IWorkflowRepository.cs`
- **WorkflowRepository** → `Modules/Workflow/Repositories/WorkflowRepository.cs`
  - SQLite-based workflow CRUD operations with EF Core
  - Batch operations: DeleteBatchAsync, GetByIdsAsync (parameterized SQL IN clauses)

#### Handlers

- **ModImportWorkflowHandler** → `Modules/Workflow/Handlers/ModImportWorkflowHandler.cs`
  - Handles MOD_IMPORT workflow type
  - 3-step state machine: compress → wait for metadata → import

#### Models

- **WorkflowInfo** → `Modules/Workflow/Models/WorkflowInfo.cs`
  - Workflow metadata, status, context JSON, timestamps

- **WorkflowStatus** → `Modules/Workflow/Entities/WorkflowEntity.cs`
  - Enum: Pending, Processing, WaitingForInput, Completed, Failed, Cancelled

- **ModImportWorkflowContext** → `Modules/Workflow/Models/ModImportWorkflowContext.cs`
  - Context type for MOD_IMPORT workflow with step-based progression

#### Events

- **WorkflowEvents** → `Modules/Workflow/WorkflowEvents.cs`
  - CREATED, STATUS_CHANGED, COMPLETED, FAILED, CANCELLED
  - EventBusIpcBridge automatically forwards all events via wildcard subscription

---

## Shared Models

- **MessageRequest** → `Models/MessageRequest.cs:3`
  - Properties: Id, Type, Payload

- **MessageResponse** → `Models/MessageResponse.cs:3`
  - Properties: Id, Success, Data, Error, ErrorDetails
  - ErrorDetails contains errorCode and data for frontend error handling (added 2026-02-21)

---

## Database

- **SQLite Connection** → `Modules/Mods/Services/ModRepository.cs:37`
- **Mods Table Schema** → `Modules/Mods/Services/ModRepository.cs:49-78`

---

## Configuration & DI

- **ServiceRouter** → `ServiceRouter.cs`
  - Routes IPC messages to appropriate facades

- **CoreServiceExtensions** → `Modules/Core/CoreServiceExtensions.cs`
  - DI registration for Core module

- **ModsServiceExtensions** → Similar pattern for each module
  - Each module has its own ServiceExtensions class

---

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.Web.WebView2.WinForms | 1.0.3800.47 | WebView2 control for WinForms |
| System.Windows.Forms | 10.0.3 | WinForms desktop framework |
| Microsoft.Data.Sqlite | 10.0.3 | SQLite database |
| Newtonsoft.Json | 13.0.4 | JSON serialization |
| Microsoft.Extensions.DependencyInjection | 10.0.3 | DI container |
| System.Drawing.Common | 10.0.3 | Image processing |
| SharpSevenZip | Latest | Archive extraction/compression with native 7z.dll (~10x faster) |
| SixLabors.ImageSharp | 3.1.12 | Image processing |
| Costura.Fody | 6.0.0 | Single-file deployment (embeds managed DLLs) |
| xUnit | Latest | Unit testing |
| Moq | 4.20.73 | Mocking |
| FluentAssertions | 7.0.1 | Test assertions |

**Native Libraries:**
- `libs/7z.dll` - Official 7-Zip DLL (architecture-specific: x64/x86) for fast archive operations

---

## Naming Conventions

- **PascalCase** for files: `ModFacade.cs`, `ModRepository.cs`, `IModFacade.cs`
- **Folders:** `Modules/`, `Services/`, `Models/`, `Facades/`

---

**Line Count:** ~350 lines
**Parent:** [KEYWORDS_INDEX.md](../KEYWORDS_INDEX.md)

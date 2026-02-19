# Domain-Driven Design Architecture

**Last Updated**: 2026-02-19
**Status**: Active Design Document
**Purpose**: Define clear boundaries between modules and service responsibilities

---

## Table of Contents

1. [Design Principles](#design-principles)
2. [Module Boundaries](#module-boundaries)
3. [Service Layer Architecture](#service-layer-architecture)
4. [Dependency Rules](#dependency-rules)
5. [Anti-Patterns to Avoid](#anti-patterns-to-avoid)
6. [Refactoring Guidelines](#refactoring-guidelines)

---

## Design Principles

### 1. Single Responsibility Principle (SRP)
Each service should have **ONE reason to change**.

**Good Example:**
```csharp
// ✅ FileService - Only handles file operations
public class FileService
{
    Task<string> CalculateSha256Async(string filePath);
    Task<bool> ExtractArchiveAsync(string archivePath, string targetDirectory);
    Task<bool> CopyFileAsync(string sourceFile, string destinationFile);
}

// ✅ ImageService - Only handles image operations
public class ImageService
{
    Task<string?> SaveImageAsync(byte[] imageData, string relativePath);
    Task<bool> DeleteImageAsync(string relativePath);
    string[] GetSupportedImageExtensions();
}
```

**Bad Example:**
```csharp
// ❌ MigrationService doing everything
public class MigrationService
{
    // File operations - should use FileService
    private async Task CopyDirectoryRecursiveAsync() { ... }

    // Classification management - should use ClassificationService
    private async Task CreateClassificationNodeAsync() { ... }

    // Configuration parsing - should use ConfigurationService/Parser
    private async Task ParseConfigurationAsync() { ... }

    // Auto-detection - should use AutoDetectionService
    private async Task DetectObjectNameAsync() { ... }
}
```

---

### 2. Dependency Inversion Principle (DIP)
**Depend on abstractions (interfaces), not concrete implementations.**

**Good Example:**
```csharp
// ✅ Service depends on interface
public class MigrationService
{
    private readonly IFileService _fileService;
    private readonly IClassificationService _classificationService;

    public MigrationService(
        IFileService fileService,
        IClassificationService classificationService)
    {
        _fileService = fileService;
        _classificationService = classificationService;
    }
}
```

**Bad Example:**
```csharp
// ❌ Direct repository access bypassing service layer
public class MigrationService
{
    private readonly IClassificationRepository _repository;

    public async Task MigrateAsync()
    {
        // Bypassing service layer!
        await _repository.InsertAsync(node);
    }
}
```

---

### 3. Service Layer > Repository Layer
**Never bypass the service layer to access repositories directly.**

**Why?**
- Services provide business logic and validation
- Services handle cross-cutting concerns (logging, caching)
- Services provide a stable API while repository implementation can change
- Services can orchestrate multiple repositories

**Correct Flow:**
```
Controller/Facade → Service → Repository → Database
```

**Bad Flow:**
```
Controller/Facade → Repository → Database  ❌ BYPASSING SERVICE LAYER
```

---

### 4. Don't Repeat Yourself (DRY)
**If logic exists in a service, USE IT. Don't reimplement it.**

**Good Example:**
```csharp
// ✅ Use existing FileService
await _fileService.CopyDirectoryAsync(sourceDir, destDir);
```

**Bad Example:**
```csharp
// ❌ Reimplementing file copy logic
private async Task CopyDirectoryRecursiveAsync(string sourceDir, string destDir)
{
    Directory.CreateDirectory(destDir);
    foreach (var file in Directory.GetFiles(sourceDir))
    {
        File.Copy(file, destFile);
    }
    // ... 38 lines of duplicate code
}
```

---

## Module Boundaries

### Core Module
**Purpose**: Fundamental services used by all other modules

**Services:**
- `FileService` - File operations (copy, move, delete, hash, extract)
- `ImageService` - Image management (save, delete, supported formats)
- `LogHelper` - Centralized logging (Debug, Info, Warning, Error)
- `PathHelper` - Path resolution (absolute ↔ relative conversion)
- `PathValidator` - Path validation
- `ProcessService` - External process execution
- `FileSystemService` - File system operations
- `FileDialogService` - File/folder selection dialogs
- `ImageServerService` - Image serving for preview

**Dependencies:** None (this is the foundation)

**Used By:** All other modules

---

### Mods Module
**Purpose**: Mod management, classification, and querying

**Services:**
- `ModManagementService` - CRUD operations for mods
- `ModFileService` - Mod file operations (extract, copy, install)
- `ModImportService` - Import mods from archives
- `ModQueryService` - Query and search mods
- `ClassificationService` - Classification tree management

**Repositories:**
- `ModRepository` - Mod data access
- `ClassificationRepository` - Classification data access

**Dependencies:**
- Core Module (FileService, ImageService, LogHelper, PathHelper)

**Used By:**
- Migration Module (for mod creation)
- Frontend (through ModFacade)

**Key Rules:**
- ✅ Other services should use `ModManagementService` for mod operations
- ✅ Other services should use `ClassificationService` for classification operations
- ❌ Other services should NEVER access `ModRepository` or `ClassificationRepository` directly

---

### Profiles Module
**Purpose**: Profile management and context

**Services:**
- `ProfileService` - Profile CRUD operations
- `ProfileContext` - Current profile context (path resolution)
- `ProfileServerService` - Profile data serving

**Repositories:**
- `ProfileRepository` - Profile data access

**Dependencies:**
- Core Module (PathHelper, LogHelper)

**Used By:** All modules that need profile-specific paths

**Key Rules:**
- ✅ Inject `IProfileContext` for profile-specific path resolution
- ✅ Use `ProfileService` for profile management
- ❌ Don't hardcode profile paths

---

### Settings Module
**Purpose**: Application settings management

**Services:**
- `GlobalSettingsService` - Global application settings
- `SettingsFileService` - Settings file operations

**Dependencies:**
- Core Module (PathHelper, LogHelper)

**Used By:** All modules that need configurable settings

**Key Rules:**
- ✅ Use `GlobalSettingsService` for reading/writing settings
- ❌ Don't manually read/write settings JSON files

---

### Tools Module
**Purpose**: Utility services for configuration, validation, and auto-detection

**Services:**
- `ConfigurationService` - Application configuration management
- `StartupValidationService` - Startup validation checks
- `ModAutoDetectionService` - Auto-detect mod object names

**Dependencies:**
- Core Module (LogHelper, PathHelper)
- Profiles Module (ProfileContext)

**Used By:**
- Migration Module (for configuration migration)
- Frontend (for validation and auto-detection)

**Key Rules:**
- ✅ Use `ConfigurationService` for all config operations
- ✅ Use `ModAutoDetectionService` for auto-detection rules
- ❌ Don't manually parse/write config files

---

### Launch Module
**Purpose**: Game and 3DMigoto launcher

**Services:**
- `D3DMigotoService` - 3DMigoto integration and launching

**Dependencies:**
- Core Module (ProcessService, LogHelper)
- Tools Module (ConfigurationService)

**Used By:** Frontend (through LaunchFacade)

---

### Migration Module
**Purpose**: Migrate data from Python application to React version

**Services:**
- `MigrationService` - **ORCHESTRATION ONLY** - Coordinates migration process
- `ClassificationThumbnailService` - Thumbnail association with classifications

**Future Services (To Be Created):**
- `PythonConfigurationParser` - Parse Python configuration files
- `PythonInstallationDetector` - Auto-detect Python installation
- `PythonInstallationAnalyzer` - Analyze Python installation

**Dependencies:**
- Core Module (FileService, ImageService, LogHelper)
- Mods Module (ModManagementService, ClassificationService)
- Profiles Module (ProfileContext)
- Tools Module (ConfigurationService, ModAutoDetectionService)

**Used By:** Frontend (through MigrationFacade)

**Key Rules:**
- ✅ MigrationService should ONLY orchestrate - call other services
- ✅ Delegate domain logic to appropriate module services
- ❌ NEVER bypass service layer to access repositories
- ❌ NEVER reimplement logic that exists in other services

---

## Service Layer Architecture

### Layered Architecture

```
┌─────────────────────────────────────────────────┐
│          Frontend (React/TypeScript)            │
└─────────────────────────────────────────────────┘
                      ↓ IPC
┌─────────────────────────────────────────────────┐
│              Facades (IPC Routing)              │
│  ModFacade, ProfileFacade, SettingsFacade, etc. │
└─────────────────────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────────┐
│            Services (Business Logic)            │
│  ModManagementService, ClassificationService    │
│  FileService, ConfigurationService, etc.        │
└─────────────────────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────────┐
│         Repositories (Data Access)              │
│  ModRepository, ClassificationRepository, etc.  │
└─────────────────────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────────┐
│              Database (SQLite)                  │
└─────────────────────────────────────────────────┘
```

### Layer Responsibilities

#### Facades
- **Responsibility**: IPC message routing and payload extraction
- **What they do**:
  - Receive IPC messages from frontend
  - Extract profileId from message
  - Call appropriate service method
  - Return response to frontend
- **What they DON'T do**:
  - Business logic
  - Data validation
  - Database access

#### Services
- **Responsibility**: Business logic and orchestration
- **What they do**:
  - Implement domain logic
  - Validate data
  - Coordinate multiple repositories
  - Handle cross-cutting concerns (logging, caching)
  - Orchestrate other services
- **What they DON'T do**:
  - Direct database access (use repositories)
  - IPC message handling (that's facades)
  - File I/O (use FileService)

#### Repositories
- **Responsibility**: Data access and persistence
- **What they do**:
  - CRUD operations on database
  - Query building
  - Data mapping
- **What they DON'T do**:
  - Business logic
  - Data validation (beyond basic constraints)
  - Calling other services

---

## Dependency Rules

### Rule 1: Dependencies Flow Downward Only

```
Frontend → Facades → Services → Repositories → Database
```

**Never allow upward dependencies:**
- ❌ Repository calling a Service
- ❌ Service calling a Facade
- ❌ Database calling anything

### Rule 2: Use Interfaces for All Dependencies

```csharp
// ✅ Correct - Depend on interface
public class ModManagementService
{
    private readonly IModRepository _repository;
    private readonly IFileService _fileService;
}

// ❌ Wrong - Depend on concrete class
public class ModManagementService
{
    private readonly ModRepository _repository;
    private readonly FileService _fileService;
}
```

### Rule 3: Never Bypass the Service Layer

```csharp
// ❌ BAD - Facade calling repository directly
public class ModFacade
{
    private readonly IModRepository _repository;  // ❌ WRONG!

    public async Task<PhotinoResponse> GetMods(PhotinoMessage message)
    {
        var mods = await _repository.GetAllAsync();  // ❌ BYPASSING SERVICE
        return PhotinoResponse.Success(mods);
    }
}

// ✅ GOOD - Facade calling service
public class ModFacade
{
    private readonly IModManagementService _modService;  // ✅ CORRECT

    public async Task<PhotinoResponse> GetMods(PhotinoMessage message)
    {
        var mods = await _modService.GetAllModsAsync();  // ✅ USING SERVICE
        return PhotinoResponse.Success(mods);
    }
}
```

### Rule 4: Cross-Module Dependencies Go Through Services

```csharp
// ✅ GOOD - MigrationService using ClassificationService
public class MigrationService
{
    private readonly IClassificationService _classificationService;

    public async Task MigrateClassificationsAsync()
    {
        await _classificationService.CreateNodeAsync(...);  // ✅ USING SERVICE
    }
}

// ❌ BAD - MigrationService accessing ClassificationRepository directly
public class MigrationService
{
    private readonly IClassificationRepository _classificationRepository;

    public async Task MigrateClassificationsAsync()
    {
        await _classificationRepository.InsertAsync(...);  // ❌ BYPASSING SERVICE
    }
}
```

---

## Anti-Patterns to Avoid

### 1. God Class (Service Doing Too Much)

**Symptoms:**
- Service has 1000+ lines of code
- Service has 10+ dependencies
- Service reimplements logic from other services
- Service accesses multiple repositories directly

**Example:**
```csharp
// ❌ God Class - Doing too much
public class MigrationService
{
    // 20+ dependencies
    private readonly IModRepository _modRepository;
    private readonly IClassificationRepository _classificationRepository;
    private readonly IFileService _fileService;
    // ... 17 more dependencies

    // 1000+ lines of code
    // Reimplementing file operations
    // Reimplementing classification management
    // Reimplementing configuration parsing
    // Reimplementing auto-detection
}
```

**Solution:**
- Extract domain logic into appropriate module services
- Use orchestration pattern - delegate to services
- Keep orchestrator thin (coordination only)

---

### 2. Bypassing Service Layer

**Symptoms:**
- Facade calling repository directly
- Service A calling Service B's repository
- Multiple services duplicating repository calls

**Example:**
```csharp
// ❌ Bypassing service layer
public class MigrationService
{
    private readonly IClassificationRepository _repository;  // ❌ WRONG

    public async Task MigrateAsync()
    {
        await _repository.InsertAsync(node);  // ❌ BYPASSING SERVICE
    }
}
```

**Solution:**
```csharp
// ✅ Using service layer
public class MigrationService
{
    private readonly IClassificationService _classificationService;  // ✅ CORRECT

    public async Task MigrateAsync()
    {
        await _classificationService.CreateNodeAsync(...);  // ✅ USING SERVICE
    }
}
```

---

### 3. Logic Duplication

**Symptoms:**
- Same code appears in multiple services
- Manual file operations instead of using FileService
- Manual JSON parsing instead of using configuration service

**Example:**
```csharp
// ❌ Duplicate logic
public class ServiceA
{
    private async Task CopyFiles()
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
            File.Copy(file, destFile);
    }
}

public class ServiceB
{
    private async Task CopyFiles()
    {
        // Same code duplicated!
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
            File.Copy(file, destFile);
    }
}
```

**Solution:**
```csharp
// ✅ Use FileService
public class ServiceA
{
    private readonly IFileService _fileService;

    private async Task CopyFiles()
    {
        await _fileService.CopyDirectoryAsync(sourceDir, destDir);
    }
}

public class ServiceB
{
    private readonly IFileService _fileService;

    private async Task CopyFiles()
    {
        await _fileService.CopyDirectoryAsync(sourceDir, destDir);
    }
}
```

---

### 4. Anemic Domain Model (Service as Data Bag)

**Symptoms:**
- Service with only getters/setters
- No business logic in service
- All logic in facade or repository

**Example:**
```csharp
// ❌ Anemic service - No business logic
public class ModManagementService
{
    public async Task<ModInfo?> GetModAsync(string sha) => await _repository.GetByIdAsync(sha);
    public async Task<List<ModInfo>> GetAllModsAsync() => await _repository.GetAllAsync();
    public async Task<bool> DeleteModAsync(string sha) => await _repository.DeleteAsync(sha);
}
```

**Solution:**
```csharp
// ✅ Rich domain service - Contains business logic
public class ModManagementService
{
    public async Task<ModInfo?> GetModAsync(string sha)
    {
        var mod = await _repository.GetByIdAsync(sha);
        if (mod != null)
        {
            // Business logic: Load preview paths
            mod.PreviewPaths = await LoadPreviewPathsAsync(mod.SHA);
        }
        return mod;
    }

    public async Task<bool> DeleteModAsync(string sha)
    {
        var mod = await _repository.GetByIdAsync(sha);
        if (mod == null) return false;

        // Business logic: Clean up related files
        await _fileService.DeleteDirectoryAsync(GetModPreviewDirectory(sha));

        return await _repository.DeleteAsync(sha);
    }
}
```

---

## Refactoring Guidelines

### When to Extract a Service

**Extract when:**
1. Logic is duplicated in 2+ places
2. Service has 1000+ lines of code
3. Service has 10+ dependencies
4. Logic belongs to a different domain

**Example:**

```csharp
// BEFORE: MigrationService doing everything (991 lines)
public class MigrationService
{
    private async Task<PythonConfiguration?> ParseConfigurationAsync(...)
    {
        // 58 lines of Python config parsing
    }
}

// AFTER: Extract parser (58 lines moved)
public interface IPythonConfigurationParser
{
    Task<PythonConfiguration?> ParseAsync(string pythonPath, string envName);
}

public class PythonConfigurationParser : IPythonConfigurationParser
{
    // 58 lines moved here
}

public class MigrationService
{
    private readonly IPythonConfigurationParser _configParser;

    public async Task AnalyzeAsync()
    {
        var config = await _configParser.ParseAsync(pythonPath, envName);
    }
}
```

---

### When to Add Methods to Existing Service

**Add when:**
1. Logic belongs to that service's domain
2. Service will remain under 1000 lines
3. Method uses service's existing dependencies
4. No duplication with other services

**Example:**

```csharp
// BEFORE: MigrationService creating classification nodes manually
public class MigrationService
{
    private readonly IClassificationRepository _repository;

    public async Task MigrateClassificationsAsync()
    {
        var node = new ClassificationNode { ... };
        if (!await _repository.ExistsAsync(nodeId))
        {
            await _repository.InsertAsync(node);  // ❌ BYPASSING SERVICE
        }
    }
}

// AFTER: Add method to ClassificationService
public interface IClassificationService
{
    Task<ClassificationNode?> CreateNodeAsync(string nodeId, string name, string? parentId = null);
    Task<bool> NodeExistsAsync(string nodeId);
}

public class ClassificationService : IClassificationService
{
    public async Task<ClassificationNode?> CreateNodeAsync(...)
    {
        if (await _repository.ExistsAsync(nodeId))
            return null;  // Already exists

        var node = new ClassificationNode { ... };
        await _repository.InsertAsync(node);
        await RefreshTreeAsync();
        return node;
    }
}

public class MigrationService
{
    private readonly IClassificationService _classificationService;

    public async Task MigrateClassificationsAsync()
    {
        await _classificationService.CreateNodeAsync(...);  // ✅ USING SERVICE
    }
}
```

---

### Refactoring Process

#### Step 1: Identify Duplication
- Search for duplicate code patterns
- Identify services bypassing other services
- Check for direct repository access

#### Step 2: Plan Extraction
- Determine which module owns the logic
- Check if service already exists
- Decide: extend existing service or create new one?

#### Step 3: Create/Extend Service
1. Add interface method
2. Implement in service class
3. Write unit tests
4. Register in DI container

#### Step 4: Refactor Callers
1. Inject service interface
2. Replace duplicate code with service calls
3. Remove duplicate methods
4. Update tests

#### Step 5: Verify
1. Build succeeds
2. All tests pass
3. Integration testing
4. Code review

---

## Quick Reference

### Good Service Design Checklist

- ✅ Service has clear single responsibility
- ✅ Service depends on interfaces, not concrete classes
- ✅ Service uses other services, not repositories from other modules
- ✅ Service is under 1000 lines
- ✅ Service has under 10 dependencies
- ✅ Service contains business logic, not just pass-through
- ✅ Service methods are well-documented
- ✅ Service is fully tested

### Bad Service Design Red Flags

- ❌ Service accesses repositories from other modules
- ❌ Service reimplements logic from other services
- ❌ Service has 1000+ lines of code
- ❌ Service has 10+ dependencies
- ❌ Service has methods unrelated to its domain
- ❌ Service is just a pass-through to repository (anemic)
- ❌ Multiple services duplicating the same logic

---

**Last Updated**: 2026-02-19
**Next Review**: When adding new modules or services

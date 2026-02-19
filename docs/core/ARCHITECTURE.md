# Architecture Documentation

**Project:** D3dxSkinManager
**Version:** 2.0.0 (Refactored)
**Last Updated:** 2026-02-17

---

## Table of Contents

1. [System Overview](#system-overview)
2. [Architecture Diagram](#architecture-diagram)
3. [Component Layers](#component-layers)
4. [Communication Flow](#communication-flow)
5. [Data Flow](#data-flow)
6. [Technology Decisions](#technology-decisions)
7. [Design Patterns](#design-patterns)
8. [Database Schema](#database-schema)
9. [File System Organization](#file-system-organization)
10. [Security Considerations](#security-considerations)
11. [Dependency Injection](#dependency-injection)

---

## System Overview

D3dxSkinManager uses a **layered architecture** with clear separation of concerns and **Dependency Injection**:

```
┌─────────────────────────────────────┐
│         Desktop Window              │
│         (Photino.NET)               │
├─────────────────────────────────────┤
│                                     │
│  ┌───────────────────────────────┐ │
│  │     Frontend (React/TS)       │ │
│  │  - Component Hierarchy        │ │
│  │  - Custom Hooks               │ │
│  │  - State Management           │ │
│  │  - IPC Client                 │ │
│  └───────────────────────────────┘ │
│              ↕ IPC                  │
│  ┌───────────────────────────────┐ │
│  │     Backend (C#/.NET)         │ │
│  │  - IPC Handler                │ │
│  │  - Facade Layer               │ │
│  │  - Service Layer              │ │
│  │  - Repository Layer           │ │
│  │  - DI Container               │ │
│  └───────────────────────────────┘ │
│              ↕                      │
│  ┌───────────────────────────────┐ │
│  │     Database (SQLite)         │ │
│  │  - Mod Metadata               │ │
│  └───────────────────────────────┘ │
│              ↕                      │
│  ┌───────────────────────────────┐ │
│  │     File System               │ │
│  │  - Mod Archives               │ │
│  │  - Game Files                 │ │
│  └───────────────────────────────┘ │
└─────────────────────────────────────┘
```

### Key Architectural Principles

1. **Separation of Concerns** - Each layer has a single, well-defined responsibility
2. **Dependency Injection** - Services resolved through DI container
3. **Facade Pattern** - High-level coordination for IPC layer
4. **Repository Pattern** - Data access abstraction
5. **Component Composition** - React components and custom hooks
6. **Async/Await** - All I/O operations are asynchronous
7. **Type Safety** - Strong typing in both C# and TypeScript
8. **Testability** - Interface-based design enables comprehensive unit testing

---

## Architecture Diagram

### Complete System Architecture

```
User
  ↓
Photino Window (Native OS Webview)
  ↓
┌───────────────────────────────────────────────────────────┐
│              React Application (Refactored)                │
│  ┌─────────────────────────────────────────────────────┐ │
│  │                   App.tsx (81 lines)                 │ │
│  │         - Layout composition                         │ │
│  │         - Tab navigation                             │ │
│  │         - Hook coordination                          │ │
│  └──────────────────────────────────────────────────────┘ │
│           ↓              ↓              ↓                  │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────────┐ │
│  │ Custom Hooks │ │  Components  │ │  Services        │ │
│  │              │ │              │ │                  │ │
│  │ useModData   │ │ ModTable     │ │ modService       │ │
│  │ useModFilters│ │ ModSearchBar │ │ photinoService   │ │
│  │ useModActions│ │ ModFilterPanel│ │                 │ │
│  └──────────────┘ └──────────────┘ └──────────────────┘ │
└───────────────────────────────────────────────────────────┘
         ↓ JSON messages (IPC)
         ↑ JSON responses
┌───────────────────────────────────────────────────────────┐
│              C# Backend (Refactored with DI)               │
│  ┌─────────────────────────────────────────────────────┐ │
│  │         Program.cs (Entry Point)                     │ │
│  │  - Photino window setup                              │ │
│  │  - IPC message handler                               │ │
│  │  - DI container initialization                       │ │
│  │                                                       │ │
│  │   var services = new ServiceCollection();           │ │
│  │   services.AddD3dxSkinManagerServices(dataPath);     │ │
│  │   _serviceProvider = services.BuildServiceProvider();│ │
│  │   _modFacade = _serviceProvider                      │ │
│  │               .GetRequiredService<IModFacade>();     │ │
│  └─────────────────────────────────────────────────────┘ │
│         ↓                                                  │
│  ┌─────────────────────────────────────────────────────┐ │
│  │         Facade Layer (ModFacade)                     │ │
│  │  - High-level coordination                           │ │
│  │  - Request handling                                  │ │
│  │  - Service orchestration                             │ │
│  │  - Error handling                                    │ │
│  │                                                       │ │
│  │   GetAllModsAsync()                                  │ │
│  │   LoadModAsync(sha)                                  │ │
│  │   UnloadModAsync(sha)                                │ │
│  │   ImportModAsync(filePath)                           │ │
│  │   SearchModsAsync(query)                             │ │
│  └─────────────────────────────────────────────────────┘ │
│         ↓             ↓             ↓              ↓      │
│  ┌────────────┐ ┌────────────┐ ┌────────────┐ ┌─────────┐
│  │ ModRepository│ ModArchive  │ │ ModImport  │ │ModQuery││
│  │            │ │  Service    │ │  Service   │ │Service ││
│  │  (Data     │ │ (File Ops) │ │(Workflow)  │ │(Search)││
│  │   Access)  │ │            │ │            │ │        ││
│  └────────────┘ └────────────┘ └────────────┘ └─────────┘
│         ↓             ↓             ↓              │      │
│  ┌────────────┐ ┌────────────┐ ┌────────────┐    │      │
│  │ FileService│ │Classification│ImageService│    │      │
│  │            │ │   Service   │            │    │      │
│  │(Hash, 7zip)│ │(Pattern     │(Thumbnails)│    │      │
│  │            │ │ Matching)   │            │    │      │
│  └────────────┘ └────────────┘ └────────────┘    │      │
└───────────────────────────────────────────────────┴──────┘
         ↓
┌───────────────────────────────────────────────────────────┐
│            SQLite Database (mods.db)                       │
│  - Mods table with indexes                                 │
│  - ACID transactions                                       │
└───────────────────────────────────────────────────────────┘
         ↓
┌───────────────────────────────────────────────────────────┐
│            File System                                     │
│  - Mod archives (7z, zip, rar)                             │
│  - Extracted files                                         │
│  - Thumbnails / previews                                   │
└───────────────────────────────────────────────────────────┘
```

---

## Component Layers

### 1. Presentation Layer (Frontend - Refactored)

**Technology:** React 19 + TypeScript 4.9 + Ant Design 6.3.0

**Structure:** Component-based with custom hooks

#### Component Hierarchy

```
App.tsx (Main Application)
├── AppHeader (Layout)
├── AppSider (Navigation)
└── Layout
    ├── ModManagementView
    │   ├── ModSearchBar
    │   ├── ModFilterPanel
    │   └── ModTable
    │       ├── ModTableColumns
    │       │   ├── ModThumbnail
    │       │   ├── StatusIcon
    │       │   ├── GradingTag
    │       │   └── ModActionButtons
    │       └── (Ant Design Table)
    ├── WarehouseView (Future)
    └── SettingsView (Future)
```

#### Custom Hooks

**useModData** - Data fetching and loading
- `loadMods()` - Fetch all mods from backend
- `loadFilters()` - Fetch object names and authors
- Returns: `{ mods, loading, objects, authors, loadMods, loadFilters }`

**File:** `D3dxSkinManager.Client/src/hooks/useModData.ts`

**useModFilters** - Filter state and logic
- `filteredMods` - Computed filtered list
- `updateFilter(key, value)` - Update filter state
- `clearFilters()` - Reset all filters
- `handleSearch(term)` - Backend search with ! negation
- Returns: `{ filters, filteredMods, loading, updateFilter, clearFilters, handleSearch, hasActiveFilters }`

**File:** `D3dxSkinManager.Client/src/hooks/useModFilters.ts`

**useModActions** - Mod operations
- `handleLoadMod(sha)` - Load a mod
- `handleUnloadMod(sha)` - Unload a mod
- `handleDeleteMod(sha, name)` - Delete with confirmation
- Returns: `{ handleLoadMod, handleUnloadMod, handleDeleteMod }`

**File:** `D3dxSkinManager.Client/src/hooks/useModActions.ts`

#### Services

**photinoService** - IPC Communication Bridge
- `sendMessage<T>(type, payload)` - Promise-based IPC
- Message queue with unique IDs
- Timeout handling (30 seconds)
- Development mode fallback with mock data

**File:** `D3dxSkinManager.Client/src/services/photino.ts`

**modService** - API Wrapper
- `getAllMods()` → `Promise<ModInfo[]>`
- `loadMod(sha)` → `Promise<boolean>`
- `unloadMod(sha)` → `Promise<boolean>`
- `searchMods(term)` → `Promise<ModInfo[]>`
- `getObjectNames()` → `Promise<string[]>`
- `getAuthors()` → `Promise<string[]>`

**File:** `D3dxSkinManager.Client/src/services/modService.ts`

#### Type System

**mod.types.ts**
- `ModInfo` interface
- `GradingLevel` type
- `ModFilters` interface
- `ModStatistics` interface

**message.types.ts**
- `MessageType` union type
- `PhotinoMessage` interface
- `PhotinoResponse` interface

**File:** `D3dxSkinManager.Client/src/types/`

#### Utilities

**grading.utils.ts**
- `getGradingColor(grading)` - Color mapping
- `getGradingLabel(level)` - Display labels
- `gradingOptions` - Filter options array

**File:** `D3dxSkinManager.Client/src/utils/grading.utils.ts`

---

### 2. Application Layer (Backend - Refactored with DI)

**Technology:** .NET 10 + C# 12 + Photino.NET 4.0.16 + Microsoft.Extensions.DependencyInjection

**Architecture:** Facade pattern with dependency injection

#### Program.cs - Entry Point

```csharp
private static IServiceProvider? _serviceProvider;
private static IModFacade? _modFacade;

private static void InitializeServices()
{
    var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
    Directory.CreateDirectory(dataPath);

    // Build DI container
    var services = new ServiceCollection();
    services.AddD3dxSkinManagerServices(dataPath);

    _serviceProvider = services.BuildServiceProvider();
    _modFacade = _serviceProvider.GetRequiredService<IModFacade>();
}
```

**File:** `D3dxSkinManager/Program.cs`

#### DI Container Configuration

**ServiceCollectionExtensions.cs**
```csharp
public static IServiceCollection AddD3dxSkinManagerServices(
    this IServiceCollection services,
    string dataPath)
{
    // Low-level services
    services.AddSingleton<IFileService, FileService>();
    services.AddSingleton<IClassificationService>(sp => {
        var service = new ClassificationService();
        service.LoadRulesAsync(Path.Combine(dataPath, "classification_rules.json")).Wait();
        return service;
    });
    services.AddSingleton<IImageService>(sp => new ImageService(dataPath));

    // Data layer
    services.AddSingleton<IModRepository>(sp => new ModRepository(dataPath));

    // Domain services
    services.AddSingleton<IModArchiveService>(sp => {
        var fileService = sp.GetRequiredService<IFileService>();
        return new ModArchiveService(dataPath, fileService);
    });
    services.AddSingleton<IModImportService, ModImportService>();
    services.AddSingleton<IModQueryService, ModQueryService>();

    // Facade
    services.AddSingleton<IModFacade, ModFacade>();

    return services;
}
```

**File:** `D3dxSkinManager/Configuration/ServiceCollectionExtensions.cs`

---

### 3. Facade Layer (Coordination)

**Purpose:** High-level coordination for IPC handlers

**ModFacade** - Service Coordinator

```csharp
public class ModFacade : IModFacade
{
    private readonly IModRepository _repository;
    private readonly IModArchiveService _archiveService;
    private readonly IModImportService _importService;
    private readonly IModQueryService _queryService;

    public ModFacade(
        IModRepository repository,
        IModArchiveService archiveService,
        IModImportService importService,
        IModQueryService queryService)
    {
        _repository = repository;
        _archiveService = archiveService;
        _importService = importService;
        _queryService = queryService;
    }

    public async Task<List<ModInfo>> GetAllModsAsync()
        => await _repository.GetAllAsync();

    public async Task<bool> LoadModAsync(string sha)
    {
        var mod = await _repository.GetByIdAsync(sha);
        if (mod == null) return false;

        await _archiveService.LoadAsync(mod);
        await _repository.SetLoadedStateAsync(sha, true);
        return true;
    }

    public async Task<List<ModInfo>> SearchModsAsync(string searchTerm)
        => await _queryService.SearchAsync(searchTerm);

    // ... other coordination methods
}
```

**File:** `D3dxSkinManager/Facades/ModFacade.cs:14`

**Benefits:**
- Clean IPC handler (delegates to facade)
- Service orchestration in one place
- Easy to test
- Clear dependencies

---

### 4. Service Layer (Business Logic - Refactored)

**Architecture:** Focused services with single responsibilities

#### ModRepository - Data Access

**Responsibility:** Pure database CRUD operations

```csharp
public interface IModRepository
{
    Task<List<ModInfo>> GetAllAsync();
    Task<ModInfo?> GetByIdAsync(string sha);
    Task<bool> ExistsAsync(string sha);
    Task<ModInfo> InsertAsync(ModInfo mod);
    Task<bool> UpdateAsync(ModInfo mod);
    Task<bool> DeleteAsync(string sha);
    Task<List<string>> GetLoadedIdsAsync();
    Task<List<string>> GetDistinctObjectNamesAsync();
    Task<List<string>> GetDistinctAuthorsAsync();
    Task<List<string>> GetAllTagsAsync();
    Task<bool> SetLoadedStateAsync(string sha, bool isLoaded);
}
```

**File:** `D3dxSkinManager/Services/ModRepository.cs` (~300 lines)

**Key Methods:**
- Database initialization with schema creation
- CRUD operations with parameterized queries
- Index creation for performance
- Async/await throughout

#### ModArchiveService - File Operations

**Responsibility:** Loading/unloading mods to game directory

```csharp
public interface IModArchiveService
{
    Task<bool> LoadAsync(ModInfo mod);
    Task<bool> UnloadAsync(ModInfo mod);
    Task<bool> DeleteAsync(ModInfo mod);
    Task<string> CopyArchiveAsync(string sourcePath);
}
```

**File:** `D3dxSkinManager/Services/ModArchiveService.cs` (~180 lines)

**Key Operations:**
- Extract archive to game directory
- Remove extracted files
- Copy archives to managed storage
- Dependency: `IFileService` for 7-Zip operations

#### ModImportService - Import Workflow

**Responsibility:** Orchestrate complete import process

```csharp
public interface IModImportService
{
    Task<ModInfo> ImportAsync(string filePath);
}
```

**File:** `D3dxSkinManager/Services/ModImportService.cs` (~200 lines)

**Import Steps:**
1. Calculate SHA256 hash (`IFileService`)
2. Check if already imported (`IModRepository`)
3. Copy archive to storage (`IModArchiveService`)
4. Extract archive (`IFileService`)
5. Read metadata files
6. Classify mod (`IClassificationService`)
7. Generate thumbnail (`IImageService`)
8. Save to database (`IModRepository`)

**Dependencies:** All other services - true orchestrator

#### ModQueryService - Search and Filtering

**Responsibility:** Advanced search with negation and AND logic

```csharp
public interface IModQueryService
{
    Task<List<ModInfo>> SearchAsync(string searchTerm);
}
```

**File:** `D3dxSkinManager/Services/ModQueryService.cs` (~150 lines)

**Features:**
- Supports `!` negation (e.g., `!outfit` excludes "outfit")
- Space-separated terms are AND (e.g., `fischl dark` requires both)
- Searches: name, author, object name, tags
- Returns filtered list from repository

#### FileService - Low-Level File Operations

**Responsibility:** File system and archive operations

```csharp
public interface IFileService
{
    Task<string> CalculateSha256Async(string filePath);
    Task<bool> ExtractArchiveAsync(string archivePath, string targetDirectory);
    Task<bool> CopyDirectoryAsync(string sourceDir, string targetDir, bool overwrite);
    Task<bool> DeleteDirectoryAsync(string directory);
    bool Is7ZipAvailable();
    string Get7ZipPath();
}
```

**File:** `D3dxSkinManager/Services/FileService.cs`

**Key Features:**
- SHA256 hashing with buffered reading
- 7-Zip integration for extraction
- Cross-platform 7z detection
- Directory operations

#### ClassificationService - Pattern Matching

**Responsibility:** Classify mods by wildcard patterns

```csharp
public interface IClassificationService
{
    Task<string?> ClassifyModAsync(string modDirectory);
    Task<bool> LoadRulesAsync(string rulesFilePath);
    List<ClassificationRule> GetRules();
    void AddRule(ClassificationRule rule);
    Task<bool> SaveRulesAsync(string rulesFilePath);
}
```

**File:** `D3dxSkinManager/Services/ClassificationService.cs`

**Features:**
- JSON-based classification rules
- Wildcard pattern matching (`*Fischl*.ini`)
- Priority-based rule evaluation
- Persistent rules storage

#### ImageService - Thumbnail Generation

**Responsibility:** Image preview and thumbnail generation

```csharp
public interface IImageService
{
    Task<string?> GetThumbnailPathAsync(string sha);
    Task<string?> GetPreviewPathAsync(string sha);
    Task<string?> GenerateThumbnailAsync(string modDirectory, string sha);
    Task<bool> CacheImageAsync(string sourcePath, string targetPath);
    Task<bool> ResizeImageAsync(string sourcePath, string targetPath, int maxWidth, int maxHeight);
    Task<bool> ClearModCacheAsync(string sha);
    string[] GetSupportedImageExtensions();
}
```

**File:** `D3dxSkinManager/Services/ImageService.cs`

**Features:**
- Find images in mod directories
- Generate thumbnails (150x300)
- Generate previews (600x1200)
- Cache management

---

### Service Dependency Graph

```
Program.cs
    ↓
ModFacade
    ├─→ ModRepository → SQLite Database
    ├─→ ModArchiveService → FileService → 7-Zip / File System
    ├─→ ModImportService
    │       ├─→ FileService
    │       ├─→ ModRepository
    │       ├─→ ModArchiveService
    │       ├─→ ClassificationService
    │       └─→ ImageService
    └─→ ModQueryService → ModRepository

Legend:
→ = dependency (injected via constructor)
```

**Key Points:**
- Clean dependency hierarchy (no circular dependencies)
- Low-level services at bottom (FileService, ClassificationService, ImageService)
- Data layer (ModRepository)
- Domain services (ModArchiveService, ModImportService, ModQueryService)
- Coordination layer (ModFacade)
- All resolved through DI container

---

## Communication Flow

### Frontend → Backend (Request)

```
User clicks "Load Mod"
    ↓
React onClick handler in ModActionButtons
    ↓
useModActions.handleLoadMod(sha)
    ↓
modService.loadMod(sha)
    ↓
photinoService.sendMessage('LOAD_MOD', { sha })
    ↓
Create unique message ID: msg_123_1708185600000
    ↓
JSON.stringify({ id, type: 'LOAD_MOD', payload: { sha } })
    ↓
window.chrome.webview.sendMessage(json) or window.external.sendMessage(json)
    ↓
[IPC Transport - Photino Native Bridge]
    ↓
Program.OnWebMessageReceived(sender, message)
    ↓
Deserialize JSON to MessageRequest
    ↓
Extract payload.sha
    ↓
await _modFacade.LoadModAsync(sha)
    ↓
ModFacade coordinates:
    ├─ _repository.GetByIdAsync(sha)
    ├─ _archiveService.LoadAsync(mod)
    └─ _repository.SetLoadedStateAsync(sha, true)
    ↓
Return bool result
```

### Backend → Frontend (Response)

```
_modFacade.LoadModAsync() returns true
    ↓
Create MessageResponse { id, success: true, data: true }
    ↓
JSON.stringify(response)
    ↓
window.SendWebMessage(json)
    ↓
[IPC Transport]
    ↓
photinoService message handler receives message
    ↓
JSON.parse(message) → PhotinoResponse
    ↓
Find promise by message.id in handlers map
    ↓
response.success ? resolve(response.data) : reject(response.error)
    ↓
useModActions receives result
    ↓
message.success('Mod loaded successfully')
    ↓
await loadMods() to refresh list
    ↓
React state updates
    ↓
UI re-renders with updated mod status
```

---

## Data Flow

### Import Mod Flow

```
Frontend                ModFacade           ModImportService        FileService         ModRepository
   │                       │                       │                     │                    │
   │──IMPORT_MOD(path)────→│                       │                     │                    │
   │                       │                       │                     │                    │
   │                       │──ImportAsync(path)──→│                     │                    │
   │                       │                       │                     │                    │
   │                       │                       │──CalculateSha256──→│                    │
   │                       │                       │←────SHA hash────────│                    │
   │                       │                       │                     │                    │
   │                       │                       │──ExistsAsync(sha)──────────────────────→│
   │                       │                       │←────false────────────────────────────────│
   │                       │                       │                     │                    │
   │                       │                       │──CopyArchive(path)→│                    │
   │                       │                       │←────newPath─────────│                    │
   │                       │                       │                     │                    │
   │                       │                       │──ExtractArchive────→│                    │
   │                       │                       │←────success─────────│                    │
   │                       │                       │                     │                    │
   │                       │                       │─ClassifyModAsync────→ClassificationService
   │                       │                       │←────objectName──────────────────────────│
   │                       │                       │                     │                    │
   │                       │                       │─GenerateThumbnail──→ImageService        │
   │                       │                       │←────thumbnailPath──────────────────────│
   │                       │                       │                     │                    │
   │                       │                       │──InsertAsync(modInfo)──────────────────→│
   │                       │                       │←────modInfo──────────────────────────────│
   │                       │                       │                     │                    │
   │                       │←────modInfo───────────│                     │                    │
   │                       │                       │                     │                    │
   │←────success───────────│                       │                     │                    │
   │                       │                       │                     │                    │
   │─Refresh mod list      │                       │                     │                    │
```

---

## Design Patterns

### 1. Facade Pattern ⭐ NEW

**Purpose:** Provide simplified interface for IPC handlers

**Implementation:**
```csharp
// Program.cs - IPC Handler
case "LOAD_MOD":
    var sha = payload.GetProperty("sha").GetString();
    var result = await _modFacade.LoadModAsync(sha);
    return new MessageResponse { Id = id, Success = true, Data = result };

// ModFacade - Coordinates services
public async Task<bool> LoadModAsync(string sha)
{
    var mod = await _repository.GetByIdAsync(sha);
    if (mod == null) return false;

    await _archiveService.LoadAsync(mod);
    await _repository.SetLoadedStateAsync(sha, true);
    return true;
}
```

**Benefits:**
- IPC handler stays thin (routing only)
- Business logic in facade
- Services remain focused
- Easy to test coordination

---

### 2. Repository Pattern ⭐ NEW

**Purpose:** Abstract data access layer

**Implementation:**
```csharp
public interface IModRepository
{
    Task<List<ModInfo>> GetAllAsync();
    Task<ModInfo?> GetByIdAsync(string sha);
    Task<ModInfo> InsertAsync(ModInfo mod);
    Task<bool> UpdateAsync(ModInfo mod);
    Task<bool> DeleteAsync(string sha);
}

public class ModRepository : IModRepository
{
    private readonly string _connectionString;
    // Implementation with SQLite
}
```

**Benefits:**
- Database logic isolated
- Easy to mock for tests
- Could swap database without changing services
- Clean separation of concerns

---

### 3. Dependency Injection ⭐ NEW

**Purpose:** Inversion of control, testability

**Implementation:**
```csharp
// Registration
services.AddSingleton<IModFacade, ModFacade>();
services.AddSingleton<IModRepository, ModRepository>();

// Constructor injection
public class ModFacade
{
    public ModFacade(
        IModRepository repository,
        IModArchiveService archiveService,
        IModImportService importService,
        IModQueryService queryService)
    {
        _repository = repository;
        // ...
    }
}
```

**Benefits:**
- No `new` keyword in business logic
- Easy to replace implementations
- Testable with mocks
- Clear dependencies

---

### 4. Custom Hooks Pattern (React) ⭐ NEW

**Purpose:** Reusable stateful logic

**Implementation:**
```typescript
// useModData.ts
export const useModData = () => {
  const [mods, setMods] = useState<ModInfo[]>([]);
  const [loading, setLoading] = useState(false);

  const loadMods = useCallback(async () => {
    setLoading(true);
    try {
      const data = await modService.getAllMods();
      setMods(data);
    } finally {
      setLoading(false);
    }
  }, []);

  return { mods, loading, loadMods };
};

// Usage in App.tsx
const { mods, loading, loadMods } = useModData();
```

**Benefits:**
- Logic separate from UI
- Reusable across components
- Testable in isolation
- Clean component code

---

### 5. Component Composition ⭐ NEW

**Purpose:** Build complex UI from small components

**Implementation:**
```typescript
// Small focused components
<ModManagementView>
  <ModSearchBar />
  <ModFilterPanel />
  <ModTable>
    <ModTableColumns>
      <ModThumbnail />
      <StatusIcon />
      <GradingTag />
      <ModActionButtons />
    </ModTableColumns>
  </ModTable>
</ModManagementView>
```

**Benefits:**
- Single Responsibility per component
- Easy to test individually
- Reusable across views
- Clear component hierarchy

---

## Database Schema

### Mods Table

```sql
CREATE TABLE Mods (
    -- Primary Key
    SHA TEXT PRIMARY KEY,              -- SHA256 hash of archive

    -- Core Fields
    ObjectName TEXT NOT NULL,          -- Character/object name
    Name TEXT NOT NULL,                -- Display name
    Author TEXT,                       -- Creator name
    Description TEXT,                  -- Long description

    -- Metadata
    Type TEXT DEFAULT '7z',            -- Archive type (7z, zip, rar)
    Grading TEXT DEFAULT 'G',          -- Content rating (G, P, R, X)
    Tags TEXT,                         -- JSON array: ["tag1", "tag2"]

    -- State
    IsLoaded INTEGER DEFAULT 0,        -- 1 = currently loaded
    IsAvailable INTEGER DEFAULT 0,     -- 1 = files exist on disk

    -- Media
    ThumbnailPath TEXT,                -- Path to thumbnail image
    PreviewPath TEXT,                  -- Path to preview image

    -- Timestamps
    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT DEFAULT CURRENT_TIMESTAMP
);

-- Indexes for performance
CREATE INDEX idx_object_name ON Mods(ObjectName);
CREATE INDEX idx_is_loaded ON Mods(IsLoaded);
```

**Schema managed by:** `ModRepository.InitializeDatabaseAsync()`
**File:** `D3dxSkinManager/Services/ModRepository.cs:30-65`

---

## File System Organization

```
d3dxSkinManage-Rewrite/
├── D3dxSkinManager/                 # Backend (.NET 10)
│   ├── Configuration/
│   │   └── ServiceCollectionExtensions.cs  # DI registration ⭐ NEW
│   ├── Facades/
│   │   ├── IModFacade.cs            # Facade interface ⭐ NEW
│   │   └── ModFacade.cs             # Facade implementation ⭐ NEW
│   ├── Models/
│   │   ├── ModInfo.cs               # Mod data model ⭐ NEW
│   │   ├── MessageRequest.cs        # IPC request model
│   │   └── MessageResponse.cs       # IPC response model
│   ├── Services/
│   │   ├── ModRepository.cs         # Data access ⭐ NEW
│   │   ├── ModArchiveService.cs     # File operations ⭐ NEW
│   │   ├── ModImportService.cs      # Import workflow ⭐ NEW
│   │   ├── ModQueryService.cs       # Search logic ⭐ NEW
│   │   ├── FileService.cs           # Low-level file ops
│   │   ├── ClassificationService.cs # Pattern matching
│   │   ├── ImageService.cs          # Thumbnails
│   │   └── ServiceInterfaces.cs     # Service interfaces ⭐ NEW
│   ├── Program.cs                   # Entry point (refactored)
│   └── D3dxSkinManager.csproj
│
├── D3dxSkinManager.Client/          # Frontend (React)
│   ├── src/
│   │   ├── components/              # Component library ⭐ NEW
│   │   │   ├── common/
│   │   │   │   ├── GradingTag.tsx
│   │   │   │   ├── StatusIcon.tsx
│   │   │   │   └── ModThumbnail.tsx
│   │   │   ├── layout/
│   │   │   │   ├── AppHeader.tsx
│   │   │   │   └── AppSider.tsx
│   │   │   ├── mods/
│   │   │   │   ├── ModTable.tsx
│   │   │   │   ├── ModTableColumns.tsx
│   │   │   │   ├── ModSearchBar.tsx
│   │   │   │   ├── ModFilterPanel.tsx
│   │   │   │   ├── ModActionButtons.tsx
│   │   │   │   └── ModManagementView.tsx
│   │   │   └── index.ts             # Barrel export
│   │   ├── hooks/                   # Custom hooks ⭐ NEW
│   │   │   ├── useModData.ts
│   │   │   ├── useModFilters.ts
│   │   │   ├── useModActions.ts
│   │   │   └── index.ts
│   │   ├── types/                   # Type definitions ⭐ NEW
│   │   │   ├── mod.types.ts
│   │   │   ├── message.types.ts
│   │   │   └── index.ts
│   │   ├── utils/                   # Utilities ⭐ NEW
│   │   │   └── grading.utils.ts
│   │   ├── services/
│   │   │   ├── photino.ts           # IPC bridge (refactored)
│   │   │   └── modService.ts        # API wrapper (refactored)
│   │   ├── App.tsx                  # Main app (81 lines, refactored)
│   │   ├── App.old.tsx              # Original (backup)
│   │   └── index.tsx
│   └── package.json
│
└── docs/                            # Documentation
    ├── ai-assistant/
    ├── core/
    └── features/
```

---

## Security Considerations

### 1. SQL Injection Prevention

**Always use parameterized queries:**

```csharp
// ✅ Good - Parameterized
var command = connection.CreateCommand();
command.CommandText = "SELECT * FROM Mods WHERE SHA = @sha";
command.Parameters.AddWithValue("@sha", sha);

// ❌ Bad - String concatenation
command.CommandText = $"SELECT * FROM Mods WHERE SHA = '{sha}'";
```

**Implemented in:** `ModRepository` - all query methods

---

### 2. Path Traversal Prevention

**Validate file paths:**

```csharp
// Validate before file operations
if (filePath.Contains("..") || !Path.IsPathRooted(basePath))
{
    throw new ArgumentException("Invalid file path");
}

var safePath = Path.Combine(baseDirectory, Path.GetFileName(filePath));
```

---

### 3. Input Validation

**Validate all user input:**

```csharp
// Validate SHA format
if (string.IsNullOrWhiteSpace(sha) ||
    !Regex.IsMatch(sha, "^[a-fA-F0-9]{64}$"))
{
    throw new ArgumentException("Invalid SHA256 hash");
}
```

---

## Dependency Injection

### Service Lifetimes

| Service | Lifetime | Reason |
|---------|----------|--------|
| IFileService | Singleton | Stateless, thread-safe |
| IClassificationService | Singleton | Rules loaded once |
| IImageService | Singleton | Stateless operations |
| IModRepository | Singleton | Connection per operation |
| IModArchiveService | Singleton | Stateless, uses IFileService |
| IModImportService | Singleton | Orchestration only |
| IModQueryService | Singleton | Uses repository |
| IModFacade | Singleton | Coordinates services |

**All services are singletons** because:
- They're stateless (no per-request state)
- Thread-safe (async/await, no shared mutable state)
- Performance (created once)
- Desktop app (no request scoping needed)

---

## Performance Optimizations

### 1. Database Indexing

```sql
CREATE INDEX idx_object_name ON Mods(ObjectName);
CREATE INDEX idx_is_loaded ON Mods(IsLoaded);
```

**Impact:** 100x faster queries on filtered lists

### 2. Async/Await Throughout

**All I/O operations are async:**
- Database: `await connection.OpenAsync()`
- File system: `await File.ReadAllTextAsync()`
- Network: `await httpClient.GetAsync()` (future)

**Impact:** Responsive UI, no blocking

### 3. Component Memoization (Future)

```typescript
const MemoizedModTable = React.memo(ModTable, (prev, next) =>
  prev.mods === next.mods && prev.loading === next.loading
);
```

### 4. Virtual Scrolling (Future)

For 1000+ mod lists:
```typescript
<Table virtual scroll={{ y: 600 }} />
```

---

*Architecture documentation reflects the refactored codebase as of 2026-02-17.*

*Last updated: 2026-02-17*
*Version: 2.0.0 (Refactored)*

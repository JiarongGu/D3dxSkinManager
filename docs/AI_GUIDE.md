# AI Assistant Guide

**Version:** 3.6
**Last Updated:** 2026-03-10
**Critical:** NEVER commit without explicit user approval!

**Recent Additions (v3.6):**
- Testing principles section with emphasis on verifying design intent
- Database schema verification through migrations
- Reusable mock helpers pattern (MockFileHelper, MockHashHelper)
- Separation of I/O from business logic for testability
- InMemoryDatabaseTestBase for integration testing with real migrations

**Previous (v3.5):**
- Unified error handling with OperationException (Code + Parameters pattern)
- Single exception type for all operations (backend and frontend)
- Added `translateErrorMessage()` helper for consistent error translation
- Backend uses JsonHelper.Serialize() for camelCase serialization
- Removed ModException and WorkflowException in favor of OperationException

**Previous (v3.4):**
- Batch edit save with loading overlay and close prevention
- Centralized `setLoading` in SlideInScreenContext for loading states
- AG Grid dirty state tracking for changed mods detection
- Fixed `BATCH_UPDATE_METADATA` and `BATCH_UPDATE_CATEGORY` to accept individual values per mod (Dictionary pattern)
- Deep cloning with lodash-es for proper state management

**Previous (v3.3):**
- Batch edit feature with AG Grid and inline text highlighting
- VSCode-style find/replace panel with global search
- Custom cell renderers for search result highlighting
- Debounced search with 300ms delay for performance

**Previous (v3.2):**
- IMemoryCache caching pattern with event-driven invalidation
- FileSystemWatcher pattern for cache invalidation
- Orphaned mod detection and cleanup workflow
- Context menu simplification for special cases (orphaned mods)

---

## 🎯 START HERE: RAG System is YOUR PRIMARY TOOL

**⚠️ CRITICAL**: Before writing ANY code, ALWAYS use the RAG system to load relevant documentation.

### Why RAG First?

1. **Prevents mistakes** - Architecture decisions, patterns, and constraints are documented
2. **Saves tokens** - Load only what you need, when you need it
3. **Stays updated** - Documentation is the source of truth, not this guide

### RAG Quick Reference

| When You Need | Load This First | Then Load |
|--------------|----------------|-----------|
| **Architecture/design decisions** | `docs/core/DESIGN_DECISIONS.md` | `docs/architecture/*.md` |
| **Find where something is** | `docs/KEYWORDS_INDEX.md` | Domain-specific file |
| **How to implement X** | `docs/ai-assistant/WORKFLOWS.md` | `docs/keywords/HOW_TO.md` |
| **Something not working** | `docs/ai-assistant/TROUBLESHOOTING.md` | Module-specific docs |
| **Understanding existing code** | `docs/architecture/CURRENT_ARCHITECTURE.md` | Module architecture |

---

## 🔥 Critical Rules (BREAK THESE = MAJOR ISSUES)

### 1. Git Commits
```bash
# ALWAYS ask before committing
"Ready to commit these changes?"  # WAIT for explicit "yes"
```

### 2. Architecture
```csharp
// Backend: ALL heavy operations
// Frontend: UI only, NO data processing
// Paths: Relative in DB, absolute at runtime
// DI: Constructor injection via interfaces

// Facades: THIN IPC layer ONLY
// - NO business logic in facades
// - NO event emission in facades
// - Just delegate to services and return results

// Services: Business logic + Event emission
// - Services perform operations AND emit events
// - Inject IProfileEventBus into services
// - Event handlers consolidate events for frontend
```

### 3. Error Handling (Unified Pattern)

**Backend: OperationException (Code + Parameters)**
```csharp
// ✅ Throw structured exceptions
using D3dxSkinManager.Modules.Core.Exceptions;

throw new OperationException(
    "MOD_DELETE_FAILED",
    new Dictionary<string, string> { { "name", modName }, { "sha", sha } },
    "Failed to delete mod"  // Optional fallback message
);

// ✅ BaseFacade automatically handles OperationException
// IPC response: { code: "MOD_DELETE_FAILED", parameters: { name: "MyMod", sha: "abc..." } }
```

**Frontend: handleError() and translateErrorMessage()**
```typescript
// ✅ For IPC errors (real-time operations)
import { handleError } from '@/shared/utils/errorHandler';
try {
  await modService.deleteMod(profileId, sha);
} catch (error: unknown) {
  handleError(error);  // Parses error, shows i18n notification
}

// ✅ For displaying stored errors (workflow.errorMessage, etc.)
import { translateErrorMessage } from '@/shared/utils/errorHandler';
const errorText = translateErrorMessage(workflow.errorMessage);
// Returns: "Failed to delete mod: MyMod" (translated with parameters)

// ❌ Never manually extract error messages
catch (error) {
  notification.error((error as Error).message);  // WRONG - no i18n
}
```

**Error Pattern Summary:**
- Backend: Single `OperationException` with `Code` + `Parameters`
- Frontend: Single `OperationError` with `code` + `parameters`
- IPC: `{ code, parameters }` serialized with camelCase
- i18n: `errors.{CODE}` pattern for all error translations
- Display: Use `translateErrorMessage()` for stored error strings

### 4. Testing Principles

**CRITICAL: Always verify design intent before fixing tests!**

```csharp
// ❌ WRONG: Making tests pass without understanding
[Fact]
public void Test_ShouldPass()
{
    // Change test to match whatever the code does
    result.Should().BeWhatever();  // Just make it green
}

// ✅ CORRECT: Verify design intent first
// 1. Check database migrations for schema
// 2. Ensure entity types match database
// 3. Fix the root cause, not symptoms
```

**Testing Best Practices:**

1. **Verify Database Schema First**
   ```csharp
   // Check migration files to understand nullable/required fields
   // Location: Modules/Fluent/Migrations/Migration_*.cs
   .WithColumn("Author").AsText().Nullable()     // Can be null
   .WithColumn("Name").AsText().NotNullable()    // Cannot be null

   // Entity should match:
   public string Name { get; set; } = string.Empty;  // Required
   public string? Author { get; set; }               // Nullable
   ```

2. **Use In-Memory Database with Migrations**
   ```csharp
   // Tests should extend InMemoryDatabaseTestBase
   // This runs real migrations on in-memory SQLite
   public class ModRepositoryIntegrationTests : InMemoryDatabaseTestBase
   {
       // Tests real database behavior, not mocks
   }
   ```

3. **Create Reusable Mock Helpers**
   ```csharp
   // Location: D3dxSkinManager.Tests/Helpers/

   // MockFileHelper - In-memory fake file system
   var mockFileHelper = new MockFileHelper();
   mockFileHelper.AddFile("path/to/file.txt", "content");

   // MockHashHelper - Predictable hash generation
   var mockHashHelper = new MockHashHelper();
   mockHashHelper.SetFileHash("file.txt", "ABC123");
   ```

4. **Separate I/O from Business Logic**
   ```csharp
   // ❌ WRONG: Direct file I/O in service
   public class ImageService
   {
       public void DeleteImage(string path)
       {
           File.Delete(path);  // Untestable
       }
   }

   // ✅ CORRECT: Inject file operations
   public class ImageService
   {
       private readonly IFileHelper _fileHelper;

       public ImageService(IFileHelper fileHelper)
       {
           _fileHelper = fileHelper;
       }

       public void DeleteImage(string path)
       {
           _fileHelper.DeleteFile(path);  // Testable
       }
   }
   ```

5. **Test Naming Convention**
   ```csharp
   [Fact]
   public async Task MethodName_Scenario_ExpectedBehavior()
   {
       // Arrange
       // Act
       // Assert
   }

   // Example:
   public async Task DeletePreviewAsync_WithMiddlePreview_ShouldRenumberSubsequentPreviews()
   ```

6. **Mock Setup Best Practices**
   ```csharp
   // Use specific setups, not general ones
   _mockRepository
       .Setup(x => x.GetByNameAsync(It.IsAny<string>()))
       .ReturnsAsync((string name) =>
           name == "existing" ? existingEntity : null);

   // Capture callbacks for event testing
   Func<EventMessage, Task>? capturedHandler = null;
   _mockEventBus
       .Setup(x => x.Subscribe(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<EventMessage, Task>>()))
       .Callback<string, string, Func<EventMessage, Task>>((m, t, h) => capturedHandler = h)
       .Returns("handler-id");
   ```

**Testing Checklist:**
- [ ] Check migrations for database schema
- [ ] Verify entity types match schema (nullable vs required)
- [ ] Use InMemoryDatabaseTestBase for integration tests
- [ ] Create mock helpers for reusable test infrastructure
- [ ] Separate I/O operations for testability
- [ ] Fix root causes, not just make tests pass

### 5. Data Conventions
```typescript
// ✅ undefined for missing data
const [mod, setMod] = useState<ModInfo>();

// ✅ null ONLY for React render returns
if (!data) return null;
```

### 6. i18n Translations
```json
// ✅ All error codes must have translations in both en.json and cn.json
// Location: D3dxSkinManager/Languages/*.json
{
  "errors.MOD_DELETE_FAILED": "Failed to delete mod: {{name}}",
  "errors.WORKFLOW_MI_DUPLICATE_MOD": "This mod already exists in your library: {{name}}",
  "errors.UNKNOWN_ERROR": "An unknown error occurred."
}
```

---

## 📋 Before Writing Code Checklist

- [ ] **Load RAG docs** - Check KEYWORDS_INDEX.md or DESIGN_DECISIONS.md first
- [ ] **Check existing patterns** - Load WORKFLOWS.md for similar examples
- [ ] **Verify module boundaries** - Never access other module's repositories
- [ ] **Plan error handling** - Use ErrorCode system

---

## 🏗️ Core Patterns (Minimal Reference)

### Backend Service (with Event Emission)
```csharp
// 1. Interface
public interface IModLifecycleService {
    Task<ModLoadResult> LoadAsync(string sha);
    Task<bool> UnloadAsync(string sha);
}

// 2. Implementation with DI + Event Emission
public class ModLifecycleService : IModLifecycleService {
    private readonly IModRepository _repository;
    private readonly IModArchiveService _archiveService;  // ✅ Layer 1 service (pure operations)
    private readonly IModCacheService _cacheService;      // ✅ Layer 1 service (pure operations)
    private readonly IProfileEventBus _eventBus;          // ✅ Inject EventBus for event emission
    private readonly ILogHelper _logger;

    public ModLifecycleService(
        IModRepository repository,
        IModArchiveService archiveService,
        IModCacheService cacheService,
        IProfileEventBus eventBus,  // ✅ Inject EventBus
        ILogHelper logger) {
        _repository = repository;
        _archiveService = archiveService;
        _cacheService = cacheService;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<ModLoadResult> LoadAsync(string sha) {
        // Business logic: category conflict resolution
        var mod = await _repository.GetByIdAsync(sha);

        // Unload conflicting mods in same category
        await HandleCategoryConflicts(mod);

        // Coordinate Layer 1 services (pure operations)
        var success = await _cacheService.EnableCacheAsync(sha)
                   || await _archiveService.ExtractAsync(sha);

        if (success) {
            // ✅ Service emits event after successful operation
            await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.LOADED, new { Sha = sha });
        }

        return new ModLoadResult { Success = success };
    }
}

// 3. Register in {Module}ServiceExtensions.cs
services.AddSingleton<IModLifecycleService, ModLifecycleService>();

// 4. Event Handler (Event Consolidation Layer - OPTIONAL)
// Use when frontend needs to react to multiple backend events with same action
public class ModListEventHandler : IModListEventHandler {
    public ModListEventHandler(IProfileEventBus eventBus) {
        // Subscribe to all mod state change events
        eventBus.Subscribe(ModuleNames.MOD, ModEvents.LOADED, HandleModChange);
        eventBus.Subscribe(ModuleNames.MOD, ModEvents.UNLOADED, HandleModChange);
        eventBus.Subscribe(ModuleNames.MOD, ModEvents.DELETED, HandleModChange);
        eventBus.Subscribe(ModuleNames.MOD, ModEvents.IMPORTED, HandleModChange);
        // ... 8 total subscriptions
    }

    private async Task HandleModChange(object data) {
        // Consolidate into single frontend event
        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.MOD_LIST_UPDATED, data);
    }
}

// Register event handler
services.AddSingleton<IModListEventHandler, ModListEventHandler>();
```

### Backend Facade (Thin IPC Layer)
```csharp
// ❌ OLD WAY: Facade has business logic + events
public class ModFacade {
    private readonly IModRepository _repository;
    private readonly IProfileEventBus _eventBus;

    public async Task<bool> LoadModAsync(string sha) {
        // ❌ Business logic in facade
        var mod = await _repository.GetByIdAsync(sha);
        if (mod.Category != null) {
            var conflicting = await _repository.GetByCategoryAsync(mod.Category);
            // Unload conflicting mods...
        }
        // ❌ Event emission in facade
        await _eventBus.EmitAsync(...);
    }
}

// ✅ NEW WAY: Facade is thin IPC layer
// Facades should ONLY handle IPC routing, not be called by other services
public interface IModFacade : IModuleFacade {
    // Empty interface - facade only handles IPC routing
    // Other services should call underlying services directly (IModRepository, IModLifecycleService, etc.)
}

public class ModFacade : BaseFacade, IModFacade {
    private readonly IModLifecycleService _lifecycleService;

    public ModFacade(IModLifecycleService lifecycleService) {
        _lifecycleService = lifecycleService;
    }

    // IPC handler method (private, called by IPC routing)
    private async Task<ModLoadResult> LoadModAsync(IpcRequest request) {
        var sha = _payloadHelper.GetRequiredValue<string>(request.Payload, "sha");
        // ✅ Just delegate - service handles everything
        return await _lifecycleService.LoadAsync(sha);
    }
}
```

### Frontend IPC Services
**ALL IPC services are consolidated in `shared/services/ipc/`**

```typescript
// Location: shared/services/ipc/modService.ts
export class ModService extends BaseModuleService {
  constructor() {
    super('MOD');  // Module name
  }

  async getModsByCategory(profileId: string, categoryId: string): Promise<ModInfo[]> {
    return this.sendArrayMessage<ModInfo>('GET_BY_CATEGORY', profileId, { categoryId });
  }
}

// Export singleton instance
export const modService = new ModService();
```

**Importing IPC Services:**
```typescript
// ✅ Use the consolidated API (recommended)
import { api } from '@/shared/services/ipc';
const mods = await api.mod.getModsByCategory(profileId, categoryId);
const profile = await api.profile.getActiveProfile();

// ✅ Or import individual services
import { modService, profileService } from '@/shared/services/ipc';
const mods = await modService.getModsByCategory(profileId, categoryId);
```

**Available IPC Services:**
- `api.mod` - Mod management (ModService)
- `api.profile` - Profile operations (ProfileService)
- `api.workflow` - Workflow management (WorkflowService)
- `api.launch` - Launch 3DMigoto/game (LaunchService)
- `api.settings` - Global settings (SettingsService)
- `api.validation` - Startup validation (ValidationService)
- `api.category` - Category tree (CategoryService)
- `api.language` - Language/i18n (LanguageService)
- `api.system` - File dialogs, system settings (SystemService)

### Event-Driven Architecture

**CRITICAL RULE: Services emit events, NOT facades!**

```csharp
// ❌ WRONG: Facade emits events
public class ModFacade {
    private readonly IModFileService _fileService;
    private readonly IProfileEventBus _eventBus;  // ❌ NO!

    public async Task<bool> LoadModAsync(string sha) {
        await _fileService.LoadAsync(sha);
        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.LOADED, new { sha });  // ❌ NO!
        return true;
    }
}

// ✅ CORRECT: Service emits events
public class ModFileService {
    private readonly IProfileEventBus _eventBus;  // ✅ YES!

    public async Task<bool> LoadAsync(string sha) {
        // Business logic here...
        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.LOADED, new { sha });  // ✅ YES!
        return true;
    }
}

// ✅ Facade is thin - just delegates
public class ModFacade {
    private readonly IModFileService _fileService;  // NO EventBus!

    public async Task<bool> LoadModAsync(string sha) {
        return await _fileService.LoadAsync(sha);  // Just delegate
    }
}
```

**Event Handler Pattern:**
```csharp
// Event handlers consolidate multiple events into one for frontend
public class ModListEventHandler : IModListEventHandler {
    public ModListEventHandler(IProfileEventBus eventBus) {
        // Subscribe to all mod state change events
        eventBus.Subscribe(ModuleNames.MOD, ModEvents.LOADED,
            async (_) => await EmitModListUpdated("LOADED"));
        eventBus.Subscribe(ModuleNames.MOD, ModEvents.UNLOADED,
            async (_) => await EmitModListUpdated("UNLOADED"));
        eventBus.Subscribe(ModuleNames.MOD, ModEvents.DELETED,
            async (_) => await EmitModListUpdated("DELETED"));
        eventBus.Subscribe(ModuleNames.MOD, ModEvents.IMPORTED,
            async (_) => await EmitModListUpdated("IMPORTED"));
        eventBus.Subscribe(ModuleNames.MOD, ModEvents.METADATA_UPDATED,
            async (_) => await EmitModListUpdated("METADATA_UPDATED"));
        eventBus.Subscribe(ModuleNames.MOD, ModEvents.CATEGORY_UPDATED,
            async (_) => await EmitModListUpdated("CATEGORY_UPDATED"));
        eventBus.Subscribe(ModuleNames.MOD, ModEvents.CACHE_CHANGED,
            async (_) => await EmitModListUpdated("CACHE_CHANGED"));
        // Total: 8 event subscriptions (was 7, added CACHE_CHANGED)
    }

    private async Task EmitModListUpdated(string sourceEvent) {
        // Consolidate into single frontend event
        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.MOD_LIST_UPDATED);
    }
}
```

**Frontend subscribes to consolidated events:**
```typescript
// In ModProvider.tsx - subscribe to consolidated MOD_LIST_UPDATED event

// BEFORE (8+ specific event subscriptions):
// eventBus.subscribe(Module.MOD, ModEventType.LOADED, handleModStateChange);
// eventBus.subscribe(Module.MOD, ModEventType.UNLOADED, handleModStateChange);
// eventBus.subscribe(Module.MOD, ModEventType.DELETED, handleModStateChange);
// eventBus.subscribe(Module.MOD, ModEventType.IMPORTED, handleModStateChange);
// ... 8+ separate subscriptions

// AFTER (1 consolidated subscription with debouncing):
const handleModListUpdate = useCallback(
  debounce(() => {
    if (!selectedProfileId) return;
    void modOps.refreshMods(selectedProfileId);  // Reload mod list
    void modOps.loadStatistics(selectedProfileId);  // Reload statistics
  }, 20),  // 20ms debounce prevents rapid-fire events
  [selectedProfileId]
);

useEffect(() => {
  if (!selectedProfileId) return;

  const unsubscribe = eventBus.subscribe(
    Module.MOD,
    ModEventType.MOD_LIST_UPDATED,  // Single consolidated event
    handleModListUpdate
  );

  return () => {
    handleModListUpdate.cancel();  // Cancel debounce on cleanup
    unsubscribe();
  };
}, [selectedProfileId, handleModListUpdate]);

// Benefits:
// - 8+ event handlers → 1 debounced handler
// - Prevents event storms (multiple rapid-fire events within 20ms handled once)
// - Simpler event flow: Backend consolidates → Frontend reacts once
```

**IPC Events (Module + Type Pattern):**
```csharp
// Backend - NO module prefix in type names
await _eventBus.EmitAsync(
    ModuleNames.MOD,      // Module
    ModEvents.LOADED,     // Type (NOT "MOD_LOADED")
    new { sha }
);

// Frontend
eventBus.subscribe(
  Module.MOD,
  ModEventType.LOADED,
  (event) => logger.info(event.payload.sha)
);
```

### State Management (Zustand)
```typescript
// Store
export const useModsStore = create<ModsState>((set) => ({
  mods: [],
  setMods: (mods) => set({ mods }),
}));

// Component
const mods = useModsStore((state) => state.mods);
```

### Debouncing with Parameters

**CRITICAL: Standard lodash debounce only keeps the LAST call's parameters!**

When multiple rapid calls occur with different arguments (e.g., `fn('A')`, `fn('B')`, `fn('C')`), only `C` gets processed. This causes parameter loss in event handlers.

**Solution: Use `memoizeDebounce` for per-parameter debouncing**

```typescript
// Location: shared/utils/memoizeDebounce.ts
import { memoizeDebounce } from '@/shared/utils/memoizeDebounce';

// ❌ WRONG: Regular debounce loses parameters
const handleEvent = useCallback(
  debounce(async (sha: string) => {
    await refreshMod(sha);  // Only last sha is processed!
  }, 20),
  []
);
// Problem: LOADED(mod1), UNLOADED(mod2), LOADED(mod3)
// → Only mod3 gets refreshed, mod1 and mod2 are lost!

// ✅ CORRECT: memoizeDebounce creates separate timer per parameter
const handleEvent = useCallback(
  memoizeDebounce(
    async (sha: string) => {
      await refreshMod(sha);
    },
    (sha) => sha,  // Cache key resolver (required, second param)
    20
  ),
  []
);
// Result: mod1, mod2, mod3 all get their own 20ms timer
// Each mod refreshes independently after its timer expires
```

**When to use which:**

| Scenario | Use | Reason |
|----------|-----|--------|
| **Different entities** (different mod SHAs) | `memoizeDebounce` | Each entity needs independent timer |
| **Same operation batching** (save panel sizes) | Regular `debounce` | Batch all changes into one save |
| **Multiple params to same entity** (save tag color) | Regular `debounce` | Last value wins (desired behavior) |

**Example: ModProvider event handling**
```typescript
// Per-mod refresh - each mod gets own timer
const handleModLoadStateChange = useCallback(
  memoizeDebounce(
    async (sha: string) => {
      if (selectedProfileIdRef.current) {
        await modOps.refreshMod(selectedProfileIdRef.current, sha);
      }
    },
    (sha) => sha,  // Cache key resolver (each SHA has independent timer)
    20
  ),
  []
);

// Event subscriptions
eventBus.subscribe(Module.MOD, ModEventType.LOADED, (event) => {
  const sha = event.payload?.sha;
  if (sha) {
    handleModLoadStateChange(sha);  // Won't lose parameters
  }
});
```

**Cleanup: cancel() without params cancels ALL timers**
```typescript
useEffect(() => {
  return () => {
    handleModLoadStateChange.cancel();  // Cancels all memoized timers
  };
}, []);
```

### Provider Initialization Order
**Location:** `App.tsx` - `AppWithProviders` component

All providers are initialized at the top level in a specific order to ensure stores are loaded before any components try to read from them. This prevents duplicate API calls and race conditions during app startup.

```typescript
// App.tsx provider hierarchy (ORDER MATTERS!)
const AppWithProviders: React.FC = () => {
  return (
    <ProfileProvider>   {/* 1. Profile context management (highest level) */}
      <SettingsProvider>    {/* 2. Loads global settings into settingsStore */}
        <ModProvider>     {/* 3. Profile-scoped side effects (depends on ProfileProvider) */}
          <ThemeProvider>         {/* 4. Reads from settingsStore */}
            <I18nInitializer>     {/* 5. Reads from settingsStore */}
              <SlideInScreenProvider>
                <App />
              </SlideInScreenProvider>
            </I18nInitializer>
          </ThemeProvider>
        </ModProvider>
      </SettingsProvider>
    </ProfileProvider>
  );
};
```

**Why this order:**
1. **ProfileProvider first** - Highest level provider that manages profile context (many features depend on this)
2. **SettingsProvider second** - Loads settings into store before ThemeProvider/i18n try to read them
3. **ModProvider third** - Sets up event subscriptions that depend on ProfileProvider context
4. **ThemeProvider/i18n after** - Read from settingsStore (no duplicate API calls)

**Benefits:**
- Prevents 8+ simultaneous GET_GLOBAL calls during startup
- Ensures settings are loaded before theme/i18n initialize
- Clear provider hierarchy in one location
- Easy to maintain and understand initialization flow

### Logging Levels
```csharp
// Use correct log level based on frequency
_logger.Verbose($"Per-item detail");     // High-frequency
_logger.Info($"Step completed");         // Milestones
_logger.Warn($"Recoverable issue");      // Potential problems
```

### Caching with IMemoryCache

**Pattern** (follows CategoryService, ModQueryService):
```csharp
public class SomeService {
    private readonly IMemoryCache _cache;
    private readonly IProfileEventBus _eventBus;
    private readonly string _cacheKey;

    public SomeService(
        IMemoryCache cache,
        IProfileContext profileContext,
        IProfileEventBus eventBus) {
        _cache = cache;
        _eventBus = eventBus;

        // Use profile-specific cache key (IMemoryCache is singleton)
        _cacheKey = $"CacheName_{profileContext.ProfileId}";

        // Subscribe to events to invalidate cache
        _eventBus.Subscribe(ModuleNames.MOD, ModEvents.CACHE_CHANGED, _ => {
            _cache.Remove(_cacheKey);
            return Task.CompletedTask;
        });
    }

    public async Task<List<Data>> GetDataAsync() {
        // GetOrCreateAsync handles cache-first pattern cleanly
        return await _cache.GetOrCreateAsync(_cacheKey, async entry => {
            // IMPORTANT: Yield to ensure async execution and allow UI updates
            // Without this, IPC calls may block UI thread during cache creation
            await Task.Yield();

            // Cache miss - build data (slow path)
            return await BuildDataAsync();
        }) ?? new List<Data>();  // Fallback if null
    }
}
```

**Key Points:**
- Always use profile-specific cache keys (IMemoryCache is singleton shared across profiles)
- Subscribe to relevant events for automatic cache invalidation
- Use `GetOrCreateAsync` for cleaner cache-first pattern (preferred over TryGetValue/Set)
- **CRITICAL: Add `await Task.Yield()` at start of factory to prevent UI blocking**
- Event-driven invalidation is preferred over time-based expiration
- Use `_cache.Remove(key)` in event handlers to invalidate cache

**Why Task.Yield() is Critical:**
- Long-running cache factories block the IPC thread
- UI thread can't process state updates (e.g., loading spinners won't show)
- `Task.Yield()` forces execution to continue asynchronously
- Apply to: cache factories, migration operations, workflow tasks, category tree building

**When to Use Caching:**
- Expensive file system operations (scanning directories)
- Complex database queries with joins
- Data that changes infrequently but is accessed frequently
- Operations triggered by FileSystemWatcher events for cache invalidation

**Example: GetActiveModsAsync**
- First call: Scans cache folder (slow)
- Subsequent calls: Returns cached result (fast)
- Cache invalidated on CACHE_CHANGED event (mod load/unload/delete)
- Uses ModCacheWatcher FileSystemWatcher for automatic invalidation

### FileSystemWatcher for Cache Invalidation

**Pattern** (follows ModCacheWatcher):
```csharp
public class SomeWatcher : IDisposable {
    private readonly IProfileEventBus _eventBus;
    private FileSystemWatcher? _watcher;
    private readonly object _lock = new();

    public void StartWatching() {
        lock (_lock) {
            if (_watcher != null) return; // Already started

            _watcher = new FileSystemWatcher(directoryPath) {
                NotifyFilter = NotifyFilters.DirectoryName,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };

            _watcher.Deleted += OnFolderDeleted;
            _watcher.Renamed += OnFolderRenamed;
        }
    }

    private void OnFolderDeleted(object sender, FileSystemEventArgs e) {
        // Use fire-and-forget pattern (don't block FileSystemWatcher thread)
        _ = Task.Run(async () => {
            try {
                await _eventBus.EmitAsync(
                    ModuleNames.MOD,
                    ModEvents.CACHE_CHANGED,
                    new { ChangeType = "deleted" }
                );
            } catch (Exception ex) {
                _logger.Error($"Failed to emit event: {ex.Message}");
            }
        });
    }

    public void Dispose() {
        lock (_lock) {
            if (_watcher != null) {
                _watcher.EnableRaisingEvents = false;
                _watcher.Deleted -= OnFolderDeleted;
                _watcher.Renamed -= OnFolderRenamed;
                _watcher.Dispose();
                _watcher = null;
            }
        }
    }
}
```

**Key Points:**
- Use fire-and-forget `Task.Run` to avoid blocking FileSystemWatcher thread
- Emit events that trigger cache invalidation in other services
- Proper cleanup with lock synchronization
- NotifyFilter should be specific to avoid excessive events

**Example: ModCacheWatcher**
- Watches cache/Mods directory for folder changes
- Detects load/unload (rename) and delete operations
- Emits CACHE_CHANGED event consumed by ModQueryService
- ModQueryService invalidates IMemoryCache on event

---

## 🚨 Common Mistakes

### ❌ Don't Do This
```typescript
// 1. Manual error handling
catch (error) {
  notification.error((error as Error).message);
}

// 2. null for missing data
const [mod, setMod] = useState<ModInfo | null>(null);

// 3. Generic CSS without BEM
<div className="item">  // Missing component prefix

// 4. Process data in frontend
const filtered = mods.filter(...);  // Should be in backend

// 5. Skip RAG lookup
// *starts coding without checking docs*
```

### ✅ Do This Instead
```typescript
// 1. Use handleError
catch (error: unknown) {
  handleError(error);
}

// 2. undefined for missing data
const [mod, setMod] = useState<ModInfo>();

// 3. BEM with component prefix
<div className="mod-list-item">

// 4. Backend handles data
const mods = await modService.getFiltered(criteria);

// 5. Check RAG first
// Read KEYWORDS_INDEX.md → Load relevant doc → Write code
```

---

## 📚 RAG Document Structure

### Must-Read Before Coding
- `docs/KEYWORDS_INDEX.md` - Find where things are
- `docs/core/DESIGN_DECISIONS.md` - All architectural constraints
- `docs/ai-assistant/WORKFLOWS.md` - Implementation patterns

### Reference During Implementation
- `docs/ai-assistant/GUIDELINES.md` - Do's and don'ts
- `docs/ai-assistant/TROUBLESHOOTING.md` - Common errors
- `docs/keywords/HOW_TO.md` - Step-by-step guides

### Domain-Specific
- `docs/keywords/BACKEND.md` - C# services/facades
- `docs/keywords/FRONTEND.md` - React components/hooks
- `docs/architecture/*.md` - System architecture

---

## 🎨 UI/CSS Guidelines

### BEM Naming (Component CSS)
```css
/* ComponentName.css */
.mod-list-panel { }                    /* Block */
.mod-list-panel-item { }               /* Element */
.mod-list-panel-item--selected { }     /* Modifier */
```

### Font Sizes
- **Regular text**: 14px (body text, labels)
- **Small text**: 12px (secondary info)
- **NEVER** use 13px or below 12px

### classnames Library
```typescript
// ✅ Always use classnames for conditionals
import classNames from 'classnames';

className={classNames('mod-list-item', {
  'mod-list-item--selected': isSelected,
  'mod-list-item--disabled': isDisabled,
})}

// ❌ Don't use template strings
className={`mod-list-item ${isSelected ? 'selected' : ''}`}
```

---

## 🔄 Workflow for New Feature

1. **Load RAG docs**
   ```bash
   Read: docs/KEYWORDS_INDEX.md
   → Find relevant module docs
   → Load WORKFLOWS.md for patterns
   ```

2. **Check constraints**
   ```bash
   Read: docs/core/DESIGN_DECISIONS.md
   → Verify approach follows architecture
   ```

3. **Implement**
   ```bash
   Backend: Service → Repository → Facade
   Frontend: Service → Hook → Component
   ```

4. **Error handling**
   ```bash
   Backend: throw new OperationException(code, parameters)
   Frontend: catch + handleError(error)
   i18n: Add errors.{CODE} to en.json and cn.json
   ```

5. **Test & commit**
   ```bash
   Build → Test → Ask user → Commit
   ```

---

## 📊 Token Optimization Strategy

### Load Docs in Order
1. **Start narrow**: `KEYWORDS_INDEX.md` → specific domain file
2. **Expand if needed**: Load related architecture docs
3. **Reference only**: Don't load entire guide into context

### When to Load What
- **First time working on module**: Load module architecture + WORKFLOWS.md
- **Adding feature**: Load HOW_TO.md + existing similar code
- **Fixing bug**: Load TROUBLESHOOTING.md + module-specific docs
- **Architecture question**: Load DESIGN_DECISIONS.md

---

## 🎯 Session Workflow

### Starting a Session
1. Check git status (`git status`)
2. Load `AI_GUIDE.md` (this file)
3. Ask user: "What would you like to work on?"
4. **Use RAG**: Load relevant docs from KEYWORDS_INDEX.md

### During Development
1. **Check RAG first** before making architectural decisions
2. Follow loaded patterns exactly
3. Use correct error handling (handleError utility)
4. Log at appropriate levels (Verbose for details, Info for milestones)

### Before Committing
1. Build succeeds
2. No hardcoded values
3. Error handling in place
4. **Ask user**: "Ready to commit these changes?"
5. Wait for explicit approval

---

## ⚡ Quick Command Reference

### Git
```bash
git status                    # Always check first
git add <files>              # Stage specific files
git commit -m "message"      # Only after user approval
```

### Build
```bash
# Production build (automated)
.\build-production.ps1

# Development build
dotnet build <project>.csproj        # Backend
cd <frontend> && npm run build       # Frontend
```

### Common Tasks
| Task | First Doc to Load |
|------|------------------|
| Build production release | `how-to/BUILD_AND_DEPLOY.md` |
| Add new module | `MODULE_ARCHITECTURE.md` |
| Add IPC handler | `WORKFLOWS.md` |
| Fix IPC issue | `TROUBLESHOOTING.md` |
| Add React component | `FRONTEND.md` + `WORKFLOWS.md` |
| Add backend service | `BACKEND.md` + `WORKFLOWS.md` |

---

## 🔍 Key Takeaways

### 1. RAG System is Mandatory
- **ALWAYS** check KEYWORDS_INDEX.md before coding
- **ALWAYS** load DESIGN_DECISIONS.md for architectural questions
- **ALWAYS** load WORKFLOWS.md for implementation patterns

### 2. Error Handling
- Backend: `throw new OperationException(code, parameters, message)`
- Frontend: `catch (error: unknown) { handleError(error); }`
- Display stored errors: `translateErrorMessage(errorString)`
- All errors use unified `errors.{CODE}` i18n pattern

### 3. Module Boundaries
- **NEVER** access other module's repositories
- **ALWAYS** use module facades for IPC
- **ALWAYS** inject dependencies via constructor

### 4. Concurrency & File Operations
- **Per-Resource Locking**: Use operation queue pattern (see ModOperationQueue) for file operations
- **Retry Logic**: Implement exponential backoff for transient IOException (file locks)
- **Non-Blocking UI**: Queue operations, return immediately, don't disable UI
- Example: Mod load/unload uses semaphores per SHA to prevent concurrent operations on same mod

### 5. UI Consistency
- Use BEM naming for component CSS
- Use classnames library for conditionals
- Font sizes: 12px or 14px only
- Load `visual-enhancements.css` for global utilities

### 6. Git Discipline
- **ALWAYS** ask before committing
- **NEVER** commit without user approval
- Include clear commit messages

---

## 🔧 Batch Edit Feature Pattern (v3.3)

### Overview
Spreadsheet-style batch metadata editor using AG Grid with VSCode-style find/replace panel.

### Key Components

**BatchEditModsScreen** (`BatchEditModsScreen.tsx`)
- Slide-in screen wrapper managing state and toolbar
- Tracks edited mods vs original mods for change detection
- Global search/replace across all text columns (name, author, description)

**BatchEditGrid** (`BatchEditGrid.tsx`)
- AG Grid with custom theming using `themeQuartz.withParams()`
- Custom cell renderers for inline text highlighting
- Uses `getRowId` with SHA to track mods through sorting
- Row height: 39px, Header height: 39px

**FindReplacePanel** (`FindReplacePanel.tsx`)
- VSCode-style dropdown panel at top-right of grid
- Inline toggle buttons (Aa, .*) inside input box
- 24×24px square icon buttons for consistency
- Focus border on input-group container, not individual input
- Debounced search (300ms) for performance

**HighlightCellRenderer** (`HighlightCellRenderer.tsx`)
- Custom AG Grid cell renderer for inline text highlighting
- Highlights matching text with `<mark>` tags
- Uses CSS variable `--search-highlight-bg` (theme-aware)
- Supports both plain text and regex search

### Color Variables Pattern
```css
/* Define base variable */
.batch-edit-grid {
  --search-highlight-bg: rgba(255, 235, 59, 0.4);
}

/* Theme-specific overrides */
:root:not(.dark) .batch-edit-grid {
  --search-highlight-bg: #ffeb3b80;  /* Light theme */
}

:root.dark .batch-edit-grid {
  --search-highlight-bg: rgba(255, 235, 59, 0.3);  /* Dark theme */
}
```

### AG Grid Theming Pattern
```typescript
// Use new Theming API, NOT legacy CSS
import { themeQuartz } from 'ag-grid-community';

const customTheme = themeQuartz.withParams({
  backgroundColor: 'var(--color-bg-container)',
  foregroundColor: 'var(--color-text-base)',
  borderColor: 'var(--color-border-secondary)',
  headerBackgroundColor: 'var(--color-bg-elevated)',
  rowBorder: true,
  borderRadius: 0,
  wrapperBorder: false,
});

<AgGridReact theme={customTheme} ... />
```

### Search/Replace Pattern
```typescript
// Global search across columns
const searchableColumns: Array<'name' | 'author' | 'description'> =
  ['name', 'author', 'description'];

const updated = editedMods.map(mod => {
  const updatedMod = { ...mod };
  searchableColumns.forEach(column => {
    const value = mod[column];
    if (typeof value !== 'string') return;
    // Perform replace on value
    if (newValue !== value) {
      updatedMod[column] = newValue;
    }
  });
  return updatedMod;
});
```

### Debounced Search Pattern
```typescript
import { debounce } from 'lodash-es';

const debouncedSearchChange = useCallback(
  debounce((searchConfig: ReplaceConfig | null) => {
    if (onSearchChange) {
      onSearchChange(searchConfig);
    }
  }, 300),  // 300ms debounce
  [onSearchChange]
);

// Cleanup
useEffect(() => {
  return () => {
    debouncedSearchChange.cancel();
  };
}, [debouncedSearchChange]);
```

### VSCode-Style Input Focus Pattern
```css
/* Focus on container, not input */
.find-replace-input-group {
  border: 1px solid var(--color-border-base);
  transition: border-color 0.2s;
}

.find-replace-input-group:focus-within {
  border-color: var(--color-primary);
}

/* Remove all input focus styling */
.find-replace-input-group .ant-input:focus {
  outline: none !important;
  border: none !important;
  border-color: transparent !important;
  box-shadow: none !important;
}
```

### Icon Button Consistency
```css
/* All icon buttons must be square and same size */
.find-replace-close,
.find-replace-icon-button,
.find-replace-checkbox-inline {
  height: 24px !important;
  width: 24px !important;
  min-width: 24px !important;
  padding: 0 !important;
  /* Add !important to override Ant Design defaults */
}
```

### Key Lessons
1. **AG Grid Theming API** - Use `themeQuartz.withParams()` instead of CSS hacks
2. **Color Variables** - Use project's `--color-*` convention, NOT `--ant-color-*`
3. **Inline Highlighting** - Use custom cell renderer with `<mark>` tags, not cell background
4. **Focus Styling** - Put focus on container (`:focus-within`), remove from input
5. **Button Sizing** - Use inline styles + `!important` to override Ant Design
6. **Debouncing** - Always debounce search input for performance (300ms minimum)
7. **Global Search** - Search across all relevant columns, not just one column

---

## 📖 Remember

> **The RAG system contains the complete truth.**
> **This guide is just a quick reference.**
> **When in doubt, load the relevant documentation.**

### Priority Order
1. **RAG docs** (source of truth)
2. **Existing code** (working examples)
3. **This guide** (quick reference only)

---

**End of Guide - Now use the RAG system! 🚀**

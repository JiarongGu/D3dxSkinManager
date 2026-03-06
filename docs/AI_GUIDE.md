# AI Assistant Guide

**Version:** 3.1
**Last Updated:** 2026-03-06
**Critical:** NEVER commit without explicit user approval!

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

### 3. Error Handling
```typescript
// ✅ Always use handleError utility
import { handleError } from '@/shared/utils/errorHandler';
try {
  await operation();
} catch (error: unknown) {
  handleError(error);  // Shows user-friendly message
}

// ❌ Never manually extract error messages
catch (error) {
  notification.error((error as Error).message);  // WRONG
}
```

### 4. Data Conventions
```typescript
// ✅ undefined for missing data
const [mod, setMod] = useState<ModInfo>();

// ✅ null ONLY for React render returns
if (!data) return null;
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
    private readonly IModArchiveService _archiveService;
    private readonly IModCacheService _cacheService;
    private readonly IProfileEventBus _eventBus;  // ✅ Inject EventBus
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
        // Business logic: category conflict resolution, extraction, etc.
        var mod = await _repository.GetByIdAsync(sha);

        // Unload conflicting mods in same category
        await HandleCategoryConflicts(mod);

        // Enable cache or extract archive
        var success = await _cacheService.EnableCacheAsync(sha)
                   || await ExtractArchive(sha);

        if (success) {
            // ✅ Service emits event after successful operation
            await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.LOADED, new { Sha = sha });
        }

        return new ModLoadResult { Success = success };
    }
}

// 3. Register in {Module}ServiceExtensions.cs
services.AddSingleton<IModLifecycleService, ModLifecycleService>();
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
const handleModListUpdate = useCallback(
  debounce(() => {
    if (!selectedProfileId) return;
    void modOps.refreshMods(selectedProfileId);  // Reload mod list
    void statisticsOps.loadStatistics(selectedProfileId);  // Reload statistics
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
  (event) => console.log(event.payload.sha)
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
   Backend: throw ModException(ErrorCodes.X)
   Frontend: catch + handleError(error)
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
- Backend: `throw ModException(ErrorCodes.X, message, context)`
- Frontend: `catch (error: unknown) { handleError(error); }`

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

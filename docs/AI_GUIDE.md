# AI Assistant Guide

**Version:** 2.2
**Last Updated:** 2026-02-25
**Critical:** NEVER commit without explicit user approval!

---

## 🚀 Quick Start RAG Router

### What do you need?

| Task | Primary Doc | Fallback |
|------|------------|----------|
| **"How is X architected?"** | [DESIGN_DECISIONS.md](core/DESIGN_DECISIONS.md) | [CURRENT_ARCHITECTURE.md](architecture/CURRENT_ARCHITECTURE.md) |
| **"Where is X located?"** | [KEYWORDS_INDEX.md](KEYWORDS_INDEX.md) → domain file | [PROJECT_STRUCTURE.md](core/PROJECT_STRUCTURE.md) |
| **"How do I create X?"** | [WORKFLOWS.md](ai-assistant/WORKFLOWS.md) | [HOW_TO.md](keywords/HOW_TO.md) |
| **"X is not working"** | [TROUBLESHOOTING.md](ai-assistant/TROUBLESHOOTING.md) | [GUIDELINES.md](ai-assistant/GUIDELINES.md) |
| **"What changed?"** | [CHANGELOG.md](CHANGELOG.md) | Git log |

---

## 🛡️ Critical Rules (MUST FOLLOW)

### 1. Git Commits
```bash
# ALWAYS ask before committing
"Ready to commit these changes?"  # WAIT for explicit "yes"
```

### 2. Architectural Constraints
```csharp
// Backend: ALL heavy operations on C# side
// Frontend: UI only, NO data processing
// IPC: Through centralized AppFacade
// Paths: Relative in DB, absolute at runtime
// DI: Use AddSingleton (no factory functions in helper)
```

### 3. Data Conventions
```typescript
// ✅ undefined for missing data (NOT null)
const [mod, setMod] = useState<ModInfo>();  // undefined by default

// ✅ null ONLY for React render returns
if (!data) return null;  // React component return

// ✅ Error handling with ErrorCode system
import { handleError, isErrorCode, ErrorCodes } from '@/shared/utils/errorHandler';

try {
  await modService.loadMod(profileId, sha);
} catch (error: unknown) {
  // handleError shows user-friendly message and returns structured error
  const modError = handleError(error);

  // Optional: Check for specific error codes
  if (isErrorCode(error, ErrorCodes.MOD_FOLDER_IN_USE)) {
    // Handle specific error case
  }
}
```

### 4. Module Boundaries
- **NEVER** access repositories from other modules
- **NEVER** bypass service layer
- **ALWAYS** inject dependencies via constructor
- **ALWAYS** use module facades for IPC routing

---

## 📊 Implementation Patterns

### Logging Levels

Backend and frontend both support these log levels (in order of verbosity):
- **VERBOSE (0)**: High-frequency events (mouse movements, IPC messages, frequent operations)
- **DEBUG (1)**: Development diagnostics
- **INFO (2)**: Normal operations (default in dev mode)
- **WARN (3)**: Potential issues
- **ERROR (4)**: Failures/exceptions
- **ALL (-1)**: Show everything including VERBOSE
- **OFF (-2)**: Disable all logging

**Important**: Console output in development mode respects the log level setting (defaults to INFO). This means VERBOSE and DEBUG logs won't appear in console unless explicitly enabled.

### Error Handling Pattern

**Backend: Throw ModException with ErrorCode**
```csharp
using D3dxSkinManager.Modules.Core.Models;

// In service method:
if (Directory.Exists(modPath) && IsDirectoryInUse(modPath))
{
    throw new ModException(
        ErrorCodes.MOD_FOLDER_IN_USE,
        "Mod folder is currently in use",
        new { modPath }  // Optional context data
    );
}

// BaseFacade automatically catches and formats error response with:
// - errorCode: "MOD_FOLDER_IN_USE"
// - data: { modPath: "..." }
```

**Frontend: Use handleError utility**
```typescript
import { handleError, isErrorCode, ErrorCodes } from '@/shared/utils/errorHandler';

try {
  await modService.loadMod(profileId, sha);
  notification.success('Mod loaded successfully');
} catch (error: unknown) {
  // handleError automatically:
  // 1. Shows user-friendly message from error code mapping
  // 2. Returns structured ModOperationError
  const modError = handleError(error);

  // Optional: Handle specific error codes
  if (isErrorCode(error, ErrorCodes.MOD_FOLDER_IN_USE)) {
    // Custom handling for this specific error
    console.log('Mod folder is locked');
  }
}
```

**Adding New Error Codes**
1. Add to `ErrorCodes.cs` (backend)
2. Add to `errorCodes.ts` (frontend)
3. Add user message to `ERROR_MESSAGES` in `errorHandler.ts`
4. Add i18n keys to `en.json` and `cn.json` (optional, for translated messages)

### Backend Service Pattern
```csharp
// 1. Interface in I{Name}Service.cs
public interface IModService {
    Task<List<ModInfo>> GetAllAsync();
}

// 2. Implementation in {Name}Service.cs
public class ModService : IModService {
    private readonly IModRepository _repository;

    public ModService(IModRepository repository) {
        _repository = repository;  // DI injection
    }
}

// 3. Registration in {Module}ServiceExtensions.cs
public static IServiceCollection AddModsServices(this IServiceCollection services) {
    services.AddSingleton<IModRepository, ModRepository>();
    services.AddSingleton<IModService, ModService>();
    services.AddSingleton<IModFacade, ModFacade>();
    return services;
}
```

### Frontend Service Pattern
```typescript
// Extend BaseModuleService with module name
class ModService extends BaseModuleService {
  constructor() {
    super('MOD');  // Module name FIXED
  }

  async getAllMods(profileId?: string): Promise<ModInfo[]> {
    return this.sendArrayMessage<ModInfo>('GET_ALL', profileId);
  }

  async loadMod(profileId: string, sha: string): Promise<boolean> {
    return this.sendBooleanMessage('LOAD', profileId, { sha });
  }
}

export const modService = new ModService();
```

### IPC Message Handling
```csharp
// Module Facade routes messages
public class ModFacade : IModFacade {
    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request) {
        try {
            object? responseData = request.Type switch {
                "GET_ALL" => await GetAllModsAsync(request.ProfileId),
                "LOAD" => await LoadModAsync(request),
                _ => throw new InvalidOperationException($"Unknown: {request.Type}")
            };
            return MessageResponse.CreateSuccess(request.Id, responseData);
        }
        catch (Exception ex) {
            return MessageResponse.CreateError(request.Id, ex.Message);
        }
    }
}
```

### IPC Event Notifications

**CRITICAL: Event Naming Convention**

All IPC notifications use **SCREAMING_SNAKE_CASE constants** defined at **module level**:

```csharp
// Backend: Define event constants at module level
// Example: Composition/DropZoneEvents.cs
namespace D3dxSkinManager.Composition;

public static class DropZoneEvents
{
    public const string CLICK = "DROP_ZONE_CLICK";
    public const string DRAG_ENTER = "DROP_ZONE_DRAG_ENTER";
    public const string DRAG_LEAVE = "DROP_ZONE_DRAG_LEAVE";
    public const string FILE_DROP = "DROP_ZONE_FILE_DROP";
    public const string MOUSE_ENTER = "DROP_ZONE_MOUSE_ENTER";
    public const string MOUSE_LEAVE = "DROP_ZONE_MOUSE_LEAVE";
}

// Example: Modules/Mods/ModEvents.cs
namespace D3dxSkinManager.Modules.Mods;

public static class ModEvents
{
    public const string MOD_LOADED = "MOD_LOADED";
    public const string MOD_UNLOADED = "MOD_UNLOADED";
    public const string MOD_DELETED = "MOD_DELETED";
    public const string CLASSIFICATION_TREE_CHANGED = "CLASSIFICATION_TREE_CHANGED";
    public const string CUSTOM_EVENT = "CUSTOM_EVENT";
}

// Core events (Modules/Core/Event/CoreEvents.cs)
public static class CoreEvents
{
    public const string APPLICATION_STARTED = "APPLICATION_STARTED";
    public const string APPLICATION_SHUTDOWN = "APPLICATION_SHUTDOWN";
    public const string MOD_LOADED = "MOD_LOADED";
    // ... etc

    public static readonly string[] All = new[] { APPLICATION_STARTED, APPLICATION_SHUTDOWN, ... };
}

// ✅ CORRECT: Use module-level constants
_ipcHandler.SendNotification(DropZoneEvents.CLICK, new { zoneId, position });
_eventEmitter.EmitAsync(ModEvents.MOD_LOADED, data: new { Sha = sha });

// ❌ WRONG: Don't use string literals
_ipcHandler.SendNotification("DROP_ZONE_CLICK", new { zoneId, position });
_eventEmitter.EmitAsync("MOD_LOADED", data: new { Sha = sha });
```

```typescript
// Frontend: EventType enum maps notification names (all SCREAMING_SNAKE_CASE)
export enum EventType {
  // Standard events: PascalCase = SCREAMING_SNAKE_CASE
  ApplicationStarted = 'APPLICATION_STARTED',
  ModLoaded = 'MOD_LOADED',
  ClassificationTreeChanged = 'CLASSIFICATION_TREE_CHANGED',

  // Custom notifications: PascalCase = SCREAMING_SNAKE_CASE
  DropZoneClick = 'DROP_ZONE_CLICK',
  DropZoneDragEnter = 'DROP_ZONE_DRAG_ENTER',
  DropZoneDragLeave = 'DROP_ZONE_DRAG_LEAVE',
  DropZoneFileDrop = 'DROP_ZONE_FILE_DROP',
  DropZoneMouseEnter = 'DROP_ZONE_MOUSE_ENTER',
  DropZoneMouseLeave = 'DROP_ZONE_MOUSE_LEAVE',
}

// ✅ CORRECT: Subscribe using the EventType enum
import { eventBus, EventType } from '../services/eventBus';

eventBus.on(EventType.DropZoneClick, (event) => {
  console.log(event.data);  // { zoneId, position }
});

// ❌ WRONG: Don't use string literals
eventBus.on('DROP_ZONE_CLICK', (event) => {  // Type-unsafe, avoid!
  console.log(event.data);
});
```

**When to Add New Event Types:**

1. **Create/Update module event constants file** → e.g., `Modules/YourModule/YourModuleEvents.cs`
   ```csharp
   public static class YourModuleEvents
   {
       public const string NEW_EVENT = "NEW_EVENT";
   }
   ```

2. **Frontend EventType enum** → Add matching entry in `eventBus.ts`
   ```typescript
   export enum EventType {
       NewEvent = 'NEW_EVENT',  // PascalCase = SCREAMING_SNAKE_CASE
   }
   ```

3. **If core system event** → Add to `CoreEvents.All` array for automatic EventBusIpcBridge subscription

4. **Document in this guide** to maintain consistency

**Module Event Constants Files:**
- `Composition/DropZoneEvents.cs` - Drop zone events
- `Modules/Mods/ModEvents.cs` - Mod operation events
- `Modules/Profiles/ProfileEvents.cs` - Profile events
- `Modules/Tools/ToolsEvents.cs` - Tools events
- `Modules/Migration/MigrationEvents.cs` - Migration events
- `Modules/Context/ContextEvents.cs` - Context lifecycle events
- `Modules/Plugins/PluginEvents.cs` - Plugin events
- `Modules/Core/Event/CoreEvents.cs` - Core system events

**Naming Pattern:**
- Backend constant: `DROP_ZONE_CLICK` → Frontend enum: `DropZoneClick = 'DROP_ZONE_CLICK'`
- Both use SCREAMING_SNAKE_CASE strings for the actual event name
- Frontend enum keys use PascalCase for TypeScript convention
- Module event class naming: `{ModuleName}Events` (e.g., `ModEvents`, `DropZoneEvents`)

**Event Structure:**
```typescript
interface Event<T = unknown> {
  type: EventType;      // Matches EventType enum value
  eventName?: string;   // For CustomEvent only
  data?: T;            // Event payload
}
```

### State Management Pattern (Zustand)

**CURRENT ARCHITECTURE:** We use Zustand for global state management, NOT React Context.

```typescript
// 1. Zustand Store (store/modsStore.ts)
export const useModsStore = create<ModsState>((set, get) => ({
  // State
  mods: [],
  modsLoading: false,

  // Actions
  setMods: (mods) => set({ mods }),
  setModsLoading: (loading) => set({ modsLoading: loading }),

  // Reset
  reset: () => set(initialState),
}));

// 2. Provider handles lifecycle only (ModsProvider.tsx)
export const ModsProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const { selectedProfileId } = useProfile();
  const reset = useModsStore((state) => state.reset);

  // Subscribe to backend events (NO module-level eventBus anymore)
  useEffect(() => {
    if (!selectedProfileId) return;

    const unsubscribeModsRefreshed = eventBus.on(EventType.ModsRefreshed, () => {
      modOps.refreshMods(selectedProfileId);
    });

    return () => {
      unsubscribeModsRefreshed();
    };
  }, [selectedProfileId]);

  // Load data on profile change
  useEffect(() => {
    if (selectedProfileId) {
      void Promise.all([
        modOps.loadMods(selectedProfileId),
        classificationOps.loadClassificationTree(selectedProfileId),
      ]);
    } else {
      reset();
    }
  }, [selectedProfileId, reset]);

  return <>{children}</>;
};

// 3. Components use hooks directly
function MyComponent() {
  const mods = useModsStore((state) => state.mods);
  const setMods = useModsStore((state) => state.setMods);

  // Call operations directly
  const handleDelete = async (sha: string) => {
    await modOps.deleteMod(profileId, sha);
  };
}
```

**Key Differences from Old Context Pattern:**
- ✅ Zustand for state (not React Context)
- ✅ Operations are imported functions (not context methods)
- ✅ Backend events subscribed in Provider (not module-level eventBus)
- ✅ Provider only handles lifecycle, not state management
- ❌ NO module-level event subscriptions anymore
- ❌ NO React Context with state passed through

### Repository Pattern
```csharp
public interface IModRepository {
    Task<List<ModInfo>> GetAllAsync();
    Task<ModInfo?> GetByIdAsync(string id);
    Task AddAsync(ModInfo mod);
    Task UpdateAsync(ModInfo mod);
    Task DeleteAsync(string id);
}

// Implementation with EF Core
public class ModRepository : IModRepository {
    private readonly AppDbContext _context;

    public async Task<List<ModInfo>> GetAllAsync() {
        return await _context.Mods.ToListAsync();
    }
}
```

### Progress Reporting (Operations > 1 second)
```csharp
// Backend
var operationId = Guid.NewGuid().ToString();
_progressReporter.CreateOperation(operationId, "Loading mod", OperationType.ModLoad);
_progressReporter.UpdateProgress(operationId, 50, "Extracting files...");
_progressReporter.CompleteOperation(operationId, "Mod loaded successfully");

// Frontend shows notification automatically
```

### Modal Pattern (CRITICAL)
```typescript
// ✅ CORRECT: Declarative, no transitions
<Modal
  open={visible}
  transitionName=""      // REQUIRED: Empty string
  maskTransitionName=""  // REQUIRED: Empty string
  centered
>
  <Content />
</Modal>

// ❌ WRONG: Causes flashing
<Modal visible={visible}>  // Old prop name
```

---

## 📊 RAG Retrieval Strategy

### Step 1: Identify Intent
```
User Query → Intent Classification:
- ARCHITECTURAL → Load DESIGN_DECISIONS.md
- LOCATION → Load KEYWORDS_INDEX.md → domain file
- IMPLEMENTATION → Load WORKFLOWS.md
- DEBUGGING → Load TROUBLESHOOTING.md
```

### Step 2: Domain Routing
```
KEYWORDS_INDEX.md routes to:
├── keywords/BACKEND.md - C# services/facades
├── keywords/FRONTEND.md - React components/hooks
├── keywords/HOW_TO.md - Step-by-step guides
└── keywords/DOCUMENTATION.md - Doc locations
```

### Step 3: Load Minimal Context
- Start with ONE document
- Only load additional if needed
- Prefer specific over general

---

## 📁 Essential Documents

### Architecture
- **[DESIGN_DECISIONS.md](core/DESIGN_DECISIONS.md)** - 18 architectural decisions
- **[MODULE_ARCHITECTURE.md](architecture/MODULE_ARCHITECTURE.md)** - Module structure
- **[CURRENT_ARCHITECTURE.md](architecture/CURRENT_ARCHITECTURE.md)** - System overview

### Implementation
- **[WORKFLOWS.md](ai-assistant/WORKFLOWS.md)** - Code generation patterns
- **[GUIDELINES.md](ai-assistant/GUIDELINES.md)** - Do's and don'ts
- **[REACT_CLOSURE_PATTERNS.md](ai-assistant/REACT_CLOSURE_PATTERNS.md)** - useStableRef

### Reference
- **[KEYWORDS_INDEX.md](KEYWORDS_INDEX.md)** - Component/service router
- **[PROJECT_STRUCTURE.md](core/PROJECT_STRUCTURE.md)** - File organization
- **[TROUBLESHOOTING.md](ai-assistant/TROUBLESHOOTING.md)** - Common errors

---

## 🔍 Quick Reference

### Tech Stack
- Backend: .NET 10, C#, WinForms + WebView2
- Frontend: React 18, TypeScript 4.9, Vite 5
- Database: SQLite + EF Core
- IPC: JSON messages via WebView2

### Module Names
```typescript
type ModuleName = 'MOD' | 'PROFILE' | 'SETTINGS' | 'SYSTEM' |
                  'TOOLS' | 'PLUGINS' | 'WAREHOUSE' | 'MIGRATION' | 'LAUNCH';
```

### Key Services
- Backend: IGlobalPathService, IModService, IProfileService
- Frontend: modService, profileService, bridgeService

### CSS Variables (Dark Theme)
```css
--primary-color: #1890ff
--background-color: #141414
--card-background: #1f1f1f
--text-color: rgba(255, 255, 255, 0.85)
```

### Font Size Guidelines
- **Regular text**: 14px (default for descriptions, body text, form inputs, labels)
- **Small text**: 12px (minimum allowed, for secondary info, hints, descriptions in compact variants)
- **NEVER** go below 12px for readability
- **NEVER** use 13px or other intermediate sizes - stick to 12px or 14px only

### Notification Animation Guidelines
- Notifications are positioned at **top center** with `placement: 'top'`
- Use **smooth slide-in from top** animation (0.3s ease-out)
- **NEVER** use default Ant Design motion (causes jiggle)
- Custom CSS animation overrides all default transitions in `custom-notification.css`
- Position: 24px from top, centered horizontally

---

## 🎯 Common Tasks

| Task | Pattern | Load Docs |
|------|---------|-----------|
| Add backend service | See Backend Service Pattern above | WORKFLOWS.md |
| Add React component | See Frontend Service Pattern above | WORKFLOWS.md |
| Fix IPC issue | Check AppFacade routing | TROUBLESHOOTING.md |
| Add new module | Create facade, service, registration | MODULE_ARCHITECTURE.md |
| Handle errors | Use `error: unknown` pattern | GUIDELINES.md |
| Add progress | Use IProgressReporter for >1s ops | OPERATION_NOTIFICATION_SYSTEM.md |

---

## ⚠️ Anti-Patterns to Avoid

1. **NEVER** bypass service layer to access repositories
2. **NEVER** use `null` for missing data (use `undefined`)
3. **NEVER** process data in frontend (backend only)
4. **NEVER** use imperative modals (causes flashing)
5. **NEVER** hardcode colors (use CSS variables)
6. **NEVER** use `(error as Error).message` - use `handleError()` from errorHandler
7. **NEVER** commit without user approval
8. **NEVER** use factory functions with AddSingleton helper
9. **NEVER** access other module's repositories directly
10. **NEVER** put business logic in facades (services only)
11. **NEVER** use font sizes below 12px or between 12px and 14px (only use 12px or 14px)

### ❌ Wrong Error Handling
```typescript
// ❌ DON'T: Manual error message extraction
catch (error) {
  notification.error('Failed: ' + (error as Error).message);
}

// ❌ DON'T: Missing error code handling
catch (error: unknown) {
  const msg = error instanceof Error ? error.message : 'Unknown';
  notification.error(msg);
}
```

### ✅ Correct Error Handling
```typescript
// ✅ DO: Use handleError utility
import { handleError } from '@/shared/utils/errorHandler';

catch (error: unknown) {
  handleError(error);  // Shows user-friendly message automatically
}
```

---

## 📝 Session Checklist

### Starting a Session
- [ ] Check current git status
- [ ] Review CHANGELOG.md for recent changes
- [ ] Load AI_GUIDE.md first
- [ ] Identify task category (use RAG router)

### Before Committing
- [ ] All tests passing
- [ ] No hardcoded values
- [ ] Error handling in place
- [ ] Ask user: "Ready to commit?"

### After Implementation
- [ ] Update KEYWORDS_INDEX if added components
- [ ] Update TROUBLESHOOTING if fixed bugs
- [ ] Update CHANGELOG.md if significant change

---

## 📊 Token Optimization

- Load ONE document first
- Use KEYWORDS_INDEX for routing
- Prefer specific docs over general
- DESIGN_DECISIONS.md has ALL constraints

---

**Remember:** This guide has the critical patterns. For detailed implementation, follow the RAG routing to specialized documents!
# AI Assistant Guide

**Version:** 2.3
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

**CRITICAL: Module + Type Pattern (Mirrors IpcRequest)**

All events use **Module + Type pattern** matching IpcRequest structure. Events have NO module prefixes in type names.

#### Backend Event System

```csharp
// Event Structure (Modules/Core/Event/EventMessage.cs)
public class EventMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Module { get; set; } = string.Empty;  // e.g., "MOD", "TASK_QUEUE"
    public string Type { get; set; } = string.Empty;    // e.g., "LOADED", "PROGRESS"
    public object? Payload { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

// Module Names (Modules/Core/Event/ModuleNames.cs)
public static class ModuleNames
{
    public const string CORE = "CORE";
    public const string DROP_ZONE = "DROP_ZONE";
    public const string MOD = "MOD";
    public const string PROFILE = "PROFILE";
    public const string TASK_QUEUE = "TASK_QUEUE";
    public const string SETTING = "SETTING";
    public const string MIGRATION = "MIGRATION";
    public const string TOOL = "TOOL";
    public const string PLUGIN = "PLUGIN";
}

// Event Type Constants - NO module prefix!
// Example: Modules/Mods/ModEvents.cs
public static class ModEvents
{
    public const string LOADED = "LOADED";                  // NOT "MOD_LOADED"
    public const string UNLOADED = "UNLOADED";
    public const string DELETED = "DELETED";
    public const string IMPORTED = "IMPORTED";
    public const string CLASSIFICATION_TREE_CHANGED = "CLASSIFICATION_TREE_CHANGED";
}

// Example: Modules/TaskQueue/TaskQueueEvents.cs
public static class TaskQueueEvents
{
    public const string ADDED = "ADDED";                    // NOT "TASK_ADDED"
    public const string STARTED = "STARTED";
    public const string PROGRESS = "PROGRESS";
    public const string COMPLETED = "COMPLETED";
}

// Example: Composition/DropZoneEvents.cs
public static class DropZoneEvents
{
    public const string CLICK = "CLICK";                    // NOT "DROP_ZONE_CLICK"
    public const string DRAG_ENTER = "DRAG_ENTER";
    public const string FILE_DROP = "FILE_DROP";
}

// Core Events (Modules/Core/Event/CoreEvents.cs)
// ⚠️ CRITICAL: Only application lifecycle events
public static class CoreEvents
{
    public const string APPLICATION_STARTED = "APPLICATION_STARTED";
    public const string APPLICATION_SHUTDOWN = "APPLICATION_SHUTDOWN";
    public const string LOG_LEVEL_CHANGED = "LOG_LEVEL_CHANGED";
}

// ✅ CORRECT: Emit with Module + Type
await _eventEmitter.EmitAsync(ModuleNames.MOD, ModEvents.LOADED, new { Sha = sha });
await _eventEmitter.EmitAsync(ModuleNames.TASK_QUEUE, TaskQueueEvents.PROGRESS, progressData);
_ipcHandler.SendNotification(ModuleNames.DROP_ZONE, DropZoneEvents.CLICK, new { zoneId, position });

// ❌ WRONG: Don't use string literals or old pattern
await _eventEmitter.EmitAsync("MOD_LOADED", data: new { Sha = sha });
_ipcHandler.SendNotification("DROP_ZONE_CLICK", new { zoneId, position });
```

#### Frontend Event System

```typescript
// Event Structure (src/shared/services/eventBus.ts)
export enum Module {
  CORE = 'CORE',
  MOD = 'MOD',
  TASK_QUEUE = 'TASK_QUEUE',
  DROP_ZONE = 'DROP_ZONE',
  SETTING = 'SETTING',
  PROFILE = 'PROFILE',
  MIGRATION = 'MIGRATION',
  TOOL = 'TOOL',
  PLUGIN = 'PLUGIN',
}

// Separate enums per module - NO module prefix!
export enum CoreEventType {
  APPLICATION_STARTED = 'APPLICATION_STARTED',
  APPLICATION_SHUTDOWN = 'APPLICATION_SHUTDOWN',
  LOG_LEVEL_CHANGED = 'LOG_LEVEL_CHANGED',
}

export enum ModEventType {
  LOADED = 'LOADED',                          // NOT 'MOD_LOADED'
  UNLOADED = 'UNLOADED',
  DELETED = 'DELETED',
  IMPORTED = 'IMPORTED',
  CLASSIFICATION_TREE_CHANGED = 'CLASSIFICATION_TREE_CHANGED',
}

export enum TaskQueueEventType {
  ADDED = 'ADDED',                            // NOT 'TASK_ADDED'
  STARTED = 'STARTED',
  PROGRESS = 'PROGRESS',
  COMPLETED = 'COMPLETED',
}

export enum DropZoneEventType {
  CLICK = 'CLICK',                            // NOT 'DROP_ZONE_CLICK'
  DRAG_ENTER = 'DRAG_ENTER',
  FILE_DROP = 'FILE_DROP',
}

// Type-safe payload mapping
export interface EventPayloadMap {
  [Module.MOD]: {
    [ModEventType.LOADED]: { sha: string };
    [ModEventType.IMPORTED]: ModInfo;
    [ModEventType.DELETED]: { sha: string; mod: ModInfo };
  };
  [Module.TASK_QUEUE]: {
    [TaskQueueEventType.PROGRESS]: { taskId: string; progress: number; message?: string };
    [TaskQueueEventType.COMPLETED]: TaskInfo;
  };
  // ... etc
}

// Event interface with typed payload
export interface Event<M extends Module = Module, T extends string = string> {
  module: M;
  type: T;
  payload?: M extends keyof EventPayloadMap
    ? T extends keyof EventPayloadMap[M]
      ? EventPayloadMap[M][T]
      : unknown
    : unknown;
}

// ✅ CORRECT: Subscribe with Module + Type
import { eventBus, Module, ModEventType } from '../services/eventBus';

eventBus.subscribe(Module.MOD, ModEventType.LOADED, (event) => {
  console.log(event.payload.sha);  // Type-safe!
});

eventBus.subscribe(Module.TASK_QUEUE, TaskQueueEventType.PROGRESS, (event) => {
  const { taskId, progress } = event.payload;  // Type-safe!
});

// ❌ WRONG: Don't use old pattern
eventBus.on(EventType.ModLoaded, (event) => {  // Old pattern, deprecated
  console.log(event.data);
});
```

#### Event Flow Architecture

```
Backend Emit:
  EventEmitter.EmitAsync(module, type, payload)
    ↓
  EventBus.EmitAsync(EventMessage { Module, Type, Payload })
    ├─→ Handler-Centric Cache (ConcurrentDictionary<HandlerId, Dict<EventId, bool>>)
    ├─→ Lazy evaluation per handler per event
    └─→ All matching handlers invoked
    ↓
  EventBusIpcBridge (subscribes to "*", "*" wildcard)
    ↓
  IpcHandler.SendNotification(module, type, payload)
    ↓
  WebView2 IPC: { category: "notification", module, type, payload }

Frontend Receive:
  bridgeService receives IPC message
    ↓
  Extracts { module, type, payload } from parsed
    ↓
  eventBus.emit({ module, type, payload })
    ↓
  Type-safe subscribers receive event
```

#### EventBus Performance Optimization

The EventBus uses a **handler-centric cache** for optimal performance:

```csharp
// Cache Structure: HandlerId → (EventId → matches: bool)
private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, bool>> _handlerEventCache;

// Benefits:
// 1. No cache invalidation on handler registration
// 2. Lazy evaluation - each handler evaluates each event only once
// 3. O(1) lookups for cached handler+event combinations
// 4. Single TryRemove operation for handler unregistration
// 5. Thread-safe with ConcurrentDictionary
```

**Performance Characteristics:**
- **First emit of event**: Pattern matching + cache store per handler
- **Subsequent emits**: O(1) cache lookup per handler
- **Register handler**: Create empty cache (no iteration)
- **Unregister handler**: Single `TryRemove` operation

#### Adding New Event Types

1. **Define event constants** (NO module prefix in type names):
   ```csharp
   // Modules/YourModule/YourModuleEvents.cs
   public static class YourModuleEvents
   {
       public const string NEW_EVENT = "NEW_EVENT";  // NOT "YOUR_MODULE_NEW_EVENT"
   }
   ```

2. **Emit events using Module + Type**:
   ```csharp
   await _eventEmitter.EmitAsync(
       ModuleNames.YOUR_MODULE,
       YourModuleEvents.NEW_EVENT,
       new { /* payload */ }
   );
   ```

3. **Add to ModuleNames if new module**:
   ```csharp
   public static class ModuleNames
   {
       public const string YOUR_MODULE = "YOUR_MODULE";
   }
   ```

4. **Frontend: Add module enum** (if new module):
   ```typescript
   export enum Module {
       YOUR_MODULE = 'YOUR_MODULE',
   }
   ```

5. **Frontend: Add event type enum**:
   ```typescript
   export enum YourModuleEventType {
       NEW_EVENT = 'NEW_EVENT',
   }
   ```

6. **Frontend: Add to EventPayloadMap** (for type safety):
   ```typescript
   export interface EventPayloadMap {
       [Module.YOUR_MODULE]: {
           [YourModuleEventType.NEW_EVENT]: { /* payload type */ };
       };
   }
   ```

#### Module Event Constants Files

- `Modules/Core/Event/CoreEvents.cs` - Core lifecycle events
- `Modules/Mods/ModEvents.cs` - Mod operation events
- `Modules/TaskQueue/TaskQueueEvents.cs` - Task queue events
- `Modules/Profiles/ProfileEvents.cs` - Profile events
- `Modules/Settings/SettingsEvents.cs` - Settings events
- `Modules/Migration/MigrationEvents.cs` - Migration events
- `Modules/Tools/ToolsEvents.cs` - Tools events
- `Composition/DropZoneEvents.cs` - Drop zone events

**Key Principles:**
- ✅ Module names are uppercase with underscores (TASK_QUEUE, DROP_ZONE)
- ✅ Event type names have NO module prefix (LOADED, not MOD_LOADED)
- ✅ Events are emitted as (Module, Type, Payload)
- ✅ Frontend subscribes with (module, type, handler)
- ✅ Full type safety with EventPayloadMap
- ❌ NEVER use CUSTOM_EVENT - every event has explicit module and type
- ❌ NEVER use string literals - always use constants/enums

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

### Slide-In Screen Pattern (CRITICAL)

**IMPORTANT:** `useSlideInScreen` captures the `content` prop only once when the screen opens. It does NOT update the content when props change!

```typescript
// ❌ WRONG: Content won't update when selectedTags changes
export const ModEditScreen: React.FC = () => {
  const [selectedTags, setSelectedTags] = useState<string[]>([]);

  const formContent = (
    <Form>
      <TagsSection tags={selectedTags} />  // This won't update!
    </Form>
  );

  useSlideInScreen({
    visible,
    content: formContent,  // Captured once, never updates
  });
};

// ✅ CORRECT: Create a self-contained component that manages its own state
const ModEditFormContent: React.FC<{ mod?: ModInfo }> = ({ mod }) => {
  // All state lives HERE
  const [selectedTags, setSelectedTags] = useState<string[]>([]);

  // Initialize from mod prop (acceptable - used only once on mount)
  useEffect(() => {
    if (mod) {
      setSelectedTags(mod.tags || []);
    }
  }, [mod]);

  return (
    <Form>
      <TagsSection tags={selectedTags} />  // Updates correctly!
    </Form>
  );
};

export const ModEditScreen: React.FC = () => {
  const mod = useModsStore(s => s.modToEdit);

  useSlideInScreen({
    visible,
    content: <ModEditFormContent mod={mod} />,  // Component re-renders on state changes
  });
};
```

**Key Rules:**
1. **NEVER** pass props to content that need to update during the screen's lifetime
2. **ALWAYS** create a separate component that subscribes to the store or manages state internally
3. The content component should be **fully self-contained** - manage state internally
4. **OK to pass initial values** as props (e.g., `mod` for form initialization) - these won't change during screen lifetime
5. Use Zustand store subscriptions inside the content component for reactive updates on changing data

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
type ModuleName = 'MOD' | 'PROFILE' | 'SETTING' | 'SYSTEM' |
                  'TOOL' | 'PLUGIN' | 'WAREHOUSE' | 'MIGRATION' | 'LAUNCH';
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
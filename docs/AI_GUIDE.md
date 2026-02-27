# AI Assistant Guide

**Version:** 2.4
**Last Updated:** 2026-02-26
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
    public const string SYSTEM = "SYSTEM";
    public const string DROP_ZONE = "DROP_ZONE";
    public const string MOD = "MOD";
    public const string CATEGORY = "CATEGORY";
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

// Example: Infrastructure/DropZoneEvents.cs
public static class DropZoneEvents
{
    public const string CLICK = "CLICK";                    // NOT "DROP_ZONE_CLICK"
    public const string DRAG_ENTER = "DRAG_ENTER";
    public const string FILE_DROP = "FILE_DROP";
}

// System Events (Modules/System/SystemEvents.cs)
// ⚠️ CRITICAL: Only application lifecycle events
public static class SystemEvents
{
    public const string APPLICATION_STARTED = "APPLICATION_STARTED";
    public const string APPLICATION_SHUTDOWN = "APPLICATION_SHUTDOWN";
    public const string LOG_LEVEL_CHANGED = "LOG_LEVEL_CHANGED";
}

// ✅ CORRECT: Emit with Module + Type
await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.LOADED, new { Sha = sha });
await _eventBus.EmitAsync(ModuleNames.TASK_QUEUE, TaskQueueEvents.PROGRESS, progressData);

// ❌ WRONG: Don't use string literals
await _eventBus.EmitAsync("MOD", "MOD_LOADED", new { Sha = sha });
```

#### Profile-Scoped Events

Events can be global or profile-scoped via the `profileId` parameter:

```csharp
// Global event (no profileId)
await _eventBus.EmitAsync(
    ModuleNames.SYSTEM,
    SystemEvents.APPLICATION_STARTED
);

// Profile-scoped event (with profileId)
await _eventBus.EmitAsync(
    ModuleNames.MOD,
    ModEvents.LOADED,
    new { sha },
    profileId: "profile-123"
);
```

**Event Filtering by ProfileId**:
```csharp
// Listen to specific profile
_eventBus.RegisterHandler(ModuleNames.MOD, ModEvents.LOADED, "profile-123", async (msg) =>
{
    _logger.Info($"Mod loaded in profile {msg.ProfileId}");
});

// Listen to all profiles
_eventBus.RegisterHandler(ModuleNames.MOD, ModEvents.LOADED, async (msg) =>
{
    _logger.Info($"Mod loaded: {msg.Payload}");
});
```

#### Frontend Event System

```typescript
// Event Structure (src/shared/services/eventBus.ts)
export enum Module {
  SYSTEM = 'SYSTEM',
  MOD = 'MOD',
  CATEGORY = 'CATEGORY',
  TASK_QUEUE = 'TASK_QUEUE',
  DROP_ZONE = 'DROP_ZONE',
  SETTING = 'SETTING',
  PROFILE = 'PROFILE',
  MIGRATION = 'MIGRATION',
  TOOL = 'TOOL',
  PLUGIN = 'PLUGIN',
}

// Separate enums per module - NO module prefix!
export enum SystemEventType {
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

- `Modules/System/SystemEvents.cs` - Application lifecycle events
- `Modules/Mod/ModEvents.cs` - Mod operation events
- `Modules/TaskQueue/TaskQueueEvents.cs` - Task queue events
- `Modules/Profile/ProfileEvents.cs` - Profile events
- `Modules/Setting/SettingEvents.cs` - Settings events
- `Modules/Migration/MigrationEvents.cs` - Migration events
- `Modules/Tool/ToolEvents.cs` - Tools events

**Key Principles:**
- ✅ Module names are UPPERCASE (SYSTEM, MOD, TASK_QUEUE)
- ✅ Event type names have NO module prefix (LOADED, not MOD_LOADED)
- ✅ Events are emitted as (Module, Type, Payload, ProfileId?)
- ✅ Frontend subscribes with (module, type, handler)
- ✅ Full type safety with EventPayloadMap
- ✅ ProfileId for profile-scoped events, null for global
- ❌ NEVER use CUSTOM_EVENT - plugins don't emit events currently
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

### CSS BEM Naming Convention

**CRITICAL:** This project uses **raw CSS** with **BEM (Block Element Modifier)** naming convention for ALL component styles.

#### 🎯 Three Golden Rules

1. **✅ USE BEM as the standard** - All component CSS files must use BEM naming convention
2. **❌ DO NOT apply BEM in `src/styles/`** - Global overrides and utilities in `src/styles/` use generic names
3. **⚠️ CONVERT all `.module.css` to regular `.css`** - No CSS Modules allowed, migrate to BEM naming

#### BEM Structure

```
Block:    .component-name
Element:  .component-name-element
Modifier: .component-name--modifier
          .component-name-element--modifier
```

**Important:** This project uses a **relaxed BEM variant**:
- Single dash (`-`) for elements (instead of double underscore `__`)
- Double dash (`--`) for modifiers (standard BEM)
- All lowercase with kebab-case

#### BEM Naming Rules

1. **Block Name = Component/File Name**
   ```css
   /* File: ModListPanel.tsx → ModListPanel.css */
   .mod-list-panel { }           /* ✅ Block */
   .mod-list-panel-header { }    /* ✅ Element */
   .mod-list-panel--compact { }  /* ✅ Modifier */
   ```

2. **Elements are children of blocks**
   ```css
   /* ✅ CORRECT */
   .about-dialog { }
   .about-dialog-header { }
   .about-dialog-content { }
   .about-dialog-footer { }

   /* ❌ WRONG - Generic names without block prefix */
   .header { }
   .content { }
   .footer { }
   ```

3. **Modifiers indicate state or variation**
   ```css
   /* ✅ CORRECT */
   .mod-list-item { }
   .mod-list-item--selected { }
   .mod-list-item--disabled { }
   .mod-list-item-tag--primary { }

   /* ❌ WRONG - Modifier without base class */
   .selected { }
   .disabled { }
   ```

4. **No nested component names**
   ```css
   /* ✅ CORRECT - Flat hierarchy */
   .mod-list-panel { }
   .mod-list-panel-item { }
   .mod-list-panel-item-name { }
   .mod-list-panel-item-actions { }

   /* ❌ WRONG - Too deeply nested */
   .mod-list-panel-item-actions-button-icon { }
   /* Better: split into separate block or use shorter name */
   .mod-list-panel-action-icon { }
   ```

#### Component-Specific Naming

```css
/* ComponentName.tsx → ComponentName.css */

/* Profile Selector Example */
.profile-selector { }                    /* Block */
.profile-selector-dropdown { }           /* Element */
.profile-selector-item { }               /* Element */
.profile-selector-item--active { }       /* Modifier */

/* Compact Button Example */
.compact-button { }                      /* Block */
.compact-button--large { }               /* Modifier */
.compact-button--small { }               /* Modifier */
.compact-button--primary { }             /* Modifier */
.compact-button--danger { }              /* Modifier */

/* Context Menu Example */
.context-menu { }                        /* Block */
.context-menu-item { }                   /* Element */
.context-menu-item-icon { }              /* Element (nested) */
.context-menu-item-label { }             /* Element (nested) */
.context-menu-item--disabled { }         /* Modifier */
.context-menu-item--danger { }           /* Modifier */
.context-menu-divider { }                /* Element */
```

#### Global/Utility Styles (Exceptions)

**Files that should NOT use BEM naming** (global utilities and library overrides):

1. **`src/index.css`** - Base element styles (body, code font definitions)
2. **`src/styles/theme-colors.css`** - CSS variables and theme definitions
3. **`src/styles/visual-enhancements.css`** - **PRIMARY FILE** for all Ant Design overrides, animations, and global utility classes
4. **`src/styles/custom-notification.css`** - Ant Design notification-specific overrides

**File Organization:**
- **`src/index.css`**: Only base HTML element styles (body, code)
- **`src/styles/visual-enhancements.css`**: ALL Ant Design component overrides, animation fixes, hover effects, and global utilities
- **`src/App.css`**: App component-specific layout styles only (app-main-layout, app-content, etc.)

```css
/* ✅ ACCEPTABLE in index.css, theme-colors.css */
:root {
  --primary-color: #1890ff;
}

body {
  margin: 0;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI';
}

/* ✅ ACCEPTABLE for Ant Design overrides in visual-enhancements.css */
.ant-btn {
  /* Override third-party library styles */
}

/* ✅ ACCEPTABLE utility classes in visual-enhancements.css */
.status-success { color: #52c41a; }
.status-error { color: #ff4d4f; }
.count-indicator { /* utility styling */ }

/* ❌ WRONG - Component-specific styles in component CSS files should use BEM */
/* File: SettingsView.css */
.container { }  /* ❌ Should be .settings-view-container */
.header { }     /* ❌ Should be .settings-view-header */
.content { }    /* ❌ Should be .settings-view-content */
```

**Rule of thumb:**
- **Global files** in `src/styles/` (`index.css`, `theme-colors.css`, `visual-enhancements.css`) → Generic names OK
- **Component files** (`ComponentName.css`) → Must use BEM with component prefix

#### CRITICAL: CSS Modules Migration

**⚠️ DO NOT USE CSS Modules (`.module.css`) in this project!**

This project uses **raw CSS with BEM naming convention**. All `.module.css` files should be converted to regular `.css` files with BEM naming.

**Migration Steps:**

1. Rename `ComponentName.module.css` → `ComponentName.css`
2. Convert camelCase class names to kebab-case BEM names
3. Add component prefix to all class names
4. Update TSX imports from `styles from './Component.module.css'` to `import './Component.css'`
5. Update className usage from `className={styles.item}` to `className="component-name-item"`

**Example Migration:**

```css
/* ❌ BEFORE: ProfileManager.module.css */
.container { }
.profileItem { }
.profileItemActive { }
.fullWidthInput { }

/* ✅ AFTER: ProfileManager.css */
.profile-manager-container { }
.profile-manager-item { }
.profile-manager-item--active { }
.profile-manager-full-width-input { }
```

```typescript
/* ❌ BEFORE: ProfileManager.tsx */
import styles from './ProfileManager.module.css';
<div className={styles.container}>
  <div className={classNames(styles.profileItem, {
    [styles.profileItemActive]: isActive
  })}>

/* ✅ AFTER: ProfileManager.tsx */
import './ProfileManager.css';
<div className="profile-manager-container">
  <div className={classNames('profile-manager-item', {
    'profile-manager-item--active': isActive
  })}>
```

#### Common BEM Mistakes

```css
/* ❌ WRONG - CamelCase */
.modListItem { }
.profileSelector { }

/* ✅ CORRECT - kebab-case */
.mod-list-item { }
.profile-selector { }

/* ❌ WRONG - Generic without component prefix */
.item { }
.button { }
.icon { }

/* ✅ CORRECT - Component-prefixed */
.mod-list-item { }
.compact-button { }
.context-menu-icon { }

/* ❌ WRONG - Single dash for modifier */
.mod-list-item-selected { }

/* ✅ CORRECT - Double dash for modifier */
.mod-list-item--selected { }

/* Note: This project uses relaxed BEM where single dash is used for elements,
   so .mod-list-item-selected could be interpreted as element "item-selected"
   Use double dash for clarity when indicating state/variation */
```

#### BEM with TypeScript/TSX

```typescript
// ❌ WRONG - Inline styles or generic classes
<div className="item">
<div style={{ color: 'red' }}>

// ✅ CORRECT - BEM classes from CSS file
import './ModListItem.css';

<div className="mod-list-item">
  <div className="mod-list-item-header">
    <span className="mod-list-item-name">{name}</span>
  </div>
</div>

// ✅ CORRECT - BEM with conditional modifiers
import classNames from 'classnames';

<div className={classNames('mod-list-item', {
  'mod-list-item--selected': isSelected,
  'mod-list-item--disabled': isDisabled,
})}>
```

### CSS ClassName Pattern
**ALWAYS** use `classnames` library for conditional/multiple classes:

```typescript
// ❌ DON'T: Template string concatenation
className={`base-class ${isActive ? 'active' : ''}`}
className={`card ${isPrimary ? 'primary' : ''} ${isDisabled ? 'disabled' : ''}`}

// ✅ DO: Use classnames library
import classNames from 'classnames';

className={classNames('base-class', {
  active: isActive,
})}

className={classNames('card', {
  primary: isPrimary,
  disabled: isDisabled,
})}

// ✅ DO: Mix static and conditional classes with BEM
className={classNames('mod-preview-keybinding-toggle', 'compact', {
  'mod-preview-keybinding-toggle--active': showKeybindings,
  'mod-preview-keybinding-toggle--disabled': !mod.hasCache,
})}
```

**Benefits:**
- Cleaner, more readable code
- No manual spacing or empty string handling
- Type-safe with TypeScript
- Handles edge cases automatically

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

## 📦 TaskQueue System (Node-Based Workflows)

### Overview
The TaskQueue system uses a **node-based workflow architecture** with declarative routing conditions. Tasks are organized as nodes in a directed graph, supporting complex workflows with branching, loops, and conditional paths.

### Architecture Components

#### 1. Task Processor Pattern
```csharp
// Simple task processor interface - no metadata or registry
public interface ITaskProcessor<TInput, TOutput>
{
    string TaskType { get; }  // Uses constants from TaskTypes class
    Task<TOutput> ProcessAsync(TInput input, IProgressReporter progressReporter, CancellationToken cancellationToken);
}

// Task types use uppercase constants
public static class TaskTypes
{
    public const string MOD_IMPORT = "MOD_IMPORT";
    public const string COMPRESS_FOLDER = "COMPRESS_FOLDER";
    public const string IMPORT_FROM_TEMP = "IMPORT_FROM_TEMP";
}

// Chain types also use uppercase constants
public static class ChainTypes
{
    public const string FOLDER_IMPORT = "FOLDER_IMPORT";
    public const string QUICK_FOLDER_IMPORT = "QUICK_FOLDER_IMPORT";
    public const string VALIDATED_IMPORT = "VALIDATED_IMPORT";
    public const string BATCH_PROCESSING = "BATCH_PROCESSING";
}
```

#### 2. Node-Based Workflow Configuration
```csharp
// Workflows use nodes with routing rules
public class TaskChainNode
{
    public required string NodeId { get; init; }         // Unique node identifier
    public required string TaskType { get; init; }       // From TaskTypes constants
    public Dictionary<string, string> InputMapping { get; init; }   // Map inputs
    public Dictionary<string, string> OutputMapping { get; init; }  // Map outputs
    public List<NodeRoutingRule> RoutingRules { get; init; }       // Conditional routing
    public string? DefaultNextNode { get; init; }        // Fallback route
}

// Routing rules define conditional transitions
public class NodeRoutingRule
{
    public required string Name { get; init; }           // Rule description
    public required RoutingCondition Condition { get; init; }
    public required string NextNodeId { get; init; }     // Target node
    public int Priority { get; init; } = 0;              // Evaluation order
}

// Rich condition types for complex logic
public enum ConditionType
{
    TaskStatus,        // Check task completion status
    OutputField,       // Check task output values
    SharedDataField,   // Check chain-level data
    HasError,          // Route on error presence
    UserInput,         // Check user-provided values
    And, Or, Not,      // Logical operators
    Always,            // Unconditional
    Custom             // Business-specific logic
}
```

### Creating a New Task Processor

#### Step 1: Define Input/Output Models
```csharp
// Models/TaskInputs/MyTaskInput.cs
public class MyTaskInput
{
    public required string FilePath { get; init; }
    public string? Options { get; init; }
}

// Models/TaskOutputs/MyTaskOutput.cs
public class MyTaskOutput
{
    public required string ResultId { get; init; }
    public bool Success { get; init; }
}
```

#### Step 2: Implement the Processor
```csharp
// Processors/MyTaskProcessor.cs
public class MyTaskProcessor : ITaskProcessor<MyTaskInput, MyTaskOutput>
{
    private readonly IMyService _service;
    private readonly ILogHelper _logger;

    // Use constant from TaskTypes class
    public string TaskType => "MY_TASK";  // Add to TaskTypes.cs

    public MyTaskProcessor(IMyService service, ILogHelper logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task<bool> ValidateInputAsync(MyTaskInput input)
    {
        // Validate input before processing
        if (string.IsNullOrEmpty(input.FilePath))
            return false;

        return File.Exists(input.FilePath);
    }

    public async Task<MyTaskOutput> ProcessAsync(
        MyTaskInput input,
        IProgressReporter progressReporter,
        CancellationToken cancellationToken)
    {
        // Report progress
        await progressReporter.ReportAsync(0.1f, "Starting process...");

        // Check cancellation
        cancellationToken.ThrowIfCancellationRequested();

        // Do work
        var result = await _service.ProcessFileAsync(input.FilePath);

        await progressReporter.ReportAsync(1.0f, "Complete!");

        return new MyTaskOutput
        {
            ResultId = result.Id,
            Success = true
        };
    }
}
```

#### Step 3: Register in DI and Add to Processing
```csharp
// TaskQueueServiceExtensions.cs
public static IServiceCollection AddTaskQueueServices(this IServiceCollection services)
{
    // ... existing registrations ...

    // Register your processor
    services.TryAddSingleton<MyTaskProcessor>();

    return services;
}

// TaskQueueService.cs - Add to ProcessTaskAsync switch
case "MY_TASK":
{
    var processor = _serviceProvider.GetService<MyTaskProcessor>();
    if (processor == null)
        throw new InvalidOperationException($"Processor not found for task type: {taskType}");

    var input = JsonSerializer.Deserialize<MyTaskInput>(inputJson, JsonOptions);
    return await processor.ProcessAsync(input, progressReporter, cancellationToken);
}
```

### Creating a Node-Based Workflow

```csharp
// Configuration/PredefinedChains.cs
public static TaskChainConfiguration MyWorkflowChain => new()
{
    StartNodeId = "validation_node",
    Nodes = new Dictionary<string, TaskChainNode>
    {
        ["validation_node"] = new TaskChainNode
        {
            NodeId = "validation_node",
            TaskType = TaskTypes.MY_TASK,
            InputMapping = new Dictionary<string, string>
            {
                ["filePath"] = "input.filePath"
            },
            RoutingRules = new List<NodeRoutingRule>
            {
                new NodeRoutingRule
                {
                    Name = "Validation passed",
                    Condition = new RoutingCondition
                    {
                        Type = ConditionType.OutputField,
                        Field = "isValid",
                        Operator = ComparisonOperator.Equals,
                        Value = true
                    },
                    NextNodeId = "process_node",
                    Priority = 1
                },
                new NodeRoutingRule
                {
                    Name = "Validation failed",
                    Condition = new RoutingCondition
                    {
                        Type = ConditionType.OutputField,
                        Field = "isValid",
                        Operator = ComparisonOperator.Equals,
                        Value = false
                    },
                    NextNodeId = "error_node",
                    Priority = 2
                }
            }
        },
        ["process_node"] = new TaskChainNode
        {
            NodeId = "process_node",
            TaskType = TaskTypes.IMPORT_FROM_TEMP,
            // ... configuration
        }
    }
};
```

### Frontend Integration

#### Use Task and Chain Type Constants
```typescript
// Import constants matching backend
import { TaskTypes } from '@/modules/taskQueue/constants/taskTypes';
import { ChainTypes } from '@/modules/taskQueue/constants/chainTypes';

// Use constants instead of strings
const taskId = await taskQueueService.addTask(
    TaskTypes.MOD_IMPORT,  // "MOD_IMPORT"
    { filePath: '/path/to/mod.zip' },
    profileId
);
```

#### Query Available Tasks (Hardcoded in Facade)
```typescript
// Get all task metadata (from facade's hardcoded list)
const response = await bridgeService.sendIpcRequest({
    module: 'TASK_QUEUE',
    type: 'GET_ALL_TASK_METADATA',
    payload: { profileId }
});

// Get specific task metadata
const response = await bridgeService.sendIpcRequest({
    module: 'TASK_QUEUE',
    type: 'GET_TASK_METADATA',
    payload: { profileId, taskType: TaskTypes.MY_TASK }
});
```

#### Continue Chain After User Input
```typescript
// When task status is AwaitingConfirmation
const response = await bridgeService.sendIpcRequest({
    module: 'TASK_QUEUE',
    type: 'CONTINUE_CHAIN',
    payload: {
        profileId,
        chainId: task.taskChainId,
        taskId: task.id,
        userInput: {
            name: formData.name,
            description: formData.description,
            grading: formData.grading
        }
    }
});
```

### Standard Chains Available (PredefinedChains.cs)

1. **`FOLDER_IMPORT`** - Interactive folder import with user metadata input
   - Compress folder → Await user confirmation → Import with metadata
2. **`QUICK_FOLDER_IMPORT`** - Automatic folder import with defaults
   - Compress folder → Auto-import without user interaction
3. **`VALIDATED_IMPORT`** - Import with validation step
   - Compress → Validate → User review → Import
4. **`BATCH_PROCESSING`** - Process multiple items in sequence
   - Configure → Process items → Complete

### Key Benefits

- **Node-Based Workflows**: Flexible graph structure instead of linear phases
- **Declarative Routing**: Conditions defined in configuration, not code
- **No Registry/Factory**: Simple direct processor execution
- **Type-Safe Constants**: All types use UPPER_SNAKE_CASE constants
- **Conditional Branching**: Support for complex business logic
- **Input/Output Mapping**: Data flows between nodes automatically
- **Progress Tracking**: Built-in progress reporting
- **Cancellation Support**: Graceful task cancellation

### Important Patterns

```csharp
// ✅ CORRECT: Use constants
public string TaskType => TaskTypes.MOD_IMPORT;
var chain = ChainTypes.FOLDER_IMPORT;

// ❌ WRONG: Don't use magic strings
public string TaskType => "mod_import";
var chain = "folder_import_chain";

// ✅ CORRECT: Simple property names
task.Input = JsonSerializer.Serialize(input);
task.Output = result;

// ❌ WRONG: Don't use old names
task.InputData = input;  // Old name
task.OutputData = output; // Old name
```

### Files to Reference

- `Modules/TaskQueue/TaskTypes.cs` - Task type constants
- `Modules/TaskQueue/ChainTypes.cs` - Chain type constants
- `Modules/TaskQueue/Models/TaskChainInfo.cs` - Node-based models
- `Modules/TaskQueue/Services/RoutingConditionEvaluator.cs` - Routing logic
- `Modules/TaskQueue/Configuration/PredefinedChains.cs` - Chain definitions
- `Modules/TaskQueue/Processors/*` - Example processors
- `Modules/TaskQueue/TaskQueueServiceExtensions.cs` - DI registration

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
12. **NEVER** use generic CSS class names without BEM component prefix (`.item`, `.button`, `.header`)
13. **NEVER** use camelCase or PascalCase in CSS class names (use kebab-case)
14. **NEVER** create deep nested BEM names (max 3-4 segments)
15. **NEVER** create new `.module.css` files - use regular `.css` with BEM naming instead
16. **NEVER** apply BEM naming to global utility files in `src/styles/` directory

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
# Design Decisions

**Last Updated:** 2026-02-23
**Purpose:** Critical architectural decisions and their rationale for AI code generation
**Scope:** WHY — the rules and constraints. For HOW to implement patterns, see [ADVANCED_PATTERNS.md](ADVANCED_PATTERNS.md)

---

## Backend Decisions

### 1. Server-Side Processing (CRITICAL)

**Decision:** ALL heavy operations MUST run on C# backend with progress updates

**Why:**
- C# is 10-100x faster than JavaScript for file/data operations
- Direct file system access without browser restrictions
- Better memory management for large files

**Pattern:**
```csharp
// Backend: Report progress for operations >1 second
public async Task<Result> HeavyOperation(IProgress<Progress> progress) {
    progress?.Report(new Progress { Percent = 0, Status = "Starting..." });
    // Do work...
    progress?.Report(new Progress { Percent = 50, Status = "Processing..." });
}
```

**Required for:**
- Archive extraction/compression
- SHA-256 calculation
- Image processing/thumbnails
- Database operations
- File I/O operations

---

### 2. Path Handling

**Decision:** Database stores RELATIVE paths, runtime uses ABSOLUTE paths

**Why:**
- Portable database (can move installation folder)
- Prevents path traversal attacks
- Consistent across different drives

**Pattern:**
```csharp
// Storage: "mods/character/mod123"
// Runtime: "C:\Games\D3DX\mods\character\mod123"
IGlobalPathService.GetAbsolutePath(relativePath);
```

---

### 3. Error Handling

**Decision:** Exceptions for unexpected, MessageResponse for expected failures

**Why:**
- Clear distinction between bugs and business logic
- Better error messages for users

**Pattern:**
```csharp
// Unexpected: Throw exception
if (file == null) throw new ArgumentNullException(nameof(file));

// Expected: Return result
if (!File.Exists(path))
    return MessageResponse.Error($"File not found: {path}");
```

---

### 4. Async Patterns

**Decision:** async/await everywhere, NO callbacks

**Pattern:**
```csharp
// Always async for I/O
public async Task<Result> LoadModAsync(string id) { ... }
```

---

### 5. Module Boundaries

**Decision:** Modules can inject services from other modules, but NOT repositories

**Pattern:**
```csharp
// ✅ GOOD: Inject service
public ModService(IImageService imageService) { }

// ❌ BAD: Inject repository from another module
public ModService(IProfileRepository profileRepo) { } // NEVER!
```

---

### 6. Service Layer Architecture (NEW - 2026-03-07)

**Decision:** Three-layer service architecture with clear separation of concerns

**Layers:**

**Layer 1 - Pure Operations:**
- No business logic, no event emission
- Reusable file/archive operations
- Example: ModArchiveService, ModCacheService
- Pattern: NO IProfileEventBus injection

**Layer 2 - Business Logic + Events:**
- Orchestrates Layer 1 services
- Handles business rules
- Emits events after operations
- Example: ModLifecycleService, ModMetadataService
- Pattern: Injects IProfileEventBus, emits events on completion

**Layer 3 - Event Consolidation:**
- Subscribes to multiple backend events
- Emits single consolidated event for frontend
- Example: ModListEventHandler, CategoryTreeEventHandler
- Pattern: Reduces frontend event complexity

**Why:**
- Clear separation between operations and business logic
- Reusable operation services (no coupling to events)
- Simpler frontend event handling (consolidated events)
- Prevents event storms (8+ events → 1 consolidated event)

**Pattern:**
```csharp
// ❌ OLD: God service with everything
public class ModFileService {
    // 856 lines: archive ops + cache ops + business logic + events
}

// ✅ NEW: Layered services
public class ModArchiveService { }       // Layer 1: Pure operations
public class ModCacheService { }         // Layer 1: Pure operations
public class ModLifecycleService { }     // Layer 2: Business logic + events
public class ModListEventHandler { }     // Layer 3: Event consolidation
```

---

### 7. Event Consolidation Pattern (NEW - 2026-03-07)

**Decision:** Backend consolidates multiple events, frontend uses debounced handlers

**Problem:**
- Frontend had 8+ separate event subscriptions
- Multiple rapid-fire events caused UI re-renders
- Complex event handling logic in frontend

**Solution:**
```csharp
// Backend: Event handler consolidates 8 events → 1
public class ModListEventHandler {
    public ModListEventHandler(IProfileEventBus eventBus) {
        // Subscribe to 8 specific events
        eventBus.Subscribe(ModuleNames.MOD, ModEvents.LOADED, HandleChange);
        eventBus.Subscribe(ModuleNames.MOD, ModEvents.UNLOADED, HandleChange);
        eventBus.Subscribe(ModuleNames.MOD, ModEvents.DELETED, HandleChange);
        // ... 5 more subscriptions
    }

    private async Task HandleChange(object data) {
        // Emit single consolidated event
        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.MOD_LIST_UPDATED, data);
    }
}
```

```typescript
// Frontend: Debounced handler (20ms) prevents event storms
const handleModListUpdate = useCallback(
  debounce(() => {
    void refreshMods();
    void loadStatistics();
  }, 20),  // 20ms debounce
  [profileId]
);

eventBus.subscribe(Module.MOD, ModEventType.MOD_LIST_UPDATED, handleModListUpdate);
```

**Benefits:**
- 8+ frontend event handlers → 1 debounced handler
- Prevents event storms (multiple events within 20ms handled once)
- Simpler frontend code

---

## Frontend Decisions

### 8. IPC Architecture

**Decision:** ALL frontend→backend communication routes through one dispatch chain:
`MessageDispatcher` (middleware pipeline) → `ProfileServiceRouter` (module → profile-scoped facade)

**Message Format:**
```typescript
interface BridgeMessage {
  module: ModuleName;     // 'MOD' | 'PROFILE' | 'SETTING' etc
  type: string;          // 'LOAD' | 'SAVE' | 'DELETE' etc
  profileId?: string;    // Profile context if needed
  payload?: unknown;     // Type-safe payload
}
```

**Pattern:**
```typescript
// Frontend sends
{ module: 'MOD', type: 'LOAD', profileId: 'abc', payload: { id: '...' } }

// Backend routes through MessageDispatcher → ProfileServiceRouter → ModuleFacade → Service
```

---

### 9. State Management

**Decision:** Zustand stores for module state, React Context for global concerns

**Why:**
- Simpler without external dependencies
- React Context sufficient for our scale
- Better TypeScript integration

**Pattern:**
```typescript
// Zustand stores for module-specific state (Updated 2026-03-07)
const modsStore = create<ModsState>((set) => ({
  mods: undefined,  // Current filtered mods
  modLoading: false,
  setMods: (mods) => set({ mods }),
}));

// React Context for global concerns
ProfileContext     // Current profile
ThemeContext       // UI theme
SettingsContext    // Global settings

// Usage
const mods = useModsStore((state) => state.mods);  // Zustand
const { selectedProfileId } = useProfile();        // Context
```

---

### 10. Frontend Service Pattern

**Decision:** ALL frontend services extend BaseModuleService

**Pattern:**
```typescript
class ModService extends BaseModuleService {
  constructor() { super('MOD'); }  // Module name

  async loadMod(id: string) {
    return this.sendMessage('LOAD', { id });
  }
}
```

---

### 11. Component Architecture

**Decision:** Presentation/Container pattern with hooks

**Pattern:**
```typescript
// Container: Logic and state
const ModListContainer = () => {
  const { mods, loadAllMods } = useMods();
  return <ModList mods={mods} onLoad={loadAllMods} />;
};

// Presentation: Pure UI
const ModList: FC<Props> = ({ mods, onLoad }) => {
  // Only UI logic
};
```

---

### 12. Modal Dialog Pattern

**Decision:** Use shared dialog components, never raw `<Modal>` or imperative APIs

**Why:**
- Imperative APIs (Modal.confirm) cause flashing
- Raw `<Modal>` requires repeating transition/centering/close-button boilerplate
- Shared dialogs handle theming, delayed loading, and consistent UX

**Pattern:**
```typescript
// ✅ GOOD: Use shared dialog components (from shared/components/dialogs/)
import { ConfirmDialog } from 'shared/components/dialogs/ConfirmDialog';
import { FormDialog } from 'shared/components/dialogs/FormDialog';
import { InfoDialog } from 'shared/components/dialogs/InfoDialog';

// Destructive confirmation → ConfirmDialog with okType="danger"
<ConfirmDialog
  visible={visible}
  title="Delete Item"
  content="Are you sure?"
  okType="danger"
  onOk={handleDelete}   // async — loading state handled automatically
  onCancel={handleClose}
/>

// Form/input dialog → FormDialog
<FormDialog
  visible={visible}
  title="Create Item"
  onOk={handleSave}     // async — loading state handled automatically
  onCancel={handleClose}
>
  <Input value={name} onChange={...} />
</FormDialog>

// Read-only info → InfoDialog
<InfoDialog visible={visible} title="About" onClose={handleClose}>
  <p>Content here</p>
</InfoDialog>

// ❌ BAD: Raw Modal — missing theming, loading, close button styling
<Modal open={visible} transitionName="" maskTransitionName="" centered>

// ❌ BAD: Imperative — causes flashing
Modal.confirm({ ... });
```

---

### 13. Null vs Undefined

**Decision:** Frontend uses `undefined` for absent data, `null` only for React

**Pattern:**
```typescript
// Data: Always undefined
const [mod, setMod] = useState<Mod>();  // NOT useState<Mod | null>(null)

// React render: Use null
if (!mod) return null;  // React requirement
```

---

### 14. Internationalization (i18n)

**Decision:** ALL user-facing text MUST use translation keys

**Pattern:**
```typescript
// ❌ BAD: Hardcoded
<Button>Load Mod</Button>

// ✅ GOOD: i18n
const { t } = useTranslation();
<Button>{t('mods.actions.load')}</Button>
```

---

### 15. React Closure Pattern

**Decision:** Use useStableRef for callbacks to avoid stale closures

**Problem:** useCallback captures stale values

**Solution:**
```typescript
// BAD: Stale closure
const handleClick = useCallback(() => {
  logger.info(items.length); // May be stale!
}, [items]);

// GOOD: Always current
const itemsRef = useStableRef(items);
const handleClick = useCallback(() => {
  logger.info(itemsRef.current.length); // Always current!
}, []);  // No deps needed
```

---

### 16. Compact Components

**Decision:** Use CompactButton, CompactSpace etc for consistent sizing

**Pattern:**
```typescript
import { CompactButton, CompactCard } from 'shared/components/compact';

// Consistent sizing across app
<CompactButton type="primary">Save</CompactButton>
```

---

### 17. CSS Strategy

**Decision:** CSS Modules/classes over inline styles

**When to use inline styles:**
- Dynamic values (width based on state)
- One-off positioning

**When to use CSS classes:**
- Reusable styles
- Hover/focus states
- Theme-aware colors

---

## Database Decisions

### 18. SQLite with EF Core

**Decision:** Single-file SQLite database with migrations

**Why:**
- Portable (single file)
- Strong typing with EF Core
- Version-controlled schema

**Pattern:**
```bash
dotnet ef migrations add AddModMetadata
dotnet ef database update
```

---

## Testing Decisions

### 19. Testing Strategy

**Decision:** Unit tests for utilities, integration tests for services

**Why:**
- Services often need database/file system
- UI testing is manual (cost/benefit)

**Pattern:**
```csharp
// Unit: Pure functions
[Fact]
public void PathHelper_ConvertsPath() { }

// Integration: With dependencies
[Fact]
public async Task ModService_LoadsMod_WithDatabase() { }
```

---

## Performance Decisions

### 20. Operation Progress

**Decision:** ALL operations >1 second MUST report progress

**Pattern:**
```csharp
// Backend
_progressReporter.CreateOperation(opId, title, type);
_progressReporter.UpdateProgress(opId, percent, status);
_progressReporter.CompleteOperation(opId, result);
```

---

### 21. CET Compatibility (NEW - 2026-04-13)

**Decision:** CET (Hardware-enforced Stack Protection) is disabled via `<CetCompat>false</CetCompat>` in the csproj

**Why:**
- .NET 8+ enables CET by default on x64
- Windows shell extensions (image thumbnail providers, context menu handlers) loaded by file dialogs are older DLLs that aren't CET-compatible
- When these extensions load, they trigger a shadow stack violation → `STATUS_STACK_BUFFER_OVERRUN` (0xc0000409) → process crash
- Only happens when right-clicking image files in file dialogs (triggers thumbnail/context menu shell extensions)

**What didn't work:**
- `AutoUpgradeEnabled = false` (old-style dialog) — still loads shell extensions
- `Application.OleRequired()` on STA thread — OLE init doesn't prevent CET violation
- Same-thread owner form — same process, same CET policy
- Subprocess without WebView2 — same exe, same CET policy
- COM `IFileOpenDialog` directly — same shell extension DLLs, same CET issue

**What works:** `<CetCompat>false</CetCompat>` disables CET for the process so shell extensions don't trigger shadow stack violations.

**Security trade-off:** CET protects against return-oriented programming (ROP) attacks. Acceptable for a desktop app that doesn't process untrusted network input.

---

## Quick Decision Matrix

| Scenario | Decision | Rationale |
|----------|----------|-----------|
| Heavy computation | C# backend | 10-100x faster |
| **Service architecture** | **3-layer (operation/logic/event)** | **Separation of concerns** |
| **Event handling** | **Backend consolidation + frontend debounce** | **Prevents event storms** |
| State management | Zustand stores + Context | Module state + global concerns |
| Service pattern | Extend BaseModuleService | Consistency |
| Modal dialogs | Declarative, no transitions | No flashing |
| Missing data | undefined (TS), null (C#) | Convention |
| User text | i18n translation keys | Localization |
| Callbacks | useStableRef | Avoid stale closures |
| Styles | CSS classes preferred | Reusability |
| Long operations | Progress reporting | UX feedback |
| Database paths | Relative paths | Portability |
| **CET / file dialogs** | **`<CetCompat>false</CetCompat>`** | **Shell extensions crash with CET enabled** |

---

**Remember:** These aren't preferences - they're architectural constraints. Violating them causes bugs.

**Updated:** 2026-04-13 - Added CET compatibility decision for file dialog shell extension crash fix
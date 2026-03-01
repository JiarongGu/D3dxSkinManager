# Design Decisions

**Last Updated:** 2026-02-23
**Purpose:** Critical architectural decisions and their rationale for AI code generation

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
public async Task<Result> LoadModAsync(string sha) { ... }
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

## Frontend Decisions

### 6. IPC Architecture

**Decision:** Centralized AppFacade routes ALL frontend→backend communication

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
{ module: 'MOD', type: 'LOAD', profileId: 'abc', payload: { sha: '...' } }

// Backend routes through AppFacade → ModuleFacade → Service
```

---

### 7. State Management

**Decision:** React Context for global state, NO Redux/MobX

**Why:**
- Simpler without external dependencies
- React Context sufficient for our scale
- Better TypeScript integration

**Pattern:**
```typescript
// Separate contexts by domain
ProfileContext     // Current profile
ModsContext       // Mod list/operations
OperationContext  // Active operations
ThemeContext      // UI theme

// Usage
const { selectedProfileId } = useProfile();
const { mods, loadMods } = useMods();
```

---

### 8. Frontend Service Pattern

**Decision:** ALL frontend services extend BaseModuleService

**Pattern:**
```typescript
class ModService extends BaseModuleService {
  constructor() { super('MOD'); }  // Module name

  async loadMod(sha: string) {
    return this.sendMessage('LOAD', { sha });
  }
}
```

---

### 9. Component Architecture

**Decision:** Presentation/Container pattern with hooks

**Pattern:**
```typescript
// Container: Logic and state
const ModListContainer = () => {
  const { mods, loadMods } = useModData();
  return <ModList mods={mods} onLoad={loadMods} />;
};

// Presentation: Pure UI
const ModList: FC<Props> = ({ mods, onLoad }) => {
  // Only UI logic
};
```

---

### 10. Modal Dialog Pattern

**Decision:** Declarative rendering with DISABLED transitions

**Why:**
- Imperative APIs cause flashing
- Instant display without animation delays
- Better React lifecycle integration

**Pattern:**
```typescript
// ✅ GOOD: Declarative with no transitions
<Modal
  open={visible}
  transitionName=""       // Disable animation
  maskTransitionName=""   // Disable mask animation
  centered
>

// ❌ BAD: Imperative
Modal.confirm({ ... });  // Causes flashing
```

---

### 11. Null vs Undefined

**Decision:** Frontend uses `undefined` for absent data, `null` only for React

**Pattern:**
```typescript
// Data: Always undefined
const [mod, setMod] = useState<Mod>();  // NOT useState<Mod | null>(null)

// React render: Use null
if (!mod) return null;  // React requirement
```

---

### 12. Internationalization (i18n)

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

### 13. React Closure Pattern

**Decision:** Use useStableRef for callbacks to avoid stale closures

**Problem:** useCallback captures stale values

**Solution:**
```typescript
// BAD: Stale closure
const handleClick = useCallback(() => {
  console.log(items.length); // May be stale!
}, [items]);

// GOOD: Always current
const itemsRef = useStableRef(items);
const handleClick = useCallback(() => {
  console.log(itemsRef.current.length); // Always current!
}, []);  // No deps needed
```

---

### 14. Compact Components

**Decision:** Use CompactButton, CompactSpace etc for consistent sizing

**Pattern:**
```typescript
import { CompactButton, CompactCard } from 'shared/components/compact';

// Consistent sizing across app
<CompactButton type="primary">Save</CompactButton>
```

---

### 15. CSS Strategy

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

### 16. SQLite with EF Core

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

### 17. Testing Strategy

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

### 18. Operation Progress

**Decision:** ALL operations >1 second MUST report progress

**Pattern:**
```csharp
// Backend
_progressReporter.CreateOperation(opId, title, type);
_progressReporter.UpdateProgress(opId, percent, status);
_progressReporter.CompleteOperation(opId, result);
```

---

## Quick Decision Matrix

| Scenario | Decision | Rationale |
|----------|----------|-----------|
| Heavy computation | C# backend | 10-100x faster |
| State management | React Context | Simplicity |
| Service pattern | Extend BaseModuleService | Consistency |
| Modal dialogs | Declarative, no transitions | No flashing |
| Missing data | undefined (TS), null (C#) | Convention |
| User text | i18n translation keys | Localization |
| Callbacks | useStableRef | Avoid stale closures |
| Styles | CSS classes preferred | Reusability |
| Long operations | Progress reporting | UX feedback |
| Database paths | Relative paths | Portability |

---

**Remember:** These aren't preferences - they're architectural constraints. Violating them causes bugs.
# AI Assistant Guide

**Version:** 2.1
**Last Updated:** 2026-02-23
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

### React Context Pattern
```typescript
// Context with ProfileId awareness
export const ModsProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const { selectedProfileId } = useProfile();
  const [state, dispatch] = useReducer(modsReducer, initialState);

  useEffect(() => {
    if (selectedProfileId) {
      loadMods(selectedProfileId);
    } else {
      dispatch({ type: 'RESET' });
    }
  }, [selectedProfileId]);

  return <ModsContext.Provider value={{ state, actions }}>{children}</ModsContext.Provider>;
};
```

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
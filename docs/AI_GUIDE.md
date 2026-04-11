# AI Assistant Guide

**Version:** 6.0
**Last Updated:** 2026-04-12
**Role:** Primary entry point — loaded by `/doc-loader` at the start of every task. Contains all mandatory session rules, the complete skills reference, and workflow patterns needed for code generation.

---

## ⚠️ Mandatory Session Rules

> Non-negotiable. Enforced by CLAUDE.md (auto-loaded); surfaced here so they are present every time this guide is loaded.

### 1. Git Commits — Never Without Approval

**NEVER** commit without explicit user approval. Always ask "Ready to commit?" and wait for a clear "yes".

### 2. Architecture Boundaries

```
Backend  → ALL heavy operations, data processing, file I/O
Frontend → UI only — NO data processing, NO business logic
Facades  → Thin delegation only — no business logic, no events
Services → Business logic + event emission
```

**Module boundaries** — never access another module's repository directly. Always call through that module's facade.

### 3. Error Handling

```csharp
// Backend — always throw OperationException
throw new OperationException("ERROR_CODE",
    new Dictionary<string, string> { { "param", value } });

// Frontend — always use handleError
catch (error: unknown) { handleError(error); }
```

Add message to **BOTH** `Languages/en.json` AND `Languages/cn.json`. Use `/error-with-i18n` skill — never add errors manually.

### 4. Events — Services Only

- ✅ Services emit events (inject `IProfileEventBus`)
- ❌ Facades **never** emit events

### 5. Frontend Data Conventions

```typescript
// ✅ undefined for absent/optional data
const [mod, setMod] = useState<ModInfo>();

// ✅ null ONLY for React render short-circuit
if (!data) return null;

// ❌ Never use null for state
const [mod, setMod] = useState<ModInfo | null>(null); // WRONG
```

### 6. Testing — Required After Every Change

After every bug fix or new feature, write tests. Before writing any test:

```
/doc-loader "write tests for <what you changed>" testing
```

Full guide: [TESTING_GUIDE.md](ai-assistant/TESTING_GUIDE.md)

---

## 🎯 Work Style — Skills → Agents → RAG → Manual

**Follow this order strictly:**

| Step | Tool | When |
|------|------|------|
| 1. **Skills** | `/skill-name` | Task matches a skill (code gen, errors, IPC, components) |
| 2. **Explore agent** | `subagent_type: "Explore"` | Understanding existing code (medium thoroughness) |
| 3. **Plan agent** | `subagent_type: "Plan"` | Planning a new feature (load DESIGN_DECISIONS.md in prompt) |
| 4. **RAG** | [KEYWORDS_INDEX.md](KEYWORDS_INDEX.md) | Loading specific docs on demand |
| 5. **Manual** | Direct editing | Only for unique business logic with no pattern match |

---

## 🔧 Skills — Complete Reference (18 Total)

### Code Generation (10 skills)

| Skill | Usage | Generates |
|-------|-------|-----------|
| `/backend-service` | `/backend-service Name Module Deps Methods` | C# service + interface + DI + events + registration |
| `/backend-facade` | `/backend-facade Name Module Services` | Thin IPC facade (delegation only, no logic) |
| `/ipc-service` | `/ipc-service Name Module Methods` | TypeScript IPC service + singleton export |
| `/react-component` | `/react-component Name type features` | Component + BEM CSS + hooks |
| `/error-with-i18n` | `/error-with-i18n CODE params "en msg" "cn msg"` | OperationException + en.json + cn.json |
| `/event-handler` | `/event-handler Name Module SourceEvents Target` | C# event consolidation handler |
| `/ipc-message-pair` | `/ipc-message-pair Module MessageType ...` | Backend handler + Frontend method (paired) |
| `/batch-operation` | `/batch-operation Module Op EntityType Params` | SQL batch + Facade handler + Frontend method |
| `/file-watcher` | `/file-watcher Name Module Path Filters Events` | FileSystemWatcher with lock safety + disposal |
| `/service-registration` | `/service-registration Module Interface Impl Lifecycle` | DI registration in ServiceExtensions.cs |

### Discovery & Documentation (8 skills)

| Skill | Usage | What It Does |
|-------|-------|--------------|
| `/doc-loader` | `/doc-loader "task" scope` | Loads relevant docs + suggests next skill |
| `/pattern-finder` | `/pattern-finder PatternType Module?` | Finds existing code patterns in codebase |
| `/doc-update-guide` | `/doc-update-guide Name ChangeType Details` | Updates AI_GUIDE.md with versioning |
| `/doc-update-reference` | `/doc-update-reference Name EntryType Details` | Updates REFERENCE.md + KEYWORDS_INDEX.md |
| `/doc-update-technical` | `/doc-update-technical Name UpdateType Details` | Updates ADVANCED_PATTERNS.md / DESIGN_DECISIONS.md |
| `/doc-monitor` | `/doc-monitor CheckType Scope` | Audits docs for broken links, redundancy |
| `/doc-cleanup` | `/doc-cleanup Operation Target Details` | Removes redundant docs, archives deprecated content |
| `/doc-optimize` | `/doc-optimize Name OptimizationType Details` | Splits oversized docs, condenses for RAG |

Full skill docs with parameters and examples: [.claude/skills/README.md](../.claude/skills/README.md)

---

## 🏛️ Architecture Patterns (Quick Reference)

### Service Layer (Business Logic + Events)

```csharp
public class ModService : IModService {
    private readonly IModRepository _repository;
    private readonly IProfileEventBus _eventBus;  // Always inject

    public async Task<Result> DoSomethingAsync() {
        var result = await _repository.GetAsync();
        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.CHANGED, data);
        return result;
    }
}
```

Generate with: `/backend-service ModService Mod IModRepository,IProfileEventBus DoSomethingAsync`

### Facade Layer (IPC Delegation Only — No Logic)

```csharp
public class ModFacade : BaseFacade {
    private readonly IModService _service;

    private async Task<Result> HandleAsync(IpcRequest req) {
        return await _service.DoSomethingAsync();  // Thin delegation only
    }
}
```

Generate with: `/backend-facade ModFacade Mod IModService`

### Frontend IPC Service

```typescript
export class ModService extends BaseModuleService {
  async doSomething(profileId: string): Promise<Result> {
    return this.sendMessage('DO_SOMETHING', profileId);
  }
}
export const modService = new ModService();
```

Generate with: `/ipc-service ModService MOD doSomething`

### UI Rules

- **BEM naming**: `.component-name__element--modifier`
- **Font sizes**: 12px or 14px only — never 13px, never below 12px
- **Colors**: CSS variables only (`var(--color-*)`)
- **Conditionals**: Use `classNames()` library

---

## 🔄 Session Workflow

### Starting a Task

1. `git status` — check branch and state
2. `/doc-loader "describe what you're doing" scope` ← mandatory gate; loads this guide + scope-specific docs
3. Use the skill suggested by doc-loader

### During Development

- **Skills first** — generate boilerplate with skills, never write it manually
- **Architecture always** — backend does all work, frontend is pure UI
- **Tests always** — write tests after every bug fix or feature

### Before Committing

1. Tests pass (`dotnet test` + `npm test`)
2. Build succeeds (`dotnet build` + no TS errors)
3. Ask user: "Ready to commit?"
4. Wait for explicit "yes"

---

## 📚 Documentation Map

**Load only what you need — use [KEYWORDS_INDEX.md](KEYWORDS_INDEX.md) to find anything fast.**

| Need | Load This |
|------|-----------|
| Find code/files quickly | [KEYWORDS_INDEX.md](KEYWORDS_INDEX.md) |
| Architecture constraints | [DESIGN_DECISIONS.md](core/DESIGN_DECISIONS.md) |
| Non-automatable patterns | [ADVANCED_PATTERNS.md](core/ADVANCED_PATTERNS.md) |
| Testing patterns + pitfalls | [TESTING_GUIDE.md](ai-assistant/TESTING_GUIDE.md) |
| React hook/closure patterns | [REACT_CLOSURE_PATTERNS.md](ai-assistant/REACT_CLOSURE_PATTERNS.md) |
| Debugging/common issues | [TROUBLESHOOTING.md](ai-assistant/TROUBLESHOOTING.md) |
| All skills with full syntax | [.claude/skills/README.md](../.claude/skills/README.md) |
| Backend C# classes/services | [keywords/BACKEND.md](keywords/BACKEND.md) |
| React components/hooks | [keywords/FRONTEND.md](keywords/FRONTEND.md) |
| How-to tasks | [keywords/HOW_TO.md](keywords/HOW_TO.md) |
| Documentation catalog | [keywords/DOCUMENTATION.md](keywords/DOCUMENTATION.md) |

---

**Workflow**: Skills → Agents → RAG → Manual. Every session. No exceptions.

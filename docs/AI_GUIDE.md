# AI Assistant Guide

**Version:** 7.4
**Last Updated:** 2026-07-06 (remote library redesign + in-app user guide shipped; post-feature cleanup
pass — dedup scrollbar CSS, CompactTitle font clamp, dead code removed. Prior 7.3 pass: StartupValidation
tool + dead LAUNCH_GAME route removed; keywords/BACKEND.md + FRONTEND.md rewritten as compact indexes;
CHANGELOG archived by month; routing is MessageDispatcher → ProfileServiceRouter, there is no AppFacade)

> Mandatory rules are in CLAUDE.md (auto-loaded). This file contains only unique reference content: the skills table, architecture quick-patterns, and documentation map.

---

## Skills — Complete Reference

### Code Generation (10 skills)

| Skill | Usage | Generates |
|-------|-------|-----------|
| `/backend-service` | `Name Module Deps Methods` | C# service + interface + DI + events |
| `/backend-facade` | `Name Module Services` | Thin IPC facade (delegation only) |
| `/ipc-service` | `Name Module Methods` | TypeScript IPC service + singleton |
| `/react-component` | `Name type features` | Component + BEM CSS + hooks |
| `/error-with-i18n` | `CODE params "en msg" "cn msg"` | OperationException + en.json + cn.json |
| `/event-handler` | `Name Module SourceEvents Target` | C# event consolidation handler |
| `/ipc-message-pair` | `Module MessageType ...` | Backend handler + Frontend method |
| `/batch-operation` | `Module Op EntityType Params` | SQL batch + Facade + Frontend |
| `/file-watcher` | `Name Module Path Filters Events` | FileSystemWatcher + disposal |
| `/service-registration` | `Module Interface Impl Lifecycle` | DI registration in ServiceExtensions.cs |

### Release/CI (1 skill)

| Skill | Usage | What It Does |
|-------|-------|--------------|
| `/release-notes` | `[from-tag] [to-ref]` | Auto-generates release notes from git log |

### Discovery & Documentation (11 skills)

| Skill | Usage | What It Does |
|-------|-------|--------------|
| `/skill-loader` | `"task description"` | Routes to relevant code-gen skills |
| `/doc-loader` | `"task" scope` | Routes to relevant docs by scope |
| `/pattern-finder` | `PatternType Module?` | Gives Glob/Grep commands for pattern |
| `/caveman` | `[lite\|full\|ultra]` | Token-optimized terse communication |
| `/post-feature` | (no args) | Audits git diff, suggests doc updates |
| `/doc-update-guide` | `ChangeType Details` | Updates this file with versioning |
| `/doc-update-reference` | `EntryType Details` | Updates KEYWORDS_INDEX.md |
| `/doc-update-technical` | `Document UpdateType Details` | Updates ADVANCED_PATTERNS / DESIGN_DECISIONS |
| `/doc-monitor` | `CheckType Scope` | Audits docs for broken links, redundancy |
| `/doc-cleanup` | `Operation Target Details` | Removes redundant docs |
| `/doc-optimize` | `Document Operation Details` | Splits oversized docs |

---

## Architecture Quick-Patterns

### Backend Service

```csharp
public class ModService : IModService {
    private readonly IModRepository _repository;
    private readonly IProfileEventBus _eventBus;

    public async Task<Result> DoSomethingAsync() {
        var result = await _repository.GetAsync();
        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.CHANGED, data);
        return result;
    }
}
```

Generate with: `/backend-service ModService Mod IModRepository,IProfileEventBus DoSomethingAsync`

### Facade (IPC delegation only)

```csharp
public class ModFacade : BaseFacade {
    private readonly IModService _service;

    private async Task<Result> HandleAsync(IpcRequest req) {
        return await _service.DoSomethingAsync();  // No logic here
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
- **Font sizes**: 12px or 14px only
- **Colors**: CSS variables only (`var(--color-*)`)
- **Conditionals**: Use `classNames()` library

---

## Documentation Map

| Need | Load This |
|------|-----------|
| **Hard-won patterns / constraints (auto-loaded)** | **`.claude/rules/*.md`** — battle-tested wiring chains + gotchas (remote library, in-app guide, UI design, filesystem serialization, enum serialization, download service…). These OVERRIDE generic advice. |
| End-user guide (also in-app Help) | [user-guide/USER_GUIDE.en.md](user-guide/USER_GUIDE.en.md) · [.cn.md](user-guide/USER_GUIDE.cn.md) |
| Find code/files | [KEYWORDS_INDEX.md](KEYWORDS_INDEX.md) |
| Architecture constraints | [DESIGN_DECISIONS.md](core/DESIGN_DECISIONS.md) |
| Non-automatable patterns | [ADVANCED_PATTERNS.md](core/ADVANCED_PATTERNS.md) |
| Testing patterns | [TESTING_GUIDE.md](ai-assistant/TESTING_GUIDE.md) |
| React closures/hooks | [REACT_CLOSURE_PATTERNS.md](ai-assistant/REACT_CLOSURE_PATTERNS.md) |
| Debugging | [TROUBLESHOOTING.md](ai-assistant/TROUBLESHOOTING.md) |
| Backend reference | [keywords/BACKEND.md](keywords/BACKEND.md) |
| Frontend reference | [keywords/FRONTEND.md](keywords/FRONTEND.md) |

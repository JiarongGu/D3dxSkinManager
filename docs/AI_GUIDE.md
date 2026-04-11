# AI Assistant Guide

**Version:** 5.1
**Last Updated:** 2026-04-12
**Role:** Deep reference — load this when you need workflow patterns, architecture examples, or the full skills list. Mandatory rules and the skills quick-reference are in `CLAUDE.md` (auto-loaded every session) — you do not need to load this guide for routine tasks.

---

## 🎯 Quick Start

> **For routine tasks** — CLAUDE.md already has everything: skills table, doc-loader instruction, architecture rules. Load this guide only when you need workflow examples or the full skills reference.

### Skills-First Workflow
```bash
# Step 1: load docs + discover the right skill
/doc-loader "describe your task" backend|frontend|ipc|testing|architecture

# Step 2: run the suggested skill, e.g.
/backend-service TextureValidationService Mod IFileHelper ValidateAsync
```

### Understanding Existing Code
```typescript
// Explore agent loads relevant docs automatically
Task(subagent_type: "Explore", description: "Understand mod loading",
     prompt: "How does mod loading work? Thoroughness: medium")

// Or: check KEYWORDS_INDEX.md → load specific doc
```

---

## 🚀 The System (Skills + RAG + Agents)

### Skills = Code Generation (18 available)

See [.claude/skills/README.md](../.claude/skills/README.md) for the complete reference.
Quick-access table (for everyday use) is in `CLAUDE.md` section 4.

**Code Generation:**
`/backend-service` `/backend-facade` `/ipc-service` `/react-component`
`/error-with-i18n` `/event-handler` `/ipc-message-pair` `/batch-operation`
`/file-watcher` `/service-registration`

**Discovery:**
`/doc-loader` (loads docs + suggests skills) · `/pattern-finder` (finds existing patterns)

**Doc Maintenance:**
`/doc-update-guide` · `/doc-update-reference` · `/doc-update-technical`
`/doc-monitor` · `/doc-cleanup` · `/doc-optimize`

### RAG = Understanding (Load docs on-demand)

**Start here**: [KEYWORDS_INDEX.md](KEYWORDS_INDEX.md) — find anything quickly

**Key Documents**:
- [DESIGN_DECISIONS.md](core/DESIGN_DECISIONS.md) — architecture constraints
- [ADVANCED_PATTERNS.md](core/ADVANCED_PATTERNS.md) — non-automatable patterns
- [TROUBLESHOOTING.md](ai-assistant/TROUBLESHOOTING.md) — common issues

### Agents = Research + Planning

**Explore agent** — understand existing code:
```typescript
Task(subagent_type: "Explore", description: "Find X pattern",
     prompt: "How is X implemented? Thoroughness: medium")
```

**Plan agent** — plan a new feature:
```typescript
Task(subagent_type: "Plan", description: "Plan Y feature",
     prompt: "Plan Y. Load DESIGN_DECISIONS.md. Create detailed plan.")
```

---

## 🏛️ Architecture Rules

> These rules are also enforced via `CLAUDE.md` (auto-loaded). Shown here as reference with code examples.

### Architecture Boundaries
```csharp
// Backend: ALL heavy operations
// Frontend: UI only, NO data processing
// Facades: THIN delegation ONLY (no business logic, no events)
// Services: Business logic + Event emission
```

### Error Handling (Unified Pattern)
```csharp
// Backend
throw new OperationException("ERROR_CODE",
    new Dictionary<string, string> { {"param", value} });

// Frontend
catch (error: unknown) { handleError(error); }

// i18n: Add to BOTH en.json AND cn.json
```

### Module Boundaries
- ❌ **NEVER** access other module's repositories
- ✅ **ALWAYS** use module facades for cross-module calls

### Event Emission
- ✅ Services emit events (inject `IProfileEventBus`)
- ❌ Facades **never** emit events (thin delegation layer only)

### Data Conventions
```typescript
// ✅ undefined for missing data
const [mod, setMod] = useState<ModInfo>();

// ✅ null ONLY for React render returns
if (!data) return null;
```

---

## 📋 Workflow Patterns

### Pattern 1: New Backend Service

```bash
# Generate complete service with skill
/backend-service TextureValidationService Mod IFileHelper ValidateAsync

# Result: Interface + Implementation + DI + Events + Registration
# No manual coding needed for boilerplate
```

### Pattern 2: Add Error Handling

```bash
# Generates exception + i18n in both languages
/error-with-i18n TEXTURE_INVALID fileName,formats \
  "Invalid texture: {{fileName}}" \
  "无效的纹理：{{fileName}}"

# Result: OperationException code + en.json + cn.json updated
```

### Pattern 3: Understand Existing Code

```typescript
// Don't read docs manually - use Explore agent
Task(subagent_type: "Explore",
     description: "Understand category tree",
     prompt: "How is category tree built? Find CategoryService. Thoroughness: medium")

// Agent auto-loads relevant docs and code
```

### Pattern 4: Plan New Feature

```typescript
// Agent loads DESIGN_DECISIONS.md + creates plan
Task(subagent_type: "Plan",
     description: "Plan mod export",
     prompt: "Plan mod export to zip. Load DESIGN_DECISIONS.md. Find compression code. Create detailed plan.")

// Review plan BEFORE coding
```

---

## 🏗️ Core Architecture (Minimal Reference)

**For detailed patterns**: See [ADVANCED_PATTERNS.md](core/ADVANCED_PATTERNS.md)

### Service Layer (Business Logic + Events)
```csharp
public class ModService : IModService {
    private readonly IProfileEventBus _eventBus;  // Always inject

    public async Task<Result> DoSomethingAsync() {
        // Business logic
        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.CHANGED, data);
    }
}
```

**Generate with**: `/backend-service ModService Mod IProfileEventBus DoSomethingAsync`

### Facade Layer (Thin IPC Only)
```csharp
public class ModFacade : BaseFacade {
    private readonly IModService _service;

    private async Task<Result> HandleAsync(IpcRequest request) {
        return await _service.DoSomethingAsync();  // Just delegate
    }
}
```

**Generate with**: `/backend-facade ModFacade Mod IModService`

### Frontend IPC
```typescript
export class ModService extends BaseModuleService {
  async doSomething(profileId: string): Promise<Result> {
    return this.sendMessage('DO_SOMETHING', profileId);
  }
}
```

**Generate with**: `/ipc-service ModService MOD doSomething`

---

## 🎨 UI Guidelines (Minimal)

- **BEM naming**: `.component-name__element--modifier`
- **Font sizes**: 12px or 14px only (never 13px or below 12px)
- **Colors**: Use CSS variables (`var(--color-*)`)
- **classnames**: Use `classNames()` library for conditionals

**For details**: See [FRONTEND.md](keywords/FRONTEND.md) (load via KEYWORDS_INDEX when needed)

---

## 🔄 Session Workflow

### Starting Session
> CLAUDE.md auto-loads — mandatory rules are already active. No manual setup needed.

1. `git status` — check current branch and state
2. Ask user: "What to work on?"
3. **Use skills** for code generation, **agents** for research/planning

### During Development
1. **Skills first** - Use skills for all repetitive patterns
2. **Agents second** - Use Explore/Plan for understanding/planning
3. **RAG last** - Load specific docs from KEYWORDS_INDEX only when needed
4. **Tests always** - After every bug fix or feature: `/doc-loader "write tests for <what>" testing`

### Before Committing
1. Write tests for changes (see [TESTING_GUIDE.md](ai-assistant/TESTING_GUIDE.md))
2. Build succeeds (`dotnet build` + no TS errors)
3. Tests pass (`dotnet test` + `npm test`)
4. **Ask user**: "Ready to commit?"
5. Wait for explicit "yes"

---

## 📚 Documentation Map

**Don't load everything** - Use this map to find what you need:

| Need | Load This | Why |
|------|-----------|-----|
| **Generate code** | [.claude/skills/README.md](../.claude/skills/README.md) | 18 skills available |
| **Find something** | [KEYWORDS_INDEX.md](KEYWORDS_INDEX.md) | Quick lookup |
| **Check architecture** | [DESIGN_DECISIONS.md](core/DESIGN_DECISIONS.md) | Architecture constraints |
| **Complex patterns** | [ADVANCED_PATTERNS.md](core/ADVANCED_PATTERNS.md) | Non-automatable patterns |
| **Debugging** | [TROUBLESHOOTING.md](ai-assistant/TROUBLESHOOTING.md) | Common issues |
| **Testing** | [TESTING_GUIDE.md](ai-assistant/TESTING_GUIDE.md) | Patterns + mandatory rules + pitfalls |

---

## ⚡ Quick Command Reference

### Git
```bash
git status           # Check first
git add <files>      # Stage
git commit -m "msg"  # Only after user approval
```

### Build
```bash
.\build-production.ps1      # Production build
dotnet build <project>      # Backend only
```

### Common Tasks

| Task | Command |
|------|---------|
| **Create service** | `/backend-service Name Module Deps Methods` |
| **Add error** | `/error-with-i18n CODE params "en" "cn"` |
| **Create IPC** | `/ipc-service Name Module Methods` |
| **Create component** | `/react-component Name type features` |
| **Understand code** | Explore agent (medium thoroughness) |
| **Plan feature** | Plan agent → loads DESIGN_DECISIONS.md |

---

## 🔍 Key Takeaways

### 1. Skills-First Development
- ✅ **PREFER** skills over manual coding (18 skills available)
- ✅ **USE** `/skill-name` for all repetitive patterns
- ❌ **DON'T** write boilerplate manually

### 2. RAG-Driven Understanding
- ✅ **START** with KEYWORDS_INDEX.md to find docs
- ✅ **LOAD** specific docs on-demand (not everything)
- ✅ **USE** Explore agent instead of manual doc reading

### 3. Agent-Assisted Work
- ✅ **Explore agent** for understanding code
- ✅ **Plan agent** for planning features
- ❌ **DON'T** skip planning phase

### 4. Mandatory Rules
See `CLAUDE.md` (auto-loaded). Summary: git approval, architecture boundaries, testing after every change.

---

**Workflow**: Skills → Agents → RAG → Manual (in that order)

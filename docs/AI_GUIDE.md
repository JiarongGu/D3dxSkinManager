# AI Assistant Guide

**Version:** 4.0 (Lean Edition)
**Last Updated:** 2026-04-11
**Critical:** NEVER commit without explicit user approval!

---

## 🎯 Quick Start

### For New Features: Skills-First Workflow

```bash
# 1. Generate code with skills (preferred)
/backend-service ServiceName Module Dependencies Methods
/error-with-i18n ERROR_CODE params "en msg" "cn msg"
/ipc-service ServiceName Module Methods
/react-component ComponentName type features

# 2. For unique logic: Check existing patterns via KEYWORDS_INDEX.md
# 3. If unsure: Use Explore agent to find similar code
```

### For Understanding Code: RAG-Driven

```typescript
// Use Explore agent - loads relevant docs automatically
Task(subagent_type: "Explore", description: "Understand mod loading",
     prompt: "How does mod loading work? Thoroughness: medium")

// Or manually: Check KEYWORDS_INDEX.md → Load specific doc
```

---

## 🚀 The System (Skills + RAG + Agents)

### Skills = Code Generation (18 available)

**USE SKILLS FOR ALL REPETITIVE CODE**

See [.claude/skills/README.md](../.claude/skills/README.md) for complete list.

**Code Generation** (12 skills):
- `/backend-service` - Service + DI + events
- `/backend-facade` - Thin IPC facade
- `/ipc-service` - Frontend IPC
- `/error-with-i18n` - Errors + i18n (en + cn)
- `/react-component` - React + CSS
- `/event-handler` - Event consolidation
- `/service-registration` - DI registration
- `/batch-operation` - SQL batch operations
- `/file-watcher` - FileSystemWatcher

**Doc Maintenance** (6 skills):
- `/doc-monitor` - Health checks
- `/doc-cleanup` - Remove redundancy
- `/doc-optimize` - RAG optimization

### RAG = Understanding (Load docs on-demand)

**Start here**: [KEYWORDS_INDEX.md](KEYWORDS_INDEX.md) - Find anything quickly

**Key Documents**:
- [DESIGN_DECISIONS.md](core/DESIGN_DECISIONS.md) - Architecture constraints
- [ADVANCED_PATTERNS.md](core/ADVANCED_PATTERNS.md) - Non-automatable patterns
- [TROUBLESHOOTING.md](ai-assistant/TROUBLESHOOTING.md) - Common issues

**Don't load everything** - Use KEYWORDS_INDEX to find what you need.

### Agents = Automation (Research + Planning)

**Explore Agent** - Understand existing code
```typescript
Task(subagent_type: "Explore", description: "Find X pattern",
     prompt: "How is X implemented? Thoroughness: medium")
```

**Plan Agent** - Plan new features
```typescript
Task(subagent_type: "Plan", description: "Plan Y feature",
     prompt: "Plan Y. Load DESIGN_DECISIONS.md. Create detailed plan.")
```

---

## 🔥 Critical Rules (BREAK = MAJOR ISSUES)

### 1. Git Commits
- ✅ **ALWAYS** ask user before committing
- ❌ **NEVER** commit without explicit "yes"

### 2. Architecture Boundaries
```csharp
// Backend: ALL heavy operations
// Frontend: UI only, NO data processing
// Facades: THIN delegation ONLY (no business logic, no events)
// Services: Business logic + Event emission
```

### 3. Error Handling (Unified Pattern)
```csharp
// Backend
throw new OperationException("ERROR_CODE",
    new Dictionary<string, string> { {"param", value} });

// Frontend
catch (error: unknown) { handleError(error); }

// i18n: Add to BOTH en.json AND cn.json
```

### 4. Module Boundaries
- ❌ **NEVER** access other module's repositories
- ✅ **ALWAYS** use module facades for cross-module calls

### 5. Event Emission
- ✅ Services emit events (inject IProfileEventBus)
- ❌ Facades NEVER emit events (thin layer only)

### 6. Data Conventions
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
1. Git status (`git status`)
2. Load this guide (AI_GUIDE.md)
3. Ask user: "What to work on?"
4. **Use skills** for code generation
5. **Use agents** for research/planning

### During Development
1. **Skills first** - Use skills for all repetitive patterns
2. **Agents second** - Use Explore/Plan for understanding/planning
3. **RAG last** - Load specific docs from KEYWORDS_INDEX only when needed

### Before Committing
1. Build succeeds
2. Tests pass
3. **Ask user**: "Ready to commit?"
4. Wait for explicit "yes"

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
| **Testing** | [TESTING_GUIDE.md](ai-assistant/TESTING_GUIDE.md) | Testing patterns |

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

### 4. Critical Discipline
- ✅ **ALWAYS** ask before git commits
- ✅ **NEVER** access other module's repositories
- ✅ **ALWAYS** use unified error handling (OperationException)

---

**Remember**:
- **Skills** = Code generation (use for all patterns)
- **RAG** = Understanding (load docs on-demand)
- **Agents** = Automation (research + planning)

**Workflow**: Skills → Agents → RAG → Manual (in that order)

---

**End of Guide** 🚀

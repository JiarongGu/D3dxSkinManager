# AI Assistant Guide

**Version:** 3.0
**Last Updated:** 2026-03-03
**Critical:** NEVER commit without explicit user approval!

---

## 🎯 START HERE: RAG System is YOUR PRIMARY TOOL

**⚠️ CRITICAL**: Before writing ANY code, ALWAYS use the RAG system to load relevant documentation.

### Why RAG First?

1. **Prevents mistakes** - Architecture decisions, patterns, and constraints are documented
2. **Saves tokens** - Load only what you need, when you need it
3. **Stays updated** - Documentation is the source of truth, not this guide

### RAG Quick Reference

| When You Need | Load This First | Then Load |
|--------------|----------------|-----------|
| **Architecture/design decisions** | `docs/core/DESIGN_DECISIONS.md` | `docs/architecture/*.md` |
| **Find where something is** | `docs/KEYWORDS_INDEX.md` | Domain-specific file |
| **How to implement X** | `docs/ai-assistant/WORKFLOWS.md` | `docs/keywords/HOW_TO.md` |
| **Something not working** | `docs/ai-assistant/TROUBLESHOOTING.md` | Module-specific docs |
| **Understanding existing code** | `docs/architecture/CURRENT_ARCHITECTURE.md` | Module architecture |

---

## 🔥 Critical Rules (BREAK THESE = MAJOR ISSUES)

### 1. Git Commits
```bash
# ALWAYS ask before committing
"Ready to commit these changes?"  # WAIT for explicit "yes"
```

### 2. Architecture
```csharp
// Backend: ALL heavy operations
// Frontend: UI only, NO data processing
// Paths: Relative in DB, absolute at runtime
// DI: Constructor injection via interfaces
```

### 3. Error Handling
```typescript
// ✅ Always use handleError utility
import { handleError } from '@/shared/utils/errorHandler';
try {
  await operation();
} catch (error: unknown) {
  handleError(error);  // Shows user-friendly message
}

// ❌ Never manually extract error messages
catch (error) {
  notification.error((error as Error).message);  // WRONG
}
```

### 4. Data Conventions
```typescript
// ✅ undefined for missing data
const [mod, setMod] = useState<ModInfo>();

// ✅ null ONLY for React render returns
if (!data) return null;
```

---

## 📋 Before Writing Code Checklist

- [ ] **Load RAG docs** - Check KEYWORDS_INDEX.md or DESIGN_DECISIONS.md first
- [ ] **Check existing patterns** - Load WORKFLOWS.md for similar examples
- [ ] **Verify module boundaries** - Never access other module's repositories
- [ ] **Plan error handling** - Use ErrorCode system

---

## 🏗️ Core Patterns (Minimal Reference)

### Backend Service
```csharp
// 1. Interface
public interface IModService {
    Task<List<ModInfo>> GetAllAsync();
}

// 2. Implementation with DI
public class ModService : IModService {
    private readonly IModRepository _repository;

    public ModService(IModRepository repository) {
        _repository = repository;
    }
}

// 3. Register in {Module}ServiceExtensions.cs
services.AddSingleton<IModService, ModService>();
```

### Frontend Service
```typescript
class ModService extends BaseModuleService {
  constructor() {
    super('MOD');  // Module name
  }

  async getAllMods(profileId?: string): Promise<ModInfo[]> {
    return this.sendArrayMessage<ModInfo>('GET_ALL', profileId);
  }
}

export const modService = new ModService();
```

### IPC Events (Module + Type Pattern)
```csharp
// Backend - NO module prefix in type names
await _eventBus.EmitAsync(
    ModuleNames.MOD,      // Module
    ModEvents.LOADED,     // Type (NOT "MOD_LOADED")
    new { sha }
);

// Frontend
eventBus.subscribe(
  Module.MOD,
  ModEventType.LOADED,
  (event) => console.log(event.payload.sha)
);
```

### State Management (Zustand)
```typescript
// Store
export const useModsStore = create<ModsState>((set) => ({
  mods: [],
  setMods: (mods) => set({ mods }),
}));

// Component
const mods = useModsStore((state) => state.mods);
```

### Logging Levels
```csharp
// Use correct log level based on frequency
_logger.Verbose($"Per-item detail");     // High-frequency
_logger.Info($"Step completed");         // Milestones
_logger.Warn($"Recoverable issue");      // Potential problems
```

---

## 🚨 Common Mistakes

### ❌ Don't Do This
```typescript
// 1. Manual error handling
catch (error) {
  notification.error((error as Error).message);
}

// 2. null for missing data
const [mod, setMod] = useState<ModInfo | null>(null);

// 3. Generic CSS without BEM
<div className="item">  // Missing component prefix

// 4. Process data in frontend
const filtered = mods.filter(...);  // Should be in backend

// 5. Skip RAG lookup
// *starts coding without checking docs*
```

### ✅ Do This Instead
```typescript
// 1. Use handleError
catch (error: unknown) {
  handleError(error);
}

// 2. undefined for missing data
const [mod, setMod] = useState<ModInfo>();

// 3. BEM with component prefix
<div className="mod-list-item">

// 4. Backend handles data
const mods = await modService.getFiltered(criteria);

// 5. Check RAG first
// Read KEYWORDS_INDEX.md → Load relevant doc → Write code
```

---

## 📚 RAG Document Structure

### Must-Read Before Coding
- `docs/KEYWORDS_INDEX.md` - Find where things are
- `docs/core/DESIGN_DECISIONS.md` - All architectural constraints
- `docs/ai-assistant/WORKFLOWS.md` - Implementation patterns

### Reference During Implementation
- `docs/ai-assistant/GUIDELINES.md` - Do's and don'ts
- `docs/ai-assistant/TROUBLESHOOTING.md` - Common errors
- `docs/keywords/HOW_TO.md` - Step-by-step guides

### Domain-Specific
- `docs/keywords/BACKEND.md` - C# services/facades
- `docs/keywords/FRONTEND.md` - React components/hooks
- `docs/architecture/*.md` - System architecture

---

## 🎨 UI/CSS Guidelines

### BEM Naming (Component CSS)
```css
/* ComponentName.css */
.mod-list-panel { }                    /* Block */
.mod-list-panel-item { }               /* Element */
.mod-list-panel-item--selected { }     /* Modifier */
```

### Font Sizes
- **Regular text**: 14px (body text, labels)
- **Small text**: 12px (secondary info)
- **NEVER** use 13px or below 12px

### classnames Library
```typescript
// ✅ Always use classnames for conditionals
import classNames from 'classnames';

className={classNames('mod-list-item', {
  'mod-list-item--selected': isSelected,
  'mod-list-item--disabled': isDisabled,
})}

// ❌ Don't use template strings
className={`mod-list-item ${isSelected ? 'selected' : ''}`}
```

---

## 🔄 Workflow for New Feature

1. **Load RAG docs**
   ```bash
   Read: docs/KEYWORDS_INDEX.md
   → Find relevant module docs
   → Load WORKFLOWS.md for patterns
   ```

2. **Check constraints**
   ```bash
   Read: docs/core/DESIGN_DECISIONS.md
   → Verify approach follows architecture
   ```

3. **Implement**
   ```bash
   Backend: Service → Repository → Facade
   Frontend: Service → Hook → Component
   ```

4. **Error handling**
   ```bash
   Backend: throw ModException(ErrorCodes.X)
   Frontend: catch + handleError(error)
   ```

5. **Test & commit**
   ```bash
   Build → Test → Ask user → Commit
   ```

---

## 📊 Token Optimization Strategy

### Load Docs in Order
1. **Start narrow**: `KEYWORDS_INDEX.md` → specific domain file
2. **Expand if needed**: Load related architecture docs
3. **Reference only**: Don't load entire guide into context

### When to Load What
- **First time working on module**: Load module architecture + WORKFLOWS.md
- **Adding feature**: Load HOW_TO.md + existing similar code
- **Fixing bug**: Load TROUBLESHOOTING.md + module-specific docs
- **Architecture question**: Load DESIGN_DECISIONS.md

---

## 🎯 Session Workflow

### Starting a Session
1. Check git status (`git status`)
2. Load `AI_GUIDE.md` (this file)
3. Ask user: "What would you like to work on?"
4. **Use RAG**: Load relevant docs from KEYWORDS_INDEX.md

### During Development
1. **Check RAG first** before making architectural decisions
2. Follow loaded patterns exactly
3. Use correct error handling (handleError utility)
4. Log at appropriate levels (Verbose for details, Info for milestones)

### Before Committing
1. Build succeeds
2. No hardcoded values
3. Error handling in place
4. **Ask user**: "Ready to commit these changes?"
5. Wait for explicit approval

---

## ⚡ Quick Command Reference

### Git
```bash
git status                    # Always check first
git add <files>              # Stage specific files
git commit -m "message"      # Only after user approval
```

### Build
```bash
dotnet build <project>.csproj        # Backend
cd <frontend> && npm run build       # Frontend
```

### Common Tasks
| Task | First Doc to Load |
|------|------------------|
| Add new module | `MODULE_ARCHITECTURE.md` |
| Add IPC handler | `WORKFLOWS.md` |
| Fix IPC issue | `TROUBLESHOOTING.md` |
| Add React component | `FRONTEND.md` + `WORKFLOWS.md` |
| Add backend service | `BACKEND.md` + `WORKFLOWS.md` |

---

## 🔍 Key Takeaways

### 1. RAG System is Mandatory
- **ALWAYS** check KEYWORDS_INDEX.md before coding
- **ALWAYS** load DESIGN_DECISIONS.md for architectural questions
- **ALWAYS** load WORKFLOWS.md for implementation patterns

### 2. Error Handling
- Backend: `throw ModException(ErrorCodes.X, message, context)`
- Frontend: `catch (error: unknown) { handleError(error); }`

### 3. Module Boundaries
- **NEVER** access other module's repositories
- **ALWAYS** use module facades for IPC
- **ALWAYS** inject dependencies via constructor

### 4. UI Consistency
- Use BEM naming for component CSS
- Use classnames library for conditionals
- Font sizes: 12px or 14px only
- Load `visual-enhancements.css` for global utilities

### 5. Git Discipline
- **ALWAYS** ask before committing
- **NEVER** commit without user approval
- Include clear commit messages

---

## 📖 Remember

> **The RAG system contains the complete truth.**
> **This guide is just a quick reference.**
> **When in doubt, load the relevant documentation.**

### Priority Order
1. **RAG docs** (source of truth)
2. **Existing code** (working examples)
3. **This guide** (quick reference only)

---

**End of Guide - Now use the RAG system! 🚀**

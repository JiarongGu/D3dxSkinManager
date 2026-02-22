# AI Assistant Guide for D3dxSkinManager

> **🚨 CRITICAL: NEVER COMMIT WITHOUT EXPLICIT USER APPROVAL 🚨**
>
> This is a rewrite of a production mod management application. You MUST ask the user "Ready to commit?" and wait for their explicit approval before running `git commit`.

> **LIVING DOCUMENT:** This guide is maintained by AI assistants across sessions.
>
> **🤖 CRITICAL FOR AI:** See [ai-assistant/DOCUMENTATION_MAINTENANCE.md](ai-assistant/DOCUMENTATION_MAINTENANCE.md) ⭐⭐⭐
>
> **Documentation Update Triggers:**
> - Spent >5 min finding info → Add to KEYWORDS_INDEX.md
> - Fixed a bug → Update CHANGELOG.md + consider TROUBLESHOOTING.md
> - Created component/service → Create feature doc + update indexes
> - Struggled with something → Add to TROUBLESHOOTING.md
> - Discovered pattern → Add to GUIDELINES.md
>
> **⚠️ CHANGELOG CRITICAL RULE:**
> - **Main CHANGELOG.md MUST be < 200 lines** (check: `wc -l docs/CHANGELOG.md`)
> - If > 150 lines: Archive old entries BEFORE adding new
> - See [maintenance/CHANGELOG_MANAGEMENT.md](maintenance/CHANGELOG_MANAGEMENT.md)
>
> **If you learned it, document it. Future AI sessions depend on you!**

**Version:** 1.4
**Last Updated:** 2026-02-22
**Project Type:** .NET 10 + WinForms + WebView2 + React 18 + TypeScript + Vite (Desktop Application)
**Audience:** AI Assistants (Primary), Human Developers (Reference)

---

## Table of Contents

1. [🎯 Purpose & Audience](#-purpose--audience)
2. [📁 Folder-Based Navigation (RAG Optimized)](#-folder-based-navigation-rag-optimized)
3. [7 Critical Rules (Non-Negotiable)](#7-critical-rules-non-negotiable)
4. [🚀 Quick Start for AI Assistants](#-quick-start-for-ai-assistants)
5. [🤖 RAG Retrieval Strategy](#-rag-retrieval-strategy)
6. [Detailed AI Guides](#detailed-ai-guides)
7. [Key Facts](#key-facts)
8. [📂 Documentation Map](#-documentation-map-folder-based-index)
9. [Token Optimization](#token-optimization)
10. [How to Update This Guide](#how-to-update-this-guide)

---

## 🎯 Purpose & Audience

### For AI Assistants (PRIMARY AUDIENCE)
Navigation system for RAG-based retrieval (folder → file routing), critical behavioral rules (7 non-negotiable), quick references to detailed guides. **Use folder structure as primary index**.

### For Human Developers (REFERENCE)
Use root [README.md](../README.md) for project setup, `docs/core/` for architecture, and `docs/features/` for features. Sections marked 🤖 are AI-specific.

---

## 📁 Folder-Based Navigation (RAG Optimized)

| Folder | Purpose | Query Type |
|--------|---------|------------|
| `docs/architecture/` | System architecture & design | "How does... work?" |
| `docs/ai-assistant/` | AI workflows & troubleshooting | "How do I..." |
| `docs/features/` | Feature deep-dives | "Where is..." |
| `docs/core/` | Project fundamentals | "What is..." |

**Quick Lookups:**
- **🔥 What changed recently?** → [CHANGELOG.md](CHANGELOG.md) ⭐⭐⭐ **START HERE**
- **Component/Service location?** → [KEYWORDS_INDEX.md](KEYWORDS_INDEX.md) (routing hub) ⭐⭐⭐
- **System architecture?** → [architecture/CURRENT_ARCHITECTURE.md](architecture/CURRENT_ARCHITECTURE.md) ⭐⭐⭐
- **Detailed changes?** → [changelogs/2026-02/](changelogs/2026-02/) (monthly detailed logs)
- **Project setup?** → [core/DEVELOPMENT.md](core/DEVELOPMENT.md)

---

## 🗂️ Keywords Index Routing System (v4.0)

> **NEW (2026-02-20):** Keywords index now uses routing system for faster lookups!

### How It Works

1. **Start at routing hub:** [KEYWORDS_INDEX.md](KEYWORDS_INDEX.md) (~150 lines)
2. **Identify your domain:** Backend? Frontend? Documentation? How-to?
3. **Load specific domain file:** Only load what you need

### Domain Files

| What You Need | Load This File | Size |
|---------------|----------------|------|
| **Backend C# code** | [keywords/BACKEND.md](keywords/BACKEND.md) | ~350 lines |
| **Frontend React code** | [keywords/FRONTEND.md](keywords/FRONTEND.md) | ~550 lines |
| **Documentation files** | [keywords/DOCUMENTATION.md](keywords/DOCUMENTATION.md) | ~220 lines |
| **How-to guides** | [keywords/HOW_TO.md](keywords/HOW_TO.md) | ~370 lines |

### Benefits

✅ **Faster lookups** - Load only relevant domain file (not everything)
✅ **Token efficient** - 150-550 lines per query (not 1,640 lines)
✅ **Clear routing** - Know exactly which file to load
✅ **Scalable** - Can add sub-folders if files grow > 500 lines

### Usage Examples

**Query:** "Where is ModFacade?"
1. It's backend code → Load [keywords/BACKEND.md](keywords/BACKEND.md)
2. Ctrl+F "ModFacade" → Find `Modules/Mods/ModFacade.cs`
3. Load source file

**Query:** "How do I add a new service?"
1. It's a how-to question → Load [keywords/HOW_TO.md](keywords/HOW_TO.md)
2. Find "Adding Services" section with step-by-step guide

**Query:** "Find useModData hook"
1. It's frontend code → Load [keywords/FRONTEND.md](keywords/FRONTEND.md)
2. Ctrl+F "useModData" → Find `src/hooks/useModData.ts`

### Maintenance Rules

- **Main index** (KEYWORDS_INDEX.md) < 200 lines (routing only)
- **Each domain file** < 500 lines
- **If file > 500 lines** → Create sub-folder (e.g., `keywords/frontend/COMPONENTS.md`)
- **Check sizes:** `wc -l docs/KEYWORDS_INDEX.md docs/keywords/*.md`

See [maintenance/KEYWORDS_INDEX_MANAGEMENT.md](maintenance/KEYWORDS_INDEX_MANAGEMENT.md) for details.

---

## 7 Critical Rules (Non-Negotiable)

### 1. **ALWAYS follow .NET + React Best Practices**
   #### Backend (.NET/C#):
   - **⭐⭐⭐ CRITICAL: Follow Domain-Driven Design principles** (see [architecture/DOMAIN_DESIGN.md](architecture/DOMAIN_DESIGN.md))
     - ✅ Services handle business logic (separation of concerns)
     - ✅ Always use service layer - **NEVER bypass to access repositories directly**
     - ✅ Depend on interfaces (IService), not concrete implementations
     - ✅ Keep services focused - Single Responsibility Principle (< 1000 lines)
     - ✅ Use existing services - Don't reimplement logic
     - ❌ **NEVER** access repositories from other modules
     - ❌ **NEVER** reimplement logic that exists in other services
     - ❌ **NEVER** create god classes (services with 10+ dependencies)
   - Use async/await for all I/O operations
   - **ALWAYS use dependency injection** - Never create service instances manually in application code
     - ✅ Services should inject dependencies through constructor: `public MyService(IDependency dep)`
     - ✅ DI registration can use `new` for utilities: `services.AddSingleton(sp => new PathHelper(dataPath))`
     - ❌ Never create services manually in application code: `var service = new MyService()`
   - Use interfaces for dependency injection
   - Inject all dependencies through constructor parameters
   - Proper exception handling with try-catch
   - Use `using` statements for IDisposable resources
   - **ALWAYS use relative paths** for data stored in database/config files (see [architecture/PATH_CONVENTIONS.md](architecture/PATH_CONVENTIONS.md))
   - Use `PathHelper` service to convert between absolute and relative paths
   - **⭐⭐⭐ CRITICAL: Service Registration Patterns**
     - The custom `AddSingleton` helper in CoreServiceExtensions does NOT support factory functions
     - ✅ **CORRECT**: `AddSingleton<IService, ServiceImpl>(services);`
     - ❌ **WRONG**: `services.AddSingleton<IService>(sp => new ServiceImpl(...));`
     - If you need complex initialization, use constructor DI with multiple constructors
   - **⭐⭐⭐ CRITICAL: Always Use GlobalPathService for Paths**
     - Never construct file paths manually in services
     - ✅ **CORRECT**: Use `IGlobalPathService.LogsDirectory`
     - ❌ **WRONG**: `Path.Combine(baseDir, "data", "logs")`
     - GlobalPathService centralizes all path management
     - See [architecture/LOGGING_ARCHITECTURE.md](architecture/LOGGING_ARCHITECTURE.md) for examples
   - **⭐⭐⭐ CRITICAL: Use IProgressReporter for Long-Running Operations**
     - See [features/OPERATION_NOTIFICATION_SYSTEM.md](features/OPERATION_NOTIFICATION_SYSTEM.md) ⭐⭐⭐
     - **ALL operations taking >1 second MUST report progress**
     - **Inject IProgressReporter**: Constructor parameter in Facade classes
     - **Create operations**: `_progressReporter.CreateOperation(operationId, title, type)`
     - **Report progress**: `_progressReporter.UpdateProgress(operationId, percent, status)`
     - **Complete operations**: `_progressReporter.CompleteOperation(operationId, result)`
     - **Handle failures**: `_progressReporter.FailOperation(operationId, error)`
     - **Example**:
       ```csharp
       // ✅ GOOD: Reports progress for long operation
       public async Task<MessageResponse> LoadModAsync(string sha, string profileId) {
           var opId = Guid.NewGuid().ToString();
           _progressReporter.CreateOperation(opId, $"Loading {sha}", OperationType.ModLoad);
           try {
               _progressReporter.UpdateProgress(opId, 50, "Extracting archive...");
               // ... do work ...
               _progressReporter.CompleteOperation(opId, "Mod loaded successfully");
               return MessageResponse.Success();
           } catch (Exception ex) {
               _progressReporter.FailOperation(opId, ex.Message);
               throw;
           }
       }
       ```

   #### Frontend (React/TypeScript):
   - Functional components with hooks (no class components)
   - TypeScript strict mode - avoid `any` type
   - **⭐⭐⭐ CRITICAL: All frontend services MUST extend BaseModuleService**
     - ✅ Correct: `class MyService extends BaseModuleService { constructor() { super('MODULE_NAME'); } }`
     - ✅ Use `this.sendMessage()` for IPC calls, not `bridgeService.sendMessage()` directly
     - ❌ Wrong: Object literal services with direct bridgeService calls
     - See classificationService.ts, languageService.ts for examples
   - **ALWAYS use React Context for state management** - No prop drilling
     - See [architecture/FRONTEND_CONTEXT_ARCHITECTURE.md](architecture/FRONTEND_CONTEXT_ARCHITECTURE.md) ⭐⭐⭐
     - ProfileContext for profile state: `const { selectedProfileId } = useProfile()`
     - OperationContext for operation notifications: `const { activeOperations } = useOperation()`
     - Module contexts (ModsContext, etc.) for module-specific state
     - NO global variables (`window.__selectedProfileId` removed)
   - **IPC Message Format:** profileId at TOP LEVEL, NOT in payload
     - ✅ Correct: `sendMessage({ module: 'MOD', type: 'GET_ALL', profileId })`
     - ❌ Wrong: `sendMessage({ module: 'MOD', type: 'GET_ALL', payload: { profileId } })`
   - **⭐⭐⭐ CRITICAL: Internationalization (i18n) Required**
     - See [features/INTERNATIONALIZATION.md](features/INTERNATIONALIZATION.md) ⭐⭐⭐
     - See [how-to/ADD_I18N_TO_COMPONENT.md](how-to/ADD_I18N_TO_COMPONENT.md) ⭐⭐⭐
     - **ALL user-facing text MUST use i18n**: Use `t('key')` instead of hardcoded strings
     - **Translation keys**: Flat structure in `Languages/en.json` and `Languages/cn.json`
     - **Usage**: `const { t } = useTranslation();` then `t('mods.actions.load')`
     - **Adding new keys**: Add to BOTH en.json AND cn.json (maintain 100% parity)
     - **DO NOT** use hardcoded English strings in any component
     - **Example**:
       ```tsx
       // ❌ BAD: Hardcoded string
       <Button>Load Mod</Button>

       // ✅ GOOD: i18n translation
       const { t } = useTranslation();
       <Button>{t('mods.actions.load')}</Button>
       ```
   - **⭐⭐⭐ CRITICAL: Avoid React Closure Issues with Callbacks**
     - See [ai-assistant/REACT_CLOSURE_PATTERNS.md](ai-assistant/REACT_CLOSURE_PATTERNS.md) ⭐⭐⭐
     - **Problem**: `useCallback` captures stale values from when callback is created
     - **Solution**: Use `useStableRef` to access current values in callbacks
     - **Example (Single value)**:
       ```tsx
       // BAD: tree is captured from when callback was created
       const handleClick = useCallback(() => {
         console.log(tree.length); // May be stale!
       }, [tree]);

       // GOOD: Always accesses current tree data
       const treeRef = useStableRef(tree);
       const handleClick = useCallback(() => {
         console.log(treeRef.current.length); // Always current!
       }, []); // Empty deps - no recreation needed
       ```
     - **Example (Multiple values)**:
       ```tsx
       // Access multiple values without callback recreation
       const [itemsRef, filtersRef] = useStableRef(items, filters);
       const handleSearch = useCallback(() => {
         return itemsRef.current.filter(f => filtersRef.current.includes(f));
       }, []); // No dependencies needed!
       ```
     - **When to use**: Callbacks passed to refs, event handlers, or third-party libraries
     - **Utility**: `src/shared/hooks/useStableRef.ts` provides `useStableRef` (supports up to 12 values)
     - **Note**: `react-hooks/exhaustive-deps` ESLint rule is disabled globally - refs don't need to be in dependency arrays
   - Separate business logic from UI logic
   - Use Ant Design components consistently
   - **ALWAYS use Compact Components for buttons and UI elements** (CompactButton, CompactSpace, etc.)
     - Located in `D3dxSkinManager.Client/src/shared/components/compact/`
     - Import from compact folder: `import { CompactButton, CompactCard } from 'shared/components/compact'`
     - Provides consistent sizing and styling across the app
     - Dark theme uses flat design (no shadows) to avoid style mismatch
     - Example: `<CompactButton type="primary">Save</CompactButton>`
     - Available components: CompactButton, CompactCard, CompactSpace, CompactDivider, CompactText, CompactAlert, CompactSection
     - All components exported through `compact/index.ts` for clean imports
   - **⭐⭐ IMPORTANT: Prefer CSS Modules/Classes over Inline Styles**
     - **Use CSS modules** (`.module.css`) or CSS classes for reusable styling
     - **Inline styles are acceptable for**:
       - Layout properties (flex, display, padding when dynamic)
       - Dynamic values that change at runtime (width based on state)
       - One-off positioning that won't be reused
     - **CSS classes are preferred for**:
       - Reusable component styles
       - Hover/focus states (can't be done inline)
       - Media queries and responsive design
       - Theme-aware colors and spacing
     - **Example**:
       ```tsx
       // ❌ BAD: Reusable styles inline
       <div style={{ padding: "16px", background: "#fff", borderRadius: "4px" }}>

       // ✅ GOOD: Reusable styles in CSS class
       <div className="card-container">
       // card-container defined in .css or .module.css file

       // ✅ ACCEPTABLE: Dynamic inline style
       <div style={{ width: `${progress}%` }}>
       ```

### 2. **ALWAYS use TypeScript and C# strictly**
   #### TypeScript:
   - Enable `strict: true` in tsconfig
   - **NEVER use `any` type** - Use `unknown` with type guards or specific types
   - Define interfaces for all data models
   - Use type guards where necessary
   - Document complex types with comments
   - **Use generic types for IPC messages:**
     - ✅ `PhotinoMessage<TPayload = unknown>` and `PhotinoResponse<TData = unknown>`
     - ✅ `sendMessage<T, TPayload = unknown>(...)`
     - ✅ `ModuleName` union type instead of `string` for modules
   - **Standardized error handling:**
     - ✅ Always use `catch (error: unknown)`
     - ✅ Use type guards: `error instanceof Error ? error.message : 'Unknown error'`
     - ❌ Never use `catch (error: any)` or `catch (error)`

   #### C#:
   - Enable nullable reference types
   - Use `var` sparingly, prefer explicit types for clarity
   - XML documentation for public APIs
   - Follow Microsoft C# coding conventions

### 3. **NEVER commit without explicit user permission**
   - **🚨 CRITICAL: ALWAYS ask the user before creating commits**
   - **This is a personal project - work directly on master branch**
   - **User prefers to push commits manually - DO NOT push to remote**
   - No feature branches or PRs required (single developer workflow)
   - See [ai-assistant/WORKFLOWS.md](ai-assistant/WORKFLOWS.md#git-workflow)

   **Commit Workflow:**
   1. Complete and test your changes
   2. Run build: `dotnet build` (backend) and `npm run build` (frontend if needed)
   3. Stage files: `git add -A`
   4. **ASK USER**: "Ready to commit?"
   5. **WAIT for user approval**
   6. Only after approval: Create commit with descriptive message
   7. **DO NOT PUSH** - User will push manually

### 4. **ALWAYS maintain backward compatibility with original Python app**
   - Check [core/ORIGINAL_COMPARISON.md](core/ORIGINAL_COMPARISON.md) for feature parity
   - Data structures should be compatible with original JSON formats
   - File paths should follow original conventions
   - See [core/MIGRATION_GUIDE.md](core/MIGRATION_GUIDE.md) for migration patterns

### 5. **ALWAYS update documentation when making changes**
   - Update [CHANGELOG.md](CHANGELOG.md) for all significant changes
   - Update [KEYWORDS_INDEX.md](KEYWORDS_INDEX.md) if adding new files/classes
   - Create feature docs in `docs/features/` for new features
   - Update [AI_GUIDE.md](AI_GUIDE.md) if discovering new patterns

### 6. **ALWAYS test changes before committing**
   - **🚨 CRITICAL: Write unit tests for new utility classes and core logic**
   - Backend: `dotnet build` (must succeed with no errors)
   - Backend Tests: `dotnet test` (must pass all tests)
   - Frontend: `npm run build` (must succeed if frontend changes)
   - Frontend Tests: `npm test` (must pass if frontend changes)
   - Integration: Start both backend and frontend, manually verify functionality
   - Path Portability: If path-related changes, test by moving/renaming folder
   - Check for console errors and warnings
   - See [ai-assistant/TESTING_GUIDE.md](ai-assistant/TESTING_GUIDE.md) ⭐⭐⭐ for comprehensive testing guide

### 7. **ALWAYS communicate clearly with the user**
   - Explain what you're doing and why
   - Ask questions when requirements are unclear
   - Provide progress updates for long-running tasks
   - Be specific about file locations and line numbers
   - Admit when you don't know something

---

## 🚀 Quick Start for AI Assistants

### First-Time Session

1. **Read this entire AI_GUIDE.md** - Contains critical rules and patterns
2. **Read [core/PROJECT_OVERVIEW.md](core/PROJECT_OVERVIEW.md)** - Understand what the project does
3. **Read [architecture/CURRENT_ARCHITECTURE.md](architecture/CURRENT_ARCHITECTURE.md)** ⭐⭐⭐ - Current system architecture
4. **Check [CHANGELOG.md](CHANGELOG.md)** - See recent changes
5. **Review [KEYWORDS_INDEX.md](KEYWORDS_INDEX.md)** - Know where things are located

### Before Making Changes

1. Read relevant feature docs in `docs/features/`
2. Check [ai-assistant/TROUBLESHOOTING.md](ai-assistant/TROUBLESHOOTING.md) for known issues
3. Review [ai-assistant/GUIDELINES.md](ai-assistant/GUIDELINES.md) for coding patterns

### After Making Changes

1. Build and test: `dotnet build && npm run build`
2. Update [CHANGELOG.md](CHANGELOG.md)
3. Update [KEYWORDS_INDEX.md](KEYWORDS_INDEX.md) if needed
4. Create/update feature documentation
5. Ask user for commit approval

---

## 🤖 RAG Retrieval Strategy

### Query Types and Routing

| Query Pattern | Route To | Example |
|---------------|----------|---------|
| "How do I add..." | `ai-assistant/WORKFLOWS.md` | "How do I add a new service?" |
| "Where is..." | `KEYWORDS_INDEX.md` | "Where is ModService?" |
| "What is..." | `core/PROJECT_OVERVIEW.md` | "What is Photino?" |
| "Architecture?" | `architecture/CURRENT_ARCHITECTURE.md` | "How does IPC routing work?" |
| "Error: ..." | `ai-assistant/TROUBLESHOOTING.md` | "Error: namespace not found" |
| "Best practice..." | `ai-assistant/GUIDELINES.md` | "Best practice for services?" |

### Folder-Based RAG Optimization

```
Query → Folder Selection → File Selection → Section
```

**Example:**
- Query: "How do I create a new React component?"
- Folder: `docs/ai-assistant/` (How-to query)
- File: `WORKFLOWS.md` (Step-by-step procedures)
- Section: "Creating Components"

---

## Detailed AI Guides

### Core Understanding (Read First)
- **[architecture/DOMAIN_DESIGN.md](architecture/DOMAIN_DESIGN.md)** ⭐⭐⭐ - **START HERE** - Domain boundaries, service responsibilities, anti-patterns
- **[ai-assistant/GUIDELINES.md](ai-assistant/GUIDELINES.md)** ⭐⭐⭐ - Coding patterns, DO's and DON'Ts
- **[architecture/CURRENT_ARCHITECTURE.md](architecture/CURRENT_ARCHITECTURE.md)** ⭐⭐⭐ - Current system architecture
- **[core/PROJECT_OVERVIEW.md](core/PROJECT_OVERVIEW.md)** ⭐⭐ - What this project does

### Task Execution
- **[ai-assistant/WORKFLOWS.md](ai-assistant/WORKFLOWS.md)** ⭐⭐⭐ - Step-by-step procedures
- **[ai-assistant/REFERENCE.md](ai-assistant/REFERENCE.md)** ⭐ - Quick command lookup
- **[core/DEVELOPMENT.md](core/DEVELOPMENT.md)** ⭐ - Development setup

### Problem Solving
- **[ai-assistant/TROUBLESHOOTING.md](ai-assistant/TROUBLESHOOTING.md)** ⭐⭐ - Known issues and solutions
- **[CHANGELOG.md](CHANGELOG.md)** ⭐ - Recent changes and fixes
- **[KEYWORDS_INDEX.md](KEYWORDS_INDEX.md)** ⭐ - File/class location lookup

### Feature Implementation
- **[features/README.md](features/README.md)** - Feature documentation index
- **[core/MIGRATION_GUIDE.md](core/MIGRATION_GUIDE.md)** - Migrating from Python version
- **[core/ORIGINAL_COMPARISON.md](core/ORIGINAL_COMPARISON.md)** - Feature parity checklist

---

## Key Facts

### Technology Stack

**Backend:**
- .NET 10 (C#)
- WinForms (desktop framework)
- WebView2 (Microsoft.Web.WebView2.WinForms)
- SQLite (Microsoft.Data.Sqlite)
- Newtonsoft.Json / System.Text.Json

**Frontend:**
- React 18+ (19.2.4)
- TypeScript 4.9+
- Ant Design 5+ (6.3.0)
- Vite (build tool)
- react-i18next (internationalization)

**Build:**
- PowerShell scripts (`build-production.ps1`)
- npm for frontend
- dotnet CLI for backend

### Project Structure

```
D3dxSkinManager/
├── D3dxSkinManager/               # .NET Backend
│   ├── Program.cs                 # Entry point
│   ├── Composition/               # ⭐ Application bootstrapping
│   │   ├── ApplicationBootstrapper.cs  # App initialization
│   │   ├── ApplicationHost.cs          # WinForms + WebView2 host
│   │   ├── ServiceContainer.cs         # DI container
│   │   ├── WebViewInitializer.cs       # WebView2 setup
│   │   ├── IpcCommunicationHandler.cs  # IPC message handler
│   │   ├── MessageDispatcher.cs        # Middleware pipeline
│   │   └── ProfileServiceRouter.cs     # Profile-scoped routing
│   ├── Modules/                   # ⭐ MODULAR ARCHITECTURE
│   │   ├── Core/                  # Shared services
│   │   ├── Context/               # Profile-scoped context
│   │   ├── Mods/                  # Mod management
│   │   ├── Profiles/              # Profile system
│   │   ├── Settings/              # Settings & file system
│   │   ├── System/                # System utilities
│   │   ├── Tools/                 # Configuration tools
│   │   ├── Launch/                # Game launching
│   │   ├── Migration/             # Python migration
│   │   ├── Plugins/               # Plugin system
│   │   └── ...
│   └── D3dxSkinManager.csproj
│
├── D3dxSkinManager.Client/        # React Frontend
│   ├── src/
│   │   ├── App.tsx                # Main component
│   │   ├── modules/               # Feature modules
│   │   │   ├── mods/
│   │   │   ├── settings/          # ⭐ Settings UI
│   │   │   └── ...
│   │   ├── shared/                # Shared components
│   │   │   ├── context/           # React contexts (theme, etc)
│   │   │   └── services/
│   │   │       └── bridgeService.ts  # C# ↔ React bridge (WebView2)
│   │   └── ...
│   └── package.json
│
├── Plugins/                       # External plugin projects (27)
│   ├── ScreenCapture/
│   ├── BatchProcessingTools/
│   └── ...
│
├── D3dxSkinManager.Tests/         # Backend tests
│   └── Modules/                   # Tests mirror module structure
│
└── docs/                          # Documentation (this folder)
    ├── AI_GUIDE.md                # This file
    ├── CHANGELOG.md               # ⭐ START HERE for recent changes
    └── architecture/              # Architecture docs
```

### Critical Files (Memorize)

| File | Purpose | Edit Frequency |
|------|---------|----------------|
| `D3dxSkinManager/Program.cs` | Main entry point | Low |
| `D3dxSkinManager/Composition/ApplicationHost.cs` | WinForms + WebView2 host | Medium |
| `D3dxSkinManager/Composition/ProfileServiceRouter.cs` | Profile-scoped routing | Medium |
| `D3dxSkinManager/Modules/Mods/Services/ModManagementService.cs` | Core mod operations | High |
| `D3dxSkinManager.Client/src/App.tsx` | Main UI | High |
| `D3dxSkinManager.Client/src/shared/services/bridgeService.ts` | WebView2 IPC bridge | Low |
| `docs/CHANGELOG.md` | Change tracking | Every session |
| `docs/AI_GUIDE.md` | This file | When learning |

### Common Commands

```bash
# Backend
cd D3dxSkinManager
dotnet build
dotnet run
dotnet clean

# Frontend (Vite)
cd D3dxSkinManager.Client
npm install
npm start            # Development server (runs vite)
npm run build        # Production build
npm run preview      # Preview production build

# Both (Production)
powershell -ExecutionPolicy Bypass -File build-production.ps1

# Git (Personal project - direct master workflow)
git status                    # Check status
git add -A                    # Stage all changes
git commit -m "message"       # Commit (ASK USER FIRST!)
# Note: User pushes manually - DO NOT use git push
```

---

## 📂 Documentation Map (Folder-Based Index)

### `docs/ai-assistant/` - AI Workflows & Strategies

| File | Purpose | When to Read |
|------|---------|--------------|
| [GUIDELINES.md](ai-assistant/GUIDELINES.md) | Coding patterns, best practices | Before coding |
| [TESTING_GUIDE.md](ai-assistant/TESTING_GUIDE.md) | ⭐⭐⭐ Testing requirements & patterns | Before making changes |
| [WORKFLOWS.md](ai-assistant/WORKFLOWS.md) | Step-by-step procedures | During tasks |
| [REFERENCE.md](ai-assistant/REFERENCE.md) | Quick command lookup | As needed |
| [TROUBLESHOOTING.md](ai-assistant/TROUBLESHOOTING.md) | Known issues & solutions | When stuck |
| [DOCUMENTATION_MAINTENANCE.md](ai-assistant/DOCUMENTATION_MAINTENANCE.md) | How to update docs | After learning |

### `docs/architecture/` - System Architecture

| File | Purpose | When to Read |
|------|---------|--------------|
| [CURRENT_ARCHITECTURE.md](architecture/CURRENT_ARCHITECTURE.md) | ⭐⭐⭐ Complete architecture guide | First session, before changes |
| [APP_FACADE_REFACTORING.md](architecture/APP_FACADE_REFACTORING.md) | AppFacade design details | Working on IPC routing |
| [FRONTEND_SERVICE_ARCHITECTURE.md](architecture/FRONTEND_SERVICE_ARCHITECTURE.md) | Frontend service pattern | Working on frontend services |
| [MODULE_STRUCTURE.md](architecture/MODULE_STRUCTURE.md) | Module organization | Adding new modules |

### `docs/core/` - Project Fundamentals

| File | Purpose | When to Read |
|------|---------|--------------|
| [PROJECT_OVERVIEW.md](core/PROJECT_OVERVIEW.md) | What/Why of project | First session |
| [PROJECT_STRUCTURE.md](core/PROJECT_STRUCTURE.md) | File organization | Finding files |
| [DEVELOPMENT.md](core/DEVELOPMENT.md) | Setup & workflow | Environment setup |
| [ORIGINAL_COMPARISON.md](core/ORIGINAL_COMPARISON.md) | Python vs .NET comparison | Feature planning |
| [MIGRATION_GUIDE.md](core/MIGRATION_GUIDE.md) | Porting features | Implementing features |

### `docs/features/` - Feature Documentation

| File | Purpose | When to Read |
|------|---------|--------------|
| [README.md](features/README.md) | Feature index | Finding features |
| (individual feature docs) | Deep-dives | Working on feature |

### Root `docs/` Files

| File | Purpose | When to Read |
|------|---------|--------------|
| [CHANGELOG.md](CHANGELOG.md) | Change history | Every session start |
| [KEYWORDS_INDEX.md](KEYWORDS_INDEX.md) | Quick file/class lookup | Finding things |
| [AI_GUIDE.md](AI_GUIDE.md) | This file | First session |

---

## Token Optimization

### Minimize Context Loading

1. **Use KEYWORDS_INDEX.md first** - Find exact file location without loading large docs
2. **Load specific sections** - Use file anchors (#section-name)
3. **Cross-reference sparingly** - Only load related docs when necessary
4. **Update KEYWORDS_INDEX.md** - Help future sessions find things faster

### Efficient RAG Queries

**Good:**
- "Where is ModService?" → KEYWORDS_INDEX.md → Direct file path
- "How to add service?" → ai-assistant/WORKFLOWS.md → Specific section

**Bad:**
- "Tell me everything about services" → Loads too much context
- No query → Randomly browsing docs

### Documentation Updates

**When to update:**
- ✅ Found info after >5 min search → Update KEYWORDS_INDEX.md (one-line entry)
- ✅ Solved a bug → Update CHANGELOG.md + TROUBLESHOOTING.md
- ✅ Created new class/service → Update KEYWORDS_INDEX.md (one-line entry)
- ✅ Discovered pattern → Update GUIDELINES.md

**⚠️ KEYWORDS_INDEX CRITICAL RULES:**
- **Main KEYWORDS_INDEX.md MUST be < 200 lines** (routing hub only)
- **Each domain file MUST be < 500 lines** (check: `wc -l docs/keywords/*.md`)
- Currently (2026-02-20):
  - ✅ KEYWORDS_INDEX.md: 150 lines (routing hub)
  - ✅ BACKEND.md: 350 lines
  - ⚠️ FRONTEND.md: 550 lines (consider splitting if grows to 600+)
  - ✅ DOCUMENTATION.md: 220 lines
  - ✅ HOW_TO.md: 370 lines
- **If domain file > 500 lines:** Create sub-folder with sub-domain files
- See [maintenance/KEYWORDS_INDEX_MANAGEMENT.md](maintenance/KEYWORDS_INDEX_MANAGEMENT.md)

**How to update:**
- Keep updates concise (one line per entry)
- Link to source files
- NO method listings - only file paths
- Add timestamps

**CHANGELOG Management Strategy** ⭐ CRITICAL:

**RULES (NON-NEGOTIABLE):**
1. **Main CHANGELOG.md MUST be < 200 lines** (currently ~100 lines)
2. **Before adding**: Check line count with `wc -l docs/CHANGELOG.md`
3. **If > 150 lines**: Archive old entries FIRST before adding new
4. **Summary only**: Maximum 5 lines per entry in main CHANGELOG
5. **Detailed changes**: ALWAYS create separate file in `changelogs/YYYY-MM/`

**See**: [maintenance/CHANGELOG_MANAGEMENT.md](maintenance/CHANGELOG_MANAGEMENT.md) ⭐⭐⭐ for complete guide

**Example Summary Entry** (5 lines max):
```markdown
### Fixed - 2026-02-20 - Migration Archive Storage ⭐⭐
Fixed migration to store archives WITHOUT extensions.
**Impact**: ✅ 173 tests pass
**Details**: [changelogs/2026-02/2026-02-20-migration-archive-storage-fix.md](...)
```

**Decision Tree:**
- Entry < 5 lines → Add to main CHANGELOG
- Entry > 5 lines → Create detailed file + add summary link
- Main > 150 lines → Archive old entries before adding new
- Main > 200 lines → **CRITICAL**: Immediate cleanup required

---

## How to Update This Guide

### When This Guide Needs Updates

1. **New Technology/Library Added** - Update Key Facts section
2. **Project Structure Changes** - Update Documentation Map
3. **New Critical Rule Discovered** - Add to 7 Critical Rules
4. **Common Task Identified** - Add to WORKFLOWS.md or REFERENCE.md
5. **Frequent Issue Found** - Add to TROUBLESHOOTING.md

### Update Process

1. Identify what changed
2. Update relevant section
3. Update "Last Updated" date at top
4. Add note in [CHANGELOG.md](CHANGELOG.md)
5. Consider updating [KEYWORDS_INDEX.md](KEYWORDS_INDEX.md) if new files added

### Maintaining Quality

- **Be concise** - Every word counts (token optimization)
- **Be specific** - Include file paths, line numbers
- **Be current** - Remove outdated information
- **Be helpful** - Think of future AI sessions reading this

---

## 🎯 Success Criteria for AI Assistants

You're doing well if:
- ✅ You ask for commit approval every time
- ✅ You update CHANGELOG.md for changes
- ✅ You reference specific files and line numbers
- ✅ You create feature documentation
- ✅ You build and test before committing
- ✅ You explain your reasoning clearly

You need improvement if:
- ❌ You commit without asking
- ❌ You use `any` type in TypeScript or use non-strict C#
- ❌ You don't update documentation
- ❌ You make changes without testing
- ❌ You can't find files (KEYWORDS_INDEX.md exists for this!)
- ❌ You duplicate code that already exists

---

## 📝 Session Template

Use this template at the start of each session:

```markdown
## Session Start Checklist

1. [ ] Read AI_GUIDE.md (this file)
2. [ ] Check CHANGELOG.md for recent changes
3. [ ] Understand user's request
4. [ ] Identify relevant documentation files
5. [ ] Ask clarifying questions if needed

## Before Committing

1. [ ] Built successfully: `dotnet build && npm run build`
2. [ ] Tested changes
3. [ ] Updated CHANGELOG.md
4. [ ] Updated KEYWORDS_INDEX.md (if new files)
5. [ ] Created/updated feature docs
6. [ ] Asked user for commit approval

## After Session

1. [ ] Updated AI_GUIDE.md if learned something
2. [ ] Added to TROUBLESHOOTING.md if solved issue
3. [ ] Updated GUIDELINES.md if found pattern
```

---

**Remember: This guide exists to help you. Use it, update it, improve it!**

*Last updated: 2026-02-22*
*Version: 1.4*

---

## Recent Major Updates (2026-02-22)

### ⭐⭐⭐ CRITICAL: Photino → WinForms + WebView2 Migration

**Major architectural change!** The application has been migrated from Photino.NET to WinForms + WebView2.

**What Changed:**
- **Desktop Framework**: Photino.NET → WinForms + WebView2
- **IPC Bridge**: `photinoService.ts` → `bridgeService.ts`
- **Window Management**: Photino window → `ApplicationHost.cs` (WinForms Form)
- **WebView Integration**: Photino's web view → Microsoft.Web.WebView2.WinForms

**New Architecture:**
- `Composition/ApplicationBootstrapper.cs` - Application initialization
- `Composition/ApplicationHost.cs` - Main form with WebView2 control
- `Composition/WebViewInitializer.cs` - WebView2 setup and custom scheme handler
- `Composition/IpcCommunicationHandler.cs` - WebView2 IPC messages
- `Composition/MessageDispatcher.cs` - Middleware pipeline with Lazy<T> caching
- `Composition/ProfileServiceRouter.cs` - Profile-scoped service providers

**Frontend Changes:**
- `photinoService.ts` renamed to `bridgeService.ts`
- IPC uses `chrome.webview.postMessage()` instead of Photino's bridge
- Custom scheme handler: `app://` URLs for local file serving

**Performance Optimizations:**
- GPU acceleration enabled for WebView2
- Pipeline caching with `Lazy<T>` in MessageDispatcher
- Custom scheme handler for efficient local file serving
- Background color set to prevent white flash

**Why the migration?**
- Better Windows integration
- More control over window behavior
- Standard WebView2 API
- Easier debugging and development

**Performance Optimizations Added (2026-02-22):**
- **CustomSchemeHandler**: LRU cache (500 items), content type caching, 4KB buffer streaming
- **SystemFileDialogService**: Fixed 2-5 second delay by reusing main UI thread (Control.Invoke)
- **LruCache Utility**: Thread-safe generic cache with automatic eviction (`Modules/Core/Utilities/LruCache.cs`)
- **Key Learning**: WebView2 streams must be writable - don't use `new MemoryStream(bytes, false)`

**Code Quality Improvements (2026-02-22 Session 2):**
- **Logging**: Replaced Console.WriteLine with ILogger in all Composition files (46 replacements)
- **Error Handling**: Replaced NotImplementedException with graceful returns + logging
- **Frontend Services**: All services now properly extend BaseModuleService (classificationService, languageService)
- **Constructor DI Pattern**: IpcCommunicationHandler, MessageDispatcher, ProfileServiceRouter now accept ILogHelper in constructor

See [technical/winforms-webview2-migration.md](../technical/winforms-webview2-migration.md) for complete migration details.

---

## Previous Updates (2026-02-21)

### New Critical Requirements:
1. **Internationalization (i18n)** - ALL user-facing text must use `t('key')` translations
2. **Operation Notifications** - ALL long-running operations must use `IProgressReporter`
3. **Vite Build System** - Frontend now uses Vite instead of Create React App

### Documentation Added:
- [features/INTERNATIONALIZATION.md](features/INTERNATIONALIZATION.md) - Complete i18n system guide
- [how-to/ADD_I18N_TO_COMPONENT.md](how-to/ADD_I18N_TO_COMPONENT.md) - Step-by-step i18n implementation
- [features/OPERATION_NOTIFICATION_SYSTEM.md](features/OPERATION_NOTIFICATION_SYSTEM.md) - Progress reporting system
- [features/DELAYED_LOADING_UX_PATTERN.md](features/DELAYED_LOADING_UX_PATTERN.md) - Delayed loading pattern

### New React Patterns:
- **OperationContext** - Global operation state and notifications
- **useDelayedLoading** - Show loading only if operation takes >100ms
- **useDragDrop** - Declarative drag & drop API (refactored)
- **Flat JSON i18n** - Easy-to-search translation keys

### Architecture Changes:
- ModsProvider now at app-level (not view-level) for global state access
- Category-based mod loading with auto-unload of conflicts
- Comprehensive error code system for user-friendly messages

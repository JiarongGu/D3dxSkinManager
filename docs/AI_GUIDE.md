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
> **If you learned it, document it. Future AI sessions depend on you!**

**Version:** 1.1
**Last Updated:** 2026-02-18
**Project Type:** .NET 8 + Photino.NET + React 18 + TypeScript (Desktop Application)
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
- **🔥 What changed last session?** → [RECENT_CHANGES.md](RECENT_CHANGES.md) ⭐⭐⭐ **START HERE**
- **Component/Service location?** → [KEYWORDS_INDEX.md](KEYWORDS_INDEX.md) ⭐
- **System architecture?** → [architecture/CURRENT_ARCHITECTURE.md](architecture/CURRENT_ARCHITECTURE.md) ⭐⭐⭐
- **Historical changes?** → [CHANGELOG.md](CHANGELOG.md)
- **Project setup?** → [core/DEVELOPMENT.md](core/DEVELOPMENT.md)

---

## 7 Critical Rules (Non-Negotiable)

### 1. **ALWAYS follow .NET + React Best Practices**
   #### Backend (.NET/C#):
   - Services handle business logic (separation of concerns)
   - Use async/await for all I/O operations
   - Use interfaces for dependency injection
   - Proper exception handling with try-catch
   - Use `using` statements for IDisposable resources

   #### Frontend (React/TypeScript):
   - Functional components with hooks (no class components)
   - TypeScript strict mode - avoid `any` type
   - Use React Query or similar for async state
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

### 2. **ALWAYS use TypeScript and C# strictly**
   #### TypeScript:
   - Enable `strict: true` in tsconfig
   - Define interfaces for all data models
   - Use type guards where necessary
   - Document complex types with comments

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
   - Backend: `dotnet build` AND `dotnet test` (must pass)
   - Frontend: `npm run build` AND `npm test` (must pass)
   - Integration: Start both backend and frontend, verify it works
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
- .NET 8 (C#)
- Photino.NET 4.0+ (desktop framework)
- SQLite (Microsoft.Data.Sqlite)
- Newtonsoft.Json

**Frontend:**
- React 18+
- TypeScript 4.9+
- Ant Design 5+
- Axios (HTTP client)

**Build:**
- PowerShell scripts (`build-production.ps1`)
- npm for frontend
- dotnet CLI for backend

### Project Structure

```
D3dxSkinManager/
├── D3dxSkinManager/               # .NET Backend
│   ├── Program.cs                 # Entry point, Photino window
│   ├── Configuration/             # DI setup
│   │   └── ServiceCollectionExtensions.cs
│   ├── Facades/                   # Top-level routing
│   │   └── AppFacade.cs           # IPC message router
│   ├── Modules/                   # ⭐ MODULAR ARCHITECTURE
│   │   ├── Core/                  # Shared services
│   │   ├── Mods/                  # Mod management
│   │   ├── Profiles/              # Profile system
│   │   ├── Settings/              # Settings & file system
│   │   ├── Plugins/               # ⭐ Plugin system (NEW LOCATION)
│   │   │   ├── Models/
│   │   │   ├── Services/          # Plugin infrastructure
│   │   │   ├── PluginsFacade.cs
│   │   │   └── PluginsServiceExtensions.cs
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
│   │   │       └── photinoService.ts  # C# ↔ React bridge
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
    ├── RECENT_CHANGES.md          # ⭐ START HERE for new sessions
    ├── AI_GUIDE.md                # This file
    └── architecture/              # Architecture docs
```

### Critical Files (Memorize)

| File | Purpose | Edit Frequency |
|------|---------|----------------|
| `D3dxSkinManager/Program.cs` | Main entry, Photino setup, IPC handler | Medium |
| `D3dxSkinManager/Services/ModService.cs` | Core mod operations | High |
| `D3dxSkinManager.Client/src/App.tsx` | Main UI | High |
| `D3dxSkinManager.Client/src/services/photino.ts` | Frontend-backend bridge | Low |
| `docs/CHANGELOG.md` | Change tracking | Every session |
| `docs/AI_GUIDE.md` | This file | When learning |

### Common Commands

```bash
# Backend
cd D3dxSkinManager
dotnet build
dotnet run
dotnet clean

# Frontend
cd D3dxSkinManager.Client
npm install
npm start
npm run build

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
- ✅ Found info after >5 min search → Update KEYWORDS_INDEX.md
- ✅ Solved a bug → Update CHANGELOG.md + TROUBLESHOOTING.md
- ✅ Created new file → Update KEYWORDS_INDEX.md
- ✅ Discovered pattern → Update GUIDELINES.md

**How to update:**
- Keep updates concise
- Link to source files
- Include line numbers for code references
- Add timestamps

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

*Last updated: 2026-02-17*
*Version: 1.0*

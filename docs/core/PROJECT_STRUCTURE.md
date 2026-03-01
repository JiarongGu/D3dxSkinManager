# Project Structure

**Project:** D3dxSkinManager
**Version:** 2.0.0
**Last Updated:** 2026-03-02

---

## Table of Contents

1. [Overview](#overview)
2. [Directory Tree](#directory-tree)
3. [Backend Structure](#backend-structure)
4. [Frontend Structure](#frontend-structure)
5. [Documentation Structure](#documentation-structure)
6. [File Naming Conventions](#file-naming-conventions)
7. [Module Organization](#module-organization)

---

## Overview

The project follows a **monorepo structure** with clear separation between backend (.NET), frontend (React), and documentation.

**Git Repository Structure (Updated 2026-02-19):**
- Single unified git repository at root level
- Previous nested `D3dxSkinManager.Client/.git` has been consolidated into root
- All project files tracked in single repository

```
D3dxSkinManager/                 # Repository root (single .git)
├── D3dxSkinManager/             # Backend (.NET 10 WinForms + WebView2 project)
├── D3dxSkinManager.Client/      # Frontend (React + TypeScript)
├── Plugins/                     # External plugin projects (9 plugins)
├── D3dxSkinManager.Tests/       # Backend unit tests
├── D3dxSkinManager.ExamplePlugin/ # Example plugin project
├── docs/                        # Documentation system
├── D3dxSkinManager.sln          # Visual Studio solution
├── build-production.ps1         # Production build script
├── .gitignore                   # Git ignore rules
└── README.md                    # Main project README
```

---

## Directory Tree

### Complete Structure

```
d3dxSkinManage-Rewrite/
│
├── D3dxSkinManager/                    # Backend C# Project
│   ├── bin/                            # Build output (ignored by git)
│   │   └── Debug/
│   │       └── net10.0-windows/
│   │           ├── D3dxSkinManager.exe
│   │           ├── *.dll               # Dependencies (WebView2, SQLite, etc.)
│   │           └── wwwroot/            # Frontend build (production only)
│   │
│   ├── obj/                            # Build intermediates (ignored)
│   │
│   ├── Infrastructure/                    # WebView2 + WinForms architecture
│   │   ├── ApplicationBootstrapper.cs
│   │   ├── ApplicationHost.cs
│   │   ├── ProfileServiceRouter.cs
│   │   └── WebView/
│   │       ├── WebViewInitializer.cs
│   │       ├── IpcHandler.cs
│   │       ├── EventBusIpcBridge.cs
│   │       ├── OptimizedForm.cs
│   │       ├── DropZoneOverlay.cs
│   │       ├── WebViewSession.cs
│   │       └── WebViewSessionManager.cs
│   │
│   ├── Modules/                        # Business logic modules (12 total)
│   │   ├── Category/                   # Category management
│   │   ├── Context/                    # DI context and configuration
│   │   ├── Core/                       # Base classes, event bus, logging
│   │   ├── Launch/                     # Game launch
│   │   ├── Migration/                  # Python config parser (legacy import)
│   │   ├── Mod/                        # Mod management
│   │   ├── Plugin/                     # Plugin system
│   │   ├── Profile/                    # Profile management
│   │   ├── Setting/                    # Settings
│   │   ├── System/                     # System operations (file dialogs, etc.)
│   │   ├── Tool/                       # Tool utilities
│   │   └── Workflow/                   # Batch operation workflows
│   │
│   ├── Program.cs                      # Entry point
│   └── D3dxSkinManager.csproj          # Project file
│
├── D3dxSkinManager.Client/             # Frontend React Project
│   ├── node_modules/                   # npm packages (ignored)
│   │
│   ├── public/                         # Static assets
│   │   ├── index.html                  # HTML template
│   │   ├── favicon.ico                 # App icon
│   │   └── manifest.json               # PWA manifest
│   │
│   ├── src/                            # Source code
│   │   ├── modules/                    # Feature modules
│   │   ├── shared/                     # Shared utilities
│   │   │   ├── services/
│   │   │   │   ├── bridgeService.ts    # WebView2 IPC bridge
│   │   │   │   └── baseModuleService.ts # Base service class
│   │   │   └── types/                  # TypeScript types
│   │   │
│   │   ├── App.css                     # App styles
│   │   ├── App.tsx                     # Main component
│   │   ├── index.css                   # Global styles
│   │   ├── index.tsx                   # Entry point
│   │   ├── logo.svg                    # Logo image
│   │   └── react-app-env.d.ts          # TypeScript declarations
│   │
│   ├── build/                          # Production build (ignored)
│   │
│   ├── package.json                    # npm configuration
│   ├── package-lock.json               # npm lockfile
│   ├── tsconfig.json                   # TypeScript configuration
│   └── .gitignore                      # Git ignore (frontend-specific)
│
├── docs/                               # Documentation System
│   ├── ai-assistant/                   # AI-specific guides
│   │   ├── DOCUMENTATION_MAINTENANCE.md # How to maintain docs
│   │   ├── GUIDELINES.md               # Coding patterns
│   │   ├── REFERENCE.md                # Quick command reference
│   │   ├── TROUBLESHOOTING.md          # Known issues
│   │   └── WORKFLOWS.md                # Step-by-step procedures
│   │
│   ├── core/                           # Core documentation
│   │   ├── ARCHITECTURE.md             # System architecture
│   │   ├── DEVELOPMENT.md              # Development guide
│   │   ├── MIGRATION_GUIDE.md          # Python → .NET migration
│   │   ├── ORIGINAL_COMPARISON.md      # Feature parity tracking
│   │   ├── PROJECT_OVERVIEW.md         # High-level overview
│   │   └── PROJECT_STRUCTURE.md        # This file
│   │
│   ├── features/                       # Feature documentation
│   │   └── README.md                   # Feature index
│   │
│   ├── maintenance/                    # Maintenance guides
│   │   └── README.md                   # Maintenance index
│   │
│   ├── AI_GUIDE.md                     # AI assistant hub
│   ├── CHANGELOG.md                    # Change history
│   ├── KEYWORDS_INDEX.md               # Quick file lookup
│   └── README.md                       # Documentation hub
│
├── D3dxSkinManager.sln                 # Visual Studio solution file
├── build-production.ps1                # Production build script
├── .gitignore                          # Git ignore rules
├── README.md                           # Main project README
├── QUICKSTART.md                       # Quick start guide
├── ARCHITECTURE.md                     # High-level architecture
├── PROJECT_SUMMARY.md                  # Project summary
├── CHANGES.md                          # Change log (root)
└── MOVING_TO_NEW_REPO.md               # Repository migration guide
```

---

## Backend Structure

### D3dxSkinManager/ (C# .NET Project)

```
D3dxSkinManager/
├── Infrastructure/              # Application infrastructure
│   ├── ApplicationBootstrapper.cs
│   ├── ApplicationHost.cs
│   ├── ProfileServiceRouter.cs
│   └── WebView/                # WebView2 subsystem
│
├── Modules/                    # Domain modules (12 total - services per-module)
│   ├── Category/
│   │   ├── Services/ICategoryService.cs, CategoryService.cs
│   │   ├── Repositories/
│   │   ├── Models/CategoryInfo.cs
│   │   ├── CategoryFacade.cs   # IPC interface
│   │   └── CategoryServiceExtensions.cs  # DI registration
│   │
│   ├── Mod/
│   │   ├── Services/IModService.cs, ModService.cs, ModQueryService.cs
│   │   ├── Repositories/IModRepository.cs, ModRepository.cs
│   │   ├── Models/ModInfo.cs
│   │   ├── ModFacade.cs        # IPC interface
│   │   └── ModServiceExtensions.cs
│   │
│   └── ...                     # 10 other modules (Context, Core, Launch, etc.)
│
├── Program.cs                   # Application entry point
│
└── D3dxSkinManager.csproj      # Project configuration
    ├─ TargetFramework: net10.0-windows
    └─ Packages: WebView2, SQLite, System.Text.Json, 7z.Libs
```

### Namespace Organization

```csharp
namespace D3dxSkinManager              // Root namespace
{
    class Program { }                  // Entry point
}

namespace D3dxSkinManager.Modules.Mod  // Module namespaces
{
    public interface IModService { }   // Service contracts
    public class ModService { }        // Service implementations
    public class ModInfo { }           // Data models
}

namespace D3dxSkinManager.Infrastructure  // Infrastructure
{
    public class ApplicationHost { }   // Application host
}
```

### File Responsibilities

| File | Purpose | Lines | Complexity |
|------|---------|-------|------------|
| **Program.cs** | Application entry point | ~50 | Low |
| **Infrastructure/ApplicationBootstrapper.cs** | DI setup, initialization | ~150 | Medium |
| **Infrastructure/WebView/IpcHandler.cs** | WebView2 IPC communication | ~200 | High |
| **Modules/*/Services/*.cs** | Module business logic | ~200-500 | Medium-High |

---

## Frontend Structure

### D3dxSkinManager.Client/ (React + TypeScript Project)

```
D3dxSkinManager.Client/
├── public/                         # Static files (served as-is)
│   ├── index.html                  # HTML template
│   ├── favicon.ico                 # App icon
│   └── manifest.json               # PWA manifest
│
├── src/                            # Source code
│   ├── shared/                     # Shared utilities & components
│   │   ├── components/             # Reusable UI components
│   │   │   └── compact/            # ⭐ Compact component system (2026-02-19)
│   │   │       ├── index.ts        # Barrel export for all components
│   │   │       ├── CompactButton.tsx      # Consistent button sizing
│   │   │       ├── CompactCard.tsx        # Card containers
│   │   │       ├── CompactSpace.tsx       # Layout spacing
│   │   │       ├── CompactDivider.tsx     # Section dividers
│   │   │       ├── CompactText.tsx        # Typography
│   │   │       ├── CompactAlert.tsx       # Alerts
│   │   │       └── CompactSection.tsx     # Page sections
│   │   │
│   │   ├── services/               # Non-UI logic
│   │   │   ├── baseModuleService.ts    # Base class for all services
│   │   │   └── bridgeService.ts        # WebView2 IPC bridge to C# backend
│   │   │
│   │   └── types/                  # Shared TypeScript types
│   │       ├── message.types.ts    # IPC message types (generic)
│   │       └── *.types.ts          # Other shared types
│   │
│   ├── modules/                    # Feature modules
│   │   ├── mods/                   # Mod management module
│   │   │   ├── components/         # Mod UI components
│   │   │   ├── services/           # modService.ts
│   │   │   └── types/              # Mod-specific types
│   │   │
│   │   ├── profiles/               # Profile management
│   │   ├── settings/               # Settings module
│   │   └── ...                     # Other modules
│   │
│   ├── App.tsx                     # Main application component
│   │   └─ Exports:
│   │       ├─ App component (default)   → Line 17
│   │       ├─ Hooks:
│   │       │   ├─ useState(mods)          → Line 18
│   │       │   ├─ useState(loading)       → Line 19
│   │       │   └─ useEffect()             → Line 21
│   │       ├─ Event handlers:
│   │       │   ├─ loadMods()              → Line 32
│   │       │   ├─ handleLoad()            → Line 42
│   │       │   └─ handleUnload()          → Line 52
│   │       └─ Render sections:
│   │           ├─ Layout                  → Line 135
│   │           ├─ Header                  → Line 136
│   │           ├─ Sidebar                 → Line 142
│   │           └─ Content (Table)         → Line 188
│   │
│   ├── index.tsx                   # React entry point
│   │   └─ Renders App to #root
│   │
│   ├── App.css                     # App-specific styles
│   ├── index.css                   # Global styles
│   ├── logo.svg                    # Logo image
│   └── react-app-env.d.ts          # TypeScript type declarations
│
├── package.json                    # npm configuration
├── package-lock.json               # Dependency lockfile
├── tsconfig.json                   # TypeScript configuration
└── .gitignore                      # Frontend-specific ignore rules
```

### Component Hierarchy

```
App (Main Layout)
├── Layout
│   ├── Header
│   │   └── Title: "D3dxSkinManager"
│   │
│   ├── Sider (Sidebar)
│   │   └── Menu
│   │       ├── Mods (selected)
│   │       ├── Warehouse
│   │       └── Settings
│   │
│   └── Content
│       └── Table (Mod List)
│           ├── Column: Status (icon)
│           ├── Column: Object
│           ├── Column: Name
│           ├── Column: Author
│           ├── Column: Tags
│           └── Column: Actions (Load/Unload buttons)
```

### Module Organization

```typescript
// Entry point chain
index.tsx
  └─ imports App from './App'
      └─ imports module services from modules/
          └─ imports baseModuleService from shared/services/baseModuleService
              └─ imports bridgeService from shared/services/bridgeService

// Service dependencies
modService.ts (extends BaseModuleService)
  └─ depends on: bridgeService.ts

bridgeService.ts
  └─ depends on: chrome.webview API (provided by WebView2)

// Type sharing
modService.ts
  └─ exports: ModInfo interface
      └─ used by: App.tsx (for state typing)
```

### File Responsibilities

| File | Purpose | Lines | Complexity |
|------|---------|-------|------------|
| **index.tsx** | React entry point | ~20 | Low |
| **App.tsx** | Main UI component | ~220 | Medium |
| **shared/services/bridgeService.ts** | WebView2 IPC communication | ~150 | High |
| **shared/services/baseModuleService.ts** | Base service class | ~100 | Medium |
| **modules/*/services/*.ts** | Module-specific services | ~50-200 | Low-Medium |

---

## Documentation Structure

### docs/ (RAG-Optimized Documentation System)

```
docs/
├── ai-assistant/                       # AI-specific guides
│   ├── DOCUMENTATION_MAINTENANCE.md    # How to update docs (critical!)
│   ├── GUIDELINES.md                   # Coding best practices
│   ├── REFERENCE.md                    # Quick command lookup
│   ├── TROUBLESHOOTING.md              # Known issues + solutions
│   └── WORKFLOWS.md                    # Step-by-step procedures
│
├── core/                               # Fundamental documentation
│   ├── ARCHITECTURE.md                 # System design
│   ├── DEVELOPMENT.md                  # Dev environment setup
│   ├── MIGRATION_GUIDE.md              # Python → .NET guide
│   ├── ORIGINAL_COMPARISON.md          # Feature parity tracking
│   ├── PROJECT_OVERVIEW.md             # What/why of project
│   └── PROJECT_STRUCTURE.md            # This file
│
├── features/                           # Feature-specific docs
│   └── README.md                       # Feature index
│
├── maintenance/                        # Maintenance guides
│   └── README.md                       # Maintenance index
│
├── AI_GUIDE.md                         # Main AI assistant hub
├── CHANGELOG.md                        # Change history
├── KEYWORDS_INDEX.md                   # Quick file lookup (RAG critical)
└── README.md                           # Documentation hub (human)
```

### Documentation Audience

| Folder/File | Primary Audience | Purpose |
|-------------|-----------------|---------|
| **ai-assistant/** | AI assistants | Workflows, patterns, troubleshooting |
| **core/** | Human developers + AI | Project fundamentals |
| **features/** | All | Feature documentation |
| **AI_GUIDE.md** | AI assistants | Navigation hub |
| **README.md** | Human developers | Getting started |
| **KEYWORDS_INDEX.md** | AI assistants | O(1) file lookup |
| **CHANGELOG.md** | All | What changed |

### Documentation Flow

```
AI Assistant starts session
    ↓
Reads: AI_GUIDE.md
    ↓
Identifies query type
    ↓
Routes to folder:
    ├─ "How to" → ai-assistant/WORKFLOWS.md
    ├─ "Where is" → KEYWORDS_INDEX.md
    ├─ "What is" → core/PROJECT_OVERVIEW.md
    └─ "Error" → ai-assistant/TROUBLESHOOTING.md
```

---

## File Naming Conventions

### Backend (C#)

**Naming:**
- **PascalCase** for all file names
- Match class name: `ModService.cs` contains `class ModService`
- Interface prefix: `IModService.cs` contains `interface IModService`

**Examples:**
```
✅ ModService.cs          (class ModService)
✅ IModService.cs         (interface IModService)
✅ Program.cs             (class Program)
❌ modService.cs          (wrong case)
❌ mod-service.cs         (wrong separator)
```

### Frontend (React/TypeScript)

**Naming:**
- **PascalCase** for React components: `App.tsx`
- **camelCase** for services/utilities: `modService.ts`, `bridgeService.ts`
- **kebab-case** for CSS: `app.css`, `index.css`

**Examples:**
```
✅ App.tsx                (React component)
✅ modService.ts          (service)
✅ bridgeService.ts       (utility)
✅ app.css                (styles)
❌ app.tsx                (component should be PascalCase)
❌ ModService.ts          (service should be camelCase)
```

### Documentation

**Naming:**
- **SCREAMING_SNAKE_CASE** for important docs: `README.md`, `CHANGELOG.md`
- **PascalCase** for regular docs: `ProjectOverview.md`
- **UPPER_CASE** for AI-critical docs: `AI_GUIDE.md`, `KEYWORDS_INDEX.md`

**Examples:**
```
✅ README.md              (entry point)
✅ CHANGELOG.md           (important)
✅ AI_GUIDE.md            (AI-critical)
✅ KEYWORDS_INDEX.md      (RAG-critical)
✅ ProjectOverview.md     (regular doc)
```

---

## Module Organization

### Backend Module Strategy

**Current:** Single project (D3dxSkinManager)

**Future:** Multi-project structure (if grows large)

```
D3dxSkinManager.Core/          # Shared models, interfaces
D3dxSkinManager.Services/      # Business logic
D3dxSkinManager.Data/          # Data access
D3dxSkinManager.Desktop/       # WinForms + WebView2 host (references others)
```

**When to split:**
- Project exceeds 5,000 lines
- Need to share code between multiple apps
- Want to distribute NuGet packages

### Frontend Module Strategy

**Current:** Single src/ folder

**Future:** Feature-based modules (if grows large)

```
src/
├── features/
│   ├── mods/
│   │   ├── components/
│   │   ├── services/
│   │   └── types/
│   ├── warehouse/
│   └── settings/
├── shared/
│   ├── components/
│   ├── services/
│   └── types/
└── App.tsx
```

**When to split:**
- src/ exceeds 3,000 lines
- Multiple developers working simultaneously
- Clear feature boundaries emerge

---

## Build Output Structure

### Development Build (Debug)

```
D3dxSkinManager/bin/Debug/net10.0-windows/
├── D3dxSkinManager.exe         # Application executable
├── D3dxSkinManager.dll         # Application library
├── D3dxSkinManager.pdb         # Debug symbols
├── Microsoft.Web.WebView2.*.dll # WebView2 runtime
├── Microsoft.Data.Sqlite.dll
├── System.Text.Json.dll
└── (other dependencies)
```

**Frontend:** Runs separately on `http://localhost:3000` during development

### Production Build (Release)

```
D3dxSkinManager/bin/Release/net10.0-windows/win-x64/publish/
├── D3dxSkinManager.exe         # Self-contained executable
├── wwwroot/                    # Bundled frontend
│   ├── index.html
│   ├── assets/
│   │   ├── *.css
│   │   └── *.js
│   └── manifest.json
└── (all dependencies bundled - WebView2, SQLite, etc.)
```

**Size:** ~15-20 MB (self-contained .NET 10 + WebView2 + frontend bundle)

---

## Special Directories

### Ignored by Git

```
# Backend
D3dxSkinManager/bin/
D3dxSkinManager/obj/
*.user
*.suo

# Frontend
D3dxSkinManager.Client/node_modules/
D3dxSkinManager.Client/build/

# Database
*.db
*.db-journal

# IDE
.vs/
.vscode/
*.swp
```

### Created at Runtime

```
# In application directory
mods/              # Mod archives
extracted/         # Extracted mod files
thumbnails/        # Thumbnail images
previews/          # Preview images
logs/              # Application logs (future)
backups/           # Database backups (future)
```

---

## Finding Files Quickly

### For AI Assistants

**Use KEYWORDS_INDEX.md first!**

Query: "Where is ModService?"
→ Search KEYWORDS_INDEX.md
→ Find: `ModService → D3dxSkinManager/Services/ModService.cs:14`
→ Load only that file

### For Human Developers

**Use IDE search:**
- Visual Studio: Ctrl+T (Go to All)
- VS Code: Ctrl+P (Quick Open)
- Grep: `grep -r "class ModService" .`

**Or follow this guide:**
- Backend code → `D3dxSkinManager/`
- Frontend code → `D3dxSkinManager.Client/src/`
- Documentation → `docs/`

---

## Related Documentation

- [ARCHITECTURE.md](ARCHITECTURE.md) - How components interact
- [PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md) - What the project does
- [KEYWORDS_INDEX.md](../KEYWORDS_INDEX.md) - Quick file lookup
- [DEVELOPMENT.md](DEVELOPMENT.md) - Setting up dev environment

---

*This structure document is maintained as the project evolves.*

*Last updated: 2026-03-02*

# Project Structure

**Project:** D3dxSkinManager
**Version:** 1.0.0
**Last Updated:** 2026-02-17

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

```
d3dxSkinManage-Rewrite/          # Repository root
├── D3dxSkinManager/             # Backend (.NET 8 project)
├── D3dxSkinManager.Client/      # Frontend (React + TypeScript)
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
│   │       └── net8.0/
│   │           ├── D3dxSkinManager.exe
│   │           ├── mods.db             # SQLite database
│   │           ├── *.dll               # Dependencies
│   │           └── wwwroot/            # Frontend (production only)
│   │
│   ├── obj/                            # Build intermediates (ignored)
│   │
│   ├── Services/                       # Business logic layer
│   │   ├── IModService.cs              # Mod service interface
│   │   └── ModService.cs               # Mod service implementation
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
│   │   ├── services/                   # API and utilities
│   │   │   ├── photino.ts              # Photino IPC bridge
│   │   │   └── modService.ts           # Mod API wrapper
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
├── Services/                    # Service layer (business logic)
│   ├── IModService.cs           # Interface: Mod operations contract
│   │   └─ Methods:
│   │       ├─ GetAllModsAsync()      → Line 15
│   │       ├─ LoadModAsync()         → Line 20
│   │       ├─ UnloadModAsync()       → Line 25
│   │       ├─ GetLoadedModsAsync()   → Line 30
│   │       └─ ImportModAsync()       → Line 35
│   │
│   └── ModService.cs            # Implementation: Mod operations
│       └─ Methods:
│           ├─ Constructor                → Line 18
│           ├─ InitializeDatabaseAsync()  → Line 24
│           ├─ GetAllModsAsync()          → Line 47
│           ├─ LoadModAsync()             → Line 66
│           ├─ UnloadModAsync()           → Line 111
│           ├─ GetLoadedModsAsync()       → Line 130
│           ├─ ImportModAsync()           → Line 148
│           └─ MapToModInfo()             → Line 158
│
├── Program.cs                   # Application entry point
│   └─ Sections:
│       ├─ Main()                     → Line 11
│       ├─ Photino window setup       → Line 24-31
│       └─ OnWebMessageReceived()     → Line 41-68
│
└── D3dxSkinManager.csproj      # Project configuration
    └─ Contains:
        ├─ TargetFramework: net8.0
        ├─ PackageReferences:
        │   ├─ Photino.NET 4.0.16
        │   ├─ Microsoft.Data.Sqlite 10.0.3
        │   └─ Newtonsoft.Json 13.0.4
        └─ Build settings
```

### Namespace Organization

```csharp
namespace D3dxSkinManager              // Root namespace
{
    class Program { }                  // Entry point
}

namespace D3dxSkinManager.Services     // Service layer
{
    public interface IModService { }   // Service contracts
    public class ModService { }        // Service implementations
    public class ModInfo { }           // Data models
}
```

### File Responsibilities

| File | Purpose | Lines | Complexity |
|------|---------|-------|------------|
| **Program.cs** | Photino host, IPC handler | ~100 | Medium |
| **Services/IModService.cs** | Service interface, ModInfo | ~60 | Low |
| **Services/ModService.cs** | Business logic, DB access | ~200 | High |

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
│   ├── services/                   # Non-UI logic
│   │   ├── photino.ts              # IPC bridge to C# backend
│   │   │   └─ Exports:
│   │   │       ├─ PhotinoService class      → Line 24
│   │   │       ├─ sendMessage()             → Line 55
│   │   │       ├─ initializeMessageReceiver() → Line 22
│   │   │       ├─ simulateBackendResponse() → Line 87
│   │   │       └─ photinoService (singleton)
│   │   │
│   │   └── modService.ts           # API wrapper for mods
│   │       └─ Exports:
│   │           ├─ ModInfo interface         → Line 3
│   │           ├─ ModService class          → Line 18
│   │           ├─ getAllMods()              → Line 23
│   │           ├─ loadMod()                 → Line 28
│   │           ├─ unloadMod()               → Line 33
│   │           ├─ getLoadedMods()           → Line 38
│   │           ├─ importMod()               → Line 43
│   │           └─ modService (singleton)
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
      └─ imports modService from './services/modService'
          └─ imports photinoService from './services/photino'

// Service dependencies
modService.ts
  └─ depends on: photino.ts

photino.ts
  └─ depends on: window.external (provided by Photino)

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
| **services/photino.ts** | IPC communication | ~150 | High |
| **services/modService.ts** | Mod API wrapper | ~50 | Low |

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
- **camelCase** for services/utilities: `modService.ts`, `photino.ts`
- **kebab-case** for CSS: `app.css`, `index.css`

**Examples:**
```
✅ App.tsx                (React component)
✅ modService.ts          (service)
✅ photino.ts             (utility)
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
D3dxSkinManager.Desktop/       # Photino host (references others)
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
D3dxSkinManager/bin/Debug/net8.0/
├── D3dxSkinManager.exe         # Application executable
├── D3dxSkinManager.dll         # Application library
├── D3dxSkinManager.pdb         # Debug symbols
├── mods.db                     # SQLite database (created at runtime)
├── Photino.NET.dll             # Dependencies
├── Microsoft.Data.Sqlite.dll
├── Newtonsoft.Json.dll
└── (other dependencies)
```

**Frontend:** Runs separately on `http://localhost:3000` during development

### Production Build (Release)

```
D3dxSkinManager/bin/Release/net8.0/win-x64/publish/
├── D3dxSkinManager.exe         # Self-contained executable
├── wwwroot/                    # Bundled frontend
│   ├── index.html
│   ├── static/
│   │   ├── css/
│   │   └── js/
│   └── asset-manifest.json
├── mods.db                     # Database (if included)
└── (all dependencies bundled)
```

**Size:** ~10-15 MB (self-contained .NET + frontend bundle)

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

*Last updated: 2026-02-17*

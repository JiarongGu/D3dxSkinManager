# D3dxSkinManager

A modern desktop application for managing 3DMigoto game mods. Built with **.NET 10 + WebView2** backend and **React 19 + TypeScript** frontend.

![Version](https://img.shields.io/badge/version-2.0.0-blue) ![.NET](https://img.shields.io/badge/.NET-10.0-purple) ![React](https://img.shields.io/badge/React-19-61DAFB)

---

## What is D3dxSkinManager?

D3dxSkinManager is a desktop application for organizing and managing game mods that use the **3DMigoto** framework. It provides a modern interface for:

- 📦 **Import & Extract** - Automatic mod archive extraction (7z, zip, rar)
- 🗂️ **Organize** - Hierarchical category system with drag & drop
- ⚡ **Load/Unload** - Quick mod activation/deactivation
- 🔍 **Search & Filter** - Find mods by name, tags, category
- 🖼️ **Preview** - Mod screenshots and thumbnails
- 📋 **Batch Operations** - Multi-select for bulk actions
- 🔄 **Migration** - Import from legacy Python d3dxSkinManage

---

## Quick Links

- **🤖 AI Guide**: [docs/AI_GUIDE.md](docs/AI_GUIDE.md) - Primary reference for AI assistants ⭐
- **🔍 Keywords Index**: [docs/KEYWORDS_INDEX.md](docs/KEYWORDS_INDEX.md) - Fast file lookup
- **📚 Documentation**: [docs/](docs/) - Complete documentation
- **📝 Changelog**: [docs/CHANGELOG.md](docs/CHANGELOG.md) - What's new

---

## Why This Project?

This is a complete rewrite of [d3dxSkinManage (Python)](https://github.com/numlinka/d3dxSkinManage) with modern technology:

| Python Version | .NET Version |
|----------------|--------------|
| Tkinter UI (outdated) | React 19 (modern) |
| ~150MB bundle | ~15MB bundle |
| Slow with 1000+ mods | Fast with any number |
| Hard to maintain | Clean architecture |
| No type safety | Full type safety |

---

## Architecture Overview

```
┌─────────────────────────────────────────┐
│         Desktop Application             │
│  (Windows 10+ with WebView2)            │
└─────────────────────────────────────────┘
              │
    ┌─────────┴──────────┐
    │                    │
┌───▼────────┐    ┌──────▼──────┐
│  Frontend  │    │   Backend   │
│            │    │             │
│ React 19   │◄──►│  .NET 10    │
│ TypeScript │    │  C#         │
│ Ant Design │    │  SQLite     │
└────────────┘    └─────────────┘
                       │
              ┌────────┴────────┐
              │                 │
         ┌────▼─────┐    ┌─────▼────┐
         │ Database │    │ File I/O │
         │ (SQLite) │    │ (Mods)   │
         └──────────┘    └──────────┘
```

### Design Principles

- **Module-Based Architecture** - Each feature is a self-contained module
- **Repository Pattern** - Clean separation of data access
- **Dependency Injection** - Testable, maintainable code
- **Type Safety** - C# backend + TypeScript frontend
- **Profile System** - Multi-profile support with isolated data
- **Event-Driven** - Reactive UI updates via event bus
- **Performance** - Memory caching, optimized queries

---

## Technology Stack

### Backend (.NET 10)
- **WebView2** - Chromium-based UI host
- **SQLite** - Embedded database for metadata
- **Dependency Injection** - Microsoft.Extensions.DependencyInjection
- **Memory Caching** - IMemoryCache for performance

### Frontend (React 19)
- **Zustand** - Global state management
- **Ant Design 6** - Professional UI components
- **TypeScript 5.9** - Type safety
- **Vite 7** - Fast development & build

---

## Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/) with npm
- Windows 10 or later (WebView2 required)

### Quick Start

```bash
# 1. Clone the repository
git clone <your-repo-url>
cd D3dxSkinManager

# 2. Install frontend dependencies
cd D3dxSkinManager.Client
npm install

# 3. Start frontend dev server
npm start

# 4. Run backend (new terminal)
cd ../D3dxSkinManager
dotnet run
```

The application will open automatically. See [docs/QUICKSTART.md](docs/QUICKSTART.md) for detailed setup instructions.

### Building for Production

```bash
# Run the production build script
powershell .\build-production.ps1

# Output: D3dxSkinManager/bin/Release/net10.0-windows/publish/
```

---

## Key Features

### 🗂️ Category System
Organize mods in a hierarchical tree structure with drag & drop support.

- Tree-based organization (characters, outfits, etc.)
- Drag & drop mods to categories
- Automatic cache invalidation for performance
- Global name uniqueness enforcement

### 👤 Profile System
Manage multiple game configurations independently.

- Isolated mod collections per profile
- Switch profiles instantly
- Profile-specific settings and paths
- Export/import profiles

### ⚙️ Workflow System
Background operations with progress tracking and batch processing.

- Download manager-style UI for mod imports
- Visual progress bars for long operations
- Batch operations (delete, resume multiple workflows)
- SQLite persistence across app restarts
- Metadata pre-filling from folder/archive detection

### 🔄 Migration
Import your existing Python d3dxSkinManage data.

- Auto-detect Python installation
- Import configuration, categories, and mods
- Preserve mod metadata and previews
- Copy or move archives (your choice)

### 🔌 Plugin System
Extend functionality with custom plugins.

- C# plugin interface
- Hot-reload support
- Access to core services
- Event subscription

---

## Project Structure

```
D3dxSkinManager/
├── D3dxSkinManager/              # Backend (.NET 10)
│   ├── Infrastructure/           # WebView2 + WinForms host
│   │   ├── ApplicationBootstrapper.cs
│   │   ├── ApplicationHost.cs
│   │   ├── ProfileServiceRouter.cs
│   │   └── WebView/              # WebView2 subsystem
│   │
│   ├── Modules/                  # Business logic modules (12 total)
│   │   ├── Category/             # Category management
│   │   ├── Context/              # DI context
│   │   ├── Core/                 # Base classes, event bus, logging
│   │   ├── Launch/               # Game launch
│   │   ├── Migration/            # Python import (legacy)
│   │   ├── Mod/                  # Mod management
│   │   ├── Plugin/               # Plugin system
│   │   ├── Profile/              # Profile management
│   │   ├── Setting/              # Settings
│   │   ├── System/               # System operations
│   │   ├── Tool/                 # Tool utilities
│   │   └── Workflow/             # Batch workflows
│   │
│   └── Program.cs                # Entry point
│
├── D3dxSkinManager.Client/       # Frontend (React 19)
│   └── src/
│       ├── modules/              # Feature modules (9 total)
│       │   ├── core/             # Core UI components
│       │   ├── launch/           # Game launch UI
│       │   ├── migration/        # Migration wizard
│       │   ├── mod/              # Mod management
│       │   ├── plugin/           # Plugin UI
│       │   ├── profile/          # Profile management
│       │   ├── setting/          # Settings UI
│       │   ├── tool/             # Tools UI
│       │   └── workflow/         # Workflow UI
│       │
│       ├── shared/               # Shared utilities
│       │   ├── components/       # Reusable UI components
│       │   ├── hooks/            # Custom React hooks
│       │   ├── services/         # IPC services (bridgeService, etc.)
│       │   └── store/            # Zustand stores
│       └── App.tsx               # Main application
│
├── D3dxSkinManager.Tests/        # Backend unit tests
├── D3dxSkinManager.ExamplePlugin/# Example plugin project
├── Plugins/                      # External plugin projects
│
└── docs/                         # Documentation (optimized for AI)
    ├── AI_GUIDE.md               # ⭐ Primary AI reference (1470 lines)
    ├── KEYWORDS_INDEX.md         # Fast file routing
    ├── core/                     # Core project docs
    ├── ai-assistant/             # AI code generation guides
    ├── architecture/             # Architecture details
    ├── features/                 # Feature-specific docs
    ├── keywords/                 # Component indexes
    └── archive/                  # Archived historical docs
```

---

## Documentation

### For Users
- **[Quick Start Guide](docs/QUICKSTART.md)** - Get started in 5 minutes
- **[Feature Guides](docs/features/)** - How to use each feature

### For Developers
- **[AI Guide](docs/AI_GUIDE.md)** - ⭐ Primary reference for AI assistants (read this first!)
- **[Keywords Index](docs/KEYWORDS_INDEX.md)** - Fast file lookup and routing
- **[Design Decisions](docs/core/DESIGN_DECISIONS.md)** - Architectural constraints
- **[Current Architecture](docs/architecture/CURRENT_ARCHITECTURE.md)** - System overview
- **[Development Guide](docs/core/DEVELOPMENT.md)** - Setup and contributing
- **[Workflows Guide](docs/ai-assistant/WORKFLOWS.md)** - Step-by-step code patterns

### Key Features Documentation
- **[Category System](docs/features/CATEGORY_SYSTEM.md)** - Hierarchical organization
- **[Profile System](docs/features/PROFILE_SYSTEM.md)** - Multi-profile support
- **[Workflow System](docs/architecture/WORKFLOW_ARCHITECTURE.md)** - Batch operations
- **[Internationalization](docs/features/INTERNATIONALIZATION.md)** - i18n support
- **[Plugins](docs/features/PLUGINS.md)** - Plugin architecture

---

## Contributing

Contributions are welcome! Please see [docs/core/DEVELOPMENT.md](docs/core/DEVELOPMENT.md) for:

- Development setup
- Code style guidelines
- Testing requirements
- Pull request process

---

## License

[Your License Here]

---

**📚 Full documentation available in [docs/](docs/)**

**For AI Code Generation**: Start with [AI_GUIDE.md](docs/AI_GUIDE.md) → [KEYWORDS_INDEX.md](docs/KEYWORDS_INDEX.md)

*Last updated: 2026-03-02*

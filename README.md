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

- **📚 Documentation**: [docs/](docs/) - Complete documentation
- **🚀 Quick Start**: [docs/QUICKSTART.md](docs/QUICKSTART.md) - Get started in 5 minutes
- **🤖 AI Guide**: [docs/AI_GUIDE.md](docs/AI_GUIDE.md) - For AI assistants
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
- **Ant Design 5** - Professional UI components
- **TypeScript 4.9** - Type safety
- **Vite** - Fast development & build

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

### ⚙️ Task Queue
Background operations with progress tracking.

- Visual progress bars for long operations
- Pause/resume/cancel support
- Concurrent task execution
- History and logging

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
├── D3dxSkinManager/              # Backend (.NET)
│   ├── Modules/                  # Feature modules
│   │   ├── Category/             # Category system
│   │   ├── Mod/                  # Mod management
│   │   ├── Profile/              # Profile system
│   │   ├── Migration/            # Data migration
│   │   └── ...                   # Other modules
│   ├── Configuration/            # DI & startup
│   └── Program.cs                # Entry point
│
├── D3dxSkinManager.Client/       # Frontend (React)
│   └── src/
│       ├── modules/              # Feature modules
│       ├── shared/               # Shared utilities
│       │   ├── components/       # Reusable components
│       │   ├── hooks/            # Custom hooks
│       │   ├── services/         # API services
│       │   └── store/            # Zustand stores
│       └── App.tsx               # Main application
│
└── docs/                         # Documentation
    ├── architecture/             # Architecture docs
    ├── features/                 # Feature guides
    └── AI_GUIDE.md               # AI assistant guide
```

---

## Documentation

### For Users
- **[Quick Start Guide](docs/QUICKSTART.md)** - Get started in 5 minutes
- **[Feature Guides](docs/features/)** - How to use each feature

### For Developers
- **[AI Guide](docs/AI_GUIDE.md)** - Start here for AI assistants ⭐
- **[Design Decisions](docs/core/DESIGN_DECISIONS.md)** - Architectural choices
- **[Module Architecture](docs/architecture/MODULE_ARCHITECTURE.md)** - How modules work
- **[Development Guide](docs/core/DEVELOPMENT.md)** - Contributing guidelines

### Key Features Documentation
- **[Category System](docs/features/CATEGORY_SYSTEM.md)** - Hierarchical organization
- **[Profile System](docs/features/PROFILE_SYSTEM.md)** - Multi-profile support
- **[Task Queue](docs/features/TASK_QUEUE_SYSTEM.md)** - Background operations
- **[Migration](docs/architecture/MIGRATION_ARCHITECTURE.md)** - Python import guide

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

*Last updated: 2026-02-26*

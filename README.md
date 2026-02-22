# D3dxSkinManager

Modern rewrite of d3dxSkinManage using **.NET 10 + WebView2** (C# backend) + **React + TypeScript** (frontend).

![Version](https://img.shields.io/badge/version-2.0.0-blue) ![.NET](https://img.shields.io/badge/.NET-8.0-purple) ![React](https://img.shields.io/badge/React-19.2-61DAFB)

## Quick Links

- **📚 Full Documentation**: [docs/](docs/)
- **🚀 Quick Start Guide**: [docs/QUICKSTART.md](docs/QUICKSTART.md)
- **🏛️ Current Architecture**: [docs/architecture/CURRENT_ARCHITECTURE.md](docs/architecture/CURRENT_ARCHITECTURE.md) ⭐
- **🔍 Keywords Index** (for AI): [docs/KEYWORDS_INDEX.md](docs/KEYWORDS_INDEX.md)
- **📊 Feature Gap Analysis**: [docs/features/FEATURE_GAP_ANALYSIS_V3.md](docs/features/FEATURE_GAP_ANALYSIS_V3.md)
- **📝 Changelog**: [docs/CHANGELOG.md](docs/CHANGELOG.md)

## Technology Stack

### Backend
- **.NET 10** - Modern, cross-platform framework
- **WebView2** - Chromium-based desktop UI framework
- **SQLite** - Embedded database
- **C#** with **Dependency Injection**

### Frontend
- **React 19** - Component-based UI
- **TypeScript 4.9** - Type safety
- **Ant Design 6.3** - Professional UI components
- **Custom Hooks** - Reusable logic

## Architecture (v2.0)

```
┌─────────────────────────────────────┐
│   React Frontend (Component-Based)  │
│   ├─ Custom Hooks                   │
│   ├─ Focused Components             │
│   └─ Type-Safe Services             │
└──────────────┬──────────────────────┘
               │ IPC (JSON)
┌──────────────┴──────────────────────┐
│   .NET Backend (DI Container)       │
│   ├─ Facade Layer                   │
│   ├─ Domain Services                │
│   ├─ Repository Pattern              │
│   └─ Low-Level Services             │
└─────────────────────────────────────┘
```

## Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/) with npm
- Windows 10+ (Linux/macOS experimental)

### Quick Start

```bash
# 1. Clone the repository
git clone <your-repo-url>
cd d3dxSkinManage-Rewrite

# 2. Install frontend dependencies
cd D3dxSkinManager.Client
npm install

# 3. Start development server
npm start

# 4. In a new terminal, run backend
cd ../D3dxSkinManager
dotnet run
```

**For detailed instructions**, see [docs/QUICKSTART.md](docs/QUICKSTART.md)

## Project Structure

```
d3dxSkinManage-Rewrite/
├── D3dxSkinManager/              # Backend (.NET 10)
│   ├── Configuration/            # DI setup ⭐
│   ├── Facades/                  # Service coordination ⭐
│   ├── Models/                   # Data models ⭐
│   ├── Services/                 # Business logic
│   └── Program.cs                # Entry point
│
├── D3dxSkinManager.Client/       # Frontend (React)
│   └── src/
│       ├── components/           # UI components ⭐
│       ├── hooks/                # Custom hooks ⭐
│       ├── types/                # TypeScript types ⭐
│       ├── utils/                # Utilities ⭐
│       ├── services/             # API wrappers
│       └── App.tsx               # Main app (81 lines)
│
└── docs/                         # Documentation
    ├── core/                     # Core documentation
    ├── ai-assistant/             # AI assistant guides
    └── README.md                 # Documentation hub
```

⭐ = New in v2.0 (Major Refactoring)

## Key Features

### Backend (v2.0)
- ✅ **Dependency Injection** - Microsoft.Extensions.DependencyInjection
- ✅ **Facade Pattern** - Clean service coordination
- ✅ **Repository Pattern** - Data access abstraction
- ✅ **Focused Services** - Single Responsibility Principle
- ✅ **7-Zip Integration** - Archive extraction
- ✅ **Classification System** - Pattern-based mod categorization
- ✅ **Image Processing** - Thumbnail generation
- ✅ **Advanced Search** - Negation and AND logic

### Frontend (v2.0)
- ✅ **Component Architecture** - 40+ focused components
- ✅ **Custom Hooks** - Reusable state logic
- ✅ **Type System** - Centralized TypeScript types
- ✅ **Mod Management** - Load, unload, delete, search, batch edit
- ✅ **Advanced Filtering** - Object, grading, tags, search
- ✅ **Professional UI** - Ant Design v5 components
- ✅ **Keyboard Shortcuts** - Power user features
- ✅ **Help System** - Built-in documentation
- ✅ **Mod Warehouse** - Browse and download mods

### Implementation Status
- 📊 **Feature Parity**: ~60% complete vs Python version
- ✅ **Core Features**: Fully implemented
- ⚠️ **Missing Features**: 15 identified (see [Feature Gap Analysis](docs/features/FEATURE_GAP_ANALYSIS.md))
  - 5 Mod Management features
  - 7 Context Menu actions
  - 3 Settings options
  - 5 Additional features

## Building for Production

```bash
# Run the build script
powershell .\build-production.ps1

# Output will be in D3dxSkinManager/bin/Release/net10.0/publish/
```

## Documentation

All documentation is in the [docs/](docs/) folder:

### For Developers
- **[Developer Hub](docs/README.md)** - Main documentation index
- **[Current Architecture](docs/architecture/CURRENT_ARCHITECTURE.md)** - Complete architecture guide ⭐
- **[Project Structure](docs/core/PROJECT_STRUCTURE.md)** - File organization
- **[Development Guide](docs/core/DEVELOPMENT.md)** - Development workflows
- **[Quick Start](docs/QUICKSTART.md)** - 5-minute setup guide

### For AI Assistants
- **[AI Guide](docs/AI_GUIDE.md)** - Navigation hub for AI
- **[Keywords Index](docs/KEYWORDS_INDEX.md)** - Fast file lookup
- **[Guidelines](docs/ai-assistant/GUIDELINES.md)** - Coding patterns
- **[Workflows](docs/ai-assistant/WORKFLOWS.md)** - Step-by-step procedures

## Version History

- **v2.0.0** (2026-02-17) - Major refactoring with DI, Facade pattern, component architecture
- **v1.0.0** (2026-02-17) - Initial rewrite from Python

See [docs/CHANGELOG.md](docs/CHANGELOG.md) for detailed changes.

## Original Project

This is a complete rewrite of [d3dxSkinManage (Python)](https://github.com/numlinka/d3dxSkinManage) v1.6.3.

**Why Rewrite?**
- Better performance (compiled C# vs interpreted Python)
- Modern UI (React vs tkinter)
- Better architecture (SOLID principles, DI)
- Smaller bundle (~15MB vs ~150MB with Electron)
- Easier to maintain and extend

## License

[Your License Here]

## Contributing

Contributions welcome! Please see [docs/core/DEVELOPMENT.md](docs/core/DEVELOPMENT.md) for guidelines.

---

**📚 For complete documentation, visit [docs/](docs/)**

*Last updated: 2026-02-17 (v2.0)*

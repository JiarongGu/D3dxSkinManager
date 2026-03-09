# D3dxSkinManager

**A modern mod manager for 3DMigoto-based game mods**

Easy-to-use desktop application for organizing, loading, and managing your game mods with a beautiful interface.

[![Latest Release](https://img.shields.io/github/v/release/JiarongGu/D3dxSkinManager?label=Download)](https://github.com/JiarongGu/D3dxSkinManager/releases/latest)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![React](https://img.shields.io/badge/React-19-61DAFB)](https://react.dev/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

---

## 📥 Download & Install

**[⬇️ Download Latest Version](https://github.com/JiarongGu/D3dxSkinManager/releases/latest)** (Windows x64)

**Quick Install:**
1. Download the ZIP file from [Releases](https://github.com/JiarongGu/D3dxSkinManager/releases/latest)
2. Extract to any folder
3. Run `D3dxSkinManager Launcher.exe`
4. Done! The launcher automatically installs .NET 10 if needed

**Requirements:** Windows 10/11 (64-bit) • ~14 MB disk space

---

## ✨ Features

- 📦 **Import & Extract** - Drag & drop ZIP, 7Z, RAR archives
- 🗂️ **Organize** - Hierarchical categories with drag & drop
- ⚡ **Load/Unload** - One-click mod activation
- 🔍 **Search & Filter** - Find mods quickly by name, tags, or author
- 🖼️ **Preview Images** - Browse mod screenshots in gallery mode
- 🏷️ **Tag System** - Label mods with custom colored tags
- 📋 **Batch Operations** - Update multiple mods at once
- 🎮 **Multi-Game Support** - Separate profiles for different games
- 🔄 **Migration Tool** - Import from older Python version
- 🌐 **Multi-Language** - English and Chinese support

---

## 🎮 Supported Games

Works with any game that uses **3DMigoto** for modding:
- Genshin Impact
- Honkai: Star Rail
- Zenless Zone Zero
- And many more!

> **Note**: You'll need to set up 3DMigoto separately for your game. This tool manages the mods.

---

## 🆘 Need Help?

- **📝 [Changelog](CHANGELOG.md)** - See what's new
- **🐛 [Report a Bug](https://github.com/JiarongGu/D3dxSkinManager/issues)** - Found an issue?
- **💡 [Request a Feature](https://github.com/JiarongGu/D3dxSkinManager/issues)** - Have an idea?

---

## 🏗️ Architecture & Technology

### High-Level Architecture

```
┌─────────────────────────────────┐
│   D3dxSkinManager.exe           │
│   (Desktop Application)         │
└─────────────────────────────────┘
          ┌─────┴─────┐
    ┌─────▼─────┐ ┌───▼───────┐
    │  Frontend │ │  Backend  │
    │  (React)  │ │  (.NET)   │
    │  WebView2 │ │  WinForms │
    └───────────┘ └───────────┘
                      │
                ┌─────┴──────┐
            ┌───▼────┐  ┌────▼────┐
            │ SQLite │  │  Files  │
            │   DB   │  │  (Mods) │
            └────────┘  └─────────┘
```

### Technology Stack

**Backend (.NET 10)**
- **WebView2** - Chromium-based UI host
- **SQLite** - Embedded database for metadata
- **Dependency Injection** - Microsoft.Extensions.DependencyInjection
- **Memory Caching** - IMemoryCache for performance

**Frontend (React 19)**
- **Zustand** - Global state management
- **Ant Design 6** - Professional UI components
- **TypeScript 5.9** - Type safety
- **Vite 7** - Fast development & build

### Design Principles

- **Module-Based** - Each feature is self-contained
- **Type-Safe** - C# + TypeScript for reliability
- **Event-Driven** - Reactive UI updates
- **Profile-Scoped** - Isolated data per game
- **Performance-First** - Memory caching, optimized queries

---

## 🔧 For Developers

### Quick Start

```bash
# Clone the repository
git clone https://github.com/JiarongGu/D3dxSkinManager.git
cd D3dxSkinManager

# Install frontend dependencies
cd D3dxSkinManager.Client
npm install

# Start dev server (terminal 1)
npm start

# Run backend (terminal 2)
cd ../D3dxSkinManager
dotnet run
```

**Prerequisites:**
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 18+](https://nodejs.org/) with npm
- Windows 10+ with WebView2

### Building for Production

```powershell
# Build for Windows x64
.\build-production.ps1
```

Output: `publish/win-x64/` (~14 MB, framework-dependent)

### Documentation for Developers

- **[AI Guide](docs/AI_GUIDE.md)** - Primary reference for AI-assisted development ⭐
- **[Keywords Index](docs/KEYWORDS_INDEX.md)** - Fast file lookup
- **[Development Guide](docs/core/DEVELOPMENT.md)** - Setup and contributing
- **[Architecture](docs/architecture/CURRENT_ARCHITECTURE.md)** - System design
- **[Release Guide](RELEASING.md)** - How to create releases

---

## 📖 Key Features Documentation

### For Users
- **[Changelog](CHANGELOG.md)** - What's new in each version
- **[Feature Guides](docs/features/)** - How to use each feature

### For Developers
- **[Category System](docs/features/CATEGORY_SYSTEM.md)** - Hierarchical organization
- **[Profile System](docs/features/PROFILE_SYSTEM.md)** - Multi-profile support
- **[Workflow System](docs/architecture/WORKFLOW_ARCHITECTURE.md)** - Batch operations
- **[Internationalization](docs/features/INTERNATIONALIZATION.md)** - i18n support
- **[Plugins](docs/features/PLUGINS.md)** - Plugin architecture

---

## 🙏 Credits

This is a complete rewrite of [d3dxSkinManage (Python)](https://github.com/numlinka/d3dxSkinManage) using modern .NET and React technology. The new version features a cleaner architecture, better performance, and full type safety.

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).

---

**For technical documentation and AI-assisted development**, see [`docs/AI_GUIDE.md`](docs/AI_GUIDE.md)

*Last updated: 2026-03-09*

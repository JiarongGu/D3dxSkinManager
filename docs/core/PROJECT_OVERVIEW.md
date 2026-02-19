# Project Overview

**Project Name:** D3dxSkinManager
**Version:** 1.0.0
**Type:** Desktop Application (Windows)
**Technology:** .NET 10 + Photino.NET + React + TypeScript
**Purpose:** Mod management for 3DMigoto-based game mods
**Status:** ✅ Active Development

**Last Updated:** 2026-02-17

---

## Table of Contents

1. [What is D3dxSkinManager?](#what-is-d3dxskinmanager)
2. [Why This Project Exists](#why-this-project-exists)
3. [Technology Stack](#technology-stack)
4. [Core Features](#core-features)
5. [Target Users](#target-users)
6. [Project Goals](#project-goals)
7. [Background Context](#background-context)
8. [Related Projects](#related-projects)

---

## What is D3dxSkinManager?

**D3dxSkinManager** is a desktop application for managing, organizing, and loading game mods that use the 3DMigoto framework. It provides a modern, user-friendly interface for:

- **Importing** mod archives (7z, zip, rar)
- **Organizing** mods by character/object
- **Loading/Unloading** mods dynamically
- **Categorizing** mods with tags and metadata
- **Previewing** mod screenshots and thumbnails
- **Managing** mod conflicts and load order

### What is 3DMigoto?

3DMigoto is a DirectX modding framework that allows replacing game textures, models, and shaders at runtime. It's commonly used for:
- Character skin mods (custom outfits, textures)
- Model replacements
- Visual enhancements
- Game modding in general

**Reference:** [3DMigoto GitHub](https://github.com/bo3b/3Dmigoto/wiki)

---

## Why This Project Exists

### Problem Statement

The original d3dxSkinManage (Python) solved these problems:
1. **Manual Mod Management** - Users had to manually copy files to game directories
2. **Mod Conflicts** - Multiple mods could conflict with each other
3. **No Organization** - Hundreds of mods with no easy way to find/manage them
4. **Load Order Issues** - No way to control which mods load first

### This Rewrite Solves

1. **Outdated Technology** - Original used Python + Tkinter (hard to maintain)
2. **Performance Issues** - Slow with large mod collections (1000+ mods)
3. **Limited UI** - Tkinter limitations made modern UI difficult
4. **Hard to Extend** - Monolithic Python codebase difficult to modify
5. **No Type Safety** - Python's dynamic typing caused bugs
6. **Windows-Only** - Tied to specific Python version and OS

### Goals of This Rewrite

- ✅ Modern, maintainable codebase (.NET + React)
- ✅ Type-safe (C# + TypeScript)
- ✅ Better performance (compiled C#, SQLite database)
- ✅ Modern UI (React + Ant Design)
- ✅ Easier to extend (service-based architecture)
- ✅ Better documentation (AI-optimized docs)
- ✅ Testable (interfaces, dependency injection)

---

## Technology Stack

### Backend

| Technology | Version | Purpose |
|------------|---------|---------|
| **.NET** | 8.0 | Core runtime and framework |
| **C#** | 12.0 | Programming language |
| **Photino.NET** | 4.0.16 | Desktop window framework (like Electron for .NET) |
| **SQLite** | 3.x | Embedded database for mod metadata |
| **Newtonsoft.Json** | 13.0.4 | JSON serialization |

**Why .NET?**
- High performance (compiled)
- Type safety
- Rich ecosystem
- Excellent tooling (Visual Studio)
- User wanted .NET over Python

**Why Photino.NET?**
- Lightweight (unlike Electron)
- Uses native OS webview
- .NET backend (unlike Tauri which requires Rust)
- Small bundle size
- Simple IPC communication

### Frontend

| Technology | Version | Purpose |
|------------|---------|---------|
| **React** | 19.2.4 | UI framework |
| **TypeScript** | 4.9.5 | Type-safe JavaScript |
| **Ant Design** | 6.3.0 | UI component library |
| **Axios** | 1.13.5 | HTTP client (future API calls) |
| **React Router** | 7.13.0 | Navigation |

**Why React?**
- Component-based architecture
- Large ecosystem
- Easy to maintain
- Good developer experience

**Why TypeScript?**
- Type safety (prevent bugs)
- Better IDE support
- Self-documenting code
- Easier refactoring

**Why Ant Design?**
- Professional UI components
- Consistent design
- Well-documented
- Rich component library

### Database

**SQLite** - Embedded relational database

**Schema:**
```sql
CREATE TABLE Mods (
    SHA TEXT PRIMARY KEY,           -- SHA256 hash (unique ID)
    ObjectName TEXT NOT NULL,       -- Character/object name
    Name TEXT NOT NULL,             -- Mod display name
    Author TEXT,                    -- Mod creator
    Description TEXT,               -- Mod description
    Type TEXT DEFAULT '7z',         -- Archive type
    Grading TEXT DEFAULT 'G',       -- Content rating
    Tags TEXT,                      -- JSON array of tags
    IsLoaded INTEGER DEFAULT 0,     -- 1 if currently loaded
    IsAvailable INTEGER DEFAULT 0,  -- 1 if files exist
    ThumbnailPath TEXT,             -- Path to thumbnail image
    PreviewPath TEXT,               -- Path to preview image
    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_object_name ON Mods(ObjectName);
CREATE INDEX idx_is_loaded ON Mods(IsLoaded);
```

**Why SQLite?**
- No server needed (embedded)
- Single file database
- Fast for read-heavy workloads
- Reliable
- Zero configuration

---

## Core Features

### Current Features (v1.0.0)

#### 1. Mod Management
- ✅ Display all mods in table
- ✅ Load/unload mods
- ✅ View mod metadata (name, author, tags)
- ✅ Filter by loaded status
- ✅ Sort by object name, mod name

#### 2. User Interface
- ✅ Modern desktop window (Photino)
- ✅ React-based UI
- ✅ Header with title
- ✅ Sidebar navigation (Mods, Warehouse, Settings)
- ✅ Mod table with actions
- ✅ Status icons (loaded/unloaded)

#### 3. Backend Services
- ✅ ModService for mod operations
- ✅ SQLite database integration
- ✅ IPC communication (C# ↔ React)
- ✅ Async/await for all I/O

#### 4. Documentation
- ✅ Comprehensive docs system
- ✅ AI-optimized (RAG-friendly)
- ✅ Two-audience design (humans + AI)

### Planned Features (Upcoming)

#### Phase 2 (Near-term)
- ⏳ Mod import (drag-and-drop, file picker)
- ⏳ 7zip integration (extract archives)
- ⏳ SHA256 hash calculation
- ⏳ Classification system (wildcard patterns)
- ⏳ Image preview (thumbnails, full previews)

#### Phase 3 (Mid-term)
- ⏳ Mod warehouse (mod browser/downloader)
- ⏳ Settings page (preferences, paths)
- ⏳ Multi-user profiles
- ⏳ Conflict detection
- ⏳ Load order management

#### Phase 4 (Long-term)
- ⏳ Plugin system (extensibility)
- ⏳ Mod backup/restore
- ⏳ Mod sharing (export/import)
- ⏳ Cloud sync
- ⏳ Automatic updates

---

## Target Users

### Primary Users

**Mod Users** - Gamers who use mods
- Want easy way to manage 100+ mods
- Need to quickly enable/disable mods
- Want to organize mods by character
- Need to see what mods they have

**Mod Creators** - People who make mods
- Need to test mods quickly
- Want metadata management
- Need to organize their creations

### Secondary Users

**Developers** - Contributors to this project
- Want clean, maintainable codebase
- Need good documentation
- Want to add features easily

**AI Assistants** - Code generation tools
- Need structured documentation
- Want quick file lookups
- Need consistent patterns

---

## Project Goals

### Technical Goals

1. **Maintainability**
   - Clean architecture (services, interfaces)
   - Type safety (C#, TypeScript)
   - Comprehensive documentation
   - Testable code

2. **Performance**
   - Handle 10,000+ mods efficiently
   - Fast database queries (indexed)
   - Efficient UI rendering (virtualization)
   - Quick startup time

3. **Extensibility**
   - Plugin system (future)
   - Service-based architecture
   - Interface-based design
   - Dependency injection ready

4. **User Experience**
   - Modern, intuitive UI
   - Fast response time
   - Clear error messages
   - Keyboard shortcuts

### Feature Parity Goals

**Match Original Python Version:**
- ✅ Mod listing
- ✅ Load/unload functionality
- ⏳ Mod import
- ⏳ Classification system
- ⏳ Image previews
- ⏳ Tags and metadata
- ⏳ Warehouse system
- ⏳ Settings management

**Exceed Original:**
- ✅ Better performance (compiled, indexed DB)
- ✅ Modern UI (React, Ant Design)
- ✅ Type safety (prevent bugs)
- ✅ Better documentation
- Future: Plugin system, cloud sync

---

## Background Context

### Project History

**Original Project:**
- Name: d3dxSkinManage
- Language: Python 3.x
- UI: Tkinter
- Storage: JSON files
- Author: numlinka
- Version: 1.6.3
- Status: Stable but hard to maintain

**This Rewrite:**
- Started: 2026-02-17
- Language: C# + TypeScript
- UI: React + Ant Design
- Storage: SQLite database
- Status: Active development

### Migration Approach

**Not a Port** - Complete rewrite
- Different architecture
- Modern technologies
- Improved patterns
- Better performance

**Backward Compatible**
- Can read original data formats (future)
- Migration tool planned
- Similar feature set

---

## Related Projects

### Dependencies

**Photino.NET** - Desktop framework
- GitHub: [tryphotino/photino.NET](https://github.com/tryphotino/photino.NET)
- Docs: [tryphotino.io](https://www.tryphotino.io/)

**3DMigoto** - Game modding framework
- GitHub: [bo3b/3Dmigoto](https://github.com/bo3b/3Dmigoto)
- Wiki: [3DMigoto Wiki](https://github.com/bo3b/3Dmigoto/wiki)

### Similar Projects

**Vortex Mod Manager** (Nexus Mods)
- General-purpose mod manager
- Supports many games
- Electron-based

**Mod Organizer 2**
- Primarily for Bethesda games
- Virtual file system
- C++ based

**This Project's Niche:**
- Specialized for 3DMigoto mods
- Lightweight (not general-purpose)
- Modern tech stack
- Easy to customize

---

## Project Structure

```
D3dxSkinManager/
├── D3dxSkinManager/              # Backend (.NET)
│   ├── Program.cs                # Entry point
│   ├── Services/                 # Business logic
│   │   ├── IModService.cs        # Interface
│   │   └── ModService.cs         # Implementation
│   └── D3dxSkinManager.csproj    # Project file
│
├── D3dxSkinManager.Client/       # Frontend (React)
│   ├── src/
│   │   ├── App.tsx               # Main component
│   │   ├── services/
│   │   │   ├── photino.ts        # IPC bridge
│   │   │   └── modService.ts     # API wrapper
│   │   └── index.tsx             # Entry point
│   └── package.json
│
├── docs/                         # Documentation
│   ├── ai-assistant/             # AI guides
│   ├── core/                     # Core docs
│   ├── features/                 # Feature docs
│   └── maintenance/              # Maintenance guides
│
├── D3dxSkinManager.sln           # Solution file
├── build-production.ps1          # Build script
└── README.md                     # Main README
```

---

## Key Concepts

### Mod

A **mod** is a modification file/archive that changes game assets:
- Textures (character skins)
- Models (3D meshes)
- Shaders (visual effects)

**Identified by:** SHA256 hash (unique per file)

**Stored as:** 7z/zip/rar archive

**Metadata:** Name, author, description, tags, thumbnails

### Object

An **object** is what the mod modifies:
- Character names (e.g., "Fischl", "Keqing")
- Generic categories (e.g., "UI", "Effects")

**Purpose:** Organize mods by what they modify

### Load/Unload

**Load** - Activate mod (copy files to game directory)
**Unload** - Deactivate mod (remove files from game directory)

**State stored in:** SQLite database (IsLoaded column)

### IPC (Inter-Process Communication)

Communication between C# backend and React frontend:

**Frontend → Backend:** JSON message
```json
{
  "id": "msg_123",
  "type": "LOAD_MOD",
  "payload": { "sha": "abc123..." }
}
```

**Backend → Frontend:** JSON response
```json
{
  "id": "msg_123",
  "success": true,
  "data": { "loaded": true }
}
```

**Transport:** Photino's SendWebMessage / ReceiveWebMessage

---

## Success Criteria

### Version 1.0 (Current)
- ✅ Application runs without errors
- ✅ Can display mods from database
- ✅ Can load/unload mods
- ✅ Modern UI with Ant Design
- ✅ Comprehensive documentation

### Version 2.0 (Next)
- ⏳ Can import new mods
- ⏳ Can classify mods automatically
- ⏳ Shows mod thumbnails
- ⏳ Has settings page

### Version 3.0 (Future)
- ⏳ Mod warehouse integration
- ⏳ Multi-user profiles
- ⏳ Plugin system
- ⏳ Automatic updates

---

## Resources

### Documentation
- [README.md](../../README.md) - Main project README
- [QUICKSTART.md](../../QUICKSTART.md) - Quick start guide
- [ARCHITECTURE.md](ARCHITECTURE.md) - System architecture
- [KEYWORDS_INDEX.md](../KEYWORDS_INDEX.md) - Quick file lookup

### External Resources
- [Photino.NET Docs](https://www.tryphotino.io/)
- [3DMigoto Wiki](https://github.com/bo3b/3Dmigoto/wiki)
- [Original Python Project](https://github.com/numlinka/d3dxSkinManage)
- [.NET Documentation](https://docs.microsoft.com/dotnet/)
- [React Documentation](https://react.dev/)

---

## Quick Facts

| Attribute | Value |
|-----------|-------|
| **Language** | C# + TypeScript |
| **Framework** | .NET 10 + React 19 |
| **Desktop Framework** | Photino.NET 4.0.16 |
| **Database** | SQLite 3.x |
| **UI Library** | Ant Design 6.3.0 |
| **Platform** | Windows (primary) |
| **License** | TBD |
| **Repository** | TBD (to be published) |

---

## Getting Started

### For Users
See [QUICKSTART.md](../../QUICKSTART.md)

### For Developers
See [DEVELOPMENT.md](DEVELOPMENT.md)

### For AI Assistants
See [AI_GUIDE.md](../AI_GUIDE.md)

---

*This document provides high-level overview. See other docs for technical details.*

*Last updated: 2026-02-17*

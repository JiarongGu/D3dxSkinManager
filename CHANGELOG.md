# Changelog

All notable user-facing changes to D3dxSkinManager will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

> **📋 Note for Developers**: This changelog is for end users and GitHub releases. For detailed technical changes, see [`docs/CHANGELOG.md`](docs/CHANGELOG.md).

---

## [Unreleased]

<!-- Add new changes here for the next release -->

---

## [1.0] - 2026-03-09

### Added
- Initial public release of D3dxSkinManager v2.0
- Modern React-based user interface with light/dark theme support
- Complete mod management system with drag-and-drop organization
- Hierarchical category system for organizing mods
- Tag management with custom colors
- Batch mod editing capabilities
- Import queue with background processing
- Image preview system with gallery navigation
- Multi-language support (English and Chinese)
- Migration tool from Python version (v1.x)
- One-click game launch integration
- Screen capture tool for documenting mods
- External work directory support
- Automatic .NET runtime installation via C++ launcher

### Changed
- Complete rewrite from Python/PyQt to .NET/React architecture
- Improved mod loading performance with caching
- Enhanced archive extraction speed (~10x faster with native 7z.dll)
- Modernized UI following Ant Design principles

### Technical Details
- Built on .NET 10 with WinForms + WebView2
- React 18 + TypeScript frontend with Vite
- SQLite database for profile-scoped data storage
- Native C++ launcher for seamless .NET runtime installation
- Single-file executable with embedded resources (~14 MB)

**Initial release of D3dxSkinManager v2.0** - Complete rewrite with modern architecture.

### Core Features

#### Mod Management
- **Import & Extract**: Support for ZIP, 7Z, RAR archives and folder imports
- **Load/Unload**: One-click mod activation with automatic conflict resolution
- **Organization**: Hierarchical category system with drag-and-drop
- **Batch Operations**: Edit multiple mods simultaneously
- **Search & Filter**: Real-time search across mod names, authors, descriptions
- **Preview System**: Image gallery with Windows Photo Viewer-style navigation
- **Metadata Editing**: Name, author, description, tags, categories

#### Category System
- **Tree Structure**: Unlimited nesting depth for organization
- **Thumbnails**: Custom category images with deduplication
- **Drag & Drop**: Rearrange categories and assign mods
- **Auto-Classification**: Inherit parent categories during import

#### Tag Management
- **Custom Tags**: Create unlimited tags with custom colors
- **Color Coding**: 10 theme-compatible colors
- **Batch Tagging**: Apply tags to multiple mods at once
- **Search by Tags**: Filter mods by tag combinations

#### Import Queue
- **Background Processing**: Import multiple mods without blocking UI
- **Progress Tracking**: Real-time progress bars for each import
- **Metadata Pre-fill**: Auto-detect mod information from folders
- **Error Recovery**: Detailed error messages with retry options

#### User Interface
- **Modern Design**: Clean, professional interface with Ant Design
- **Theme Support**: Light and dark themes
- **Responsive Layout**: Resizable panels with state persistence
- **Multi-language**: English and Chinese translations
- **Keyboard Shortcuts**: Efficient navigation

#### Game Integration
- **Launch Support**: One-click launch of 3DMigoto + game
- **Configuration**: Manage launch settings per profile
- **Screen Capture**: Built-in tool for documenting mods

#### Profile System
- **Multiple Profiles**: Separate configurations for different games
- **Profile Switching**: Instant switch between profiles
- **External Storage**: Support for custom work directory locations
- **Migration Tool**: Import from Python version (v1.x)

### Installation

**Requirements:**
- Windows 10/11 (x64)
- .NET 10 Runtime (automatically installed by launcher)
- ~14 MB disk space for application
- Variable space for mod storage

**Installation Steps:**
1. Download `D3dxSkinManager-v1.0-win-x64.zip`
2. Extract to any folder
3. Run `D3dxSkinManager Launcher.exe`
4. Launcher will auto-install .NET 10 if needed

### Package Contents

```
D3dxSkinManager-v1.0-win-x64.zip
├── D3dxSkinManager Launcher.exe  (C++ launcher, auto-installs .NET)
├── D3dxSkinManager.exe            (Main application, ~12 MB)
├── libs/7z.dll                    (Native 7-Zip library for fast extraction)
└── data/languages/
    ├── en.json                    (English translations)
    └── cn.json                    (Chinese translations)
```

### Known Limitations

- Windows-only (no macOS/Linux support)
- Requires .NET 10 runtime
- 3DMigoto-specific mod format

### Migration from v1.x (Python Version)

Users migrating from the Python version can use the built-in migration tool:

1. Open D3dxSkinManager v2.0
2. Go to Tools → Migration Tool
3. Select your v1.x installation directory
4. Follow the wizard to import profiles, categories, and mods

**What Gets Migrated:**
- ✅ Mod archives and metadata
- ✅ Category structure
- ✅ Profile configurations
- ✅ Launch settings

**What Doesn't Migrate:**
- ❌ Python-specific settings
- ❌ Deprecated features

### Credits

- **Architecture**: .NET 10 + React + TypeScript
- **UI Framework**: Ant Design
- **Archive Library**: SharpSevenZip with official 7z.dll
- **Database**: SQLite
- **Build Tool**: Vite

### Support

- **Issues**: https://github.com/JiarongGu/D3dxSkinManager/issues
- **Documentation**: https://github.com/JiarongGu/D3dxSkinManager/tree/master/docs

---

## Version History

| Version | Date | Type | Summary |
|---------|------|------|---------|
| **1.0** | 2026-03-09 | Major | Initial public release (v2.0 rewrite) |
| *Legacy* | 2024-2025 | - | Python/PyQt version (archived) |

---

**Note**: For detailed technical changes and developer documentation, see [`docs/CHANGELOG.md`](docs/CHANGELOG.md).

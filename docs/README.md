# D3dxSkinManager Documentation

**For AI Code Generation & Development Reference**

---

## 🚀 Quick Start

| Need | Go To |
|------|-------|
| **AI Assistant Guide** | [AI_GUIDE.md](AI_GUIDE.md) ⭐⭐⭐ |
| **Find Component/Service** | [KEYWORDS_INDEX.md](KEYWORDS_INDEX.md) ⭐⭐⭐ |
| **Current Architecture** | [architecture/CURRENT_ARCHITECTURE.md](architecture/CURRENT_ARCHITECTURE.md) ⭐⭐⭐ |
| **See Changes** | [CHANGELOG.md](CHANGELOG.md) ⭐ |
| **Project Setup** | [core/DEVELOPMENT.md](core/DEVELOPMENT.md) |

---

## 📁 Structure

```
docs/
├── AI_GUIDE.md                    ⭐ START HERE (AI assistants)
├── KEYWORDS_INDEX.md              ⭐ Component lookup
├── CHANGELOG.md                   ⭐ Complete change history
│
├── ai-assistant/                  🤖 AI workflows & guidelines
│   ├── GUIDELINES.md              Coding patterns & best practices
│   ├── WORKFLOWS.md               Step-by-step procedures
│   ├── TESTING_GUIDE.md           Testing requirements
│   ├── TROUBLESHOOTING.md         Known issues & solutions
│   ├── REACT_CLOSURE_PATTERNS.md  ⭐ useStableRef pattern guide
│   ├── DOCUMENTATION_MAINTENANCE.md Documentation update guide
│   └── REFERENCE.md               Quick command reference
│
├── architecture/                  🏛️ System architecture & design
│   ├── CURRENT_ARCHITECTURE.md    ⭐ Complete system architecture
│   ├── MIGRATION_ARCHITECTURE.md  ⭐ NEW Migration system (2026-02-20)
│   ├── MIGRATION_PARSER_ARCHITECTURE.md  Parser service details
│   ├── DOMAIN_DESIGN.md           Domain boundaries & services
│   ├── FRONTEND_CONTEXT_ARCHITECTURE.md  React context system
│   ├── PROFILE_SERVICE_ARCHITECTURE.md   Profile system
│   ├── PATH_CONVENTIONS.md        Path handling patterns
│   └── ...                        Other architecture docs
│
├── core/                          🏗️ Project fundamentals
│   ├── PROJECT_OVERVIEW.md        What & why of project
│   ├── DEVELOPMENT.md             Development setup
│   ├── PROJECT_STRUCTURE.md       File organization
│   └── MIGRATION_GUIDE.md         Migrating from Python
│
├── features/                      ✨ Feature documentation
│   ├── INTERNATIONALIZATION.md    ⭐⭐ i18n system (EN/CN)
│   ├── CATEGORY_SYSTEM.md         Category management
│   ├── DELAYED_LOADING_UX_PATTERN.md Loading UX pattern
│   ├── PLUGINS.md                 Plugin system
│   ├── PROFILE_SYSTEM.md          Profile management
│   ├── PROFILE_AWARE_EVENTS.md    Profile-scoped events
│   └── THEME_SYSTEM.md            Theme system
│
├── how-to/                        📖 Step-by-step guides
│   └── ADD_I18N_TO_COMPONENT.md   ⭐ Adding translations
│
├── keywords/                      🔍 Keyword routing system (v4.0)
│   ├── BACKEND.md                 Backend classes & services
│   ├── FRONTEND.md                Frontend components & hooks
│   ├── DOCUMENTATION.md           Documentation files
│   └── HOW_TO.md                  How-to guides
│
├── migration/                     📦 Migration from Python version
│   └── MIGRATION_DESIGN.md        Original design document
│
├── changelogs/                    📝 Detailed change logs
│   └── YYYY-MM/                   Monthly changelog folders
│       ├── YYYY-MM-DD-name.md     Detailed change descriptions
│       └── monthly-archive.md     Archived month entries
│
├── maintenance/                   🔧 Maintenance procedures
│   ├── README.md                  Maintenance overview
│   ├── CHANGELOG_MANAGEMENT.md    ⭐ Changelog guidelines (< 200 lines rule)
│   └── KEYWORDS_INDEX_MANAGEMENT.md Keywords index maintenance
│
└── archive/                       📁 Historical documentation
    ├── 2026-02-19-migration-refactoring/  Migration refactoring session
    ├── 2026-02-17-roadmap/                Original roadmap (archived)
    ├── CONVERSION_COMPLETE_V2.md          V2 conversion complete
    ├── ARCHITECTURE_OLD.md                ⭐ OLD - See CURRENT_ARCHITECTURE.md instead
    └── ...                                Other archived docs
```

---

## 🎯 For AI Assistants

**Critical Rules:** Read [AI_GUIDE.md](AI_GUIDE.md) first!

1. Use [KEYWORDS_INDEX.md](KEYWORDS_INDEX.md) to find files (routing system v4.0)
2. Read [ai-assistant/GUIDELINES.md](ai-assistant/GUIDELINES.md) before coding
3. Update [CHANGELOG.md](CHANGELOG.md) after changes (keep < 200 lines!)
4. **Never commit without user approval**
5. **ALL user-facing text must use i18n** - See [how-to/ADD_I18N_TO_COMPONENT.md](how-to/ADD_I18N_TO_COMPONENT.md)
6. **ALL long operations must report progress** - See [features/OPERATION_NOTIFICATION_SYSTEM.md](features/OPERATION_NOTIFICATION_SYSTEM.md)

---

## 📦 Migration System (Updated 2026-02-20)

**Architecture**: Step-based workflow with 6 distinct steps

| Step | Purpose | Documentation |
|------|---------|--------------|
| Step 1 | Analyze Source | [MIGRATION_ARCHITECTURE.md](architecture/MIGRATION_ARCHITECTURE.md#step-1-analyze-source) |
| Step 2 | Migrate Configuration | [MIGRATION_ARCHITECTURE.md](architecture/MIGRATION_ARCHITECTURE.md#step-2-migrate-configuration) |
| Step 3 | Migrate Classifications | [MIGRATION_ARCHITECTURE.md](architecture/MIGRATION_ARCHITECTURE.md#step-3-migrate-classifications) |
| Step 4 | Migrate Classification Thumbnails | [MIGRATION_ARCHITECTURE.md](architecture/MIGRATION_ARCHITECTURE.md#step-4-migrate-classification-thumbnails) |
| Step 5 | Migrate Mod Archives | [MIGRATION_ARCHITECTURE.md](architecture/MIGRATION_ARCHITECTURE.md#step-5-migrate-mod-archives) |
| Step 6 | Migrate Mod Previews | [MIGRATION_ARCHITECTURE.md](architecture/MIGRATION_ARCHITECTURE.md#step-6-migrate-mod-previews) |

**Key Features:**
- ✅ Archives stored WITHOUT extensions (matches Python format)
- ✅ SharpCompress auto-detects format (ZIP/7z/RAR)
- ✅ Thin orchestrator pattern (205 lines, down from 991)
- ✅ Each step is independently testable
- ✅ Proper service layer usage

**See**: [MIGRATION_ARCHITECTURE.md](architecture/MIGRATION_ARCHITECTURE.md) for complete details

---

## 📊 Project Status

✅ UI Complete (14 phases, 40+ components)
✅ Frontend: ~470 kB bundle (Vite build)
✅ Backend: .NET 10 + WebView2
✅ Technology Stack: React 19 + TypeScript 5.9 + Vite 7 + Ant Design 6
✅ i18n: Complete bilingual support (EN/CN, 507 keys each)
✅ Operation Notifications: Real-time progress tracking with IProgressReporter
✅ Docs: Complete & organized (well-structured)
📊 Feature Parity: ~70% vs Python v1.6.3

---

## 🆕 Recent Major Updates (2026-02-21)

### Critical New Requirements
1. **Internationalization (i18n)** - ALL user-facing text must use `t('key')` translations
2. **Operation Notifications** - ALL long-running operations must use `IProgressReporter`
3. **Vite Build System** - Frontend now uses Vite instead of Create React App

### New Features
- ⭐⭐⭐ Complete i18n system with flat JSON structure
- ⭐⭐⭐ Real-time operation progress with push notifications
- ⭐⭐⭐ Category-based mod loading with auto-unload
- ⭐⭐⭐ Declarative drag & drop API (useDragDrop hook)
- ⭐⭐ Delayed loading pattern (useDelayedLoading)

### Documentation Cleanup
- Routing system for keywords index (v4.0) - Faster lookups
- CHANGELOG.md reduced from 463 to 101 lines
- AI_GUIDE.md v1.3 with i18n and progress reporting requirements
- Removed 3 obsolete files (47KB+ freed)
- Comprehensive feature docs for new systems

---

*Updated: 2026-03-09*

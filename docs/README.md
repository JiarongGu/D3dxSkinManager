# D3dxSkinManager Documentation

**For AI Code Generation & Development Reference**

---

## 🚀 Quick Start

| Need | Go To |
|------|-------|
| **AI Assistant Guide** | [AI_GUIDE.md](AI_GUIDE.md) ⭐⭐⭐ |
| **Find Component/Service** | [KEYWORDS_INDEX.md](KEYWORDS_INDEX.md) ⭐⭐⭐ |
| **Current Architecture** | [architecture/CURRENT_ARCHITECTURE.md](architecture/CURRENT_ARCHITECTURE.md) ⭐⭐⭐ |
| **Feature Gap Analysis** | [features/FEATURE_GAP_ANALYSIS_V3.md](features/FEATURE_GAP_ANALYSIS_V3.md) ⭐ |
| **See Changes** | [CHANGELOG.md](CHANGELOG.md) ⭐ |
| **Project Setup** | [core/DEVELOPMENT.md](core/DEVELOPMENT.md) |

---

## 📁 Structure

```
docs/
├── AI_GUIDE.md                    ⭐ START HERE (AI assistants)
├── KEYWORDS_INDEX.md              ⭐ Component lookup
├── CHANGELOG.md                   ⭐ Complete change history
├── QUICKSTART.md                  User guide
│
├── ai-assistant/                  🤖 AI workflows & guidelines
│   ├── GUIDELINES.md              Coding patterns & best practices
│   ├── WORKFLOWS.md               Step-by-step procedures
│   ├── TESTING_GUIDE.md           Testing requirements
│   ├── TROUBLESHOOTING.md         Known issues & solutions
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
│   ├── FEATURE_GAP_ANALYSIS_V3.md Feature parity analysis
│   ├── PLUGINS.md                 Plugin system
│   ├── PROFILE_SYSTEM.md          Profile management
│   └── THEME_SYSTEM.md            Theme system
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
│   └── CHANGELOG_MANAGEMENT.md    ⭐ Changelog guidelines (< 200 lines rule)
│
└── archive/                       📁 Historical documentation
    ├── 2026-02-19-migration-refactoring/  Migration refactoring session
    ├── 2026-02-17-roadmap/                Original roadmap (archived)
    ├── CONVERSION_COMPLETE_V2.md          V2 conversion complete
    └── ...                                Other archived docs
```

---

## 🎯 For AI Assistants

**Critical Rules:** Read [AI_GUIDE.md](AI_GUIDE.md) first!

1. Use [KEYWORDS_INDEX.md](KEYWORDS_INDEX.md) to find files
2. Read [ai-assistant/GUIDELINES.md](ai-assistant/GUIDELINES.md) before coding
3. Update [CHANGELOG.md](CHANGELOG.md) after changes
4. **Never commit without user approval**

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
✅ Frontend: 387 kB bundle
✅ Backend: .NET 10 + Photino.NET
✅ Docs: Complete & organized
📊 Feature Parity: ~60% vs Python v1.6.3

---

*Updated: 2026-02-17*

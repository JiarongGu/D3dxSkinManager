# D3dxSkinManager Documentation

**For AI Code Generation & Development Reference**

---

## 🚀 Quick Start

| Need | Go To |
|------|-------|
| **End-user guide (EN / 中文)** | [user-guide/USER_GUIDE.en.md](user-guide/USER_GUIDE.en.md) · [user-guide/USER_GUIDE.cn.md](user-guide/USER_GUIDE.cn.md) — also shown in-app (Help) |
| **AI Assistant Guide** | [AI_GUIDE.md](AI_GUIDE.md) ⭐⭐⭐ |
| **Find Component/Service** | [KEYWORDS_INDEX.md](KEYWORDS_INDEX.md) ⭐⭐⭐ |
| **Current Architecture** | [architecture/CURRENT_ARCHITECTURE.md](architecture/CURRENT_ARCHITECTURE.md) ⭐⭐⭐ |
| **See Changes** | [CHANGELOG.md](CHANGELOG.md) ⭐ |
| **Project Setup** | [core/DEVELOPMENT.md](core/DEVELOPMENT.md) |

---

## 📁 Structure

```
docs/
├── AI_GUIDE.md                    ⭐ Entry point (AI assistants) — rules, skills, workflow
├── KEYWORDS_INDEX.md              ⭐ Routing hub — find anything fast
├── CHANGELOG.md                   ⭐ Complete change history
│
├── ai-assistant/                  🤖 AI-specific guides
│   ├── REACT_CLOSURE_PATTERNS.md  ⭐ React hook patterns (closures, useStableRef)
│   ├── REFERENCE.md               Quick command reference
│   ├── TESTING_GUIDE.md           Testing requirements and patterns
│   └── TROUBLESHOOTING.md         Known issues & solutions
│
├── architecture/                  🏛️ System architecture & design
│   ├── CURRENT_ARCHITECTURE.md    ⭐ Complete system architecture
│   ├── DOMAIN_DESIGN.md           Domain boundaries & services
│   ├── EVENT_HUB_ARCHITECTURE.md  IProfileEventBus patterns
│   ├── FRONTEND_CONTEXT_ARCHITECTURE.md  React context system
│   ├── FRONTEND_SERVICE_ARCHITECTURE.md  Frontend IPC services
│   ├── PATH_CONVENTIONS.md        Path handling patterns
│   ├── WORKFLOW_ARCHITECTURE.md   Workflow engine design
│   └── ...                        Other architecture docs
│
├── core/                          🏗️ Project fundamentals
│   ├── ADVANCED_PATTERNS.md       Complex non-automatable patterns
│   ├── DESIGN_DECISIONS.md        Architecture constraints (authoritative)
│   ├── DEVELOPMENT.md             Development setup
│   ├── PROJECT_OVERVIEW.md        What & why of project
│   └── PROJECT_STRUCTURE.md       File organization
│
├── features/                      ✨ Feature documentation
│   ├── CATEGORY_SYSTEM.md         Category management
│   ├── CACHE_MANAGEMENT.md        Caching patterns
│   ├── DELAYED_LOADING_UX_PATTERN.md Loading UX pattern
│   ├── INTERNATIONALIZATION.md    i18n system (EN/CN)
│   ├── PLUGINS.md                 Plugin system
│   ├── PROFILE_SYSTEM.md          Profile management
│   ├── PROFILE_AWARE_EVENTS.md    Profile-scoped events
│   └── THEME_SYSTEM.md            Theme system
│
├── how-to/                        📖 Step-by-step guides
│   ├── ADD_I18N_TO_COMPONENT.md   ⭐ Adding translations
│   ├── BUILD_AND_DEPLOY.md        Build and deployment
│   ├── GITHUB_RELEASE_GUIDE.md    Creating releases
│   └── TESTING_RELEASES.md        QA a release build
│
├── keywords/                      🔍 Keyword routing (v4.0)
│   ├── BACKEND.md                 C# classes, services, modules
│   ├── DOCUMENTATION.md           Documentation catalog
│   ├── FRONTEND.md                React components, hooks
│   └── HOW_TO.md                  Task-based how-to index
│
└── changelogs/                    📝 Detailed change logs
    └── 2026-03/                   Monthly folders
        └── YYYY-MM-DD-name.md     Individual change entries
```

---

## 🎯 For AI Assistants

**Critical Rules:** Read [AI_GUIDE.md](AI_GUIDE.md) first — it is the entry point and contains all mandatory session rules.

1. Read `docs/AI_GUIDE.md` before any code generation (mandatory rules, skills, workflow)
2. Use [KEYWORDS_INDEX.md](KEYWORDS_INDEX.md) to find files (routing system v4.0)
3. Run `/doc-loader "task" scope` to load task-specific docs
4. **Never commit without user approval**
5. **ALL user-facing text must use i18n** — See [how-to/ADD_I18N_TO_COMPONENT.md](how-to/ADD_I18N_TO_COMPONENT.md)

---

## 📦 Migration System

**Architecture**: Step-based workflow with 6 distinct steps (Python → .NET migration)

**Key Features:**
- ✅ Archives stored WITHOUT extensions (matches Python format)
- ✅ SharpCompress auto-detects format (ZIP/7z/RAR)
- ✅ Thin orchestrator pattern — each step is independently testable
- ✅ Proper service layer usage

**See**: [architecture/WORKFLOW_ARCHITECTURE.md](architecture/WORKFLOW_ARCHITECTURE.md) for workflow engine details

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

## 🆕 Recent Major Updates (2026-04-12)

### Documentation System Overhaul
- **AI_GUIDE.md v6.0** — Now the mandatory entry point with all session rules built in
- **doc-loader** — Always loads AI_GUIDE.md first; no longer depends on deleted WORKFLOWS.md
- **CLAUDE.md** — Added explicit "Read AI_GUIDE.md" as Step 1 before code generation
- Removed stale docs (WORKFLOWS.md, GUIDELINES.md references cleaned up throughout)

### See [CHANGELOG.md](CHANGELOG.md) for full change history

---

*Updated: 2026-03-09*

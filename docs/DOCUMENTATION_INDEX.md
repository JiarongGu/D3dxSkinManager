# Documentation Index

**Last Updated:** 2026-02-23
**Purpose:** Central index for all documentation (replaces scattered README files)

---

## 🚀 Quick Start Guides

| Guide | Purpose | For |
|-------|---------|-----|
| [AI_GUIDE.md](AI_GUIDE.md) | AI assistant onboarding and rules | AI Sessions |
| [README.md](../README.md) | Project overview and setup | Developers |
| [core/DEVELOPMENT.md](core/DEVELOPMENT.md) | Development environment setup | New Contributors |

---

## 📚 Architecture Documentation

| Document | Purpose | Status |
|----------|---------|--------|
| [CURRENT_ARCHITECTURE.md](architecture/CURRENT_ARCHITECTURE.md) | System architecture overview | ✅ Current |
| [MODULE_ARCHITECTURE.md](architecture/MODULE_ARCHITECTURE.md) | Module structure and organization | ✅ Current |
| [APP_FACADE_REFACTORING.md](architecture/APP_FACADE_REFACTORING.md) | Centralized AppFacade pattern | ✅ Current |
| [DOMAIN_DESIGN.md](architecture/DOMAIN_DESIGN.md) | Domain-driven design principles | ✅ Current |
| [FRONTEND_CONTEXT_ARCHITECTURE.md](architecture/FRONTEND_CONTEXT_ARCHITECTURE.md) | React context architecture | ✅ Current |
| [PATH_CONVENTIONS.md](architecture/PATH_CONVENTIONS.md) | Path handling conventions | ✅ Current |
| [LOGGING_ARCHITECTURE.md](architecture/LOGGING_ARCHITECTURE.md) | Logging system design | ✅ Current |


---

## 🔧 Feature Documentation

| Feature | Document | Status |
|---------|----------|--------|
| Operation Notifications | [OPERATION_NOTIFICATION_SYSTEM.md](features/OPERATION_NOTIFICATION_SYSTEM.md) | ✅ Implemented |
| Internationalization | [INTERNATIONALIZATION.md](features/INTERNATIONALIZATION.md) | ✅ Implemented |
| Delayed Loading UX | [DELAYED_LOADING_UX_PATTERN.md](features/DELAYED_LOADING_UX_PATTERN.md) | ✅ Implemented |
| Classification System | [CLASSIFICATION_SYSTEM.md](features/CLASSIFICATION_SYSTEM.md) | ✅ Implemented |
| Plugin System | [PLUGIN_SYSTEM.md](features/PLUGIN_SYSTEM.md) | ✅ Implemented |
| Mod Detection | [AUTO_MOD_DETECTION.md](features/AUTO_MOD_DETECTION.md) | ✅ Implemented |
| Migration Tool | [MIGRATION_TOOL.md](features/MIGRATION_TOOL.md) | ✅ Implemented |
| Archive Support | [ARCHIVE_SUPPORT.md](features/ARCHIVE_SUPPORT.md) | ✅ Implemented |

---

## 🤖 AI Assistant Resources

| Resource | Purpose |
|----------|---------|
| [GUIDELINES.md](ai-assistant/GUIDELINES.md) | Coding patterns and best practices |
| [WORKFLOWS.md](ai-assistant/WORKFLOWS.md) | Step-by-step procedures |
| [TROUBLESHOOTING.md](ai-assistant/TROUBLESHOOTING.md) | Common issues and solutions |
| [TESTING_GUIDE.md](ai-assistant/TESTING_GUIDE.md) | Testing requirements |
| [REFERENCE.md](ai-assistant/REFERENCE.md) | Quick command reference |
| [REACT_CLOSURE_PATTERNS.md](ai-assistant/REACT_CLOSURE_PATTERNS.md) | React callback patterns |

---

## 📋 How-To Guides

| Task | Guide |
|------|-------|
| Add i18n to component | [ADD_I18N_TO_COMPONENT.md](how-to/ADD_I18N_TO_COMPONENT.md) |
| Add a new service | [keywords/HOW_TO.md](keywords/HOW_TO.md#adding-services) |
| Create React component | [keywords/HOW_TO.md](keywords/HOW_TO.md#frontend-development) |
| Add IPC message | [keywords/HOW_TO.md](keywords/HOW_TO.md#ipc-messages) |

---

## 🔍 Lookup Tables

| Index | Purpose | Size |
|-------|---------|------|
| [KEYWORDS_INDEX.md](KEYWORDS_INDEX.md) | Component/service routing hub | ~150 lines |
| [keywords/BACKEND.md](keywords/BACKEND.md) | Backend component locations | ~350 lines |
| [keywords/FRONTEND.md](keywords/FRONTEND.md) | Frontend component locations | ~550 lines |
| [keywords/DOCUMENTATION.md](keywords/DOCUMENTATION.md) | Documentation file index | ~220 lines |
| [keywords/HOW_TO.md](keywords/HOW_TO.md) | Task procedures | ~370 lines |

---

## 📅 Change Logs

| Log | Purpose |
|-----|---------|
| [CHANGELOG.md](CHANGELOG.md) | Main changelog (< 200 lines) |
| [changelogs/2026-02/](changelogs/2026-02/) | February 2026 detailed changes |
| [changelogs/archive/](changelogs/archive/) | Historical changes |

### Changelog Management
- Main CHANGELOG.md must stay under 200 lines
- Detailed changes go in monthly folders
- See [maintenance/CHANGELOG_MANAGEMENT.md](maintenance/CHANGELOG_MANAGEMENT.md)

---

## 📊 Project Information

| Document | Purpose |
|----------|---------|
| [PROJECT_OVERVIEW.md](core/PROJECT_OVERVIEW.md) | What and why of the project |
| [PROJECT_STRUCTURE.md](core/PROJECT_STRUCTURE.md) | File and folder organization |
| [ORIGINAL_COMPARISON.md](core/ORIGINAL_COMPARISON.md) | Python vs .NET comparison |
| [MIGRATION_GUIDE.md](core/MIGRATION_GUIDE.md) | Porting from Python |
| [DESIGN_DECISIONS.md](core/DESIGN_DECISIONS.md) | Architectural decisions |

---

## 🧹 Maintenance

| Document | Purpose |
|----------|---------|
| [DOCUMENTATION_MAINTENANCE.md](ai-assistant/DOCUMENTATION_MAINTENANCE.md) | How to maintain docs |
| [CHANGELOG_MANAGEMENT.md](maintenance/CHANGELOG_MANAGEMENT.md) | Changelog rules |
| [KEYWORDS_INDEX_MANAGEMENT.md](maintenance/KEYWORDS_INDEX_MANAGEMENT.md) | Keywords index rules |

---

## Navigation Tips

1. **Finding Components/Services:** Start with [KEYWORDS_INDEX.md](KEYWORDS_INDEX.md)
2. **Recent Changes:** Check [CHANGELOG.md](CHANGELOG.md)
3. **Architecture Questions:** See [CURRENT_ARCHITECTURE.md](architecture/CURRENT_ARCHITECTURE.md)
4. **Module Questions:** See [MODULE_ARCHITECTURE.md](architecture/MODULE_ARCHITECTURE.md)
5. **How-To Tasks:** Check [keywords/HOW_TO.md](keywords/HOW_TO.md)
6. **AI Guidelines:** Read [AI_GUIDE.md](AI_GUIDE.md)

---

## Documentation Health Metrics

- **Total Files:** 75 markdown files
- **Total Lines:** ~30,000 lines (after optimization)
- **Deprecated Files:** 4 (marked with warnings)
- **Last Cleanup:** 2026-02-23

---

*This index is the authoritative source for documentation navigation. Keep it updated when adding new docs.*
# Keywords Index

> **🤖 AI ASSISTANTS:** This is the routing hub! Load specific domain files for detailed info.
>
> **NEW ROUTING SYSTEM (v4.0):**
> - 🔹 **Backend code?** → Load [keywords/BACKEND.md](keywords/BACKEND.md)
> - 🔹 **Frontend code?** → Load [keywords/FRONTEND.md](keywords/FRONTEND.md)
> - 🔹 **Documentation?** → Load [keywords/DOCUMENTATION.md](keywords/DOCUMENTATION.md)
> - 🔹 **How-to guides?** → Load [keywords/HOW_TO.md](keywords/HOW_TO.md)

**Purpose:** Fast routing to domain-specific indexes (< 500 lines each).

**Last Updated:** 2026-02-20 (v4.0 - Routing System Implemented)

**Management Guide:** [maintenance/KEYWORDS_INDEX_MANAGEMENT.md](maintenance/KEYWORDS_INDEX_MANAGEMENT.md)

---

## 🔍 Quick Routing Guide

| What You Need | Load This File |
|---------------|----------------|
| **Backend C# classes, services, modules** | [keywords/BACKEND.md](keywords/BACKEND.md) (~350 lines) |
| **React components, hooks, services** | [keywords/FRONTEND.md](keywords/FRONTEND.md) (~550 lines) |
| **Documentation files, guides** | [keywords/DOCUMENTATION.md](keywords/DOCUMENTATION.md) (~220 lines) |
| **How-to tasks, common operations** | [keywords/HOW_TO.md](keywords/HOW_TO.md) (~370 lines) |

---

## Quick Summary (Use Routing Files for Details!)

### Backend (C#) - [FULL DETAILS →](keywords/BACKEND.md)

| What You Need | Where To Look |
|---------------|---------------|
| Entry point & DI | [keywords/BACKEND.md](keywords/BACKEND.md#entry-point) |
| Core services | [keywords/BACKEND.md](keywords/BACKEND.md#core-module) |
| Mods module | [keywords/BACKEND.md](keywords/BACKEND.md#mods-module) |
| Migration system | [keywords/BACKEND.md](keywords/BACKEND.md#migration-module) |
| Other modules | [keywords/BACKEND.md](keywords/BACKEND.md#modules) |

### Frontend (React + TypeScript) - [FULL DETAILS →](keywords/FRONTEND.md)

| What You Need | Where To Look |
|---------------|---------------|
| Components | [keywords/FRONTEND.md](keywords/FRONTEND.md#module-components) |
| Hooks | [keywords/FRONTEND.md](keywords/FRONTEND.md#custom-hooks) |
| Services | [keywords/FRONTEND.md](keywords/FRONTEND.md#services) |
| Context providers | [keywords/FRONTEND.md](keywords/FRONTEND.md#context-providers) |
| Dialogs & windows | [keywords/FRONTEND.md](keywords/FRONTEND.md#dialog-components) |

### Documentation - [FULL DETAILS →](keywords/DOCUMENTATION.md)

| What You Need | Where To Look |
|---------------|---------------|
| Architecture docs | [keywords/DOCUMENTATION.md](keywords/DOCUMENTATION.md#architecture-documentation) |
| AI assistant guides | [keywords/DOCUMENTATION.md](keywords/DOCUMENTATION.md#ai-assistant-guides) |
| Feature docs | [keywords/DOCUMENTATION.md](keywords/DOCUMENTATION.md#feature-documentation) |
| Maintenance guides | [keywords/DOCUMENTATION.md](keywords/DOCUMENTATION.md#maintenance-guides) |

### How-To Guides - [FULL DETAILS →](keywords/HOW_TO.md)

| What You Need | Where To Look |
|---------------|---------------|
| Adding services | [keywords/HOW_TO.md](keywords/HOW_TO.md#adding-services) |
| Adding components | [keywords/HOW_TO.md](keywords/HOW_TO.md#adding-components) |
| Using patterns | [keywords/HOW_TO.md](keywords/HOW_TO.md#facade-pattern) |
| Build & run | [keywords/HOW_TO.md](keywords/HOW_TO.md#building--running) |
| Common issues | [keywords/HOW_TO.md](keywords/HOW_TO.md#common-issues) |

---

## Most Common Quick Links

### Backend Entry Points
- **Program.cs** → `D3dxSkinManager/Program.cs` (main entry, DI, IPC)
- **ModFacade** → `Modules/Mods/ModFacade.cs` (mod operations)
- **MigrationService** → `Modules/Migration/Services/MigrationService.cs` (migration)

**Full Backend Index:** [keywords/BACKEND.md](keywords/BACKEND.md)

### Frontend Entry Points
- **App.tsx** → `src/App.tsx` (root component)
- **ModsView** → `src/modules/mods/components/ModsView.tsx` (main mods UI)
- **bridgeService** → `src/shared/services/bridgeService.ts` (IPC)

**Full Frontend Index:** [keywords/FRONTEND.md](keywords/FRONTEND.md)

### Essential Documentation
- **AI_GUIDE.md** → `docs/AI_GUIDE.md` (start here for AI workflows)
- **ARCHITECTURE.md** → `docs/architecture/CURRENT_ARCHITECTURE.md` (system design)
- **MIGRATION_ARCHITECTURE.md** → `docs/architecture/MIGRATION_ARCHITECTURE.md` (migration system)

**Full Documentation Index:** [keywords/DOCUMENTATION.md](keywords/DOCUMENTATION.md)

### Quick How-To
- **Add backend service** → [keywords/HOW_TO.md#adding-services](keywords/HOW_TO.md#adding-services)
- **Add React component** → [keywords/HOW_TO.md#adding-components](keywords/HOW_TO.md#adding-components)
- **⭐ Loading without flicker** → [keywords/HOW_TO.md#delayed-loading-no-flicker-pattern](keywords/HOW_TO.md#delayed-loading-no-flicker-pattern) **NEW!**
- **Build project** → [keywords/HOW_TO.md#build-for-production](keywords/HOW_TO.md#build-for-production)

**Full How-To Index:** [keywords/HOW_TO.md](keywords/HOW_TO.md)

---

## File Structure Summary

```
D3dxSkinManager/
├── Program.cs                           (entry point)
├── Modules/                             (backend modules)
│   ├── Core/                           (file, image, logging)
│   ├── Mods/                           (mod management)
│   ├── Migration/                      (Python → React migration)
│   ├── Settings/                       (settings)
│   ├── Profiles/                       (profiles)
│   ├── Plugins/                        (plugin infrastructure)
│   ├── Launch/                         (game launch)
│   └── Tools/                          (utilities)
│
D3dxSkinManager.Client/src/
├── App.tsx                             (root component)
├── modules/                            (feature modules)
├── components/                         (shared components)
├── shared/                             (context, components, utils)
├── hooks/                              (custom hooks)
├── services/                           (API services)
└── types/                              (TypeScript types)

docs/
├── KEYWORDS_INDEX.md                   (this file - routing hub)
├── keywords/                           (domain-specific indexes)
│   ├── BACKEND.md                      (C# classes & services)
│   ├── FRONTEND.md                     (React components & hooks)
│   ├── DOCUMENTATION.md                (docs catalog)
│   └── HOW_TO.md                       (task-based guides)
├── architecture/                       (system design docs)
├── maintenance/                        (maintenance guides)
└── changelogs/                         (detailed change history)
```

---

## Using The Routing System

### For AI Assistants:

**Step 1:** Identify what you need:
- Backend code? → Load [keywords/BACKEND.md](keywords/BACKEND.md)
- Frontend code? → Load [keywords/FRONTEND.md](keywords/FRONTEND.md)
- Documentation? → Load [keywords/DOCUMENTATION.md](keywords/DOCUMENTATION.md)
- How-to guide? → Load [keywords/HOW_TO.md](keywords/HOW_TO.md)

**Step 2:** Use Ctrl+F in the loaded file to find specific items

**Step 3:** Follow file paths to load source files

### Benefits:
- ✅ Each domain file < 500 lines (fast to load)
- ✅ Clear separation of concerns
- ✅ Easy to maintain and update
- ✅ Scalable (can add sub-folders if needed)

---

**Current Line Count**: ~150 lines (Target: < 200 lines for routing hub)
**Last Updated**: 2026-02-20 (v4.0 - Routing System)
**Next Review**: 2026-05-01

---

**Routing System Files:**
- [keywords/BACKEND.md](keywords/BACKEND.md) (~350 lines)
- [keywords/FRONTEND.md](keywords/FRONTEND.md) (~550 lines)
- [keywords/DOCUMENTATION.md](keywords/DOCUMENTATION.md) (~220 lines)
- [keywords/HOW_TO.md](keywords/HOW_TO.md) (~370 lines)

**Backup:**
- [architecture/KEYWORDS_INDEX_DETAILED_BACKUP.md](architecture/KEYWORDS_INDEX_DETAILED_BACKUP.md) (original detailed index)

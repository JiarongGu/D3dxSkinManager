# Keywords Index

> **🤖 AI ASSISTANTS:** This is the routing hub! Load specific domain files for detailed info.
>
> **NEW ROUTING SYSTEM (v4.0):**
> - 🔹 **Backend code?** → Load [keywords/BACKEND.md](keywords/BACKEND.md)
> - 🔹 **Frontend code?** → Load [keywords/FRONTEND.md](keywords/FRONTEND.md)
> - 🔹 **Documentation?** → Load [keywords/DOCUMENTATION.md](keywords/DOCUMENTATION.md)
> - 🔹 **How-to guides?** → Load [keywords/HOW_TO.md](keywords/HOW_TO.md)

**Purpose:** Fast routing to domain-specific indexes (< 500 lines each).

**Last Updated:** 2026-04-12

---

## 🔍 Quick Routing Guide

| What You Need | Load This File |
|---------------|----------------|
| **Backend C# classes, services, modules** | [keywords/BACKEND.md](keywords/BACKEND.md) |
| **React components, hooks, services** | [keywords/FRONTEND.md](keywords/FRONTEND.md) |
| **Documentation files, guides** | [keywords/DOCUMENTATION.md](keywords/DOCUMENTATION.md) |
| **How-to tasks, common operations** | [keywords/HOW_TO.md](keywords/HOW_TO.md) |

---

## Quick Summary (Use Routing Files for Details!)

### Backend (C#) - [FULL DETAILS →](keywords/BACKEND.md)

| What You Need | Where To Look |
|---------------|---------------|
| Entry, infrastructure, IPC routing | [keywords/BACKEND.md](keywords/BACKEND.md) — "Entry + Infrastructure" |
| Core services (registry, download, WebView) | [keywords/BACKEND.md](keywords/BACKEND.md) — "Core module" |
| Mod services (lifecycle, archive, merge, ini) | [keywords/BACKEND.md](keywords/BACKEND.md) — "Mod module" |
| Everything else (tool, setting, workflow…) | [keywords/BACKEND.md](keywords/BACKEND.md) — "Other modules" |

### Frontend (React + TypeScript) - [FULL DETAILS →](keywords/FRONTEND.md)

| What You Need | Where To Look |
|---------------|---------------|
| Module components (mod, tool, setting…) | [keywords/FRONTEND.md](keywords/FRONTEND.md) — "Modules" |
| Shared atoms/dialogs (compact, common) | [keywords/FRONTEND.md](keywords/FRONTEND.md) — "Shared components" |
| Hooks, context, stores | [keywords/FRONTEND.md](keywords/FRONTEND.md) — "Shared hooks/context/stores" |
| IPC services + types | [keywords/FRONTEND.md](keywords/FRONTEND.md) — "Services" / "Types" |

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
- **Program.cs** → `D3dxSkinManager/Program.cs` (main entry)
- **ProfileServiceRouter** → `Infrastructure/ProfileServiceRouter.cs` (IPC module → facade routing)
- **ModFacade** → `Modules/Mod/ModFacade.cs` (mod operations)
- **MigrationRunner** → `Modules/Fluent/Services/MigrationRunner.cs` (database schema migrations)

**Full Backend Index:** [keywords/BACKEND.md](keywords/BACKEND.md)

### Frontend Entry Points
- **App.tsx** → `src/App.tsx` (root component)
- **ModHierarchicalView** → `src/modules/mod/components/ModHierarchicalView.tsx` (main 3-panel mods UI)
- **bridgeService** → `src/shared/services/bridgeService.ts` (IPC)

**Full Frontend Index:** [keywords/FRONTEND.md](keywords/FRONTEND.md)

### Essential Documentation
- **CLAUDE.md** → `CLAUDE.md` (mandatory rules — auto-loaded every session)
- **AI_GUIDE.md** → `docs/AI_GUIDE.md` (⭐ entry point — mandatory rules, all 18 skills, session workflow)
- **ARCHITECTURE.md** → `docs/architecture/CURRENT_ARCHITECTURE.md` (system design)
- **WORKFLOW_ARCHITECTURE.md** → `docs/architecture/WORKFLOW_ARCHITECTURE.md` (workflow engine + migration step system)
- **DATABASE_MIGRATION_ARCHITECTURE.md** → `docs/architecture/DATABASE_MIGRATION_ARCHITECTURE.md` (Fluent database migrations)

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
**Last Updated**: 2026-07-06 (v4.1 — remote library redesign + in-app user guide)
**Next Review**: 2026-05-01

---

**Routing System Files:**
- [keywords/BACKEND.md](keywords/BACKEND.md) (~120 lines, rewritten 2026-07-05)
- [keywords/FRONTEND.md](keywords/FRONTEND.md) (~120 lines, rewritten 2026-07-05)
- [keywords/DOCUMENTATION.md](keywords/DOCUMENTATION.md) (~220 lines)
- [keywords/HOW_TO.md](keywords/HOW_TO.md) (~370 lines)

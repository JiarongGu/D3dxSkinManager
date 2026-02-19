# KEYWORDS_INDEX Routing Logic Implementation

**Status**: ✅ IMPLEMENTED (2026-02-20)
**Created**: 2026-02-20
**Updated**: 2026-02-20

---

## Implementation Status: COMPLETE

**Main Index:** `docs/KEYWORDS_INDEX.md` (~150 lines - routing hub)
**Domain Indexes:**
- ✅ `docs/keywords/BACKEND.md` (~350 lines)
- ✅ `docs/keywords/FRONTEND.md` (~550 lines)
- ✅ `docs/keywords/DOCUMENTATION.md` (~220 lines)
- ✅ `docs/keywords/HOW_TO.md` (~370 lines)

---

## Folder Structure with Routing (IMPLEMENTED)

### Problem to Solve
As project grows:
- Single file may become too large even with 500-line limit
- AI needs to load entire file for simple lookups
- Hard to maintain specific domain areas

### Solution: Folder-Based Routing

```
docs/
├── KEYWORDS_INDEX.md               ← Routing hub (~150 lines)
└── keywords/                       ← Domain-specific indexes
    ├── BACKEND.md                  ← Backend classes/services (~350 lines)
    ├── FRONTEND.md                 ← Frontend components/hooks (~550 lines)
    ├── DOCUMENTATION.md            ← Documentation catalog (~220 lines)
    └── HOW_TO.md                   ← Task-based guides (~370 lines)
```

**Current Size**: ~1,640 lines total (distributed across 5 files)
**Per-Query Load**: ~150-550 lines (single file)

---

## Routing Logic

### Step 1: AI Determines Domain
Based on user query, determine which index file to load:

**User Query** → **Index File**
- "Where is ModFacade?" → `BACKEND.md`
- "Find ModTable component" → `FRONTEND.md`
- "How do I add a service?" → `HOW_TO.md`
- "Where are config files?" → `WHERE_IS.md`
- "Find migration docs" → `DOCUMENTATION.md`

### Step 2: Load Specific Index
- Load only the relevant index file (not all)
- Saves tokens, faster lookups
- Each file stays under 200 lines

### Step 3: Follow Links
- Index has file path
- AI loads the actual file
- No need to load all indexes

---

## File Size Targets

| File | Target Size | Content |
|------|-------------|---------|
| README.md | 50 lines | Routing guide, domain map |
| BACKEND.md | < 200 lines | All backend classes |
| FRONTEND.md | < 200 lines | All frontend components |
| DOCUMENTATION.md | < 100 lines | Doc files only |
| HOW_TO.md | < 100 lines | Common task links |
| WHERE_IS.md | < 100 lines | Quick location map |

**Total**: ~750 lines (distributed)
**Per Query Load**: ~50-200 lines (single file)

---

## README.md Structure (Routing Guide)

```markdown
# Keywords Index - Routing Guide

> **🤖 AI: Choose the right index file based on your query**

## Quick Routing

| You Need | Load This File |
|----------|----------------|
| Backend class/service | [BACKEND.md](BACKEND.md) |
| Frontend component/hook | [FRONTEND.md](FRONTEND.md) |
| Documentation file | [DOCUMENTATION.md](DOCUMENTATION.md) |
| "How do I..." | [HOW_TO.md](HOW_TO.md) |
| "Where is..." | [WHERE_IS.md](WHERE_IS.md) |

## Routing Examples

- "Where is ModFacade?" → Load [BACKEND.md](BACKEND.md)
- "Find useModData hook" → Load [FRONTEND.md](FRONTEND.md)
- "How to add service?" → Load [HOW_TO.md](HOW_TO.md)
- "Where are archives stored?" → Load [WHERE_IS.md](WHERE_IS.md)

## File Structure

Each index file follows format:
`Name → path/to/File.ext (purpose)`

**Example:**
- **ModFacade** → `Modules/Mods/ModFacade.cs` (mod operations IPC)
```

---

## BACKEND.md Example

```markdown
# Backend Index

## Modules

### Core Module
- **FileService** → `Modules/Core/Services/FileService.cs` (file operations)
- **ImageService** → `Modules/Core/Services/ImageService.cs` (image processing)
- **LogHelper** → `Modules/Core/Services/LogHelper.cs` (logging)

### Mods Module
- **ModFacade** → `Modules/Mods/ModFacade.cs` (IPC entry point)
- **ModManagementService** → `Modules/Mods/Services/ModManagementService.cs` (CRUD)
- **ModFileService** → `Modules/Mods/Services/ModFileService.cs` (load/unload)
[...]

**Line Count**: ~150 lines
```

---

## FRONTEND.md Example

```markdown
# Frontend Index

## Components

### Mods Module
- **ModsView** → `src/modules/mods/components/ModsView.tsx` (main view)
- **ModTable** → `src/components/mods/ModTable.tsx` (mod list)
- **ModEditDialog** → `src/modules/mods/components/ModEditDialog/` (edit UI)

### Hooks
- **useModData** → `src/hooks/useModData.ts` (data fetching)
- **useModFilters** → `src/hooks/useModFilters.ts` (filtering)
- **useModActions** → `src/hooks/useModActions.ts` (operations)
[...]

**Line Count**: ~150 lines
```

---

## HOW_TO.md Example

```markdown
# How-To Index

## Common Tasks

| Task | Documentation | Key Files |
|------|--------------|-----------|
| Add new service | ai-assistant/WORKFLOWS.md | Modules/*/Services/ |
| Add React component | ai-assistant/WORKFLOWS.md | src/components/ |
| Update CHANGELOG | maintenance/CHANGELOG_MANAGEMENT.md | CHANGELOG.md |
| Build project | core/DEVELOPMENT.md | build-production.ps1 |
[...]

**Line Count**: ~50 lines
```

---

## WHERE_IS.md Example

```markdown
# Where-Is Index

## Quick Locations

| What | Location |
|------|----------|
| Main entry (backend) | D3dxSkinManager/Program.cs |
| Main UI (frontend) | D3dxSkinManager.Client/src/App.tsx |
| Data directory | data/ (runtime) |
| Mod archives | data/profiles/*/mods/{SHA} |
| Config files | data/settings/, data/profiles/*/ |
[...]

**Line Count**: ~50 lines
```

---

## Implementation Plan

### Phase 1: Structure Creation
1. Create `docs/keywords/` folder
2. Create README.md with routing guide
3. Split current KEYWORDS_INDEX.md into domain files

### Phase 2: Migration
1. Move backend entries → BACKEND.md
2. Move frontend entries → FRONTEND.md
3. Move how-to entries → HOW_TO.md
4. Move location entries → WHERE_IS.md
5. Keep docs/KEYWORDS_INDEX.md as legacy redirect

### Phase 3: Update Documentation
1. Update AI_GUIDE.md with routing strategy
2. Update KEYWORDS_INDEX_MANAGEMENT.md
3. Add routing examples

### Phase 4: Deprecate Old File
1. Replace docs/KEYWORDS_INDEX.md with redirect to keywords/README.md
2. Update all documentation links
3. Archive old KEYWORDS_INDEX.md

---

## Benefits

### For AI Assistants:
✅ Load only relevant index (saves tokens)
✅ Faster lookups (smaller files)
✅ Clear routing logic (know which file to load)
✅ Better organization (domain separation)

### For Maintenance:
✅ Easier to update (smaller, focused files)
✅ Domain-specific ownership (backend vs frontend)
✅ Scalable (add new domains as needed)
✅ Each file stays under size limits

### For Users:
✅ Faster AI responses (less context loading)
✅ Better organization
✅ Easier to navigate manually

---

## Decision Criteria

### When to Implement:
- ✅ Current KEYWORDS_INDEX.md > 400 lines
- ✅ Frequent queries from different domains
- ✅ Noticeable performance impact

### Current Status:
- ❌ Current file: 243 lines (well under limit)
- ❌ Single domain queries work well
- ❌ No performance issues

### Recommendation:
**Defer implementation until:**
1. KEYWORDS_INDEX.md approaches 400 lines, OR
2. AI performance degrades, OR
3. Domain separation becomes necessary

**Monitor**: Review quarterly

---

## Alternative: Hybrid Approach

Keep single file but add domain markers:

```markdown
# Keywords Index

## Quick Jump
- [Backend](#backend-c)
- [Frontend](#frontend-react--typescript)
- [Documentation](#documentation)
- [How-To](#how-do-i)

---

## Backend (C#)
[All backend entries...]

---

## Frontend (React + TypeScript)
[All frontend entries...]
```

**Pros**:
- Simpler (no folder structure)
- Still works with Ctrl+F
- Good enough for current size

**Cons**:
- Still loads entire file
- Harder to maintain as it grows

---

## Conclusion

**Implementation Status**: ✅ COMPLETE (2026-02-20)
**Current Structure**: 5-file routing system
**Total Size**: ~1,640 lines (distributed)
**Per-Query Load**: ~150-550 lines

---

## Future Enhancement: Sub-Folder Support

### When Needed

If individual domain files exceed 500 lines:
- **BACKEND.md** > 500 lines → Create `keywords/backend/` sub-folder
- **FRONTEND.md** > 500 lines → Create `keywords/frontend/` sub-folder

### Sub-Folder Structure Example

**If FRONTEND.md grows too large:**

```
docs/keywords/
├── BACKEND.md                      (~350 lines - OK)
├── DOCUMENTATION.md                (~220 lines - OK)
├── HOW_TO.md                       (~370 lines - OK)
├── FRONTEND.md                     (~150 lines - routing only)
└── frontend/                       (sub-domain split)
    ├── COMPONENTS.md               (React components)
    ├── HOOKS.md                    (Custom hooks)
    ├── SERVICES.md                 (API services)
    ├── DIALOGS.md                  (Dialog components)
    └── UTILITIES.md                (Utils, types, styles)
```

**Updated FRONTEND.md becomes a router:**

```markdown
# Frontend Keywords Index

> **Load specific sub-file for details:**

## Quick Routing

| What You Need | Load This File |
|---------------|----------------|
| React components | [frontend/COMPONENTS.md](frontend/COMPONENTS.md) |
| Custom hooks | [frontend/HOOKS.md](frontend/HOOKS.md) |
| Services & APIs | [frontend/SERVICES.md](frontend/SERVICES.md) |
| Dialogs & windows | [frontend/DIALOGS.md](frontend/DIALOGS.md) |
| Utils, types, styles | [frontend/UTILITIES.md](frontend/UTILITIES.md) |

**Line Count**: ~30 lines (router only)
```

### Sub-Folder Benefits

✅ Keeps all files under 500 lines
✅ Maintains fast loading
✅ Clear hierarchical organization
✅ Scalable indefinitely
✅ Two-level routing (domain → sub-domain)

### Implementation Trigger

**Create sub-folders when:**
1. Any domain file > 450 lines (yellow alert)
2. Any domain file > 500 lines (red alert - must split)
3. Clear logical sub-domains exist

**Current Status (2026-02-20):**
- ✅ BACKEND.md: 350 lines (OK)
- ⚠️ FRONTEND.md: 550 lines (Consider splitting soon)
- ✅ DOCUMENTATION.md: 220 lines (OK)
- ✅ HOW_TO.md: 370 lines (OK)

**Recommendation:**
- Monitor FRONTEND.md growth
- Consider splitting if it reaches 600+ lines
- Split into components, hooks, services, dialogs, utilities

---

**Status**: ✅ Routing system implemented
**Next Steps**: Monitor file sizes, implement sub-folders if needed
**Review Date**: 2026-05-01 (quarterly review)

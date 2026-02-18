# Feature Documentation Index

**Project:** D3dxSkinManager
**Last Updated:** 2026-02-17

---

## Overview

This folder contains detailed documentation for each feature in D3dxSkinManager. Each feature document provides:

- Purpose and use cases
- Implementation details
- API reference
- Usage examples
- Related files and line numbers

---

## Feature Status Legend

- ✅ **Complete** - Fully implemented and tested
- ⏳ **In Progress** - Currently being developed
- 📋 **Planned** - Scheduled for future development
- ❌ **Not Planned** - Not on roadmap

---

## Core Features (v1.0)

### ✅ Mod Listing
**Status:** Complete
**File:** TBD (create when feature mature)

**Description:** Display all mods in a table with sorting and filtering.

**Key Components:**
- Backend: `ModService.GetAllModsAsync()` → [ModService.cs:47](../../D3dxSkinManager/Services/ModService.cs#L47)
- Frontend: `App.tsx` mod table → [App.tsx:188](../../D3dxSkinManager.Client/src/App.tsx#L188)
- IPC: `GET_ALL_MODS` message type

---

### ✅ Load/Unload Mods
**Status:** Complete (backend), UI pending
**File:** TBD

**Description:** Activate or deactivate mods by updating database state.

**Key Components:**
- Backend:
  - `ModService.LoadModAsync()` → [ModService.cs:66](../../D3dxSkinManager/Services/ModService.cs#L66)
  - `ModService.UnloadModAsync()` → [ModService.cs:111](../../D3dxSkinManager/Services/ModService.cs#L111)
- Frontend: Load/Unload buttons → [App.tsx:42](../../D3dxSkinManager.Client/src/App.tsx#L42)
- IPC: `LOAD_MOD`, `UNLOAD_MOD` message types

---

### ✅ SQLite Database Storage
**Status:** Complete
**File:** TBD

**Description:** Store mod metadata in SQLite database with indexed queries.

**Key Components:**
- Schema: `InitializeDatabaseAsync()` → [ModService.cs:24](../../D3dxSkinManager/Services/ModService.cs#L24)
- Connection: SQLite connection string
- Tables: Mods (SHA, ObjectName, Name, etc.)
- Indexes: ObjectName, IsLoaded

---

### ✅ IPC Communication
**Status:** Complete
**File:** TBD

**Description:** JSON-based message passing between React frontend and C# backend.

**Key Components:**
- Backend: `OnWebMessageReceived()` → [Program.cs:41](../../D3dxSkinManager/Program.cs#L41)
- Frontend: `photinoService` → [photino.ts:24](../../D3dxSkinManager.Client/src/services/photino.ts#L24)
- Protocol: PhotinoMessage → PhotinoResponse

---

## Planned Features (v1.1+)

### ⏳ Mod Import
**Status:** Planned Phase 2
**File:** TBD (create when implemented)

**Description:** Import new mods from archive files (7z, zip, rar).

**Planned Components:**
- SHA256 calculation
- Archive extraction
- Metadata reading
- Thumbnail generation
- File copying

**See:** [ORIGINAL_COMPARISON.md](../core/ORIGINAL_COMPARISON.md#feature-parity-matrix)

---

### ⏳ Classification System
**Status:** Planned Phase 2
**File:** TBD

**Description:** Automatically detect mod's object name using wildcard patterns.

**Planned Components:**
- Pattern matching engine
- Rule-based classification
- Manual override
- Custom rules

---

### ⏳ Image Previews
**Status:** Planned Phase 2
**File:** TBD

**Description:** Display thumbnail and full-size preview images for mods.

**Planned Components:**
- Thumbnail viewer
- Full preview modal
- Image gallery
- Screenshot capture (future)

---

### 📋 Mod Warehouse
**Status:** Planned Phase 3
**File:** TBD

**Description:** Browse, download, and upload mods from online repository.

**Planned Components:**
- Mod browser UI
- Search and filter
- Download manager
- Upload functionality (future)

---

### 📋 Settings Page
**Status:** Planned Phase 2
**File:** TBD

**Description:** Configure application settings and preferences.

**Planned Settings:**
- Game directory path
- Mod storage path
- Auto-load on startup
- Theme selection
- Language (future)

---

### 📋 Multi-User Profiles
**Status:** Planned Phase 3
**File:** TBD

**Description:** Support multiple user profiles with separate mod lists.

**Planned Components:**
- Profile management
- Profile switching
- Per-profile settings
- Data isolation

---

### 📋 Plugin System
**Status:** Planned Phase 4
**File:** TBD

**Description:** Extensibility through C# plugins.

**Planned Components:**
- Plugin interface
- Plugin loader
- Plugin manager UI
- Plugin marketplace (future)

---

## Feature Documentation Template

When creating feature documentation, use this template:

```markdown
# Feature Name

**Status:** ✅ Complete / ⏳ In Progress / 📋 Planned
**Phase:** 1, 2, 3, or 4
**Location:** `path/to/main/file.cs:123`

## Purpose

One paragraph describing what this feature does and why it exists.

## Use Cases

- **Use Case 1:** Description
- **Use Case 2:** Description

## Implementation

### Backend

**Service:** `ServiceName.MethodName()`
- File: `path/to/file.cs:123`
- Async: Yes/No
- Returns: Type description

**Database:**
- Tables affected: TableName
- Queries: Description
- Transactions: Yes/No

### Frontend

**Component:** `ComponentName`
- File: `path/to/Component.tsx:45`
- Props: List of props
- State: List of state variables

**API Calls:**
- `methodName()` → Backend service

### IPC

**Message Type:** `MESSAGE_TYPE`
- Payload: `{ field: type }`
- Response: `{ field: type }`

## Usage Examples

### Backend Usage

```csharp
// Example C# code
var result = await service.MethodAsync(param);
```

### Frontend Usage

```typescript
// Example TypeScript code
const result = await service.method(param);
```

## API Reference

### Backend API

#### MethodName(param: Type): Promise<ReturnType>

**Description:** What this method does

**Parameters:**
- `param` (Type) - Description

**Returns:** ReturnType - Description

**Throws:**
- `ExceptionType` - When this happens

### Frontend API

#### method(param: Type): Promise<ReturnType>

**Description:** What this method does

**Example:**
```typescript
const result = await service.method('value');
```

## Related Files

- `backend/file.cs:123` - Description
- `frontend/file.tsx:45` - Description
- `docs/other-doc.md` - Related documentation

## Related Features

- [Feature Name](FEATURE_NAME.md) - How they relate

## Troubleshooting

### Common Issue 1

**Problem:** Description

**Solution:** How to fix

### Common Issue 2

**Problem:** Description

**Solution:** How to fix

## Future Enhancements

- Enhancement 1
- Enhancement 2

## Change History

- **2026-02-17:** Initial implementation
- **2026-MM-DD:** Added XYZ

---

*Last updated: 2026-MM-DD*
```

---

## Creating Feature Documentation

### When to Create

Create feature documentation when:
- ✅ Feature is complete (or mostly complete)
- ✅ Feature is complex (>200 LOC)
- ✅ Feature will be referenced often
- ✅ Feature has public API

### Where to Place

```
docs/features/
├── README.md                    # This file
├── MOD_IMPORT.md                # Individual feature
├── CLASSIFICATION.md
├── IMAGE_PREVIEWS.md
└── ...
```

### Naming Convention

- **SCREAMING_SNAKE_CASE.md** for feature files
- Match feature name exactly
- Use underscores to separate words

**Examples:**
```
✅ MOD_IMPORT.md
✅ CLASSIFICATION_SYSTEM.md
✅ IMAGE_PREVIEWS.md
❌ mod-import.md          (wrong case)
❌ ModImport.md           (wrong case)
❌ Import.md              (too vague)
```

---

## Maintaining Feature Docs

### Update Triggers

Update feature documentation when:
- ✅ Implementation changes significantly
- ✅ API changes (parameters, return types)
- ✅ New use cases discovered
- ✅ Bug fixes that affect behavior
- ✅ Performance improvements

### Don't Update For

- ❌ Minor bug fixes (internal)
- ❌ Code refactoring (if API unchanged)
- ❌ Comment changes
- ❌ Formatting changes

---

## Quick Navigation

### By Status

**Completed Features:**
- [Mod Listing](#-mod-listing)
- [Load/Unload Mods](#-loadunload-mods)
- [SQLite Storage](#-sqlite-database-storage)
- [IPC Communication](#-ipc-communication)

**In Progress:**
- (None currently)

**Planned:**
- [Mod Import](#-mod-import)
- [Classification System](#-classification-system)
- [Image Previews](#-image-previews)
- [Mod Warehouse](#-mod-warehouse)
- [Settings Page](#-settings-page)
- [Multi-User Profiles](#-multi-user-profiles)
- [Plugin System](#-plugin-system)

### By Phase

**Phase 1 (v1.0) - Current:**
- Mod Listing ✅
- Load/Unload Mods ✅
- SQLite Storage ✅
- IPC Communication ✅

**Phase 2 (v1.1) - Next:**
- Mod Import ⏳
- Classification System ⏳
- Image Previews ⏳
- Settings Page ⏳

**Phase 3 (v2.0) - Mid-term:**
- Mod Warehouse 📋
- Multi-User Profiles 📋

**Phase 4 (v3.0+) - Long-term:**
- Plugin System 📋

---

## Related Documentation

- [ORIGINAL_COMPARISON.md](../core/ORIGINAL_COMPARISON.md) - Feature parity with Python version
- [ARCHITECTURE.md](../core/ARCHITECTURE.md) - System architecture
- [MIGRATION_GUIDE.md](../core/MIGRATION_GUIDE.md) - Porting features from Python
- [WORKFLOWS.md](../ai-assistant/WORKFLOWS.md) - How to implement features
- [CHANGELOG.md](../CHANGELOG.md) - What changed when

---

## For AI Assistants

When documenting a feature:

1. **Read template above**
2. **Fill in all sections** (or mark TBD)
3. **Include file paths and line numbers**
4. **Add code examples**
5. **Update this README** with link to new feature doc
6. **Update KEYWORDS_INDEX.md** with new file
7. **Update CHANGELOG.md** with doc creation

**Remember:** Good documentation helps future AI sessions (and humans) understand the codebase quickly.

---

*This index is maintained by developers and AI assistants.*

*Last updated: 2026-02-17*

# KEYWORDS_INDEX Management Guide

**Purpose**: Keep the KEYWORDS_INDEX.md file effective, searchable, and maintainable.

**Target**: Main KEYWORDS_INDEX.md should be **under 500 lines** total.

---

## Core Principles

### 1. KEYWORDS_INDEX.md = Quick Lookup Only
- **Purpose**: Fast file/class/concept lookup for AI RAG systems
- **Target Audience**: AI assistants searching for "where is X?"
- **Size Limit**: Maximum 500 lines
- **Content**: File paths and primary entry points only
- **Format**: One-line entries with minimal detail

### 2. Detailed Documentation = Separate Files
- **Location**: Actual architecture/feature docs
- **Purpose**: Complete technical documentation
- **No Size Limit**: Can be as detailed as needed
- **Examples**: `architecture/CURRENT_ARCHITECTURE.md`, `features/PLUGINS.md`

---

## Problems with Current Index (1096 lines)

### ❌ Too Much Detail:
```markdown
### Facades ⭐ NEW
- **IModFacade** (interface) → `D3dxSkinManager/Facades/IModFacade.cs`
  - GetAllModsAsync → `:10`
  - LoadModAsync → `:11`
  - UnloadModAsync → `:12`
  - ImportModAsync → `:13`
  - SearchModsAsync → `:14`
```
**Problem**: Listing every method with line numbers is excessive

### ✅ Better Approach:
```markdown
### Facades
- **ModFacade** → `Modules/Mods/ModFacade.cs` (mod operations)
- **SettingsFacade** → `Modules/Settings/SettingsFacade.cs` (settings management)
- **MigrationFacade** → `Modules/Migration/MigrationFacade.cs` (Python migration)
```
**Why Better**: One line per item, just enough to find the file

---

## Rules for KEYWORDS_INDEX.md

### ✅ DO Include:
1. **File paths only** - No line numbers for every method
2. **Primary entry points** - Main classes/files
3. **Module structure** - High-level organization
4. **Quick navigation** - Jump links between sections
5. **Brief descriptions** - 2-3 words max per item

### ❌ DO NOT Include:
1. **Method listings** - Move to architecture docs
2. **Line numbers for every method** - Only for key entry points
3. **Detailed explanations** - Move to feature docs
4. **Code examples** - Move to architecture docs
5. **Historical information** - Move to CHANGELOG

---

## Index Entry Formats

### Template for Backend Classes:
```markdown
### [Module Name]
- **ClassName** → `path/to/File.cs` (purpose)
- **AnotherClass** → `path/to/Another.cs` (purpose)
```

### Template for Frontend Components:
```markdown
### [Component Category]
- **ComponentName** → `src/path/Component.tsx` (purpose)
- **AnotherComponent** → `src/path/Another.tsx` (purpose)
```

### Example - Good (Concise):
```markdown
## Backend Services

### Core Services
- **FileService** → `Modules/Core/Services/FileService.cs` (file operations, archive extraction)
- **ImageService** → `Modules/Core/Services/ImageService.cs` (image processing, thumbnails)
- **LogHelper** → `Modules/Core/Services/LogHelper.cs` (logging infrastructure)

### Mod Services
- **ModManagementService** → `Modules/Mods/Services/ModManagementService.cs` (CRUD operations)
- **ModFileService** → `Modules/Mods/Services/ModFileService.cs` (load/unload/cache)
- **ModImportService** → `Modules/Mods/Services/ModImportService.cs` (import workflow)
```

### Example - Bad (Too Detailed):
```markdown
## Backend Services

### Core Services

- **IFileService** (interface) → `Modules/Core/Services/FileService.cs:11-22`
- **FileService** → `Modules/Core/Services/FileService.cs:24`
  - CalculateSha256Async → `:26-48`
  - ExtractArchiveAsync → `:51-90`
  - CopyDirectoryAsync → `:93-120`
  - DeleteDirectoryAsync → `:123-137`
  [20 more lines...]
```

---

## Size Management Strategy

### Two-Tier System:

**Tier 1: KEYWORDS_INDEX.md** (< 500 lines)
- Location: `docs/KEYWORDS_INDEX.md`
- Content: Quick lookup entries only
- One line per item
- Update: When new modules/services added

**Tier 2: Module Reference Files** (No size limit)
- Location: `docs/architecture/MODULE_REFERENCE_[NAME].md`
- Content: Detailed class/method information
- Update: When implementation changes

### Example Split:

**KEYWORDS_INDEX.md:**
```markdown
### Mods Module
- **ModFacade** → `Modules/Mods/ModFacade.cs` (IPC entry point)
- **ModManagementService** → `Modules/Mods/Services/ModManagementService.cs` (CRUD operations)
- **ModFileService** → `Modules/Mods/Services/ModFileService.cs` (file operations)

**Details**: [architecture/MODULE_REFERENCE_MODS.md](architecture/MODULE_REFERENCE_MODS.md)
```

**architecture/MODULE_REFERENCE_MODS.md:**
```markdown
# Mods Module Reference

## ModFacade
**File**: `Modules/Mods/ModFacade.cs`

### Methods:
- GetAllModsAsync() : line 45
- LoadModAsync(sha) : line 67
- UnloadModAsync(sha) : line 89
[Detailed documentation...]
```

---

## Maintenance Schedule

### Quarterly Review (Every 3 months):
1. **Check line count**: `wc -l docs/KEYWORDS_INDEX.md`
2. **If > 500 lines**: Extract detailed sections to MODULE_REFERENCE files
3. **Update links**: Ensure all references work
4. **Verify accuracy**: Check file paths are current

### When Adding New Entry:
1. **Check size first**: `wc -l docs/KEYWORDS_INDEX.md`
2. **If > 450 lines**: Consider if this belongs in MODULE_REFERENCE instead
3. **Use one-line format**: `ClassName → path/to/File.cs (purpose)`
4. **No method listings**: Just the file path and brief purpose

---

## Cleanup Process

### Step 1: Identify Sections to Extract
Look for sections with:
- Multiple sub-items per entry (> 5 lines per class)
- Method listings with line numbers
- Detailed descriptions

### Step 2: Create MODULE_REFERENCE File
```bash
touch docs/architecture/MODULE_REFERENCE_[NAME].md
```

### Step 3: Move Detailed Content
Move the detailed method listings and descriptions to the reference file.

### Step 4: Update Index with Summary + Link
Replace detailed section with one-line entries and link to reference.

---

## File Structure

```
docs/
├── KEYWORDS_INDEX.md                      ← Quick lookup (< 500 lines)
│
└── architecture/
    ├── CURRENT_ARCHITECTURE.md            ← System overview
    ├── MODULE_REFERENCE_CORE.md           ← Core module details
    ├── MODULE_REFERENCE_MODS.md           ← Mods module details
    ├── MODULE_REFERENCE_MIGRATION.md      ← Migration module details
    └── MODULE_REFERENCE_FRONTEND.md       ← Frontend components details
```

---

## Decision Tree

```
Need to Add Entry to Index
    │
    ├─ Is it a new file/class?
    │   └─ YES → Add one-line entry to KEYWORDS_INDEX
    │
    ├─ Is it detailed info (methods, line numbers)?
    │   └─ YES → Add to MODULE_REFERENCE file
    │
    └─ KEYWORDS_INDEX > 500 lines?
        └─ YES → Extract details to MODULE_REFERENCE
```

---

## Quick Reference

| Scenario | Action |
|----------|--------|
| New class/service | Add one-line entry to KEYWORDS_INDEX |
| New method | Add to MODULE_REFERENCE file only |
| Index > 450 lines | Start planning cleanup |
| Index > 500 lines | Immediate cleanup required |
| Quarterly review | Check size and accuracy |

---

## For AI Assistants

### Before Adding to KEYWORDS_INDEX:
1. Check current line count: `wc -l docs/KEYWORDS_INDEX.md`
2. If > 450 lines: Consider MODULE_REFERENCE file
3. Use one-line format: `Name → path (purpose)`
4. NO method listings in main index

### When Searching:
1. **Quick lookup**: Check KEYWORDS_INDEX.md
2. **Detailed info**: Check architecture/MODULE_REFERENCE_*.md
3. **Code details**: Read actual source file

### After Session:
1. Verify KEYWORDS_INDEX.md is < 500 lines
2. Check all links work
3. Update MODULE_REFERENCE files if needed

---

## Examples

### ✅ Good Entry (1 line):
```markdown
- **ModFileService** → `Modules/Mods/Services/ModFileService.cs` (load/unload/cache operations)
```

### ❌ Bad Entry (Too Detailed):
```markdown
- **IModFileService** (interface) → `Modules/Mods/Services/ModFileService.cs:23-48`

- **ModFileService** → `Modules/Mods/Services/ModFileService.cs:60`
  - Constructor → `:68-81`
  - LoadAsync → `:89-130`
  - UnloadAsync → `:135-163`
  - DeleteAsync → `:168-224`
  [10 more methods...]
```

---

**Last Updated**: 2026-02-21
**Routing System**: Implemented (v4.0)
**Status**:
- ✅ KEYWORDS_INDEX.md: 179 lines (routing hub, target < 200)
- ✅ BACKEND.md: 463 lines (target < 500)
- ✅ DOCUMENTATION.md: 275 lines (target < 500)
- ⚠️ FRONTEND.md: 712 lines (OVER target, needs split at 750+)
- ⚠️ HOW_TO.md: 596 lines (OVER target, needs split at 650+)
**Next Actions**:
- Monitor FRONTEND.md (currently 712 lines, consider splitting at 750)
- Monitor HOW_TO.md (currently 596 lines, consider splitting at 650)
**Next Review**: 2026-03-01
